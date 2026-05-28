using UnityEngine;
using UnityEngine.UI;

public class EngravingSystem : MonoBehaviour
{
    public RenderTexture renderTexture;
    public Material brushMaterial;
    public EngravingSettings engravingSettings;

    public RawImage targetCanvas;

    private Vector2 lastUV;
    private bool hasLastUV = false;
    public ScrollRect scrollRect;
    public BladeGeneration bladeGeneration;
    void Awake()
    {
        if (renderTexture == null || !renderTexture.IsCreated())
        {
            renderTexture = new RenderTexture(2048, 2048, 0, RenderTextureFormat.RFloat);
            renderTexture.filterMode = FilterMode.Bilinear;  // ADD THIS
            renderTexture.Create();
            ClearToWhite();
        }

        if (targetCanvas != null)
            targetCanvas.texture = renderTexture;

        if (bladeGeneration != null)                   
               bladeGeneration.SetEngravingTexture(renderTexture);

    }

    void Update()
    {
        if (targetCanvas == null) return;

        Vector2 mousePos = Input.mousePosition;
        bool overCanvas = RectTransformUtility.RectangleContainsScreenPoint(
          targetCanvas.rectTransform,
          mousePos,
          null
         );

        // Only disable scroll if clicking ON the drawing canvas
        if (Input.GetMouseButtonDown(0) && overCanvas)
        {
            if (scrollRect != null)
                scrollRect.enabled = false;

            hasLastUV = false;
        }

        // Re-enable scroll when mouse is released
        if (Input.GetMouseButtonUp(0))
        {
            if (scrollRect != null)
                scrollRect.enabled = true;

            hasLastUV = false;
            return;
        }

        // If mouse is not held or not over canvas, do nothing
        if (!Input.GetMouseButton(0) || !overCanvas)
            return;

        // Convert mouse to UV
        Vector2 local;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
            targetCanvas.rectTransform,
            Input.mousePosition,
            null,
            out local)) return;

        Vector2 uv = (local + targetCanvas.rectTransform.sizeDelta * 0.5f) /
                     targetCanvas.rectTransform.sizeDelta;

        // Brush parameters
        brushMaterial.SetFloat("_Depth", engravingSettings.depthValue);
        brushMaterial.SetFloat("_Size", engravingSettings.brushSize);

        // If this is the first point, just draw once
        if (!hasLastUV)
        {
            DrawStamp(uv);
            lastUV = uv;
            hasLastUV = true;
            return;
        }

        // Interpolate between lastUV and uv
        float dist = Vector2.Distance(lastUV, uv);
        int steps = Mathf.CeilToInt(dist * 200); // 50 = resolution; increase for smoother lines

        for (int i = 0; i <= steps; i++)
        {
            Vector2 p = Vector2.Lerp(lastUV, uv, i / (float)steps);
            DrawStamp(p);
        }

        lastUV = uv;
    }

    void DrawStamp(Vector2 uv)
    {
        brushMaterial.SetVector("_BrushPos", new Vector4(uv.x, uv.y, 0, 0));
        brushMaterial.SetFloat("_DoBrush", 1);

        RenderTexture temp = RenderTexture.GetTemporary(
            renderTexture.width,
            renderTexture.height,
            0,
            renderTexture.format
        );

        Graphics.Blit(renderTexture, temp);
        Graphics.Blit(temp, renderTexture, brushMaterial);

        RenderTexture.ReleaseTemporary(temp);

        brushMaterial.SetFloat("_DoBrush", 0);
    }

    void ClearToWhite()
    {
        RenderTexture active = RenderTexture.active;
        RenderTexture.active = renderTexture;
        GL.Clear(true, true, Color.white);
        RenderTexture.active = active;
    }

    public void ClearCanvas()
    {
        ClearToWhite();

        // Also update the RawImage texture reference in case it was reassigned
        if (targetCanvas != null)
            targetCanvas.texture = renderTexture;
    }

    public void ApplyPreset(Texture2D preset)
    {
        if (preset == null) return;
        Graphics.Blit(preset, renderTexture);
        if (targetCanvas != null)
            targetCanvas.texture = renderTexture;
        if (bladeGeneration != null)
            bladeGeneration.BakeEngravingSnapshot();
    }
}
