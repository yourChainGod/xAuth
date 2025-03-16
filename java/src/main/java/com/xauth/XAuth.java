package com.xauth;

import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.ObjectMapper;
import org.apache.http.HttpEntity;
import org.apache.http.HttpResponse;
import org.apache.http.HttpStatus;
import org.apache.http.NameValuePair;
import org.apache.http.client.CookieStore;
import org.apache.http.client.config.RequestConfig;
import org.apache.http.client.entity.UrlEncodedFormEntity;
import org.apache.http.client.methods.CloseableHttpResponse;
import org.apache.http.client.methods.HttpGet;
import org.apache.http.client.methods.HttpPost;
import org.apache.http.client.methods.HttpRequestBase;
import org.apache.http.client.protocol.HttpClientContext;
import org.apache.http.client.utils.URIBuilder;
import org.apache.http.cookie.Cookie;
import org.apache.http.impl.client.BasicCookieStore;
import org.apache.http.impl.client.CloseableHttpClient;
import org.apache.http.impl.client.HttpClients;
import org.apache.http.impl.cookie.BasicClientCookie;
import org.apache.http.message.BasicNameValuePair;
import org.apache.http.util.EntityUtils;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

import java.io.IOException;
import java.net.URI;
import java.net.URISyntaxException;
import java.nio.charset.StandardCharsets;
import java.util.*;
import java.util.regex.Matcher;
import java.util.regex.Pattern;

/**
 * Twitter OAuth认证工具类
 * 支持OAuth1和OAuth2认证流程
 */
public class XAuth {
    private static final Logger logger = LoggerFactory.getLogger(XAuth.class);

    // 常量定义
    private static final String TWITTER_AUTHORITY = "twitter.com";
    private static final String TWITTER_ORIGIN = "https://twitter.com";
    private static final String TWITTER_API_BASE = "https://twitter.com/i/api/2";
    private static final String USER_AGENT = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36";
    private static final String AUTHORIZATION = "Bearer AAAAAAAAAAAAAAAAAAAAANRILgAAAAAAnNwIzUejRCOuH5E6I8xnZz4puTs%3D1Zv7ttfk8LF81IUq16cHjhLTvJu4FA33AGWWjCpTnA";
    private static final int MAX_RETRIES = 3;
    private static final int RETRY_INTERVAL = 1000; // milliseconds

    // 账户状态码映射
    private static final Map<Integer, String> ACCOUNT_STATE = new HashMap<>();
    static {
        ACCOUNT_STATE.put(32, "Bad Token");
        ACCOUNT_STATE.put(64, "SUSPENDED");
        ACCOUNT_STATE.put(141, "SUSPENDED");
        ACCOUNT_STATE.put(326, "LOCKED");
    }

    private final String authToken;
    private final CloseableHttpClient client;
    private final CloseableHttpClient client2;
    private final CookieStore cookieStore;
    private final HttpClientContext context;
    private final ObjectMapper objectMapper;

    /**
     * 初始化XAuth实例
     *
     * @param authToken Twitter认证token
     * @throws IllegalArgumentException 当auth_token为空时
     */
    public XAuth(String authToken) {
        if (authToken == null || authToken.isEmpty()) {
            throw new IllegalArgumentException("auth_token不能为空");
        }

        this.authToken = authToken;
        this.objectMapper = new ObjectMapper();
        this.cookieStore = new BasicCookieStore();

        // 设置auth_token cookie
        BasicClientCookie cookie = new BasicClientCookie("auth_token", authToken);
        cookie.setDomain(TWITTER_AUTHORITY);
        cookie.setPath("/");
        cookieStore.addCookie(cookie);

        // 创建HTTP上下文
        this.context = HttpClientContext.create();
        context.setCookieStore(cookieStore);

        // 创建HTTP客户端
        RequestConfig requestConfig = RequestConfig.custom()
                .setConnectTimeout(10000)
                .setSocketTimeout(10000)
                .build();

        this.client = HttpClients.custom()
                .setDefaultCookieStore(cookieStore)
                .setDefaultRequestConfig(requestConfig)
                .build();

        this.client2 = HttpClients.custom()
                .setDefaultCookieStore(cookieStore)
                .setDefaultRequestConfig(requestConfig)
                .build();
    }

    /**
     * 设置请求头
     *
     * @param request HTTP请求
     * @param includeTwitterHeaders 是否包含Twitter特定的headers
     */
    private void setHeaders(HttpRequestBase request, boolean includeTwitterHeaders) {
        request.setHeader("User-Agent", USER_AGENT);

        if (includeTwitterHeaders) {
            request.setHeader("authority", TWITTER_AUTHORITY);
            request.setHeader("origin", TWITTER_ORIGIN);
            request.setHeader("x-twitter-auth-type", "OAuth2Session");
            request.setHeader("x-twitter-active-user", "yes");
            request.setHeader("authorization", AUTHORIZATION);
        }
    }

    /**
     * 处理响应状态
     *
     * @param response HTTP响应
     * @throws IOException 当请求失败时
     * @throws InterruptedException 当线程被中断时
     */
    private void handleResponse(HttpResponse response) throws IOException, InterruptedException {
        int statusCode = response.getStatusLine().getStatusCode();
        if (statusCode == HttpStatus.SC_TOO_MANY_REQUESTS) {
            Thread.sleep(RETRY_INTERVAL);
        }
    }

    /**
     * 获取Twitter认证token
     *
     * @param oauthToken OAuth token
     * @return 认证token字符串
     * @throws IOException 当请求失败时
     * @throws InterruptedException 当线程被中断时
     * @throws XAuthException 当token无效或响应格式不正确时
     */
    public String getTwitterToken(String oauthToken) throws IOException, InterruptedException, XAuthException {
        if (oauthToken == null || oauthToken.isEmpty()) {
            throw new XAuthException("oauth_token不能为空");
        }

        URI uri;
        try {
            uri = new URIBuilder("https://api.x.com/oauth/authenticate")
                    .addParameter("oauth_token", oauthToken)
                    .build();
        } catch (URISyntaxException e) {
            throw new XAuthException("构建URI失败", e);
        }

        HttpGet request = new HttpGet(uri);
        setHeaders(request, false);

        try (CloseableHttpResponse response = client2.execute(request, context)) {
            handleResponse(response);
            
            HttpEntity entity = response.getEntity();
            String content = EntityUtils.toString(entity, StandardCharsets.UTF_8);

            if (!content.contains("authenticity_token")) {
                if (content.contains("The request token for this page is invalid")) {
                    throw new XAuthException("请求oauth_token无效");
                }
                throw new XAuthException("响应中未找到authenticity_token");
            }

            // 尝试两种可能的token格式
            String[] tokenMarkers = {
                    "name=\"authenticity_token\" value=\"",
                    "name=\"authenticity_token\" type=\"hidden\" value=\""
            };

            String token = null;
            for (String marker : tokenMarkers) {
                if (content.contains(marker)) {
                    int startIndex = content.indexOf(marker) + marker.length();
                    int endIndex = content.indexOf("\"", startIndex);
                    if (startIndex >= 0 && endIndex > startIndex) {
                        token = content.substring(startIndex, endIndex);
                        break;
                    }
                }
            }

            if (token == null || token.isEmpty()) {
                throw new XAuthException("获取到的authenticity_token为空");
            }

            return token;
        }
    }

    /**
     * 执行OAuth1认证流程
     *
     * @param oauthToken OAuth token
     * @return OAuth验证码
     * @throws IOException 当请求失败时
     * @throws InterruptedException 当线程被中断时
     * @throws XAuthException 当认证失败时
     */
    public String oauth1(String oauthToken) throws IOException, InterruptedException, XAuthException {
        String authenticityToken = getTwitterToken(oauthToken);

        HttpPost request = new HttpPost("https://x.com/oauth/authorize");
        setHeaders(request, false);

        List<NameValuePair> params = new ArrayList<>();
        params.add(new BasicNameValuePair("authenticity_token", authenticityToken));
        params.add(new BasicNameValuePair("oauth_token", oauthToken));
        request.setEntity(new UrlEncodedFormEntity(params, StandardCharsets.UTF_8));

        try (CloseableHttpResponse response = client2.execute(request, context)) {
            handleResponse(response);
            
            HttpEntity entity = response.getEntity();
            String content = EntityUtils.toString(entity, StandardCharsets.UTF_8);

            if (!content.contains("oauth_verifier")) {
                if (content.contains("This account is suspended.")) {
                    throw new XAuthException("该账户已被封禁");
                }
                throw new XAuthException("未找到oauth_verifier");
            }

            Pattern pattern = Pattern.compile("oauth_verifier=([^\"&]+)");
            Matcher matcher = pattern.matcher(content);
            if (matcher.find()) {
                String verifier = matcher.group(1);
                if (verifier == null || verifier.isEmpty()) {
                    throw new XAuthException("获取到的oauth_verifier为空");
                }
                return verifier;
            } else {
                throw new XAuthException("未找到oauth_verifier");
            }
        }
    }

    /**
     * 获取认证码
     *
     * @param params 请求参数
     * @return 认证码
     * @throws IOException 当请求失败时
     * @throws InterruptedException 当线程被中断时
     * @throws XAuthException 当参数无效或响应格式不正确时
     */
    public String getAuthCode(Map<String, String> params) throws IOException, InterruptedException, XAuthException {
        if (params == null || params.isEmpty()) {
            throw new XAuthException("参数不能为空");
        }

        URIBuilder uriBuilder = new URIBuilder();
        uriBuilder.setScheme("https")
                .setHost(TWITTER_AUTHORITY)
                .setPath("/i/api/2/oauth2/authorize");

        for (Map.Entry<String, String> entry : params.entrySet()) {
            uriBuilder.addParameter(entry.getKey(), entry.getValue());
        }

        URI uri;
        try {
            uri = uriBuilder.build();
        } catch (URISyntaxException e) {
            throw new XAuthException("构建URI失败", e);
        }

        HttpGet request = new HttpGet(uri);
        setHeaders(request, true);

        try (CloseableHttpResponse response = client.execute(request, context)) {
            handleResponse(response);
            
            HttpEntity entity = response.getEntity();
            String content = EntityUtils.toString(entity, StandardCharsets.UTF_8);

            JsonNode data;
            try {
                data = objectMapper.readTree(content);
            } catch (Exception e) {
                throw new XAuthException("响应不是有效的JSON格式", e);
            }

            // 处理CSRF token
            if (data.has("code") && data.get("code").asInt() == 353) {
                // 获取ct0 cookie
                Cookie ct0Cookie = null;
                for (Cookie cookie : cookieStore.getCookies()) {
                    if ("ct0".equals(cookie.getName())) {
                        ct0Cookie = cookie;
                        break;
                    }
                }

                if (ct0Cookie != null) {
                    // 重新发起请求，带上CSRF token
                    HttpGet newRequest = new HttpGet(uri);
                    setHeaders(newRequest, true);
                    newRequest.setHeader("x-csrf-token", ct0Cookie.getValue());
                    
                    try (CloseableHttpResponse newResponse = client.execute(newRequest, context)) {
                        handleResponse(newResponse);
                        
                        HttpEntity newEntity = newResponse.getEntity();
                        String newContent = EntityUtils.toString(newEntity, StandardCharsets.UTF_8);
                        
                        try {
                            data = objectMapper.readTree(newContent);
                        } catch (Exception e) {
                            throw new XAuthException("响应不是有效的JSON格式", e);
                        }
                    }
                } else {
                    throw new XAuthException("未找到ct0 cookie");
                }
            }

            // 检查错误
            if (data.has("errors") && data.get("errors").isArray() && data.get("errors").size() > 0) {
                JsonNode error = data.get("errors").get(0);
                if (error.has("code")) {
                    int errorCode = error.get("code").asInt();
                    if (ACCOUNT_STATE.containsKey(errorCode)) {
                        throw new XAuthException("token状态错误: " + ACCOUNT_STATE.get(errorCode));
                    }
                }
            }

            // 获取auth_code
            if (!data.has("auth_code") || data.get("auth_code").isNull()) {
                throw new XAuthException("响应中未找到auth_code");
            }

            return data.get("auth_code").asText();
        }
    }

    /**
     * 执行OAuth2认证流程
     *
     * @param params 请求参数
     * @return 认证码
     * @throws IOException 当请求失败时
     * @throws InterruptedException 当线程被中断时
     * @throws XAuthException 当认证失败时
     */
    public String oauth2(Map<String, String> params) throws IOException, InterruptedException, XAuthException {
        String authCode = getAuthCode(params);

        HttpPost request = new HttpPost(TWITTER_API_BASE + "/oauth2/authorize");
        setHeaders(request, true);
        
        // 获取ct0 cookie并设置CSRF token
        for (Cookie cookie : cookieStore.getCookies()) {
            if ("ct0".equals(cookie.getName())) {
                request.setHeader("x-csrf-token", cookie.getValue());
                break;
            }
        }

        List<NameValuePair> formParams = new ArrayList<>();
        formParams.add(new BasicNameValuePair("approval", "true"));
        formParams.add(new BasicNameValuePair("code", authCode));
        request.setEntity(new UrlEncodedFormEntity(formParams, StandardCharsets.UTF_8));

        try (CloseableHttpResponse response = client.execute(request, context)) {
            handleResponse(response);
            
            HttpEntity entity = response.getEntity();
            String content = EntityUtils.toString(entity, StandardCharsets.UTF_8);

            if (!content.contains("redirect_uri")) {
                throw new XAuthException("响应中未找到redirect_uri");
            }

            return authCode;
        }
    }

    /**
     * 关闭HTTP客户端
     */
    public void close() {
        try {
            if (client != null) {
                client.close();
            }
            if (client2 != null) {
                client2.close();
            }
        } catch (IOException e) {
            logger.error("关闭HTTP客户端失败", e);
        }
    }
} 