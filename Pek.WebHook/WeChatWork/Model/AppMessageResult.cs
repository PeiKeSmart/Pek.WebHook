#nullable enable
namespace DH.WebHook;

/// <summary>应用消息发送结果</summary>
public class AppMessageResult
{
    /// <summary>是否成功</summary>
    public Boolean Success { get; set; }

    /// <summary>错误码</summary>
    public Int32 ErrorCode { get; set; }

    /// <summary>错误信息</summary>
    public String? ErrorMessage { get; set; }

    /// <summary>无效用户</summary>
    public String? InvalidUser { get; set; }
}
