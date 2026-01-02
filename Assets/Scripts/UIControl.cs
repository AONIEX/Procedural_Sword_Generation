using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using TMPro;


public class UIControl : MonoBehaviour
{
    public BladeGeneration bladeGen;

    public GameObject sliderRowPrefab;
    public GameObject dropdownRowPrefab;

    public Transform uiParent;

    private bool buildingUI = false;

    [Header("Auto Generation")]
    public bool autoGenerate = true;
    public float maxGenerateRate = 4f; // times per second

    private float lastGenerateTime = 0f;
    private bool pendingGenerate = false;
    public List<BladePreset> presets;
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
        foreach (Transform child in uiParent)
            Destroy(child.gameObject);

        buildingUI = true;

        if (bladeGen != null)
            GenerateForObject(bladeGen, uiParent);

        if (bladeGen != null && bladeGen.splineGen != null)
            GenerateForObject(bladeGen.splineGen, uiParent);

        buildingUI = false;
    }

    void GenerateForObject(object obj, Transform parent)
    {
        if (obj == null) return;

        FieldInfo[] fields = obj.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance);

        foreach (var field in fields)
        {
            object value = field.GetValue(obj);
            string labelName = PrettyFieldName(field.Name);

            // FLOAT / INT with [Range]
            RangeAttribute range = field.GetCustomAttribute<RangeAttribute>();
            if (range != null &&
                (field.FieldType == typeof(float) || field.FieldType == typeof(int)))
            {
                CreateSlider(obj, field, parent, labelName, range);
                continue;
            }

            // ENUM
            if (field.FieldType.IsEnum)
            {
                CreateEnumDropdown(obj, field, parent, labelName);
                continue;
            }

            // NESTED CLASS
            if (field.FieldType.IsClass &&
                field.FieldType != typeof(string) &&
                !field.FieldType.IsSubclassOf(typeof(UnityEngine.Object)))
            {
                if (value == null)
                {
                    value = Activator.CreateInstance(field.FieldType);
                    field.SetValue(obj, value);
                }

                GenerateForObject(value, parent);
                continue;
            }
        }
    }

    // -----------------------------
    // ENUM DROPDOWN
    // -----------------------------
    void CreateEnumDropdown(object obj, FieldInfo field, Transform parent, string labelName)
    {
        GameObject row = Instantiate(dropdownRowPrefab, parent);

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
                    // Update enum
                    bladeGen.splineGen.bladePreset = (BladePresets)newValue;

                    // Load preset (this replaces all settings objects)
                    bladeGen.splineGen.LoadPreset();

                    // Rebuild UI so sliders point to the NEW objects
                    RefreshUI();
                }
                return;
            }

            if (!buildingUI)
                RequestGenerate();
        });
    }
  

    // -----------------------------
    // SLIDER CREATION
    // -----------------------------
    void CreateSlider(object obj, FieldInfo field, Transform parent, string labelName, RangeAttribute range)
    {
        GameObject row = Instantiate(sliderRowPrefab, parent);

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
}