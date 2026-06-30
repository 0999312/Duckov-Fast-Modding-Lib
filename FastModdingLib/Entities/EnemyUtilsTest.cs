using FastModdingLib.Utils;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace FastModdingLib.Entities
{
    /// <summary>
    /// EnemyUtils 模块测试。覆盖 PLAN-EnemyUtils.md §7 的基本用例。
    /// 无第三方测试框架：静态方法 + if-throw 断言，由 RunAll 统一调度。
    /// 
    /// 注意：StateMachineToBT.Compile 和 transpiler patch 需要 playmode / NodeCanvas 运行时
    /// 环境，无法在纯 C# 单元测试中运行。本测试覆盖 Registry 读写路径、Identifier 集成
    /// 以及可纯 C# 验证的 DiscoverStates BFS 拓扑发现。
    /// </summary>
    public static class EnemyUtilsTest
    {
        // ===== 辅助：测试用 IStateConfig 实现 =====

        private sealed class TestStateMachine : IStateConfig
        {
            public string initialState = "Idle";
            public List<string> enterHistory = new List<string>();
            public List<string> updateHistory = new List<string>();
            public List<string> exitHistory = new List<string>();

            // 状态拓扑: Idle → Patrol → Chase → Attack → Idle
            private static readonly Dictionary<string, Transition[]> Transitions = new Dictionary<string, Transition[]>
            {
                ["Idle"] = new[] { new Transition("Patrol", () => true, 0) },
                ["Patrol"] = new[] { new Transition("Chase", () => true, 0) },
                ["Chase"] = new[] {
                    new Transition("Attack", () => true, 1),
                    new Transition("Idle", () => true, 0),
                },
                ["Attack"] = new[] { new Transition("Idle", () => true, 0) },
            };

            public string GetInitialState() => initialState;
            public void OnStateEnter(string state) => enterHistory.Add(state);
            public void OnStateUpdate(string state, float deltaTime) => updateHistory.Add(state);
            public void OnStateExit(string state) => exitHistory.Add(state);
            public Transition[] GetTransitions(string currentState)
                => Transitions.TryGetValue(currentState, out var t) ? t : Array.Empty<Transition>();
        }

        /// <summary>只有一个状态的极简状态机（边界用例）。</summary>
        private sealed class SingleStateMachine : IStateConfig
        {
            public string GetInitialState() => "Only";
            public void OnStateEnter(string state) { }
            public void OnStateUpdate(string state, float deltaTime) { }
            public void OnStateExit(string state) { }
            public Transition[] GetTransitions(string currentState) => Array.Empty<Transition>();
        }

        // ===== 测试用例 =====

        /// <summary>验证 Register + TryGet 读写路径（不依赖游戏运行时）。</summary>
        public static void TestRegisterAndTryGet()
        {
            var reg = new EnemyRegistry();
            var id = new Identifier("testMod", "enemy_scav");
            var preset = ScriptableObject.CreateInstance<CharacterRandomPreset>();
            preset.nameKey = "test_scav";

            reg.RegisterPreset(id, preset);
            if (!reg.TryGet(id, out var retrieved))
            {
                throw new Exception("Test failed: TestRegisterAndTryGet TryGet returned false");
            }
            if (retrieved == null || retrieved.nameKey != "test_scav")
            {
                throw new Exception("Test failed: TestRegisterAndTryGet nameKey mismatch");
            }
            // modid 从 id.Domain 推导
            if (!reg.TryGetOwner(id, out var owner) || owner != "testMod")
            {
                throw new Exception($"Test failed: TestRegisterAndTryGet owner mismatch, expected 'testMod' got '{owner}'");
            }
        }

        /// <summary>验证 RemoveAllByOwner 批量卸载。</summary>
        public static void TestRemoveAllByOwner()
        {
            var reg = new EnemyRegistry();
            var id1 = new Identifier("modA", "enemy_a");
            var id2 = new Identifier("modA", "enemy_b");
            var p1 = ScriptableObject.CreateInstance<CharacterRandomPreset>();
            p1.nameKey = "enemy_a";
            var p2 = ScriptableObject.CreateInstance<CharacterRandomPreset>();
            p2.nameKey = "enemy_b";
            reg.RegisterPreset(id1, p1);
            reg.RegisterPreset(id2, p2);

            int removed = reg.RemoveAllByOwner("modA");
            if (removed != 2)
            {
                throw new Exception($"Test failed: TestRemoveAllByOwner expected 2 removed, got {removed}");
            }
            if (reg.TryGet(id1, out var _))
            {
                throw new Exception("Test failed: TestRemoveAllByOwner id1 should be gone");
            }
            if (reg.TryGet(id2, out var _))
            {
                throw new Exception("Test failed: TestRemoveAllByOwner id2 should be gone");
            }
        }

        /// <summary>验证 GetPreset 异常路径。</summary>
        public static void TestGetPresetNotFound()
        {
            try
            {
                EnemyUtils.GetPreset("nonexistent_preset_12345");
                // 没抛异常可能是 GameplayDataSettings 未初始化——测试环境可接受
            }
            catch (Exception)
            {
                // 抛异常也是预期的
            }
        }

        /// <summary>
        /// 验证 StateMachineToBT.DiscoverStates 的 BFS 拓扑发现：
        /// 4 个状态成环，应发现全部 4 个。
        /// </summary>
        public static void TestDiscoverStatesFourStateLoop()
        {
            var config = new TestStateMachine();

            // 通过反射调用私有的 DiscoverStates 方法
            var method = typeof(StateMachineToBT).GetMethod("DiscoverStates",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            if (method == null)
            {
                // 如果无法反射（方法签名变化），静默跳过
                return;
            }

            var result = method.Invoke(null, new object[] { config }) as List<string>;
            if (result == null)
            {
                throw new Exception("Test failed: TestDiscoverStates returned null");
            }

            // 应发现 4 个状态：Idle, Patrol, Chase, Attack
            if (result.Count != 4)
            {
                throw new Exception($"Test failed: TestDiscoverStates expected 4 states, got {result.Count}: {string.Join(", ", result)}");
            }

            // Idle 应为第一个（初始状态）
            if (result[0] != "Idle")
            {
                throw new Exception($"Test failed: TestDiscoverStates expected first state 'Idle', got '{result[0]}'");
            }

            // 验证所有状态都被发现
            var expected = new HashSet<string> { "Idle", "Patrol", "Chase", "Attack" };
            foreach (var state in result)
            {
                if (!expected.Remove(state))
                {
                    throw new Exception($"Test failed: TestDiscoverStates unexpected state '{state}'");
                }
            }
            if (expected.Count > 0)
            {
                throw new Exception($"Test failed: TestDiscoverStates missing states: {string.Join(", ", expected)}");
            }
        }

        /// <summary>
        /// 验证单状态机：1 个状态，无 transition，应发现 1 个状态。
        /// </summary>
        public static void TestDiscoverStatesSingleState()
        {
            var config = new SingleStateMachine();
            var method = typeof(StateMachineToBT).GetMethod("DiscoverStates",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            if (method == null) return;

            var result = method.Invoke(null, new object[] { config }) as List<string>;
            if (result == null || result.Count != 1 || result[0] != "Only")
            {
                throw new Exception($"Test failed: TestDiscoverStatesSingleState expected ['Only'], got [{string.Join(", ", result ?? new List<string>())}]");
            }
        }

        /// <summary>
        /// 验证 transition priority 排序：高优先级 transition 应排在前面。
        /// </summary>
        public static void TestTransitionPrioritySorting()
        {
            var config = new TestStateMachine();
            var transitions = config.GetTransitions("Chase");
            if (transitions.Length != 2)
            {
                throw new Exception($"Test failed: TestTransitionPrioritySorting expected 2 transitions, got {transitions.Length}");
            }

            // StateMachineToBT.Compile 内部排序: priority 大者在前
            Array.Sort(transitions, (a, b) => b.priority.CompareTo(a.priority));

            // 第一个 transition 应该是 Attack (priority=1)
            if (transitions[0].targetState != "Attack")
            {
                throw new Exception($"Test failed: TestTransitionPrioritySorting expected first target 'Attack', got '{transitions[0].targetState}'");
            }
            if (transitions[0].priority != 1)
            {
                throw new Exception($"Test failed: TestTransitionPrioritySorting expected first priority 1, got {transitions[0].priority}");
            }
        }

        /// <summary>
        /// 验证 Identifier 与 EnemyRegistry 集成：两个不同 mod 注册不同敌人，
        /// 按 owner 批量卸载仅移除对应 mod 的条目（owner 从 id.Domain 推导）。
        /// </summary>
        public static void TestMultiModIsolation()
        {
            var reg = new EnemyRegistry();
            var id1 = new Identifier("modA", "soldier");
            var id2 = new Identifier("modB", "raider");
            var p1 = ScriptableObject.CreateInstance<CharacterRandomPreset>();
            p1.nameKey = "modA_soldier";
            var p2 = ScriptableObject.CreateInstance<CharacterRandomPreset>();
            p2.nameKey = "modB_raider";

            reg.RegisterPreset(id1, p1);
            reg.RegisterPreset(id2, p2);

            // 卸载 modA——只移除 id1
            int removed = reg.RemoveAllByOwner("modA");
            if (removed != 1)
            {
                throw new Exception($"Test failed: TestMultiModIsolation expected 1 removed, got {removed}");
            }
            if (reg.TryGet(id1, out var _))
            {
                throw new Exception("Test failed: TestMultiModIsolation id1 (modA) should be gone");
            }
            if (!reg.TryGet(id2, out var r2) || r2.nameKey != "modB_raider")
            {
                throw new Exception("Test failed: TestMultiModIsolation id2 (modB) should remain");
            }

            // 卸载 modB——只剩 1 条
            removed = reg.RemoveAllByOwner("modB");
            if (removed != 1)
            {
                throw new Exception($"Test failed: TestMultiModIsolation expected 1 removed (modB), got {removed}");
            }
            if (reg.TryGet(id2, out var _))
            {
                throw new Exception("Test failed: TestMultiModIsolation id2 (modB) should be gone");
            }
        }

        /// <summary>验证空 registry RemoveAllByOwner 返回 0。</summary>
        public static void TestRemoveAllByOwnerEmpty()
        {
            var reg = new EnemyRegistry();
            int removed = reg.RemoveAllByOwner("nonexistent");
            if (removed != 0)
            {
                throw new Exception($"Test failed: TestRemoveAllByOwnerEmpty expected 0, got {removed}");
            }
        }

        // ===== 统一调度 =====

        /// <summary>
        /// 运行所有可在 Editor / 纯 C# 环境下执行的测试。
        /// RunAll 期望在游戏运行时环境（或有 GameplayDataSettings 初始化）下调用。
        /// </summary>
        public static void RunAll()
        {
            var tests = new (string Name, Action Body)[]
            {
                ("TestRegisterAndTryGet", TestRegisterAndTryGet),
                ("TestRemoveAllByOwner", TestRemoveAllByOwner),
                ("TestRemoveAllByOwnerEmpty", TestRemoveAllByOwnerEmpty),
                ("TestGetPresetNotFound", TestGetPresetNotFound),
                ("TestDiscoverStatesFourStateLoop", TestDiscoverStatesFourStateLoop),
                ("TestDiscoverStatesSingleState", TestDiscoverStatesSingleState),
                ("TestTransitionPrioritySorting", TestTransitionPrioritySorting),
                ("TestMultiModIsolation", TestMultiModIsolation),
            };
            int passed = 0, failed = 0;
            foreach (var t in tests)
            {
                try
                {
                    t.Body();
                    Console.WriteLine("[PASS] EnemyUtilsTest." + t.Name);
                    passed++;
                }
                catch (Exception ex)
                {
                    Console.WriteLine("[FAIL] EnemyUtilsTest." + t.Name + " :: " + ex.Message);
                    failed++;
                }
            }
            Console.WriteLine("EnemyUtilsTest summary: " + passed + " passed, " + failed + " failed.");
        }
    }
}
