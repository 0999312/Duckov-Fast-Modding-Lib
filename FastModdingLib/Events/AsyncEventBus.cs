using System;
using System.Collections;
using System.Collections.Generic;

namespace FastModdingLib.Events
{
    /// <summary>
    /// 异步（协程）事件总线。handler 为 <see cref="Func{T, IEnumerator}"/>，
    /// 通过注入的 <see cref="EventBusRunner"/> 启动协程。
    /// 同 <see cref="EventBus"/> 维护 _byOwner 索引以支持批量卸载（PLAN §5.2）。
    /// </summary>
    public sealed class AsyncEventBus
    {
        private readonly SortedSet<AsyncTaskItemBase> _handlers = new SortedSet<AsyncTaskItemBase>();
        private readonly Dictionary<object, List<AsyncTaskItemBase>> _byOwner =
            new Dictionary<object, List<AsyncTaskItemBase>>();
        private readonly object _lock = new object();
        private EventBusRunner? _runner;

        /// <summary>
        /// 注入协程宿主。由 Bootstrap 在创建 runner 后调用（internal：EventBusRunner 不对外暴露）。
        /// </summary>
        internal void Init(EventBusRunner runner)
        {
            _runner = runner ?? throw new ArgumentNullException(nameof(runner));
        }

        public void Register<T>(Func<T, IEnumerator> handler) where T : Event
        {
            Register<T>(handler, 0, null);
        }

        public void Register<T>(Func<T, IEnumerator> handler, int priority, object? ownerMod) where T : Event
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            var item = new AsyncTaskItem<T>(handler, priority, ownerMod);
            lock (_lock)
            {
                _handlers.Add(item);
                if (ownerMod != null)
                {
                    if (!_byOwner.TryGetValue(ownerMod, out var list))
                    {
                        list = new List<AsyncTaskItemBase>();
                        _byOwner[ownerMod] = list;
                    }
                    list.Add(item);
                }
            }
        }

        public bool Unregister<T>(Func<T, IEnumerator> handler) where T : Event
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            lock (_lock)
            {
                AsyncTaskItem<T>? target = null;
                foreach (var item in _handlers)
                {
                    if (item is AsyncTaskItem<T> typed && ReferenceEquals(typed.Handler, handler))
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

        public void Clear()
        {
            lock (_lock)
            {
                _handlers.Clear();
                _byOwner.Clear();
            }
        }

        /// <summary>
        /// 广播事件。按 priority 降序依次启动 handler 协程；遇 Cancelled 停止后续。
        /// 完成后调用 onComplete（传入 evt.Cancelled）。
        /// 内部 ToArray() snapshot 后迭代（PLAN §13）。
        /// </summary>
        public IEnumerator Post(Event evt, Action<bool>? onComplete = null)
        {
            if (evt == null) throw new ArgumentNullException(nameof(evt));
            if (_runner == null)
            {
                throw new InvalidOperationException(
                    "AsyncEventBus has not been initialized with an EventBusRunner. " +
                    "Call Init(runner) before posting events.");
            }
            AsyncTaskItemBase[] snapshot;
            lock (_lock)
            {
                var arr = new AsyncTaskItemBase[_handlers.Count];
                _handlers.CopyTo(arr);
                Array.Reverse(arr);
                snapshot = arr;
            }
            foreach (var task in snapshot)
            {
                IEnumerator coroutine = task.DelegateAsync(evt);
                if (coroutine != null)
                {
                    _runner.Run(coroutine);
                }
                if (evt.Cancelled) break;
            }
            onComplete?.Invoke(evt.Cancelled);
            yield break;
        }

        private void RemoveFromOwner(AsyncTaskItemBase item)
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