# [hdd.cm推特低至1毛5](https://hdd.cm/)

# XAuth JavaScript版本

这是Twitter OAuth认证库的JavaScript实现版本，支持OAuth1和OAuth2认证流程。

## 功能特点

- 支持OAuth1和OAuth2认证流程
- 自动处理CSRF token
- 内置请求重试机制
- 完善的错误处理
- Promise-based API

## 文件结构

- `xauth.js` - 主要实现文件，包含XAuth类
- `test.js` - 测试程序
- `package.json` - 项目配置和依赖项

## 系统要求

- Node.js 12.0+
- 依赖包：axios

## 安装依赖

```bash
npm install
```

## 使用方法

### 初始化

```javascript
const XAuth = require('./xauth');

const x = new XAuth('your_auth_token');
```

### OAuth2认证

```javascript
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
```

### OAuth1认证

```javascript
// OAuth1认证
x.oauth1('your_oauth_token')
    .then(verifier => console.log('OAuth1验证码:', verifier))
    .catch(error => console.error('错误:', error));
```

### 使用async/await

```javascript
async function authenticate() {
    try {
        // OAuth2认证
        const authCode = await x.oauth2(params);
        console.log('OAuth2认证码:', authCode);
        
        // OAuth1认证
        const verifier = await x.oauth1('your_oauth_token');
        console.log('OAuth1验证码:', verifier);
    } catch (error) {
        console.error('认证失败:', error);
    }
}

authenticate();
```

## 错误处理

库使用Promise来处理异步操作，您可以使用catch方法或try/catch（与async/await一起使用）来捕获错误：

```javascript
// 使用Promise
x.oauth2(params)
    .then(authCode => console.log('成功:', authCode))
    .catch(error => console.error('失败:', error));

// 使用async/await
try {
    const authCode = await x.oauth2(params);
    console.log('成功:', authCode);
} catch (error) {
    console.error('失败:', error);
}
```

## 运行测试

```bash
node test.js
```

## 注意事项

1. 确保提供有效的auth_token
2. 注意API请求限制
3. 正确处理错误响应
4. 遵守Twitter API使用条款 