use anyhow::Context;
use reqwest::{Client, ClientBuilder, cookie::Jar, header};
use serde_json::Value;
use std::{collections::HashMap, sync::Arc, time::Duration};
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

/// Twitter OAuth认证工具
pub struct XAuth {
    client: Client,
    client2: Client,
    auth_token: String,
}

impl XAuth {
    // 常量定义
    const TWITTER_AUTHORITY: &'static str = "twitter.com";
    const TWITTER_ORIGIN: &'static str = "https://twitter.com";
    const TWITTER_API_BASE: &'static str = "https://twitter.com/i/api/2";
    const USER_AGENT: &'static str = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36";
    const AUTHORIZATION: &'static str = "Bearer AAAAAAAAAAAAAAAAAAAAANRILgAAAAAAnNwIzUejRCOuH5E6I8xnZz4puTs%3D1Zv7ttfk8LF81IUq16cHjhLTvJu4FA33AGWWjCpTnA";
    const MAX_RETRIES: u32 = 3;
    const RETRY_INTERVAL: Duration = Duration::from_secs(1);

    /// 创建新的XAuth实例
    pub fn new(auth_token: &str) -> Result<Self, XAuthError> {
        if auth_token.is_empty() {
            return Err(XAuthError::ParamError("auth_token不能为空".to_string()));
        }

        let cookie_jar = Arc::new(Jar::default());
        let auth_cookie = format!("auth_token={}", auth_token);
        cookie_jar.add_cookie_str(&auth_cookie, &"https://twitter.com".parse().unwrap());

        // 创建主客户端
        let mut headers = header::HeaderMap::new();
        headers.insert(header::AUTHORITY, header::HeaderValue::from_static(Self::TWITTER_AUTHORITY));
        headers.insert(header::ORIGIN, header::HeaderValue::from_static(Self::TWITTER_ORIGIN));
        headers.insert("x-twitter-auth-type", header::HeaderValue::from_static("OAuth2Session"));
        headers.insert("x-twitter-active-user", header::HeaderValue::from_static("yes"));
        headers.insert(header::AUTHORIZATION, header::HeaderValue::from_static(Self::AUTHORIZATION));

        let client = ClientBuilder::new()
            .user_agent(Self::USER_AGENT)
            .default_headers(headers)
            .cookie_provider(Arc::clone(&cookie_jar))
            .timeout(Duration::from_secs(10))
            .build()?;

        // 创建次要客户端
        let client2 = ClientBuilder::new()
            .user_agent(Self::USER_AGENT)
            .cookie_provider(cookie_jar)
            .timeout(Duration::from_secs(10))
            .build()?;

        Ok(Self {
            client,
            client2,
            auth_token: auth_token.to_string(),
        })
    }

    /// 获取Twitter认证token
    pub async fn get_twitter_token(&self, oauth_token: &str) -> Result<String, XAuthError> {
        if oauth_token.is_empty() {
            return Err(XAuthError::ParamError("oauth_token不能为空".to_string()));
        }

        let params = [("oauth_token", oauth_token)];
        let response = self.client2
            .get("https://api.x.com/oauth/authenticate")
            .query(&params)
            .send()
            .await?;

        // 处理请求限制
        if response.status() == reqwest::StatusCode::TOO_MANY_REQUESTS {
            tokio::time::sleep(Self::RETRY_INTERVAL).await;
            return self.get_twitter_token(oauth_token).await;
        }

        let content = response.text().await?;

        if !content.contains("authenticity_token") {
            if content.contains("The request token for this page is invalid") {
                return Err(XAuthError::AuthError("请求oauth_token无效".to_string()));
            }
            return Err(XAuthError::ParseError("响应中未找到authenticity_token".to_string()));
        }

        // 尝试两种可能的token格式
        let token_markers = [
            "name=\"authenticity_token\" value=\"",
            "name=\"authenticity_token\" type=\"hidden\" value=\"",
        ];

        for marker in token_markers {
            if let Some(parts) = content.split(marker).nth(1) {
                if let Some(token) = parts.split('\"').next() {
                    if !token.is_empty() {
                        return Ok(token.to_string());
                    }
                }
            }
        }

        Err(XAuthError::ParseError("获取到的authenticity_token为空".to_string()))
    }

    /// 执行OAuth1认证流程
    pub async fn oauth1(&self, oauth_token: &str) -> Result<String, XAuthError> {
        let authenticity_token = self.get_twitter_token(oauth_token).await?;

        let params = [
            ("authenticity_token", authenticity_token),
            ("oauth_token", oauth_token.to_string()),
        ];

        let response = self.client2
            .post("https://x.com/oauth/authorize")
            .form(&params)
            .send()
            .await?;

        // 处理请求限制
        if response.status() == reqwest::StatusCode::TOO_MANY_REQUESTS {
            tokio::time::sleep(Self::RETRY_INTERVAL).await;
            return self.oauth1(oauth_token).await;
        }

        let content = response.text().await?;

        if !content.contains("oauth_verifier") {
            if content.contains("This account is suspended.") {
                return Err(XAuthError::AccountStateError("该账户已被封禁".to_string()));
            }
            return Err(XAuthError::ParseError("未找到oauth_verifier".to_string()));
        }

        if let Some(parts) = content.split("oauth_verifier=").nth(1) {
            if let Some(verifier) = parts.split('\"').next() {
                if !verifier.is_empty() {
                    return Ok(verifier.to_string());
                }
            }
        }

        Err(XAuthError::ParseError("获取到的oauth_verifier为空".to_string()))
    }

    /// 获取认证码
    pub async fn get_auth_code(&self, params: &HashMap<String, String>) -> Result<String, XAuthError> {
        if params.is_empty() {
            return Err(XAuthError::ParamError("参数不能为空".to_string()));
        }

        let url = format!("{}/oauth2/authorize", Self::TWITTER_API_BASE);
        let response = self.client
            .get(&url)
            .query(&params)
            .send()
            .await?;

        // 处理请求限制
        if response.status() == reqwest::StatusCode::TOO_MANY_REQUESTS {
            tokio::time::sleep(Self::RETRY_INTERVAL).await;
            return self.get_auth_code(params).await;
        }

        let data: Value = response.json().await
            .context("解析响应JSON失败")?;

        // 处理CSRF token
        if let Some(code) = data.get("code") {
            if code.as_i64() == Some(353) {
                // 获取ct0 cookie并设置到请求头
                if let Some(cookies) = response.headers().get(header::SET_COOKIE) {
                    if let Ok(cookie_str) = cookies.to_str() {
                        if cookie_str.contains("ct0=") {
                            let ct0 = cookie_str
                                .split("ct0=")
                                .nth(1)
                                .and_then(|s| s.split(';').next())
                                .ok_or_else(|| XAuthError::ParseError("解析ct0 cookie失败".to_string()))?;

                            // 创建新的客户端并设置ct0 header
                            let mut headers = header::HeaderMap::new();
                            headers.insert("x-csrf-token", header::HeaderValue::from_str(ct0)?);
                            
                            // 递归调用，使用新的客户端
                            return self.get_auth_code(params).await;
                        }
                    }
                }
                return Err(XAuthError::AuthError("未找到ct0 cookie".to_string()));
            }
        }

        // 检查错误
        if let Some(errors) = data.get("errors").and_then(|e| e.as_array()) {
            if let Some(error) = errors.first() {
                if let Some(error_code) = error.get("code").and_then(|c| c.as_i64()) {
                    match error_code {
                        32 => return Err(XAuthError::AccountStateError("Bad Token".to_string())),
                        64 | 141 => return Err(XAuthError::AccountStateError("SUSPENDED".to_string())),
                        326 => return Err(XAuthError::AccountStateError("LOCKED".to_string())),
                        _ => {}
                    }
                }
            }
        }

        // 获取auth_code
        if let Some(auth_code) = data.get("auth_code").and_then(|c| c.as_str()) {
            return Ok(auth_code.to_string());
        }

        Err(XAuthError::ParseError("响应中未找到auth_code".to_string()))
    }

    /// 执行OAuth2认证流程
    pub async fn oauth2(&self, params: &HashMap<String, String>) -> Result<String, XAuthError> {
        let auth_code = self.get_auth_code(params).await?;

        let form_data = [
            ("approval", "true"),
            ("code", &auth_code),
        ];

        let url = format!("{}/oauth2/authorize", Self::TWITTER_API_BASE);
        let response = self.client
            .post(&url)
            .form(&form_data)
            .send()
            .await?;

        // 处理请求限制
        if response.status() == reqwest::StatusCode::TOO_MANY_REQUESTS {
            tokio::time::sleep(Self::RETRY_INTERVAL).await;
            return self.oauth2(params).await;
        }

        let content = response.text().await?;

        if !content.contains("redirect_uri") {
            return Err(XAuthError::ParseError("响应中未找到redirect_uri".to_string()));
        }

        Ok(auth_code)
    }
} 