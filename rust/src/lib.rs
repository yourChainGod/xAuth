use reqwest::{
    cookie::{CookieStore, Jar},
    header::{HeaderMap, HeaderValue, AUTHORIZATION},
    Client, ClientBuilder, StatusCode, Url,
};
use serde_json::Value;
use std::{collections::HashMap, future::Future, sync::Arc, time::Duration};
use thiserror::Error;

/// Twitter OAuth认证错误
#[derive(Error, Debug)]
pub enum XAuthError {
    #[error("HTTP请求错误: {0}")]
    RequestError(#[from] reqwest::Error),

    #[error("参数错误: {0}")]
    ParamError(String),

    #[error("认证错误: {0}")]
    AuthError(String),

    #[error("响应解析错误: {0}")]
    ParseError(String),

    #[error("账户状态错误: {0}")]
    AccountStateError(String),
}

/// 允许覆写端点与重试参数，便于本地 mock 测试。
#[derive(Clone, Debug)]
pub struct XAuthConfig {
    pub authenticate_url: String,
    pub oauth1_authorize_url: String,
    pub api_base: String,
    pub user_agent: String,
    pub authorization: String,
    pub timeout: Duration,
    pub max_retries: u32,
    pub retry_interval: Duration,
}

impl Default for XAuthConfig {
    fn default() -> Self {
        Self {
            authenticate_url: "https://api.x.com/oauth/authenticate".to_string(),
            oauth1_authorize_url: "https://x.com/oauth/authorize".to_string(),
            api_base: "https://twitter.com/i/api/2".to_string(),
            user_agent: "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36".to_string(),
            authorization: "Bearer AAAAAAAAAAAAAAAAAAAAANRILgAAAAAAnNwIzUejRCOuH5E6I8xnZz4puTs%3D1Zv7ttfk8LF81IUq16cHjhLTvJu4FA33AGWWjCpTnA".to_string(),
            timeout: Duration::from_secs(10),
            max_retries: 3,
            retry_interval: Duration::from_secs(1),
        }
    }
}

/// Twitter OAuth认证工具
pub struct XAuth {
    client: Client,
    client2: Client,
    cookie_jar: Arc<Jar>,
    config: XAuthConfig,
}

impl XAuth {
    const TWITTER_AUTHORITY: &'static str = "twitter.com";
    const TWITTER_ORIGIN: &'static str = "https://twitter.com";

    /// 创建新的XAuth实例
    pub fn new(auth_token: &str) -> Result<Self, XAuthError> {
        Self::new_with_config(auth_token, XAuthConfig::default())
    }

    /// 创建带可配置端点的XAuth实例
    pub fn new_with_config(auth_token: &str, config: XAuthConfig) -> Result<Self, XAuthError> {
        if auth_token.is_empty() {
            return Err(XAuthError::ParamError("auth_token不能为空".to_string()));
        }

        let cookie_jar = Arc::new(Jar::default());
        let auth_cookie = format!("auth_token={auth_token}; Path=/");

        let mut cookie_urls = vec![
            "https://twitter.com".to_string(),
            "https://x.com".to_string(),
            "https://api.x.com".to_string(),
            config.authenticate_url.clone(),
            config.oauth1_authorize_url.clone(),
            config.api_base.clone(),
        ];
        cookie_urls.sort();
        cookie_urls.dedup();

        for raw_url in cookie_urls {
            let Ok(url) = raw_url.parse::<Url>() else {
                continue;
            };
            if url.scheme().is_empty() || url.host_str().is_none() {
                continue;
            }
            cookie_jar.add_cookie_str(&auth_cookie, &url);
        }

        let mut headers = HeaderMap::new();
        headers.insert("authority", HeaderValue::from_static(Self::TWITTER_AUTHORITY));
        headers.insert("origin", HeaderValue::from_static(Self::TWITTER_ORIGIN));
        headers.insert("x-twitter-auth-type", HeaderValue::from_static("OAuth2Session"));
        headers.insert("x-twitter-active-user", HeaderValue::from_static("yes"));
        headers.insert(
            AUTHORIZATION,
            HeaderValue::from_str(&config.authorization)
                .map_err(|err| XAuthError::ParseError(format!("authorization header无效: {err}")))?,
        );

        let client = ClientBuilder::new()
            .user_agent(config.user_agent.clone())
            .default_headers(headers)
            .cookie_provider(Arc::clone(&cookie_jar))
            .timeout(config.timeout)
            .build()?;

        let client2 = ClientBuilder::new()
            .user_agent(config.user_agent.clone())
            .cookie_provider(Arc::clone(&cookie_jar))
            .timeout(config.timeout)
            .build()?;

        Ok(Self {
            client,
            client2,
            cookie_jar,
            config,
        })
    }

    /// 带429重试发送请求
    async fn send_with_retry<F, Fut>(&self, mut request_fn: F) -> Result<reqwest::Response, XAuthError>
    where
        F: FnMut() -> Fut,
        Fut: Future<Output = Result<reqwest::Response, reqwest::Error>>,
    {
        for attempt in 0..=self.config.max_retries {
            let response = request_fn().await?;
            if response.status() != StatusCode::TOO_MANY_REQUESTS {
                return Ok(response);
            }

            if attempt == self.config.max_retries {
                return Err(XAuthError::AuthError("请求频率限制，重试失败".to_string()));
            }

            tokio::time::sleep(self.config.retry_interval).await;
        }

        Err(XAuthError::AuthError("请求频率限制，重试失败".to_string()))
    }

    /// 获取指定cookie值
    fn get_cookie_value(&self, name: &str) -> Option<String> {
        let mut urls = vec![
            "https://twitter.com".to_string(),
            "https://x.com".to_string(),
            "https://api.x.com".to_string(),
            self.config.api_base.clone(),
            self.config.authenticate_url.clone(),
            self.config.oauth1_authorize_url.clone(),
        ];
        urls.sort();
        urls.dedup();

        for raw_url in urls {
            let Ok(url) = raw_url.parse::<Url>() else {
                continue;
            };
            let Some(cookie_header) = self.cookie_jar.cookies(&url) else {
                continue;
            };
            let Ok(cookie_str) = cookie_header.to_str() else {
                continue;
            };

            for cookie in cookie_str.split(';').map(str::trim) {
                if let Some((cookie_name, cookie_value)) = cookie.split_once('=') {
                    if cookie_name == name && !cookie_value.is_empty() {
                        return Some(cookie_value.to_string());
                    }
                }
            }
        }

        None
    }

    fn extract_first(content: &str, markers: &[&str]) -> Option<String> {
        for marker in markers {
            if let Some(parts) = content.split(marker).nth(1) {
                if let Some(value) = parts.split('"').next() {
                    if !value.is_empty() {
                        return Some(value.to_string());
                    }
                }
            }
        }
        None
    }

    /// 获取Twitter认证token
    pub async fn get_twitter_token(&self, oauth_token: &str) -> Result<String, XAuthError> {
        if oauth_token.is_empty() {
            return Err(XAuthError::ParamError("oauth_token不能为空".to_string()));
        }

        let params = [("oauth_token", oauth_token)];
        let response = self
            .send_with_retry(|| {
                self.client2
                    .get(&self.config.authenticate_url)
                    .query(&params)
                    .send()
            })
            .await?;

        let content = response.text().await?;
        if !content.contains("authenticity_token") {
            if content.contains("The request token for this page is invalid") {
                return Err(XAuthError::AuthError("请求oauth_token无效".to_string()));
            }
            return Err(XAuthError::ParseError(
                "响应中未找到authenticity_token".to_string(),
            ));
        }

        let token = Self::extract_first(
            &content,
            &[
                "name=\"authenticity_token\" value=\"",
                "name=\"authenticity_token\" type=\"hidden\" value=\"",
            ],
        )
        .ok_or_else(|| XAuthError::ParseError("获取到的authenticity_token为空".to_string()))?;

        Ok(token)
    }

    /// 执行OAuth1认证流程
    pub async fn oauth1(&self, oauth_token: &str) -> Result<String, XAuthError> {
        let authenticity_token = self.get_twitter_token(oauth_token).await?;

        let response = self
            .send_with_retry(|| {
                self.client2
                    .post(&self.config.oauth1_authorize_url)
                    .form(&[
                        ("authenticity_token", authenticity_token.as_str()),
                        ("oauth_token", oauth_token),
                    ])
                    .send()
            })
            .await?;

        let content = response.text().await?;
        if !content.contains("oauth_verifier") {
            if content.contains("This account is suspended.") {
                return Err(XAuthError::AccountStateError("该账户已被封禁".to_string()));
            }
            return Err(XAuthError::ParseError("未找到oauth_verifier".to_string()));
        }

        let verifier = content
            .split("oauth_verifier=")
            .nth(1)
            .and_then(|parts| parts.split('"').next())
            .filter(|value| !value.is_empty())
            .ok_or_else(|| XAuthError::ParseError("获取到的oauth_verifier为空".to_string()))?;

        Ok(verifier.to_string())
    }

    /// 获取认证码
    pub async fn get_auth_code(&self, params: &HashMap<String, String>) -> Result<String, XAuthError> {
        if params.is_empty() {
            return Err(XAuthError::ParamError("参数不能为空".to_string()));
        }

        let url = format!("{}/oauth2/authorize", self.config.api_base);
        let mut csrf_refreshed = false;

        loop {
            let response = self
                .send_with_retry(|| {
                    let mut request = self.client.get(&url).query(params);
                    if let Some(ct0) = self.get_cookie_value("ct0") {
                        request = request.header("x-csrf-token", ct0);
                    }
                    request.send()
                })
                .await?;

            let content = response.text().await?;
            let data: Value = serde_json::from_str(&content)
                .map_err(|err| XAuthError::ParseError(format!("解析响应JSON失败: {err}")))?;

            if data.get("code").and_then(|code| code.as_i64()) == Some(353) {
                if self.get_cookie_value("ct0").is_some() && !csrf_refreshed {
                    csrf_refreshed = true;
                    continue;
                }
                return Err(XAuthError::AuthError("未找到ct0 cookie".to_string()));
            }

            if let Some(errors) = data.get("errors").and_then(|e| e.as_array()) {
                if let Some(error) = errors.first() {
                    if let Some(error_code) = error.get("code").and_then(|c| c.as_i64()) {
                        match error_code {
                            32 => return Err(XAuthError::AccountStateError("Bad Token".to_string())),
                            64 | 141 => {
                                return Err(XAuthError::AccountStateError("SUSPENDED".to_string()))
                            }
                            326 => return Err(XAuthError::AccountStateError("LOCKED".to_string())),
                            _ => {}
                        }
                    }
                }
            }

            if let Some(auth_code) = data.get("auth_code").and_then(|code| code.as_str()) {
                return Ok(auth_code.to_string());
            }

            return Err(XAuthError::ParseError(
                "响应中未找到auth_code".to_string(),
            ));
        }
    }

    /// 执行OAuth2认证流程
    pub async fn oauth2(&self, params: &HashMap<String, String>) -> Result<String, XAuthError> {
        let auth_code = self.get_auth_code(params).await?;
        let url = format!("{}/oauth2/authorize", self.config.api_base);

        let response = self
            .send_with_retry(|| {
                let mut request = self.client.post(&url).form(&[("approval", "true"), ("code", auth_code.as_str())]);
                if let Some(ct0) = self.get_cookie_value("ct0") {
                    request = request.header("x-csrf-token", ct0);
                }
                request.send()
            })
            .await?;

        let content = response.text().await?;
        let has_redirect_uri = if content.contains("redirect_uri") {
            true
        } else {
            serde_json::from_str::<Value>(&content)
                .ok()
                .and_then(|value| value.get("redirect_uri").or_else(|| value.get("redirectUri")).cloned())
                .is_some()
        };

        if !has_redirect_uri {
            return Err(XAuthError::ParseError(
                "响应中未找到redirect_uri".to_string(),
            ));
        }

        Ok(auth_code)
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use std::io::{Read, Write};
    use std::net::{TcpListener, TcpStream};
    use std::sync::{atomic::{AtomicBool, Ordering}, Arc};
    use std::thread;

    struct TestServer {
        base_url: String,
        stop: Arc<AtomicBool>,
        join: Option<thread::JoinHandle<()>>,
    }

    impl TestServer {
        fn spawn<F>(mut handler: F) -> Self
        where
            F: FnMut(usize, String) -> (u16, Vec<(String, String)>, String) + Send + 'static,
        {
            let listener = TcpListener::bind("127.0.0.1:0").expect("bind test server");
            listener.set_nonblocking(true).expect("set nonblocking");
            let addr = listener.local_addr().expect("local addr");
            let stop = Arc::new(AtomicBool::new(false));
            let stop_flag = Arc::clone(&stop);
            let join = thread::spawn(move || {
                let mut count = 0usize;
                while !stop_flag.load(Ordering::Relaxed) {
                    match listener.accept() {
                        Ok((mut stream, _)) => {
                            let request = read_request(&mut stream);
                            let (status, headers, body) = handler(count, request);
                            count += 1;
                            write_response(&mut stream, status, &headers, &body);
                        }
                        Err(err) if err.kind() == std::io::ErrorKind::WouldBlock => {
                            thread::sleep(Duration::from_millis(5));
                        }
                        Err(_) => break,
                    }
                }
            });

            Self {
                base_url: format!("http://{}", addr),
                stop,
                join: Some(join),
            }
        }

        fn url(&self, path: &str) -> String {
            format!("{}{}", self.base_url, path)
        }
    }

    impl Drop for TestServer {
        fn drop(&mut self) {
            self.stop.store(true, Ordering::Relaxed);
            let _ = TcpStream::connect(self.base_url.trim_start_matches("http://"));
            if let Some(join) = self.join.take() {
                let _ = join.join();
            }
        }
    }

    fn read_request(stream: &mut TcpStream) -> String {
        stream
            .set_read_timeout(Some(Duration::from_millis(200)))
            .expect("set read timeout");
        let mut buffer = Vec::new();
        let mut temp = [0u8; 1024];

        loop {
            match stream.read(&mut temp) {
                Ok(0) => break,
                Ok(n) => {
                    buffer.extend_from_slice(&temp[..n]);
                    if let Some(headers_end) = find_headers_end(&buffer) {
                        let content_length = parse_content_length(&buffer[..headers_end]);
                        let body_len = buffer.len() - headers_end;
                        if body_len >= content_length {
                            break;
                        }
                    }
                }
                Err(err)
                    if err.kind() == std::io::ErrorKind::WouldBlock
                        || err.kind() == std::io::ErrorKind::TimedOut =>
                {
                    break;
                }
                Err(err) => panic!("read request failed: {err}"),
            }
        }

        String::from_utf8(buffer).expect("utf8 request")
    }

    fn find_headers_end(buffer: &[u8]) -> Option<usize> {
        buffer
            .windows(4)
            .position(|window| window == b"\r\n\r\n")
            .map(|index| index + 4)
    }

    fn parse_content_length(headers: &[u8]) -> usize {
        let text = String::from_utf8_lossy(headers).to_ascii_lowercase();
        for line in text.lines() {
            if let Some(value) = line.strip_prefix("content-length:") {
                return value.trim().parse::<usize>().unwrap_or(0);
            }
        }
        0
    }

    fn write_response(stream: &mut TcpStream, status: u16, headers: &[(String, String)], body: &str) {
        let reason = match status {
            200 => "OK",
            429 => "Too Many Requests",
            500 => "Internal Server Error",
            _ => "OK",
        };
        let mut response = format!(
            "HTTP/1.1 {} {}\r\nContent-Length: {}\r\nConnection: close\r\n",
            status,
            reason,
            body.as_bytes().len()
        );
        for (key, value) in headers {
            response.push_str(&format!("{}: {}\r\n", key, value));
        }
        response.push_str("\r\n");
        response.push_str(body);
        stream.write_all(response.as_bytes()).expect("write response");
        stream.flush().expect("flush response");
    }

    #[tokio::test]
    async fn retries_429_when_fetching_twitter_token() {
        let server = TestServer::spawn(|index, request| match index {
            0 => {
                assert!(request.starts_with("GET /oauth/authenticate?oauth_token=oauth-token"));
                (429, vec![], "rate limited".to_string())
            }
            _ => (
                200,
                vec![],
                "<input name=\"authenticity_token\" value=\"token-123\">".to_string(),
            ),
        });

        let config = XAuthConfig {
            authenticate_url: server.url("/oauth/authenticate"),
            oauth1_authorize_url: server.url("/oauth/authorize"),
            api_base: server.url("/i/api/2"),
            retry_interval: Duration::from_millis(1),
            ..XAuthConfig::default()
        };
        let xauth = XAuth::new_with_config("demo-token", config).unwrap();
        let token = xauth.get_twitter_token("oauth-token").await.unwrap();
        assert_eq!(token, "token-123");
    }

    #[tokio::test]
    async fn oauth1_parses_verifier() {
        let server = TestServer::spawn(|index, request| match index {
            0 => {
                assert!(request.starts_with("GET /oauth/authenticate?oauth_token=oauth-token"));
                (
                    200,
                    vec![],
                    "<input name=\"authenticity_token\" type=\"hidden\" value=\"token-456\">"
                        .to_string(),
                )
            }
            _ => {
                assert!(request.contains("authenticity_token=token-456"));
                (200, vec![], "oauth_verifier=verifier-789\"".to_string())
            }
        });

        let config = XAuthConfig {
            authenticate_url: server.url("/oauth/authenticate"),
            oauth1_authorize_url: server.url("/oauth/authorize"),
            api_base: server.url("/i/api/2"),
            retry_interval: Duration::from_millis(1),
            ..XAuthConfig::default()
        };
        let xauth = XAuth::new_with_config("demo-token", config).unwrap();
        let verifier = xauth.oauth1("oauth-token").await.unwrap();
        assert_eq!(verifier, "verifier-789");
    }

    #[tokio::test]
    async fn oauth2_uses_ct0_and_returns_auth_code() {
        let server = TestServer::spawn(|index, request| match index {
            0 => (
                200,
                vec![("Set-Cookie".to_string(), "ct0=csrf-123; Path=/".to_string())],
                "{\"code\":353}".to_string(),
            ),
            1 => {
                assert!(request.contains("x-csrf-token: csrf-123"));
                (200, vec![], "{\"auth_code\":\"AUTH-CODE-1\"}".to_string())
            }
            _ => {
                assert!(request.contains("x-csrf-token: csrf-123"));
                assert!(request.contains("approval=true"));
                assert!(request.contains("code=AUTH-CODE-1"));
                (
                    200,
                    vec![],
                    "{\"redirect_uri\":\"https://example.com/callback\"}".to_string(),
                )
            }
        });

        let config = XAuthConfig {
            authenticate_url: server.url("/oauth/authenticate"),
            oauth1_authorize_url: server.url("/oauth/authorize"),
            api_base: server.url("/i/api/2"),
            retry_interval: Duration::from_millis(1),
            ..XAuthConfig::default()
        };
        let xauth = XAuth::new_with_config("demo-token", config).unwrap();
        let mut params = HashMap::new();
        params.insert("client_id".to_string(), "client-1".to_string());
        params.insert("redirect_uri".to_string(), "https://example.com/callback".to_string());
        params.insert("response_type".to_string(), "code".to_string());
        params.insert("code_challenge".to_string(), "challenge".to_string());
        params.insert("code_challenge_method".to_string(), "plain".to_string());

        let code = xauth.oauth2(&params).await.unwrap();
        assert_eq!(code, "AUTH-CODE-1");
    }
}
