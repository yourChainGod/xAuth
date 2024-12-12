const axios = require('axios');

class XAuth {
    static TWITTER_AUTHORITY = 'twitter.com';
    static TWITTER_ORIGIN = 'https://twitter.com';
    static TWITTER_API_BASE = 'https://twitter.com/i/api/2';
    static USER_AGENT = 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36';
    static AUTHORIZATION = 'Bearer AAAAAAAAAAAAAAAAAAAAANRILgAAAAAAnNwIzUejRCOuH5E6I8xnZz4puTs%3D1Zv7ttfk8LF81IUq16cHjhLTvJu4FA33AGWWjCpTnA';
    static MAX_RETRIES = 3;
    static RETRY_INTERVAL = 1000; // milliseconds

    static ACCOUNT_STATE = {
        32: 'Bad Token',
        64: 'SUSPENDED',
        141: 'SUSPENDED',
        326: 'LOCKED'
    };

    constructor(authToken) {
        this.authToken = authToken;
        this.client = this._createAxiosInstance(true);
        this.client2 = this._createAxiosInstance(false);
    }

    _createAxiosInstance(includeTwitterHeaders = true) {
        const headers = {
            'user-agent': XAuth.USER_AGENT,
            'Cookie': `auth_token=${this.authToken}`
        };

        if (includeTwitterHeaders) {
            Object.assign(headers, {
                'authority': XAuth.TWITTER_AUTHORITY,
                'origin': XAuth.TWITTER_ORIGIN,
                'x-twitter-auth-type': 'OAuth2Session',
                'x-twitter-active-user': 'yes',
                'authorization': XAuth.AUTHORIZATION
            });
        }

        return axios.create({
            headers,
            timeout: 10000,
            validateStatus: null
        });
    }

    async _handleResponse(response, retryFunc) {
        if (response.status === 429) {
            await new Promise(resolve => setTimeout(resolve, XAuth.RETRY_INTERVAL));
            if (retryFunc) {
                return await retryFunc();
            }
        }
    }

    async getTwitterToken(oauthToken) {
        if (!oauthToken) {
            throw new Error('oauth_token不能为空');
        }

        const response = await this.client2.get('https://api.x.com/oauth/authenticate', {
            params: { oauth_token: oauthToken }
        });

        await this._handleResponse(response);
        const content = response.data;

        if (!content.includes('authenticity_token')) {
            if (content.includes('The request token for this page is invalid')) {
                throw new Error('请求oauth_token无效');
            }
            throw new Error('响应中未找到authenticity_token');
        }

        let token = null;
        const tokenMarkers = [
            'name="authenticity_token" value="',
            'name="authenticity_token" type="hidden" value="'
        ];

        for (const marker of tokenMarkers) {
            if (content.includes(marker)) {
                token = content.split(marker)[1].split('"')[0];
                break;
            }
        }

        if (!token) {
            throw new Error('获取到的authenticity_token为空');
        }

        return token;
    }

    async oauth1(oauthToken) {
        const authenticityToken = await this.getTwitterToken(oauthToken);

        const data = new URLSearchParams({
            authenticity_token: authenticityToken,
            oauth_token: oauthToken
        });

        const response = await this.client2.post('https://x.com/oauth/authorize', data);
        await this._handleResponse(response);

        const content = response.data;

        if (!content.includes('oauth_verifier')) {
            if (content.includes('This account is suspended.')) {
                throw new Error('该账户已被封禁');
            }
            throw new Error('未找到oauth_verifier');
        }

        const verifier = content.split('oauth_verifier=')[1].split('"')[0];
        if (!verifier) {
            throw new Error('获取到的oauth_verifier为空');
        }

        return verifier;
    }

    async getAuthCode(params) {
        if (!params || Object.keys(params).length === 0) {
            throw new Error('参数不能为空');
        }

        const response = await this.client.get(`${XAuth.TWITTER_API_BASE}/oauth2/authorize`, {
            params,
            headers: this.client.defaults.headers // 确保使用当前的headers
        });

        await this._handleResponse(response, () => this.getAuthCode(params));
        console.log('响应数据:', response.data);

        if (!response.data || typeof response.data !== 'object') {
            throw new Error('响应格式错误');
        }

        const data = response.data;

        if (data.code === 353) {
            const ct0Cookie = response.headers['set-cookie']?.find(cookie => cookie.startsWith('ct0='));
            if (ct0Cookie) {
                const ct0 = ct0Cookie.split(';')[0].split('=')[1];
                this.client.defaults.headers['x-csrf-token'] = ct0;
                return await this.getAuthCode(params);
            }
            throw new Error('未找到ct0 cookie');
        }

        if (data.errors && data.errors.length > 0) {
            const errorCode = data.errors[0].code;
            if (XAuth.ACCOUNT_STATE[errorCode]) {
                throw new Error(`token状态错误: ${XAuth.ACCOUNT_STATE[errorCode]}`);
            }
        }

        if (!data.auth_code) {
            throw new Error('响应中未找到auth_code');
        }

        return data.auth_code;
    }

    async oauth2(params) {
        const authCode = await this.getAuthCode(params);

        const data = new URLSearchParams({
            approval: 'true',
            code: authCode
        });

        const response = await this.client.post(
            `${XAuth.TWITTER_API_BASE}/oauth2/authorize`,
            data,
            {
                headers: {
                    ...this.client.defaults.headers, // 包含所有现有的headers
                    'Content-Type': 'application/x-www-form-urlencoded',
                    'Accept': 'application/json, text/plain, */*'
                },
                maxRedirects: 5,
                validateStatus: null
            }
        );

        await this._handleResponse(response, () => this.oauth2(params));

        if (!response.data.includes('redirect_uri')) {
            throw new Error('响应中未找到redirect_uri');
        }

        return authCode;
    }
}

module.exports = XAuth;
