# xAuth Python 版本

Twitter / X OAuth 示例仓库的 Python 实现，支持 OAuth1 与 OAuth2。

## 文件

- `xauth.py`：主要实现
- `test.py`：示例入口（读取环境变量）
- `requirements.txt`：依赖列表

## 依赖

- Python 3.8+
- `requests`

## 安装

```bash
pip install -r requirements.txt
```

## 环境变量

- `XAUTH_AUTH_TOKEN`（必填）
- `XAUTH_CLIENT_ID`（必填）
- `XAUTH_REDIRECT_URI`（必填）
- `XAUTH_OAUTH1_TOKEN`（可选）
- `XAUTH_SCOPE` / `XAUTH_STATE` / `XAUTH_CODE_CHALLENGE` / `XAUTH_CODE_CHALLENGE_METHOD`（可选）

## 运行示例

```bash
python test.py
```

## 本地 mock 回归

```bash
python mock_test.py
```

## 真实站点 smoke test

```bash
XAUTH_AUTH_TOKEN=your_token python real_smoke.py
```

只测单站可追加：

```bash
XAUTH_AUTH_TOKEN=your_token XAUTH_SITE=taskon python real_smoke.py
```

## 说明

当前 `test.py` 是示例脚本，不是 pytest 单元测试。若要做可复现验证，建议补 mock 响应测试。
