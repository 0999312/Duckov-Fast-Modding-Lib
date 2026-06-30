using System;

namespace FastModdingLib.Events.GameEvents
{
    /// <summary>
    /// 游戏语言变更事件。当玩家切换游戏语言时触发。
    /// 仅观察用途，不支持取消。
    /// </summary>
    public sealed class LanguageChangedEvent : Event
    {
        /// <summary>
        /// 新的语言代码，例如 "zh_cn"、"en_us"、"ja_jp" 等。
        /// </summary>
        public string LangCode { get; }

        /// <summary>
        /// 初始化 <see cref="LanguageChangedEvent"/> 实例。
        /// </summary>
        /// <param name="langCode">游戏语言代码。</param>
        /// <exception cref="ArgumentNullException"><paramref name="langCode"/> 为 null 时抛出。</exception>
        public LanguageChangedEvent(string langCode)
        {
            if (langCode == null)
                throw new ArgumentNullException(nameof(langCode));
            LangCode = langCode;
        }
    }
}
