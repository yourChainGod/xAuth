// Package main 提供Twitter OAuth认证功能
package main

import (
	"fmt"
	"net/http"
	"strings"
	"time"

	"github.com/go-resty/resty/v2"
	"github.com/tidwall/gjson"
)

// 常量定义
const (
	TwitterAuthority = "twitter.com"
	TwitterOrigin    = "https://twitter.com"
	TwitterAPIBase   = "https://twitter.com/i/api/2"
	UserAgent        = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36"
	Authorization    = "Bearer AAAAAAAAAAAAAAAAAAAAANRILgAAAAAAnNwIzUejRCOuH5E6I8xnZz4puTs%3D1Zv7ttfk8LF81IUq16cHjhLTvJu4FA33AGWWjCpTnA"
	MaxRetries       = 3
	RetryInterval    = 1 * time.Second
)

// AccountState 定义账户状态映射
var AccountState = map[int64]string{
	32:  "Bad Token",
	64:  "SUSPENDED",
	141: "SUSPENDED",
	326: "LOCKED",
}

// XAuth 提供Twitter OAuth认证功能
type XAuth struct {
	client    *resty.Client
	client2   *resty.Client
	authToken string
}

// NewTwitter 创建新的XAuth实例
func NewTwitter(authToken string) *XAuth {
	if authToken == "" {
		panic("authToken不能为空")
	}

	baseHeaders := map[string]string{
		"user-agent": UserAgent,
	}

	// 创建主客户端
	client := resty.New().
		SetHeaders(baseHeaders).
		SetHeaders(map[string]string{
			"authority":             TwitterAuthority,
			"origin":                TwitterOrigin,
			"x-twitter-auth-type":   "OAuth2Session",
			"x-twitter-active-user": "yes",
			"authorization":         Authorization,
		}).
		SetRetryCount(MaxRetries).
		SetRetryWaitTime(RetryInterval).
		SetTimeout(10 * time.Second).
		SetCookie(&http.Cookie{Name: "auth_token", Value: authToken})

	// 创建次要客户端
	client2 := resty.New().
		SetHeaders(baseHeaders).
		SetRetryCount(MaxRetries).
		SetRetryWaitTime(RetryInterval).
		SetTimeout(10 * time.Second).
		SetCookie(&http.Cookie{Name: "auth_token", Value: authToken})

	return &XAuth{
		client:    client,
		client2:   client2,
		authToken: authToken,
	}
}

// GetTwitterToken 获取Twitter认证token
func (x *XAuth) GetTwitterToken(oauthToken string) (string, error) {
	if oauthToken == "" {
		return "", fmt.Errorf("oauth_token不能为空")
	}

	params := map[string]string{
		"oauth_token": oauthToken,
	}

	resp, err := x.client2.R().SetQueryParams(params).
		Get("https://api.x.com/oauth/authenticate")
	if err != nil {
		return "", fmt.Errorf("请求Twitter认证失败: %w", err)
	}

	// 处理请求限制
	if resp.StatusCode() == http.StatusTooManyRequests {
		time.Sleep(RetryInterval)
		return x.GetTwitterToken(oauthToken)
	}

	if !strings.Contains(resp.String(), "authenticity_token") {
		if strings.Contains(resp.String(), "The request token for this page is invalid") {
			return "", fmt.Errorf("请求oauth_token无效")
		}
		return "", fmt.Errorf("响应中未找到authenticity_token")
	}

	// 尝试两种可能的token格式
	var token string
	tokenMarkers := []string{
		"name=\"authenticity_token\" value=\"",
		"name=\"authenticity_token\" type=\"hidden\" value=\"",
	}

	for _, marker := range tokenMarkers {
		parts := strings.Split(resp.String(), marker)
		if len(parts) >= 2 {
			token = strings.Split(parts[1], "\"")[0]
			break
		}
	}

	if token == "" {
		return "", fmt.Errorf("获取到的authenticity_token为空")
	}

	return token, nil
}

// Oauth1 执行OAuth1认证流程
func (x *XAuth) Oauth1(oauthToken string) (string, error) {
	authenticityToken, err := x.GetTwitterToken(oauthToken)
	if err != nil {
		return "", fmt.Errorf("获取Twitter token失败: %w", err)
	}

	data := map[string]string{
		"authenticity_token": authenticityToken,
		"oauth_token":        oauthToken,
	}

	resp, err := x.client2.R().SetFormData(data).
		Post("https://x.com/oauth/authorize")
	if err != nil {
		return "", fmt.Errorf("OAuth1认证请求失败: %w", err)
	}

	// 处理请求限制
	if resp.StatusCode() == http.StatusTooManyRequests {
		time.Sleep(RetryInterval)
		return x.Oauth1(oauthToken)
	}

	if !strings.Contains(resp.String(), "oauth_verifier") {
		if strings.Contains(resp.String(), "This account is suspended.") {
			return "", fmt.Errorf("该账户已被封禁")
		}
		return "", fmt.Errorf("未找到oauth_verifier")
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

// GetAuthCode 获取认证码
func (x *XAuth) GetAuthCode(params map[string]string) (string, error) {
	if len(params) == 0 {
		return "", fmt.Errorf("参数不能为空")
	}

	resp, err := x.client.R().
		SetQueryParams(params).
		Get(TwitterAPIBase + "/oauth2/authorize")
	if err != nil {
		return "", fmt.Errorf("请求auth_code失败: %w", err)
	}

	// 处理请求限制
	if resp.StatusCode() == http.StatusTooManyRequests {
		time.Sleep(RetryInterval)
		return x.GetAuthCode(params)
	}

	// 处理CSRF token
	code := gjson.Get(resp.String(), "code")
	if code.Exists() && code.Int() == 353 {
		for _, cookie := range resp.Cookies() {
			if cookie.Name == "ct0" {
				x.client.SetHeader("x-csrf-token", cookie.Value)
				return x.GetAuthCode(params)
			}
		}
		return "", fmt.Errorf("未找到ct0 cookie")
	}

	// 检查账户状态
	if errCode := gjson.Get(resp.String(), "errors.0.code"); errCode.Exists() {
		if state, exists := AccountState[errCode.Int()]; exists {
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

// Oauth2 执行OAuth2认证流程
func (x *XAuth) Oauth2(params map[string]string) (string, error) {
	authCode, err := x.GetAuthCode(params)
	if err != nil {
		return "", fmt.Errorf("获取auth_code失败: %w", err)
	}

	data := map[string]string{
		"approval": "true",
		"code":     authCode,
	}

	resp, err := x.client.R().SetFormData(data).
		Post(TwitterAPIBase + "/oauth2/authorize")
	if err != nil {
		return "", fmt.Errorf("OAuth2认证请求失败: %w", err)
	}

	// 处理请求限制
	if resp.StatusCode() == http.StatusTooManyRequests {
		time.Sleep(RetryInterval)
		return x.Oauth2(params)
	}

	if !strings.Contains(resp.String(), "redirect_uri") {
		return "", fmt.Errorf("响应中未找到redirect_uri")
	}

	return authCode, nil
}
