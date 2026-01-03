using System;
using UnityEngine;

// Add this new attribute class to your project
[AttributeUsage(AttributeTargets.Field)]
public class Vector2RangeAttribute : Attribute
{
    public float MinX { get; private set; }
    public float MaxX { get; private set; }
    public float MinY { get; private set; }
    public float MaxY { get; private set; }

    // Constructor for same range on both X and Y
    public Vector2RangeAttribute(float min, float max)
    {
        MinX = min;
        MaxX = max;
        MinY = min;
        MaxY = max;
    }

    // Constructor for different ranges on X and Y
    public Vector2RangeAttribute(float minX, float maxX, float minY, float maxY)
    {
        MinX = minX;
        MaxX = maxX;
        MinY = minY;
        MaxY = maxY;
    }
}
