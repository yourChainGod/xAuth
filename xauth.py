import requests
import time
from typing import Dict, Optional

class XAuth:
    TWITTER_AUTHORITY = "twitter.com"
    TWITTER_ORIGIN = "https://twitter.com"
    TWITTER_API_BASE = "https://twitter.com/i/api/2"
    USER_AGENT = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36"
    AUTHORIZATION = "Bearer AAAAAAAAAAAAAAAAAAAAANRILgAAAAAAnNwIzUejRCOuH5E6I8xnZz4puTs%3D1Zv7ttfk8LF81IUq16cHjhLTvJu4FA33AGWWjCpTnA"
    MAX_RETRIES = 3
    RETRY_INTERVAL = 1

    ACCOUNT_STATE = {
        32: "Bad Token",
        64: "SUSPENDED",
        141: "SUSPENDED",
        326: "LOCKED"
    }

    def __init__(self, auth_token: str):
        """初始化XAuth实例"""
        self.auth_token = auth_token
        self.session = self._create_session()
        self.session2 = self._create_session(include_twitter_headers=False)

    def _create_session(self, include_twitter_headers: bool = True) -> requests.Session:
        """创建配置好的requests session"""
        session = requests.Session()
        
        # 设置基础headers
        headers = {
            "user-agent": self.USER_AGENT
        }
        
        if include_twitter_headers:
            headers.update({
                "authority": self.TWITTER_AUTHORITY,
                "origin": self.TWITTER_ORIGIN,
                "x-twitter-auth-type": "OAuth2Session",
                "x-twitter-active-user": "yes",
                "authorization": self.AUTHORIZATION
            })
        
        session.headers.update(headers)
        session.cookies.set("auth_token", self.auth_token)
        
        return session

    def _handle_response(self, response: requests.Response, retry_func=None) -> None:
        """处理响应状态"""
        if response.status_code == 429:  # Too Many Requests
            time.sleep(self.RETRY_INTERVAL)
            if retry_func:
                return retry_func()
            response.raise_for_status()
        

    def get_twitter_token(self, oauth_token: str) -> str:
        """获取Twitter认证token"""
        if not oauth_token:
            raise ValueError("oauth_token不能为空")

        params = {"oauth_token": oauth_token}
        response = self.session2.get("https://api.x.com/oauth/authenticate", params=params)
        self._handle_response(response)
        
        content = response.text
        
        if "authenticity_token" not in content:
            if "The request token for this page is invalid" in content:
                raise ValueError("请求oauth_token无效")
            raise ValueError("响应中未找到authenticity_token")

        # 尝试两种可能的token格式
        token_markers = [
            'name="authenticity_token" value="',
            'name="authenticity_token" type="hidden" value="'
        ]
        
        token = None
        for marker in token_markers:
            if marker in content:
                token = content.split(marker)[1].split('"')[0]
                break
                
        if not token:
            raise ValueError("获取到的authenticity_token为空")
        return token

    def oauth1(self, oauth_token: str) -> str:
        """执行OAuth1认证流程"""
        authenticity_token = self.get_twitter_token(oauth_token)
        
        data = {
            "authenticity_token": authenticity_token,
            "oauth_token": oauth_token
        }
        
        response = self.session2.post("https://x.com/oauth/authorize", data=data)
        self._handle_response(response)
        
        content = response.text
        
        if "oauth_verifier" not in content:
            if "This account is suspended." in content:
                raise ValueError("该账户已被封禁")
            raise ValueError("未找到oauth_verifier")
            
        verifier = content.split("oauth_verifier=")[1].split('"')[0]
        if not verifier:
            raise ValueError("获取到的oauth_verifier为空")
            
        return verifier

    def get_auth_code(self, params: Dict[str, str]) -> str:
        """获取认证码"""
        if not params:
            raise ValueError("参数不能为空")

        def retry():
            return self.get_auth_code(params)

        response = self.session.get(f"{self.TWITTER_API_BASE}/oauth2/authorize", params=params)
        self._handle_response(response, retry)
        data = response.json()
        
        # 处理CSRF token
        if data.get("code") == 353:
            ct0 = response.cookies.get("ct0")
            if ct0:
                self.session.headers["x-csrf-token"] = ct0
                return self.get_auth_code(params)
            raise ValueError("未找到ct0 cookie")

        # 检查错误
        if "errors" in data and data["errors"]:
            error_code = data["errors"][0].get("code")
            if error_code in self.ACCOUNT_STATE:
                raise ValueError(f"token状态错误: {self.ACCOUNT_STATE[error_code]}")

        auth_code = data.get("auth_code")
        if not auth_code:
            raise ValueError("响应中未找到auth_code")
            
        return auth_code

    def oauth2(self, params: Dict[str, str]) -> str:
        """执行OAuth2认证流程"""
        auth_code = self.get_auth_code(params)
        
        data = {
            "approval": "true",
            "code": auth_code
        }
        
        def retry():
            return self.oauth2(params)

        response = self.session.post(f"{self.TWITTER_API_BASE}/oauth2/authorize", data=data)
        self._handle_response(response, retry)

        if  "redirect_uri" not in response.text:
            raise ValueError("响应中未找到redirect_uri")
        
        return auth_code
