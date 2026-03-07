package main

import (
	"net/http"
	"net/http/httptest"
	"testing"
	"time"
)

func TestGetTwitterTokenRetries429(t *testing.T) {
	attempts := 0
	server := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		switch r.URL.Path {
		case "/oauth/authenticate":
			attempts++
			if attempts == 1 {
				w.WriteHeader(http.StatusTooManyRequests)
				_, _ = w.Write([]byte("rate limited"))
				return
			}
			_, _ = w.Write([]byte(`<input name="authenticity_token" value="token-123">`))
		default:
			t.Fatalf("unexpected path: %s", r.URL.Path)
		}
	}))
	defer server.Close()

	x := NewTwitterWithOptions("demo-token", XAuthOptions{
		AuthenticateURL:    server.URL + "/oauth/authenticate",
		OAuth1AuthorizeURL: server.URL + "/oauth/authorize",
		APIBase:            server.URL + "/i/api/2",
		RetryInterval:      time.Millisecond,
	})

	token, err := x.GetTwitterToken("oauth-token")
	if err != nil {
		t.Fatalf("GetTwitterToken returned error: %v", err)
	}
	if token != "token-123" {
		t.Fatalf("expected token-123, got %q", token)
	}
	if attempts != 2 {
		t.Fatalf("expected 2 attempts, got %d", attempts)
	}
}

func TestOauth1ParsesVerifier(t *testing.T) {
	server := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		switch r.URL.Path {
		case "/oauth/authenticate":
			_, _ = w.Write([]byte(`<input name="authenticity_token" type="hidden" value="token-456">`))
		case "/oauth/authorize":
			if err := r.ParseForm(); err != nil {
				t.Fatalf("ParseForm failed: %v", err)
			}
			if got := r.Form.Get("authenticity_token"); got != "token-456" {
				t.Fatalf("unexpected authenticity_token: %q", got)
			}
			_, _ = w.Write([]byte(`redirect?oauth_verifier=verifier-789"`))
		default:
			t.Fatalf("unexpected path: %s", r.URL.Path)
		}
	}))
	defer server.Close()

	x := NewTwitterWithOptions("demo-token", XAuthOptions{
		AuthenticateURL:    server.URL + "/oauth/authenticate",
		OAuth1AuthorizeURL: server.URL + "/oauth/authorize",
		APIBase:            server.URL + "/i/api/2",
		RetryInterval:      time.Millisecond,
	})

	verifier, err := x.Oauth1("oauth-token")
	if err != nil {
		t.Fatalf("Oauth1 returned error: %v", err)
	}
	if verifier != "verifier-789" {
		t.Fatalf("expected verifier-789, got %q", verifier)
	}
}

func TestOauth2UsesCSRFAndReturnsAuthCode(t *testing.T) {
	getCalls := 0
	server := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		switch {
		case r.Method == http.MethodGet && r.URL.Path == "/i/api/2/oauth2/authorize":
			getCalls++
			if getCalls == 1 {
				http.SetCookie(w, &http.Cookie{Name: "ct0", Value: "csrf-123", Path: "/"})
				_, _ = w.Write([]byte(`{"code":353}`))
				return
			}
			if got := r.Header.Get("x-csrf-token"); got != "csrf-123" {
				t.Fatalf("expected GET x-csrf-token csrf-123, got %q", got)
			}
			_, _ = w.Write([]byte(`{"auth_code":"AUTH-CODE-1"}`))
		case r.Method == http.MethodPost && r.URL.Path == "/i/api/2/oauth2/authorize":
			if got := r.Header.Get("x-csrf-token"); got != "csrf-123" {
				t.Fatalf("expected POST x-csrf-token csrf-123, got %q", got)
			}
			if err := r.ParseForm(); err != nil {
				t.Fatalf("ParseForm failed: %v", err)
			}
			if got := r.Form.Get("code"); got != "AUTH-CODE-1" {
				t.Fatalf("expected code AUTH-CODE-1, got %q", got)
			}
			_, _ = w.Write([]byte(`{"redirect_uri":"https://example.com/callback"}`))
		default:
			t.Fatalf("unexpected request: %s %s", r.Method, r.URL.Path)
		}
	}))
	defer server.Close()

	x := NewTwitterWithOptions("demo-token", XAuthOptions{
		AuthenticateURL:    server.URL + "/oauth/authenticate",
		OAuth1AuthorizeURL: server.URL + "/oauth/authorize",
		APIBase:            server.URL + "/i/api/2",
		RetryInterval:      time.Millisecond,
	})

	code, err := x.Oauth2(map[string]string{
		"client_id":         "client-1",
		"redirect_uri":      "https://example.com/callback",
		"response_type":     "code",
		"code_challenge":    "challenge",
		"code_challenge_method": "plain",
	})
	if err != nil {
		t.Fatalf("Oauth2 returned error: %v", err)
	}
	if code != "AUTH-CODE-1" {
		t.Fatalf("expected AUTH-CODE-1, got %q", code)
	}
	if getCalls != 2 {
		t.Fatalf("expected 2 GET calls, got %d", getCalls)
	}
}

func TestOauth2MapsAccountStateErrors(t *testing.T) {
	server := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		_, _ = w.Write([]byte(`{"errors":[{"code":326}]}`))
	}))
	defer server.Close()

	x := NewTwitterWithOptions("demo-token", XAuthOptions{
		AuthenticateURL:    server.URL + "/oauth/authenticate",
		OAuth1AuthorizeURL: server.URL + "/oauth/authorize",
		APIBase:            server.URL + "/i/api/2",
		RetryInterval:      time.Millisecond,
	})

	_, err := x.GetAuthCode(map[string]string{"client_id": "demo"})
	if err == nil {
		t.Fatal("expected account state error")
	}
	want := "token状态错误: LOCKED"
	if got := err.Error(); got != want {
		t.Fatalf("expected %q, got %q", want, got)
	}
}

func TestOauth2AcceptsTextRedirectURI(t *testing.T) {
	server := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		switch {
		case r.Method == http.MethodGet:
			_, _ = w.Write([]byte(`{"auth_code":"AUTH-TEXT"}`))
		case r.Method == http.MethodPost:
			_, _ = w.Write([]byte(`redirect_uri=https://example.com/callback`))
		default:
			t.Fatalf("unexpected method: %s", r.Method)
		}
	}))
	defer server.Close()

	x := NewTwitterWithOptions("demo-token", XAuthOptions{
		AuthenticateURL:    server.URL + "/oauth/authenticate",
		OAuth1AuthorizeURL: server.URL + "/oauth/authorize",
		APIBase:            server.URL + "/i/api/2",
		RetryInterval:      time.Millisecond,
	})

	code, err := x.Oauth2(map[string]string{"client_id": "demo"})
	if err != nil {
		t.Fatalf("Oauth2 returned error: %v", err)
	}
	if code != "AUTH-TEXT" {
		t.Fatalf("expected AUTH-TEXT, got %q", code)
	}
}

