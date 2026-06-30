using System;
using System.Collections.Generic;

namespace FastModdingLib.Events
{
    /// <summary>
    /// 同步事件总线。handler 按 Priority 降序执行（数值大者先），
    /// 遇 <see cref="Event.Cancelled"/> 即停止后续 handler。
    /// 修复参考设计两处缺陷（PLAN §5）：
    ///  - §5.1 同优先级 handler 不相互覆盖：TaskItemBase 以 RegistrationId 作二级键。
    ///  - §5.2 缺少 Unregister：维护 _byOwner 索引，支持按 ownerMod 批量卸载。
    /// </summary>
    public sealed class EventBus
    {
        // 按 CompareTo（Priority 升序 + RegistrationId 升序）排序；Post 时倒序遍历以实现"数值大者先"。
        private readonly SortedSet<TaskItemBase> _handlers = new SortedSet<TaskItemBase>();
        private readonly Dictionary<object, List<TaskItemBase>> _byOwner =
            new Dictionary<object, List<TaskItemBase>>();
        private readonly object _lock = new object();

        /// <summary>
        /// 以默认优先级 0 注册 handler。
        /// </summary>
        public void Register<T>(Action<T> handler) where T : Event
        {
            Register<T>(handler, 0, null);
        }

        /// <summary>
        /// 以指定优先级注册 handler，ownerMod 为 null。
        /// </summary>
        public void Register<T>(Action<T> handler, int priority) where T : Event
        {
            Register<T>(handler, priority, null);
        }

        /// <summary>
        /// 注册 handler。priority 数值越大越先执行；ownerMod 用于 <see cref="UnregisterAll"/>。
        /// </summary>
        public void Register<T>(Action<T> handler, int priority, object? ownerMod) where T : Event
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            var item = new TaskItem<T>(handler, priority, ownerMod);
            lock (_lock)
            {
                _handlers.Add(item);
                if (ownerMod != null)
                {
                    if (!_byOwner.TryGetValue(ownerMod, out var list))
                    {
                        list = new List<TaskItemBase>();
                        _byOwner[ownerMod] = list;
                    }
                    list.Add(item);
                }
            }
        }

        /// <summary>
        /// 按 delegate 引用精确移除单个 handler。返回是否成功移除。
        /// </summary>
        public bool Unregister<T>(Action<T> handler) where T : Event
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            lock (_lock)
            {
                TaskItem<T>? target = null;
                foreach (var item in _handlers)
                {
                    if (item is TaskItem<T> typed && ReferenceEquals(typed.Handler, handler))
                    {
                        target = typed;
                        break;
                    }
                }
                if (target == null) return false;
                _handlers.Remove(target);
                RemoveFromOwner(target);
                return true;
            }
        }

        /// <summary>
        /// 按 ownerMod 批量移除所有关联 handler。返回移除条数。
        /// </summary>
        public int UnregisterAll(object ownerMod)
        {
            if (ownerMod == null) throw new ArgumentNullException(nameof(ownerMod));
            lock (_lock)
            {
                if (!_byOwner.TryGetValue(ownerMod, out var list)) return 0;
                int count = list.Count;
                foreach (var item in list)
                {
                    _handlers.Remove(item);
                }
                list.Clear();
                _byOwner.Remove(ownerMod);
                return count;
            }
        }

        /// <summary>
        /// 清空所有 handler（含 owner 索引）。供 hot-reload / 测试使用。
        /// </summary>
        public void Clear()
        {
            lock (_lock)
            {
                _handlers.Clear();
                _byOwner.Clear();
            }
        }

        /// <summary>
        /// 广播事件。按 priority 降序（数值大者先）依次调用 handler；
        /// 若事件可取消且被 SetCancelled，则停止后续 handler。
        /// 返回 <see cref="Event.Cancelled"/>。
        /// 内部 ToArray() snapshot 后迭代，避免迭代中 Unregister 引发 InvalidOperationException（PLAN §13）。
        /// </summary>
        public bool Post(Event evt)
        {
            if (evt == null) throw new ArgumentNullException(nameof(evt));
            TaskItemBase[] snapshot;
            lock (_lock)
            {
                // 倒序遍历：SortedSet 升序，末尾 priority 最大者先执行。
                var arr = new TaskItemBase[_handlers.Count];
                _handlers.CopyTo(arr);
                // 反转使 priority 大者在前；同 priority 内 RegistrationId 小者在前（注册早者先）。
                Array.Reverse(arr);
                snapshot = arr;
            }
            foreach (var task in snapshot)
            {
                task.Delegate(evt);
                if (evt.Cancelled) break;
            }
            return evt.Cancelled;
        }

        private void RemoveFromOwner(TaskItemBase item)
        {
            if (item.OwnerMod == null) return;
            if (_byOwner.TryGetValue(item.OwnerMod, out var list))
            {
                list.Remove(item);
                if (list.Count == 0) _byOwner.Remove(item.OwnerMod);
            }
        }
    }
}