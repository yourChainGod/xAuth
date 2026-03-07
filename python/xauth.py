import re
import time
from typing import Any, Callable, Dict, List, Optional

import requests


class XAuth:
    """Twitter OAuth认证工具类，支持OAuth1和OAuth2认证流程"""

    TWITTER_AUTHORITY = "twitter.com"
    TWITTER_ORIGIN = "https://twitter.com"
    TWITTER_API_BASE = "https://twitter.com/i/api/2"
    USER_AGENT = (
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 "
        "(KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36"
    )
    AUTHORIZATION = (
        "Bearer "
        "AAAAAAAAAAAAAAAAAAAAANRILgAAAAAAnNwIzUejRCOuH5E6I8xnZz4puTs%3D"
        "1Zv7ttfk8LF81IUq16cHjhLTvJu4FA33AGWWjCpTnA"
    )
    MAX_RETRIES = 3
    RETRY_INTERVAL = 1

    ACCOUNT_STATE = {
        32: "Bad Token",
        64: "SUSPENDED",
        141: "SUSPENDED",
        326: "LOCKED",
    }

    def __init__(self, auth_token: str):
        if not auth_token:
            raise ValueError("auth_token不能为空")

        self.auth_token = auth_token
        self.session = self._create_session()
        self.session2 = self._create_session(include_twitter_headers=False)

    def _create_session(self, include_twitter_headers: bool = True) -> requests.Session:
        session = requests.Session()

        headers = {"user-agent": self.USER_AGENT}
        if include_twitter_headers:
            headers.update(
                {
                    "authority": self.TWITTER_AUTHORITY,
                    "origin": self.TWITTER_ORIGIN,
                    "x-twitter-auth-type": "OAuth2Session",
                    "x-twitter-active-user": "yes",
                    "authorization": self.AUTHORIZATION,
                }
            )

        session.headers.update(headers)
        session.cookies.set("auth_token", self.auth_token)
        return session

    def _request_with_retry(
        self,
        session: requests.Session,
        method: str,
        url: str,
        **kwargs: Any,
    ) -> requests.Response:
        last_response: Optional[requests.Response] = None

        for attempt in range(self.MAX_RETRIES + 1):
            response = session.request(method, url, **kwargs)
            last_response = response
            if response.status_code != 429:
                return response

            if attempt == self.MAX_RETRIES:
                break
            time.sleep(self.RETRY_INTERVAL)

        if last_response is None:
            raise ValueError("请求失败，未收到响应")

        last_response.raise_for_status()
        return last_response

    @staticmethod
    def _extract_first(content: str, patterns: List[str]) -> Optional[str]:
        for pattern in patterns:
            match = re.search(pattern, content)
            if match:
                value = match.group(1)
                if value:
                    return value
        return None

    def get_twitter_token(self, oauth_token: str) -> str:
        if not oauth_token:
            raise ValueError("oauth_token不能为空")

        response = self._request_with_retry(
            self.session2,
            "GET",
            "https://api.x.com/oauth/authenticate",
            params={"oauth_token": oauth_token},
        )
        content = response.text

        if "authenticity_token" not in content:
            if "The request token for this page is invalid" in content:
                raise ValueError("请求oauth_token无效")
            raise ValueError("响应中未找到authenticity_token")

        token = self._extract_first(
            content,
            [
                r'name="authenticity_token"\s+value="([^"]+)"',
                r'name="authenticity_token"\s+type="hidden"\s+value="([^"]+)"',
            ],
        )
        if not token:
            raise ValueError("获取到的authenticity_token为空")
        return token

    def oauth1(self, oauth_token: str) -> str:
        authenticity_token = self.get_twitter_token(oauth_token)
        response = self._request_with_retry(
            self.session2,
            "POST",
            "https://x.com/oauth/authorize",
            data={
                "authenticity_token": authenticity_token,
                "oauth_token": oauth_token,
            },
        )
        content = response.text

        if "oauth_verifier" not in content:
            if "This account is suspended." in content:
                raise ValueError("该账户已被封禁")
            raise ValueError("未找到oauth_verifier")

        verifier = self._extract_first(content, [r"oauth_verifier=([^\"&]+)"])
        if not verifier:
            raise ValueError("获取到的oauth_verifier为空")
        return verifier

    def get_auth_code(self, params: Dict[str, str]) -> str:
        if not params:
            raise ValueError("参数不能为空")

        csrf_attempted = False
        for _ in range(2):
            response = self._request_with_retry(
                self.session,
                "GET",
                f"{self.TWITTER_API_BASE}/oauth2/authorize",
                params=params,
            )

            try:
                data = response.json()
            except ValueError as exc:
                raise ValueError("响应不是有效的JSON格式") from exc

            if data.get("code") == 353 and not csrf_attempted:
                ct0 = response.cookies.get("ct0") or self.session.cookies.get("ct0")
                if ct0:
                    self.session.headers["x-csrf-token"] = ct0
                    csrf_attempted = True
                    continue
                raise ValueError("未找到ct0 cookie")

            if "errors" in data and data["errors"]:
                error_code = data["errors"][0].get("code")
                if error_code in self.ACCOUNT_STATE:
                    raise ValueError(f"token状态错误: {self.ACCOUNT_STATE[error_code]}")

            auth_code = data.get("auth_code")
            if not auth_code:
                raise ValueError("响应中未找到auth_code")
            return auth_code

        raise ValueError("获取auth_code失败")

    def oauth2(self, params: Dict[str, str]) -> str:
        auth_code = self.get_auth_code(params)
        response = self._request_with_retry(
            self.session,
            "POST",
            f"{self.TWITTER_API_BASE}/oauth2/authorize",
            data={"approval": "true", "code": auth_code},
        )

        redirect_uri = None
        try:
            payload = response.json()
            if isinstance(payload, dict):
                redirect_uri = payload.get("redirect_uri")
        except ValueError:
            payload = None

        if not redirect_uri and "redirect_uri" not in response.text:
            raise ValueError("响应中未找到redirect_uri")

        return auth_code
