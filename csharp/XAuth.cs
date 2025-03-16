using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;

namespace XAuth
{
    /// <summary>
    /// Twitter OAuth认证工具类
    /// 支持OAuth1和OAuth2认证流程
    /// </summary>
    public class XAuthClient : IDisposable
    {
        private const string TwitterAuthority = "twitter.com";
        private const string TwitterOrigin = "https://twitter.com";
        private const string TwitterApiBase = "https://twitter.com/i/api/2";
        private const string UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36";
        private const string Authorization = "Bearer AAAAAAAAAAAAAAAAAAAAANRILgAAAAAAnNwIzUejRCOuH5E6I8xnZz4puTs%3D1Zv7ttfk8LF81IUq16cHjhLTvJu4FA33AGWWjCpTnA";
        private const int MaxRetries = 3;
        private const int RetryInterval = 1000; // milliseconds

        private static readonly Dictionary<int, string> AccountState = new Dictionary<int, string>
        {
            { 32, "Bad Token" },
            { 64, "SUSPENDED" },
            { 141, "SUSPENDED" },
            { 326, "LOCKED" }
        };

        private readonly string _authToken;
        private readonly HttpClient _client;
        private readonly HttpClient _client2;
        private string _lastError;

        /// <summary>
        /// 初始化XAuth实例
        /// </summary>
        /// <param name="authToken">Twitter认证token</param>
        /// <exception cref="ArgumentNullException">当auth_token为空时抛出</exception>
        public XAuthClient(string authToken)
        {
            if (string.IsNullOrEmpty(authToken))
            {
                throw new ArgumentNullException(nameof(authToken), "auth_token不能为空");
            }

            _authToken = authToken;

            // 创建HTTP客户端
            _client = CreateHttpClient(true);
            _client2 = CreateHttpClient(false);
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
        private HttpClient CreateHttpClient(bool includeTwitterHeaders = true)
        {
            var handler = new HttpClientHandler
            {
                UseCookies = true,
                AllowAutoRedirect = true
            };

            var client = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(10)
            };

            // 设置通用头部
            client.DefaultRequestHeaders.Add("User-Agent", UserAgent);
            client.DefaultRequestHeaders.Add("Cookie", $"auth_token={_authToken}");

            if (includeTwitterHeaders)
            {
                client.DefaultRequestHeaders.Add("authority", TwitterAuthority);
                client.DefaultRequestHeaders.Add("origin", TwitterOrigin);
                client.DefaultRequestHeaders.Add("x-twitter-auth-type", "OAuth2Session");
                client.DefaultRequestHeaders.Add("x-twitter-active-user", "yes");
                client.DefaultRequestHeaders.Add("authorization", Authorization);
            }

            return client;
        }

        /// <summary>
        /// 处理响应状态
        /// </summary>
        /// <param name="response">HTTP响应</param>
        /// <param name="retryFunc">重试函数</param>
        /// <returns>如果需要重试，返回重试函数的结果</returns>
        private async Task<T> HandleResponseAsync<T>(HttpResponseMessage response, Func<Task<T>> retryFunc)
        {
            if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            {
                await Task.Delay(RetryInterval);
                if (retryFunc != null)
                {
                    return await retryFunc();
                }
                throw new XAuthException("请求频率限制，重试失败");
            }

            return default;
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

            var response = await _client2.GetAsync($"https://api.x.com/oauth/authenticate?oauth_token={oauthToken}");
            await HandleResponseAsync(response, () => GetTwitterTokenAsync(oauthToken));
            var content = await response.Content.ReadAsStringAsync();

            if (!content.Contains("authenticity_token"))
            {
                if (content.Contains("The request token for this page is invalid"))
                {
                    _lastError = "请求oauth_token无效";
                    throw new XAuthException(_lastError);
                }
                _lastError = "响应中未找到authenticity_token";
                throw new XAuthException(_lastError);
            }

            string token = null;
            string[] tokenMarkers = {
                "name=\"authenticity_token\" value=\"",
                "name=\"authenticity_token\" type=\"hidden\" value=\""
            };

            foreach (var marker in tokenMarkers)
            {
                if (content.Contains(marker))
                {
                    int startIndex = content.IndexOf(marker) + marker.Length;
                    int endIndex = content.IndexOf("\"", startIndex);
                    if (startIndex >= 0 && endIndex > startIndex)
                    {
                        token = content.Substring(startIndex, endIndex - startIndex);
                        break;
                    }
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
            string authenticityToken = await GetTwitterTokenAsync(oauthToken);

            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("authenticity_token", authenticityToken),
                new KeyValuePair<string, string>("oauth_token", oauthToken)
            });

            var response = await _client2.PostAsync("https://x.com/oauth/authorize", content);
            await HandleResponseAsync(response, () => OAuth1Async(oauthToken));
            var responseContent = await response.Content.ReadAsStringAsync();

            if (!responseContent.Contains("oauth_verifier"))
            {
                if (responseContent.Contains("This account is suspended."))
                {
                    _lastError = "该账户已被封禁";
                    throw new XAuthException(_lastError);
                }
                _lastError = "未找到oauth_verifier";
                throw new XAuthException(_lastError);
            }

            var match = Regex.Match(responseContent, "oauth_verifier=([^\"&]+)");
            if (match.Success)
            {
                string verifier = match.Groups[1].Value;
                if (string.IsNullOrEmpty(verifier))
                {
                    _lastError = "获取到的oauth_verifier为空";
                    throw new XAuthException(_lastError);
                }
                return verifier;
            }
            else
            {
                _lastError = "未找到oauth_verifier";
                throw new XAuthException(_lastError);
            }
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

            var uriBuilder = new UriBuilder($"{TwitterApiBase}/oauth2/authorize");
            var query = HttpUtility.ParseQueryString(string.Empty);
            foreach (var param in parameters)
            {
                query[param.Key] = param.Value;
            }
            uriBuilder.Query = query.ToString();

            var request = new HttpRequestMessage(HttpMethod.Get, uriBuilder.Uri);
            foreach (var header in _client.DefaultRequestHeaders)
            {
                request.Headers.Add(header.Key, header.Value);
            }

            var response = await _client.SendAsync(request);
            await HandleResponseAsync(response, () => GetAuthCodeAsync(parameters));
            var content = await response.Content.ReadAsStringAsync();

            JsonDocument data;
            try
            {
                data = JsonDocument.Parse(content);
            }
            catch (JsonException)
            {
                _lastError = "响应不是有效的JSON格式";
                throw new XAuthException(_lastError);
            }

            // 处理CSRF token
            if (data.RootElement.TryGetProperty("code", out var codeElement) && codeElement.GetInt32() == 353)
            {
                string ct0 = null;
                if (response.Headers.TryGetValues("Set-Cookie", out var cookies))
                {
                    foreach (var cookie in cookies)
                    {
                        if (cookie.StartsWith("ct0="))
                        {
                            ct0 = cookie.Split(';')[0].Split('=')[1];
                            break;
                        }
                    }
                }

                if (!string.IsNullOrEmpty(ct0))
                {
                    // 重新发起请求，带上CSRF token
                    var newRequest = new HttpRequestMessage(HttpMethod.Get, uriBuilder.Uri);
                    foreach (var header in _client.DefaultRequestHeaders)
                    {
                        newRequest.Headers.Add(header.Key, header.Value);
                    }
                    newRequest.Headers.Add("x-csrf-token", ct0);

                    var newResponse = await _client.SendAsync(newRequest);
                    await HandleResponseAsync(newResponse, () => GetAuthCodeAsync(parameters));
                    var newContent = await newResponse.Content.ReadAsStringAsync();

                    try
                    {
                        data = JsonDocument.Parse(newContent);
                    }
                    catch (JsonException)
                    {
                        _lastError = "响应不是有效的JSON格式";
                        throw new XAuthException(_lastError);
                    }
                }
                else
                {
                    _lastError = "未找到ct0 cookie";
                    throw new XAuthException(_lastError);
                }
            }

            // 检查错误
            if (data.RootElement.TryGetProperty("errors", out var errorsElement) && errorsElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var error in errorsElement.EnumerateArray())
                {
                    if (error.TryGetProperty("code", out var errorCodeElement))
                    {
                        int errorCode = errorCodeElement.GetInt32();
                        if (AccountState.TryGetValue(errorCode, out string state))
                        {
                            _lastError = $"token状态错误: {state}";
                            throw new XAuthException(_lastError);
                        }
                    }
                }
            }

            // 获取auth_code
            if (!data.RootElement.TryGetProperty("auth_code", out var authCodeElement) || authCodeElement.ValueKind == JsonValueKind.Null)
            {
                _lastError = "响应中未找到auth_code";
                throw new XAuthException(_lastError);
            }

            return authCodeElement.GetString();
        }

        /// <summary>
        /// 执行OAuth2认证流程
        /// </summary>
        /// <param name="parameters">请求参数</param>
        /// <returns>认证码</returns>
        /// <exception cref="XAuthException">当认证失败时</exception>
        public async Task<string> OAuth2Async(Dictionary<string, string> parameters)
        {
            string authCode = await GetAuthCodeAsync(parameters);

            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("approval", "true"),
                new KeyValuePair<string, string>("code", authCode)
            });

            var request = new HttpRequestMessage(HttpMethod.Post, $"{TwitterApiBase}/oauth2/authorize")
            {
                Content = content
            };

            foreach (var header in _client.DefaultRequestHeaders)
            {
                request.Headers.Add(header.Key, header.Value);
            }

            // 获取ct0 cookie并设置CSRF token
            var cookies = _client.DefaultRequestHeaders.GetValues("Cookie");
            foreach (var cookie in cookies)
            {
                var match = Regex.Match(cookie, "ct0=([^;]+)");
                if (match.Success)
                {
                    request.Headers.Add("x-csrf-token", match.Groups[1].Value);
                    break;
                }
            }

            request.Headers.Add("Content-Type", "application/x-www-form-urlencoded");
            request.Headers.Add("Accept", "application/json, text/plain, */*");

            var response = await _client.SendAsync(request);
            await HandleResponseAsync(response, () => OAuth2Async(parameters));
            var responseContent = await response.Content.ReadAsStringAsync();

            if (!responseContent.Contains("redirect_uri"))
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