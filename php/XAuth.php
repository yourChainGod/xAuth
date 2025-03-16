<?php
/**
 * Twitter OAuth认证工具类
 * 支持OAuth1和OAuth2认证流程
 */
class XAuth {
    // 常量定义
    private const TWITTER_AUTHORITY = 'twitter.com';
    private const TWITTER_ORIGIN = 'https://twitter.com';
    private const TWITTER_API_BASE = 'https://twitter.com/i/api/2';
    private const USER_AGENT = 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36';
    private const AUTHORIZATION = 'Bearer AAAAAAAAAAAAAAAAAAAAANRILgAAAAAAnNwIzUejRCOuH5E6I8xnZz4puTs%3D1Zv7ttfk8LF81IUq16cHjhLTvJu4FA33AGWWjCpTnA';
    private const MAX_RETRIES = 3;
    private const RETRY_INTERVAL = 1; // seconds

    // 账户状态码映射
    private const ACCOUNT_STATE = [
        32 => 'Bad Token',
        64 => 'SUSPENDED',
        141 => 'SUSPENDED',
        326 => 'LOCKED'
    ];

    private string $authToken;
    private array $cookies;
    private array $headers;
    private array $twitterHeaders;

    /**
     * 初始化XAuth实例
     * 
     * @param string $authToken Twitter认证token
     * @throws \InvalidArgumentException 当auth_token为空时
     */
    public function __construct(string $authToken) {
        if (empty($authToken)) {
            throw new \InvalidArgumentException('auth_token不能为空');
        }

        $this->authToken = $authToken;
        $this->cookies = ['auth_token' => $authToken];
        
        // 设置基础headers
        $this->headers = [
            'User-Agent' => self::USER_AGENT
        ];
        
        // 设置Twitter特定headers
        $this->twitterHeaders = array_merge($this->headers, [
            'Authority' => self::TWITTER_AUTHORITY,
            'Origin' => self::TWITTER_ORIGIN,
            'X-Twitter-Auth-Type' => 'OAuth2Session',
            'X-Twitter-Active-User' => 'yes',
            'Authorization' => self::AUTHORIZATION
        ]);
    }

    /**
     * 发送HTTP请求
     * 
     * @param string $method 请求方法
     * @param string $url 请求URL
     * @param array $options 请求选项
     * @param bool $includeTwitterHeaders 是否包含Twitter特定headers
     * @return array 响应数据
     * @throws \RuntimeException 当请求失败时
     */
    private function request(string $method, string $url, array $options = [], bool $includeTwitterHeaders = true): array {
        $headers = $includeTwitterHeaders ? $this->twitterHeaders : $this->headers;
        
        $curlOptions = [
            CURLOPT_URL => $url,
            CURLOPT_RETURNTRANSFER => true,
            CURLOPT_FOLLOWLOCATION => true,
            CURLOPT_TIMEOUT => 10,
            CURLOPT_HTTPHEADER => $this->formatHeaders($headers),
            CURLOPT_COOKIE => $this->formatCookies($this->cookies)
        ];
        
        if ($method === 'POST') {
            $curlOptions[CURLOPT_POST] = true;
            
            if (!empty($options['data'])) {
                $curlOptions[CURLOPT_POSTFIELDS] = $options['data'];
            }
        } elseif (!empty($options['params'])) {
            $url .= '?' . http_build_query($options['params']);
            $curlOptions[CURLOPT_URL] = $url;
        }
        
        $curl = curl_init();
        curl_setopt_array($curl, $curlOptions);
        
        $response = curl_exec($curl);
        $httpCode = curl_getinfo($curl, CURLINFO_HTTP_CODE);
        $headerSize = curl_getinfo($curl, CURLINFO_HEADER_SIZE);
        $error = curl_error($curl);
        
        curl_close($curl);
        
        if ($response === false) {
            throw new \RuntimeException('请求失败: ' . $error);
        }
        
        // 处理请求限制
        if ($httpCode === 429) {
            sleep(self::RETRY_INTERVAL);
            return $this->request($method, $url, $options, $includeTwitterHeaders);
        }
        
        return [
            'status' => $httpCode,
            'body' => $response
        ];
    }

    /**
     * 格式化请求头
     * 
     * @param array $headers 请求头数组
     * @return array 格式化后的请求头
     */
    private function formatHeaders(array $headers): array {
        $result = [];
        foreach ($headers as $key => $value) {
            $result[] = "$key: $value";
        }
        return $result;
    }

    /**
     * 格式化Cookie
     * 
     * @param array $cookies Cookie数组
     * @return string 格式化后的Cookie字符串
     */
    private function formatCookies(array $cookies): string {
        $result = [];
        foreach ($cookies as $key => $value) {
            $result[] = "$key=$value";
        }
        return implode('; ', $result);
    }

    /**
     * 获取Twitter认证token
     * 
     * @param string $oauthToken OAuth token
     * @return string 认证token字符串
     * @throws \InvalidArgumentException 当token无效或响应格式不正确时
     */
    public function getTwitterToken(string $oauthToken): string {
        if (empty($oauthToken)) {
            throw new \InvalidArgumentException('oauth_token不能为空');
        }

        $response = $this->request('GET', 'https://api.x.com/oauth/authenticate', [
            'params' => ['oauth_token' => $oauthToken]
        ], false);
        
        $content = $response['body'];
        
        if (strpos($content, 'authenticity_token') === false) {
            if (strpos($content, 'The request token for this page is invalid') !== false) {
                throw new \InvalidArgumentException('请求oauth_token无效');
            }
            throw new \InvalidArgumentException('响应中未找到authenticity_token');
        }

        // 尝试两种可能的token格式
        $tokenMarkers = [
            'name="authenticity_token" value="',
            'name="authenticity_token" type="hidden" value="'
        ];
        
        $token = null;
        foreach ($tokenMarkers as $marker) {
            if (strpos($content, $marker) !== false) {
                $parts = explode($marker, $content);
                $token = explode('"', $parts[1])[0];
                break;
            }
        }
                
        if (empty($token)) {
            throw new \InvalidArgumentException('获取到的authenticity_token为空');
        }
        
        return $token;
    }

    /**
     * 执行OAuth1认证流程
     * 
     * @param string $oauthToken OAuth token
     * @return string OAuth验证码
     * @throws \InvalidArgumentException 当认证失败时
     */
    public function oauth1(string $oauthToken): string {
        $authenticityToken = $this->getTwitterToken($oauthToken);
        
        $data = http_build_query([
            'authenticity_token' => $authenticityToken,
            'oauth_token' => $oauthToken
        ]);
        
        $response = $this->request('POST', 'https://x.com/oauth/authorize', [
            'data' => $data
        ], false);
        
        $content = $response['body'];
        
        if (strpos($content, 'oauth_verifier') === false) {
            if (strpos($content, 'This account is suspended.') !== false) {
                throw new \InvalidArgumentException('该账户已被封禁');
            }
            throw new \InvalidArgumentException('未找到oauth_verifier');
        }
        
        $parts = explode('oauth_verifier=', $content);
        $verifier = explode('"', $parts[1])[0];
        
        if (empty($verifier)) {
            throw new \InvalidArgumentException('获取到的oauth_verifier为空');
        }
        
        return $verifier;
    }

    /**
     * 获取认证码
     * 
     * @param array $params 请求参数
     * @return string 认证码
     * @throws \InvalidArgumentException 当参数无效或响应格式不正确时
     */
    public function getAuthCode(array $params): string {
        if (empty($params)) {
            throw new \InvalidArgumentException('参数不能为空');
        }

        $url = self::TWITTER_API_BASE . '/oauth2/authorize';
        $response = $this->request('GET', $url, ['params' => $params]);
        
        $data = json_decode($response['body'], true);
        if (json_last_error() !== JSON_ERROR_NONE) {
            throw new \InvalidArgumentException('响应不是有效的JSON格式');
        }
        
        // 处理CSRF token
        if (isset($data['code']) && $data['code'] === 353) {
            // 获取ct0 cookie
            preg_match('/ct0=([^;]+)/', $response['headers'] ?? '', $matches);
            if (!empty($matches[1])) {
                $this->twitterHeaders['X-CSRF-Token'] = $matches[1];
                return $this->getAuthCode($params);
            }
            throw new \InvalidArgumentException('未找到ct0 cookie');
        }

        // 检查错误
        if (isset($data['errors']) && !empty($data['errors'])) {
            $errorCode = $data['errors'][0]['code'] ?? null;
            if (isset(self::ACCOUNT_STATE[$errorCode])) {
                throw new \InvalidArgumentException('token状态错误: ' . self::ACCOUNT_STATE[$errorCode]);
            }
        }

        if (empty($data['auth_code'])) {
            throw new \InvalidArgumentException('响应中未找到auth_code');
        }
        
        return $data['auth_code'];
    }

    /**
     * 执行OAuth2认证流程
     * 
     * @param array $params 请求参数
     * @return string 认证码
     * @throws \InvalidArgumentException 当认证失败时
     */
    public function oauth2(array $params): string {
        $authCode = $this->getAuthCode($params);
        
        $data = http_build_query([
            'approval' => 'true',
            'code' => $authCode
        ]);
        
        $url = self::TWITTER_API_BASE . '/oauth2/authorize';
        $response = $this->request('POST', $url, ['data' => $data]);
        
        if (strpos($response['body'], 'redirect_uri') === false) {
            throw new \InvalidArgumentException('响应中未找到redirect_uri');
        }
        
        return $authCode;
    }
} 