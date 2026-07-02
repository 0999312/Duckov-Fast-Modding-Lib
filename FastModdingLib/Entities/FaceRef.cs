using System;

namespace FastModdingLib
{
    // ═══════════════════════════════════════════════════════════════
    //  FaceRef — 捏脸引用
    // ═══════════════════════════════════════════════════════════════

    /// <summary>捏脸引用模式。</summary>
    public enum FaceRefMode
    {
        /// <summary>不设置脸部（使用 CharacterModel 默认）。</summary>
        None,
        /// <summary>引用已有 CustomFacePreset 预设。</summary>
        Preset,
        /// <summary>使用玩家当前捏脸数据。</summary>
        PlayerFace,
        /// <summary>自定义面部部件组合。</summary>
        Custom
    }

    /// <summary>
    /// 捏脸引用。支持引用已有预设、玩家捏脸或自定义部件组合。
    /// modder 无需在 Unity 编辑器中创建 CustomFacePreset。
    /// </summary>
    /// <example>
    /// <code>
    /// // 引用已有预设
    /// var face = FaceRef.Preset("Boss_Red");
    ///
    /// // 使用玩家捏脸
    /// var face = FaceRef.PlayerFace();
    ///
    /// // 自定义部件
    /// var face = FaceRef.Custom(new FacePartIds { HairId = "Hair_Long_01" });
    /// </code>
    /// </example>
    public struct FaceRef
    {
        /// <summary>引用模式。</summary>
        public FaceRefMode Mode;

        /// <summary>预设名称（Mode=Preset 时使用）。</summary>
        public string? PresetName;

        /// <summary>自定义部件 ID（Mode=Custom 时使用）。</summary>
        public FacePartIds CustomParts;

        /// <summary>引用游戏已有预设。</summary>
        /// <param name="name">预设名称，如 "Boss_Red"、"Default"。</param>
        public static FaceRef Preset(string name)
            => new FaceRef { Mode = FaceRefMode.Preset, PresetName = name };

        /// <summary>使用玩家当前捏脸数据。</summary>
        public static FaceRef PlayerFace()
            => new FaceRef { Mode = FaceRefMode.PlayerFace };

        /// <summary>自定义部件组合。</summary>
        public static FaceRef Custom(FacePartIds parts)
            => new FaceRef { Mode = FaceRefMode.Custom, CustomParts = parts };

        /// <summary>不设置脸部。</summary>
        public static FaceRef None => new FaceRef { Mode = FaceRefMode.None };
    }

    // ═══════════════════════════════════════════════════════════════
    //  FacePartIds — 自定义面部部件 ID
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// 自定义面部部件 ID 组合。
    /// 对应游戏 CustomFacePartTypes 的 8 个分类。
    /// 未设置的部件使用默认值。
    /// </summary>
    public struct FacePartIds
    {
        /// <summary>头发部件 ID。</summary>
        public string? HairId;
        /// <summary>眼睛部件 ID。</summary>
        public string? EyeId;
        /// <summary>嘴巴部件 ID。</summary>
        public string? MouthId;
        /// <summary>眉毛部件 ID。</summary>
        public string? EyebrowId;
        /// <summary>装饰部件 ID。</summary>
        public string? DecorationId;
        /// <summary>尾巴部件 ID。</summary>
        public string? TailId;
        /// <summary>脚部部件 ID。</summary>
        public string? FootId;
        /// <summary>翅膀部件 ID。</summary>
        public string? WingId;
    }
}
