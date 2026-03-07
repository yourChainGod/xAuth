package main

import (
	"fmt"
	"log"
	"os"
)

func mustEnv(key string) string {
	value := os.Getenv(key)
	if value == "" {
		log.Fatalf("请先设置 %s 环境变量", key)
	}
	return value
}

func main() {
	x := NewTwitter(mustEnv("XAUTH_AUTH_TOKEN"))
	params := map[string]string{
		"code_challenge":        getenvDefault("XAUTH_CODE_CHALLENGE", "challenge"),
		"code_challenge_method": getenvDefault("XAUTH_CODE_CHALLENGE_METHOD", "plain"),
		"client_id":             mustEnv("XAUTH_CLIENT_ID"),
		"redirect_uri":          mustEnv("XAUTH_REDIRECT_URI"),
		"response_type":         "code",
		"scope":                 getenvDefault("XAUTH_SCOPE", "tweet.read users.read follows.read follows.write offline.access"),
		"state":                 getenvDefault("XAUTH_STATE", "demo-state"),
	}

	authCode, err := x.Oauth2(params)
	if err != nil {
		log.Fatal(err)
	}
	fmt.Println("OAuth2 认证码:", authCode)

	oauth1Token := os.Getenv("XAUTH_OAUTH1_TOKEN")
	if oauth1Token == "" {
		fmt.Println("未设置 XAUTH_OAUTH1_TOKEN，跳过 OAuth1 示例")
		return
	}

	oauth1, err := x.Oauth1(oauth1Token)
	if err != nil {
		log.Fatal(err)
	}
	fmt.Println("OAuth1 验证码:", oauth1)
}

func getenvDefault(key, fallback string) string {
	value := os.Getenv(key)
	if value == "" {
		return fallback
	}
	return value
}
