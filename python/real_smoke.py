import json
import os
import sys
import time
from pathlib import Path
from urllib.parse import parse_qs, urlparse

import requests

from xauth import XAuth

ROOT = Path(__file__).resolve().parent.parent
SITE_FILE = ROOT / "test-sites.json"


def load_sites() -> dict:
    return json.loads(SITE_FILE.read_text())


def run_oauth2(client: XAuth, site_name: str, site: dict) -> dict:
    params = dict(site["params"])
    params["state"] = f"real-smoke-{site_name}-{int(time.time() * 1000)}"
    code = client.oauth2(params)
    return {
        "site": site_name,
        "kind": "oauth2",
        "ok": True,
        "length": len(code),
        "prefix": code[:10],
    }


def run_oauth1(client: XAuth, site_name: str, site: dict) -> dict:
    response = requests.get(
        site["entry_url"],
        headers={"User-Agent": XAuth.USER_AGENT},
        timeout=20,
        allow_redirects=False,
    )
    location = response.headers.get("location", "")
    oauth_token = parse_qs(urlparse(location).query).get("oauth_token", [None])[0]
    if not oauth_token:
        raise RuntimeError("未从站点跳转中提取到 oauth_token")
    verifier = client.oauth1(oauth_token)
    return {
        "site": site_name,
        "kind": "oauth1",
        "ok": True,
        "length": len(verifier),
        "prefix": verifier[:10],
    }


def run_site(client: XAuth, site_name: str, site: dict) -> dict:
    if site["kind"] == "oauth2":
        return run_oauth2(client, site_name, site)
    if site["kind"] == "oauth1":
        return run_oauth1(client, site_name, site)
    raise RuntimeError(f"未知站点类型: {site['kind']}")


def main() -> int:
    auth_token = os.getenv("XAUTH_AUTH_TOKEN")
    if not auth_token:
        print("请先设置 XAUTH_AUTH_TOKEN 环境变量")
        return 1

    target = os.getenv("XAUTH_SITE")
    sites = load_sites()
    if target:
        if target not in sites:
            print(f"未知站点: {target}")
            return 1
        selected = {target: sites[target]}
    else:
        selected = sites

    client = XAuth(auth_token)
    results = []
    exit_code = 0

    for name, site in selected.items():
        try:
            results.append(run_site(client, name, site))
        except Exception as exc:
            exit_code = 1
            results.append(
                {
                    "site": name,
                    "kind": site.get("kind"),
                    "ok": False,
                    "error": str(exc),
                }
            )

    print(json.dumps(results, ensure_ascii=False, indent=2))
    return exit_code


if __name__ == "__main__":
    sys.exit(main())
