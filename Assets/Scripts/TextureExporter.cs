using UnityEngine;
using UnityEditor;
using System.IO;

public class TextureExporter : MonoBehaviour
{
    public Material material;
    public RenderTexture renderTexture;
    public Camera bakeCamera;
    public MeshRenderer meshRenderer; // The object with your sword material

    [Header("Sword Naming")]
    public string swordName = "Blade";

    [Header("Export Settings")]
    public int textureResolution = 2048;
    public bool exportAlbedo = true;
    public bool exportNormal = true;
    public bool exportMetallic = true;
    public bool exportSmoothness = true;

    public void ExportAllTextures(string folderPath)
    {

        if (!Directory.Exists(folderPath))
            Directory.CreateDirectory(folderPath);

        if (exportAlbedo)
            ExportMap("_BaseMap", folderPath + swordName + "_Albedo.png", false);

        if (exportNormal)
            ExportMap("_BumpMap", folderPath + swordName + "_Normal.png", true);

        if (exportMetallic)
            ExportMap("_MetallicGlossMap", folderPath + swordName + "_Metallic.png", false);

        if (exportSmoothness)
            ExportSmoothnessMap(folderPath + swordName + "_Smoothness.png");

        Debug.Log($"All textures exported to: {folderPath}");

#if UNITY_EDITOR
        AssetDatabase.Refresh();
#endif
    }

    void ExportMap(string propertyName, string filePath, bool isNormalMap)
    {
        if (material == null || bakeCamera == null)
        {
            Debug.LogError("Material or bakeCamera not assigned!");
            return;
        }

        // Try to get the texture directly from the material first
        if (material.HasProperty(propertyName))
        {
            Texture texture = material.GetTexture(propertyName);
            if (texture != null)
            {
                SaveTextureToFile(texture, filePath, isNormalMap);
                return;
            }
        }

        // If no texture found, bake it from the material
        BakeTextureFromMaterial(propertyName, filePath, isNormalMap);
    }

    void BakeTextureFromMaterial(string propertyName, string filePath, bool isNormalMap)
    {
        // Create or resize render texture
        if (renderTexture == null || renderTexture.width != textureResolution)
        {
            renderTexture = new RenderTexture(textureResolution, textureResolution, 0, RenderTextureFormat.ARGB32);
        }

        // Create a temporary material for baking specific properties
        Material bakeMaterial = new Material(Shader.Find("Hidden/Internal-Colored"));

        // Set up the bake
        bakeCamera.targetTexture = renderTexture;
        bakeCamera.Render();

        // Read the render texture
        Texture2D bakedTexture = new Texture2D(textureResolution, textureResolution, TextureFormat.RGBA32, false, !isNormalMap);
        RenderTexture.active = renderTexture;
        bakedTexture.ReadPixels(new Rect(0, 0, textureResolution, textureResolution), 0, 0);
        bakedTexture.Apply();
        RenderTexture.active = null;

        // Save
        SaveTexture2D(bakedTexture, filePath);

        Destroy(bakeMaterial);
    }

    void ExportSmoothnessMap(string filePath)
    {
        // Smoothness is often in the alpha channel of metallic map
        if (material.HasProperty("_MetallicGlossMap"))
        {
            Texture metallicMap = material.GetTexture("_MetallicGlossMap");
            if (metallicMap != null)
            {
                ExtractSmoothnessFromMetallicMap(metallicMap, filePath);
                return;
            }
        }

        // Or it might be a separate value
        if (material.HasProperty("_Smoothness"))
        {
            float smoothness = material.GetFloat("_Smoothness");
            CreateSolidColorTexture(smoothness, filePath);
        }
    }

    void ExtractSmoothnessFromMetallicMap(Texture source, string filePath)
    {
        Texture2D readable = GetReadableTexture(source);
        Texture2D smoothnessMap = new Texture2D(readable.width, readable.height, TextureFormat.RGB24, false);

        Color[] pixels = readable.GetPixels();
        for (int i = 0; i < pixels.Length; i++)
        {
            float smoothness = pixels[i].a; // Alpha channel contains smoothness
            pixels[i] = new Color(smoothness, smoothness, smoothness, 1f);
        }

        smoothnessMap.SetPixels(pixels);
        smoothnessMap.Apply();

        SaveTexture2D(smoothnessMap, filePath);

        if (readable != source)
            Destroy(readable);
    }

    void CreateSolidColorTexture(float value, string filePath)
    {
        Texture2D tex = new Texture2D(256, 256, TextureFormat.RGB24, false);
        Color color = new Color(value, value, value, 1f);
        Color[] pixels = new Color[256 * 256];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = color;

        tex.SetPixels(pixels);
        tex.Apply();
        SaveTexture2D(tex, filePath);
        Destroy(tex);
    }

    void SaveTextureToFile(Texture source, string filePath, bool isNormalMap)
    {
        Texture2D readable = GetReadableTexture(source);
        SaveTexture2D(readable, filePath);

        if (readable != source)
            Destroy(readable);
    }

    Texture2D GetReadableTexture(Texture source)
    {
        // If already a readable Texture2D, return it
        if (source is Texture2D tex2D)
        {
            try
            {
                tex2D.GetPixel(0, 0);
                return tex2D;
            }
            catch { }
        }

        // Otherwise, create a readable copy via RenderTexture
        RenderTexture tmp = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32);
        Graphics.Blit(source, tmp);

        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = tmp;

        Texture2D readable = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
        readable.ReadPixels(new Rect(0, 0, tmp.width, tmp.height), 0, 0);
        readable.Apply();

        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(tmp);

        return readable;
    }

    void SaveTexture2D(Texture2D texture, string filePath)
    {
        byte[] bytes = texture.EncodeToPNG();
        File.WriteAllBytes(filePath, bytes);
        Debug.Log($"Saved: {filePath}");
    }
}