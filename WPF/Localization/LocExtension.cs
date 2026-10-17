using System;
using System.Windows.Markup;

namespace ZenTimings.Localization
{
    /// <summary>
    /// XAML 标记扩展，用于 Binding 的 FallbackValue / TargetNullValue 这类
    /// 无法使用 DynamicResource 的位置。用法：FallbackValue={loc:Loc Loc_Main_CpuNa}
    /// </summary>
    [MarkupExtensionReturnType(typeof(string))]
    public sealed class LocExtension : MarkupExtension
    {
        public LocExtension() { }

        public LocExtension(string key)
        {
            Key = key;
        }

        /// <summary>资源 key，如 Loc_Main_CpuNa。</summary>
        [ConstructorArgument("key")]
        public string Key { get; set; }

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            if (string.IsNullOrEmpty(Key))
                return string.Empty;

            return LocalizationManager.Instance.Get(Key);
        }
    }
}
