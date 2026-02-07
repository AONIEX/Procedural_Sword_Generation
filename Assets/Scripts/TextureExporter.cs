using UnityEngine;
using System.IO;

public class TextureExporter : MonoBehaviour
{
    public Material material;
    public RenderTexture renderTexture;
    public Camera bakeCamera;

    [Header("Sword Naming")]
    public string swordName = "Blade"; // Name for the folder

    void Update()
    {
     
    }

    public void ExportTexture(string folderPath)
    {
        if (bakeCamera == null || renderTexture == null || material == null)
        {
            Debug.LogError("Assign bakeCamera, renderTexture, and material first!");
            return;
        }

        // Render the material onto the RenderTexture
        bakeCamera.targetTexture = renderTexture;
        bakeCamera.Render();

        // Read pixels into Texture2D
        Texture2D tex = new Texture2D(renderTexture.width, renderTexture.height, TextureFormat.RGBA32, false, false);
        RenderTexture.active = renderTexture;
        tex.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
        tex.Apply();
        RenderTexture.active = null;

        // Convert from linear to gamma
        Color[] pixels = tex.GetPixels();
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i].r = Mathf.LinearToGammaSpace(pixels[i].r);
            pixels[i].g = Mathf.LinearToGammaSpace(pixels[i].g);
            pixels[i].b = Mathf.LinearToGammaSpace(pixels[i].b);
        }
        tex.SetPixels(pixels);
        tex.Apply();

        // Save texture
        string fileName = folderPath + swordName + "_Texture_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".png";
        File.WriteAllBytes(fileName, tex.EncodeToPNG());

        Debug.Log("Exported texture to folder: " + folderPath);
    }
}
