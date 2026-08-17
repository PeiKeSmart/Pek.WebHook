using System.ComponentModel;

namespace DH.WebHook;

/// <summary>企业微信自建应用配置</summary>
public class WeChatWorkAppConfig
{
    /// <summary>应用AgentId</summary>
    [Description("应用AgentId")]
    public String Id { get; set; } = String.Empty;

    /// <summary>应用名称（对应WeChatWorkAppClient的应用名）</summary>
    [Description("应用名称")]
    public String Name { get; set; } = String.Empty;

    /// <summary>应用Secret</summary>
    [Description("应用Secret")]
    public String Secret { get; set; } = String.Empty;
}
