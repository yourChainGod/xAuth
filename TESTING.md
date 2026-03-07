# xAuth Testing Guide

## 测试分层

xAuth 当前建议分成两层测试：

1. **本地 mock 回归**
   - 不消耗真实 token
   - 用于覆盖关键错误分支与协议细节
   - 应作为日常开发默认回归

2. **真实站点 smoke test**
   - 会消耗真实 `auth_token`
   - 只用于抽检真实 X / Twitter 授权链路
   - 当前建议使用已验证样本：`taskon`、`galxe`、`postwise`

## 一、本地 mock 回归

### Python

```bash
cd python
python mock_test.py
```

覆盖：
- `429 -> retry`
- `353 -> ct0 -> x-csrf-token -> auth_code`
- `oauth1 -> authenticity_token -> oauth_verifier`

### JavaScript

```bash
cd javascript
npm install
npm run test:mock
```

覆盖：
- `429 -> retry`
- cookie / `ct0` / `csrf`
- JSON `redirect_uri`
- OAuth1 verifier 解析

### Go

```bash
cd golang
go test ./...
```

覆盖：
- `429 -> retry`
- OAuth1 token / verifier 流程
- OAuth2 `ct0` / `csrf` / account state / redirect_uri`

### PHP

```bash
cd php
php mock_test.php
```

覆盖：
- `429 -> retry`
- OAuth1 authenticity token / verifier 解析
- OAuth2 `ct0 -> auth_code -> redirect_uri`

### Java

```bash
cd java
mvn test
```

覆盖：
- `429 -> retry`
- OAuth1 verifier 解析
- OAuth2 `ct0` / `csrf` / account state 映射

### Rust

```bash
cd rust
cargo test
```

覆盖：
- `429 -> retry`
- OAuth1 / OAuth2 本地 mock 流程
- 文本 / JSON `redirect_uri` 兼容

### C#

```bash
cd csharp/XAuth.Tests
dotnet test
```

覆盖：
- `429 -> retry`
- OAuth1 verifier 解析
- OAuth2 `ct0` / `csrf` / account state 映射

## 二、真实站点 smoke test

站点样本集中在根目录 `test-sites.json`。

### Python 真站抽检

```bash
cd python
XAUTH_AUTH_TOKEN=your_token python real_smoke.py
```

只测单站：

```bash
cd python
XAUTH_AUTH_TOKEN=your_token XAUTH_SITE=taskon python real_smoke.py
cd python
XAUTH_AUTH_TOKEN=your_token XAUTH_SITE=galxe python real_smoke.py
cd python
XAUTH_AUTH_TOKEN=your_token XAUTH_SITE=postwise python real_smoke.py
```

## 当前已验证样本

- `taskon`：OAuth2
- `galxe`：OAuth2
- `postwise`：OAuth1

## 推荐工作流

开发时：

1. 先跑 mock 回归
2. 修改通过后再抽检 1 个真站
3. 提交前可选择抽检 2~3 个真站
4. 本机缺工具链时，依赖 GitHub Actions CI 继续验证 PHP / Java / Rust / C#

这样既能节省 token，也能保证协议链路不飘。
