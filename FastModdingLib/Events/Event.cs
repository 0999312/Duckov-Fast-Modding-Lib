using System;

namespace FastModdingLib.Events
{
    /// <summary>
    /// 所有 FML 事件的基类。handler 间通过 priority 协作：高优先级先执行，
    /// 可通过 <see cref="SetCancelled"/> 叫停后续 handler。
    /// </summary>
    public abstract class Event
    {
        /// <summary>
        /// 是否已被取消。一旦置 true 不可逆（单向 OR 语义）。
        /// </summary>
        public bool Cancelled { get; private set; }

        /// <summary>
        /// 是否为可取消事件。由 <see cref="CancelableAttribute"/> 在类上标记决定。
        /// </summary>
        public bool IsCancelable()
        {
            return GetType().IsDefined(typeof(CancelableAttribute), false);
        }

        /// <summary>
        /// 设置取消状态。对未标记 <see cref="CancelableAttribute"/> 的事件抛
        /// <see cref="NotSupportedException"/>。
        /// 语义：<c>Cancelled = Cancelled || cancelled</c>（单向 OR，置 true 后不可逆）。
        /// </summary>
        /// <param name="cancelled">是否取消。</param>
        /// <exception cref="NotSupportedException">事件不可取消时抛出。</exception>
        public void SetCancelled(bool cancelled)
        {
            if (!IsCancelable())
            {
                throw new NotSupportedException(
                    $"Event type {GetType().FullName} is not marked with [Cancelable] and cannot be cancelled.");
            }
            Cancelled = Cancelled || cancelled;
        }
    }
}