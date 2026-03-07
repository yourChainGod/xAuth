package com.xauth;

import java.util.HashMap;
import java.util.Map;

/**
 * Twitter OAuth 示例入口
 */
public class XAuthTest {
    private static String mustEnv(String key) {
        String value = System.getenv(key);
        if (value == null || value.isEmpty()) {
            throw new IllegalArgumentException("请先设置 " + key + " 环境变量");
        }
        return value;
    }

    private static String envOr(String key, String fallback) {
        String value = System.getenv(key);
        return (value == null || value.isEmpty()) ? fallback : value;
    }

    public static void main(String[] args) {
        XAuth xAuth = null;

        try {
            xAuth = new XAuth(mustEnv("XAUTH_AUTH_TOKEN"));

            Map<String, String> params = new HashMap<>();
            params.put("code_challenge", envOr("XAUTH_CODE_CHALLENGE", "challenge"));
            params.put("code_challenge_method", envOr("XAUTH_CODE_CHALLENGE_METHOD", "plain"));
            params.put("client_id", mustEnv("XAUTH_CLIENT_ID"));
            params.put("redirect_uri", mustEnv("XAUTH_REDIRECT_URI"));
            params.put("response_type", "code");
            params.put("scope", envOr("XAUTH_SCOPE", "tweet.read users.read follows.read follows.write offline.access"));
            params.put("state", envOr("XAUTH_STATE", "demo-state"));

            String authCode = xAuth.oauth2(params);
            System.out.println("OAuth2 认证码: " + authCode);

            String oauth1Token = System.getenv("XAUTH_OAUTH1_TOKEN");
            if (oauth1Token != null && !oauth1Token.isEmpty()) {
                String oauth1Verifier = xAuth.oauth1(oauth1Token);
                System.out.println("OAuth1 验证码: " + oauth1Verifier);
            } else {
                System.out.println("未设置 XAUTH_OAUTH1_TOKEN，跳过 OAuth1 示例");
            }
        } catch (Exception e) {
            System.err.println("错误: " + e.getMessage());
            e.printStackTrace();
            System.exit(1);
        } finally {
            if (xAuth != null) {
                xAuth.close();
            }
        }
    }
}
