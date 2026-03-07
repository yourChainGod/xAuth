package com.xauth;

import com.sun.net.httpserver.Headers;
import com.sun.net.httpserver.HttpExchange;
import com.sun.net.httpserver.HttpHandler;
import com.sun.net.httpserver.HttpServer;
import org.junit.jupiter.api.Test;

import java.io.IOException;
import java.io.InputStream;
import java.io.OutputStream;
import java.net.InetSocketAddress;
import java.net.URI;
import java.net.URLDecoder;
import java.nio.charset.StandardCharsets;
import java.util.HashMap;
import java.util.Map;
import java.util.concurrent.atomic.AtomicInteger;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertThrows;

class XAuthMockTest {

    @Test
    void retries429WhenFetchingTwitterToken() throws Exception {
        AtomicInteger attempts = new AtomicInteger();
        try (TestServer server = new TestServer()) {
            server.handle("/oauth/authenticate", exchange -> {
                int current = attempts.incrementAndGet();
                if (current == 1) {
                    send(exchange, 429, "rate limited");
                    return;
                }
                send(exchange, 200, "<input name=\"authenticity_token\" value=\"token-123\">");
            });

            XAuth.Options options = new XAuth.Options();
            options.authenticateUrl = server.url("/oauth/authenticate");
            options.oauth1AuthorizeUrl = server.url("/oauth/authorize");
            options.apiBase = server.url("/i/api/2");
            options.retryIntervalMs = 1;

            try (XAuth xAuth = new XAuth("demo-token", options)) {
                assertEquals("token-123", xAuth.getTwitterToken("oauth-token"));
                assertEquals(2, attempts.get());
            }
        }
    }

    @Test
    void parsesOauth1Verifier() throws Exception {
        try (TestServer server = new TestServer()) {
            server.handle("/oauth/authenticate", exchange ->
                    send(exchange, 200, "<input name=\"authenticity_token\" type=\"hidden\" value=\"token-456\">")
            );
            server.handle("/oauth/authorize", exchange -> {
                String body = readBody(exchange);
                if (!body.contains("authenticity_token=token-456")) {
                    throw new AssertionError("缺少 authenticity_token");
                }
                send(exchange, 200, "oauth_verifier=verifier-789\"");
            });

            XAuth.Options options = new XAuth.Options();
            options.authenticateUrl = server.url("/oauth/authenticate");
            options.oauth1AuthorizeUrl = server.url("/oauth/authorize");
            options.apiBase = server.url("/i/api/2");
            options.retryIntervalMs = 1;

            try (XAuth xAuth = new XAuth("demo-token", options)) {
                assertEquals("verifier-789", xAuth.oauth1("oauth-token"));
            }
        }
    }

    @Test
    void oauth2UsesCt0AndReturnsAuthCode() throws Exception {
        AtomicInteger getCalls = new AtomicInteger();
        try (TestServer server = new TestServer()) {
            server.handle("/i/api/2/oauth2/authorize", exchange -> {
                if ("GET".equals(exchange.getRequestMethod())) {
                    int current = getCalls.incrementAndGet();
                    if (current == 1) {
                        Headers headers = exchange.getResponseHeaders();
                        headers.add("Set-Cookie", "ct0=csrf-123; Path=/");
                        send(exchange, 200, "{\"code\":353}");
                        return;
                    }
                    assertEquals("csrf-123", exchange.getRequestHeaders().getFirst("x-csrf-token"));
                    send(exchange, 200, "{\"auth_code\":\"AUTH-CODE-1\"}");
                    return;
                }

                assertEquals("POST", exchange.getRequestMethod());
                assertEquals("csrf-123", exchange.getRequestHeaders().getFirst("x-csrf-token"));
                String body = readBody(exchange);
                if (!body.contains("code=AUTH-CODE-1")) {
                    throw new AssertionError("POST body 未包含 auth code");
                }
                send(exchange, 200, "{\"redirect_uri\":\"https://example.com/callback\"}");
            });

            XAuth.Options options = new XAuth.Options();
            options.authenticateUrl = server.url("/oauth/authenticate");
            options.oauth1AuthorizeUrl = server.url("/oauth/authorize");
            options.apiBase = server.url("/i/api/2");
            options.retryIntervalMs = 1;

            try (XAuth xAuth = new XAuth("demo-token", options)) {
                Map<String, String> params = new HashMap<>();
                params.put("client_id", "client-1");
                params.put("redirect_uri", "https://example.com/callback");
                params.put("response_type", "code");
                params.put("code_challenge", "challenge");
                params.put("code_challenge_method", "plain");

                assertEquals("AUTH-CODE-1", xAuth.oauth2(params));
                assertEquals(2, getCalls.get());
            }
        }
    }

    @Test
    void mapsAccountStateErrors() throws Exception {
        try (TestServer server = new TestServer()) {
            server.handle("/i/api/2/oauth2/authorize", exchange ->
                    send(exchange, 200, "{\"errors\":[{\"code\":326}]}")
            );

            XAuth.Options options = new XAuth.Options();
            options.authenticateUrl = server.url("/oauth/authenticate");
            options.oauth1AuthorizeUrl = server.url("/oauth/authorize");
            options.apiBase = server.url("/i/api/2");
            options.retryIntervalMs = 1;

            try (XAuth xAuth = new XAuth("demo-token", options)) {
                Map<String, String> params = new HashMap<>();
                params.put("client_id", "demo");
                XAuthException error = assertThrows(XAuthException.class, () -> xAuth.getAuthCode(params));
                assertEquals("token状态错误: LOCKED", error.getMessage());
            }
        }
    }

    private static void send(HttpExchange exchange, int statusCode, String body) throws IOException {
        byte[] data = body.getBytes(StandardCharsets.UTF_8);
        exchange.sendResponseHeaders(statusCode, data.length);
        try (OutputStream outputStream = exchange.getResponseBody()) {
            outputStream.write(data);
        }
    }

    private static String readBody(HttpExchange exchange) throws IOException {
        try (InputStream inputStream = exchange.getRequestBody()) {
            return new String(inputStream.readAllBytes(), StandardCharsets.UTF_8);
        }
    }

    private static final class TestServer implements AutoCloseable {
        private final HttpServer server;

        private TestServer() throws IOException {
            this.server = HttpServer.create(new InetSocketAddress("127.0.0.1", 0), 0);
            this.server.start();
        }

        private void handle(String path, HttpHandler handler) {
            server.createContext(path, handler);
        }

        private String url(String path) {
            return "http://127.0.0.1:" + server.getAddress().getPort() + path;
        }

        @Override
        public void close() {
            server.stop(0);
        }
    }
}
