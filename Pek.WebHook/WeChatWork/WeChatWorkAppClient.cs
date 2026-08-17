#nullable enable
using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

using NewLife;
using NewLife.Log;

namespace DH.WebHook;

/// <summary>企业微信应用消息客户端。通过自建应用 API 给指定用户发送消息</summary>
public static class WeChatWorkAppClient
{
    #region 属性
    /// <summary>默认应用名称</summary>
    public const String DefaultAppName = "default";

    private static readonly ConcurrentDictionary<String, TokenCacheItem> _tokenCache = new();
    private static readonly SemaphoreSlim _tokenLock = new(1, 1);
    private static readonly HttpClient _httpClient = new()
    {
        BaseAddress = new Uri("https://qyapi.weixin.qq.com"),
        // 企业微信API正常毫秒级响应，设置合理超时，避免异常时长时间挂起占用线程
        Timeout = TimeSpan.FromSeconds(15),
    };

    /// <summary>Token缓存项</summary>
    private sealed class TokenCacheItem
    {
        /// <summary>访问令牌</summary>
        public String AccessToken { get; set; } = String.Empty;

        /// <summary>过期时间</summary>
        public DateTime ExpireTime { get; set; }
    }
    #endregion

    #region 应用获取

    /// <summary>获取应用信息（默认应用读取 WebHookSetting 主配置，其余从 WeChatWorkApps 集合按名称读取）</summary>
    /// <param name="appName">应用名称</param>
    /// <returns>应用配置</returns>
    private static WeChatWorkAppConfig GetApp(String appName)
    {
        if (appName.IsNullOrWhiteSpace()) throw new ArgumentNullException(nameof(appName));

        var s = WebHookSetting.Current;

        // 默认应用：读取 WebHookSetting 主配置
        if (appName == DefaultAppName)
        {
            if (s.WeChatWorkCorpId.IsNullOrWhiteSpace()) throw new InvalidOperationException("未配置企业微信CorpId");
            if (s.WeChatWorkAgentId.IsNullOrWhiteSpace()) throw new InvalidOperationException("未配置企业微信AgentId");
            if (s.WeChatWorkCorpSecret.IsNullOrWhiteSpace()) throw new InvalidOperationException("未配置企业微信CorpSecret");

            return new WeChatWorkAppConfig { Id = s.WeChatWorkAgentId, Name = DefaultAppName, Secret = s.WeChatWorkCorpSecret };
        }

        // 附加应用：从 WeChatWorkApps 集合按名称查找
        var cfg = s.WeChatWorkApps?.FirstOrDefault(e => e.Name.EqualIgnoreCase(appName));
        if (cfg == null) throw new InvalidOperationException($"未配置企业微信应用: {appName}");
        if (cfg.Id.IsNullOrWhiteSpace()) throw new InvalidOperationException($"未配置企业微信应用[{appName}] Id");
        if (cfg.Secret.IsNullOrWhiteSpace()) throw new InvalidOperationException($"未配置企业微信应用[{appName}] Secret");

        return cfg;
    }
    #endregion

    /// <summary>异步获取AccessToken（自动缓存，过期刷新），指定应用</summary>
    /// <param name="appName">应用名称（默认应用或 WeChatWorkApps 集合中已配置的应用）</param>
    /// <returns>AccessToken字符串</returns>
    public static async Task<String> GetAccessTokenAsync(String appName)
    {
        var app = GetApp(appName);

        var key = $"wx:{appName}";

        if (_tokenCache.TryGetValue(key, out var item) && DateTime.UtcNow < item.ExpireTime)
            return item.AccessToken;

        await _tokenLock.WaitAsync().ConfigureAwait(false);
        try
        {
            // 双重检查，避免并发重复获取
            if (_tokenCache.TryGetValue(key, out item) && DateTime.UtcNow < item.ExpireTime)
                return item.AccessToken;

            var corpId = WebHookSetting.Current.WeChatWorkCorpId;
            var url = $"/cgi-bin/gettoken?corpid={corpId}&corpsecret={app.Secret}";
            var response = await _httpClient.GetFromJsonAsync<GetTokenResponse>(url).ConfigureAwait(false);

            if (response == null || response.AccessToken.IsNullOrEmpty())
                throw new InvalidOperationException($"获取企业微信AccessToken失败: {response?.ErrMsg ?? "响应为空"}");

            item = new TokenCacheItem
            {
                AccessToken = response.AccessToken,
                // 提前5分钟过期，确保安全
                ExpireTime = DateTime.UtcNow.AddSeconds(response.ExpiresIn - 300),
            };
            _tokenCache[key] = item;

            XTrace.WriteLine("[WeChatWorkApp] 获取AccessToken成功，有效期{0}秒", response.ExpiresIn);
            return item.AccessToken;
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    /// <summary>发送文本消息给指定用户，使用默认应用</summary>
    /// <param name="userId">用户UserID（多人用|分隔）</param>
    /// <param name="content">文本内容，最长2048字节</param>
    /// <param name="safe">是否保密消息</param>
    /// <returns>发送结果</returns>
    public static Task<AppMessageResult> SendTextAsync(String userId, String content, Boolean safe = false) =>
        SendTextAsync(DefaultAppName, userId, content, safe);

    /// <summary>发送文本消息给指定用户，指定应用</summary>
    /// <param name="appName">应用名称（需先 RegisterApp）</param>
    /// <param name="userId">用户UserID（多人用|分隔）</param>
    /// <param name="content">文本内容，最长2048字节</param>
    /// <param name="safe">是否保密消息</param>
    /// <returns>发送结果</returns>
    public static async Task<AppMessageResult> SendTextAsync(String appName, String userId, String content, Boolean safe = false)
    {
        if (userId.IsNullOrWhiteSpace()) throw new ArgumentNullException(nameof(userId));
        if (content.IsNullOrWhiteSpace()) throw new ArgumentNullException(nameof(content));

        var app = GetApp(appName);
        var token = await GetAccessTokenAsync(appName).ConfigureAwait(false);

        var body = new TextMessageRequest
        {
            ToUser = userId,
            AgentId = app.Id,
            Text = new TextContent { Content = content },
            Safe = safe ? 1 : 0
        };

        return await SendMessageAsync(token, body).ConfigureAwait(false);
    }

    /// <summary>发送Markdown消息给指定用户，使用默认应用</summary>
    /// <param name="userId">用户UserID（多人用|分隔）</param>
    /// <param name="content">Markdown内容，最长4096字节</param>
    /// <returns>发送结果</returns>
    public static Task<AppMessageResult> SendMarkdownAsync(String userId, String content) =>
        SendMarkdownAsync(DefaultAppName, userId, content);

    /// <summary>发送Markdown消息给指定用户，指定应用</summary>
    /// <param name="appName">应用名称（需先 RegisterApp）</param>
    /// <param name="userId">用户UserID（多人用|分隔）</param>
    /// <param name="content">Markdown内容，最长4096字节</param>
    /// <returns>发送结果</returns>
    public static async Task<AppMessageResult> SendMarkdownAsync(String appName, String userId, String content)
    {
        if (userId.IsNullOrWhiteSpace()) throw new ArgumentNullException(nameof(userId));
        if (content.IsNullOrWhiteSpace()) throw new ArgumentNullException(nameof(content));

        var app = GetApp(appName);
        var token = await GetAccessTokenAsync(appName).ConfigureAwait(false);

        var body = new MarkdownMessageRequest
        {
            ToUser = userId,
            AgentId = app.Id,
            Markdown = new MarkdownContent { Content = content }
        };

        return await SendMessageAsync(token, body).ConfigureAwait(false);
    }

    /// <summary>发送文本卡片消息给指定用户（带链接跳转），使用默认应用</summary>
    /// <param name="userId">用户UserID（多人用|分隔）</param>
    /// <param name="title">标题，最长128字节</param>
    /// <param name="description">描述，最长512字节</param>
    /// <param name="url">点击后跳转的链接</param>
    /// <param name="btnTxt">按钮文字，最长16字节。默认"查看详情"</param>
    /// <returns>发送结果</returns>
    public static Task<AppMessageResult> SendTextCardAsync(String userId, String title, String description, String url, String? btnTxt = null) =>
        SendTextCardAsync(DefaultAppName, userId, title, description, url, btnTxt);

    /// <summary>发送文本卡片消息给指定用户（带链接跳转），指定应用</summary>
    /// <param name="appName">应用名称（需先 RegisterApp）</param>
    /// <param name="userId">用户UserID（多人用|分隔）</param>
    /// <param name="title">标题，最长128字节</param>
    /// <param name="description">描述，最长512字节</param>
    /// <param name="url">点击后跳转的链接</param>
    /// <param name="btnTxt">按钮文字，最长16字节</param>
    /// <returns>发送结果</returns>
    public static async Task<AppMessageResult> SendTextCardAsync(String appName, String userId, String title, String description, String url, String? btnTxt)
    {
        if (userId.IsNullOrWhiteSpace()) throw new ArgumentNullException(nameof(userId));
        if (title.IsNullOrWhiteSpace()) throw new ArgumentNullException(nameof(title));
        if (url.IsNullOrWhiteSpace()) throw new ArgumentNullException(nameof(url));

        var app = GetApp(appName);
        var token = await GetAccessTokenAsync(appName).ConfigureAwait(false);

        var body = new TextCardMessageRequest
        {
            ToUser = userId,
            AgentId = app.Id,
            TextCard = new TextCardContent
            {
                Title = title,
                Description = description,
                Url = url,
                BtnTxt = btnTxt ?? "查看详情"
            }
        };

        return await SendMessageAsync(token, body).ConfigureAwait(false);
    }

    /// <summary>
    /// 发送消息（通用方法）
    /// </summary>
    private static async Task<AppMessageResult> SendMessageAsync(String token, Object body)
    {
        var url = $"/cgi-bin/message/send?access_token={token}";

        try
        {
            var response = await _httpClient.PostAsJsonAsync(url, body);
            var result = await response.Content.ReadFromJsonAsync<SendMessageResponse>();

            if (result == null)
                return new AppMessageResult { Success = false, ErrorMessage = "响应为空" };

            if (result.ErrorCode != 0)
            {
                XTrace.WriteLine("[WeChatWorkApp] 消息发送失败: errcode={0}, errmsg={1}", result.ErrorCode, result.ErrorMessage);
                return new AppMessageResult { Success = false, ErrorCode = result.ErrorCode, ErrorMessage = result.ErrorMessage };
            }

            XTrace.WriteLine("[WeChatWorkApp] 消息发送成功，无效用户: {0}", result.InvalidUser);
            return new AppMessageResult { Success = true, InvalidUser = result.InvalidUser };
        }
        catch (Exception ex)
        {
            XTrace.WriteException(ex);
            return new AppMessageResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    #region 内部模型

    private class GetTokenResponse
    {
        [JsonPropertyName("errcode")]
        public Int32 ErrorCode { get; set; }

        [JsonPropertyName("errmsg")]
        public String ErrMsg { get; set; } = String.Empty;

        [JsonPropertyName("access_token")]
        public String? AccessToken { get; set; }

        [JsonPropertyName("expires_in")]
        public Int32 ExpiresIn { get; set; }
    }

    private class TextMessageRequest
    {
        [JsonPropertyName("touser")]
        public String ToUser { get; set; } = String.Empty;

        [JsonPropertyName("msgtype")]
        public String MsgType => "text";

        [JsonPropertyName("agentid")]
        public String AgentId { get; set; } = String.Empty;

        [JsonPropertyName("text")]
        public TextContent Text { get; set; } = new();

        [JsonPropertyName("safe")]
        public Int32 Safe { get; set; }
    }

    private class TextContent
    {
        [JsonPropertyName("content")]
        public String Content { get; set; } = String.Empty;
    }

    private class MarkdownMessageRequest
    {
        [JsonPropertyName("touser")]
        public String ToUser { get; set; } = String.Empty;

        [JsonPropertyName("msgtype")]
        public String MsgType => "markdown";

        [JsonPropertyName("agentid")]
        public String AgentId { get; set; } = String.Empty;

        [JsonPropertyName("markdown")]
        public MarkdownContent Markdown { get; set; } = new();
    }

    private class MarkdownContent
    {
        [JsonPropertyName("content")]
        public String Content { get; set; } = String.Empty;
    }

    private class TextCardMessageRequest
    {
        [JsonPropertyName("touser")]
        public String ToUser { get; set; } = String.Empty;

        [JsonPropertyName("msgtype")]
        public String MsgType => "textcard";

        [JsonPropertyName("agentid")]
        public String AgentId { get; set; } = String.Empty;

        [JsonPropertyName("textcard")]
        public TextCardContent TextCard { get; set; } = new();
    }

    private class TextCardContent
    {
        [JsonPropertyName("title")]
        public String Title { get; set; } = String.Empty;

        [JsonPropertyName("description")]
        public String Description { get; set; } = String.Empty;

        [JsonPropertyName("url")]
        public String Url { get; set; } = String.Empty;

        [JsonPropertyName("btntxt")]
        public String BtnTxt { get; set; } = String.Empty;
    }

    private class SendMessageResponse
    {
        [JsonPropertyName("errcode")]
        public Int32 ErrorCode { get; set; }

        [JsonPropertyName("errmsg")]
        public String ErrorMessage { get; set; } = String.Empty;

        [JsonPropertyName("invaliduser")]
        public String? InvalidUser { get; set; }
    }

    #endregion
}
