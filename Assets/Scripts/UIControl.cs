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
    public TMP_Dropdown sectionDropdown; // Dropdown to switch between sections

    private bool buildingUI = false;

    [Header("Auto Generation")]
    public bool autoGenerate = true;
    public float maxGenerateRate = 4f;

    private float lastGenerateTime = 0f;
    private bool pendingGenerate = false;
    public List<BladePreset> presets;

    // Store all sections and their UI elements
    private Dictionary<string, List<GameObject>> sectionUIElements = new Dictionary<string, List<GameObject>>();
    private string currentSection = "";

    IEnumerator Start()
    {
        yield return null;
        yield return null;

        RefreshUI();
    }

    void Update()
    {
        if (!pendingGenerate)
            return;

        float interval = 1f / maxGenerateRate;

        if (Time.time - lastGenerateTime >= interval)
        {
            pendingGenerate = false;
            lastGenerateTime = Time.time;
            GenerateBlade();
        }
    }

    public void RefreshUI()
    {
        // Clear existing UI
        foreach (Transform child in uiParent)
            Destroy(child.gameObject);

        sectionUIElements.Clear();

        buildingUI = true;

        // Generate all UI elements organized by section
        if (bladeGen != null)
            GenerateForObject(bladeGen);

        if (bladeGen != null && bladeGen.splineGen != null)
            GenerateForObject(bladeGen.splineGen);

        buildingUI = false;

        // Setup section dropdown
        SetupSectionDropdown();

        // Explicitly show General section after everything is set up
        if (sectionUIElements.ContainsKey("General"))
        {
            ShowSection("General");
        }
        else if (sectionUIElements.Count > 0)
        {
            ShowSection(sectionUIElements.Keys.First());
        }
    }

    void SetupSectionDropdown()
    {
        if (sectionDropdown == null) return;

        sectionDropdown.ClearOptions();

        List<string> sectionNames = sectionUIElements.Keys.OrderBy(s => s == "General" ? "" : s).ToList();
        sectionDropdown.AddOptions(sectionNames);

        sectionDropdown.onValueChanged.RemoveAllListeners();
        sectionDropdown.onValueChanged.AddListener(index =>
        {
            if (index >= 0 && index < sectionNames.Count)
            {
                ShowSection(sectionNames[index]);
            }
        });

        // Set to first section
        if (sectionNames.Count > 0)
        {
            sectionDropdown.value = 0;
        }
    }

    void ShowSection(string sectionName)
    {
        currentSection = sectionName;

        Debug.Log($"ShowSection called for: {sectionName}");

        // Hide all UI elements
        foreach (var kvp in sectionUIElements)
        {
            foreach (var element in kvp.Value)
            {
                if (element != null)
                    element.SetActive(false);
            }
        }

        // Show only the selected section
        if (sectionUIElements.ContainsKey(sectionName))
        {
            Debug.Log($"Found section {sectionName} with {sectionUIElements[sectionName].Count} elements");
            foreach (var element in sectionUIElements[sectionName])
            {
                if (element != null)
                {
                    element.SetActive(true);
                    Debug.Log($"Activated element: {element.name}");
                }
            }
        }
        else
        {
            Debug.LogWarning($"Section {sectionName} not found in sectionUIElements!");
        }
    }

    void GenerateForObject(object obj)
    {
        if (obj == null) return;

        FieldInfo[] fields = obj.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance);

        var nonEnumFields = new List<(FieldInfo field, DisplayNameAttribute attr, int order)>();
        var enumFields = new List<(FieldInfo field, DisplayNameAttribute attr, int order)>();
        var curveFields = new List<(FieldInfo field, DisplayNameAttribute attr, int order)>();

        foreach (var field in fields)
        {
            if (field.GetCustomAttribute<HideInUIAttribute>() != null)
                continue;

            DisplayNameAttribute displayAttr = field.GetCustomAttribute<DisplayNameAttribute>();
            int order = displayAttr?.Order ?? 0;

            if (field.FieldType.IsEnum)
                enumFields.Add((field, displayAttr, order));
            else if (field.FieldType == typeof(AnimationCurve))
                curveFields.Add((field, displayAttr, order));
            else
                nonEnumFields.Add((field, displayAttr, order));
        }

        nonEnumFields = nonEnumFields.OrderBy(x => x.order).ToList();
        enumFields = enumFields.OrderBy(x => x.order).ToList();
        curveFields = curveFields.OrderBy(x => x.order).ToList();

        foreach (var (field, displayAttr, order) in nonEnumFields)
        {
            object value = field.GetValue(obj);
            string section = displayAttr?.Section ?? "General";
            string labelName = displayAttr?.DisplayName ?? PrettyFieldName(field.Name);

            // FLOAT / INT
            RangeAttribute range = field.GetCustomAttribute<RangeAttribute>();
            if (range != null &&
                (field.FieldType == typeof(float) || field.FieldType == typeof(int)))
            {
                GameObject slider = CreateSlider(obj, field, labelName, range);
                AddToSection(section, slider);
                continue;
            }

            // VECTOR2
            if (field.FieldType == typeof(Vector2))
            {
                CreateVector2Sliders(obj, field, labelName, section);
                continue;
            }

            // BOOL
            if (field.FieldType == typeof(bool))
            {
                GameObject toggle = CreateToggle(obj, field, labelName);
                AddToSection(section, toggle);
                continue;
            }

            // LIST OF CLASSES → expand each element
            if (field.FieldType.IsGenericType &&
                field.FieldType.GetGenericTypeDefinition() == typeof(List<>))
            {
                Type elementType = field.FieldType.GetGenericArguments()[0];

                if (elementType.IsClass && elementType != typeof(string))
                {
                    IList list = (IList)field.GetValue(obj);

                    string sectionName = displayAttr?.Section ?? "General";
                    string listTitle = displayAttr?.DisplayName ?? PrettyFieldName(field.Name);


                    // Generate UI for each element
                    for (int i = 0; i < list.Count; i++)
                    {
                        object element = list[i];

                        // ✔ Create only the per‑element header
                        GameObject layerHeader = CreateHeaderRow($"{listTitle} {i + 1}");
                        AddToSection(sectionName, layerHeader);

                        // ✔ Generate UI for the element
                        GenerateForObject(element);
                    }

                    continue;
                }
            }
            // NESTED CLASS (must come AFTER list)
            if (field.FieldType.IsClass &&
              field.FieldType != typeof(string) &&
              !field.FieldType.IsSubclassOf(typeof(UnityEngine.Object)) &&
              !field.FieldType.IsGenericType)   // ← prevents recursion into List<T>
            {
                if (value == null)
                {
                    value = Activator.CreateInstance(field.FieldType);
                    field.SetValue(obj, value);
                }

                GenerateForObject(value);
                continue;
            }
        }

        // ENUMS
        foreach (var (field, displayAttr, order) in enumFields)
        {
            string section = displayAttr?.Section ?? "General";
            string labelName = displayAttr?.DisplayName ?? PrettyFieldName(field.Name);

            GameObject dropdown = CreateEnumDropdown(obj, field, labelName);
            AddToSection(section, dropdown);
        }

        // CURVES
        foreach (var (field, displayAttr, order) in curveFields)
        {
            string section = displayAttr?.Section ?? "General";
            string labelName = displayAttr?.DisplayName ?? PrettyFieldName(field.Name);

            GameObject curveEditor = CreateCurveEditor(obj, field, labelName);
            AddToSection(section, curveEditor);
        }
    }

    void AddToSection(string sectionName, GameObject uiElement)
    {
        if (!sectionUIElements.ContainsKey(sectionName))
        {
            sectionUIElements[sectionName] = new List<GameObject>();
        }

        sectionUIElements[sectionName].Add(uiElement);

        // Hide by default - will be shown when section is selected
        uiElement.SetActive(false);
    }

    GameObject CreateEnumDropdown(object obj, FieldInfo field, string labelName)
    {
        GameObject row = Instantiate(dropdownRowPrefab, uiParent);

        TMP_Text[] texts = row.GetComponentsInChildren<TMP_Text>();

        TMP_Text titleText = null;
        TMP_Text valueLabel = null;

        foreach (var t in texts)
        {
            if (t.gameObject.name.ToLower().Contains("title"))
                titleText = t;
            else
                valueLabel = t;
        }

        if (titleText == null)
            titleText = texts.Length > 0 ? texts[0] : row.AddComponent<TextMeshProUGUI>();

        if (valueLabel == null)
            valueLabel = texts.Length > 1 ? texts[1] : row.AddComponent<TextMeshProUGUI>();

        titleText.text = labelName;

        TMP_Dropdown dropdown = row.GetComponentInChildren<TMP_Dropdown>();
        dropdown.ClearOptions();
        dropdown.AddOptions(new List<string>(Enum.GetNames(field.FieldType)));

        object currentValue = field.GetValue(obj);
        int index = Array.IndexOf(Enum.GetValues(field.FieldType), currentValue);
        dropdown.value = index;

        dropdown.onValueChanged.AddListener(i =>
        {
            Array values = Enum.GetValues(field.FieldType);
            object newValue = values.GetValue(i);
            field.SetValue(obj, newValue);

            if (field.Name == "bladePreset")
            {
                if (bladeGen != null && bladeGen.splineGen != null)
                {
                    bladeGen.splineGen.bladePreset = (BladePresets)newValue;
                    bladeGen.splineGen.LoadPreset();
                    RefreshUI();
                }
                return;
            }

            if (!buildingUI)
                RequestGenerate();
        });

        return row;
    }

    GameObject CreateSlider(object obj, FieldInfo field, string labelName, RangeAttribute range)
    {
        GameObject row = Instantiate(sliderRowPrefab, uiParent);

        TMP_Text labelTMP = row.GetComponentInChildren<TMP_Text>();
        Slider slider = row.GetComponentInChildren<Slider>();

        float startValue =
            field.FieldType == typeof(int)
                ? (int)field.GetValue(obj)
                : (float)field.GetValue(obj);

        slider.minValue = range.min;
        slider.maxValue = range.max;
        slider.wholeNumbers = field.FieldType == typeof(int);
        slider.value = startValue;

        UpdateLabel(labelTMP, labelName, startValue, field.FieldType == typeof(int));

        slider.onValueChanged.AddListener(value =>
        {
            if (field.FieldType == typeof(int))
                field.SetValue(obj, Mathf.RoundToInt(value));
            else
                field.SetValue(obj, value);

            if (!buildingUI)
                RequestGenerate();

            UpdateLabel(labelTMP, labelName, value, field.FieldType == typeof(int));
        });

        return row;
    }

    void CreateVector2Sliders(object obj, FieldInfo field, string labelName, string section)
    {
        Vector2 currentValue = (Vector2)field.GetValue(obj);

        // Get Vector2Range attribute if it exists, otherwise use defaults
        Vector2RangeAttribute v2Range = field.GetCustomAttribute<Vector2RangeAttribute>();
        float minX = v2Range?.MinX ?? 0f;
        float maxX = v2Range?.MaxX ?? 2f;
        float minY = v2Range?.MinY ?? 0f;
        float maxY = v2Range?.MaxY ?? 2f;

        // Create X slider (Min)
        GameObject xRow = Instantiate(sliderRowPrefab, uiParent);
        TMP_Text xLabel = xRow.GetComponentInChildren<TMP_Text>();
        Slider xSlider = xRow.GetComponentInChildren<Slider>();

        xSlider.minValue = minX;
        xSlider.maxValue = maxX;
        xSlider.value = currentValue.x;
        UpdateLabel(xLabel, $"{labelName} (Min)", currentValue.x, false);

        xSlider.onValueChanged.AddListener(value =>
        {
            Vector2 vec = (Vector2)field.GetValue(obj);
            vec.x = value;
            field.SetValue(obj, vec);

            if (!buildingUI)
                RequestGenerate();

            UpdateLabel(xLabel, $"{labelName} (Min)", value, false);
        });

        AddToSection(section, xRow);

        // Create Y slider (Max)
        GameObject yRow = Instantiate(sliderRowPrefab, uiParent);
        TMP_Text yLabel = yRow.GetComponentInChildren<TMP_Text>();
        Slider ySlider = yRow.GetComponentInChildren<Slider>();

        ySlider.minValue = minY;
        ySlider.maxValue = maxY;
        ySlider.value = currentValue.y;
        UpdateLabel(yLabel, $"{labelName} (Max)", currentValue.y, false);

        ySlider.onValueChanged.AddListener(value =>
        {
            Vector2 vec = (Vector2)field.GetValue(obj);
            vec.y = value;
            field.SetValue(obj, vec);

            if (!buildingUI)
                RequestGenerate();

            UpdateLabel(yLabel, $"{labelName} (Max)", value, false);
        });

        AddToSection(section, yRow);
    }

    GameObject CreateToggle(object obj, FieldInfo field, string labelName)
    {
        GameObject row = Instantiate(toggleRowPrefab, uiParent);

        TMP_Text label = row.GetComponentInChildren<TMP_Text>();
        Toggle toggle = row.GetComponentInChildren<Toggle>();

        if (label != null)
            label.text = labelName;

        bool currentValue = (bool)field.GetValue(obj);
        toggle.isOn = currentValue;

        toggle.onValueChanged.AddListener(value =>
        {
            field.SetValue(obj, value);

            if (!buildingUI)
                RequestGenerate();
        });

        return row;
    }

    GameObject CreateCurveEditor(object obj, FieldInfo field, string labelName)
    {
        GameObject row = Instantiate(curveEditorPrefab, uiParent);

        RuntimeCurveEditor editor = row.GetComponent<RuntimeCurveEditor>();
        if (editor == null)
            editor = row.AddComponent<RuntimeCurveEditor>();

        AnimationCurve currentCurve = (AnimationCurve)field.GetValue(obj);
        if (currentCurve == null)
        {
            currentCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
            field.SetValue(obj, currentCurve);
        }

        editor.Initialize(currentCurve, labelName);

        editor.onCurveChanged = (newCurve) =>
        {
            field.SetValue(obj, newCurve);

            if (!buildingUI)
                RequestGenerate();
        };

        return row;
    }

    void UpdateLabel(TMP_Text labelTMP, string fieldName, float value, bool isInt)
    {
        string text = isInt
            ? $"{fieldName}: {Mathf.RoundToInt(value)}"
            : $"{fieldName}: {value:F3}";

        labelTMP.text = text;
    }

    string PrettyFieldName(string raw)
    {
        string withSpaces = raw.Replace("_", " ");

        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        for (int i = 0; i < withSpaces.Length; i++)
        {
            char c = withSpaces[i];
            if (i > 0 && char.IsUpper(c) && withSpaces[i - 1] != ' ')
                sb.Append(' ');
            sb.Append(c);
        }

        return sb.ToString();
    }

    public void GenerateBlade()
    {
        bladeGen.Generate();
    }

    public void ToggleAutoGenerate(bool state)
    {
        autoGenerate = state;
    }

    void RequestGenerate()
    {
        if (!autoGenerate)
            return;

        pendingGenerate = true;
    }
    GameObject CreateHeaderRow(string label)
    {
        GameObject row = Instantiate(headerRowPrefab, uiParent);
        TMP_Text text = row.GetComponentInChildren<TMP_Text>();
        text.text = label;
        return row;
    }


}