using FastModdingLib.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FastModdingLib.Register
{
    /// <summary>
    /// <see cref="IRegistry{T}"/> 的薄包装实现。维护 entry→owner 映射，
    /// 在 <see cref="Remove(Identifier)"/> / <see cref="RemoveAllByOwner"/> 删除后
    /// 调用 <see cref="OnRemoved"/> 供子类做 native 侧善后。
    /// </summary>
    public class SimpleRegistry<T> : IRegistry<T>
    {
        protected Dictionary<Identifier, T> dict;
        protected Dictionary<Identifier, string> _owners;

        public SimpleRegistry()
        {
            dict = new Dictionary<Identifier, T>();
            _owners = new Dictionary<Identifier, string>();
        }

        /// <summary>
        /// 删除一条 entry 时触发的 native 善后回调。默认空实现；
        /// 子类 override 以清理 native 侧资源（如 <c>Destroy(gameObject)</c>）。
        /// </summary>
        /// <param name="modid">被删 entry 的 owner modid；若 entry 无 owner 记录则为 null。</param>
        protected virtual void OnRemoved(Identifier id, T value, string? modid)
        {
        }

        public virtual T this[Identifier id]
        {
            get => dict[id];
            set => Set(id, value, RegistryManager.CurrentModid);
        }

        public virtual bool TryGet(Identifier id, out T value)
        {
            bool ret = dict.TryGetValue(id, out value);
            return ret;
        }

        public virtual T Get(Identifier id)
        {
            T var;
            bool ret = dict.TryGetValue(id, out var);
            if (!ret) throw new IndexOutOfRangeException("Key not exist.");
            return var;
        }

        public virtual void Set(Identifier id, T value)
        {
            Set(id, value, RegistryManager.CurrentModid);
        }

        public virtual void Set(Identifier id, T value, string modid)
        {
            dict[id] = value;
            _owners[id] = modid;
        }

        public virtual bool Remove(Identifier id)
        {
            if (!dict.TryGetValue(id, out var value))
            {
                return false;
            }
            dict.Remove(id);
            _owners.TryGetValue(id, out var modid);
            _owners.Remove(id);
            try
            {
                OnRemoved(id, value, modid);
            }
            catch (Exception e)
            {
                Debug.LogError($"[SimpleRegistry.OnRemoved] id={id.Domain}:{id.Path} modid={modid} threw: {e}");
            }
            return true;
        }

        public virtual bool Remove(Identifier id, out string? modidRemoved)
        {
            if (!TryGetOwner(id, out modidRemoved))
            {
                modidRemoved = null;
                return false;
            }
            return Remove(id);
        }

        public virtual bool TryGetOwner(Identifier id, out string? modid)
        {
            return _owners.TryGetValue(id, out modid);
        }

        public virtual IReadOnlyList<Identifier> GetAllByOwner(string modid)
        {
            return _owners.Where(kvp => kvp.Value == modid).Select(kvp => kvp.Key).ToList();
        }

        public virtual int RemoveAllByOwner(string modid)
        {
            var keys = _owners.Where(kvp => kvp.Value == modid).Select(kvp => kvp.Key).ToList();
            int removed = 0;
            foreach (var key in keys)
            {
                try
                {
                    if (Remove(key)) removed++;
                }
                catch (Exception e)
                {
                    Debug.LogError($"[SimpleRegistry.RemoveAllByOwner] id={key.Domain}:{key.Path} modid={modid} threw: {e}");
                }
            }
            return removed;
        }

        public virtual void Clear()
        {
            dict.Clear();
            _owners.Clear();
        }

        public IEnumerator<KeyValuePair<Identifier, T>> GetEnumerator()
        {
            // 返回快照枚举器，避免迭代中变异 dict 引发 InvalidOperationException。
            foreach (var kvp in dict.ToArray())
            {
                yield return kvp;
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}