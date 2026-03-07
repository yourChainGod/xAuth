# xAuth Go 版本

这是 Twitter / X OAuth 示例仓库的 Go 实现，支持 OAuth1 与 OAuth2 流程。

## 文件结构

- `xauth.go`：主要实现
- `test.go`：示例入口（读取环境变量）
- `go.mod` / `go.sum`：Go 模块与依赖锁定

## 系统要求

- Go 1.23+
- 外部依赖：
  - `github.com/go-resty/resty/v2`
  - `github.com/tidwall/gjson`

## 环境变量

- `XAUTH_AUTH_TOKEN`（必填）
- `XAUTH_CLIENT_ID`（必填）
- `XAUTH_REDIRECT_URI`（必填）
- `XAUTH_OAUTH1_TOKEN`（可选）
- `XAUTH_SCOPE`（可选）
- `XAUTH_STATE`（可选）
- `XAUTH_CODE_CHALLENGE`（可选）
- `XAUTH_CODE_CHALLENGE_METHOD`（可选）

## 安装依赖

```bash
go mod download
```

## 运行示例

```bash
go run .
```

## 本地 mock 回归

```bash
go test ./...
```

## 使用示例

```go
x := NewTwitter(os.Getenv("XAUTH_AUTH_TOKEN"))
params := map[string]string{
    "code_challenge":        "challenge",
    "code_challenge_method": "plain",
    "client_id":             os.Getenv("XAUTH_CLIENT_ID"),
    "redirect_uri":          os.Getenv("XAUTH_REDIRECT_URI"),
    "response_type":         "code",
    "scope":                 "tweet.read users.read follows.read follows.write offline.access",
    "state":                 "demo-state",
}

authCode, err := x.Oauth2(params)
if err != nil {
    log.Fatal(err)
}
fmt.Println(authCode)
```

## 说明

- 当前 `test.go` 是示例程序，不是 `go test` 风格的单元测试。
- 若要补自动化测试，建议新增 `*_test.go` 文件并配合 mock server 验证 OAuth 响应分支。
