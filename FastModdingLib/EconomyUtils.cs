using Duckov.Economy;
using FastModdingLib.Events;
using FastModdingLib.Events.GameEvents;
using FastModdingLib.Register;
using FastModdingLib.Utils;
using System;

namespace FastModdingLib
{
    /// <summary>
    /// 游戏经济系统封装（PLAN.md Phase 2）。
    /// 提供金钱增删查、物品解锁控制，以及 <see cref="MoneyChangedEvent"/> 订阅便捷 API。
    /// 所有写操作直接转发到游戏原生 <c>Duckov.Economy.EconomyManager</c> 静态 API，
    /// 由其负责触发 <c>OnMoneyChanged</c> / <c>OnItemUnlockStateChanged</c> 原生事件，
    /// FML <see cref="GameEventAdapters"/> 已将这些原生事件桥接到 FML EventBus。
    /// </summary>
    public static class EconomyUtils
    {
        // ---- 金钱查询 / 增删 ----

        /// <summary>
        /// 获取玩家当前账户金钱数额。
        /// 若 <c>EconomyManager</c> 尚未初始化（游戏未加载存档），返回 0。
        /// </summary>
        public static long GetMoney()
        {
            return EconomyManager.Money;
        }

        /// <summary>
        /// 增加玩家账户金钱。<paramref name="amount"/> 可为负数（等效扣除）。
        /// 调用后游戏会触发 <c>OnMoneyChanged</c>，进而发布 <see cref="MoneyChangedEvent"/>。
        /// </summary>
        /// <param name="amount">增加的金额；可为负。</param>
        /// <returns>是否成功（<c>EconomyManager.Instance</c> 存在时为 true）。</returns>
        public static bool AddMoney(long amount)
        {
            return EconomyManager.Add(amount);
        }

        /// <summary>
        /// 扣除玩家账户金钱。<paramref name="amount"/> 应为正数。
        /// 内部以 <c>EconomyManager.Add(-amount)</c> 实现，不检查余额是否充足
        /// （游戏原生 <c>Add</c> 不做余额校验，允许金钱为负）。
        /// 调用后游戏会触发 <c>OnMoneyChanged</c>。
        /// </summary>
        /// <param name="amount">扣除的金额；建议传正数。</param>
        /// <returns>是否成功（<c>EconomyManager.Instance</c> 存在时为 true）。</returns>
        public static bool RemoveMoney(long amount)
        {
            return EconomyManager.Add(-amount);
        }

        /// <summary>
        /// 直接设置玩家账户金钱为指定数额。
        /// 内部以 <c>Add(delta)</c>（delta = <paramref name="amount"/> - 当前金钱）实现，
        /// 避免依赖 <c>EconomyManager.Money</c> 的 private setter。
        /// 调用后游戏会触发 <c>OnMoneyChanged</c>。
        /// </summary>
        /// <param name="amount">目标金钱数额。</param>
        /// <returns>是否成功（<c>EconomyManager.Instance</c> 存在时为 true）。</returns>
        public static bool SetMoney(long amount)
        {
            return EconomyManager.Add(amount - EconomyManager.Money);
        }

        // ---- 物品解锁控制 ----

        /// <summary>
        /// 解锁指定物品（使其在对应商店中可见 / 可购买）。
        /// 转发到 <c>EconomyManager.Unlock(itemTypeID, needConfirm, showUI)</c>。
        /// </summary>
        /// <param name="itemTypeId">物品 ItemTypeID。</param>
        /// <param name="needConfirm">
        /// true 时物品进入"待确认解锁"队列（玩家需在商店确认）；
        /// false 时立即解锁。默认 false（立即解锁）。
        /// </param>
        /// <param name="showUI">是否显示解锁通知 UI。默认 true。</param>
        public static void UnlockItem(int itemTypeId, bool needConfirm = false, bool showUI = true)
        {
            EconomyManager.Unlock(itemTypeId, needConfirm, showUI);
        }

        /// <summary>
        /// 解锁指定物品（使其在对应商店中可见 / 可购买）。Identifier 版本。
        /// 内部通过 <see cref="ItemUtils.TryResolveTypeId"/> 将 Identifier 解析为原生 TypeID。
        /// </summary>
        /// <param name="id">物品 Identifier（与 CreateCustomItem 注册时使用的 id 一致）。</param>
        /// <inheritdoc cref="UnlockItem(int, bool, bool)"/>
        public static void UnlockItem(Identifier id, bool needConfirm = false, bool showUI = true)
        {
            if (ItemUtils.TryResolveTypeId(id, out int typeId))
                UnlockItem(typeId, needConfirm, showUI);
        }

        /// <summary>
        /// 确认一个处于"待确认解锁"队列中的物品，将其正式加入已解锁列表。
        /// 转发到 <c>EconomyManager.ConfirmUnlock(itemTypeID)</c>。
        /// </summary>
        /// <param name="itemTypeId">物品 ItemTypeID。</param>
        public static void ConfirmUnlockItem(int itemTypeId)
        {
            EconomyManager.ConfirmUnlock(itemTypeId);
        }

        /// <summary>
        /// 确认一个处于"待确认解锁"队列中的物品。Identifier 版本。
        /// </summary>
        /// <param name="id">物品 Identifier。</param>
        public static void ConfirmUnlockItem(Identifier id)
        {
            if (ItemUtils.TryResolveTypeId(id, out int typeId))
                ConfirmUnlockItem(typeId);
        }

        /// <summary>
        /// 查询物品是否已解锁（含默认解锁 + 已解锁列表 + 待确认队列的判定由原生 API 处理）。
        /// 转发到 <c>EconomyManager.IsUnlocked(itemTypeID)</c>。
        /// </summary>
        /// <param name="itemTypeId">物品 ItemTypeID。</param>
        /// <returns>已解锁返回 true；否则 false。</returns>
        public static bool IsItemUnlocked(int itemTypeId)
        {
            return EconomyManager.IsUnlocked(itemTypeId);
        }

        /// <summary>
        /// 查询物品是否已解锁。Identifier 版本。
        /// </summary>
        /// <param name="id">物品 Identifier。</param>
        /// <returns>已解锁返回 true；Identifier 无法解析或未解锁返回 false。</returns>
        public static bool IsItemUnlocked(Identifier id)
        {
            return ItemUtils.TryResolveTypeId(id, out int typeId) && IsItemUnlocked(typeId);
        }

        /// <summary>
        /// 查询物品是否处于"待确认解锁"队列中。
        /// 转发到 <c>EconomyManager.IsWaitingForUnlockConfirm(itemTypeID)</c>。
        /// </summary>
        /// <param name="itemTypeId">物品 ItemTypeID。</param>
        /// <returns>在待确认队列中返回 true；否则 false。</returns>
        public static bool IsItemWaitingForUnlockConfirm(int itemTypeId)
        {
            return EconomyManager.IsWaitingForUnlockConfirm(itemTypeId);
        }

        /// <summary>
        /// 查询物品是否处于"待确认解锁"队列中。Identifier 版本。
        /// </summary>
        /// <param name="id">物品 Identifier。</param>
        /// <returns>在待确认队列中返回 true；Identifier 无法解析或不在队列中返回 false。</returns>
        public static bool IsItemWaitingForUnlockConfirm(Identifier id)
        {
            return ItemUtils.TryResolveTypeId(id, out int typeId) && IsItemWaitingForUnlockConfirm(typeId);
        }

        // ---- 事件订阅便捷 API ----

        /// <summary>
        /// 订阅 <see cref="MoneyChangedEvent"/>（玩家金钱变更）。
        /// 封装 <see cref="EventBusManager.Sync"/> 的 <c>Register</c>，并以
        /// <see cref="RegistryManager.CurrentModid"/> 作为 owner，便于 mod 卸载时
        /// 通过 <see cref="EventBus.UnregisterAll"/> 批量清理。
        /// </summary>
        /// <param name="handler">事件处理回调。</param>
        /// <exception cref="ArgumentNullException"><paramref name="handler"/> 为 null。</exception>
        public static void OnMoneyChanged(Action<MoneyChangedEvent> handler)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            EventBusManager.Instance.Sync.Register(
                handler, 0, RegistryManager.CurrentModid);
        }

        /// <summary>
        /// 订阅 <see cref="ItemUnlockStateChangedEvent"/>（物品解锁状态变更）。
        /// 以 <see cref="RegistryManager.CurrentModid"/> 作为 owner，便于 mod 卸载时批量清理。
        /// </summary>
        /// <param name="handler">事件处理回调。</param>
        /// <exception cref="ArgumentNullException"><paramref name="handler"/> 为 null。</exception>
        public static void OnItemUnlockStateChanged(Action<ItemUnlockStateChangedEvent> handler)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            EventBusManager.Instance.Sync.Register(
                handler, 0, RegistryManager.CurrentModid);
        }

        /// <summary>
        /// 简化版金钱变更回调：直接以 <c>(oldMoney, nowMoney)</c> 两参形式订阅。
        /// 内部包装为 <see cref="MoneyChangedEvent"/> handler，owner 为
        /// <see cref="RegistryManager.CurrentModid"/>。
        /// </summary>
        /// <param name="callback">接收 (oldMoney, nowMoney) 的回调。</param>
        /// <exception cref="ArgumentNullException"><paramref name="callback"/> 为 null。</exception>
        public static void RegisterMoneyChangedCallback(Action<long, long> callback)
        {
            if (callback == null) throw new ArgumentNullException(nameof(callback));
            void Handler(MoneyChangedEvent e) => callback(e.OldMoney, e.NowMoney);
            EventBusManager.Instance.Sync.Register(
                (Action<MoneyChangedEvent>)Handler, 0, RegistryManager.CurrentModid);
        }
    }
}