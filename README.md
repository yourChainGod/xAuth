# [hdd.cm推特低至1毛5](https://hdd.cm/)

# Twitter OAuth认证库

这是一个用于处理Twitter OAuth认证的多语言实现库，支持OAuth1和OAuth2认证流程。该库提供了Python、JavaScript、Go、Rust、PHP、Java、C#和易语言八种实现版本，可以根据项目需求选择合适的版本。

## 功能特点

- 支持OAuth1和OAuth2认证流程
- 自动处理CSRF token
- 内置请求重试机制
- 完善的错误处理
- 多语言支持（Python、JavaScript、Go、Rust、PHP、Java、C#、易语言）

## 项目结构

```
xAuth/
├── python/           # Python实现
│   ├── xauth.py      # 主要实现
│   ├── test.py       # 测试代码
│   ├── requirements.txt
│   └── README.md     # Python版本说明
├── javascript/       # JavaScript实现
│   ├── xauth.js      # 主要实现
│   ├── test.js       # 测试代码
│   ├── package.json
│   └── README.md     # JavaScript版本说明
├── golang/           # Go实现
│   ├── xauth.go      # 主要实现
│   ├── test.go       # 测试代码
│   ├── go.mod
│   ├── go.sum
│   └── README.md     # Go版本说明
├── rust/             # Rust实现
│   ├── src/
│   │   ├── lib.rs    # 主要实现
│   │   └── main.rs   # 测试代码
│   ├── Cargo.toml
│   └── README.md     # Rust版本说明
├── php/              # PHP实现
│   ├── XAuth.php     # 主要实现
│   ├── test.php      # 测试代码
│   └── README.md     # PHP版本说明
├── java/             # Java实现
│   ├── src/
│   │   └── main/
│   │       └── java/
│   │           └── com/
│   │               └── xauth/
│   │                   ├── XAuth.java         # 主要实现
│   │                   ├── XAuthException.java # 异常类
│   │                   └── XAuthTest.java     # 测试代码
│   ├── pom.xml
│   └── README.md     # Java版本说明
├── csharp/           # C#实现
│   ├── XAuth/
│   │   ├── XAuth.cs      # 主要实现
│   │   ├── Program.cs    # 测试程序
│   │   └── XAuth.csproj  # 项目文件
│   └── README.md     # C#版本说明
└── 易语言/            # 易语言实现
    ├── XAuth.e       # 主要实现和测试代码
    └── README.md     # 易语言版本说明
```

## 使用方法

每种语言实现都有其独立的README.md文件，包含详细的使用说明和示例代码。请参考各语言文件夹中的README.md文件：

- [Python版本使用说明](python/README.md)
- [JavaScript版本使用说明](javascript/README.md)
- [Go版本使用说明](golang/README.md)
- [Rust版本使用说明](rust/README.md)
- [PHP版本使用说明](php/README.md)
- [Java版本使用说明](java/README.md)
- [C#版本使用说明](csharp/README.md)
- [易语言版本使用说明](易语言/README.md)

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
- C#: `csharp/XAuth/Program.cs`
- 易语言: `易语言/XAuth.e` (包含测试代码)

运行测试的详细说明请参考各语言文件夹中的README.md文件。

## 许可证

MIT License
