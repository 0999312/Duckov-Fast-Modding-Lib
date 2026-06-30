using Duckov.Options;
using System;
using UnityEngine;
using UnityEngine.UI;

namespace FastModdingLib.Options
{
    /// <summary>
    /// 用 builder 模式逐行向设置面板添加 UI 元素。
    /// 每行自动使用 label + control 水平布局，持久化走 <see cref="OptionsManager"/>。
    /// </summary>
    public class ModOptionsBuilder
    {
        private readonly RectTransform _parent;
        private readonly string _modId;
        private readonly string _title;

        internal ModOptionsBuilder(RectTransform parent, string modId, string displayName)
        {
            _parent = parent;
            _modId = modId;
            _title = displayName;

            // 标题行
            var titleGo = new GameObject("ModOptions_Title");
            titleGo.transform.SetParent(parent, false);
            var titleText = titleGo.AddComponent<Text>();
            titleText.text = displayName;
            titleText.fontSize = 18;
            titleText.fontStyle = FontStyle.Bold;
            titleText.color = Color.white;
            titleText.alignment = TextAnchor.MiddleLeft;
            var titleLayout = titleGo.AddComponent<LayoutElement>();
            titleLayout.minHeight = 30;
        }

        private string PrefixKey(string key) => $"{_modId}_{key}";

        private GameObject MakeRow(string labelText, out GameObject labelGo, out GameObject controlGo)
        {
            // 水平行容器
            var row = new GameObject("ModOptions_Row");
            row.transform.SetParent(_parent, false);
            var rowLayout = row.AddComponent<HorizontalLayoutGroup>();
            rowLayout.padding = new RectOffset(0, 0, 2, 2);
            rowLayout.spacing = 10;
            rowLayout.childAlignment = TextAnchor.MiddleLeft;
            rowLayout.childForceExpandWidth = false;

            var rowElement = row.AddComponent<LayoutElement>();
            rowElement.minHeight = 28;
            rowElement.flexibleWidth = 1;

            // Label
            labelGo = new GameObject("ModOptions_Label");
            labelGo.transform.SetParent(row.transform, false);
            var labelTextComp = labelGo.AddComponent<Text>();
            labelTextComp.text = labelText;
            labelTextComp.fontSize = 14;
            labelTextComp.color = Color.white;
            labelTextComp.alignment = TextAnchor.MiddleLeft;
            var labelLayout = labelGo.AddComponent<LayoutElement>();
            labelLayout.minWidth = 150;
            labelLayout.flexibleWidth = 0.6f;

            // Control 容器
            controlGo = new GameObject("ModOptions_Control");
            controlGo.transform.SetParent(row.transform, false);
            var controlLayout = controlGo.AddComponent<LayoutElement>();
            controlLayout.flexibleWidth = 0.4f;

            return row;
        }

        /// <summary>添加 Toggle 开关（true/false），自动 OptionsManager 持久化。</summary>
        public void AddToggle(string key, bool defaultValue, string label)
        {
            string saveKey = PrefixKey(key);
            bool savedValue = OptionsManager.Load(saveKey, defaultValue);

            var row = MakeRow(label, out _, out var controlGo);

            var toggleGo = new GameObject("ModOptions_Toggle");
            toggleGo.transform.SetParent(controlGo.transform, false);
            var toggle = toggleGo.AddComponent<Toggle>();

            // Toggle 背景
            var bg = new GameObject("Background");
            bg.transform.SetParent(toggleGo.transform, false);
            var bgImage = bg.AddComponent<Image>();
            bgImage.color = Color.gray;
            var bgLayout = bg.AddComponent<LayoutElement>();
            bgLayout.minWidth = 40;
            bgLayout.minHeight = 20;

            // Toggle 勾选
            var checkmark = new GameObject("Checkmark");
            checkmark.transform.SetParent(bg.transform, false);
            var checkImage = checkmark.AddComponent<Image>();
            checkImage.color = Color.white;
            var checkRect = checkmark.AddComponent<RectTransform>();
            checkRect.anchoredPosition = Vector2.zero;
            checkRect.sizeDelta = Vector2.one * 16;

            toggle.targetGraphic = bgImage;
            toggle.graphic = checkImage;
            toggle.isOn = savedValue;
            toggle.onValueChanged.AddListener(val =>
            {
                OptionsManager.Save(saveKey, val);
            });
        }

        /// <summary>添加 Slider 滑块，自动 OptionsManager 持久化。</summary>
        public void AddSlider(string key, float defaultValue, float min, float max, string label)
        {
            string saveKey = PrefixKey(key);
            float savedValue = OptionsManager.Load(saveKey, defaultValue);

            var row = MakeRow(label, out _, out var controlGo);

            var sliderGo = new GameObject("ModOptions_Slider");
            sliderGo.transform.SetParent(controlGo.transform, false);
            var slider = sliderGo.AddComponent<Slider>();
            slider.minValue = min;
            slider.maxValue = max;
            slider.wholeNumbers = false;
            slider.value = Mathf.Clamp(savedValue, min, max);

            // Slider background
            var bg = new GameObject("Background");
            bg.transform.SetParent(sliderGo.transform, false);
            var bgImage = bg.AddComponent<Image>();
            bgImage.color = Color.gray;
            var bgRect = bg.AddComponent<RectTransform>();
            bgRect.sizeDelta = new Vector2(100, 16);

            // Slider fill area
            var fillArea = new GameObject("FillArea");
            fillArea.transform.SetParent(sliderGo.transform, false);
            var fill = new GameObject("Fill");
            fill.transform.SetParent(fillArea.transform, false);
            var fillImage = fill.AddComponent<Image>();
            fillImage.color = Color.white;

            slider.targetGraphic = bgImage;
            slider.fillRect = fill.GetComponent<RectTransform>();
            slider.onValueChanged.AddListener(val =>
            {
                OptionsManager.Save(saveKey, val);
            });
        }

        /// <summary>添加下拉菜单，自动 OptionsManager 持久化。</summary>
        public void AddDropdown(string key, string[] options, int defaultIndex, string label)
        {
            string saveKey = PrefixKey(key);
            int savedIndex = OptionsManager.Load(saveKey, defaultIndex);
            savedIndex = Mathf.Clamp(savedIndex, 0, options.Length - 1);

            var row = MakeRow(label, out _, out var controlGo);

            var dropdownGo = new GameObject("ModOptions_Dropdown");
            dropdownGo.transform.SetParent(controlGo.transform, false);
            var dropdown = dropdownGo.AddComponent<Dropdown>();
            dropdown.ClearOptions();
            foreach (var opt in options) dropdown.options.Add(new Dropdown.OptionData(opt));
            dropdown.value = savedIndex;
            dropdown.onValueChanged.AddListener(idx =>
            {
                OptionsManager.Save(saveKey, idx);
            });

            var dropdownLayout = dropdownGo.AddComponent<LayoutElement>();
            dropdownLayout.minWidth = 120;
            dropdownLayout.minHeight = 24;
        }

        /// <summary>添加按钮。</summary>
        public void AddButton(string label, Action onClick)
        {
            var row = MakeRow("", out _, out var controlGo);

            var btnGo = new GameObject("ModOptions_Button");
            btnGo.transform.SetParent(controlGo.transform, false);
            var btn = btnGo.AddComponent<Button>();
            var btnText = btnGo.AddComponent<Text>();
            btnText.text = label;
            btnText.fontSize = 14;
            btnText.color = Color.white;
            btnText.alignment = TextAnchor.MiddleCenter;

            var btnImage = btnGo.AddComponent<Image>();
            btnImage.color = new Color(0.3f, 0.3f, 0.3f, 1f);
            btn.targetGraphic = btnImage;
            btn.onClick.AddListener(() => onClick());

            var btnLayout = btnGo.AddComponent<LayoutElement>();
            btnLayout.minWidth = 80;
            btnLayout.minHeight = 28;
        }
    }
}
