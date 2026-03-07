const assert = require('assert');
const XAuth = require('./xauth');

async function testOAuth2UsesCSRFAndReturnsAuthCode() {
    const x = new XAuth('demo-token');
    let getCalls = 0;

    x.client = {
        get: async (_url, config) => {
            getCalls += 1;
            if (getCalls === 1) {
                return {
                    status: 200,
                    data: { code: 353 },
                    headers: { 'set-cookie': ['ct0=csrf-123; Path=/'] }
                };
            }

            assert.strictEqual(x.csrfToken, 'csrf-123');
            assert.strictEqual(config.params.client_id, 'client-1');
            return {
                status: 200,
                data: { auth_code: 'AUTH-CODE-1' },
                headers: {}
            };
        },
        post: async (_url, data, _config) => {
            assert.strictEqual(x.csrfToken, 'csrf-123');
            assert.strictEqual(data.get('code'), 'AUTH-CODE-1');
            return {
                status: 200,
                data: { redirect_uri: 'https://example.com/callback' },
                headers: {}
            };
        }
    };

    x._requestWithRetry = async (requestFn) => {
        for (let attempt = 0; attempt <= XAuth.MAX_RETRIES; attempt += 1) {
            const response = await requestFn();
            x._updateCookies(response.headers?.['set-cookie']);
            if (x.cookies.ct0) {
                x.csrfToken = x.cookies.ct0;
            }
            if (response.status !== 429) {
                return response;
            }
        }
        throw new Error('请求频率限制，重试失败');
    };

    const code = await x.oauth2({
        client_id: 'client-1',
        redirect_uri: 'https://example.com/callback',
        response_type: 'code',
        code_challenge: 'challenge',
        code_challenge_method: 'plain'
    });

    assert.strictEqual(code, 'AUTH-CODE-1');
    assert.strictEqual(getCalls, 2);
}

async function testOAuth1ParsesVerifier() {
    const x = new XAuth('demo-token');

    x.client2 = {
        get: async (_url, config) => {
            assert.strictEqual(config.params.oauth_token, 'oauth-token');
            return {
                status: 200,
                data: '<input name="authenticity_token" type="hidden" value="token-456">',
                headers: {}
            };
        },
        post: async (_url, data, _config) => {
            assert.strictEqual(data.get('authenticity_token'), 'token-456');
            return {
                status: 200,
                data: 'oauth_verifier=verifier-789"',
                headers: {}
            };
        }
    };

    x._requestWithRetry = async (requestFn) => requestFn();

    const verifier = await x.oauth1('oauth-token');
    assert.strictEqual(verifier, 'verifier-789');
}

async function testRetryOn429() {
    const x = new XAuth('demo-token');
    let attempts = 0;

    x.client2 = {
        get: async () => {
            attempts += 1;
            if (attempts === 1) {
                return { status: 429, data: '', headers: {} };
            }
            return {
                status: 200,
                data: '<input name="authenticity_token" value="token-123">',
                headers: {}
            };
        }
    };

    x._requestWithRetry = async (requestFn) => {
        for (let attempt = 0; attempt <= XAuth.MAX_RETRIES; attempt += 1) {
            const response = await requestFn();
            if (response.status !== 429) {
                return response;
            }
        }
        throw new Error('请求频率限制，重试失败');
    };

    const token = await x.getTwitterToken('oauth-token');
    assert.strictEqual(token, 'token-123');
    assert.strictEqual(attempts, 2);
}

async function main() {
    await testOAuth2UsesCSRFAndReturnsAuthCode();
    await testOAuth1ParsesVerifier();
    await testRetryOn429();
    console.log('mock tests passed');
}

main().catch((error) => {
    console.error(error);
    process.exit(1);
});
