# [hdd.cm推特低至1毛5](https://hdd.cm/)

# XAuth Java版本

这是Twitter OAuth认证库的Java实现版本，支持OAuth1和OAuth2认证流程。

## 功能特点

- 支持OAuth1和OAuth2认证流程
- 自动处理CSRF token
- 内置请求重试机制
- 完善的错误处理
- 面向对象的API设计
- 完整的JavaDoc文档

## 文件结构

- `src/main/java/com/xauth/XAuthClient.java` - 主要实现类
- `src/main/java/com/xauth/XAuthException.java` - 自定义异常类
- `src/test/java/com/xauth/XAuthTest.java` - 测试类
- `pom.xml` - Maven项目配置文件

## 系统要求

- Java 8+
- Maven 3.6+（用于构建）

## 安装依赖

```bash
mvn clean install
```

## 使用方法

### 初始化

```java
import com.xauth.XAuthClient;
import com.xauth.XAuthException;

// 创建XAuthClient实例
XAuthClient client = new XAuthClient("your_auth_token");
```

### OAuth2认证

```java
// 设置OAuth2参数
Map<String, String> params = new HashMap<>();
params.put("code_challenge", "challenge");
params.put("code_challenge_method", "plain");
params.put("client_id", "your_client_id");
params.put("redirect_uri", "your_redirect_uri");
params.put("response_type", "code");
params.put("scope", "tweet.read users.read follows.read follows.write offline.access");
params.put("state", "your_state");

try {
    String authCode = client.oauth2(params);
    System.out.println("OAuth2认证码: " + authCode);
} catch (XAuthException e) {
    System.err.println("OAuth2认证失败: " + e.getMessage());
}
```

### OAuth1认证

```java
try {
    String oauth1Verifier = client.oauth1("your_oauth_token");
    System.out.println("OAuth1验证码: " + oauth1Verifier);
} catch (XAuthException e) {
    System.err.println("OAuth1认证失败: " + e.getMessage());
}
```

## 错误处理

库使用自定义的`XAuthException`来处理错误，您可以使用try-catch来捕获这些异常：

```java
try {
    // 执行认证操作
    String authCode = client.oauth2(params);
    System.out.println("认证成功: " + authCode);
} catch (XAuthException e) {
    // 处理错误
    System.err.println("认证失败: " + e.getMessage());
    e.printStackTrace();
}
```

## 资源管理

确保在不再需要客户端时关闭它：

```java
try {
    // 使用客户端...
} finally {
    client.close();
}
```

或者使用try-with-resources语法（如果XAuthClient实现了AutoCloseable接口）：

```java
try (XAuthClient client = new XAuthClient("your_auth_token")) {
    // 使用客户端...
}
```

## 运行测试

```bash
mvn test
```

## 注意事项

1. 确保提供有效的auth_token
2. 注意API请求限制
3. 正确处理错误响应
4. 遵守Twitter API使用条款 