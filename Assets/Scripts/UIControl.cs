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
    public GameObject sliderPrefab;
    public Transform uiParent;

    private bool buildingUI = false;

    IEnumerator Start()
    {
        // Wait 2 frames so Unity fully deserializes lists
        yield return null;
        yield return null;

        RefreshUI();
    }

    // Call this whenever profiles change
    public void RefreshUI()
    {
        // Clear old UI
        foreach (Transform child in uiParent)
            Destroy(child.gameObject);

        buildingUI = true;

        // Generate top-level BladeGeneration sliders
        GenerateForObject(bladeGen, uiParent, "");

        // Generate profile sliders manually
        for (int i = 0; i < bladeGen.profiles.Count; i++)
            CreateProfileUI(bladeGen.profiles[i], i);

        buildingUI = false;
    }

    void GenerateUI()
    {
        GenerateForObject(bladeGen, uiParent, "");
    }

    void GenerateForObject(object obj, Transform parent, string prefix)
    {
        if (obj == null) return;

        Type type = obj.GetType();
        FieldInfo[] fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance);

        foreach (var field in fields)
        {
            object value = field.GetValue(obj);

            // Build label
            string fieldName = field.Name;
            DisplayNameAttribute displayName = field.GetCustomAttribute<DisplayNameAttribute>();
            if (displayName != null)
                fieldName = displayName.Name;

            if (!string.IsNullOrEmpty(prefix))
                fieldName = prefix + " - " + fieldName;

            // FLOAT / INT with [Range]
            RangeAttribute range = field.GetCustomAttribute<RangeAttribute>();
            if (range != null && (field.FieldType == typeof(float) || field.FieldType == typeof(int)))
            {
                CreateSlider(obj, field, parent, fieldName, range);
                continue;
            }

            // Skip bools (no toggles wanted)
            if (field.FieldType == typeof(bool))
                continue;

            // Skip lists — handled manually
            if (field.FieldType.IsGenericType &&
                field.FieldType.GetGenericTypeDefinition() == typeof(List<>))
                continue;

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

                GenerateForObject(value, parent, fieldName);
                continue;
            }
        }
    }

    // -----------------------------
    // MANUAL PROFILE UI GENERATION
    // -----------------------------
    void CreateProfileUI(BladeGeneration.BladeSectionProfile profile, int index)
    {
        CreateSlider(profile,
            profile.GetType().GetField("position"),
            uiParent,
            "Profile " + index + " - Position",
            new RangeAttribute(0f, 0.95f));

        CreateSlider(profile,
            profile.GetType().GetField("fullerDepth"),
            uiParent,
            "Profile " + index + " - Fuller Depth",
            new RangeAttribute(0f, 0.9f));

        CreateSlider(profile,
            profile.GetType().GetField("fullerWidth"),
            uiParent,
            "Profile " + index + " - Fuller Width",
            new RangeAttribute(0f, 0.9f));

        CreateSlider(profile,
            profile.GetType().GetField("fullerOffset"),
            uiParent,
            "Profile " + index + " - Fuller Offset",
            new RangeAttribute(0f, 1f));

        CreateSlider(profile,
            profile.GetType().GetField("numberOfFullers"),
            uiParent,
            "Profile " + index + " - Number of Fullers",
            new RangeAttribute(1, 7));

        CreateSlider(profile,
            profile.GetType().GetField("spacingPercent"),
            uiParent,
            "Profile " + index + " - Spacing Percent",
            new RangeAttribute(0f, 1f));
    }

    // -----------------------------
    // SLIDER CREATION
    // -----------------------------
    void CreateSlider(object obj, FieldInfo field, Transform parent, string labelName, RangeAttribute range)
    {
        GameObject row = Instantiate(sliderPrefab, parent);
        Slider slider = row.GetComponentInChildren<Slider>();
        TMP_Text labelTMP = row.GetComponentInChildren<TMP_Text>();
        Text label = row.GetComponentInChildren<Text>();

        float startValue = field.FieldType == typeof(int)
            ? (int)field.GetValue(obj)
            : (float)field.GetValue(obj);

        slider.minValue = range.min;
        slider.maxValue = range.max;
        slider.wholeNumbers = field.FieldType == typeof(int);
        slider.value = startValue;

        UpdateLabel(labelTMP, label, labelName, startValue, field.FieldType == typeof(int));

        slider.onValueChanged.AddListener(value =>
        {
            if (obj == null) return;
            if (field == null) return;

            if (field.FieldType == typeof(int))
                field.SetValue(obj, Mathf.RoundToInt(value));
            else
                field.SetValue(obj, value);

            if (!buildingUI)
                GenerateBlade();

            UpdateLabel(labelTMP, label, labelName, value, field.FieldType == typeof(int));
        });
    }

    void UpdateLabel(TMP_Text labelTMP, Text label, string fieldName, float value, bool isInt)
    {
        string text = isInt
            ? fieldName + ": " + Mathf.RoundToInt(value)
            : fieldName + ": " + value.ToString("F3");

        if (labelTMP != null)
            labelTMP.text = text;
        else if (label != null)
            label.text = text;
    }

    public void GenerateBlade()
    {
        bladeGen.Generate();
    }
}

