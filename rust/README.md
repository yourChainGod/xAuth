# [hdd.cm推特低至1毛5](https://hdd.cm/)

# XAuth Rust版本

这是Twitter OAuth认证库的Rust实现版本，支持OAuth1和OAuth2认证流程。

## 功能特点

- 支持OAuth1和OAuth2认证流程
- 自动处理CSRF token
- 内置请求重试机制
- 完善的错误处理
- 异步API设计
- 类型安全

## 文件结构

- `src/lib.rs` - 主要实现文件，包含XAuth结构体和相关函数
- `src/main.rs` - 测试程序
- `Cargo.toml` - 项目配置和依赖项

## 系统要求

- Rust 1.39+（支持async/await）
- 依赖包：reqwest, tokio, serde等

## 安装依赖

```bash
cargo build
```

## 使用方法

### 初始化

```rust
use std::collections::HashMap;
use xauth::XAuth;

#[tokio::main]
async fn main() -> Result<(), Box<dyn std::error::Error>> {
    // 创建XAuth实例
    let x = XAuth::new("your_auth_token")?;
    
    // 后续代码...
    Ok(())
}
```

### OAuth2认证

```rust
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
```

### OAuth1认证

```rust
// OAuth1认证
match x.oauth1("your_oauth_token").await {
    Ok(verifier) => println!("OAuth1 验证码: {}", verifier),
    Err(e) => eprintln!("OAuth1 认证错误: {}", e),
}
```

## 错误处理

库使用Rust的Result类型来处理错误，您可以使用match或?运算符来处理错误：

```rust
// 使用match
match x.oauth2(&params).await {
    Ok(auth_code) => {
        // 处理成功情况
        println!("认证成功: {}", auth_code);
    },
    Err(e) => {
        // 处理错误情况
        eprintln!("认证失败: {}", e);
    }
}

// 使用?运算符
let auth_code = x.oauth2(&params).await?;
println!("认证成功: {}", auth_code);
```

## 运行测试

```bash
cargo run
```

## 注意事项

1. 确保提供有效的auth_token
2. 注意API请求限制
3. 正确处理错误响应
4. 遵守Twitter API使用条款 