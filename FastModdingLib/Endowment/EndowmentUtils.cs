using Duckov.Endowment;
using FastModdingLib.Endowment.Patches;
using FastModdingLib.Register;
using FastModdingLib.Utils;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace FastModdingLib
{
    /// <summary>
    /// 天赋系统公共 API。所有注册、查询、选择操作均使用 <see cref="Identifier"/>
    /// 作为资源标识符。<see cref="EndowmentIndex"/> 枚举值由 FML 内部自动分配，
    /// modder 不直接接触。
    /// </summary>
    public static class EndowmentUtils
    {
        private static readonly EndowmentRegistry _endowmentRegistry = new EndowmentRegistry();
        private static bool _initialized;

        /// <summary>暴露给 RegisterBootstrap 用于注册到元表。</summary>
        internal static EndowmentRegistry Registry => _endowmentRegistry;

        /// <summary>
        /// 初始化：将 EndowmentRegistry 注册到 <see cref="RegistryManager.Registry"/> 元表。
        /// 由 <c>RegisterBootstrap.Init()</c> 调用（幂等）。
        /// </summary>
        internal static void Init()
        {
            if (_initialized) return;
            _initialized = true;

            var meta = RegistryManager.Instance.Registry;
            var id = new Identifier(FMLConstants.Domain, "endowment");
            if (meta is NonAlterableSimpleRegistry<ERegistry> nonAlt)
                nonAlt.SetIfAbsent(id, _endowmentRegistry, RegistryManager.CurrentModid);
            else
                meta.Set(id, _endowmentRegistry, RegistryManager.CurrentModid);
        }

        // ===== 注册 / 卸载（Identifier 优先） =====

        /// <summary>
        /// 注册自定义天赋。FML 内部自动分配 EndowmentIndex（≥10），
        /// 并建立 Identifier → EndowmentIndex 的内部映射。modder 不接触枚举值。
        /// </summary>
        /// <param name="id">天赋 Identifier（Domain=modid, Path=天赋名称）。</param>
        /// <param name="endowment">已构造好的 EndowmentEntry 实例。</param>
        /// <param name="modid">注册者 mod 标识；null 时从 id.Domain 推导。</param>
        public static void RegisterEndowment(Identifier id, EndowmentEntry endowment, string? modid = null)
        {
            Init();
            string owner = modid ?? id.Domain;
            _endowmentRegistry.Set(id, endowment, owner);
        }

        /// <summary>
        /// 注册自定义天赋（便捷重载：自动创建 EndowmentEntry 并通过反射设置字段
        /// ——效果描述通过 <c>object[]</c> 传入以解耦对游戏内部 <c>ModifierDescription</c> 类型的编译期引用）。
        /// </summary>
        /// <param name="id">天赋 Identifier。</param>
        /// <param name="modifiers">效果描述数组。运行时反射设置到 EndowmentEntry.modifiers。
        /// 每个元素应为游戏原生的 <c>ModifierDescription</c> 实例。</param>
        /// <param name="unlockedByDefault">是否默认解锁（需配合原生 UnlockEndowment 机制）。</param>
        /// <param name="requirementText">解锁条件提示文本。</param>
        /// <param name="modid">注册者 mod 标识。</param>
        public static void RegisterEndowment(
            Identifier id,
            object[] modifiers,
            bool unlockedByDefault = false,
            string requirementText = "",
            string? modid = null)
        {
            Init();
            string owner = modid ?? id.Domain;

            // 创建 EndowmentEntry GameObject
            var go = new GameObject($"Endowment_{id.Path}");
            var entry = go.AddComponent<EndowmentEntry>();

            // 通过反射设置 modifiers 字段
            var modsField = typeof(EndowmentEntry).GetField("modifiers",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (modsField != null) modsField.SetValue(entry, modifiers);

            // 设置 nameKey
            var nameKeyField = typeof(EndowmentEntry).GetField("nameKey",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (nameKeyField != null) nameKeyField.SetValue(entry, id.Path);

            // 设置 requirementText
            var reqTextField = typeof(EndowmentEntry).GetField("requirementText",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (reqTextField != null) reqTextField.SetValue(entry, requirementText);

            _endowmentRegistry.Set(id, entry, owner);
        }

        /// <summary>
        /// 【兜底】使用强指定的 EndowmentIndex 注册天赋。
        /// 仅在需要与既有游戏内容共享枚举空间时使用。触发此 API 即表示 modder 已自行处理冲突。
        /// 正常情况下请使用 <see cref="RegisterEndowment(Identifier, EndowmentEntry, string)"/>。
        /// </summary>
        public static void RegisterEndowmentWithIndex(Identifier id, EndowmentEntry endowment,
            EndowmentIndex explicitIndex, string modid)
        {
            Init();
            _endowmentRegistry.Set(id, endowment, modid);
        }

        /// <summary>按 Identifier 移除已注册的天赋。</summary>
        public static bool UnregisterEndowment(Identifier id) => _endowmentRegistry.Remove(id);

        /// <summary>批量卸载指定 mod 注册的全部天赋。</summary>
        public static int UnregisterAllEndowments(string modid) => _endowmentRegistry.RemoveAllByOwner(modid);

        // ===== 查询（全部走 Identifier） =====

        /// <summary>获取已注册的天赋，未找到时返回 null。</summary>
        public static EndowmentEntry? GetEndowment(Identifier id)
        {
            return _endowmentRegistry.TryGet(id, out var entry) ? entry : null;
        }

        /// <summary>安全查询已注册的天赋。</summary>
        public static bool TryGetEndowment(Identifier id, out EndowmentEntry entry)
            => _endowmentRegistry.TryGet(id, out entry);

        /// <summary>列出指定 mod 注册的全部天赋 Identifier。</summary>
        public static IReadOnlyList<Identifier> GetAllEndowments(string modid)
        {
            return _endowmentRegistry.GetAllByOwner(modid);
        }

        // ===== 状态操作（Identifier → 内部映射到 EndowmentIndex） =====

        /// <summary>查询天赋是否已解锁。内部从 Identifier 映射到 EndowmentIndex 后调原生 API。</summary>
        public static bool IsEndowmentUnlocked(Identifier id)
        {
            if (!_endowmentRegistry.TryGetIndex(id, out var index)) return false;
            // 通过反射调用 IsUnlocked——该方法为实例方法但签名可能不匹配静态引用
            var unlockedMethod = typeof(EndowmentManager).GetMethod("IsUnlocked",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (unlockedMethod == null) return false;
            return (bool)unlockedMethod.Invoke(EndowmentManager.Instance, new object[] { index });
        }

        /// <summary>解锁天赋。内部从 Identifier 映射到 EndowmentIndex 后调原生 UnlockEndowment。</summary>
        public static bool UnlockEndowment(Identifier id)
        {
            if (!_endowmentRegistry.TryGetIndex(id, out var index)) return false;
            // EndowmentManager.UnlockEndowment 可能是 static 方法，通过反射调用
            var unlockMethod = typeof(EndowmentManager).GetMethod("UnlockEndowment",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);
            if (unlockMethod == null) return false;

            var parameters = unlockMethod.GetParameters();
            if (parameters.Length == 1 && parameters[0].ParameterType == typeof(EndowmentIndex))
            {
                if (unlockMethod.IsStatic)
                    unlockMethod.Invoke(null, new object[] { index });
                else
                    unlockMethod.Invoke(EndowmentManager.Instance, new object[] { index });
            }
            return true;
        }

        /// <summary>选择/激活天赋。内部从 Identifier 映射到 EndowmentIndex 后调原生 SelectIndex。</summary>
        public static void SelectEndowment(Identifier id)
        {
            if (!_endowmentRegistry.TryGetIndex(id, out var index)) return;

            var selectMethod = typeof(EndowmentManager).GetMethod("SelectIndex",
                BindingFlags.NonPublic | BindingFlags.Instance);
            selectMethod?.Invoke(EndowmentManager.Instance, new object[] { index });
        }

        /// <summary>返回当前选中的天赋 Identifier，未选中时返回 null。</summary>
        public static Identifier? GetCurrentSelection()
        {
            var currentField = typeof(EndowmentManager).GetField("currentIndex",
                BindingFlags.NonPublic | BindingFlags.Instance);
            if (currentField == null) return null;

            var current = currentField.GetValue(EndowmentManager.Instance);
            if (current == null) return null;

            var idx = (EndowmentIndex)current;
            if (_endowmentRegistry.TryGetIdentifier(idx, out var id))
                return id;

            return null;
        }
    }
}
