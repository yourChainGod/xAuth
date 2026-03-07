# xAuth Rust 版本

Twitter / X OAuth 示例仓库的 Rust 实现，支持 OAuth1 与 OAuth2。

## 文件

- `src/lib.rs`：主要实现
- `src/main.rs`：示例入口（读取环境变量）
- `Cargo.toml`：依赖配置

## 依赖

- Rust 1.75+
- `reqwest`
- `tokio`
- `serde_json`
- `thiserror`

## 构建 / 运行

```bash
cargo run
```

## 本地 mock 回归

```bash
cargo test
```

## 环境变量

- `XAUTH_AUTH_TOKEN`（必填）
- `XAUTH_CLIENT_ID`（必填）
- `XAUTH_REDIRECT_URI`（必填）
- `XAUTH_OAUTH1_TOKEN`（可选）
- `XAUTH_SCOPE` / `XAUTH_STATE` / `XAUTH_CODE_CHALLENGE` / `XAUTH_CODE_CHALLENGE_METHOD`（可选）

## 说明

当前 `main.rs` 是示例入口，不是 Rust 单元测试。建议后续增加 mock HTTP 用例覆盖 429、353、错误码映射等分支。
