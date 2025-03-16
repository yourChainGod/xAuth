const XAuth = require('./xauth');

async function main() {
    // 创建XAuth实例
    const x = new XAuth('5a0be24d859ad6820906bbc28b0ac9219a71489b');

    try {
        // OAuth2认证测试
        const params = {
            code_challenge: 'challenge',
            code_challenge_method: 'plain',
            client_id: 'MjdoOFVuNlJwRmxQT1VnWVBiUkw6MTpjaQ',
            redirect_uri: 'https://dapp.uxlink.io/authGateway',
            response_type: 'code',
            scope: 'tweet.read users.read follows.read follows.write offline.access',
            state: '1867096870644760576'
        };

        const authCode = await x.oauth2(params);
        console.log('OAuth2 认证码:', authCode);

        // OAuth1认证测试
        const oauth1Verifier = await x.oauth1('lSgWPQAAAAABuWQYAAABk7sKZI0');
        console.log('OAuth1 验证码:', oauth1Verifier);

    } catch (error) {
        console.error('错误:', error.message);
    }
}

main();
