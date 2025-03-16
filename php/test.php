<?php
require_once 'XAuth.php';

/**
 * Twitter OAuth认证测试
 */
function main() {
    try {
        // 创建XAuth实例
        $x = new XAuth('5a0be24d859ad6820906bbc28b0ac9219a71489b');

        // OAuth2认证测试
        $params = [
            'code_challenge' => 'challenge',
            'code_challenge_method' => 'plain',
            'client_id' => 'MjdoOFVuNlJwRmxQT1VnWVBiUkw6MTpjaQ',
            'redirect_uri' => 'https://dapp.uxlink.io/authGateway',
            'response_type' => 'code',
            'scope' => 'tweet.read users.read follows.read follows.write offline.access',
            'state' => '1867096870644760576'
        ];

        $authCode = $x->oauth2($params);
        echo "OAuth2 认证码: $authCode\n";

        // OAuth1认证测试
        $oauth1Verifier = $x->oauth1('lSgWPQAAAAABuWQYAAABk7sKZI0');
        echo "OAuth1 验证码: $oauth1Verifier\n";

    } catch (\Exception $e) {
        echo "错误: " . $e->getMessage() . "\n";
    }
}

// 执行测试
main(); 