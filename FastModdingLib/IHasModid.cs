namespace FastModdingLib
{
    /// <summary>
    /// 统一 mod 标识符接口。
    /// 依赖 FML 的模组在其主类（继承 <c>Duckov.Modding.ModBehaviour</c>）上实现此接口，
    /// 使 FML 工具方法能通过 <see cref="GetModid"/> 获取调用方 mod 身份。
    /// </summary>
    /// <example>
    /// <code>
    /// public class MyMod : Duckov.Modding.ModBehaviour, IHasModid
    /// {
    ///     public string GetModid() => "MyMod";
    ///     // ...
    /// }
    /// </code>
    /// </example>
    public interface IHasModid
    {
        /// <summary>
        /// 返回本模组的唯一标识符。
        /// </summary>
        string GetModid();
    }
}
