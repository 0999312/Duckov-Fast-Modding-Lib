using Duckov.Quests;
using ItemStatsSystem;
using System;
using System.Reflection;
using UnityEngine;

namespace FastModdingLib.Quests
{
    /// <summary>
    /// FML 扩展击杀任务：支持按武器标签匹配击杀。
    /// 继承自 <see cref="Task"/>，不 Patch 原生 QuestTask_KillCount。
    /// </summary>
    public class FMLTask_KillCountByTag : Task
    {
        [SerializeField] private int requireAmount = 1;
        [SerializeField] private int amount;
        [SerializeField] private string weaponTag = "";
        [SerializeField] private string requireEnemyName = "";
        [SerializeField] private bool requireHeadShot;

        public int RequireAmount { get => requireAmount; internal set => requireAmount = value; }
        public int Amount => amount;
        public string WeaponTag { get => weaponTag; internal set => weaponTag = value ?? ""; }
        public string RequireEnemyName { get => requireEnemyName; internal set => requireEnemyName = value ?? ""; }
        public bool RequireHeadShot { get => requireHeadShot; internal set => requireHeadShot = value; }

        private static readonly MethodInfo? _addEventHandler = typeof(Health)
            .GetEvent("OnDead", BindingFlags.Public | BindingFlags.Static)
            ?.GetAddMethod();

        private static readonly MethodInfo? _removeEventHandler = typeof(Health)
            .GetEvent("OnDead", BindingFlags.Public | BindingFlags.Static)
            ?.GetRemoveMethod();

        private Delegate? _subscribedHandler;

        protected override bool CheckFinished()
            => amount >= requireAmount;

        public override object GenerateSaveData() => amount;
        public override void SetupSaveData(object data)
        { if (data is int n) amount = n; }

        protected override void OnInit()
        {
            if (_addEventHandler != null)
            {
                var handler = Delegate.CreateDelegate(
                    typeof(Action<Health, DamageInfo>), this,
                    GetType().GetMethod("OnEnemyDead", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?? throw new InvalidOperationException("OnEnemyDead method not found"));
                _subscribedHandler = handler;
                _addEventHandler.Invoke(null, new object[] { handler });
            }
        }

        private void OnDisable()
        {
            if (_removeEventHandler != null && _subscribedHandler != null)
            {
                _removeEventHandler.Invoke(null, new object[] { _subscribedHandler });
                _subscribedHandler = null;
            }
        }

        private void OnEnemyDead(Health health, DamageInfo info)
        {
            if (health.team == Teams.player) return;

            var fromChar = info.fromCharacter;
            if (fromChar == null || !fromChar.IsMainCharacter()) return;

            if (!string.IsNullOrEmpty(weaponTag))
            {
                var weapon = fromChar.PrimWeaponSlot()?.Content;
                if (weapon == null) return;
                if (!ItemUtils.HasTag(weapon, weaponTag)) return;
            }

            if (!string.IsNullOrEmpty(requireEnemyName))
            {
                var victim = health.TryGetCharacter();
                if (victim?.characterPreset?.nameKey != requireEnemyName) return;
            }

            if (requireHeadShot && info.crit <= 0) return;

            if (amount < requireAmount)
            {
                amount++;
                ReportStatusChanged();
            }
        }
    }
}
