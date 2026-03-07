using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace XAuth
{
    class Program
    {
        static string MustEnv(string key)
        {
            var value = Environment.GetEnvironmentVariable(key);
            if (string.IsNullOrEmpty(value))
            {
                throw new InvalidOperationException($"请先设置 {key} 环境变量");
            }
            return value;
        }

        static string EnvOr(string key, string fallback)
        {
            var value = Environment.GetEnvironmentVariable(key);
            return string.IsNullOrEmpty(value) ? fallback : value;
        }

        static async Task Main(string[] args)
        {
            try
            {
                using var xAuth = new XAuthClient(MustEnv("XAUTH_AUTH_TOKEN"));

                var parameters = new Dictionary<string, string>
                {
                    { "code_challenge", EnvOr("XAUTH_CODE_CHALLENGE", "challenge") },
                    { "code_challenge_method", EnvOr("XAUTH_CODE_CHALLENGE_METHOD", "plain") },
                    { "client_id", MustEnv("XAUTH_CLIENT_ID") },
                    { "redirect_uri", MustEnv("XAUTH_REDIRECT_URI") },
                    { "response_type", "code" },
                    { "scope", EnvOr("XAUTH_SCOPE", "tweet.read users.read follows.read follows.write offline.access") },
                    { "state", EnvOr("XAUTH_STATE", "demo-state") }
                };

                string authCode = await xAuth.OAuth2Async(parameters);
                Console.WriteLine($"OAuth2 认证码: {authCode}");

                var oauth1Token = Environment.GetEnvironmentVariable("XAUTH_OAUTH1_TOKEN");
                if (!string.IsNullOrEmpty(oauth1Token))
                {
                    string oauth1Verifier = await xAuth.OAuth1Async(oauth1Token);
                    Console.WriteLine($"OAuth1 验证码: {oauth1Verifier}");
                }
                else
                {
                    Console.WriteLine("未设置 XAUTH_OAUTH1_TOKEN，跳过 OAuth1 示例");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"错误: {ex.Message}");
                Environment.ExitCode = 1;
            }
        }
    }
}
