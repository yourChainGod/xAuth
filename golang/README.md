# [hdd.cm推特低至1毛5](https://hdd.cm/)

# XAuth Go版本

这是Twitter OAuth认证库的Go实现版本，支持OAuth1和OAuth2认证流程。

## 功能特点

- 支持OAuth1和OAuth2认证流程
- 自动处理CSRF token
- 内置请求重试机制
- 完善的错误处理
- 符合Go语言习惯的API设计

## 文件结构

- `xauth.go` - 主要实现文件
- `test.go` - 测试程序
- `go.mod` - Go模块定义
- `go.sum` - 依赖版本锁定文件

## 系统要求

- Go 1.13+
- 依赖包：net/http, encoding/json等标准库

## 安装依赖

```bash
go mod download
```

## 使用方法

### 初始化

```go
import (
    "fmt"
    "log"
)

// 创建XAuth实例
x := NewTwitter("your_auth_token")
```

### OAuth2认证

```go
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
if err != nil {
    log.Fatalf("OAuth2认证失败: %v", err)
}
fmt.Printf("OAuth2认证码: %s\n", authCode)
```

### OAuth1认证

```go
// OAuth1认证
oauth1Verifier, err := x.Oauth1("your_oauth_token")
if err != nil {
    log.Fatalf("OAuth1认证失败: %v", err)
}
fmt.Printf("OAuth1验证码: %s\n", oauth1Verifier)
```

## 错误处理

库使用Go的标准错误处理方式，返回错误作为第二个返回值：

```go
authCode, err := x.Oauth2(params)
if err != nil {
    // 处理错误
    log.Printf("认证失败: %v", err)
    return
}

// 使用authCode
fmt.Printf("认证成功，认证码: %s\n", authCode)
```

## 运行测试

```bash
go run test.go
```

## 注意事项

1. 确保提供有效的auth_token
2. 注意API请求限制
3. 正确处理错误响应
4. 遵守Twitter API使用条款 