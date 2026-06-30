using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace FastModdingLib.Entities
{
    /// <summary>
    /// 将 C# IStateConfig 编译为 NodeCanvas BehaviourTree（PLAN-EnemyUtils §3）。
    /// 所有 NodeCanvas API 通过反射调用以避免编译期对 ParadoxNotion.dll 的硬引用。
    /// </summary>
    public static class StateMachineToBT
    {
        private const string BT_TYPE = "NodeCanvas.Framework.BehaviourTree";
        private const string GRAPH_TYPE = "NodeCanvas.Framework.Graph";

        /// <summary>编译状态机为 BehaviourTree。返回 object（实际类型为 BehaviourTree）。</summary>
        public static object Compile(IStateConfig config)
        {
            if (config == null) { Debug.LogError("[StateMachineToBT] config is null."); return null; }
            try
            {
                var states = DiscoverStates(config);
                if (states.Count == 0) { Debug.LogError("[StateMachineToBT] No states discovered."); return null; }

                Type btType = FindType(BT_TYPE);
                if (btType == null) { Debug.LogError("[StateMachineToBT] BehaviourTree type not found."); return null; }
                var bt = ScriptableObject.CreateInstance(btType);
                bt.name = "FML_Compiled_" + config.GetType().Name;

                Type graphType = FindType(GRAPH_TYPE);
                if (graphType == null) { Debug.LogError("[StateMachineToBT] Graph type not found."); return bt; }
                var graph = Activator.CreateInstance(graphType, nonPublic: true);

                var gf = btType.GetField("graph", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (gf != null) gf.SetValue(bt, graph);
                else { var gp = btType.GetProperty("graph", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic); gp?.SetValue(bt, graph); }

                var addNode = graphType.GetMethod("AddNode", Type.EmptyTypes)
                    ?? graphType.GetMethod("AddNode", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (addNode == null) { Debug.LogError("[StateMachineToBT] Graph.AddNode not found."); return bt; }

                var stateNodes = new Dictionary<string, object>(states.Count);
                foreach (var state in states)
                {
                    try
                    {
                        var node = addNode.Invoke(graph, null);
                        if (node == null) continue;
                        var np = node.GetType().GetProperty("name", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        if (np != null && np.CanWrite) np.SetValue(node, state);
                        else { var nf = node.GetType().GetField("name", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic); nf?.SetValue(node, state); }
                        stateNodes[state] = node;
                    }
                    catch (Exception ex) { Debug.LogError($"[StateMachineToBT] Node '{state}': {ex.Message}"); }
                }

                foreach (var kvp in stateNodes)
                {
                    Transition[] transitions;
                    try { transitions = config.GetTransitions(kvp.Key) ?? Array.Empty<Transition>(); }
                    catch (Exception ex) { Debug.LogError($"[StateMachineToBT] GetTransitions('{kvp.Key}'): {ex.Message}"); continue; }
                    Array.Sort(transitions, (a, b) => b.priority.CompareTo(a.priority));
                    foreach (var t in transitions)
                    {
                        if (string.IsNullOrEmpty(t.targetState)) continue;
                        if (!stateNodes.TryGetValue(t.targetState, out var tn)) continue;
                        try { ConnectNodes(kvp.Value, tn); }
                        catch (Exception ex) { Debug.LogError($"Connect '{kvp.Key}' → '{t.targetState}': {ex.Message}"); }
                    }
                }

                var init = config.GetInitialState();
                if (!string.IsNullOrEmpty(init) && stateNodes.TryGetValue(init, out var en))
                    SetEntryNode(graph, en, graphType);

                return bt;
            }
            catch (Exception e) { Debug.LogError($"[StateMachineToBT.Compile] Failed: {e}"); return null; }
        }

        private static List<string> DiscoverStates(IStateConfig config)
        {
            var discovered = new List<string>();
            var queue = new Queue<string>();
            var visited = new HashSet<string>();
            var init = config.GetInitialState();
            if (!string.IsNullOrEmpty(init)) { queue.Enqueue(init); visited.Add(init); }
            while (queue.Count > 0)
            {
                var cur = queue.Dequeue();
                discovered.Add(cur);
                Transition[] transitions;
                try { transitions = config.GetTransitions(cur) ?? Array.Empty<Transition>(); }
                catch (Exception e) { Debug.LogWarning($"[FML StateMachineToBT] GetTransitions('{cur}') threw: {e.Message}"); continue; }
                foreach (var t in transitions)
                    if (!string.IsNullOrEmpty(t.targetState) && visited.Add(t.targetState))
                        queue.Enqueue(t.targetState);
            }
            return discovered;
        }

        private static void ConnectNodes(object from, object to)
        {
            var t = from.GetType();
            var m = t.GetMethod("ConnectTo", new[] { t }) ?? t.GetMethod("AddConnection", new[] { t });
            m?.Invoke(from, new[] { to });
        }

        private static void SetEntryNode(object graph, object node, Type graphType)
        {
            var f = graphType.GetField("entryNode", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            f?.SetValue(graph, node);
        }

        private static Type FindType(string name)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try { var t = asm.GetType(name, false); if (t != null) return t; }
                catch (Exception) { /* 个别程序集加载异常，继续尝试下一个 */ }
            }
            return null;
        }
    }
}
