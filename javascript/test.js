const XAuth = require('./xauth');

function buildOAuth2Params() {
    const clientId = process.env.XAUTH_CLIENT_ID;
    const redirectUri = process.env.XAUTH_REDIRECT_URI;

    if (!clientId || !redirectUri) {
        throw new Error('请先设置 XAUTH_CLIENT_ID 和 XAUTH_REDIRECT_URI 环境变量');
    }

    return {
        code_challenge: process.env.XAUTH_CODE_CHALLENGE || 'challenge',
        code_challenge_method: process.env.XAUTH_CODE_CHALLENGE_METHOD || 'plain',
        client_id: clientId,
        redirect_uri: redirectUri,
        response_type: 'code',
        scope: process.env.XAUTH_SCOPE || 'tweet.read users.read follows.read follows.write offline.access',
        state: process.env.XAUTH_STATE || 'demo-state'
    };
}

async function main() {
    const authToken = process.env.XAUTH_AUTH_TOKEN;
    const oauth1Token = process.env.XAUTH_OAUTH1_TOKEN;

    if (!authToken) {
        console.error('请先设置 XAUTH_AUTH_TOKEN 环境变量');
        process.exitCode = 1;
        return;
    }

    const x = new XAuth(authToken);

    try {
        const authCode = await x.oauth2(buildOAuth2Params());
        console.log('OAuth2 认证码:', authCode);

        if (oauth1Token) {
            const oauth1Verifier = await x.oauth1(oauth1Token);
            console.log('OAuth1 验证码:', oauth1Verifier);
        } else {
            console.log('未设置 XAUTH_OAUTH1_TOKEN，跳过 OAuth1 示例');
        }
    } catch (error) {
        console.error('错误:', error.message);
        process.exitCode = 1;
    }
}

main();
