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
        // Wait 2 frames so Unity fully deserializes objects
        yield return null;
        yield return null;

        RefreshUI();
    }

    // Call this whenever values change
    public void RefreshUI()
    {
        // Clear old UI
        foreach (Transform child in uiParent)
            Destroy(child.gameObject);

        buildingUI = true;

        // Generate UI for BladeGeneration and all nested classes (including FullerSettings)
        GenerateForObject(bladeGen, uiParent, "");

        buildingUI = false;
    }

    void GenerateForObject(object obj, Transform parent, string prefix)
    {
        if (obj == null) return;

        Type type = obj.GetType();
        FieldInfo[] fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance);

        foreach (var field in fields)
        {
            object value = field.GetValue(obj);

            // Build label name
            string fieldName = field.Name;
            DisplayNameAttribute displayName = field.GetCustomAttribute<DisplayNameAttribute>();
            if (displayName != null)
                fieldName = displayName.Name;

            if (!string.IsNullOrEmpty(prefix))
                fieldName = prefix + " - " + fieldName;

            // FLOAT / INT with [Range]
            RangeAttribute range = field.GetCustomAttribute<RangeAttribute>();
            if (range != null &&
                (field.FieldType == typeof(float) || field.FieldType == typeof(int)))
            {
                CreateSlider(obj, field, parent, fieldName, range);
                continue;
            }

            // Skip bools
            if (field.FieldType == typeof(bool))
                continue;

            // Skip lists (no longer used)
            if (field.FieldType.IsGenericType &&
                field.FieldType.GetGenericTypeDefinition() == typeof(List<>))
                continue;

            // NESTED SERIALIZABLE CLASS (e.g. FullerSettings)
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
    // SLIDER CREATION
    // -----------------------------
    void CreateSlider(
        object obj,
        FieldInfo field,
        Transform parent,
        string labelName,
        RangeAttribute range)
    {
        GameObject row = Instantiate(sliderPrefab, parent);

        Slider slider = row.GetComponentInChildren<Slider>();
        TMP_Text labelTMP = row.GetComponentInChildren<TMP_Text>();
        Text labelLegacy = row.GetComponentInChildren<Text>();

        float startValue =
            field.FieldType == typeof(int)
                ? (int)field.GetValue(obj)
                : (float)field.GetValue(obj);

        slider.minValue = range.min;
        slider.maxValue = range.max;
        slider.wholeNumbers = field.FieldType == typeof(int);
        slider.value = startValue;

        UpdateLabel(labelTMP, labelLegacy, labelName, startValue, field.FieldType == typeof(int));

        slider.onValueChanged.AddListener(value =>
        {
            if (obj == null || field == null)
                return;

            if (field.FieldType == typeof(int))
                field.SetValue(obj, Mathf.RoundToInt(value));
            else
                field.SetValue(obj, value);

            if (!buildingUI)
                GenerateBlade();

            UpdateLabel(labelTMP, labelLegacy, labelName, value, field.FieldType == typeof(int));
        });
    }

    void UpdateLabel(
        TMP_Text labelTMP,
        Text labelLegacy,
        string fieldName,
        float value,
        bool isInt)
    {
        string text = isInt
            ? fieldName + ": " + Mathf.RoundToInt(value)
            : fieldName + ": " + value.ToString("F3");

        if (labelTMP != null)
            labelTMP.text = text;
        else if (labelLegacy != null)
            labelLegacy.text = text;
    }

    public void GenerateBlade()
    {
        bladeGen.Generate();
    }
}
