using System;
using System.Collections.Generic;

namespace FastModdingLib.Events
{
    /// <summary>
    /// EventBus 同步总线测试。覆盖 PLAN-EventBus.md §10 的 6 类用例 + Clear。
    /// 无第三方测试框架：静态方法 + if-throw 断言，由 RunAll 统一调度。
    /// </summary>
    public static class EventBusTest
    {
        // ===== 测试用事件类型 =====

        private sealed class TestEvent : Event
        {
            public List<int> CallOrder = new List<int>();
        }

        [Cancelable]
        private sealed class CancelableTestEvent : Event
        {
            public bool HighCalled;
            public bool LowCalled;
        }

        private sealed class NonCancelableEvent : Event
        {
        }

        // ===== §10 测试用例 =====

        public static void TestPriorityOrder()
        {
            var bus = new EventBus();
            var evt = new TestEvent();
            bus.Register<TestEvent>(e => e.CallOrder.Add(10), 10, null);
            bus.Register<TestEvent>(e => e.CallOrder.Add(5), 5, null);
            bus.Register<TestEvent>(e => e.CallOrder.Add(1), 1, null);
            bus.Post(evt);
            if (evt.CallOrder.Count != 3
                || evt.CallOrder[0] != 10
                || evt.CallOrder[1] != 5
                || evt.CallOrder[2] != 1)
            {
                throw new Exception("Test failed: TestPriorityOrder expected 10->5->1, got "
                                    + string.Join(",", evt.CallOrder));
            }
        }

        public static void TestSamePriorityBothCalled()
        {
            var bus = new EventBus();
            var evt = new TestEvent();
            int a = 0, b = 0;
            bus.Register<TestEvent>(e => a++, 5, null);
            bus.Register<TestEvent>(e => b++, 5, null);
            bus.Post(evt);
            if (a != 1 || b != 1)
            {
                throw new Exception("Test failed: TestSamePriorityBothCalled expected both=1, got a=" + a + " b=" + b);
            }
        }

        public static void TestCancelableStopsLowPriority()
        {
            var bus = new EventBus();
            var evt = new CancelableTestEvent();
            bus.Register<CancelableTestEvent>(e =>
            {
                e.HighCalled = true;
                e.SetCancelled(true);
            }, 10, null);
            bus.Register<CancelableTestEvent>(e => e.LowCalled = true, 1, null);
            bus.Post(evt);
            if (!evt.HighCalled)
            {
                throw new Exception("Test failed: TestCancelableStopsLowPriority high handler not called");
            }
            if (evt.LowCalled)
            {
                throw new Exception("Test failed: TestCancelableStopsLowPriority low handler should not be called");
            }
            if (!evt.Cancelled)
            {
                throw new Exception("Test failed: TestCancelableStopsLowPriority event should be cancelled");
            }
        }

        public static void TestNonCancelableThrows()
        {
            var bus = new EventBus();
            var evt = new NonCancelableEvent();
            bool threw = false;
            bus.Register<NonCancelableEvent>(e =>
            {
                try
                {
                    e.SetCancelled(true);
                }
                catch (NotSupportedException)
                {
                    threw = true;
                }
            }, 1, null);
            bus.Post(evt);
            if (!threw)
            {
                throw new Exception("Test failed: TestNonCancelableThrows expected NotSupportedException");
            }
        }

        public static void TestUnregisterStopsHandler()
        {
            var bus = new EventBus();
            var evt = new TestEvent();
            int calls = 0;
            Action<TestEvent> handler = e => calls++;
            bus.Register<TestEvent>(handler, 1, null);
            bool removed = bus.Unregister<TestEvent>(handler);
            if (!removed)
            {
                throw new Exception("Test failed: TestUnregisterStopsHandler Unregister returned false");
            }
            bus.Post(evt);
            if (calls != 0)
            {
                throw new Exception("Test failed: TestUnregisterStopsHandler handler called after unregister: " + calls);
            }
        }

        public static void TestUnregisterAllByOwner()
        {
            var bus = new EventBus();
            var evt = new TestEvent();
            int callsA = 0, callsB = 0;
            var ownerA = new object();
            var ownerB = new object();
            bus.Register<TestEvent>(e => callsA++, 5, ownerA);
            bus.Register<TestEvent>(e => callsA++, 3, ownerA);
            bus.Register<TestEvent>(e => callsB++, 1, ownerB);
            int removed = bus.UnregisterAll(ownerA);
            if (removed != 2)
            {
                throw new Exception("Test failed: TestUnregisterAllByOwner expected removed=2, got " + removed);
            }
            bus.Post(evt);
            if (callsA != 0)
            {
                throw new Exception("Test failed: TestUnregisterAllByOwner ownerA handlers still called: " + callsA);
            }
            if (callsB != 1)
            {
                throw new Exception("Test failed: TestUnregisterAllByOwner ownerB handler missing, expected=1 got=" + callsB);
            }
        }

        public static void TestClear()
        {
            var bus = new EventBus();
            var evt = new TestEvent();
            int calls = 0;
            bus.Register<TestEvent>(e => calls++, 10, null);
            bus.Register<TestEvent>(e => calls++, 5, new object());
            bus.Register<TestEvent>(e => calls++, 1, null);
            bus.Clear();
            bus.Post(evt);
            if (calls != 0)
            {
                throw new Exception("Test failed: TestClear handlers called after Clear: " + calls);
            }
        }

        // ===== 统一调度 =====

        public static void RunAll()
        {
            var tests = new (string Name, Action Body)[]
            {
                ("TestPriorityOrder", TestPriorityOrder),
                ("TestSamePriorityBothCalled", TestSamePriorityBothCalled),
                ("TestCancelableStopsLowPriority", TestCancelableStopsLowPriority),
                ("TestNonCancelableThrows", TestNonCancelableThrows),
                ("TestUnregisterStopsHandler", TestUnregisterStopsHandler),
                ("TestUnregisterAllByOwner", TestUnregisterAllByOwner),
                ("TestClear", TestClear),
            };
            int passed = 0, failed = 0;
            foreach (var t in tests)
            {
                try
                {
                    t.Body();
                    Console.WriteLine("[PASS] EventBusTest." + t.Name);
                    passed++;
                }
                catch (Exception ex)
                {
                    Console.WriteLine("[FAIL] EventBusTest." + t.Name + " :: " + ex.Message);
                    failed++;
                }
            }
            Console.WriteLine("EventBusTest summary: " + passed + " passed, " + failed + " failed.");
        }
    }
}