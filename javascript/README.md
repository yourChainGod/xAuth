# xAuth JavaScript 版本

Twitter / X OAuth 示例仓库的 JavaScript 实现，支持 OAuth1 与 OAuth2。

## 文件

- `xauth.js`：主要实现
- `test.js`：示例入口（读取环境变量）
- `package.json`：依赖配置

## 依赖

- Node.js 18+
- `axios`

## 安装

```bash
npm install
```

## 环境变量

- `XAUTH_AUTH_TOKEN`（必填）
- `XAUTH_CLIENT_ID`（必填）
- `XAUTH_REDIRECT_URI`（必填）
- `XAUTH_OAUTH1_TOKEN`（可选）
- `XAUTH_SCOPE` / `XAUTH_STATE` / `XAUTH_CODE_CHALLENGE` / `XAUTH_CODE_CHALLENGE_METHOD`（可选）

## 运行示例

```bash
node test.js
```

## 本地 mock 回归

```bash
npm run test:mock
```

## 说明

当前 `test.js` 是示例脚本，不是自动化测试。建议后续补充基于 mock server 的集成测试。
