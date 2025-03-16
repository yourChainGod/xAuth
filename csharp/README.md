# XAuth C# .NET版本

这是Twitter OAuth认证库的C# .NET实现版本，支持OAuth1和OAuth2认证流程。

## 功能特点

- 支持OAuth1和OAuth2认证流程
- 自动处理CSRF token
- 内置请求重试机制
- 完善的错误处理
- 异步API设计
- 完整的XML文档注释

## 项目结构

- `XAuth.cs` - 主要实现文件，包含XAuthClient类和XAuthException类
- `Program.cs` - 测试程序
- `XAuth.csproj` - 项目文件

## 系统要求

- .NET 6.0或更高版本
- 支持异步/await的C#编译器

## 使用方法

### 初始化

```csharp
// 创建XAuth实例
using var xAuth = new XAuthClient("your_auth_token");
```

### OAuth2认证

```csharp
// OAuth2认证
var parameters = new Dictionary<string, string>
{
    { "code_challenge", "challenge" },
    { "code_challenge_method", "plain" },
    { "client_id", "your_client_id" },
    { "redirect_uri", "your_redirect_uri" },
    { "response_type", "code" },
    { "scope", "tweet.read users.read follows.read follows.write offline.access" },
    { "state", "your_state" }
};

try
{
    string authCode = await xAuth.OAuth2Async(parameters);
    Console.WriteLine($"OAuth2认证成功，认证码: {authCode}");
}
catch (XAuthException ex)
{
    Console.WriteLine($"OAuth2认证失败: {ex.Message}");
}
```

### OAuth1认证

```csharp
// OAuth1认证
try
{
    string oauth1Verifier = await xAuth.OAuth1Async("your_oauth_token");
    Console.WriteLine($"OAuth1认证成功，验证码: {oauth1Verifier}");
}
catch (XAuthException ex)
{
    Console.WriteLine($"OAuth1认证失败: {ex.Message}");
}
```

## 错误处理

库使用XAuthException类来表示认证过程中的错误。您可以捕获这些异常并处理它们，或者使用LastError属性获取最后一次错误信息。

```csharp
try
{
    // 执行认证操作
}
catch (XAuthException ex)
{
    Console.WriteLine($"认证失败: {ex.Message}");
}

// 或者使用LastError属性
if (string.IsNullOrEmpty(xAuth.LastError))
{
    Console.WriteLine("操作成功");
}
else
{
    Console.WriteLine($"操作失败: {xAuth.LastError}");
}
```

## 资源管理

XAuthClient类实现了IDisposable接口，因此您应该在使用完毕后释放资源：

```csharp
using (var xAuth = new XAuthClient("your_auth_token"))
{
    // 使用xAuth
}
// 资源会在这里自动释放
```

或者手动调用Dispose方法：

```csharp
var xAuth = new XAuthClient("your_auth_token");
try
{
    // 使用xAuth
}
finally
{
    xAuth.Dispose();
}
```

## 编译和运行

### 使用Visual Studio

1. 打开Visual Studio
2. 打开XAuth.csproj项目文件
3. 按F5运行项目

### 使用命令行

```bash
cd csharp/XAuth
dotnet build
dotnet run
```

## 注意事项

1. 确保提供有效的auth_token
2. 注意API请求限制
3. 正确处理错误响应
4. 遵守Twitter API使用条款 