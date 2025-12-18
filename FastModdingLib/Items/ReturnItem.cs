using Cysharp.Threading.Tasks;
using Duckov.Buffs;
using Duckov.UI;
using ItemStatsSystem;
using SodaCraft.Localizations;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using static ItemStatsSystem.UsageBehavior;

namespace FastModdingLib
{
    public class ReturnItem : UsageBehavior
    {
        public int ItemTypeID;
        
        public bool showItemName = false;
        [LocalizationKey("Default")]
        private string descKey = "UI_ReturnItem";

        public override DisplaySettingsData DisplaySettings
        {
            get
            {
                DisplaySettingsData result = default(DisplaySettingsData);
                if (showItemName)
                {
                    result.display = true;
                    result.description = "";
                    result.description += $"{descKey.ToPlainText()}: {ItemAssetsCollection.GetPrefab(ItemTypeID).DisplayName}";
                }
                return result;
            }
        }

        public override bool CanBeUsed(Item item, object user)
        {
            return item.IsUsable(user);
        }

        protected override void OnUse(Item item, object user)
        {
            CharacterMainControl? characterMainControl = user as CharacterMainControl;
            if (!(characterMainControl == null))
            {
                Generate(characterMainControl).Forget();
            }

        }
        private bool running;
        private async UniTask Generate(CharacterMainControl character)
        {
            if (running)
            {
                return;
            }
            running = true;
            Item item = await ItemAssetsCollection.InstantiateAsync(ItemTypeID);
            string displayName = item.DisplayName;
            bool num = character.PickupItem(item);
            if (!num && item != null)
            {
                if (item.ActiveAgent != null)
                {
                    item.AgentUtilities.ReleaseActiveAgent();
                }
                PlayerStorage.Push(item);
            }
            running = false;
        }

    }

}
