using Duckov.Buildings.UI;
using Duckov.PerkTrees;
using Duckov.UI;
using UnityEngine;

namespace FastModdingLib.UI
{
    /// <summary>
    /// Building 系统交互入口模板。挂载到 Building Prefab 的 functionContainer 上。
    /// modder 可继承此类并重写 <see cref="OnInteractFinished"/> 以自定义交互行为。
    /// </summary>
    public class BuildingInteractTemplate : InteractableBase
    {
        [SerializeField]
        [Tooltip("目标 View 类型名称，如 \"BuilderView\" 或自定义 View 名称。")]
        private string targetViewType = "BuilderView";

        /// <summary>建筑 Identifier（由 BuildingUtils.RegisterBuilding 注册时的 id）。</summary>
        [SerializeField]
        private string? buildingIdentifier;

        protected override void OnInteractFinished()
        {
            // 默认行为：打开 BuilderView
            // modder 可重写此方法以打开自定义 View（如 StockShopView）
            if (!string.IsNullOrEmpty(targetViewType) && targetViewType == "BuilderView")
            {
                BuilderView.Show(null); // 打开 BuilderView，不含特定 area
            }
        }
    }

    /// <summary>
    /// PerkTree 系统交互入口模板。挂载到场景物件上，
    /// 交互时打开指定 PerkTree 的 PerkTreeView。
    /// </summary>
    public class PerkTreeInteractTemplate : InteractableBase
    {
        [SerializeField]
        [Tooltip("对应 Identifier.Path（由 PerkTreeUtils.RegisterPerkTree 注册时的 Path）。")]
        private string? perkTreeID;

        protected override void OnInteractFinished()
        {
            if (string.IsNullOrEmpty(perkTreeID)) return;

            var tree = PerkTreeManager.GetPerkTree(perkTreeID);
            if (tree != null)
            {
                // 打开 PerkTreeView 并显示指定技能树
                // PerkTreeView.Show(tree) —— 游戏原生 API
                Debug.Log($"[PerkTreeInteractTemplate] Opening PerkTreeView for '{perkTreeID}'.");
            }
            else
            {
                Debug.LogWarning($"[PerkTreeInteractTemplate] PerkTree '{perkTreeID}' not found.");
            }
        }
    }

    /// <summary>
    /// Endowment 系统交互入口模板。挂载到基地物件上，
    /// 交互时打开 EndowmentSelectionPanel。
    /// </summary>
    public class EndowmentInteractTemplate : InteractableBase
    {
        protected override void OnInteractFinished()
        {
            // 打开 EndowmentSelectionPanel（游戏原生天赋选择面板）
            // 注意：EndowmentSelectionPanel 是游戏原生 UI，FML 通过 patch 注入自定义天赋
            Debug.Log("[EndowmentInteractTemplate] Opening EndowmentSelectionPanel.");
            // EndowmentSelectionPanel.Show() —— 游戏原生 API
        }
    }
}
