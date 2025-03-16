# [hdd.cm推特低至1毛5](https://hdd.cm/)

# XAuth PHP版本

这是Twitter OAuth认证库的PHP实现版本，支持OAuth1和OAuth2认证流程。

## 功能特点

- 支持OAuth1和OAuth2认证流程
- 自动处理CSRF token
- 内置请求重试机制
- 完善的错误处理
- 简洁易用的API

## 文件结构

- `XAuth.php` - 主要实现文件，包含XAuth类
- `test.php` - 测试程序

## 系统要求

- PHP 7.0+
- curl扩展

## 安装依赖

```bash
# 检查是否安装了curl扩展
php -m | grep curl
```

## 使用方法

### 初始化

```php
require_once 'XAuth.php';

// 创建XAuth实例
$x = new XAuth('your_auth_token');
```

### OAuth2认证

```php
// OAuth2认证
$params = [
    'code_challenge' => 'challenge',
    'code_challenge_method' => 'plain',
    'client_id' => 'your_client_id',
    'redirect_uri' => 'your_redirect_uri',
    'response_type' => 'code',
    'scope' => 'tweet.read users.read follows.read follows.write offline.access',
    'state' => 'your_state'
];

try {
    $authCode = $x->oauth2($params);
    echo "OAuth2 认证码: $authCode\n";
} catch (\Exception $e) {
    echo "OAuth2 认证失败: " . $e->getMessage() . "\n";
}
```

### OAuth1认证

```php
// OAuth1认证
try {
    $oauth1Verifier = $x->oauth1('your_oauth_token');
    echo "OAuth1 验证码: $oauth1Verifier\n";
} catch (\Exception $e) {
    echo "OAuth1 认证失败: " . $e->getMessage() . "\n";
}
```

## 错误处理

库使用PHP的异常机制来处理错误，您可以使用try-catch来捕获这些异常：

```php
try {
    // 执行认证操作
    $authCode = $x->oauth2($params);
    echo "认证成功: $authCode\n";
} catch (\Exception $e) {
    // 处理错误
    echo "认证失败: " . $e->getMessage() . "\n";
}
```

## 运行测试

```bash
php test.php
```

## 注意事项

1. 确保提供有效的auth_token
2. 注意API请求限制
3. 正确处理错误响应
4. 遵守Twitter API使用条款 