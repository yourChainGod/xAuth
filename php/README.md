# xAuth PHP 版本

Twitter / X OAuth 示例仓库的 PHP 实现，支持 OAuth1 与 OAuth2。

## 文件

- `XAuth.php`：主要实现
- `test.php`：示例入口（读取环境变量）

## 依赖

- PHP 8.0+
- curl 扩展

## 环境变量

- `XAUTH_AUTH_TOKEN`（必填）
- `XAUTH_CLIENT_ID`（必填）
- `XAUTH_REDIRECT_URI`（必填）
- `XAUTH_OAUTH1_TOKEN`（可选）
- `XAUTH_SCOPE` / `XAUTH_STATE` / `XAUTH_CODE_CHALLENGE` / `XAUTH_CODE_CHALLENGE_METHOD`（可选）

## 运行示例

```bash
php test.php
```

## 本地 mock 回归

```bash
php mock_test.php
```

## 说明

当前 `test.php` 是示例脚本，不是 PHPUnit 测试。若要稳定回归，建议引入 mock 响应与自动化测试。
