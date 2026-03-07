import unittest

from xauth import XAuth


class FakeResponse:
    def __init__(self, status_code=200, text="", json_data=None, cookies=None):
        self.status_code = status_code
        self.text = text
        self._json_data = json_data
        self.cookies = cookies or {}

    def json(self):
        if self._json_data is None:
            raise ValueError("no json")
        return self._json_data

    def raise_for_status(self):
        if self.status_code >= 400:
            raise ValueError(f"http {self.status_code}")


class FakeSession:
    def __init__(self, handler):
        self.handler = handler
        self.headers = {}
        self.cookies = {}
        self.calls = []

    def request(self, method, url, **kwargs):
        self.calls.append((method, url, kwargs))
        response = self.handler(self, method, url, **kwargs)
        for key, value in getattr(response, "cookies", {}).items():
            self.cookies[key] = value
        return response


class XAuthMockTests(unittest.TestCase):
    def test_oauth2_uses_ct0_and_returns_auth_code(self):
        x = XAuth("demo-token")
        get_calls = {"count": 0}

        def main_handler(session, method, url, **kwargs):
            if method == "GET":
                get_calls["count"] += 1
                if get_calls["count"] == 1:
                    return FakeResponse(json_data={"code": 353}, cookies={"ct0": "csrf-123"})
                self.assertEqual(session.headers.get("x-csrf-token"), "csrf-123")
                self.assertEqual(kwargs["params"]["client_id"], "client-1")
                return FakeResponse(json_data={"auth_code": "AUTH-CODE-1"})

            if method == "POST":
                self.assertEqual(session.headers.get("x-csrf-token"), "csrf-123")
                self.assertEqual(kwargs["data"]["code"], "AUTH-CODE-1")
                return FakeResponse(json_data={"redirect_uri": "https://example.com/callback"})

            raise AssertionError(f"unexpected request: {method} {url}")

        x.session = FakeSession(main_handler)
        x.session2 = FakeSession(lambda *_args, **_kwargs: FakeResponse())

        code = x.oauth2(
            {
                "client_id": "client-1",
                "redirect_uri": "https://example.com/callback",
                "response_type": "code",
                "code_challenge": "challenge",
                "code_challenge_method": "plain",
            }
        )

        self.assertEqual(code, "AUTH-CODE-1")
        self.assertEqual(get_calls["count"], 2)

    def test_oauth1_parses_verifier(self):
        x = XAuth("demo-token")

        def oauth1_handler(_session, method, url, **kwargs):
            if method == "GET":
                self.assertIn("oauth_token", kwargs["params"])
                return FakeResponse(
                    text='<input name="authenticity_token" type="hidden" value="token-456">'
                )
            if method == "POST":
                self.assertEqual(kwargs["data"]["authenticity_token"], "token-456")
                return FakeResponse(text='oauth_verifier=verifier-789"')
            raise AssertionError(f"unexpected request: {method} {url}")

        x.session = FakeSession(lambda *_args, **_kwargs: FakeResponse())
        x.session2 = FakeSession(oauth1_handler)

        verifier = x.oauth1("oauth-token")
        self.assertEqual(verifier, "verifier-789")

    def test_retry_on_429(self):
        x = XAuth("demo-token")
        attempts = {"count": 0}

        def retry_handler(_session, method, url, **kwargs):
            attempts["count"] += 1
            if attempts["count"] == 1:
                return FakeResponse(status_code=429)
            return FakeResponse(text='<input name="authenticity_token" value="token-123">')

        x.session2 = FakeSession(retry_handler)
        token = x.get_twitter_token("oauth-token")
        self.assertEqual(token, "token-123")
        self.assertEqual(attempts["count"], 2)


if __name__ == "__main__":
    unittest.main()
