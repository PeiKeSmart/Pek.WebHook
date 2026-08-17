using NewLife.Configuration;

using System.ComponentModel;

namespace DH.WebHook;

/// <summary>WebHooks设置</summary>
[DisplayName("WebHooks设置")]
//[XmlConfigFile("Config/WebHookSetting.config", 10000)]
[Config("WebHookSetting")]
//public class WebHookSetting : XmlConfig<WebHookSetting>
public class WebHookSetting : Config<WebHookSetting> {
    /// <summary>钉钉Host</summary>
    [Description("钉钉Host")]
    public String DingTalkHost { get; set; }

    /// <summary>钉钉机器人接口地址</summary>
    [Description("钉钉机器人接口地址")]
    public String DingTalkSendUrl { get; set; }

    /// <summary>企业微信机器人 Webhook 地址</summary>
    [Description("企业微信机器人 Webhook 地址")]
    public String WeChatWorkWebhookUrl { get; set; }

    #region 企业微信应用消息配置

    /// <summary>企业微信CorpId</summary>
    [Description("企业微信CorpId")]
    public String WeChatWorkCorpId { get; set; } = String.Empty;

    /// <summary>企业微信应用AgentId</summary>
    [Description("企业微信应用AgentId")]
    public String WeChatWorkAgentId { get; set; } = String.Empty;

    /// <summary>企业微信应用CorpSecret</summary>
    [Description("企业微信应用CorpSecret")]
    public String WeChatWorkCorpSecret { get; set; } = String.Empty;

    /// <summary>企业微信应用集合（Id/Name/Secret，Name对应应用名）</summary>
    [Description("企业微信应用集合")]
    public List<WeChatWorkAppConfig> WeChatWorkApps { get; set; } = [];

    #endregion
}
