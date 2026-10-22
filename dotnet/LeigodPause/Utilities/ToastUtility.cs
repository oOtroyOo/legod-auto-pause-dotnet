using System.Reflection;
using Microsoft.Toolkit.Uwp.Notifications;
using Microsoft.Win32;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;

namespace LeigodPause;

/// <summary>
/// Windows 10/11 系统 Toast 通知（操作中心 / 通知中心）工具。
/// 通知中心显示的应用来源名由 AUMID 的 DisplayName 决定；若不指定，会回退为 exe 文件名（英文）。
/// 这里注册一个自定义 AUMID 并写入中文 DisplayName / 图标，使通知以友好名称（如“雷神加速器”）显示。
/// </summary>
public static class ToastUtility
{
    private static readonly string NotifyRegistryKey = (string)(
        typeof(ToastNotificationManagerCompat).GetMethod("GetRegistrySubKey", BindingFlags.NonPublic | BindingFlags.Static, null, [], null)!
    ).Invoke(null, null);

    private static readonly ToastNotificationHistoryCompat ToastNotificationHistory = ToastNotificationManagerCompat.History;
    private static readonly ToastNotifierCompat ToastNotifierCompat = ToastNotificationManagerCompat.CreateToastNotifier();

    /// <summary>
    /// 弹出一条系统 Toast 通知。title 为加粗标题，message 为正文。
    /// tag 可选：相同 tag 会更新（而非叠加）同一条通知，避免重复堆积；
    /// appName 可选：通知底部的归属来源文本。
    /// </summary>
    public static void Show(string message, string? title = null, string? tag = null)
    {
        try
        {
            var builder = new ToastContentBuilder();
            if (!string.IsNullOrWhiteSpace(title))
            {
                builder.AddText(title);
            }

            builder.AddText(message)
                .Show(toast =>
                {
                    if (!string.IsNullOrEmpty(tag))
                    {
                        toast.Tag = tag;
                    }
                });
        }
        catch (Exception ex)
        {
            // 通知平台不可用（被组策略/服务禁用等）时不应影响主程序
            Debug.WriteLine($"[ToastUtility] 显示通知失败: {ex}");
        }
    }

    /// <summary>
    /// 移除指定 tag 的历史通知。
    /// </summary>
    public static void Remove(string tag)
    {
        if (!string.IsNullOrEmpty(tag))
        {
            ToastNotificationHistory.Remove(tag);
        }
    }
}