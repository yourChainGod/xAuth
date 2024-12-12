# [hdd.cm推特低至1毛5](https://hdd.cm/)


# go和Python版本经过完整测试，js版本为AI生成，自行测试使用

# Twitter OAuth Authentication Library

这是一个用于处理Twitter OAuth认证的多语言实现库，支持OAuth1和OAuth2认证流程。该库提供了Go、Python和Node.js三种实现版本，可以根据项目需求选择合适的版本。

## 功能特点

- 支持OAuth1和OAuth2认证流程
- 自动处理CSRF token
- 内置请求重试机制
- 完善的错误处理
- 多语言支持（Go、Python、Node.js）

## 使用方法

### Go版本

```go
x := NewTwitter("your_auth_token")

// OAuth2认证
params := map[string]string{
    "code_challenge":        "challenge",
    "code_challenge_method": "plain",
    "client_id":            "your_client_id",
    "redirect_uri":         "your_redirect_uri",
    "response_type":        "code",
    "scope":                "tweet.read users.read follows.read follows.write offline.access",
    "state":                "your_state",
}
authCode, err := x.Oauth2(params)

// OAuth1认证
oauth1Verifier, err := x.Oauth1("your_oauth_token")
```

### Python版本

```python
from xauth import XAuth

x = XAuth("your_auth_token")

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

# OAuth1认证
oauth1_verifier = x.oauth1("your_oauth_token")
```

### Node.js版本

```javascript
const XAuth = require('./xauth');

const x = new XAuth('your_auth_token');

// OAuth2认证
const params = {
    code_challenge: 'challenge',
    code_challenge_method: 'plain',
    client_id: 'your_client_id',
    redirect_uri: 'your_redirect_uri',
    response_type: 'code',
    scope: 'tweet.read users.read follows.read follows.write offline.access',
    state: 'your_state'
};

x.oauth2(params)
    .then(authCode => console.log('OAuth2认证码:', authCode))
    .catch(error => console.error('错误:', error));

// OAuth1认证
x.oauth1('your_oauth_token')
    .then(verifier => console.log('OAuth1验证码:', verifier))
    .catch(error => console.error('错误:', error));
```

## 安装依赖

### Go版本
```bash
go mod download
```

### Python版本
```bash
pip install -r requirements.txt
```

### Node.js版本
```bash
npm install
```

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

## 开发说明

- 使用统一的错误处理机制
- 实现请求重试逻辑
- 保持代码风格一致
- 添加适当的日志记录

## 测试

每个语言版本都提供了对应的测试文件：

- Go: `test.go`
- Python: `test.py`
- Node.js: `test.js`

运行测试：

```bash
# Go
go run test.go

# Python
python test.py

# Node.js
node test.js
```

## 贡献指南

1. Fork 项目
2. 创建特性分支
3. 提交更改
4. 推送到分支
5. 创建 Pull Request

## 许可证

MIT License
