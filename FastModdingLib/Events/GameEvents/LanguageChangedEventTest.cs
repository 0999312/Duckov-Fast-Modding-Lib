using System;

namespace FastModdingLib.Events.GameEvents
{
    /// <summary>
    /// <see cref="LanguageChangedEvent"/> 的手动测试方法。
    /// 验证 handler 能正确接收事件并读取 <see cref="LanguageChangedEvent.LangCode"/>。
    /// </summary>
    public static class LanguageChangedEventTest
    {
        /// <summary>
        /// 运行测试：注册 handler → Post 事件 → 断言 handler 被调用且 LangCode 正确。
        /// 所有断言使用 if + throw 风格，无第三方测试依赖。
        /// </summary>
        public static void Run()
        {
            var bus = new EventBus();
            string? receivedCode = null;

            bus.Register<LanguageChangedEvent>(evt =>
            {
                receivedCode = evt.LangCode;
            });

            bus.Post(new LanguageChangedEvent("zh_cn"));

            if (receivedCode == null)
                throw new Exception("LanguageChangedEventTest FAILED: handler was not called.");
            if (receivedCode != "zh_cn")
                throw new Exception(
                    $"LanguageChangedEventTest FAILED: expected 'zh_cn' but got '{receivedCode}'.");

            Console.WriteLine("[PASS] LanguageChangedEventTest: handler received correct language code 'zh_cn'.");
        }
    }
}
