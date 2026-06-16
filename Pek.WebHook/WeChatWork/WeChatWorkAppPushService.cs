using NewLife;
using NewLife.Log;

namespace DH.WebHook;

/// <summary>企业微信应用消息推送服务。基于 <see cref="WeChatWorkAppClient"/> 的便捷封装，自动读取 <see cref="WebHookSetting"/> 配置</summary>
public static class WeChatWorkAppPushService
{
    /// <summary>
    /// 发送文本消息给指定企业微信用户
    /// </summary>
    /// <param name="weComUserId">企业微信UserID（多人用|分隔）</param>
    /// <param name="content">文本内容</param>
    /// <returns>发送结果</returns>
    public static async Task<AppMessageResult> SendTextAsync(String weComUserId, String content)
    {
        try
        {
            return await WeChatWorkAppClient.SendTextAsync(weComUserId, content);
        }
        catch (Exception ex)
        {
            XTrace.WriteException(ex);
            return new AppMessageResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    /// <summary>
    /// 发送Markdown消息给指定企业微信用户
    /// </summary>
    /// <param name="weComUserId">企业微信UserID（多人用|分隔）</param>
    /// <param name="content">Markdown内容</param>
    /// <returns>发送结果</returns>
    public static async Task<AppMessageResult> SendMarkdownAsync(String weComUserId, String content)
    {
        try
        {
            return await WeChatWorkAppClient.SendMarkdownAsync(weComUserId, content);
        }
        catch (Exception ex)
        {
            XTrace.WriteException(ex);
            return new AppMessageResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    /// <summary>
    /// 发送文本卡片消息给指定企业微信用户（带链接跳转）
    /// </summary>
    /// <param name="weComUserId">企业微信UserID（多人用|分隔）</param>
    /// <param name="title">标题</param>
    /// <param name="description">描述</param>
    /// <param name="url">链接地址</param>
    /// <param name="btnText">按钮文字</param>
    /// <returns>发送结果</returns>
    public static async Task<AppMessageResult> SendTextCardAsync(String weComUserId, String title, String description, String url, String? btnText = null)
    {
        try
        {
            return await WeChatWorkAppClient.SendTextCardAsync(weComUserId, title, description, url, btnText);
        }
        catch (Exception ex)
        {
            XTrace.WriteException(ex);
            return new AppMessageResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    /// <summary>
    /// 发送告警消息给指定企业微信用户
    /// </summary>
    /// <param name="weComUserId">企业微信UserID</param>
    /// <param name="title">告警标题</param>
    /// <param name="detail">告警详情</param>
    /// <returns>发送结果</returns>
    public static async Task<AppMessageResult> SendAlertAsync(String weComUserId, String title, String detail)
    {
        var content = $"## ⚠️ {title}\n\n{detail}\n\n> 请及时处理";
        return await SendMarkdownAsync(weComUserId, content);
    }
}
