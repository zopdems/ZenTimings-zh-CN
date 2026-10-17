using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;

namespace ZenTimings.Localization
{
    /// <summary>
    /// 运行时语言切换。界面文本通过 DynamicResource 引用 Localization/Strings*.xaml 中的条目，
    /// 切换语言时只需整体替换 Application 级资源字典，已打开的窗口会自动刷新。
    /// </summary>
    public sealed class LocalizationManager
    {
        /// <summary>内置语言的代码。</summary>
        public const string EnglishCode = "en-US";
        public const string ChineseSimplifiedCode = "zh-CN";

        /// <summary>打包在程序中的语言，顺序即选项框中的显示顺序。</summary>
        public static readonly IReadOnlyList<string> SupportedLanguages =
            new[] { EnglishCode, ChineseSimplifiedCode };

        private static readonly Lazy<LocalizationManager> Lazy =
            new Lazy<LocalizationManager>(() => new LocalizationManager());

        public static LocalizationManager Instance => Lazy.Value;

        /// <summary>语言切换后触发。</summary>
        public event EventHandler LanguageChanged;

        private readonly Dictionary<string, ResourceDictionary> cache =
            new Dictionary<string, ResourceDictionary>(StringComparer.OrdinalIgnoreCase);

        private readonly object sync = new object();
        private string current = EnglishCode;

        private LocalizationManager() { }

        /// <summary>当前语言代码（如 zh-CN）。</summary>
        public string Language => current;

        /// <summary>语言代码对应的显示名（始终用该语言自身书写，便于用户辨认）。</summary>
        public static string GetDisplayName(string code)
        {
            switch (Normalize(code))
            {
                case ChineseSimplifiedCode: return "简体中文";
                case EnglishCode: return "English";
                default: return code;
            }
        }

        /// <summary>
        /// 把任意语言代码归一到受支持的代码；无法识别时回退到英文。
        /// 简体中文（zh-Hans、zh-CN、zh-SG 等）统一映射到 zh-CN。
        /// </summary>
        public static string Normalize(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
                return EnglishCode;

            string lower = code.Trim().ToLowerInvariant();

            foreach (string supported in SupportedLanguages)
            {
                if (string.Equals(lower, supported, StringComparison.OrdinalIgnoreCase))
                    return supported;
            }

            // 中文的各种写法：zh、zh-Hans、zh-Hant-CN 等
            if (lower.StartsWith("zh", StringComparison.Ordinal))
                return lower.Contains("hant") || lower.Contains("tw") || lower.Contains("hk")
                    ? EnglishCode
                    : ChineseSimplifiedCode;

            return EnglishCode;
        }

        /// <summary>
        /// 根据系统 UI 语言猜测一个可用的初始语言。
        /// 用 InstalledUICulture 而不是 CurrentUICulture：应用启动时会把线程的
        /// CurrentUICulture 强制设为 en-US，用它探测永远只能得到英文。
        /// </summary>
        public static string DetectFromSystem()
        {
            try
            {
                return Normalize(CultureInfo.InstalledUICulture.Name);
            }
            catch
            {
                return EnglishCode;
            }
        }

        /// <summary>
        /// 应用指定语言。线程安全；重复应用同一语言会被忽略。
        /// </summary>
        public void Apply(string language, bool force = false)
        {
            string target = Normalize(language);

            lock (sync)
            {
                if (!force && string.Equals(target, current, StringComparison.OrdinalIgnoreCase))
                    return;

                // 英文没有单独的文件（App.xaml 已合并），此时 dictionary 为 null，
                // 摘掉其它语言字典就等于回到英文。
                ResourceDictionary dictionary = GetDictionary(target);

                current = target;

                Application app = Application.Current;
                if (app == null)
                    return;

                app.Dispatcher.Invoke(() =>
                {
                    var merged = app.Resources.MergedDictionaries;

                    // 先摘掉上一个字符串字典，再挂新的，避免重复累积。
                    for (int i = merged.Count - 1; i >= 0; i--)
                    {
                        if (merged[i] is StringResourceDictionary)
                            merged.RemoveAt(i);
                    }

                    if (dictionary != null)
                        merged.Add(dictionary);

                    // 让已打开的窗口、托盘菜单等按新语言重新取值。
                    // ThemedWindow 位于父命名空间 ZenTimings，直接引用即可。
                    try
                    {
                        ThemedWindow.RefreshAllOpenWindows();
                    }
                    catch { }
                });
            }

            LanguageChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// 取一条界面文本。找不到资源时返回 key 本身，便于直接定位缺失项。
        /// </summary>
        public string Get(string key)
        {
            if (string.IsNullOrEmpty(key))
                return string.Empty;

            try
            {
                Application app = Application.Current;
                if (app == null)
                    return key;

                object value = app.TryFindResource(key);
                string text = value as string;

                // TryFindResource 找不到会抛异常，这里再兜一次底。
                if (text == null && app.Resources.Contains(key))
                    text = app.Resources[key] as string;

                return text ?? key;
            }
            catch
            {
                return key;
            }
        }

        /// <summary>取一条带占位符的文本，如 Get("X", 3) => string.Format(X, 3)。</summary>
        public static string Format(string key, params object[] args)
        {
            string template = Instance.Get(key);
            if (args == null || args.Length == 0)
                return template;

            try
            {
                return string.Format(template, args);
            }
            catch (FormatException)
            {
                return template;
            }
        }

        private ResourceDictionary GetDictionary(string language)
        {
            lock (sync)
            {
                if (cache.TryGetValue(language, out ResourceDictionary cached))
                    return cached;

                ResourceDictionary loaded = Load(language);
                if (loaded != null)
                    cache[language] = loaded;

                return loaded;
            }
        }

        /// <summary>
        /// 语言代码对应的资源文件名。英文由 App.xaml 直接合并，不需要单独文件。
        /// 注意：文件名不能带 .zh-CN / .zhCN 这类区域性后缀，否则 MSBuild 的
        /// AssignCulture 会把它当成区域性资源，资源名不会以预期路径注册，
        /// pack URI 就定位不到（表现为界面切不了中文）。
        /// </summary>
        private static string ResourceFileName(string language)
        {
            switch (Normalize(language))
            {
                case ChineseSimplifiedCode: return "StringsChinese.xaml";
                default: return null;
            }
        }

        private static ResourceDictionary Load(string language)
        {
            string file = ResourceFileName(language);
            if (file == null)
                return null;

            string[] uris =
            {
                $"pack://application:,,,/ZenTimings;component/Localization/{file}",
                $"pack://application:,,,/Localization/{file}",
            };

            foreach (string uri in uris)
            {
                try
                {
                    var dictionary = new StringResourceDictionary
                    {
                        Source = new Uri(uri, UriKind.Absolute)
                    };
                    return dictionary;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Localization: could not load '{uri}': {ex.Message}");
                }
            }

            return null;
        }

        /// <summary>
        /// 标记用字典，切换语言时据此识别并移除旧的字符串字典。
        /// </summary>
        private sealed class StringResourceDictionary : ResourceDictionary
        {
        }
    }

    /// <summary>C# 代码中取界面文本的快捷入口。</summary>
    public static class Loc
    {
        /// <summary>取一条界面文本。</summary>
        public static string Get(string key) => LocalizationManager.Instance.Get(key);

        /// <summary>取一条带占位符的界面文本。</summary>
        public static string Format(string key, params object[] args) =>
            LocalizationManager.Format(key, args);
    }
}
