# xAuth Java 版本

这是 Twitter / X OAuth 示例仓库的 Java 实现，支持 OAuth1 与 OAuth2 流程。

## 文件结构

- `src/main/java/com/xauth/XAuth.java`：主要实现类
- `src/main/java/com/xauth/XAuthException.java`：异常类型
- `src/main/java/com/xauth/XAuthTest.java`：示例入口
- `pom.xml`：Maven 构建配置

## 系统要求

- Java 11+
- Maven 3.6+

## 依赖

- Apache HttpClient
- Jackson Databind
- SLF4J / Logback

## 环境变量

- `XAUTH_AUTH_TOKEN`（必填）
- `XAUTH_CLIENT_ID`（必填）
- `XAUTH_REDIRECT_URI`（必填）
- `XAUTH_OAUTH1_TOKEN`（可选）
- `XAUTH_SCOPE`（可选）
- `XAUTH_STATE`（可选）
- `XAUTH_CODE_CHALLENGE`（可选）
- `XAUTH_CODE_CHALLENGE_METHOD`（可选）

## 构建

```bash
mvn package
```

## 运行示例

```bash
java -cp target/xauth-1.0-SNAPSHOT-jar-with-dependencies.jar com.xauth.XAuthTest
```

## 本地 mock 回归

```bash
mvn test
```

## 使用示例

```java
import com.xauth.XAuth;

Map<String, String> params = new HashMap<>();
params.put("code_challenge", "challenge");
params.put("code_challenge_method", "plain");
params.put("client_id", System.getenv("XAUTH_CLIENT_ID"));
params.put("redirect_uri", System.getenv("XAUTH_REDIRECT_URI"));
params.put("response_type", "code");
params.put("scope", "tweet.read users.read follows.read follows.write offline.access");
params.put("state", "demo-state");

XAuth client = new XAuth(System.getenv("XAUTH_AUTH_TOKEN"));
try {
    String authCode = client.oauth2(params);
    System.out.println(authCode);
} finally {
    client.close();
}
```

## 说明

- 当前 `XAuthTest.java` 是示例入口，不是 JUnit 测试。
- 如果后续需要标准化测试，建议迁移到 `src/test/java` 并引入 mock HTTP 响应。
