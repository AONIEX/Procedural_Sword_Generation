using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UIControl : MonoBehaviour
{
    public BladeGeneration bladeGen;

    public GameObject sliderRowPrefab;
    public GameObject dropdownRowPrefab;
    public GameObject toggleRowPrefab;
    public GameObject curveEditorPrefab;
    public GameObject headerRowPrefab;

    public Transform uiParent;
    public TMP_Dropdown sectionDropdown;

    private bool buildingUI = false;

    [Header("Auto Generation")]
    public bool autoGenerate = true;
    public float maxGenerateRate = 4f;

    private float lastGenerateTime = 0f;
    private bool pendingGenerate = false;

    // Section -> SubSection -> UI elements
    private Dictionary<string, Dictionary<string, List<GameObject>>> sectionUIElements
        = new Dictionary<string, Dictionary<string, List<GameObject>>>();

    IEnumerator Start()
    {
        yield return null;
        yield return null;
        RefreshUI();
    }

    void Update()
    {
        if (!pendingGenerate) return;

        float interval = 1f / maxGenerateRate;
        if (Time.time - lastGenerateTime >= interval)
        {
            pendingGenerate = false;
            lastGenerateTime = Time.time;
            GenerateBlade();
        }
    }

    // ====================== UI BUILD ======================

    public void RefreshUI()
    {
        foreach (Transform child in uiParent)
            Destroy(child.gameObject);

        sectionUIElements.Clear();
        buildingUI = true;

        if (bladeGen != null)
            GenerateForObject(bladeGen);

        if (bladeGen != null && bladeGen.splineGen != null)
            GenerateForObject(bladeGen.splineGen);

        buildingUI = false;

        SetupSectionDropdown();

        if (sectionUIElements.ContainsKey("General"))
            ShowSection("General");
        else if (sectionUIElements.Count > 0)
            ShowSection(sectionUIElements.Keys.First());
    }

    void SetupSectionDropdown()
    {
        if (sectionDropdown == null) return;

        sectionDropdown.ClearOptions();
        var names = sectionUIElements.Keys
            .OrderBy(s => s == "General" ? "" : s)
            .ToList();

        sectionDropdown.AddOptions(names);
        sectionDropdown.onValueChanged.RemoveAllListeners();

        sectionDropdown.onValueChanged.AddListener(i =>
        {
            if (i >= 0 && i < names.Count)
                ShowSection(names[i]);
        });

        if (names.Count > 0)
            sectionDropdown.value = 0;
    }

  void ShowSection(string section)
  {
        // Hide everything first
        foreach (var sec in sectionUIElements.Values)
            foreach (var sub in sec.Values)
                foreach (var go in sub)
                    if (go != null) go.SetActive(false);

        if (!sectionUIElements.ContainsKey(section))
            return;

        int siblingIndex = 0;

        // IMPORTANT: deterministic subsection order
        foreach (var subPair in sectionUIElements[section]
            .OrderBy(s => s.Key)) // alphabetical, or customize
        {
            foreach (var go in subPair.Value)
            {
                if (go == null) continue;

                go.SetActive(true);
                go.transform.SetSiblingIndex(siblingIndex++);
            }
        }
    }


    // ====================== CORE GENERATION ======================

    void GenerateForObject(
        object obj,
        string currentSection = "General",
        string currentSubSection = "Basic"
    )
    {
        if (obj == null) return;

        var fields = obj.GetType()
            .GetFields(BindingFlags.Public | BindingFlags.Instance)
            .Where(f => f.GetCustomAttribute<HideInUIAttribute>() == null)
            .Select(f =>
            {
                var a = f.GetCustomAttribute<DisplayNameAttribute>();
                return (field: f, attr: a, order: a?.Order ?? 0);
            })
            .OrderBy(x => x.order)
            .ToList();

        foreach (var (field, attr, _) in fields)
        {
            object value = field.GetValue(obj);

            string section = attr?.Section ?? currentSection;
            string subSection = attr?.SubSection ?? currentSubSection;
            string label = attr?.DisplayName ?? PrettyFieldName(field.Name);

            // ---------- RANGE ----------
            RangeAttribute range = field.GetCustomAttribute<RangeAttribute>();
            if (range != null &&
                (field.FieldType == typeof(float) || field.FieldType == typeof(int)))
            {
                EnsureSubSectionExists(section, subSection);
                AddToSection(section, subSection,
                    CreateSlider(obj, field, label, range));
                continue;
            }

            // ---------- VECTOR2 ----------
            if (field.FieldType == typeof(Vector2))
            {
                EnsureSubSectionExists(section, subSection);
                CreateVector2Sliders(obj, field, label, section, subSection);
                continue;
            }

            // ---------- BOOL ----------
            if (field.FieldType == typeof(bool))
            {
                EnsureSubSectionExists(section, subSection);
                AddToSection(section, subSection,
                    CreateToggle(obj, field, label));
                continue;
            }

            // ---------- ENUM ----------
            if (field.FieldType.IsEnum)
            {
                EnsureSubSectionExists(section, subSection);
                AddToSection(section, subSection,
                    CreateEnumDropdown(obj, field, label));
                continue;
            }

            // ---------- CURVE ----------
            if (field.FieldType == typeof(AnimationCurve))
            {
                EnsureSubSectionExists(section, subSection);
                AddToSection(section, subSection,
                    CreateCurveEditor(obj, field, label));
                continue;
            }

            // ---------- LIST ----------
            if (field.FieldType.IsGenericType &&
                field.FieldType.GetGenericTypeDefinition() == typeof(List<>))
            {
                IList list = (IList)value;
                if (list == null) continue;

                EnsureSubSectionExists(section, subSection);

                for (int i = 0; i < list.Count; i++)
                {
                    GameObject listHeader =
                        CreateHeaderRow($"{label} {i + 1}");

                    AddToSection(section, subSection, listHeader);

                    GenerateForObject(list[i], section, subSection);
                }
                continue;
            }

            // ---------- NESTED CLASS ----------
            if (field.FieldType.IsClass &&
                field.FieldType != typeof(string) &&
                !field.FieldType.IsSubclassOf(typeof(UnityEngine.Object)))
            {
                if (value == null)
                {
                    value = Activator.CreateInstance(field.FieldType);
                    field.SetValue(obj, value);
                }

                GenerateForObject(value, section, subSection);
            }
        }
    }

    // ====================== SECTION MANAGEMENT ======================

    void EnsureSubSectionExists(string section, string subSection)
    {
        if (!sectionUIElements.ContainsKey(section))
            sectionUIElements[section] = new Dictionary<string, List<GameObject>>();

        var subs = sectionUIElements[section];

        if (!subs.ContainsKey(subSection))
        {
            subs[subSection] = new List<GameObject>();

            GameObject header = CreateHeaderRow(subSection);
            header.SetActive(false);
            subs[subSection].Add(header);
        }
    }

    void AddToSection(string section, string subSection, GameObject element)
    {
        element.SetActive(false);
        sectionUIElements[section][subSection].Add(element);
    }

    GameObject CreateHeaderRow(string label)
    {
        GameObject row = Instantiate(headerRowPrefab, uiParent);
        TMP_Text text = row.GetComponentInChildren<TMP_Text>();
        if (text != null) text.text = label;
        return row;
    }

    // ====================== UI ELEMENTS ======================

    GameObject CreateSlider(object obj, FieldInfo field, string label, RangeAttribute range)
    {
        GameObject row = Instantiate(sliderRowPrefab, uiParent);
        TMP_Text text = row.GetComponentInChildren<TMP_Text>();
        Slider slider = row.GetComponentInChildren<Slider>();

        float value = Convert.ToSingle(field.GetValue(obj));
        slider.minValue = range.min;
        slider.maxValue = range.max;
        slider.wholeNumbers = field.FieldType == typeof(int);
        slider.value = value;

        UpdateLabel(text, label, value, slider.wholeNumbers);

        slider.onValueChanged.AddListener(v =>
        {
            if (field.FieldType == typeof(int))
            {
                int iv = Mathf.RoundToInt(v);
                field.SetValue(obj, iv);
                UpdateLabel(text, label, iv, true);
            }
            else
            {
                field.SetValue(obj, v);
                UpdateLabel(text, label, v, false);
            }

            if (!buildingUI)
                RequestGenerate();
        });

        return row;
    }

    void CreateVector2Sliders(object obj, FieldInfo field, string label,
                          string section, string subSection)
    {
        Vector2 v = (Vector2)field.GetValue(obj);

        // ⭐ Get the attribute
        Vector2RangeAttribute range = field.GetCustomAttribute<Vector2RangeAttribute>();

        GameObject minRow = Instantiate(sliderRowPrefab, uiParent);
        GameObject maxRow = Instantiate(sliderRowPrefab, uiParent);

        Slider minSlider = minRow.GetComponentInChildren<Slider>();
        Slider maxSlider = maxRow.GetComponentInChildren<Slider>();

        TMP_Text minText = minRow.GetComponentInChildren<TMP_Text>();
        TMP_Text maxText = maxRow.GetComponentInChildren<TMP_Text>();

        // ⭐ Apply attribute min/max to sliders
        if (range != null)
        {
            minSlider.minValue = range.MinX;
            minSlider.maxValue = range.MaxX;

            maxSlider.minValue = range.MinY;
            maxSlider.maxValue = range.MaxY;
        }

        // Set initial values
        minSlider.value = v.x;
        maxSlider.value = v.y;

        UpdateLabel(minText, $"{label} Min", v.x, false);
        UpdateLabel(maxText, $"{label} Max", v.y, false);

        // Min slider logic
        minSlider.onValueChanged.AddListener(val =>
        {
            Vector2 nv = (Vector2)field.GetValue(obj);
            nv.x = val;
            field.SetValue(obj, nv);
            UpdateLabel(minText, $"{label} Min", val, false);
            if (!buildingUI) RequestGenerate();
        });

        // Max slider logic
        maxSlider.onValueChanged.AddListener(val =>
        {
            Vector2 nv = (Vector2)field.GetValue(obj);
            nv.y = val;
            field.SetValue(obj, nv);
            UpdateLabel(maxText, $"{label} Max", val, false);
            if (!buildingUI) RequestGenerate();
        });

        AddToSection(section, subSection, minRow);
        AddToSection(section, subSection, maxRow);
    }
    GameObject CreateToggle(object obj, FieldInfo field, string label)
    {
        GameObject row = Instantiate(toggleRowPrefab, uiParent);
        TMP_Text text = row.GetComponentInChildren<TMP_Text>();
        Toggle toggle = row.GetComponentInChildren<Toggle>();

        if (text != null) text.text = label;
        toggle.isOn = (bool)field.GetValue(obj);

        toggle.onValueChanged.AddListener(v =>
        {
            field.SetValue(obj, v);
            if (!buildingUI) RequestGenerate();
        });

        return row;
    }

    GameObject CreateEnumDropdown(object obj, FieldInfo field, string label)
    {
        GameObject row = Instantiate(dropdownRowPrefab, uiParent);
        TMP_Text text = row.GetComponentInChildren<TMP_Text>();
        TMP_Dropdown dd = row.GetComponentInChildren<TMP_Dropdown>();

        if (text != null) text.text = label;

        dd.ClearOptions();
        dd.AddOptions(Enum.GetNames(field.FieldType).ToList());
        dd.value = Array.IndexOf(Enum.GetValues(field.FieldType), field.GetValue(obj));

        dd.onValueChanged.AddListener(i =>
        {
            field.SetValue(obj, Enum.GetValues(field.FieldType).GetValue(i));
            if (!buildingUI) RequestGenerate();
        });

        return row;
    }

    GameObject CreateCurveEditor(object obj, FieldInfo field, string label)
    {
        GameObject row = Instantiate(curveEditorPrefab, uiParent);
        RuntimeCurveEditor editor = row.GetComponent<RuntimeCurveEditor>();

        AnimationCurve curve = (AnimationCurve)field.GetValue(obj)
            ?? AnimationCurve.Linear(0, 0, 1, 1);

        editor.Initialize(curve, label);
        editor.onCurveChanged = c =>
        {
            field.SetValue(obj, c);
            if (!buildingUI) RequestGenerate();
        };

        return row;
    }

    // ====================== UTILS ======================

    void UpdateLabel(TMP_Text t, string name, float v, bool isInt)
    {
        if (t == null) return;
        t.text = isInt ? $"{name}: {(int)v}" : $"{name}: {v:F3}";
    }

    string PrettyFieldName(string raw)
    {
        return System.Text.RegularExpressions.Regex
            .Replace(raw, "(\\B[A-Z])", " $1");
    }

    public void GenerateBlade() => bladeGen.Generate();

    void RequestGenerate()
    {
        if (autoGenerate) pendingGenerate = true;
    }
}
