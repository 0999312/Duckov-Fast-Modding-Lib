using FastModdingLib.Utils;
using System.Collections.Generic;

namespace FastModdingLib.Register
{
    /// <summary>
    /// 统一注册表抽象。扩能后支持删除 / 清空 / 遍历 / owner 索引 / 批量卸载。
    /// 既有 4 方法签名保持不变，新增方法均带可空标注。
    /// </summary>
    public interface IRegistry<T> : ERegistry, IEnumerable<KeyValuePair<Identifier, T>>
    {
        // —— 既有签名（不变） ——
        /// <summary>读写索引器。set 等价于 <see cref="Set(Identifier, T)"/>（默认 owner = <see cref="RegistryManager.CurrentModid"/>）。</summary>
        T this[Identifier id] { get; set; }

        /// <summary>尝试获取；不存在返回 false。</summary>
        bool TryGet(Identifier id, out T value);

        /// <summary>获取；不存在抛 <see cref="System.IndexOutOfRangeException"/>。</summary>
        T Get(Identifier id);

        /// <summary>写入；默认 owner = <see cref="RegistryManager.CurrentModid"/>。</summary>
        void Set(Identifier id, T value);

        // —— 新增：删除 / 清空 ——
        /// <summary>移除指定 entry，返回是否实际移除。</summary>
        bool Remove(Identifier id);

        /// <summary>清空全部 entry（包含所有 owner）；不触发 <c>OnRemoved</c> 回调。</summary>
        void Clear();

        // —— 新增：显式带 owner 的写入 ——
        /// <summary>写入并显式指定 owner modid。</summary>
        void Set(Identifier id, T value, string modid);

        /// <summary>移除并回传被删 entry 的 owner modid。</summary>
        bool Remove(Identifier id, out string? modidRemoved);

        // —— 新增：按 owner 索引 / 批量卸载 ——
        /// <summary>查询指定 entry 的 owner modid。</summary>
        bool TryGetOwner(Identifier id, out string? modid);

        /// <summary>返回指定 owner 注册的全部 entry 标识符（快照）。</summary>
        IReadOnlyList<Identifier> GetAllByOwner(string modid);

        /// <summary>按 owner 批量移除，返回实际删除条数；native 侧善后走 <c>OnRemoved</c> 回调。</summary>
        new int RemoveAllByOwner(string modid);

        // —— 新增：枚举 ——
        /// <summary>返回 entry 快照枚举器（迭代中变异安全）。</summary>
        new IEnumerator<KeyValuePair<Identifier, T>> GetEnumerator();
    }
}