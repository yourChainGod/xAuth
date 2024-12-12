package main

import (
	"fmt"
	"net/http"
	"strings"
	"time"

	"github.com/go-resty/resty/v2"
	"github.com/tidwall/gjson"
)

const (
	twitterAuthority = "twitter.com"
	twitterOrigin    = "https://twitter.com"
	twitterAPIBase   = "https://twitter.com/i/api/2"
	userAgent        = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36"
	authorization    = "Bearer AAAAAAAAAAAAAAAAAAAAANRILgAAAAAAnNwIzUejRCOuH5E6I8xnZz4puTs%3D1Zv7ttfk8LF81IUq16cHjhLTvJu4FA33AGWWjCpTnA"
	maxRetries       = 3
	retryInterval    = 1 * time.Second
)

// AccountState 定义账户状态映射
var accountState = map[int64]string{
	32:  "Bad Token",
	64:  "SUSPENDED",
	141: "SUSPENDED",
	326: "LOCKED",
}

type XAuth struct {
	client    *resty.Client
	client2   *resty.Client
	authToken string
}

// NewTwitter 创建新的XAuth实例
func NewTwitter(authToken string) *XAuth {
	baseHeaders := map[string]string{
		"user-agent": userAgent,
	}

	// 创建主客户端
	client := resty.New().
		SetHeaders(baseHeaders).
		SetHeaders(map[string]string{
			"authority":             twitterAuthority,
			"origin":                twitterOrigin,
			"x-twitter-auth-type":   "OAuth2Session",
			"x-twitter-active-user": "yes",
			"authorization":         authorization,
		}).
		SetRetryCount(maxRetries).
		SetRetryWaitTime(retryInterval).
		SetTimeout(10 * time.Second).
		SetCookie(&http.Cookie{Name: "auth_token", Value: authToken})

	// 创建次要客户端
	client2 := resty.New().
		SetHeaders(baseHeaders).
		SetRetryCount(maxRetries).
		SetRetryWaitTime(retryInterval).
		SetTimeout(10 * time.Second).
		SetCookie(&http.Cookie{Name: "auth_token", Value: authToken})

	return &XAuth{
		client:    client,
		client2:   client2,
		authToken: authToken,
	}
}

func (x *XAuth) get_twitter_token(oauth_token string) (string, error) {
	if oauth_token == "" {
		return "", fmt.Errorf("oauth_token不能为空")
	}

	params := map[string]string{
		"oauth_token": oauth_token,
	}

	resp, err := x.client2.R().SetQueryParams(params).
		Get("https://api.x.com/oauth/authenticate")
	if err != nil {
		return "", fmt.Errorf("请求Twitter认证失败: %w", err)
	}

	if !strings.Contains(resp.String(), "authenticity_token") {
		if strings.Contains(resp.String(), "The request token for this page is invalid") {
			return "", fmt.Errorf("请求oauth_token无效")
		}
		return "", fmt.Errorf("响应中未找到authenticity_token")
	}

	parts := strings.Split(resp.String(), "name=\"authenticity_token\" value=\"")
	if len(parts) < 2 {
		parts = strings.Split(resp.String(), "name=\"authenticity_token\" type=\"hidden\" value=\"")
		if len(parts) < 2 {
			return "", fmt.Errorf("authenticity_token格式错误")
		}
	}

	token := strings.Split(parts[1], "\"")[0]
	if token == "" {
		return "", fmt.Errorf("获取到的authenticity_token为空")
	}

	return token, nil
}

func (x *XAuth) Oauth1(oauth_token string) (string, error) {
	authenticityToken, err := x.get_twitter_token(oauth_token)
	if err != nil {
		return "", fmt.Errorf("获取Twitter token失败: %w", err)
	}

	data := map[string]string{
		"authenticity_token": authenticityToken,
		"oauth_token":        oauth_token,
	}

	resp, err := x.client2.R().SetFormData(data).
		Post("https://x.com/oauth/authorize")
	if err != nil {
		return "", fmt.Errorf("OAuth1认证请求失败: %w", err)
	}

	if !strings.Contains(resp.String(), "oauth_verifier") {
		if strings.Contains(resp.String(), "This account is suspended.") {
			return "", fmt.Errorf("该账户已被封禁")
		}
	}

	parts := strings.Split(resp.String(), "oauth_verifier=")
	if len(parts) < 2 {
		return "", fmt.Errorf("oauth_verifier格式错误")
	}

	verifier := strings.Split(parts[1], "\"")[0]
	if verifier == "" {
		return "", fmt.Errorf("获取到的oauth_verifier为空")
	}

	return verifier, nil
}

func (x *XAuth) get_auth_code(params map[string]string) (string, error) {
	if len(params) == 0 {
		return "", fmt.Errorf("参数不能为空")
	}

	resp, err := x.client.R().
		SetQueryParams(params).
		Get(twitterAPIBase + "/oauth2/authorize")
	if err != nil {
		return "", fmt.Errorf("请求auth_code失败: %w", err)
	}

	// 处理请求限制
	if resp.StatusCode() == http.StatusTooManyRequests {
		time.Sleep(retryInterval)
		return x.get_auth_code(params)
	}

	// 处理CSRF token
	code := gjson.Get(resp.String(), "code")
	if code.Exists() && code.Int() == 353 {
		for _, cookie := range resp.Cookies() {
			if cookie.Name == "ct0" {
				x.client.SetHeader("x-csrf-token", cookie.Value)
				return x.get_auth_code(params)
			}
		}
		return "", fmt.Errorf("未找到ct0 cookie")
	}

	// 检查账户状态
	if errCode := gjson.Get(resp.String(), "errors.0.code"); errCode.Exists() {
		if state, exists := accountState[errCode.Int()]; exists {
			return "", fmt.Errorf("token状态错误: %s", state)
		}
	}

	// 获取auth_code
	authCode := gjson.Get(resp.String(), "auth_code")
	if !authCode.Exists() {
		return "", fmt.Errorf("响应中未找到auth_code")
	}

	return authCode.String(), nil
}

func (x *XAuth) Oauth2(params map[string]string) (string, error) {
	authCode, err := x.get_auth_code(params)
	if err != nil {
		return "", fmt.Errorf("获取auth_code失败: %w", err)
	}

	data := map[string]string{
		"approval": "true",
		"code":     authCode,
	}

	resp, err := x.client.R().SetFormData(data).
		Post(twitterAPIBase + "/oauth2/authorize")
	if err != nil {
		return "", fmt.Errorf("OAuth2认证请求失败: %w", err)
	}

	// 处理请求限制
	if resp.StatusCode() == http.StatusTooManyRequests {
		time.Sleep(retryInterval)
		return x.Oauth2(params)
	}

	if !strings.Contains(resp.String(), "redirect_uri") {
		return "", fmt.Errorf("响应中未找到redirect_uri")
	}

	return authCode, nil
}
