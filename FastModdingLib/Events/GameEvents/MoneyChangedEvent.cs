namespace FastModdingLib.Events.GameEvents
{
    /// <summary>
    /// 玩家金钱变更事件。桥接自游戏原生 <c>EconomyManager.OnMoneyChanged</c> 事件。
    /// 仅观察用途，不支持取消。
    /// </summary>
    public sealed class MoneyChangedEvent : Event
    {
        /// <summary>变更前的金钱数额。</summary>
        public long OldMoney { get; }

        /// <summary>变更后的金钱数额。</summary>
        public long NowMoney { get; }

        public MoneyChangedEvent(long oldMoney, long nowMoney)
        {
            OldMoney = oldMoney;
            NowMoney = nowMoney;
        }
    }
}