# xAuth

一个面向 Twitter / X OAuth 场景的多语言示例仓库，提供 Python、JavaScript、Go、Rust、PHP、Java、C# 七种实现。

## 当前状态

- 支持 OAuth1 与 OAuth2 授权流程
- 各语言目录内提供的是**示例程序 / smoke script**，不是完整自动化测试套件
- Python / JavaScript / Go / PHP / Java / Rust / C# 均已补本地 **mock 回归入口**；其中 Python / JavaScript / Go 已在当前宿主完成实跑验证，其余四门因缺工具链暂为静态落地
- 示例已改为通过环境变量读取 token，避免把真实凭据写进仓库

## 目录结构

```text
xAuth/
├── python/
│   ├── xauth.py
│   ├── test.py
│   ├── requirements.txt
│   └── README.md
├── javascript/
│   ├── xauth.js
│   ├── test.js
│   ├── package.json
│   ├── package-lock.json
│   └── README.md
├── golang/
│   ├── xauth.go
│   ├── test.go
│   ├── go.mod
│   ├── go.sum
│   └── README.md
├── rust/
│   ├── src/
│   │   ├── lib.rs
│   │   └── main.rs
│   ├── Cargo.toml
│   └── README.md
├── php/
│   ├── XAuth.php
│   ├── test.php
│   └── README.md
├── java/
│   ├── src/main/java/com/xauth/
│   │   ├── XAuth.java
│   │   ├── XAuthException.java
│   │   └── XAuthTest.java
│   ├── pom.xml
│   └── README.md
└── csharp/
    ├── XAuth.cs
    ├── Program.cs
    ├── XAuth.csproj
    └── README.md
```

## 示例所需环境变量

所有语言示例统一使用以下环境变量：

- `XAUTH_AUTH_TOKEN`：登录后的 auth_token
- `XAUTH_CLIENT_ID`：OAuth2 client_id
- `XAUTH_REDIRECT_URI`：OAuth2 redirect_uri
- `XAUTH_OAUTH1_TOKEN`：可选，OAuth1 request token
- `XAUTH_SCOPE`：可选，默认 `tweet.read users.read follows.read follows.write offline.access`
- `XAUTH_STATE`：可选，默认 `demo-state`
- `XAUTH_CODE_CHALLENGE`：可选，默认 `challenge`
- `XAUTH_CODE_CHALLENGE_METHOD`：可选，默认 `plain`

可复制 `.env.example` 并按需导出环境变量。

真实站点样本配置已统一收口到根目录 `test-sites.json`；测试分层说明见 `TESTING.md`。

## 运行示例

### Python

```bash
cd python
pip install -r requirements.txt
python test.py
```

### JavaScript

```bash
cd javascript
npm install
node test.js
```

### Go

```bash
cd golang
go run .
```

### Rust

```bash
cd rust
cargo run
```

### PHP

```bash
cd php
php test.php
```

### Java

```bash
cd java
mvn package
java -cp target/xauth-1.0-SNAPSHOT-jar-with-dependencies.jar com.xauth.XAuthTest
```

### C#

```bash
cd csharp
dotnet run
```

## 本地 mock 回归

### Python

```bash
cd python
python mock_test.py
```

### JavaScript

```bash
cd javascript
npm run test:mock
```

### Go

```bash
cd golang
go test ./...
```

### PHP

```bash
cd php
php mock_test.php
```

### Java

```bash
cd java
mvn test
```

### Rust

```bash
cd rust
cargo test
```

### C#

```bash
cd csharp/XAuth.Tests
dotnet test
```

## CI

仓库已提供 GitHub Actions CI，当前覆盖：Python、JavaScript、Go、PHP、Java、Rust、C#。
其中 PHP / Java / Rust / C# 主要依赖 CI 进行真实工具链验证。

## 已知问题 / 建议

1. 当前仓库主要是多语言实现与示例集合，缺少 mock-based 自动化测试。
2. 若要用于生产环境，建议补充：
   - 统一测试夹具与伪造响应
   - CI 构建矩阵
   - 依赖版本锁定与安全扫描
   - 更明确的错误码与返回值约定

## 许可证

MIT License
