/**
 * Twitter OAuth认证工具类
 * 支持OAuth1和OAuth2认证流程
 */
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

    /**
     * 初始化XAuth实例
     * @param {string} authToken - Twitter认证token
     */
    constructor(authToken) {
        if (!authToken) {
            throw new Error('authToken不能为空');
        }

        this.authToken = authToken;
        this.cookies = {
            auth_token: authToken
        };
        this.csrfToken = null;
        this.client = this._createAxiosInstance(true);
        this.client2 = this._createAxiosInstance(false);
    }

    /**
     * 创建配置好的Axios实例
     * @param {boolean} includeTwitterHeaders - 是否包含Twitter特定的headers
     * @returns {import('axios').AxiosInstance}
     * @private
     */
    _createAxiosInstance(includeTwitterHeaders = true) {
        const headers = {
            'user-agent': XAuth.USER_AGENT
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

        const client = axios.create({
            headers,
            timeout: 10000,
            maxRedirects: 5,
            validateStatus: () => true
        });

        client.interceptors.request.use((config) => {
            config.headers = config.headers || {};
            config.headers.Cookie = this._getCookieHeader();

            if (includeTwitterHeaders && this.csrfToken) {
                config.headers['x-csrf-token'] = this.csrfToken;
            }

            return config;
        });

        client.interceptors.response.use((response) => {
            this._updateCookies(response.headers?.['set-cookie']);

            if (this.cookies.ct0) {
                this.csrfToken = this.cookies.ct0;
            }

            return response;
        });

        return client;
    }

    /**
     * 获取当前Cookie头
     * @returns {string}
     * @private
     */
    _getCookieHeader() {
        return Object.entries(this.cookies)
            .filter(([, value]) => value !== undefined && value !== null && value !== '')
            .map(([key, value]) => `${key}=${value}`)
            .join('; ');
    }

    /**
     * 更新响应中的Cookie
     * @param {string[]|string|undefined} setCookieHeaders
     * @private
     */
    _updateCookies(setCookieHeaders) {
        const cookieList = Array.isArray(setCookieHeaders)
            ? setCookieHeaders
            : setCookieHeaders
                ? [setCookieHeaders]
                : [];

        for (const header of cookieList) {
            const cookiePair = String(header).split(';')[0];
            const [name, ...rest] = cookiePair.split('=');
            if (!name || rest.length === 0) {
                continue;
            }

            this.cookies[name.trim()] = rest.join('=').trim();
        }
    }

    /**
     * 带429重试的请求执行器
     * @param {() => Promise<import('axios').AxiosResponse>} requestFn
     * @returns {Promise<import('axios').AxiosResponse>}
     * @private
     */
    async _requestWithRetry(requestFn) {
        for (let attempt = 0; attempt <= XAuth.MAX_RETRIES; attempt++) {
            const response = await requestFn();
            if (response.status !== 429) {
                return response;
            }

            if (attempt === XAuth.MAX_RETRIES) {
                break;
            }

            await new Promise(resolve => setTimeout(resolve, XAuth.RETRY_INTERVAL));
        }

        throw new Error('请求频率限制，重试失败');
    }

    /**
     * 将响应体安全转换为文本
     * @param {any} data
     * @returns {string}
     * @private
     */
    _extractText(data) {
        if (typeof data === 'string') {
            return data;
        }

        if (Buffer.isBuffer(data)) {
            return data.toString('utf8');
        }

        if (data === null || data === undefined) {
            return '';
        }

        return JSON.stringify(data);
    }

    /**
     * 判断响应中是否包含redirect_uri
     * @param {any} data
     * @returns {boolean}
     * @private
     */
    _hasRedirectUri(data) {
        if (typeof data === 'string') {
            return data.includes('redirect_uri');
        }

        return Boolean(
            data &&
            typeof data === 'object' &&
            !Array.isArray(data) &&
            (Object.prototype.hasOwnProperty.call(data, 'redirect_uri') ||
             Object.prototype.hasOwnProperty.call(data, 'redirectUri'))
        );
    }

    /**
     * 获取Twitter认证token
     * @param {string} oauthToken - OAuth token
     * @returns {Promise<string>}
     */
    async getTwitterToken(oauthToken) {
        if (!oauthToken) {
            throw new Error('oauth_token不能为空');
        }

        const response = await this._requestWithRetry(() => this.client2.get('https://api.x.com/oauth/authenticate', {
            params: { oauth_token: oauthToken }
        }));

        const content = this._extractText(response.data);

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

    /**
     * 执行OAuth1认证流程
     * @param {string} oauthToken - OAuth token
     * @returns {Promise<string>}
     */
    async oauth1(oauthToken) {
        const authenticityToken = await this.getTwitterToken(oauthToken);

        const data = new URLSearchParams({
            authenticity_token: authenticityToken,
            oauth_token: oauthToken
        });

        const response = await this._requestWithRetry(() => this.client2.post(
            'https://x.com/oauth/authorize',
            data,
            {
                headers: {
                    'Content-Type': 'application/x-www-form-urlencoded'
                }
            }
        ));

        const content = this._extractText(response.data);

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

    /**
     * 获取认证码
     * @param {Object} params - 请求参数
     * @returns {Promise<string>}
     */
    async getAuthCode(params) {
        if (!params || Object.keys(params).length === 0) {
            throw new Error('参数不能为空');
        }

        const response = await this._requestWithRetry(() => this.client.get(`${XAuth.TWITTER_API_BASE}/oauth2/authorize`, {
            params
        }));

        const data = response.data;
        if (!data || typeof data !== 'object' || Array.isArray(data)) {
            throw new Error('响应格式错误');
        }

        if (data.code === 353) {
            if (this.cookies.ct0) {
                this.csrfToken = this.cookies.ct0;
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

    /**
     * 执行OAuth2认证流程
     * @param {Object} params - 请求参数
     * @returns {Promise<string>}
     */
    async oauth2(params) {
        const authCode = await this.getAuthCode(params);

        const data = new URLSearchParams({
            approval: 'true',
            code: authCode
        });

        const response = await this._requestWithRetry(() => this.client.post(
            `${XAuth.TWITTER_API_BASE}/oauth2/authorize`,
            data,
            {
                headers: {
                    'Content-Type': 'application/x-www-form-urlencoded',
                    'Accept': 'application/json, text/plain, */*'
                }
            }
        ));

        if (!this._hasRedirectUri(response.data)) {
            throw new Error('响应中未找到redirect_uri');
        }

        return authCode;
    }
}

module.exports = XAuth;
