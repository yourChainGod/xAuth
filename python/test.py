import os
import sys

from xauth import XAuth


def build_oauth2_params() -> dict:
    client_id = os.getenv("XAUTH_CLIENT_ID")
    redirect_uri = os.getenv("XAUTH_REDIRECT_URI")
    if not client_id or not redirect_uri:
        raise RuntimeError("请先设置 XAUTH_CLIENT_ID 和 XAUTH_REDIRECT_URI 环境变量")

    return {
        "code_challenge": os.getenv("XAUTH_CODE_CHALLENGE", "challenge"),
        "code_challenge_method": os.getenv("XAUTH_CODE_CHALLENGE_METHOD", "plain"),
        "client_id": client_id,
        "redirect_uri": redirect_uri,
        "response_type": "code",
        "scope": os.getenv(
            "XAUTH_SCOPE",
            "tweet.read users.read follows.read follows.write offline.access",
        ),
        "state": os.getenv("XAUTH_STATE", "demo-state"),
    }


def main() -> int:
    auth_token = os.getenv("XAUTH_AUTH_TOKEN")
    oauth1_token = os.getenv("XAUTH_OAUTH1_TOKEN")

    if not auth_token:
        print("请先设置 XAUTH_AUTH_TOKEN 环境变量")
        return 1

    x = XAuth(auth_token)

    try:
        params = build_oauth2_params()
        auth_code = x.oauth2(params)
        print(f"OAuth2 认证码: {auth_code}")

        if oauth1_token:
            oauth1_verifier = x.oauth1(oauth1_token)
            print(f"OAuth1 验证码: {oauth1_verifier}")
        else:
            print("未设置 XAUTH_OAUTH1_TOKEN，跳过 OAuth1 示例")

        return 0
    except Exception as e:
        print(f"错误: {str(e)}")
        return 1


if __name__ == "__main__":
    sys.exit(main())
