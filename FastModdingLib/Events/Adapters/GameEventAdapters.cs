using FastModdingLib.Events.GameEvents;
using SodaCraft.Localizations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;

namespace FastModdingLib.Events.Adapters
{
    /// <summary>
    /// 游戏原生 C# 事件 → FML EventBus 桥接层（PLAN-EventBus §6）。
    /// 在 <c>ModBehaviour.OnAfterSetup</c> 阶段由 <c>EventBusBootstrap.Init</c> 调用
    /// <see cref="WireUp"/> 一次性订阅 15 个原生事件并 publish 到
    /// <see cref="EventBusManager.Sync"/>；mod 卸载时调用 <see cref="TearDown"/> 解除订阅，
    /// 避免静态事件回调泄漏（PLAN §13 风险对策）。
    ///
    /// 实现说明：14 个事件（Health.OnHurt / Health.OnDead / LevelManager.OnLevelInitialized /
    /// LevelManager.OnMainCharacterDead / LevelManager.OnControllingCharacterChanged /
    /// EconomyManager.OnMoneyChanged / EconomyManager.OnItemUnlockStateChanged /
    /// AIMainBrain.OnPlayerHearSound / AIMainBrain.OnSoundSpawned /
    /// CraftingManager.OnItemCrafted / CraftingManager.OnFormulaUnlocked /
    /// QuestManager.OnTaskFinishedEvent / SavesSystem.OnCollectSaveData /
    /// SavesCounter.OnKillCountChanged）所属类型位于 Krafs.Publicizer 公开化的
    /// <c>TeamSoda.Duckov.Core</c> 程序集中。公开化副本与原始程序集同时被引用（csproj 的
    /// <c>TeamSoda.*</c> 通配符引用了原始 DLL），导致这些类型在编译期存在二义性（CS0229），
    /// 无法直接以 <c>Type.Event += handler</c> 形式订阅。故对这 14 个事件采用运行时反射 +
    /// <see cref="DynamicMethod"/> 动态构造匹配委托的方式订阅；<c>LocalizationManager</c>
    /// 所在的 <c>SodaLocalization</c> 未被公开化，无二义性，直接编译期订阅。
    /// </summary>
    public static class GameEventAdapters
    {
        // 反射订阅的事件信息与对应委托，供 TearDown 精确 -= 。
        private static readonly List<(EventInfo Evt, Delegate Handler)> _wired =
            new List<(EventInfo, Delegate)>();

        // LocalizationManager.OnSetLanguage 直接订阅的委托引用。
        private static Action<SystemLanguage>? _onSetLanguageHandler;

        /// <summary>
        /// 订阅 15 个游戏原生事件，将它们转发为 FML EventBus 事件。
        /// 必须与 <see cref="TearDown"/> 严格配对调用。
        /// </summary>
        public static void WireUp()
        {
            // ---- B3: 5 个核心事件 ----

            // Health.OnHurt → HurtEvent（可取消，但 effect 已应用）
            // 原生签名：Action<Health, DamageInfo>
            WireDynamicEvent("Health", "OnHurt", nameof(OnHurtBridge));

            // Health.OnDead → EntityDeathEvent（仅观察）
            // 原生签名：Action<Health, DamageInfo>
            WireDynamicEvent("Health", "OnDead", nameof(OnDeadBridge));

            // LevelManager.OnLevelInitialized → LevelInitializedEvent（仅观察）
            // 原生签名：Action（无参）
            WireDynamicEvent("LevelManager", "OnLevelInitialized", nameof(OnLevelInitBridge));

            // EconomyManager.OnMoneyChanged → MoneyChangedEvent（仅观察）
            // 原生签名：Action<long, long>
            WireDynamicEvent("Duckov.Economy.EconomyManager", "OnMoneyChanged", nameof(OnMoneyChangedBridge));

            // LocalizationManager.OnSetLanguage → LanguageChangedEvent（仅观察）
            // 原生签名：Action<SystemLanguage>（SodaLocalization 未公开化，可直接编译期订阅）
            _onSetLanguageHandler = OnSetLanguage;
            LocalizationManager.OnSetLanguage += _onSetLanguageHandler;

            // ---- B5: 剩余 10 个事件 ----

            // AIMainBrain.OnPlayerHearSound → PlayerHearSoundEvent（仅观察）
            // 原生签名待确认：推测 Action<SoundData>（1 参）
            WireDynamicEvent("AIMainBrain", "OnPlayerHearSound", nameof(OnPlayerHearSoundBridge));

            // AIMainBrain.OnSoundSpawned → SoundSpawnedEvent（仅观察）
            // 原生签名待确认：推测 Action<SoundData>（1 参）
            WireDynamicEvent("AIMainBrain", "OnSoundSpawned", nameof(OnSoundSpawnedBridge));

            // LevelManager.OnMainCharacterDead → PlayerDeathEvent（仅观察）
            // 原生签名：Action<DamageInfo>（1 参，值类型）—— 运行时确认，非推测。
            WireDynamicEvent("LevelManager", "OnMainCharacterDead", nameof(OnPlayerDeathBridge));

            // LevelManager.OnControllingCharacterChanged → ControllingCharacterChangedEvent（仅观察）
            // 原生签名待确认：推测 Action<CharacterMainControl, CharacterMainControl>（old, new 2 参）
            WireDynamicEvent("LevelManager", "OnControllingCharacterChanged", nameof(OnControllingCharChangedBridge));

            // EconomyManager.OnItemUnlockStateChanged → ItemUnlockStateChangedEvent（仅观察）
            // 原生签名：Action<ItemID, bool>（PLAN §6 表）
            WireDynamicEvent("Duckov.Economy.EconomyManager", "OnItemUnlockStateChanged", nameof(OnItemUnlockStateChangedBridge));

            // CraftingManager.OnItemCrafted → ItemCraftedEvent（仅观察）
            // 原生签名：Action<ItemData, int>（PLAN §6 表）
            WireDynamicEvent("CraftingManager", "OnItemCrafted", nameof(OnItemCraftedBridge));

            // CraftingManager.OnFormulaUnlocked → FormulaUnlockedEvent（仅观察）
            // 原生签名：Action<CraftingFormula>（PLAN §6 表）
            WireDynamicEvent("CraftingManager", "OnFormulaUnlocked", nameof(OnFormulaUnlockedBridge));

            // QuestManager.OnTaskFinishedEvent → QuestTaskFinishedEvent（仅观察）
            // 原生签名：Action<QuestTask>（PLAN §6 表）
            WireDynamicEvent("QuestManager", "OnTaskFinishedEvent", nameof(OnQuestTaskFinishedBridge));

            // SavesSystem.OnCollectSaveData → CollectSaveDataEvent（仅观察）
            // 原生签名待确认：推测 Action<SaveData>（1 参）
            WireDynamicEvent("SavesSystem", "OnCollectSaveData", nameof(OnCollectSaveDataBridge));

            // SavesCounter.OnKillCountChanged → KillCountChangedEvent（仅观察）
            // 原生签名：Action<int>（PLAN §6 表）
            WireDynamicEvent("SavesCounter", "OnKillCountChanged", nameof(OnKillCountChangedBridge));
        }

        /// <summary>
        /// 解除 <see cref="WireUp"/> 订阅的全部原生事件。mod 卸载时必须调用。
        /// </summary>
        public static void TearDown()
        {
            foreach (var (evt, handler) in _wired)
            {
                try { evt.RemoveEventHandler(null, handler); }
                catch (Exception e) { Debug.LogWarning($"[FML GameEventAdapters] TearDown RemoveEventHandler failed: {e.Message}"); }
            }
            _wired.Clear();

            if (_onSetLanguageHandler != null)
            {
                LocalizationManager.OnSetLanguage -= _onSetLanguageHandler;
                _onSetLanguageHandler = null;
            }
        }

        // ---- 反射 + DynamicMethod 订阅辅助 ----

        /// <summary>
        /// 通过运行时反射定位静态事件 <c>{typeName}.{eventName}</c>，动态构造一个与其
        /// 委托类型完全匹配的 handler（调用本类名为 <paramref name="bridgeMethod"/> 的静态方法），
        /// 并 <c>+=</c> 订阅。委托与 EventInfo 缓存到 <see cref="_wired"/> 供 TearDown。
        /// </summary>
        private static void WireDynamicEvent(string typeName, string eventName, string bridgeMethod)
        {
            Type? type = FindType(typeName);
            if (type == null)
            {
                Debug.LogWarning($"[FML GameEventAdapters] 未找到类型 {typeName}，跳过 {eventName} 桥接。");
                return;
            }
            EventInfo? evt = type.GetEvent(eventName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            if (evt == null)
            {
                Debug.LogWarning($"[FML GameEventAdapters] {typeName}.{eventName} 不存在，跳过桥接。");
                return;
            }
            Type? handlerType = evt.EventHandlerType;
            if (handlerType == null)
            {
                Debug.LogWarning($"[FML GameEventAdapters] {typeName}.{eventName} 无 EventHandlerType，跳过桥接。");
                return;
            }

            MethodInfo? bridge = typeof(GameEventAdapters).GetMethod(
                bridgeMethod, BindingFlags.NonPublic | BindingFlags.Static);
            if (bridge == null)
            {
                Debug.LogWarning($"[FML GameEventAdapters] 桥接方法 {bridgeMethod} 未找到，跳过 {eventName}。");
                return;
            }

            // 取委托 Invoke 的参数类型列表，用于构造 DynamicMethod。
            MethodInfo? invoke = handlerType.GetMethod("Invoke");
            if (invoke == null) return;
            Type[] paramTypes = invoke.GetParameters().Select(p => p.ParameterType).ToArray();
            ParameterInfo[] bridgeParams = bridge.GetParameters();

            // 桥接方法参数数不能多于原生事件参数数（否则调用时栈下溢，必然产生 InvalidProgramException）。
            if (bridgeParams.Length > paramTypes.Length)
            {
                Debug.LogWarning(
                    $"[FML GameEventAdapters] {typeName}.{eventName} 原生参数数（{paramTypes.Length}）" +
                    $"少于桥接方法 {bridgeMethod} 参数数（{bridgeParams.Length}），无法桥接，跳过。");
                return;
            }

            // 仅加载桥接方法实际需要的参数；多余的原生参数不入栈，
            // 避免 call 后栈非空、ret 时触发 InvalidProgramException（如 OnMainCharacterDead 实为 Action<DamageInfo>）。
            // 数量不一致时记录警告，便于后续校正桥接签名（不静默丢数据）。
            if (bridgeParams.Length != paramTypes.Length)
            {
                Debug.LogWarning(
                    $"[FML GameEventAdapters] {typeName}.{eventName} 原生参数数（{paramTypes.Length}）" +
                    $"与桥接方法 {bridgeMethod} 参数数（{bridgeParams.Length}）不一致，" +
                    $"将丢弃多余的原生参数。建议更新桥接方法签名以匹配原生事件。");
            }

            var dm = new DynamicMethod("fml_" + eventName, null, paramTypes, typeof(GameEventAdapters));
            ILGenerator il = dm.GetILGenerator();
            for (int i = 0; i < bridgeParams.Length; i++)
            {
                // 加载第 i 个参数到栈顶
                if (i == 0) il.Emit(OpCodes.Ldarg_0);
                else if (i == 1) il.Emit(OpCodes.Ldarg_1);
                else if (i == 2) il.Emit(OpCodes.Ldarg_2);
                else if (i == 3) il.Emit(OpCodes.Ldarg_3);
                else il.Emit(OpCodes.Ldarg_S, (byte)i);
                // 值类型需装箱为 object 以匹配桥接方法签名
                if (paramTypes[i].IsValueType)
                {
                    il.Emit(OpCodes.Box, paramTypes[i]);
                }
            }
            il.Emit(OpCodes.Call, bridge);
            il.Emit(OpCodes.Ret);

            Delegate del = dm.CreateDelegate(handlerType);
            evt.AddEventHandler(null, del);
            _wired.Add((evt, del));
        }

        /// <summary>
        /// 在当前已加载程序集中查找指定全名（全局命名空间用裸名，如 "Health"）的类型。
        /// 运行时游戏只加载原始程序集，不存在公开化副本的二义性。
        /// </summary>
        private static Type? FindType(string fullTypeName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    Type? t = asm.GetType(fullTypeName, false);
                    if (t != null) return t;
                }
                catch (Exception) { /* 个别程序集加载异常，继续尝试下一个 */ }
            }
            return null;
        }

        // ---- 桥接方法：native 事件参数 → FML EventBus publish ----
        // 参数以 object 接收（值类型由 DynamicMethod 装箱，引用类型直接传递）。

        private static void OnHurtBridge(object target, object info)
        {
            EventBusManager.Instance.Sync.Post(new HurtEvent(target, info));
        }

        private static void OnDeadBridge(object victim, object info)
        {
            EventBusManager.Instance.Sync.Post(new EntityDeathEvent(victim, info));
        }

        private static void OnLevelInitBridge()
        {
            // 原生事件无参；Manager 字段留空（见 LevelInitializedEvent 文档）。
            EventBusManager.Instance.Sync.Post(new LevelInitializedEvent(null));
        }

        private static void OnMoneyChangedBridge(object oldMoney, object nowMoney)
        {
            EventBusManager.Instance.Sync.Post(
                new MoneyChangedEvent((long)oldMoney, (long)nowMoney));
        }

        private static void OnSetLanguage(SystemLanguage lang)
        {
            // LanguageChangedEvent.LangCode 约定为 "zh_cn" 格式（见 LanguageChangedEvent 文档），
            // 而非 SystemLanguage 枚举名（"ChineseSimplified"）。通过 I18n.GetLangCode 统一转换，
            // 保持实现与文档一致。
            string langCode = I18n.GetLangCode(lang);
            EventBusManager.Instance.Sync.Post(new LanguageChangedEvent(langCode));
        }

        // ---- B5 桥接方法：native 事件参数 → FML EventBus publish ----
        // 参数以 object 接收（值类型由 DynamicMethod 装箱，引用类型直接传递）。
        // 原生签名均为推测（除 PLAN §6 表明确标注的），若实际不符需调整桥接方法参数个数。

        private static void OnPlayerHearSoundBridge(object soundInfo)
        {
            EventBusManager.Instance.Sync.Post(new PlayerHearSoundEvent(soundInfo));
        }

        private static void OnSoundSpawnedBridge(object soundInfo)
        {
            EventBusManager.Instance.Sync.Post(new SoundSpawnedEvent(soundInfo));
        }

        private static void OnPlayerDeathBridge(object info)
        {
            // 原生签名确认为 Action<DamageInfo>（值类型，DynamicMethod 装箱后以 object 传入）。
            EventBusManager.Instance.Sync.Post(new PlayerDeathEvent(info));
        }

        private static void OnControllingCharChangedBridge(object oldChar, object newChar)
        {
            EventBusManager.Instance.Sync.Post(
                new ControllingCharacterChangedEvent(oldChar, newChar));
        }

        private static void OnItemUnlockStateChangedBridge(object itemId, object unlocked)
        {
            // bool 为值类型，DynamicMethod 装箱后在此拆箱。
            EventBusManager.Instance.Sync.Post(
                new ItemUnlockStateChangedEvent(itemId, (bool)unlocked));
        }

        private static void OnItemCraftedBridge(object itemData, object count)
        {
            // int 为值类型，DynamicMethod 装箱后在此拆箱。
            EventBusManager.Instance.Sync.Post(
                new ItemCraftedEvent(itemData, (int)count));
        }

        private static void OnFormulaUnlockedBridge(object formula)
        {
            EventBusManager.Instance.Sync.Post(new FormulaUnlockedEvent(formula));
        }

        private static void OnQuestTaskFinishedBridge(object task)
        {
            EventBusManager.Instance.Sync.Post(new QuestTaskFinishedEvent(task));
        }

        private static void OnCollectSaveDataBridge(object saveData)
        {
            EventBusManager.Instance.Sync.Post(new CollectSaveDataEvent(saveData));
        }

        private static void OnKillCountChangedBridge(object total)
        {
            // int 为值类型，DynamicMethod 装箱后在此拆箱。
            EventBusManager.Instance.Sync.Post(new KillCountChangedEvent((int)total));
        }
    }
}