using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;

namespace FastModdingLib.Events
{
    /// <summary>
    /// 异步（UniTask）事件总线。handler 为 <see cref="Func{T, UniTask}"/>，
    /// 通过 UniTask PlayerLoop 调度，无需 MonoBehaviour 协程宿主。
    /// 同 <see cref="EventBus"/> 维护 _byOwner 索引以支持批量卸载。
    /// </summary>
    public sealed class AsyncEventBus
    {
        private readonly SortedSet<AsyncTaskItemBase> _handlers = new SortedSet<AsyncTaskItemBase>();
        private readonly Dictionary<object, List<AsyncTaskItemBase>> _byOwner =
            new Dictionary<object, List<AsyncTaskItemBase>>();
        private readonly object _lock = new object();

        public void Register<T>(Func<T, UniTask> handler) where T : Event
        {
            Register<T>(handler, 0, null);
        }

        public void Register<T>(Func<T, UniTask> handler, int priority, object? ownerMod) where T : Event
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

        public bool Unregister<T>(Func<T, UniTask> handler) where T : Event
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
        /// 广播事件。按 priority 降序依次 await handler；遇 Cancelled 停止后续。
        /// 内部 ToArray() snapshot 后迭代，避免迭代中 Unregister 引发并发修改。
        /// </summary>
        public async UniTask Post(Event evt)
        {
            if (evt == null) throw new ArgumentNullException(nameof(evt));

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
                try
                {
                    await task.DelegateAsync(evt);
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogError($"[AsyncEventBus] Handler threw for {evt.GetType().Name}: {ex}");
                }
                if (evt.Cancelled) break;
            }
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
