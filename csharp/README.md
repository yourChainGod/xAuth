# [hdd.cm推特低至1毛5](https://hdd.cm/)

# XAuth C#版本

这是Twitter OAuth认证库的C# .NET实现版本，支持OAuth1和OAuth2认证流程。

## 功能特点

- 支持OAuth1和OAuth2认证流程
- 自动处理CSRF token
- 内置请求重试机制
- 完善的错误处理
- 异步API设计
- 完整的XML文档

## 文件结构

- `XAuth.cs` - 主要实现类，包含XAuthClient类
- `Program.cs` - 测试程序
- `XAuth.csproj` - .NET项目文件

## 系统要求

- .NET 6.0+
- 支持async/await的C#编译器

## 使用方法

### 初始化

```csharp
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

// 创建XAuthClient实例
var client = new XAuthClient("your_auth_token");
```

### OAuth2认证

```csharp
// 设置OAuth2参数
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
    string authCode = await client.OAuth2Async(parameters);
    Console.WriteLine($"OAuth2认证码: {authCode}");
}
catch (XAuthException ex)
{
    Console.WriteLine($"OAuth2认证失败: {ex.Message}");
}
```

### OAuth1认证

```csharp
try
{
    string oauth1Verifier = await client.OAuth1Async("your_oauth_token");
    Console.WriteLine($"OAuth1验证码: {oauth1Verifier}");
}
catch (XAuthException ex)
{
    Console.WriteLine($"OAuth1认证失败: {ex.Message}");
}
```

## 错误处理

库使用自定义的`XAuthException`来处理错误，您可以使用try-catch来捕获这些异常：

```csharp
try
{
    // 执行认证操作
    string authCode = await client.OAuth2Async(parameters);
    Console.WriteLine($"认证成功: {authCode}");
}
catch (XAuthException ex)
{
    // 处理错误
    Console.WriteLine($"认证失败: {ex.Message}");
}
catch (Exception ex)
{
    // 处理其他异常
    Console.WriteLine($"发生错误: {ex.Message}");
}
```

## 资源管理

确保在不再需要客户端时释放资源：

```csharp
// 如果XAuthClient实现了IDisposable接口
client.Dispose();
```

或者使用using语句：

```csharp
using (var client = new XAuthClient("your_auth_token"))
{
    // 使用客户端...
}
```

## 编译和运行

### 使用Visual Studio

1. 打开XAuth.csproj文件
2. 按F5运行项目

### 使用命令行

```bash
# 编译项目
dotnet build

# 运行项目
dotnet run
```

## 注意事项

1. 确保提供有效的auth_token
2. 注意API请求限制
3. 正确处理错误响应
4. 遵守Twitter API使用条款 