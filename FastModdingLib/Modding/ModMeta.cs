using Duckov.Modding;

namespace FastModdingLib.Modding
{
    /// <summary>
    /// fml.json 中声明的一条依赖项。可同时提供 <see cref="Name"/> 与 <see cref="WorkshopId"/>；
    /// 匹配时以 <see cref="Name"/> 优先，<see cref="WorkshopId"/> 兜底，
    /// 二者均不命中则视为该依赖未装载。
    /// </summary>
    /// <remarks>
    /// <para>JSON 形态兼容两种写法：</para>
    /// <list type="bullet">
    /// <item>字符串形式（仅按 Name 匹配）：<c>"HarmonyLoadMod"</c></item>
    /// <item>对象形式（Name 优先 + WorkshopId 兜底）：
    ///   <c>{ "name": "HarmonyLoadMod", "workshopId": "3589088839" }</c></item>
    /// </list>
    /// </remarks>
    public struct ModDependency
    {
        /// <summary>依赖目标的 mod 名称（与 <c>ModInfo.name</c> 比对）。匹配时优先使用，可为空。</summary>
        public string Name;

        /// <summary>依赖目标的 Steam 创意工坊 ID（字符串形式，与
        /// <c>ModInfo.publishedFileId</c> 比对）。Name 未命中时用作兜底，可为空。</summary>
        public string WorkshopId;

        /// <summary>name 与 workshopId 均为空，视为无效条目。</summary>
        public bool IsEmpty => string.IsNullOrEmpty(Name) && string.IsNullOrEmpty(WorkshopId);

        /// <summary>
        /// 判断此依赖是否匹配已加载的 <paramref name="info"/>。
        /// 规则：Name 非空且匹配 → 命中；否则 WorkshopId 非空且匹配 → 命中；否则未命中。
        /// </summary>
        public bool Matches(ModInfo info)
        {
            if (!string.IsNullOrEmpty(Name) && info.name == Name) return true;
            if (!string.IsNullOrEmpty(WorkshopId) && info.publishedFileId.ToString() == WorkshopId) return true;
            return false;
        }

        public override string ToString()
        {
            if (!string.IsNullOrEmpty(Name) && !string.IsNullOrEmpty(WorkshopId))
                return $"{Name} (workshop:{WorkshopId})";
            if (!string.IsNullOrEmpty(Name)) return Name;
            if (!string.IsNullOrEmpty(WorkshopId)) return $"workshop:{WorkshopId}";
            return "(empty)";
        }
    }

    /// <summary>
    /// fml.json 解析结果。每个 mod 根目录下的 fml.json 声明 modid、优先级、依赖关系与自激活策略。
    /// 仅 <c>modid</c> 为必填字段；其余可选。
    /// </summary>
    public struct ModMeta
    {
        /// <summary>模组唯一标识符。与 ModInfo.name 一致，用于依赖解析。</summary>
        public string ModId;

        /// <summary>加载优先级。数值越小越早激活。默认 int.MaxValue（最后）。</summary>
        public int Priority;

        /// <summary>依赖列表（硬依赖）。全部就绪后才允许激活。
        /// 每条依赖支持 Name 优先 + WorkshopId 兜底匹配（见 <see cref="ModDependency"/>）。</summary>
        public ModDependency[] Dependencies;

        /// <summary>建议在其后加载的 modid 列表（软依赖/联动）。
        /// 目标存在时强制排序；不存在时静默跳过，不影响激活。
        /// 仅按 Name 匹配（保持软依赖的轻量语义）。</summary>
        public string[] LoadAfter;

        public string[] LoadBefore;

        /// <summary>依赖就绪后是否自动激活（无需玩家手动开关）。默认 false。</summary>
        public bool AutoActivate;

        /// <summary>fml.json 是否成功加载并完成 modid 校验。false 表示文件不存在、缺 modid 或格式错误。</summary>
        public bool Loaded;

        public static ModMeta Default => new ModMeta
        {
            ModId = "",
            Priority = int.MaxValue,
            Dependencies = new ModDependency[]{},
            LoadAfter = new string[]{},
            LoadBefore = new string[]{},
            AutoActivate = false,
            Loaded = false
        };
    }
}
