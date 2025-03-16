# [hdd.cm推特低至1毛5](https://hdd.cm/)

# XAuth Python版本

这是Twitter OAuth认证库的Python实现版本，支持OAuth1和OAuth2认证流程。

## 功能特点

- 支持OAuth1和OAuth2认证流程
- 自动处理CSRF token
- 内置请求重试机制
- 完善的错误处理
- 简洁易用的API

## 文件结构

- `xauth.py` - 主要实现文件，包含XAuth类
- `test.py` - 测试程序
- `requirements.txt` - 依赖项列表

## 系统要求

- Python 3.6+
- 依赖包：requests

## 安装依赖

```bash
pip install -r requirements.txt
```

## 使用方法

### 初始化

```python
from xauth import XAuth

x = XAuth("your_auth_token")
```

### OAuth2认证

```python
# OAuth2认证
params = {
    "code_challenge": "challenge",
    "code_challenge_method": "plain",
    "client_id": "your_client_id",
    "redirect_uri": "your_redirect_uri",
    "response_type": "code",
    "scope": "tweet.read users.read follows.read follows.write offline.access",
    "state": "your_state"
}
auth_code = x.oauth2(params)
print(f"OAuth2 认证码: {auth_code}")
```

### OAuth1认证

```python
# OAuth1认证
oauth1_verifier = x.oauth1("your_oauth_token")
print(f"OAuth1 验证码: {oauth1_verifier}")
```

## 错误处理

库会抛出异常来表示认证过程中的错误。您可以使用try-except来捕获这些异常：

```python
try:
    # 执行认证操作
    auth_code = x.oauth2(params)
except Exception as e:
    print(f"认证失败: {str(e)}")
```

## 运行测试

```bash
python test.py
```

## 注意事项

1. 确保提供有效的auth_token
2. 注意API请求限制
3. 正确处理错误响应
4. 遵守Twitter API使用条款 