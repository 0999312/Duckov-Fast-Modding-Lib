# 捏脸系统 FML 集成设计

> 基于 `DecompiledDLL/Core/CustomFacePreset.cs` 等源码分析
> 最后更新：2026-07-01

---

## 1. 游戏侧架构

### 1.1 核心类

| 类 | 类型 | 说明 |
|----|------|------|
| `CustomFacePreset` | ScriptableObject | 捏脸预设，持有 `CustomFaceSettingData settings` |
| `CustomFaceData` | ScriptableObject | 面部部件集合 + 默认预设 |
| `CustomFaceSettingData` | Serializable 结构体 | 实际捏脸参数数据（头发、眼睛、嘴巴等） |
| `CustomFaceManager` | MonoBehaviour | 保存/加载捏脸设置 |
| `CustomFacePartCollection` | — | 面部部件集合（头发集合、眼睛集合等） |
| `CustomFacePart` | — | 单个面部部件 |
| `CharacterModel` | MonoBehaviour | 角色模型，提供 `SetFaceFromPreset()` / `SetFaceFromData()` |

### 1.2 面部部件分类

`CustomFaceData` 包含 8 类部件：
- `hairs` — 头发
- `eyes` — 眼睛
- `mouths` — 嘴巴
- `eyebrows` — 眉毛
- `decorations` — 装饰
- `tails` — 尾巴
- `foots` — 脚部
- `wings` — 翅膀

### 1.3 捏脸应用流程

```
CharacterRandomPreset.CreateCharacterAsync()
  ↓
characterModel.SetFaceFromPreset(facePreset)    // 从预设应用
  或
characterModel.SetFaceFromData(faceData)        // 从数据应用
```

```
CharacterRandomPreset 字段:
  facePreset: CustomFacePreset          — NPC 使用此字段
  usePlayerPreset: bool                 — true 时用玩家自己的捏脸数据
```

### 1.4 解包数据中的预设

在 `ExportedProject/Assets/MonoBehaviour/` 中有约 55 个 `CustomFacePreset_*.asset` 文件，包括：
- `CustomFacePreset_Default.asset` — 默认
- `CustomFacePreset_Boss_Red.asset` — Boss 红
- `CustomFacePreset_XiaoMing.asset` — 小明
- `CustomFacePreset_Solider.asset` — 士兵
- `CustomFacePreset_Usec.asset` — USEC
- 等等...

---

## 2. FML 集成方案

### 2.1 设计原则

modder **不需要**在 Unity 编辑器中手动创建 `CustomFacePreset`。FML 提供以下能力：

| 场景 | FML 方案 |
|------|---------|
| 引用已有脸部预设 | `FaceRef.Preset("Boss_Red")` — 按名称引用游戏已有预设 |
| 引用玩家捏脸 | `FaceRef.PlayerFace()` — 使用玩家当前捏脸数据 |
| 自定义捏脸参数 | `FaceRef.Custom(hairId, eyeId, mouthId, ...)` — 通过部件 ID 组合 |

### 2.2 `FaceRef` 设计

```csharp
namespace FastModdingLib
{
    /// <summary>捏脸引用。支持引用已有预设、玩家捏脸或自定义部件组合。</summary>
    public struct FaceRef
    {
        /// <summary>模式。</summary>
        public FaceRefMode Mode;

        /// <summary>预设名称（Mode=Preset 时使用）。</summary>
        public string? PresetName;

        /// <summary>自定义部件 ID 列表（Mode=Custom 时使用）。</summary>
        public FacePartIds? CustomParts;

        /// <summary>引用游戏已有预设。</summary>
        public static FaceRef Preset(string name)
            => new FaceRef { Mode = FaceRefMode.Preset, PresetName = name };

        /// <summary>使用玩家当前捏脸。</summary>
        public static FaceRef PlayerFace()
            => new FaceRef { Mode = FaceRefMode.PlayerFace };

        /// <summary>自定义部件组合。</summary>
        public static FaceRef Custom(FacePartIds parts)
            => new FaceRef { Mode = FaceRefMode.Custom, CustomParts = parts };

        /// <summary>不设置脸部（使用 CharacterModel 默认）。</summary>
        public static FaceRef None => new FaceRef { Mode = FaceRefMode.None };
    }

    public enum FaceRefMode
    {
        None,          // 不设置
        Preset,        // 引用已有 CustomFacePreset
        PlayerFace,    // 使用玩家捏脸
        Custom         // 自定义部件组合
    }

    /// <summary>自定义面部部件 ID。</summary>
    public struct FacePartIds
    {
        public string? HairId;
        public string? EyeId;
        public string? MouthId;
        public string? EyebrowId;
        public string? DecorationId;
        public string? TailId;
        public string? FootId;
        public string? WingId;
    }
}
```

### 2.3 EnemyPresetData 集成

```csharp
public class EnemyPresetData
{
    // ... 现有字段 ...

    /// <summary>捏脸配置。</summary>
    public FaceRef Face { get; set; } = FaceRef.None;

    /// <summary>是否使用玩家捏脸（快捷方式，等价于 Face = FaceRef.PlayerFace()）。</summary>
    public bool UsePlayerFace { get; set; }

    internal CharacterRandomPreset ToNative()
    {
        var preset = ScriptableObject.CreateInstance<CharacterRandomPreset>();

        // 捏脸处理
        if (UsePlayerFace)
        {
            SetField(preset, "usePlayerPreset", true);
        }
        else if (Face.Mode == FaceRefMode.Preset && Face.PresetName != null)
        {
            // 通过 Resources 或 GameplayDataSettings 查找已有预设
            var facePreset = FindPresetByName(Face.PresetName);
            SetField(preset, "facePreset", facePreset);
        }
        else if (Face.Mode == FaceRefMode.Custom && Face.CustomParts.HasValue)
        {
            // 创建新 CustomFacePreset + CustomFaceSettingData
            var facePreset = CreateCustomFacePreset(Face.CustomParts.Value);
            SetField(preset, "facePreset", facePreset);
        }

        // ... 其余字段 ...
    }
}
```

### 2.4 预设查找策略

```csharp
/// <summary>按名称查找已有 CustomFacePreset。</summary>
internal static CustomFacePreset? FindPresetByName(string name)
{
    // 策略1：从 GameplayDataSettings.CustomFaceData 的预设列表中查找
    // 策略2：从 Resources.Load 查找
    // 策略3：遍历所有已加载的 CustomFacePreset ScriptableObject
    return null; // 具体实现待定
}

/// <summary>根据部件 ID 创建自定义捏脸预设。</summary>
internal static CustomFacePreset CreateCustomFacePreset(FacePartIds parts)
{
    var preset = ScriptableObject.CreateInstance<CustomFacePreset>();
    var settings = new CustomFaceSettingData();

    // 通过 CustomFacePartCollection 查找并设置各部件
    // 具体实现需要了解 CustomFaceSettingData 的内部结构
    // 如果无法直接设置，通过 CustomFaceManager.SaveSetting 保存后再加载

    preset.settings = settings;
    return preset;
}
```

---

## 3. 使用示例

### 3.1 引用已有预设

```csharp
var npc = new EnemyPresetData
{
    NameKey = "NPC_Merchant",
    NpcRole = NpcRole.Merchant,
    Face = FaceRef.Preset("Boss_Red")  // 使用 Boss_Red 的脸
};
```

### 3.2 使用玩家捏脸（创建"克隆"NPC）

```csharp
var clone = new EnemyPresetData
{
    NameKey = "NPC_Clone",
    UsePlayerFace = true  // 使用玩家当前捏脸数据
};
```

### 3.3 自定义组合

```csharp
var custom = new EnemyPresetData
{
    NameKey = "NPC_Custom",
    Face = FaceRef.Custom(new FacePartIds
    {
        HairId = "Hair_Long_01",
        EyeId = "Eye_Blue_02",
        MouthId = "Mouth_Smile_01"
    })
};
```

---

## 4. 待研究项

| 项目 | 说明 | 优先级 | 状态 |
|------|------|--------|------|
| `CustomFaceSettingData` 内部结构 | 需要完整了解该结构体的字段以支持自定义捏脸 | P0 | ⏳ Phase 5 |
| 面部部件 ID 命名规则 | 需要从 `CustomFacePartCollection` 了解各部件的 ID 格式 | P1 | ⏳ Phase 5 |
| 运行时预设创建 | `ScriptableObject.CreateInstance<CustomFacePreset>()` 后如何正确填充数据 | P0 | ⚠️ `FaceRef`/`FacePartIds` 类型已实现，运行时查找待 Phase 5 |
| 脸部材质/纹理 | 自定义脸部可能需要额外的纹理资源 | P2 | ⏳ Phase 5 |

---

*本设计的核心类型（`FaceRef`、`FacePartIds`、`FaceRefMode` 枚举、`NpcRole` 枚举）已在 `FastModdingLib/Entities/` 中实现，并已集成到 `EnemyPresetData` DTO。完整的捏脸预设创建和运行时面部定制留待 Phase 5。*
