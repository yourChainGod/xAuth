use std::collections::HashMap;
use xauth::XAuth;

#[tokio::main]
async fn main() -> Result<(), Box<dyn std::error::Error>> {
    // 创建XAuth实例
    let x = XAuth::new("5a0be24d859ad6820906bbc28b0ac9219a71489b")?;

    // OAuth2认证测试
    let mut params = HashMap::new();
    params.insert("code_challenge".to_string(), "challenge".to_string());
    params.insert("code_challenge_method".to_string(), "plain".to_string());
    params.insert("client_id".to_string(), "MjdoOFVuNlJwRmxQT1VnWVBiUkw6MTpjaQ".to_string());
    params.insert("redirect_uri".to_string(), "https://dapp.uxlink.io/authGateway".to_string());
    params.insert("response_type".to_string(), "code".to_string());
    params.insert("scope".to_string(), "tweet.read users.read follows.read follows.write offline.access".to_string());
    params.insert("state".to_string(), "1867096870644760576".to_string());

    match x.oauth2(&params).await {
        Ok(auth_code) => println!("OAuth2 认证码: {}", auth_code),
        Err(e) => eprintln!("OAuth2 认证错误: {}", e),
    }

    // OAuth1认证测试
    match x.oauth1("lSgWPQAAAAABuWQYAAABk7sKZI0").await {
        Ok(verifier) => println!("OAuth1 验证码: {}", verifier),
        Err(e) => eprintln!("OAuth1 认证错误: {}", e),
    }

    Ok(())
} 