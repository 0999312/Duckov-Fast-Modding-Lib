using Duckov.CustomOptions;
using Duckov.Options;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace FastModdingLib.Options
{
    /// <summary>
    /// Mod 设置面板注册表。监听 <see cref="CustomOptionsPanel.OnPanelEnabled"/>，
    /// 当游戏设置界面打开 CustomOptions 标签页时，向 RectTransform 填充 mod 注册的 UI 元素。
    /// 持久化走 <see cref="OptionsManager"/>（ES3）。
    /// 支持按 mod 批量卸载面板，避免 mod 重载时面板残留。
    /// </summary>
    public static class ModOptionsRegistry
    {
        private static readonly List<RegisteredPanel> _panels = new List<RegisteredPanel>();
        private static bool _hooked;

        private struct RegisteredPanel
        {
            public string ModId;
            public string DisplayName;
            public Action<ModOptionsBuilder> Build;
        }

        /// <summary>
        /// 注册一个 mod 设置面板。
        /// </summary>
        /// <param name="modId">mod 标识符（用于 OptionsManager key prefix）。</param>
        /// <param name="displayName">面板显示标题（可用 i18n）。</param>
        /// <param name="build">用 builder 填充 UI 元素的回调。</param>
        public static void RegisterPanel(string modId, string displayName, Action<ModOptionsBuilder> build)
        {
            EnsureHook();
            _panels.Add(new RegisteredPanel { ModId = modId, DisplayName = displayName, Build = build });
        }

        /// <summary>
        /// 移除指定 mod 注册的全部设置面板。
        /// 若所有面板均已移除则自动解除对 CustomOptionsPanel 的订阅。
        /// </summary>
        public static void UnregisterPanel(string modId)
        {
            _panels.RemoveAll(p => p.ModId == modId);
            if (_panels.Count == 0)
            {
                TearDownHook();
            }
        }

        /// <summary>
        /// 移除全部面板并解除事件订阅。通常在 mod 卸载时调用。
        /// </summary>
        public static void UnregisterAllPanels()
        {
            _panels.Clear();
            TearDownHook();
        }

        private static void EnsureHook()
        {
            if (_hooked) return;
            _hooked = true;
            CustomOptionsPanel.OnPanelEnabled += OnPanelEnabled;
        }

        private static void TearDownHook()
        {
            if (!_hooked) return;
            _hooked = false;
            CustomOptionsPanel.OnPanelEnabled -= OnPanelEnabled;
        }

        private static void OnPanelEnabled(RectTransform parent)
        {
            // 清除之前的内容（防止重复注册时残留）
            foreach (Transform child in parent)
            {
                GameObject.Destroy(child.gameObject);
            }

            foreach (var panel in _panels)
            {
                var builder = new ModOptionsBuilder(parent, panel.ModId, panel.DisplayName);
                try
                {
                    panel.Build(builder);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[ModOptionsRegistry] Panel '{panel.ModId}' build threw: {e}");
                }
            }
        }
    }
}
