using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections.Generic;

public class RuntimeCurveEditor : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler
{
    [Header("References")]
    public RawImage curveDisplay;
    public TMP_Text labelText;

    [Header("Settings")]
    public int textureWidth = 256;
    public int textureHeight = 128;
    public Color curveColor = new Color(0.4f, 0.8f, 1f, 1f); // Bright cyan/blue
    public Color backgroundColor = new Color(0.08f, 0.05f, 0.15f, 1f); // Deep purple/black
    public Color gridColor = new Color(0.25f, 0.15f, 0.35f, 1f); // Purple tint
    public Color keyframeColor = new Color(1f, 0.85f, 0.3f, 1f); // Golden yellow
    public Color selectedKeyframeColor = new Color(1f, 0.3f, 0.5f, 1f); // Pink/magenta
    public float keyframeSize = 10f;

    private AnimationCurve curve;
    private Texture2D curveTexture;
    private int selectedKeyIndex = -1;
    private bool isDragging = false;

    public System.Action<AnimationCurve> onCurveChanged;

    void Awake()
    {
        if (curveDisplay == null)
            curveDisplay = GetComponentInChildren<RawImage>();

        if (labelText == null)
            labelText = GetComponentInChildren<TMP_Text>();
    }

    public void Initialize(AnimationCurve animCurve, string label)
    {
        if (curveDisplay == null)
            curveDisplay = GetComponentInChildren<RawImage>();

        if (labelText == null)
            labelText = GetComponentInChildren<TMP_Text>();

        // Create texture if not already created
        if (curveTexture == null)
        {
            curveTexture = new Texture2D(textureWidth, textureHeight);
            curveTexture.filterMode = FilterMode.Point;
            if (curveDisplay != null)
                curveDisplay.texture = curveTexture;
        }

        // Copy the curve properly
        if (animCurve != null && animCurve.keys.Length > 0)
        {
            curve = new AnimationCurve(animCurve.keys);
        }
        else
        {
            // Default curve if none provided
            curve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        }

        if (labelText != null)
            labelText.text = label;

        RedrawCurve();
    }

    public AnimationCurve GetCurve()
    {
        return curve;
    }

    void RedrawCurve()
    {
        if (curveTexture == null || curve == null) return;

        // Clear texture
        Color[] pixels = new Color[textureWidth * textureHeight];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = backgroundColor;

        // Draw grid
        DrawGrid(pixels);

        // Draw curve with glow
        for (int x = 0; x < textureWidth; x++)
        {
            float t = x / (float)(textureWidth - 1);
            float value = Mathf.Clamp01(curve.Evaluate(t));

            int y = Mathf.RoundToInt(value * (textureHeight - 1));
            y = Mathf.Clamp(y, 0, textureHeight - 1);

            // Draw glow behind curve
            for (int dy = -3; dy <= 3; dy++)
            {
                int py = y + dy;
                if (py >= 0 && py < textureHeight)
                {
                    float alpha = 1f - (Mathf.Abs(dy) / 4f);
                    Color glowColor = curveColor * alpha * 0.3f;
                    pixels[py * textureWidth + x] = Color.Lerp(pixels[py * textureWidth + x], glowColor, alpha * 0.5f);
                }
            }

            // Draw main curve line (thicker)
            for (int dy = -1; dy <= 1; dy++)
            {
                int py = y + dy;
                if (py >= 0 && py < textureHeight)
                    pixels[py * textureWidth + x] = curveColor;
            }
        }

        // Draw keyframes
        if (curve.keys != null)
        {
            for (int i = 0; i < curve.keys.Length; i++)
            {
                var key = curve.keys[i];
                int x = Mathf.RoundToInt(Mathf.Clamp01(key.time) * (textureWidth - 1));
                int y = Mathf.RoundToInt(Mathf.Clamp01(key.value) * (textureHeight - 1));

                Color keyColor = (i == selectedKeyIndex) ? selectedKeyframeColor : keyframeColor;
                DrawKeyframe(pixels, x, y, keyColor);
            }
        }

        curveTexture.SetPixels(pixels);
        curveTexture.Apply();
    }

    void DrawGrid(Color[] pixels)
    {
        // Vertical lines
        for (int x = 0; x < textureWidth; x += textureWidth / 4)
        {
            for (int y = 0; y < textureHeight; y++)
            {
                pixels[y * textureWidth + x] = gridColor;
            }
        }

        // Horizontal lines
        for (int y = 0; y < textureHeight; y += textureHeight / 4)
        {
            for (int x = 0; x < textureWidth; x++)
            {
                pixels[y * textureWidth + x] = gridColor;
            }
        }
    }

    void DrawKeyframe(Color[] pixels, int cx, int cy, Color color)
    {
        int radius = Mathf.RoundToInt(keyframeSize / 2f);

        // Draw outer glow
        for (int y = -radius - 2; y <= radius + 2; y++)
        {
            for (int x = -radius - 2; x <= radius + 2; x++)
            {
                float dist = Mathf.Sqrt(x * x + y * y);
                if (dist > radius && dist <= radius + 2)
                {
                    int px = cx + x;
                    int py = cy + y;

                    if (px >= 0 && px < textureWidth && py >= 0 && py < textureHeight)
                    {
                        float alpha = 1f - ((dist - radius) / 2f);
                        Color glowColor = color * alpha;
                        pixels[py * textureWidth + px] = Color.Lerp(pixels[py * textureWidth + px], glowColor, alpha * 0.5f);
                    }
                }
            }
        }

        // Draw main keyframe circle
        for (int y = -radius; y <= radius; y++)
        {
            for (int x = -radius; x <= radius; x++)
            {
                if (x * x + y * y <= radius * radius)
                {
                    int px = cx + x;
                    int py = cy + y;

                    if (px >= 0 && px < textureWidth && py >= 0 && py < textureHeight)
                    {
                        pixels[py * textureWidth + px] = color;
                    }
                }
            }
        }

        // Draw center highlight
        int highlightRadius = Mathf.Max(1, radius / 3);
        Color highlightColor = Color.Lerp(color, Color.white, 0.6f);
        for (int y = -highlightRadius; y <= highlightRadius; y++)
        {
            for (int x = -highlightRadius; x <= highlightRadius; x++)
            {
                if (x * x + y * y <= highlightRadius * highlightRadius)
                {
                    int px = cx + x - highlightRadius / 2;
                    int py = cy + y + highlightRadius / 2;

                    if (px >= 0 && px < textureWidth && py >= 0 && py < textureHeight)
                    {
                        pixels[py * textureWidth + px] = highlightColor;
                    }
                }
            }
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        Vector2 localPoint;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            curveDisplay.rectTransform, eventData.position, eventData.pressEventCamera, out localPoint);

        Vector2 normalizedPoint = GetNormalizedPoint(localPoint);

        // Check if clicking near existing keyframe
        int nearestKey = GetNearestKeyframe(normalizedPoint, 0.05f);

        // Right-click: Delete keyframe
        if (eventData.button == PointerEventData.InputButton.Right)
        {
            if (nearestKey >= 0 && curve.keys.Length > 2)
            {
                curve.RemoveKey(nearestKey);
                selectedKeyIndex = -1;
                RedrawCurve();
                onCurveChanged?.Invoke(curve);
                Debug.Log($"Deleted keyframe. Remaining keyframes: {curve.keys.Length}");
            }
            else if (nearestKey >= 0 && curve.keys.Length <= 2)
            {
                Debug.Log("Cannot delete - curve must have at least 2 keyframes");
            }
            return; // Don't proceed with left-click logic
        }

        // Left-click: Select or add keyframe
        if (eventData.button == PointerEventData.InputButton.Left)
        {
            if (nearestKey >= 0)
            {
                // Selected existing keyframe
                selectedKeyIndex = nearestKey;
                isDragging = true;
                RedrawCurve();
            }
            else
            {
                // Add new keyframe
                Keyframe newKey = new Keyframe(normalizedPoint.x, normalizedPoint.y);
                selectedKeyIndex = curve.AddKey(newKey);
                isDragging = true;
                RedrawCurve();
                onCurveChanged?.Invoke(curve);
                Debug.Log($"Added keyframe at ({normalizedPoint.x:F2}, {normalizedPoint.y:F2})");
            }
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isDragging || selectedKeyIndex < 0) return;

        Vector2 localPoint;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            curveDisplay.rectTransform, eventData.position, eventData.pressEventCamera, out localPoint);

        Vector2 normalizedPoint = GetNormalizedPoint(localPoint);
        normalizedPoint.x = Mathf.Clamp01(normalizedPoint.x);
        normalizedPoint.y = Mathf.Clamp01(normalizedPoint.y);

        Keyframe key = curve.keys[selectedKeyIndex];
        key.time = normalizedPoint.x;
        key.value = normalizedPoint.y;
        curve.MoveKey(selectedKeyIndex, key);

        // Update selected index after move
        selectedKeyIndex = FindKeyframeIndex(key.time);

        RedrawCurve();
        onCurveChanged?.Invoke(curve);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        isDragging = false;
    }

    Vector2 GetNormalizedPoint(Vector2 localPoint)
    {
        Rect rect = curveDisplay.rectTransform.rect;
        float x = (localPoint.x - rect.xMin) / rect.width;
        float y = (localPoint.y - rect.yMin) / rect.height;
        return new Vector2(x, y);
    }

    int GetNearestKeyframe(Vector2 normalizedPoint, float threshold)
    {
        float minDist = threshold;
        int nearestIndex = -1;

        for (int i = 0; i < curve.keys.Length; i++)
        {
            Vector2 keyPos = new Vector2(curve.keys[i].time, curve.keys[i].value);
            float dist = Vector2.Distance(keyPos, normalizedPoint);

            if (dist < minDist)
            {
                minDist = dist;
                nearestIndex = i;
            }
        }

        return nearestIndex;
    }

    int FindKeyframeIndex(float time)
    {
        for (int i = 0; i < curve.keys.Length; i++)
        {
            if (Mathf.Approximately(curve.keys[i].time, time))
                return i;
        }
        return -1;
    }
}