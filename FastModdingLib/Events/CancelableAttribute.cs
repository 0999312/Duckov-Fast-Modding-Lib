using System;

namespace FastModdingLib.Events
{
    /// <summary>
    /// 标记一个 <see cref="Event"/> 子类为可取消事件。
    /// 仅标记了此特性的事件才能在 handler 中调用 <see cref="Event.SetCancelled"/>。
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class CancelableAttribute : Attribute
    {
    }
}