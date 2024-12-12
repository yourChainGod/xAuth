from xauth import XAuth

def main():
    # 创建XAuth实例
    x = XAuth("5a0be24d859ad6820906bbc28b0ac9219a71489b")

    try:
        # OAuth2认证测试
        params = {
            "code_challenge": "challenge",
            "code_challenge_method": "plain",
            "client_id": "MjdoOFVuNlJwRmxQT1VnWVBiUkw6MTpjaQ",
            "redirect_uri": "https://dapp.uxlink.io/authGateway",
            "response_type": "code",
            "scope": "tweet.read users.read follows.read follows.write offline.access",
            "state": "1867096870644760576"
        }
        auth_code = x.oauth2(params)
        print(f"OAuth2 认证码: {auth_code}")

        # OAuth1认证测试
        oauth1_verifier = x.oauth1("lSgWPQAAAAABuWQYAAABk7sKZI0")
        print(f"OAuth1 验证码: {oauth1_verifier}")

    except Exception as e:
        print(f"错误: {str(e)}")

if __name__ == "__main__":
    main()
