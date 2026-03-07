<?php
require_once 'XAuth.php';

class MockXAuth extends XAuth {
    private array $mainQueue = [];
    private array $secondaryQueue = [];

    public function queueMain(callable $handler): void {
        $this->mainQueue[] = $handler;
    }

    public function queueSecondary(callable $handler): void {
        $this->secondaryQueue[] = $handler;
    }

    protected function request(string $method, string $url, array $options = [], bool $includeTwitterHeaders = true, int $attempt = 0): array {
        $queue = $includeTwitterHeaders ? $this->mainQueue : $this->secondaryQueue;
        if (empty($queue)) {
            throw new RuntimeException("未为请求 {$method} {$url} 准备 mock 响应");
        }

        $handler = array_shift($queue);
        if ($includeTwitterHeaders) {
            $this->mainQueue = $queue;
        } else {
            $this->secondaryQueue = $queue;
        }

        $result = $handler($method, $url, $options, $includeTwitterHeaders, $attempt);
        if (!is_array($result) || !isset($result['status']) || !array_key_exists('body', $result)) {
            throw new RuntimeException('mock 响应格式错误');
        }

        if (!empty($result['cookies']) && is_array($result['cookies'])) {
            $reflection = new ReflectionClass(XAuth::class);
            $cookiesProperty = $reflection->getProperty('cookies');
            $cookiesProperty->setAccessible(true);
            $cookies = $cookiesProperty->getValue($this);
            foreach ($result['cookies'] as $name => $value) {
                $cookies[$name] = $value;
            }
            $cookiesProperty->setValue($this, $cookies);

            if (!empty($result['cookies']['ct0'])) {
                $headersProperty = $reflection->getProperty('twitterHeaders');
                $headersProperty->setAccessible(true);
                $headers = $headersProperty->getValue($this);
                $headers['X-CSRF-Token'] = $result['cookies']['ct0'];
                $headersProperty->setValue($this, $headers);
            }
        }

        if ($result['status'] === 429 && $attempt < 3) {
            return $this->request($method, $url, $options, $includeTwitterHeaders, $attempt + 1);
        }

        if ($result['status'] === 429) {
            throw new RuntimeException('请求频率限制，重试失败');
        }

        return [
            'status' => $result['status'],
            'body' => $result['body'],
            'headers' => $result['headers'] ?? []
        ];
    }
}

function assertSameValue($expected, $actual, string $message): void {
    if ($expected !== $actual) {
        throw new RuntimeException($message . "\nexpected: " . var_export($expected, true) . "\nactual: " . var_export($actual, true));
    }
}

function runOAuth2UsesCsrfTest(): void {
    $x = new MockXAuth('demo-token');
    $getCalls = 0;

    $x->queueMain(function ($method, $url, $options) use (&$getCalls) {
        $getCalls++;
        if ($getCalls === 1) {
            return ['status' => 200, 'body' => json_encode(['code' => 353]), 'cookies' => ['ct0' => 'csrf-123']];
        }
        assertSameValue('client-1', $options['params']['client_id'], 'client_id 不匹配');
        return ['status' => 200, 'body' => json_encode(['auth_code' => 'AUTH-CODE-1'])];
    });
    $x->queueMain(function ($method, $url, $options) {
        parse_str($options['data'], $form);
        assertSameValue('AUTH-CODE-1', $form['code'], 'OAuth2 code 不匹配');
        return ['status' => 200, 'body' => json_encode(['redirect_uri' => 'https://example.com/callback'])];
    });

    $code = $x->oauth2([
        'client_id' => 'client-1',
        'redirect_uri' => 'https://example.com/callback',
        'response_type' => 'code',
        'code_challenge' => 'challenge',
        'code_challenge_method' => 'plain',
    ]);

    assertSameValue('AUTH-CODE-1', $code, 'OAuth2 auth_code 不匹配');
    assertSameValue(2, $getCalls, 'OAuth2 GET 次数不匹配');
}

function runOAuth1ParsesVerifierTest(): void {
    $x = new MockXAuth('demo-token');
    $x->queueSecondary(function ($method, $url, $options) {
        return ['status' => 200, 'body' => '<input name="authenticity_token" type="hidden" value="token-456">'];
    });
    $x->queueSecondary(function ($method, $url, $options) {
        parse_str($options['data'], $form);
        assertSameValue('token-456', $form['authenticity_token'], 'authenticity_token 不匹配');
        return ['status' => 200, 'body' => 'oauth_verifier=verifier-789"'];
    });

    $verifier = $x->oauth1('oauth-token');
    assertSameValue('verifier-789', $verifier, 'OAuth1 verifier 不匹配');
}

function runRetryOn429Test(): void {
    $x = new MockXAuth('demo-token');
    $attempts = 0;
    $x->queueSecondary(function () use (&$attempts) {
        $attempts++;
        return ['status' => 429, 'body' => 'rate limited'];
    });
    $x->queueSecondary(function () use (&$attempts) {
        $attempts++;
        return ['status' => 200, 'body' => '<input name="authenticity_token" value="token-123">'];
    });

    $token = $x->getTwitterToken('oauth-token');
    assertSameValue('token-123', $token, '429 重试后的 token 不匹配');
    assertSameValue(2, $attempts, '429 重试次数不匹配');
}

runOAuth2UsesCsrfTest();
runOAuth1ParsesVerifierTest();
runRetryOn429Test();

echo "mock tests passed\n";
