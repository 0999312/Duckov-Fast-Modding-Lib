using FastModdingLib.Utils;
using System;

namespace FastModdingLib.Register
{
    /// <summary>
    /// Register 一体化模块测试。覆盖 PLAN-Register.md §8 验收清单 DOD 全部用例。
    /// 无第三方测试框架：静态方法 + if-throw 断言，由 RunAll 统一调度。
    /// </summary>
    public static class RegisterTest
    {
        // ===== 测试用 Registry 子类（验证 OnRemoved 回调） =====

        private sealed class CountingRegistry : SimpleRegistry<string>
        {
            public int RemovedCount;
            public Identifier? LastRemovedId;
            public string? LastRemovedModid;

            protected override void OnRemoved(Identifier id, string value, string? modid)
            {
                RemovedCount++;
                LastRemovedId = id;
                LastRemovedModid = modid;
            }
        }

        // ===== §8 DOD 测试用例 =====

        public static void TestSetGetTryGet()
        {
            var reg = new SimpleRegistry<string>();
            var id = new Identifier("test", "hello");
            reg.Set(id, "hello");
            if (reg.Get(id) != "hello")
            {
                throw new Exception("Test failed: TestSetGetTryGet Get returned " + reg.Get(id));
            }
            if (!reg.TryGet(id, out var val) || val != "hello")
            {
                throw new Exception("Test failed: TestSetGetTryGet TryGet returned " + val);
            }
            var missing = new Identifier("test", "missing");
            if (reg.TryGet(missing, out var _))
            {
                throw new Exception("Test failed: TestSetGetTryGet TryGet should be false for missing key");
            }
        }

        public static void TestRemove()
        {
            var reg = new SimpleRegistry<string>();
            var id = new Identifier("test", "hello");
            reg.Set(id, "hello");
            if (!reg.Remove(id))
            {
                throw new Exception("Test failed: TestRemove Remove returned false for existing key");
            }
            if (reg.TryGet(id, out var _))
            {
                throw new Exception("Test failed: TestRemove TryGet should be false after Remove");
            }
            if (reg.Remove(id))
            {
                throw new Exception("Test failed: TestRemove Remove returned true for already-removed key");
            }
        }

        public static void TestClear()
        {
            var reg = new SimpleRegistry<string>();
            reg.Set(new Identifier("test", "a"), "1");
            reg.Set(new Identifier("test", "b"), "2");
            reg.Set(new Identifier("test", "c"), "3");
            reg.Clear();
            int count = 0;
            var enumerator = reg.GetEnumerator();
            while (enumerator.MoveNext())
            {
                count++;
            }
            if (count != 0)
            {
                throw new Exception("Test failed: TestClear expected count=0 after Clear, got " + count);
            }
        }

        public static void TestSetWithModid()
        {
            var reg = new SimpleRegistry<string>();
            var id = new Identifier("test", "hello");
            reg.Set(id, "hello", "modA");
            if (!reg.TryGetOwner(id, out var modid) || modid != "modA")
            {
                throw new Exception("Test failed: TestSetWithModid TryGetOwner returned " + modid);
            }
        }

        public static void TestGetAllByOwner()
        {
            var reg = new SimpleRegistry<string>();
            reg.Set(new Identifier("test", "a1"), "1", "modA");
            reg.Set(new Identifier("test", "a2"), "2", "modA");
            reg.Set(new Identifier("test", "a3"), "3", "modA");
            reg.Set(new Identifier("test", "b1"), "1", "modB");
            var modA = reg.GetAllByOwner("modA");
            if (modA.Count != 3)
            {
                throw new Exception("Test failed: TestGetAllByOwner expected modA count=3, got " + modA.Count);
            }
            var modB = reg.GetAllByOwner("modB");
            if (modB.Count != 1)
            {
                throw new Exception("Test failed: TestGetAllByOwner expected modB count=1, got " + modB.Count);
            }
        }

        public static void TestRemoveAllByOwner()
        {
            var reg = new SimpleRegistry<string>();
            reg.Set(new Identifier("test", "a1"), "1", "modA");
            reg.Set(new Identifier("test", "a2"), "2", "modA");
            reg.Set(new Identifier("test", "a3"), "3", "modA");
            reg.Set(new Identifier("test", "b1"), "1", "modB");
            int removed = reg.RemoveAllByOwner("modA");
            if (removed != 3)
            {
                throw new Exception("Test failed: TestRemoveAllByOwner expected removed=3, got " + removed);
            }
            if (reg.GetAllByOwner("modA").Count != 0)
            {
                throw new Exception("Test failed: TestRemoveAllByOwner modA entries not cleared");
            }
            if (reg.GetAllByOwner("modB").Count != 1)
            {
                throw new Exception("Test failed: TestRemoveAllByOwner modB entries should remain");
            }
        }

        public static void TestRemoveAllByOwnerTriggersOnRemoved()
        {
            var reg = new CountingRegistry();
            reg.Set(new Identifier("test", "a1"), "1", "modA");
            reg.Set(new Identifier("test", "a2"), "2", "modA");
            reg.Set(new Identifier("test", "b1"), "1", "modB");
            int removed = reg.RemoveAllByOwner("modA");
            if (removed != 2)
            {
                throw new Exception("Test failed: TestRemoveAllByOwnerTriggersOnRemoved expected removed=2, got " + removed);
            }
            if (reg.RemovedCount != 2)
            {
                throw new Exception("Test failed: TestRemoveAllByOwnerTriggersOnRemoved OnRemoved count expected=2, got " + reg.RemovedCount);
            }
            if (reg.GetAllByOwner("modB").Count != 1)
            {
                throw new Exception("Test failed: TestRemoveAllByOwnerTriggersOnRemoved modB should remain");
            }
        }

        public static void TestReverseLookupRegistry()
        {
            var reg = new ReverseLookupRegistry<string, int>(s => s.Length);
            var id = new Identifier("test", "hello");
            reg.Register(5, id, "hello", "modA");
            if (!reg.TryGetIdentifier(5, out var id2) || !id2!.Equals(id))
            {
                throw new Exception("Test failed: TestReverseLookupRegistry TryGetIdentifier returned " + id2);
            }
            if (!reg.Remove(id))
            {
                throw new Exception("Test failed: TestReverseLookupRegistry Remove returned false");
            }
            if (reg.TryGetIdentifier(5, out var _))
            {
                throw new Exception("Test failed: TestReverseLookupRegistry reverse index not cleared after Remove");
            }
        }

        public static void TestReverseLookupRemoveAllByOwner()
        {
            var reg = new ReverseLookupRegistry<string, int>(s => s.Length);
            reg.Register(2, new Identifier("test", "a1"), "ab", "modA");
            reg.Register(3, new Identifier("test", "a2"), "abc", "modA");
            reg.Register(4, new Identifier("test", "b1"), "abcd", "modB");
            reg.Register(5, new Identifier("test", "b2"), "abcde", "modB");
            int removed = reg.RemoveAllByOwner("modA");
            if (removed != 2)
            {
                throw new Exception("Test failed: TestReverseLookupRemoveAllByOwner expected removed=2, got " + removed);
            }
            if (reg.TryGetIdentifier(2, out var _))
            {
                throw new Exception("Test failed: TestReverseLookupRemoveAllByOwner reverse index for modA key=2 not cleared");
            }
            if (reg.TryGetIdentifier(3, out var _))
            {
                throw new Exception("Test failed: TestReverseLookupRemoveAllByOwner reverse index for modA key=3 not cleared");
            }
            if (!reg.TryGetIdentifier(4, out var _))
            {
                throw new Exception("Test failed: TestReverseLookupRemoveAllByOwner reverse index for modB key=4 should remain");
            }
            if (!reg.TryGetIdentifier(5, out var _))
            {
                throw new Exception("Test failed: TestReverseLookupRemoveAllByOwner reverse index for modB key=5 should remain");
            }
        }

        public static void TestEnterModScope()
        {
            string before = RegistryManager.CurrentModid;
            var reg = new SimpleRegistry<string>();
            var id = new Identifier("test", "scoped");
            using (RegistryManager.EnterModScope("testmod"))
            {
                if (RegistryManager.CurrentModid != "testmod")
                {
                    throw new Exception("Test failed: TestEnterModScope CurrentModid expected 'testmod', got " + RegistryManager.CurrentModid);
                }
                reg.Set(id, "hello");
            }
            if (RegistryManager.CurrentModid != before)
            {
                throw new Exception("Test failed: TestEnterModScope CurrentModid not restored, expected " + before + " got " + RegistryManager.CurrentModid);
            }
            if (!reg.TryGetOwner(id, out var modid) || modid != "testmod")
            {
                throw new Exception("Test failed: TestEnterModScope TryGetOwner expected 'testmod', got " + modid);
            }
        }

        public static void TestIdentifierToString()
        {
            var id = new Identifier("domain", "path");
            if (id.ToString() != "domain:path")
            {
                throw new Exception("Test failed: TestIdentifierToString expected 'domain:path', got " + id.ToString());
            }
        }

        public static void TestIdentifierParseRoundTrip()
        {
            var parsed = Identifier.Parse("domain:path");
            var constructed = new Identifier("domain", "path");
            if (!parsed.Equals(constructed))
            {
                throw new Exception("Test failed: TestIdentifierParseRoundTrip Parse result not Equals to constructed");
            }
            if (parsed.Domain != "domain" || parsed.Path != "path")
            {
                throw new Exception("Test failed: TestIdentifierParseRoundTrip Domain/Path mismatch");
            }
        }

        public static void TestIdentifierTryParseFail()
        {
            if (Identifier.TryParse("invalid", out var _))
            {
                throw new Exception("Test failed: TestIdentifierTryParseFail expected false for 'invalid' (no colon)");
            }
            if (Identifier.TryParse(null!, out var _))
            {
                throw new Exception("Test failed: TestIdentifierTryParseFail expected false for null");
            }
            if (!Identifier.TryParse("domain:path", out var ok) || ok == null)
            {
                throw new Exception("Test failed: TestIdentifierTryParseFail expected true for valid 'domain:path'");
            }
        }

        public static void TestIdentifierWithSubdirectory()
        {
            // 两参数构造函数 — 允许 path 中含 /
            var id = new Identifier("domain", "path/to/resource");
            if (id.Path != "path/to/resource")
            {
                throw new Exception("Test failed: TestIdentifierWithSubdirectory Path expected 'path/to/resource', got " + id.Path);
            }
            if (id.ToString() != "domain:path/to/resource")
            {
                throw new Exception("Test failed: TestIdentifierWithSubdirectory ToString expected 'domain:path/to/resource', got " + id.ToString());
            }

            // 单参数解析 — 允许 path 中含 /
            var parsed = Identifier.Parse("domain:path/to/resource");
            if (!parsed.Equals(id))
            {
                throw new Exception("Test failed: TestIdentifierWithSubdirectory Parse roundtrip mismatch");
            }
            if (parsed.Domain != "domain" || parsed.Path != "path/to/resource")
            {
                throw new Exception("Test failed: TestIdentifierWithSubdirectory Domain/Path mismatch: " + parsed.Domain + " / " + parsed.Path);
            }

            // TryParse 应该对含 / path 返回 true
            if (!Identifier.TryParse("ns:sub/dir/res", out var ok) || ok == null)
            {
                throw new Exception("Test failed: TestIdentifierWithSubdirectory TryParse should succeed for path with /");
            }

            // domain 中不允许含 /
            try
            {
                var _ = new Identifier("bad/domain", "path");
                throw new Exception("Test failed: TestIdentifierWithSubdirectory expected exception for / in domain (two-arg)");
            }
            catch (ArgumentException) { /* expected */ }

            // 单参数 domain 中不允许含 /
            if (Identifier.TryParse("bad/domain:path", out var _))
            {
                throw new Exception("Test failed: TestIdentifierWithSubdirectory TryParse should fail for / in domain");
            }

            // 深层子目录
            var deep = new Identifier("ns", "a/b/c/d/e");
            if (deep.Path != "a/b/c/d/e")
            {
                throw new Exception("Test failed: TestIdentifierWithSubdirectory deep path mismatch");
            }
        }

        public static void TestNonAlterableSetIfAbsent()
        {
            var reg = new NonAlterableSimpleRegistry<string>();
            var id = new Identifier("test", "x");
            if (!reg.SetIfAbsent(id, "a", "modA"))
            {
                throw new Exception("Test failed: TestNonAlterableSetIfAbsent first SetIfAbsent should return true");
            }
            if (reg.SetIfAbsent(id, "b", "modA"))
            {
                throw new Exception("Test failed: TestNonAlterableSetIfAbsent second SetIfAbsent should return false");
            }
            if (reg.Get(id) != "a")
            {
                throw new Exception("Test failed: TestNonAlterableSetIfAbsent Get expected 'a', got " + reg.Get(id));
            }
        }

        public static void TestRegistryManagerBootDoesNotThrow()
        {
            try
            {
                var _ = RegistryManager.Instance;
                var itemID = RegistryManager.Instance.ItemID;
                if (itemID == null)
                {
                    throw new Exception("Test failed: TestRegistryManagerBootDoesNotThrow ItemID is null");
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Test failed: TestRegistryManagerBootDoesNotThrow threw: " + ex.Message);
            }
        }

        // ===== 统一调度 =====

        public static void RunAll()
        {
            var tests = new (string Name, Action Body)[]
            {
                ("TestSetGetTryGet", TestSetGetTryGet),
                ("TestRemove", TestRemove),
                ("TestClear", TestClear),
                ("TestSetWithModid", TestSetWithModid),
                ("TestGetAllByOwner", TestGetAllByOwner),
                ("TestRemoveAllByOwner", TestRemoveAllByOwner),
                ("TestRemoveAllByOwnerTriggersOnRemoved", TestRemoveAllByOwnerTriggersOnRemoved),
                ("TestReverseLookupRegistry", TestReverseLookupRegistry),
                ("TestReverseLookupRemoveAllByOwner", TestReverseLookupRemoveAllByOwner),
                ("TestEnterModScope", TestEnterModScope),
                ("TestIdentifierToString", TestIdentifierToString),
                ("TestIdentifierParseRoundTrip", TestIdentifierParseRoundTrip),
                ("TestIdentifierTryParseFail", TestIdentifierTryParseFail),
                ("TestIdentifierWithSubdirectory", TestIdentifierWithSubdirectory),
                ("TestNonAlterableSetIfAbsent", TestNonAlterableSetIfAbsent),
                ("TestRegistryManagerBootDoesNotThrow", TestRegistryManagerBootDoesNotThrow),
            };
            int passed = 0, failed = 0;
            foreach (var t in tests)
            {
                try
                {
                    t.Body();
                    Console.WriteLine("[PASS] RegisterTest." + t.Name);
                    passed++;
                }
                catch (Exception ex)
                {
                    Console.WriteLine("[FAIL] RegisterTest." + t.Name + " :: " + ex.Message);
                    failed++;
                }
            }
            Console.WriteLine("RegisterTest summary: " + passed + " passed, " + failed + " failed.");
        }
    }
}