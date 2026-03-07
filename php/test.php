<?php
require_once 'XAuth.php';

function must_env(string $key): string {
    $value = getenv($key);
    if ($value === false || $value === '') {
        throw new RuntimeException("请先设置 {$key} 环境变量");
    }
    return $value;
}

function env_or(string $key, string $fallback): string {
    $value = getenv($key);
    return ($value === false || $value === '') ? $fallback : $value;
}

try {
    $x = new XAuth(must_env('XAUTH_AUTH_TOKEN'));

    $params = [
        'code_challenge' => env_or('XAUTH_CODE_CHALLENGE', 'challenge'),
        'code_challenge_method' => env_or('XAUTH_CODE_CHALLENGE_METHOD', 'plain'),
        'client_id' => must_env('XAUTH_CLIENT_ID'),
        'redirect_uri' => must_env('XAUTH_REDIRECT_URI'),
        'response_type' => 'code',
        'scope' => env_or('XAUTH_SCOPE', 'tweet.read users.read follows.read follows.write offline.access'),
        'state' => env_or('XAUTH_STATE', 'demo-state'),
    ];

    $authCode = $x->oauth2($params);
    echo "OAuth2 认证码: {$authCode}\n";

    $oauth1Token = getenv('XAUTH_OAUTH1_TOKEN');
    if ($oauth1Token !== false && $oauth1Token !== '') {
        $oauth1Verifier = $x->oauth1($oauth1Token);
        echo "OAuth1 验证码: {$oauth1Verifier}\n";
    } else {
        echo "未设置 XAUTH_OAUTH1_TOKEN，跳过 OAuth1 示例\n";
    }
} catch (Throwable $e) {
    echo '错误: ' . $e->getMessage() . "\n";
    exit(1);
}
