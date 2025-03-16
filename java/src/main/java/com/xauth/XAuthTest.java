package com.xauth;

import java.util.HashMap;
import java.util.Map;

/**
 * Twitter OAuth认证测试
 */
public class XAuthTest {
    
    public static void main(String[] args) {
        XAuth xAuth = null;
        
        try {
            // 创建XAuth实例
            xAuth = new XAuth("5a0be24d859ad6820906bbc28b0ac9219a71489b");
            
            // OAuth2认证测试
            Map<String, String> params = new HashMap<>();
            params.put("code_challenge", "challenge");
            params.put("code_challenge_method", "plain");
            params.put("client_id", "MjdoOFVuNlJwRmxQT1VnWVBiUkw6MTpjaQ");
            params.put("redirect_uri", "https://dapp.uxlink.io/authGateway");
            params.put("response_type", "code");
            params.put("scope", "tweet.read users.read follows.read follows.write offline.access");
            params.put("state", "1867096870644760576");
            
            String authCode = xAuth.oauth2(params);
            System.out.println("OAuth2 认证码: " + authCode);
            
            // OAuth1认证测试
            String oauth1Verifier = xAuth.oauth1("lSgWPQAAAAABuWQYAAABk7sKZI0");
            System.out.println("OAuth1 验证码: " + oauth1Verifier);
            
        } catch (Exception e) {
            System.err.println("错误: " + e.getMessage());
            e.printStackTrace();
        } finally {
            // 关闭HTTP客户端
            if (xAuth != null) {
                xAuth.close();
            }
        }
    }
} 