using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace XAuth.Tests
{
    public class XAuthMockTests
    {
        [Fact]
        public async Task GetTwitterToken_Retries429()
        {
            var attempts = 0;
            using var server = new TestServer();
            server.Handle("/oauth/authenticate", async ctx =>
            {
                attempts += 1;
                if (attempts == 1)
                {
                    await server.Write(ctx, 429, "rate limited");
                    return;
                }
                await server.Write(ctx, 200, "<input name=\"authenticity_token\" value=\"token-123\">");
            });

            var options = new XAuthClientOptions
            {
                AuthenticateUrl = server.Url("/oauth/authenticate"),
                OAuth1AuthorizeUrl = server.Url("/oauth/authorize"),
                ApiBase = server.Url("/i/api/2"),
                RetryIntervalMs = 1,
                Timeout = TimeSpan.FromSeconds(5)
            };

            using var client = new XAuthClient("demo-token", options);
            var token = await client.GetTwitterTokenAsync("oauth-token");
            Assert.Equal("token-123", token);
            Assert.Equal(2, attempts);
        }

        [Fact]
        public async Task OAuth1_ParsesVerifier()
        {
            using var server = new TestServer();
            server.Handle("/oauth/authenticate", ctx => server.Write(ctx, 200, "<input name=\"authenticity_token\" type=\"hidden\" value=\"token-456\">"));
            server.Handle("/oauth/authorize", async ctx =>
            {
                var body = await server.ReadBody(ctx);
                Assert.Contains("authenticity_token=token-456", body);
                await server.Write(ctx, 200, "oauth_verifier=verifier-789\"");
            });

            var options = new XAuthClientOptions
            {
                AuthenticateUrl = server.Url("/oauth/authenticate"),
                OAuth1AuthorizeUrl = server.Url("/oauth/authorize"),
                ApiBase = server.Url("/i/api/2"),
                RetryIntervalMs = 1,
                Timeout = TimeSpan.FromSeconds(5)
            };

            using var client = new XAuthClient("demo-token", options);
            var verifier = await client.OAuth1Async("oauth-token");
            Assert.Equal("verifier-789", verifier);
        }

        [Fact]
        public async Task OAuth2_UsesCt0AndReturnsAuthCode()
        {
            var getCalls = 0;
            using var server = new TestServer();
            server.Handle("/i/api/2/oauth2/authorize", async ctx =>
            {
                if (ctx.Request.HttpMethod == "GET")
                {
                    getCalls += 1;
                    if (getCalls == 1)
                    {
                        ctx.Response.AddHeader("Set-Cookie", "ct0=csrf-123; Path=/");
                        await server.Write(ctx, 200, "{\"code\":353}");
                        return;
                    }

                    Assert.Equal("csrf-123", ctx.Request.Headers["x-csrf-token"]);
                    await server.Write(ctx, 200, "{\"auth_code\":\"AUTH-CODE-1\"}");
                    return;
                }

                Assert.Equal("POST", ctx.Request.HttpMethod);
                Assert.Equal("csrf-123", ctx.Request.Headers["x-csrf-token"]);
                var body = await server.ReadBody(ctx);
                Assert.Contains("code=AUTH-CODE-1", body);
                await server.Write(ctx, 200, "{\"redirect_uri\":\"https://example.com/callback\"}");
            });

            var options = new XAuthClientOptions
            {
                AuthenticateUrl = server.Url("/oauth/authenticate"),
                OAuth1AuthorizeUrl = server.Url("/oauth/authorize"),
                ApiBase = server.Url("/i/api/2"),
                RetryIntervalMs = 1,
                Timeout = TimeSpan.FromSeconds(5)
            };

            using var client = new XAuthClient("demo-token", options);
            var authCode = await client.OAuth2Async(new Dictionary<string, string>
            {
                ["client_id"] = "client-1",
                ["redirect_uri"] = "https://example.com/callback",
                ["response_type"] = "code",
                ["code_challenge"] = "challenge",
                ["code_challenge_method"] = "plain"
            });

            Assert.Equal("AUTH-CODE-1", authCode);
            Assert.Equal(2, getCalls);
        }

        [Fact]
        public async Task GetAuthCode_MapsAccountStateErrors()
        {
            using var server = new TestServer();
            server.Handle("/i/api/2/oauth2/authorize", ctx => server.Write(ctx, 200, "{\"errors\":[{\"code\":326}]}"));

            var options = new XAuthClientOptions
            {
                AuthenticateUrl = server.Url("/oauth/authenticate"),
                OAuth1AuthorizeUrl = server.Url("/oauth/authorize"),
                ApiBase = server.Url("/i/api/2"),
                RetryIntervalMs = 1,
                Timeout = TimeSpan.FromSeconds(5)
            };

            using var client = new XAuthClient("demo-token", options);
            var ex = await Assert.ThrowsAsync<XAuthException>(() => client.GetAuthCodeAsync(new Dictionary<string, string> { ["client_id"] = "demo" }));
            Assert.Equal("token状态错误: LOCKED", ex.Message);
        }

        private sealed class TestServer : IDisposable
        {
            private readonly HttpListener _listener;
            private readonly CancellationTokenSource _cts = new CancellationTokenSource();
            private readonly ConcurrentDictionary<string, Func<HttpListenerContext, Task>> _handlers = new ConcurrentDictionary<string, Func<HttpListenerContext, Task>>();
            private readonly Task _loop;
            private readonly string _prefix;

            public TestServer()
            {
                var port = GetFreePort();
                _prefix = $"http://127.0.0.1:{port}/";
                _listener = new HttpListener();
                _listener.Prefixes.Add(_prefix);
                _listener.Start();
                _loop = Task.Run(ListenLoop);
            }

            public void Handle(string path, Func<HttpListenerContext, Task> handler) => _handlers[path] = handler;

            public string Url(string path) => _prefix.TrimEnd('/') + path;

            public async Task<string> ReadBody(HttpListenerContext context)
            {
                using var reader = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding ?? Encoding.UTF8);
                return await reader.ReadToEndAsync();
            }

            public async Task Write(HttpListenerContext context, int statusCode, string body)
            {
                var bytes = Encoding.UTF8.GetBytes(body);
                context.Response.StatusCode = statusCode;
                context.Response.ContentType = "application/json; charset=utf-8";
                context.Response.ContentLength64 = bytes.LongLength;
                await context.Response.OutputStream.WriteAsync(bytes, 0, bytes.Length);
                context.Response.Close();
            }

            private async Task ListenLoop()
            {
                while (!_cts.IsCancellationRequested)
                {
                    HttpListenerContext? context = null;
                    try
                    {
                        context = await _listener.GetContextAsync();
                        if (_handlers.TryGetValue(context.Request.Url?.AbsolutePath ?? string.Empty, out var handler))
                        {
                            await handler(context);
                        }
                        else
                        {
                            await Write(context, 404, "not found");
                        }
                    }
                    catch when (_cts.IsCancellationRequested)
                    {
                        break;
                    }
                    catch
                    {
                        if (context != null && context.Response.OutputStream.CanWrite)
                        {
                            try { await Write(context, 500, "error"); } catch { }
                        }
                    }
                }
            }

            public void Dispose()
            {
                _cts.Cancel();
                _listener.Stop();
                _listener.Close();
                try { _loop.Wait(TimeSpan.FromSeconds(1)); } catch { }
            }

            private static int GetFreePort()
            {
                var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
                listener.Start();
                var port = ((IPEndPoint)listener.LocalEndpoint).Port;
                listener.Stop();
                return port;
            }
        }
    }
}
