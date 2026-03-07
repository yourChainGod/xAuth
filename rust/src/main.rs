use std::{collections::HashMap, env};
use xauth::XAuth;

fn must_env(key: &str) -> Result<String, String> {
    env::var(key).map_err(|_| format!("请先设置 {key} 环境变量"))
}

fn env_or(key: &str, fallback: &str) -> String {
    env::var(key).unwrap_or_else(|_| fallback.to_string())
}

#[tokio::main]
async fn main() -> Result<(), Box<dyn std::error::Error>> {
    let auth_token = match must_env("XAUTH_AUTH_TOKEN") {
        Ok(value) => value,
        Err(message) => {
            eprintln!("{message}");
            std::process::exit(1);
        }
    };

    let client_id = match must_env("XAUTH_CLIENT_ID") {
        Ok(value) => value,
        Err(message) => {
            eprintln!("{message}");
            std::process::exit(1);
        }
    };

    let redirect_uri = match must_env("XAUTH_REDIRECT_URI") {
        Ok(value) => value,
        Err(message) => {
            eprintln!("{message}");
            std::process::exit(1);
        }
    };

    let x = XAuth::new(&auth_token)?;

    let mut params = HashMap::new();
    params.insert("code_challenge".to_string(), env_or("XAUTH_CODE_CHALLENGE", "challenge"));
    params.insert(
        "code_challenge_method".to_string(),
        env_or("XAUTH_CODE_CHALLENGE_METHOD", "plain"),
    );
    params.insert("client_id".to_string(), client_id);
    params.insert("redirect_uri".to_string(), redirect_uri);
    params.insert("response_type".to_string(), "code".to_string());
    params.insert(
        "scope".to_string(),
        env_or(
            "XAUTH_SCOPE",
            "tweet.read users.read follows.read follows.write offline.access",
        ),
    );
    params.insert("state".to_string(), env_or("XAUTH_STATE", "demo-state"));

    match x.oauth2(&params).await {
        Ok(auth_code) => println!("OAuth2 认证码: {}", auth_code),
        Err(e) => {
            eprintln!("OAuth2 认证错误: {}", e);
            std::process::exit(1);
        }
    }

    match env::var("XAUTH_OAUTH1_TOKEN") {
        Ok(oauth1_token) => match x.oauth1(&oauth1_token).await {
            Ok(verifier) => println!("OAuth1 验证码: {}", verifier),
            Err(e) => {
                eprintln!("OAuth1 认证错误: {}", e);
                std::process::exit(1);
            }
        },
        Err(_) => eprintln!("未设置 XAUTH_OAUTH1_TOKEN，跳过 OAuth1 示例"),
    }

    Ok(())
}
