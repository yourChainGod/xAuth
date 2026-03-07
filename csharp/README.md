# xAuth C# 版本

Twitter / X OAuth 示例仓库的 C# 实现，支持 OAuth1 与 OAuth2。

## 文件

- `XAuth.cs`：主要实现
- `Program.cs`：示例入口（读取环境变量）
- `XAuth.csproj`：项目文件

## 依赖

- .NET 6.0+

## 环境变量

- `XAUTH_AUTH_TOKEN`（必填）
- `XAUTH_CLIENT_ID`（必填）
- `XAUTH_REDIRECT_URI`（必填）
- `XAUTH_OAUTH1_TOKEN`（可选）
- `XAUTH_SCOPE` / `XAUTH_STATE` / `XAUTH_CODE_CHALLENGE` / `XAUTH_CODE_CHALLENGE_METHOD`（可选）

## 运行示例

```bash
dotnet run
```

## 本地 mock 回归

```bash
cd XAuth.Tests
dotnet test
```

## 说明

当前 `Program.cs` 是示例入口，不是 xUnit / NUnit 自动化测试。若要进入生产使用，建议补 mock 测试与 CI。
