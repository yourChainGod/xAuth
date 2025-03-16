# [hdd.cm推特低至1毛5](https://hdd.cm/)


# go和Python版本经过完整测试，js版本为AI生成，自行测试使用

# Twitter OAuth认证库

这是一个用于处理Twitter OAuth认证的多语言实现库，支持OAuth1和OAuth2认证流程。该库提供了Python、JavaScript、Go、Rust、PHP和Java六种实现版本，可以根据项目需求选择合适的版本。

## 功能特点

- 支持OAuth1和OAuth2认证流程
- 自动处理CSRF token
- 内置请求重试机制
- 完善的错误处理
- 多语言支持（Python、JavaScript、Go、Rust、PHP、Java）

## 项目结构

```
xAuth/
├── python/           # Python实现
│   ├── xauth.py      # 主要实现
│   ├── test.py       # 测试代码
│   └── requirements.txt
├── javascript/       # JavaScript实现
│   ├── xauth.js      # 主要实现
│   ├── test.js       # 测试代码
│   └── package.json
├── golang/           # Go实现
│   ├── xauth.go      # 主要实现
│   ├── test.go       # 测试代码
│   ├── go.mod
│   └── go.sum
├── rust/             # Rust实现
│   ├── src/
│   │   ├── lib.rs    # 主要实现
│   │   └── main.rs   # 测试代码
│   └── Cargo.toml
├── php/              # PHP实现
│   ├── XAuth.php     # 主要实现
│   └── test.php      # 测试代码
└── java/             # Java实现
    ├── src/
    │   └── main/
    │       └── java/
    │           └── com/
    │               └── xauth/
    │                   ├── XAuth.java         # 主要实现
    │                   ├── XAuthException.java # 异常类
    │                   └── XAuthTest.java     # 测试代码
    └── pom.xml
```

## 使用方法

### Python版本

```python
from xauth import XAuth

x = XAuth("your_auth_token")

# OAuth2认证
params = {
    "code_challenge": "challenge",
    "code_challenge_method": "plain",
    "client_id": "your_client_id",
    "redirect_uri": "your_redirect_uri",
    "response_type": "code",
    "scope": "tweet.read users.read follows.read follows.write offline.access",
    "state": "your_state"
}
auth_code = x.oauth2(params)

# OAuth1认证
oauth1_verifier = x.oauth1("your_oauth_token")
```

### JavaScript版本

```javascript
const XAuth = require('./xauth');

const x = new XAuth('your_auth_token');

// OAuth2认证
const params = {
    code_challenge: 'challenge',
    code_challenge_method: 'plain',
    client_id: 'your_client_id',
    redirect_uri: 'your_redirect_uri',
    response_type: 'code',
    scope: 'tweet.read users.read follows.read follows.write offline.access',
    state: 'your_state'
};

x.oauth2(params)
    .then(authCode => console.log('OAuth2认证码:', authCode))
    .catch(error => console.error('错误:', error));

// OAuth1认证
x.oauth1('your_oauth_token')
    .then(verifier => console.log('OAuth1验证码:', verifier))
    .catch(error => console.error('错误:', error));
```

### Go版本

```go
x := NewTwitter("your_auth_token")

// OAuth2认证
params := map[string]string{
    "code_challenge":        "challenge",
    "code_challenge_method": "plain",
    "client_id":            "your_client_id",
    "redirect_uri":         "your_redirect_uri",
    "response_type":        "code",
    "scope":                "tweet.read users.read follows.read follows.write offline.access",
    "state":                "your_state",
}
authCode, err := x.Oauth2(params)

// OAuth1认证
oauth1Verifier, err := x.Oauth1("your_oauth_token")
```

### Rust版本

```rust
use std::collections::HashMap;
use xauth::XAuth;

// 创建XAuth实例
let x = XAuth::new("your_auth_token")?;

// OAuth2认证
let mut params = HashMap::new();
params.insert("code_challenge".to_string(), "challenge".to_string());
params.insert("code_challenge_method".to_string(), "plain".to_string());
params.insert("client_id".to_string(), "your_client_id".to_string());
params.insert("redirect_uri".to_string(), "your_redirect_uri".to_string());
params.insert("response_type".to_string(), "code".to_string());
params.insert("scope".to_string(), "tweet.read users.read follows.read follows.write offline.access".to_string());
params.insert("state".to_string(), "your_state".to_string());

match x.oauth2(&params).await {
    Ok(auth_code) => println!("OAuth2 认证码: {}", auth_code),
    Err(e) => eprintln!("OAuth2 认证错误: {}", e),
}

// OAuth1认证
match x.oauth1("your_oauth_token").await {
    Ok(verifier) => println!("OAuth1 验证码: {}", verifier),
    Err(e) => eprintln!("OAuth1 认证错误: {}", e),
}
```

### PHP版本

```php
require_once 'XAuth.php';

// 创建XAuth实例
$x = new XAuth('your_auth_token');

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
    
    // OAuth1认证
    $oauth1Verifier = $x->oauth1('your_oauth_token');
    echo "OAuth1 验证码: $oauth1Verifier\n";
} catch (\Exception $e) {
    echo "错误: " . $e->getMessage() . "\n";
}
```

### Java版本

```java
import com.xauth.XAuth;
import com.xauth.XAuthException;
import java.util.HashMap;
import java.util.Map;

// 创建XAuth实例
XAuth xAuth = new XAuth("your_auth_token");

try {
    // OAuth2认证
    Map<String, String> params = new HashMap<>();
    params.put("code_challenge", "challenge");
    params.put("code_challenge_method", "plain");
    params.put("client_id", "your_client_id");
    params.put("redirect_uri", "your_redirect_uri");
    params.put("response_type", "code");
    params.put("scope", "tweet.read users.read follows.read follows.write offline.access");
    params.put("state", "your_state");
    
    String authCode = xAuth.oauth2(params);
    System.out.println("OAuth2 认证码: " + authCode);
    
    // OAuth1认证
    String oauth1Verifier = xAuth.oauth1("your_oauth_token");
    System.out.println("OAuth1 验证码: " + oauth1Verifier);
    
} catch (Exception e) {
    System.err.println("错误: " + e.getMessage());
} finally {
    // 关闭HTTP客户端
    xAuth.close();
}
```

## 安装依赖

### Python版本
```bash
cd python
pip install -r requirements.txt
```

### JavaScript版本
```bash
cd javascript
npm install
```

### Go版本
```bash
cd golang
go mod download
```

### Rust版本
```bash
cd rust
cargo build
```

### PHP版本
```bash
# PHP需要安装curl扩展
php -m | grep curl
```

### Java版本
```bash
cd java
mvn clean package
```

## 错误处理

库会处理以下常见错误情况：

- 无效的认证token
- 请求限制（自动重试）
- 账户状态问题（封禁、锁定等）
- CSRF token缺失
- 网络请求失败

## 注意事项

1. 确保提供有效的auth_token
2. 注意API请求限制
3. 正确处理错误响应
4. 遵守Twitter API使用条款

## 测试

每个语言版本都提供了对应的测试文件：

- Python: `python/test.py`
- JavaScript: `javascript/test.js`
- Go: `golang/test.go`
- Rust: `rust/src/main.rs`
- PHP: `php/test.php`
- Java: `java/src/main/java/com/xauth/XAuthTest.java`

运行测试：

```bash
# Python
cd python
python test.py

# JavaScript
cd javascript
node test.js

# Go
cd golang
go run test.go

# Rust
cd rust
cargo run

# PHP
cd php
php test.php

# Java
cd java
mvn exec:java -Dexec.mainClass="com.xauth.XAuthTest"
```

## 许可证

MIT License
