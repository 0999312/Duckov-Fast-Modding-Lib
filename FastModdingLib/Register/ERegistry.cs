namespace FastModdingLib.Register
{
    /// <summary>
    /// 元注册表标记接口。所有可注册到 <see cref="RegistryManager.Registry"/> 的注册表
    /// 均实现此接口。提供无泛型的 <see cref="RemoveAllByOwner"/> 入口，便于
    /// <see cref="RegistryManager"/> 遍历元表批量卸载而不必关心具体泛型参数。
    /// </summary>
    public interface ERegistry
    {
        /// <summary>
        /// 按所属 mod 批量移除全部 entry，返回实际删除条数。
        /// 由各 <see cref="IRegistry{T}"/> 实现统一提供，<see cref="RegistryManager"/>
        /// 通过元表遍历调用此方法完成一次性卸载。
        /// </summary>
        int RemoveAllByOwner(string modid);
    }
}