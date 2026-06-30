using Duckov.PerkTrees;
using FastModdingLib.Register;
using FastModdingLib.Utils;
using System;
using UnityEngine;

namespace FastModdingLib
{
    public static class PerkTreeUtils
    {
        private static readonly PerkTreeRegistry _perkRegistry = new PerkTreeRegistry();
        private static bool _initialized;

        internal static PerkTreeRegistry Registry => _perkRegistry;

        internal static void Init()
        {
            if (_initialized) return;
            _initialized = true;
            var id = new Identifier("fastmoddinglib", "perk");
            var meta = RegistryManager.Instance.Registry;
            if (meta is NonAlterableSimpleRegistry<ERegistry> nonAlt)
                nonAlt.SetIfAbsent(id, _perkRegistry, RegistryManager.CurrentModid);
            else
                meta.Set(id, _perkRegistry, RegistryManager.CurrentModid);
        }

        /// <summary>
        /// 在现有技能树上注册一个新 Perk。
        /// </summary>
        /// <param name="treeId">目标 PerkTree ID。</param>
        /// <param name="perkName">Perk 唯一名称（也用作 GameObject.name，影响存档 key）。</param>
        /// <param name="req">解锁需求（等级/货币/时间）。</param>
        /// <param name="icon">技能图标 Sprite。</param>
        /// <param name="modid">注册者 mod 身份。</param>
        /// <returns>创建的 Perk 实例。</returns>
        public static Perk AddPerk(string treeId, string perkName, PerkRequirement req, Sprite icon, string modid)
        {
            Init();
            var tree = PerkTreeManager.GetPerkTree(treeId);
            if (tree == null)
                throw new ArgumentException($"PerkTree '{treeId}' not found.", nameof(treeId));

            // 创建子 GameObject 挂 Perk 组件
            var perkGo = new GameObject(perkName);
            perkGo.transform.SetParent(tree.transform, false);
            var perk = perkGo.AddComponent<Perk>();
            // 反射设置字段（Perk 的字段是 [SerializeField]）
            // ID 字段：设为 0 表示新 Perk
            var idField = typeof(Perk).GetField("id", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (idField != null) idField.SetValue(perk, 0);

            // 设置 Perk 图标
            if (icon != null)
            {
                var iconField = typeof(Perk).GetField("icon",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                if (iconField != null)
                {
                    iconField.SetValue(perk, icon);
                }
                else
                {
                    Debug.LogWarning($"[PerkTreeUtils.AddPerk] Perk.icon field not found via reflection; icon not set for '{perkName}'.");
                }
            }

            // 设置 Perk 前置条件
            if (req != null)
            {
                var reqField = typeof(Perk).GetField("requirement",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                if (reqField != null)
                {
                    reqField.SetValue(perk, req);
                }
                else
                {
                    Debug.LogWarning($"[PerkTreeUtils.AddPerk] Perk.requirement field not found via reflection; requirement not set for '{perkName}'.");
                }
            }

            // 强制让 PerkTree 重新扫描子 Perk
            tree.Collect();

            // 注册到 FML Registry
            var identifier = new Identifier("fastmoddinglib", $"perk_{treeId}_{perkName}");
            _perkRegistry.Set(identifier, perk, modid);

            return perk;
        }

        /// <summary>
        /// 在两个 Perk 之间建立前置关系（fromPerk → toPerk：from 是 to 的前置）。
        /// 通过 NodeCanvas Graph API 创建 PerkRelationNode 连接。
        /// </summary>
        public static void ConnectPerks(string treeId, string fromPerkName, string toPerkName)
        {
            var tree = PerkTreeManager.GetPerkTree(treeId);
            if (tree == null) return;

            var fromPerk = FindPerkInTree(tree, fromPerkName);
            var toPerk = FindPerkInTree(tree, toPerkName);
            if (fromPerk == null || toPerk == null) return;

            // 通过 NodeCanvas Graph API 建立节点连接
            var graph = tree.relationGraphOwner?.graph as PerkRelationGraph;
            if (graph == null) return;

            var fromNode = graph.GetRelatedNode(fromPerk);
            var toNode = graph.GetRelatedNode(toPerk);
            if (fromNode != null && toNode != null)
            {
                // NodeCanvas: PerkRelationNodeBase 的 AddConnection 或类似 API
                // TODO: 确认 PerkRelationNode.ConnectTo 或 graph.ConnectNodes (from, to) 的准确签名
                // 当前用 try/catch 包装，待运行时验证
                try
                {
                    // 尝试通过反射调用 ConnectTo（NodeCanvas Node 基类可能有此方法）
                    var connectMethod = fromNode.GetType().GetMethod("ConnectTo", new[] { typeof(NodeCanvas.Framework.Node) })
                        ?? fromNode.GetType().GetMethod("AddConnection", new[] { typeof(NodeCanvas.Framework.Node) });
                    connectMethod?.Invoke(fromNode, new object[] { toNode });
                }
                catch (Exception e)
                {
                    Debug.LogError($"[PerkTreeUtils.ConnectPerks] Failed to connect '{fromPerkName}' → '{toPerkName}': {e}");
                }
            }
        }

        /// <summary>强制解锁指定 Perk。</summary>
        public static void ForceUnlock(string treeId, string perkName)
        {
            var tree = PerkTreeManager.GetPerkTree(treeId);
            if (tree == null) return;
            var perk = FindPerkInTree(tree, perkName);
            if (perk != null)
            {
                perk.ForceUnlock();
            }
        }

        private static Perk? FindPerkInTree(PerkTree tree, string perkName)
        {
            foreach (var perk in tree.perks)
            {
                if (perk != null && perk.name == perkName)
                    return perk;
            }
            return null;
        }

        /// <summary>按 Identifier 移除 Perk。</summary>
        public static bool RemovePerk(Identifier id) => _perkRegistry.Remove(id);

        /// <summary>批量卸载指定 mod 注册的全部 Perk。</summary>
        public static int RemoveAllPerks(string modid) => _perkRegistry.RemoveAllByOwner(modid);
    }
}
