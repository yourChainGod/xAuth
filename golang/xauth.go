// Package main 提供 Twitter / X OAuth 认证功能。
package main

import (
	"fmt"
	"net/http"
	"net/http/cookiejar"
	"net/url"
	"regexp"
	"strings"
	"time"

	"github.com/go-resty/resty/v2"
	"github.com/tidwall/gjson"
)

// 常量定义。
const (
	TwitterAuthority        = "twitter.com"
	TwitterOrigin           = "https://twitter.com"
	TwitterAPIBase          = "https://twitter.com/i/api/2"
	TwitterAuthenticateURL  = "https://api.x.com/oauth/authenticate"
	TwitterOAuth1Authorize  = "https://x.com/oauth/authorize"
	UserAgent               = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36"
	Authorization          = "Bearer AAAAAAAAAAAAAAAAAAAAANRILgAAAAAAnNwIzUejRCOuH5E6I8xnZz4puTs%3D1Zv7ttfk8LF81IUq16cHjhLTvJu4FA33AGWWjCpTnA"
	MaxRetries              = 3
	DefaultTimeout          = 10 * time.Second
	RetryInterval           = 1 * time.Second
)

// AccountState 定义账户状态映射。
var AccountState = map[int64]string{
	32:  "Bad Token",
	64:  "SUSPENDED",
	141: "SUSPENDED",
	326: "LOCKED",
}

var authenticityPatterns = []*regexp.Regexp{
	regexp.MustCompile(`name="authenticity_token"\s+value="([^"]+)"`),
	regexp.MustCompile(`name="authenticity_token"\s+type="hidden"\s+value="([^"]+)"`),
}

var oauthVerifierPattern = regexp.MustCompile(`oauth_verifier=([^"&]+)`)

// XAuthOptions 允许测试时覆盖端点与超时参数。
type XAuthOptions struct {
	AuthenticateURL   string
	OAuth1AuthorizeURL string
	APIBase           string
	UserAgent         string
	Authorization     string
	Timeout           time.Duration
	RetryInterval     time.Duration
	MaxRetries        int
}

// XAuth 提供 Twitter OAuth 认证功能。
type XAuth struct {
	client             *resty.Client
	client2            *resty.Client
	authToken          string
	authenticateURL    string
	oauth1AuthorizeURL string
	apiBase            string
	maxRetries         int
	retryInterval      time.Duration
	cookieJar          http.CookieJar
}

// NewTwitter 创建新的 XAuth 实例。
func NewTwitter(authToken string) *XAuth {
	return NewTwitterWithOptions(authToken, XAuthOptions{})
}

// NewTwitterWithOptions 创建支持端点覆写的 XAuth 实例。
func NewTwitterWithOptions(authToken string, opts XAuthOptions) *XAuth {
	if authToken == "" {
		panic("authToken不能为空")
	}

	if opts.AuthenticateURL == "" {
		opts.AuthenticateURL = TwitterAuthenticateURL
	}
	if opts.OAuth1AuthorizeURL == "" {
		opts.OAuth1AuthorizeURL = TwitterOAuth1Authorize
	}
	if opts.APIBase == "" {
		opts.APIBase = TwitterAPIBase
	}
	if opts.UserAgent == "" {
		opts.UserAgent = UserAgent
	}
	if opts.Authorization == "" {
		opts.Authorization = Authorization
	}
	if opts.Timeout == 0 {
		opts.Timeout = DefaultTimeout
	}
	if opts.RetryInterval == 0 {
		opts.RetryInterval = RetryInterval
	}
	if opts.MaxRetries == 0 {
		opts.MaxRetries = MaxRetries
	}

	jar, err := cookiejar.New(nil)
	if err != nil {
		panic(fmt.Sprintf("创建 cookie jar 失败: %v", err))
	}

	baseHeaders := map[string]string{
		"user-agent": opts.UserAgent,
	}

	client := resty.New().
		SetHeaders(baseHeaders).
		SetHeaders(map[string]string{
			"authority":             TwitterAuthority,
			"origin":                TwitterOrigin,
			"x-twitter-auth-type":   "OAuth2Session",
			"x-twitter-active-user": "yes",
			"authorization":         opts.Authorization,
		}).
		SetRetryCount(0).
		SetTimeout(opts.Timeout).
		SetCookieJar(jar)

	client2 := resty.New().
		SetHeaders(baseHeaders).
		SetRetryCount(0).
		SetTimeout(opts.Timeout).
		SetCookieJar(jar)

	x := &XAuth{
		client:             client,
		client2:            client2,
		authToken:          authToken,
		authenticateURL:    opts.AuthenticateURL,
		oauth1AuthorizeURL: opts.OAuth1AuthorizeURL,
		apiBase:            opts.APIBase,
		maxRetries:         opts.MaxRetries,
		retryInterval:      opts.RetryInterval,
		cookieJar:          jar,
	}

	x.seedAuthTokenCookies()
	return x
}

func (x *XAuth) seedAuthTokenCookies() {
	cookie := &http.Cookie{Name: "auth_token", Value: x.authToken, Path: "/"}
	seen := map[string]bool{}
	for _, rawURL := range []string{
		"https://x.com",
		"https://api.x.com",
		"https://twitter.com",
		x.authenticateURL,
		x.oauth1AuthorizeURL,
		x.apiBase,
	} {
		u, err := url.Parse(rawURL)
		if err != nil || u.Scheme == "" || u.Host == "" {
			continue
		}
		key := u.Scheme + "://" + u.Host
		if seen[key] {
			continue
		}
		seen[key] = true
		x.cookieJar.SetCookies(u, []*http.Cookie{cookie})
	}
}

func (x *XAuth) csrfToken() string {
	seen := map[string]bool{}
	for _, rawURL := range []string{x.apiBase, x.oauth1AuthorizeURL, x.authenticateURL, "https://x.com", "https://api.x.com", "https://twitter.com"} {
		u, err := url.Parse(rawURL)
		if err != nil || u.Scheme == "" || u.Host == "" {
			continue
		}
		key := u.Scheme + "://" + u.Host
		if seen[key] {
			continue
		}
		seen[key] = true
		for _, cookie := range x.cookieJar.Cookies(u) {
			if cookie.Name == "ct0" && cookie.Value != "" {
				return cookie.Value
			}
		}
	}
	return ""
}

func (x *XAuth) doWithRetry(request func() (*resty.Response, error)) (*resty.Response, error) {
	var lastResp *resty.Response

	for attempt := 0; attempt <= x.maxRetries; attempt++ {
		resp, err := request()
		if err != nil {
			return nil, err
		}
		lastResp = resp
		if resp.StatusCode() != http.StatusTooManyRequests {
			return resp, nil
		}
		if attempt == x.maxRetries {
			break
		}
		time.Sleep(x.retryInterval)
	}

	if lastResp == nil {
		return nil, fmt.Errorf("请求失败，未收到响应")
	}
	return lastResp, nil
}

func extractFirst(content string, patterns []*regexp.Regexp) string {
	for _, pattern := range patterns {
		if match := pattern.FindStringSubmatch(content); len(match) > 1 && match[1] != "" {
			return match[1]
		}
	}
	return ""
}

// GetTwitterToken 获取 Twitter 认证 token。
func (x *XAuth) GetTwitterToken(oauthToken string) (string, error) {
	if oauthToken == "" {
		return "", fmt.Errorf("oauth_token不能为空")
	}

	resp, err := x.doWithRetry(func() (*resty.Response, error) {
		return x.client2.R().SetQueryParams(map[string]string{
			"oauth_token": oauthToken,
		}).Get(x.authenticateURL)
	})
	if err != nil {
		return "", fmt.Errorf("请求 Twitter 认证失败: %w", err)
	}
	if resp.StatusCode() == http.StatusTooManyRequests {
		return "", fmt.Errorf("请求频率限制，重试失败")
	}

	content := resp.String()
	if !strings.Contains(content, "authenticity_token") {
		if strings.Contains(content, "The request token for this page is invalid") {
			return "", fmt.Errorf("请求oauth_token无效")
		}
		return "", fmt.Errorf("响应中未找到authenticity_token")
	}

	token := extractFirst(content, authenticityPatterns)
	if token == "" {
		return "", fmt.Errorf("获取到的authenticity_token为空")
	}
	return token, nil
}

// Oauth1 执行 OAuth1 认证流程。
func (x *XAuth) Oauth1(oauthToken string) (string, error) {
	authenticityToken, err := x.GetTwitterToken(oauthToken)
	if err != nil {
		return "", fmt.Errorf("获取 Twitter token 失败: %w", err)
	}

	resp, err := x.doWithRetry(func() (*resty.Response, error) {
		return x.client2.R().
			SetHeader("Content-Type", "application/x-www-form-urlencoded").
			SetFormData(map[string]string{
				"authenticity_token": authenticityToken,
				"oauth_token":        oauthToken,
			}).
			Post(x.oauth1AuthorizeURL)
	})
	if err != nil {
		return "", fmt.Errorf("OAuth1认证请求失败: %w", err)
	}
	if resp.StatusCode() == http.StatusTooManyRequests {
		return "", fmt.Errorf("请求频率限制，重试失败")
	}

	content := resp.String()
	if !strings.Contains(content, "oauth_verifier") {
		if strings.Contains(content, "This account is suspended.") {
			return "", fmt.Errorf("该账户已被封禁")
		}
		return "", fmt.Errorf("未找到oauth_verifier")
	}

	match := oauthVerifierPattern.FindStringSubmatch(content)
	if len(match) < 2 || match[1] == "" {
		return "", fmt.Errorf("获取到的oauth_verifier为空")
	}
	return match[1], nil
}

// GetAuthCode 获取认证码。
func (x *XAuth) GetAuthCode(params map[string]string) (string, error) {
	if len(params) == 0 {
		return "", fmt.Errorf("参数不能为空")
	}

	csrfRetried := false
	for attempts := 0; attempts < 2; attempts++ {
		resp, err := x.doWithRetry(func() (*resty.Response, error) {
			req := x.client.R().SetQueryParams(params)
			if csrf := x.csrfToken(); csrf != "" {
				req.SetHeader("x-csrf-token", csrf)
			}
			return req.Get(x.apiBase + "/oauth2/authorize")
		})
		if err != nil {
			return "", fmt.Errorf("请求auth_code失败: %w", err)
		}
		if resp.StatusCode() == http.StatusTooManyRequests {
			return "", fmt.Errorf("请求频率限制，重试失败")
		}

		body := resp.String()
		code := gjson.Get(body, "code")
		if code.Exists() && code.Int() == 353 && !csrfRetried {
			csrf := x.csrfToken()
			if csrf == "" {
				return "", fmt.Errorf("未找到ct0 cookie")
			}
			x.client.SetHeader("x-csrf-token", csrf)
			csrfRetried = true
			continue
		}

		if errCode := gjson.Get(body, "errors.0.code"); errCode.Exists() {
			if state, exists := AccountState[errCode.Int()]; exists {
				return "", fmt.Errorf("token状态错误: %s", state)
			}
		}

		authCode := gjson.Get(body, "auth_code")
		if !authCode.Exists() || authCode.String() == "" {
			return "", fmt.Errorf("响应中未找到auth_code")
		}
		return authCode.String(), nil
	}

	return "", fmt.Errorf("获取auth_code失败")
}

// Oauth2 执行 OAuth2 认证流程。
func (x *XAuth) Oauth2(params map[string]string) (string, error) {
	authCode, err := x.GetAuthCode(params)
	if err != nil {
		return "", fmt.Errorf("获取auth_code失败: %w", err)
	}

	resp, err := x.doWithRetry(func() (*resty.Response, error) {
		req := x.client.R().
			SetHeader("Content-Type", "application/x-www-form-urlencoded").
			SetHeader("Accept", "application/json, text/plain, */*").
			SetFormData(map[string]string{
				"approval": "true",
				"code":     authCode,
			})
			if csrf := x.csrfToken(); csrf != "" {
				req.SetHeader("x-csrf-token", csrf)
			}
			return req.Post(x.apiBase + "/oauth2/authorize")
	})
	if err != nil {
		return "", fmt.Errorf("OAuth2认证请求失败: %w", err)
	}
	if resp.StatusCode() == http.StatusTooManyRequests {
		return "", fmt.Errorf("请求频率限制，重试失败")
	}

	body := resp.String()
	if !strings.Contains(body, "redirect_uri") && !gjson.Get(body, "redirect_uri").Exists() {
		return "", fmt.Errorf("响应中未找到redirect_uri")
	}
	return authCode, nil
}
