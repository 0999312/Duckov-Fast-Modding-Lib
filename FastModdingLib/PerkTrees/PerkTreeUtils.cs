using Duckov.PerkTrees;
using FastModdingLib.Register;
using FastModdingLib.Utils;
using NodeCanvas.Framework;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace FastModdingLib
{
    public static class PerkTreeUtils
    {
        private static readonly PerkTreeRegistry _perkRegistry = new PerkTreeRegistry();
        private static readonly HashSet<string> _registeredTreeIds = new HashSet<string>();
        private static readonly Dictionary<string, HashSet<string>> _treeIdsByOwner = new Dictionary<string, HashSet<string>>();
        private static bool _initialized;

        /// <summary>使用 <c>"PerkTree_"</c> 前缀标记 FML 注册的自定义 PerkTree。</summary>
        internal const string FML_TREE_PREFIX = "PerkTree_";

        internal static PerkTreeRegistry Registry => _perkRegistry;

        internal static void Init()
        {
            if (_initialized) return;
            _initialized = true;
            var id = new Identifier(FMLConstants.Domain, "perk");
            var meta = RegistryManager.Instance.Registry;
            if (meta is NonAlterableSimpleRegistry<ERegistry> nonAlt)
                nonAlt.SetIfAbsent(id, _perkRegistry, RegistryManager.CurrentModid);
            else
                meta.Set(id, _perkRegistry, RegistryManager.CurrentModid);
        }

        /// <summary>从 Identifier.Domain 反查对应的 PerkTree ID。</summary>
        private static string ResolveTreeId(string domain)
        {
            // 1) 遍历已注册条目，查找 domain 匹配的树
            foreach (var kvp in _perkRegistry)
            {
                if (kvp.Key.Domain == domain && kvp.Key.Path != null && kvp.Key.Path.StartsWith("tree_"))
                {
                    // registry key 为 "domain:tree_{treeId}"，提取 treeId
                    return kvp.Key.Path.Substring("tree_".Length);
                }
            }

            // 2) 尝试直接用 domain 作为原生 treeId
            var nativeTree = PerkTreeManager.GetPerkTree(domain);
            if (nativeTree != null)
                return domain;

            // 3) 兜底：检查是否有 PerkTreeManager 中名称以 domain 开头的树
            var perkTreesField = typeof(PerkTreeManager).GetField("perkTrees",
                BindingFlags.Static | BindingFlags.NonPublic);
            if (perkTreesField?.GetValue(null) is System.Collections.IList trees)
            {
                foreach (var t in trees)
                {
                    if (t is PerkTree pt && pt.name != null && pt.name.StartsWith(domain))
                        return pt.name;
                }
            }

            return domain;
        }

        // ===== 注册 Perk =====

        /// <summary>
        /// 在技能树上注册新 Perk。
        /// id.Domain → 推导 treeId（匹配已注册的 PerkTree 或作为原生 treeId 使用）
        /// id.Path → perk 唯一名称（兼作 GameObject.name，影响存档 key）
        /// </summary>
        public static Perk AddPerk(Identifier id, PerkRequirement req, Sprite icon, string? modid = null)
        {
            Init();
            string owner = modid ?? id.Domain;
            string treeId = ResolveTreeId(id.Domain);

            var tree = PerkTreeManager.GetPerkTree(treeId);
            if (tree == null)
                throw new ArgumentException($"PerkTree '{treeId}' not found (resolved from domain '{id.Domain}').");

            string perkName = id.Path;

            // 创建子 GameObject 挂 Perk 组件
            var perkGo = new GameObject(perkName);
            perkGo.transform.SetParent(tree.transform, false);
            var perk = perkGo.AddComponent<Perk>();

            // 反射设置字段（Perk 的字段是 [SerializeField]）
            var idField = typeof(Perk).GetField("id", BindingFlags.Instance | BindingFlags.NonPublic);
            if (idField != null) idField.SetValue(perk, 0);

            // 设置 Perk 图标
            if (icon != null)
            {
                var iconField = typeof(Perk).GetField("icon",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (iconField != null)
                {
                    iconField.SetValue(perk, icon);
                }
                else
                {
                    Debug.LogWarning($"[PerkTreeUtils.AddPerk] Perk.icon field not found; icon not set for '{perkName}'.");
                }
            }

            // 设置 Perk 前置条件
            if (req != null)
            {
                var reqField = typeof(Perk).GetField("requirement",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (reqField != null)
                {
                    reqField.SetValue(perk, req);
                }
                else
                {
                    Debug.LogWarning($"[PerkTreeUtils.AddPerk] Perk.requirement field not found; requirement not set for '{perkName}'.");
                }
            }

            // 强制让 PerkTree 重新扫描子 Perk
            tree.Collect();

            // 注册到 FML Registry（使用 id 本身作为 key）
            _perkRegistry.Set(id, perk, owner);

            return perk;
        }

        /// <summary>在现有技能树上注册新 Perk（旧版 string 签名）。</summary>
        [Obsolete("Use AddPerk(Identifier, PerkRequirement, Sprite, string) instead.")]
        public static Perk AddPerk(string treeId, string perkName, PerkRequirement req, Sprite icon, string modid)
        {
            return AddPerk(new Identifier(modid, perkName), req, icon, modid);
        }

        // ===== 连接 Perk =====

        /// <summary>
        /// 建立 Perk 前置关系：fromPerk 是 toPerk 的前置条件。
        /// 两个参数均为 Identifier——FML 从 Registry 反查对应的 Perk 实例。
        /// 使用 NodeCanvas Graph API 创建 PerkRelationNode 连接。
        /// </summary>
        public static void ConnectPerks(Identifier fromPerkId, Identifier toPerkId)
        {
            if (!_perkRegistry.TryGet(fromPerkId, out var fromPerk)) return;
            if (!_perkRegistry.TryGet(toPerkId, out var toPerk)) return;

            // 两者应在同一棵树上——从 Master 反查验证
            if (fromPerk.Master == null || toPerk.Master == null) return;
            if (fromPerk.Master != toPerk.Master) return;

            var graph = fromPerk.Master.relationGraphOwner?.graph as PerkRelationGraph;
            if (graph == null) return;

            // 确保两者在图中都有节点
            var fromNode = graph.GetRelatedNode(fromPerk)
                ?? graph.AddNode<PerkRelationNode>(Vector2.zero);
            fromNode.relatedNode = fromPerk;

            var toNode = graph.GetRelatedNode(toPerk)
                ?? graph.AddNode<PerkRelationNode>(Vector2.zero);
            toNode.relatedNode = toPerk;

            // NodeCanvas API：尝试 ConnectTo，回退反射
            var connectMethod = fromNode.GetType().GetMethod("ConnectTo",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                null, new[] { typeof(NodeCanvas.Framework.Node) }, null);
            if (connectMethod != null)
            {
                connectMethod.Invoke(fromNode, new object[] { toNode });
            }
            else
            {
                // 回退：使用 graph.ConnectNodes
                var altMethod = graph.GetType().GetMethod("ConnectNodes",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                    null, new[] { typeof(NodeCanvas.Framework.Node), typeof(NodeCanvas.Framework.Node) }, null);
                altMethod?.Invoke(graph, new object[] { fromNode, toNode });
            }
        }

        /// <summary>建立 Perk 前置关系（旧版 string 签名）。</summary>
        [Obsolete("Use ConnectPerks(Identifier, Identifier) instead.")]
        public static void ConnectPerks(string treeId, string fromPerkName, string toPerkName)
        {
            var tree = PerkTreeManager.GetPerkTree(treeId);
            if (tree == null) return;

            var fromPerk = FindPerkInTree(tree, fromPerkName);
            var toPerk = FindPerkInTree(tree, toPerkName);
            if (fromPerk == null || toPerk == null) return;

            foreach (var kvp in _perkRegistry)
            {
                if (kvp.Value == fromPerk)
                    ConnectPerks(kvp.Key, new Identifier(treeId, toPerkName));
            }
        }

        // ===== PerkBehaviour 辅助 =====

        /// <summary>在已有 Perk 上挂载自定义 PerkBehaviour。perkId 为 Identifier。</summary>
        public static T AddPerkBehaviour<T>(Identifier perkId) where T : PerkBehaviour
        {
            if (!_perkRegistry.TryGet(perkId, out var perk)) return null;
            return perk.gameObject.AddComponent<T>();
        }

        // ===== 注册完整 PerkTree =====

        /// <summary>
        /// 完整注册一棵自定义 PerkTree，含 LevelConfig patch。
        /// 自动创建 PerkTree GameObject + PerkRelationGraph + 注入到 PerkTreeManager。
        /// </summary>
        /// <param name="id">Identifier——Domain=modid, Path=treeID。</param>
        /// <param name="horizontal">连线方向是否水平（默认 false=垂直）。</param>
        /// <returns>创建的 PerkTree 实例。</returns>
        public static PerkTree RegisterPerkTree(Identifier id, bool horizontal = false)
        {
            Init();
            string treeId = id.Path;

            // 1. 创建 PerkTree GameObject + PerkTree 组件
            var go = new GameObject($"{FML_TREE_PREFIX}{treeId}");
            var tree = go.AddComponent<PerkTree>();

            // 反射设置 tree ID 字段
            var idField = typeof(PerkTree).GetField("perkTreeID", BindingFlags.Instance | BindingFlags.NonPublic);
            idField?.SetValue(tree, treeId);

            // 2. 创建 PerkRelationGraph（ScriptableObject）+ PerkTreeRelationGraphOwner
            var graph = ScriptableObject.CreateInstance<PerkRelationGraph>();
            graph.name = $"PerkRelationGraph_{treeId}";
            var graphOwner = go.AddComponent<PerkTreeRelationGraphOwner>();

            var graphField = typeof(PerkTreeRelationGraphOwner).GetField("graph",
                BindingFlags.Instance | BindingFlags.NonPublic);
            graphField?.SetValue(graphOwner, graph);

            var relGraphOwnerField = typeof(PerkTree).GetField("relationGraphOwner",
                BindingFlags.Instance | BindingFlags.NonPublic);
            relGraphOwnerField?.SetValue(tree, graphOwner);

            // 3. 注入到 PerkTreeManager.perkTrees（internal 字段，通过反射访问）
            var perkTreesField = typeof(PerkTreeManager).GetField("perkTrees",
                BindingFlags.Static | BindingFlags.NonPublic);
            var perkTrees = perkTreesField?.GetValue(null) as System.Collections.IList;
            if (perkTrees != null && !perkTrees.Contains(tree))
                perkTrees.Add(tree);

            // 4. 记录已注册的 treeId（供 IsFMLTree / patches 使用）和 owner 映射
            _registeredTreeIds.Add(treeId);
            if (!_treeIdsByOwner.TryGetValue(id.Domain, out var treeSet))
            {
                treeSet = new HashSet<string>();
                _treeIdsByOwner[id.Domain] = treeSet;
            }
            treeSet.Add(treeId);
            // 在 Perk 注册表中注册占位条目（便于按 modid 卸载时触发遍历）
            var registryId = new Identifier(id.Domain, $"tree_{treeId}");
            _perkRegistry.Set(registryId, null!, id.Domain);

            Debug.Log($"[PerkTreeUtils] Registered PerkTree '{treeId}' from mod '{id.Domain}'.");
            return tree;
        }

        /// <summary>检查指定 treeId 是否由 FML 注册。</summary>
        internal static bool IsFMLTree(string treeId)
        {
            return _registeredTreeIds.Contains(treeId);
        }

        // ===== 解锁 =====

        /// <summary>强制解锁指定 Perk。perkId 为 Identifier。</summary>
        public static void ForceUnlock(Identifier perkId)
        {
            if (!_perkRegistry.TryGet(perkId, out var perk)) return;
            perk.ForceUnlock();
        }

        /// <summary>强制解锁指定 Perk（旧版 string 签名）。</summary>
        [Obsolete("Use ForceUnlock(Identifier) instead.")]
        public static void ForceUnlock(string treeId, string perkName)
        {
            var tree = PerkTreeManager.GetPerkTree(treeId);
            if (tree == null) return;
            var perk = FindPerkInTree(tree, perkName);
            if (perk != null)
                perk.ForceUnlock();
        }

        // ===== 移除 =====

        /// <summary>按 Identifier 移除 Perk。</summary>
        public static bool RemovePerk(Identifier id) => _perkRegistry.Remove(id);

        /// <summary>批量卸载指定 mod 注册的全部 Perk 和自定义 PerkTree。</summary>
        public static int RemoveAllPerks(string modid)
        {
            int count = _perkRegistry.RemoveAllByOwner(modid);

            // 清理该 mod 注册的自定义 PerkTree
            if (_treeIdsByOwner.TryGetValue(modid, out var treeSet))
            {
                foreach (var treeId in treeSet)
                {
                    // 从 PerkTreeManager 移除
                    var perkTreesField = typeof(PerkTreeManager).GetField("perkTrees",
                        BindingFlags.Static | BindingFlags.NonPublic);
                    if (perkTreesField?.GetValue(null) is System.Collections.IList trees)
                    {
                        for (int i = trees.Count - 1; i >= 0; i--)
                        {
                            if (trees[i] is PerkTree t && t.name != null &&
                                t.name == $"{FML_TREE_PREFIX}{treeId}")
                            {
                                trees.RemoveAt(i);
                                if (t.gameObject != null)
                                    UnityEngine.Object.Destroy(t.gameObject);
                                break;
                            }
                        }
                    }
                    _registeredTreeIds.Remove(treeId);
                    count++;
                }
                treeSet.Clear();
            }

            return count;
        }

        // ===== 内部辅助 =====

        private static Perk? FindPerkInTree(PerkTree tree, string perkName)
        {
            foreach (var perk in tree.perks)
            {
                if (perk != null && perk.name == perkName)
                    return perk;
            }
            return null;
        }
    }
}
