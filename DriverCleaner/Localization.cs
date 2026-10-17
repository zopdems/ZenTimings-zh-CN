using System;
using System.Collections.Generic;
using System.Globalization;

namespace ZenTimings.DriverCleaner
{
    /// <summary>
    /// 清理进程用的极简本地化。
    ///
    /// DriverCleaner.exe 是独立的控制台程序，没有 WPF 应用上下文（没有 Application.Current），
    /// 因此无法复用主程序的资源字典，这里自带一份双语表，按系统 UI 语言选择。
    /// 文案需与主程序 WPF/Localization/StringsChinese.xaml 中的 Loc_Driver_* 保持一致。
    /// </summary>
    internal static class Loc
    {
        private static readonly Dictionary<string, string> En = new Dictionary<string, string>
        {
            ["DriverCleanupFailed"] = "Driver cleanup failed: ",
            ["OpenScmFailed"] = "Could not open the Service Control Manager. Error: ",
            ["OpenServiceFailed"] = "Could not open the inpoutx64 service. Error: ",
            ["QueryServiceFailed"] = "Could not query the inpoutx64 service. Error: ",
            ["StopFailed"] = "Could not stop the inpoutx64 driver. Error: ",
            ["Removed"] = "The inpoutx64 driver was successfully removed.",
            ["FileDeleteFailed"] = "The service was removed, but the driver file could not be deleted.",
            ["FileDeleteFailed2"] = "The inpoutx64 service was removed, but the driver file could not be deleted.",
            ["InUse"] = "The inpoutx64 driver is already being stopped or is currently in use. Cleanup was aborted.",
            ["StopAborted"] = "The inpoutx64 driver could not be stopped. It may be in use by another application. Cleanup was aborted.",
            ["ServiceDeleteFailed"] = "The inpoutx64 driver stopped, but the service could not be deleted.",
        };

        private static readonly Dictionary<string, string> Zh = new Dictionary<string, string>
        {
            ["DriverCleanupFailed"] = "驱动清理失败：",
            ["OpenScmFailed"] = "无法打开服务控制管理器。错误：",
            ["OpenServiceFailed"] = "无法打开 inpoutx64 服务。错误：",
            ["QueryServiceFailed"] = "无法查询 inpoutx64 服务。错误：",
            ["StopFailed"] = "无法停止 inpoutx64 驱动。错误：",
            ["Removed"] = "inpoutx64 驱动已成功移除。",
            ["FileDeleteFailed"] = "服务已移除，但无法删除驱动文件。",
            ["FileDeleteFailed2"] = "inpoutx64 服务已移除，但无法删除驱动文件。",
            ["InUse"] = "inpoutx64 驱动正在停止或正被占用，清理已中止。",
            ["StopAborted"] = "无法停止 inpoutx64 驱动，可能被其他程序占用。清理已中止。",
            ["ServiceDeleteFailed"] = "inpoutx64 驱动已停止，但无法删除服务。",
        };

        private static readonly bool UseChinese = DetectChinese();

        private static bool DetectChinese()
        {
            try
            {
                string name = CultureInfo.CurrentUICulture.Name;
                return !string.IsNullOrEmpty(name) &&
                       name.StartsWith("zh", StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>取一条界面文本；找不到就退回英文，再找不到就返回 key 本身。</summary>
        public static string Get(string key)
        {
            string value;
            if (UseChinese && Zh.TryGetValue(key, out value))
                return value;
            if (En.TryGetValue(key, out value))
                return value;
            return key;
        }
    }
}
