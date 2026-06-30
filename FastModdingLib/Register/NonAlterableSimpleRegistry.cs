using FastModdingLib.Utils;
using System;

namespace FastModdingLib.Register
{
    /// <summary>
    /// "加完了不能改"语义的 <see cref="SimpleRegistry{T}"/>。
    /// <see cref="Set(Identifier, T)"/> / <see cref="Set(Identifier, T, string)"/> 在重复 key 时抛
    /// <see cref="ArgumentException"/>；<see cref="SetIfAbsent"/> 提供 TryAdd 静默语义。
    /// 删除路径（<see cref="SimpleRegistry{T}.Remove"/> / <see cref="SimpleRegistry{T}.RemoveAllByOwner"/>
    /// / <see cref="SimpleRegistry{T}.Clear"/>）继承自基类，不受"不可改"约束。
    /// </summary>
    public class NonAlterableSimpleRegistry<T> : SimpleRegistry<T>
    {
        public override T this[Identifier id]
        {
            get => dict[id];
            set => SetIfAbsent(id, value, RegistryManager.CurrentModid);
        }

        public override void Set(Identifier id, T value)
        {
            Set(id, value, RegistryManager.CurrentModid);
        }

        public override void Set(Identifier id, T value, string modid)
        {
            if (!dict.TryAdd(id, value))
            {
                throw new ArgumentException("Key already exists!");
            }
            _owners[id] = modid;
        }

        /// <summary>
        /// TryAdd 语义：成功写入返回 true；key 已存在则静默返回 false，不抛异常、不覆盖。
        /// </summary>
        public bool SetIfAbsent(Identifier id, T value, string modid)
        {
            if (!dict.TryAdd(id, value))
            {
                return false;
            }
            _owners[id] = modid;
            return true;
        }
    }
}