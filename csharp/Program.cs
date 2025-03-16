using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace XAuth
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("Twitter OAuth认证测试");
            Console.WriteLine("=====================");

            try
            {
                // 创建XAuth实例
                using var xAuth = new XAuthClient("5a0be24d859ad6820906bbc28b0ac9219a71489b");

                // OAuth2认证测试
                Console.WriteLine("\n执行OAuth2认证测试...");
                var parameters = new Dictionary<string, string>
                {
                    { "code_challenge", "challenge" },
                    { "code_challenge_method", "plain" },
                    { "client_id", "MjdoOFVuNlJwRmxQT1VnWVBiUkw6MTpjaQ" },
                    { "redirect_uri", "https://dapp.uxlink.io/authGateway" },
                    { "response_type", "code" },
                    { "scope", "tweet.read users.read follows.read follows.write offline.access" },
                    { "state", "1867096870644760576" }
                };

                try
                {
                    string authCode = await xAuth.OAuth2Async(parameters);
                    Console.WriteLine($"OAuth2认证成功，认证码: {authCode}");
                }
                catch (XAuthException ex)
                {
                    Console.WriteLine($"OAuth2认证失败: {ex.Message}");
                }

                // OAuth1认证测试
                Console.WriteLine("\n执行OAuth1认证测试...");
                try
                {
                    string oauth1Verifier = await xAuth.OAuth1Async("lSgWPQAAAAABuWQYAAABk7sKZI0");
                    Console.WriteLine($"OAuth1认证成功，验证码: {oauth1Verifier}");
                }
                catch (XAuthException ex)
                {
                    Console.WriteLine($"OAuth1认证失败: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"发生错误: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"内部错误: {ex.InnerException.Message}");
                }
            }

            Console.WriteLine("\n测试完成，按任意键退出...");
            Console.ReadKey();
        }
    }
} 