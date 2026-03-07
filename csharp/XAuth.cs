using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace XAuth
{
    /// <summary>
    /// Twitter OAuth认证工具类
    /// 支持OAuth1和OAuth2认证流程
    /// </summary>
    public sealed class XAuthClientOptions
    {
        public string AuthenticateUrl { get; init; } = "https://api.x.com/oauth/authenticate";
        public string OAuth1AuthorizeUrl { get; init; } = "https://x.com/oauth/authorize";
        public string ApiBase { get; init; } = "https://twitter.com/i/api/2";
        public string UserAgent { get; init; } = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36";
        public string Authorization { get; init; } = "Bearer AAAAAAAAAAAAAAAAAAAAANRILgAAAAAAnNwIzUejRCOuH5E6I8xnZz4puTs%3D1Zv7ttfk8LF81IUq16cHjhLTvJu4FA33AGWWjCpTnA";
        public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(10);
        public int MaxRetries { get; init; } = 3;
        public int RetryIntervalMs { get; init; } = 1000;
    }

    public class XAuthClient : IDisposable
    {
        private const string TwitterAuthority = "twitter.com";
        private const string TwitterOrigin = "https://twitter.com";
        private const string TwitterApiBaseDefault = "https://twitter.com/i/api/2";
        private const string UserAgentDefault = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36";
        private const string AuthorizationDefault = "Bearer AAAAAAAAAAAAAAAAAAAAANRILgAAAAAAnNwIzUejRCOuH5E6I8xnZz4puTs%3D1Zv7ttfk8LF81IUq16cHjhLTvJu4FA33AGWWjCpTnA";

        private static readonly Dictionary<int, string> AccountState = new Dictionary<int, string>
        {
            { 32, "Bad Token" },
            { 64, "SUSPENDED" },
            { 141, "SUSPENDED" },
            { 326, "LOCKED" }
        };

        private readonly CookieContainer _cookieContainer;
        private readonly HttpClient _client;
        private readonly HttpClient _client2;
        private readonly string _authenticateUrl;
        private readonly string _oauth1AuthorizeUrl;
        private readonly string _apiBase;
        private readonly int _maxRetries;
        private readonly int _retryIntervalMs;
        private string _csrfToken = string.Empty;
        private string _lastError = string.Empty;

        /// <summary>
        /// 初始化XAuth实例
        /// </summary>
        /// <param name="authToken">Twitter认证token</param>
        /// <exception cref="ArgumentNullException">当auth_token为空时抛出</exception>
        public XAuthClient(string authToken) : this(authToken, new XAuthClientOptions())
        {
        }

        public XAuthClient(string authToken, XAuthClientOptions options)
        {
            if (string.IsNullOrEmpty(authToken))
            {
                throw new ArgumentNullException(nameof(authToken), "auth_token不能为空");
            }

            options ??= new XAuthClientOptions();
            _authenticateUrl = options.AuthenticateUrl;
            _oauth1AuthorizeUrl = options.OAuth1AuthorizeUrl;
            _apiBase = options.ApiBase;
            _maxRetries = options.MaxRetries;
            _retryIntervalMs = options.RetryIntervalMs;

            _cookieContainer = new CookieContainer();
            AddAuthCookies(authToken);
            _client = CreateHttpClient(true, options);
            _client2 = CreateHttpClient(false, options);
        }

        /// <summary>
        /// 获取最后一次错误信息
        /// </summary>
        public string LastError => _lastError;

        /// <summary>
        /// 创建配置好的HttpClient实例
        /// </summary>
        /// <param name="includeTwitterHeaders">是否包含Twitter特定的headers</param>
        /// <returns>配置好的HttpClient实例</returns>
        private HttpClient CreateHttpClient(bool includeTwitterHeaders = true, XAuthClientOptions? options = null)
        {
            options ??= new XAuthClientOptions();

            var handler = new HttpClientHandler
            {
                UseCookies = true,
                CookieContainer = _cookieContainer,
                AllowAutoRedirect = true
            };

            var client = new HttpClient(handler)
            {
                Timeout = options.Timeout
            };

            client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", options.UserAgent);

            if (includeTwitterHeaders)
            {
                client.DefaultRequestHeaders.TryAddWithoutValidation("authority", TwitterAuthority);
                client.DefaultRequestHeaders.TryAddWithoutValidation("origin", TwitterOrigin);
                client.DefaultRequestHeaders.TryAddWithoutValidation("x-twitter-auth-type", "OAuth2Session");
                client.DefaultRequestHeaders.TryAddWithoutValidation("x-twitter-active-user", "yes");
                client.DefaultRequestHeaders.TryAddWithoutValidation("authorization", options.Authorization);
            }

            return client;
        }

        /// <summary>
        /// 添加跨域可用的auth_token cookie
        /// </summary>
        /// <param name="authToken">认证token</param>
        private void AddAuthCookies(string authToken)
        {
            foreach (var rawUri in new[] { "https://twitter.com", "https://x.com", _apiBase, _authenticateUrl, _oauth1AuthorizeUrl })
            {
                if (!Uri.TryCreate(rawUri, UriKind.Absolute, out var uri) || string.IsNullOrEmpty(uri.Host))
                {
                    continue;
                }

                _cookieContainer.Add(uri, new Cookie("auth_token", authToken, "/", uri.Host));
            }
        }

        /// <summary>
        /// 发送带429重试的HTTP请求
        /// </summary>
        /// <param name="sendAsync">请求工厂</param>
        /// <returns>最终HTTP响应</returns>
        private async Task<HttpResponseMessage> SendWithRetryAsync(Func<Task<HttpResponseMessage>> sendAsync)
        {
            for (var attempt = 0; attempt <= _maxRetries; attempt++)
            {
                var response = await sendAsync();
                if (response.StatusCode != HttpStatusCode.TooManyRequests)
                {
                    return response;
                }

                response.Dispose();

                if (attempt == _maxRetries)
                {
                    _lastError = "请求频率限制，重试失败";
                    throw new XAuthException(_lastError);
                }

                await Task.Delay(_retryIntervalMs);
            }

            _lastError = "请求频率限制，重试失败";
            throw new XAuthException(_lastError);
        }

        /// <summary>
        /// 更新CSRF token
        /// </summary>
        /// <param name="ct0">ct0 cookie值</param>
        private void UpdateCsrfToken(string ct0)
        {
            if (string.IsNullOrEmpty(ct0))
            {
                return;
            }

            _csrfToken = ct0;
            if (_client.DefaultRequestHeaders.Contains("x-csrf-token"))
            {
                _client.DefaultRequestHeaders.Remove("x-csrf-token");
            }

            _client.DefaultRequestHeaders.TryAddWithoutValidation("x-csrf-token", ct0);
        }

        /// <summary>
        /// 从Cookie容器中获取指定Cookie值
        /// </summary>
        /// <param name="name">Cookie名称</param>
        /// <returns>Cookie值</returns>
        private string? GetCookieValue(string name)
        {
            foreach (var rawUri in new[] { "https://twitter.com", "https://x.com", "https://api.x.com", _apiBase, _authenticateUrl, _oauth1AuthorizeUrl })
            {
                if (!Uri.TryCreate(rawUri, UriKind.Absolute, out var uri))
                {
                    continue;
                }

                foreach (Cookie cookie in _cookieContainer.GetCookies(uri))
                {
                    if (string.Equals(cookie.Name, name, StringComparison.Ordinal))
                    {
                        return cookie.Value;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// 构造查询字符串
        /// </summary>
        /// <param name="parameters">参数集合</param>
        /// <returns>查询字符串</returns>
        private static string BuildQueryString(Dictionary<string, string> parameters)
        {
            return string.Join("&", parameters.Select(param =>
                $"{Uri.EscapeDataString(param.Key)}={Uri.EscapeDataString(param.Value)}"));
        }

        /// <summary>
        /// 解析JSON文档
        /// </summary>
        /// <param name="content">响应体</param>
        /// <returns>JSON文档</returns>
        private JsonDocument ParseJson(string content)
        {
            try
            {
                return JsonDocument.Parse(content);
            }
            catch (JsonException)
            {
                _lastError = "响应不是有效的JSON格式";
                throw new XAuthException(_lastError);
            }
        }

        /// <summary>
        /// 判断响应中是否包含redirect_uri
        /// </summary>
        /// <param name="content">响应内容</param>
        /// <returns>是否存在redirect_uri</returns>
        private static bool HasRedirectUri(string content)
        {
            if (string.IsNullOrEmpty(content))
            {
                return false;
            }

            if (content.Contains("redirect_uri", StringComparison.Ordinal))
            {
                return true;
            }

            try
            {
                using var data = JsonDocument.Parse(content);
                return data.RootElement.TryGetProperty("redirect_uri", out _) ||
                       data.RootElement.TryGetProperty("redirectUri", out _);
            }
            catch (JsonException)
            {
                return false;
            }
        }

        /// <summary>
        /// 获取Twitter认证token
        /// </summary>
        /// <param name="oauthToken">OAuth token</param>
        /// <returns>认证token字符串</returns>
        /// <exception cref="XAuthException">当token无效或响应格式不正确时</exception>
        public async Task<string> GetTwitterTokenAsync(string oauthToken)
        {
            if (string.IsNullOrEmpty(oauthToken))
            {
                _lastError = "oauth_token不能为空";
                throw new XAuthException(_lastError);
            }

            using var response = await SendWithRetryAsync(() =>
                _client2.GetAsync($"{_authenticateUrl}?oauth_token={Uri.EscapeDataString(oauthToken)}"));

            var content = await response.Content.ReadAsStringAsync();

            if (!content.Contains("authenticity_token", StringComparison.Ordinal))
            {
                if (content.Contains("The request token for this page is invalid", StringComparison.Ordinal))
                {
                    _lastError = "请求oauth_token无效";
                    throw new XAuthException(_lastError);
                }

                _lastError = "响应中未找到authenticity_token";
                throw new XAuthException(_lastError);
            }

            string? token = null;
            string[] tokenMarkers =
            {
                "name=\"authenticity_token\" value=\"",
                "name=\"authenticity_token\" type=\"hidden\" value=\""
            };

            foreach (var marker in tokenMarkers)
            {
                var markerIndex = content.IndexOf(marker, StringComparison.Ordinal);
                if (markerIndex < 0)
                {
                    continue;
                }

                var startIndex = markerIndex + marker.Length;
                var endIndex = content.IndexOf('"', startIndex);
                if (endIndex > startIndex)
                {
                    token = content.Substring(startIndex, endIndex - startIndex);
                    break;
                }
            }

            if (string.IsNullOrEmpty(token))
            {
                _lastError = "获取到的authenticity_token为空";
                throw new XAuthException(_lastError);
            }

            return token;
        }

        /// <summary>
        /// 执行OAuth1认证流程
        /// </summary>
        /// <param name="oauthToken">OAuth token</param>
        /// <returns>OAuth验证码</returns>
        /// <exception cref="XAuthException">当认证失败时</exception>
        public async Task<string> OAuth1Async(string oauthToken)
        {
            var authenticityToken = await GetTwitterTokenAsync(oauthToken);

            using var response = await SendWithRetryAsync(() =>
                _client2.PostAsync(
                    _oauth1AuthorizeUrl,
                    new FormUrlEncodedContent(new[]
                    {
                        new KeyValuePair<string, string>("authenticity_token", authenticityToken),
                        new KeyValuePair<string, string>("oauth_token", oauthToken)
                    })));

            var responseContent = await response.Content.ReadAsStringAsync();

            if (!responseContent.Contains("oauth_verifier", StringComparison.Ordinal))
            {
                if (responseContent.Contains("This account is suspended.", StringComparison.Ordinal))
                {
                    _lastError = "该账户已被封禁";
                    throw new XAuthException(_lastError);
                }

                _lastError = "未找到oauth_verifier";
                throw new XAuthException(_lastError);
            }

            var match = Regex.Match(responseContent, "oauth_verifier=([^\"&]+)");
            if (!match.Success || string.IsNullOrEmpty(match.Groups[1].Value))
            {
                _lastError = "获取到的oauth_verifier为空";
                throw new XAuthException(_lastError);
            }

            return match.Groups[1].Value;
        }

        /// <summary>
        /// 获取认证码
        /// </summary>
        /// <param name="parameters">请求参数</param>
        /// <returns>认证码</returns>
        /// <exception cref="XAuthException">当参数无效或响应格式不正确时</exception>
        public async Task<string> GetAuthCodeAsync(Dictionary<string, string> parameters)
        {
            if (parameters == null || parameters.Count == 0)
            {
                _lastError = "参数不能为空";
                throw new XAuthException(_lastError);
            }

            var queryString = BuildQueryString(parameters);
            var requestUri = string.IsNullOrEmpty(queryString)
                ? $"{_apiBase}/oauth2/authorize"
                : $"{_apiBase}/oauth2/authorize?{queryString}";

            using var response = await SendWithRetryAsync(() => _client.GetAsync(requestUri));
            var content = await response.Content.ReadAsStringAsync();

            using var data = ParseJson(content);

            if (data.RootElement.TryGetProperty("code", out var codeElement) && codeElement.GetInt32() == 353)
            {
                var ct0 = GetCookieValue("ct0");
                if (!string.IsNullOrEmpty(ct0))
                {
                    UpdateCsrfToken(ct0);
                    return await GetAuthCodeAsync(parameters);
                }

                _lastError = "未找到ct0 cookie";
                throw new XAuthException(_lastError);
            }

            if (data.RootElement.TryGetProperty("errors", out var errorsElement) &&
                errorsElement.ValueKind == JsonValueKind.Array &&
                errorsElement.GetArrayLength() > 0)
            {
                var error = errorsElement.EnumerateArray().First();
                if (error.TryGetProperty("code", out var errorCodeElement))
                {
                    var errorCode = errorCodeElement.GetInt32();
                    if (AccountState.TryGetValue(errorCode, out var state))
                    {
                        _lastError = $"token状态错误: {state}";
                        throw new XAuthException(_lastError);
                    }
                }
            }

            if (!data.RootElement.TryGetProperty("auth_code", out var authCodeElement) ||
                authCodeElement.ValueKind == JsonValueKind.Null ||
                string.IsNullOrEmpty(authCodeElement.GetString()))
            {
                _lastError = "响应中未找到auth_code";
                throw new XAuthException(_lastError);
            }

            return authCodeElement.GetString()!;
        }

        /// <summary>
        /// 执行OAuth2认证流程
        /// </summary>
        /// <param name="parameters">请求参数</param>
        /// <returns>认证码</returns>
        /// <exception cref="XAuthException">当认证失败时</exception>
        public async Task<string> OAuth2Async(Dictionary<string, string> parameters)
        {
            var authCode = await GetAuthCodeAsync(parameters);

            var ct0 = GetCookieValue("ct0");
            if (!string.IsNullOrEmpty(ct0))
            {
                UpdateCsrfToken(ct0);
            }

            using var response = await SendWithRetryAsync(() =>
                _client.PostAsync(
                    $"{_apiBase}/oauth2/authorize",
                    new FormUrlEncodedContent(new[]
                    {
                        new KeyValuePair<string, string>("approval", "true"),
                        new KeyValuePair<string, string>("code", authCode)
                    })));

            var responseContent = await response.Content.ReadAsStringAsync();
            if (!HasRedirectUri(responseContent))
            {
                _lastError = "响应中未找到redirect_uri";
                throw new XAuthException(_lastError);
            }

            return authCode;
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            _client?.Dispose();
            _client2?.Dispose();
        }
    }

    /// <summary>
    /// Twitter OAuth认证异常
    /// </summary>
    public class XAuthException : Exception
    {
        /// <summary>
        /// 创建一个新的XAuthException实例
        /// </summary>
        /// <param name="message">异常消息</param>
        public XAuthException(string message) : base(message)
        {
        }

        /// <summary>
        /// 创建一个新的XAuthException实例
        /// </summary>
        /// <param name="message">异常消息</param>
        /// <param name="innerException">内部异常</param>
        public XAuthException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
