using UnityEngine;
using System.IO;
using UnityEngine.Rendering;

public class ShaderGraphPBRBaker_Runtime : MonoBehaviour
{
    private const string BAKED_PBR_SHADER = "Standard";

    public class BakeResult
    {
        public string baseColorPath;
        public string normalPath;
        public string metallicPath;
        public string smoothnessPath;
        public string materialPath;
        public Material bakedMaterial; // Return the actual material object
        public bool success;
    }

    /// <summary>
    /// Bakes PBR maps from a material to textures at runtime
    /// </summary>
    public static BakeResult BakePBRMaps(Material material, string outputFolder, int resolution = 1024, bool createMaterial = true)
    {
        BakeResult result = new BakeResult();

        if (material == null)
        {
            Debug.LogError("Cannot bake: material is null");
            result.success = false;
            return result;
        }

        Debug.Log($"Starting runtime PBR bake for material: {material.name}");

        // Ensure output folder exists
        if (!Directory.Exists(outputFolder))
        {
            Directory.CreateDirectory(outputFolder);
            Debug.Log("Created folder: " + outputFolder);
        }

        // Store values before baking
        float metallicValue = material.HasProperty("_Metalicness") ? material.GetFloat("_Metalicness") : 1.0f;
        float smoothnessValue = material.HasProperty("_Smoothness") ? material.GetFloat("_Smoothness") : 0.5f;

        // Bake all maps
        result.baseColorPath = BakeBaseColor(material, resolution, outputFolder, material.name + "_BaseColor.png");
        result.normalPath = BakeNormalMap(material, resolution, outputFolder, material.name + "_Normal.png");
        result.metallicPath = BakeMetallicMap(material, resolution, outputFolder, material.name + "_Metallic.png", metallicValue);
        result.smoothnessPath = BakeSmoothnessMap(material, resolution, outputFolder, material.name + "_Smoothness.png", smoothnessValue);

        // Create material if requested
        if (createMaterial)
        {
            result.bakedMaterial = CreateBakedMaterial(
                material.name,
                outputFolder,
                result.baseColorPath,
                result.normalPath,
                result.metallicPath,
                result.smoothnessPath,
                metallicValue,
                smoothnessValue
            );
            result.materialPath = Path.Combine(outputFolder, material.name + "_Baked.mat");
        }

        result.success = true;

        Debug.Log($"Finished baking PBR maps for {material.name}");
        return result;
    }

    private static Material CreateBakedMaterial(string baseName, string folderPath, string baseColorPath, string normalPath, string metallicPath, string smoothnessPath, float metallicValue, float smoothnessValue)
    {
        Debug.Log("Creating baked material...");

        // Try to find the custom shader first, fallback to Standard
        Shader shader = Shader.Find("Custom/BakedPBR");
        if (shader == null)
        {
            Debug.LogWarning("Custom BakedPBR shader not found, using Standard shader");
            shader = Shader.Find("Standard");
        }

        if (shader == null)
        {
            Debug.LogError("Could not find any suitable shader!");
            return null;
        }

        Material newMat = new Material(shader);
        newMat.name = baseName + "_Baked";

#if UNITY_EDITOR
        // In editor, load textures from AssetDatabase
        if (!string.IsNullOrEmpty(baseColorPath))
        {
            // Convert absolute path to relative if needed
            string relativeBaseColorPath = ConvertToRelativePath(baseColorPath);
            Texture2D baseColor = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(relativeBaseColorPath);
            if (baseColor != null)
            {
                newMat.SetTexture("_MainTex", baseColor);
                newMat.SetColor("_Color", Color.white);
                Debug.Log("Assigned base color texture");
            }
        }

        if (!string.IsNullOrEmpty(normalPath))
        {
            string relativeNormalPath = ConvertToRelativePath(normalPath);
            Texture2D normal = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(relativeNormalPath);
            if (normal != null)
            {
                newMat.SetTexture("_BumpMap", normal);
                if (newMat.HasProperty("_BumpScale"))
                    newMat.SetFloat("_BumpScale", 1.0f);
                Debug.Log("Assigned normal map");
            }
        }

        if (!string.IsNullOrEmpty(metallicPath))
        {
            string relativeMetallicPath = ConvertToRelativePath(metallicPath);
            Texture2D metallic = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(relativeMetallicPath);
            if (metallic != null)
            {
                newMat.SetTexture("_MetallicGlossMap", metallic);
                Debug.Log("Assigned metallic map");
            }
        }
#else
    // At runtime, load textures from file
    if (!string.IsNullOrEmpty(baseColorPath) && File.Exists(baseColorPath))
    {
        Texture2D baseColor = LoadTextureFromFile(baseColorPath);
        if (baseColor != null)
        {
            newMat.SetTexture("_MainTex", baseColor);
            newMat.SetColor("_Color", Color.white);
            Debug.Log("Assigned base color texture");
        }
    }

    if (!string.IsNullOrEmpty(normalPath) && File.Exists(normalPath))
    {
        Texture2D normal = LoadTextureFromFile(normalPath);
        if (normal != null)
        {
            newMat.SetTexture("_BumpMap", normal);
            if (newMat.HasProperty("_BumpScale"))
                newMat.SetFloat("_BumpScale", 1.0f);
            Debug.Log("Assigned normal map");
        }
    }

    if (!string.IsNullOrEmpty(metallicPath) && File.Exists(metallicPath))
    {
        Texture2D metallic = LoadTextureFromFile(metallicPath);
        if (metallic != null)
        {
            newMat.SetTexture("_MetallicGlossMap", metallic);
            Debug.Log("Assigned metallic map");
        }
    }
#endif

        // Set metallic and smoothness values (same as editor version)
        newMat.SetFloat("_Metallic", metallicValue);
        newMat.SetFloat("_Glossiness", smoothnessValue);

        Debug.Log("Material values - Metallic: " + metallicValue + ", Smoothness: " + smoothnessValue);

#if UNITY_EDITOR
        // In editor, convert absolute path to Assets-relative path
        string materialPath = Path.Combine(folderPath, baseName + "_Baked.mat");

        // Convert to Assets-relative path if it's an absolute path
        if (materialPath.Contains(Application.dataPath))
        {
            materialPath = "Assets" + materialPath.Substring(Application.dataPath.Length);
        }

        // Normalize path separators
        materialPath = materialPath.Replace("\\", "/");

        Debug.Log("Attempting to save material to: " + materialPath);

        // Ensure the folder exists in AssetDatabase
        string folderOnly = Path.GetDirectoryName(materialPath).Replace("\\", "/");
        if (!UnityEditor.AssetDatabase.IsValidFolder(folderOnly))
        {
            string[] folders = folderOnly.Split('/');
            string currentPath = folders[0];

            for (int i = 1; i < folders.Length; i++)
            {
                string newPath = currentPath + "/" + folders[i];
                if (!UnityEditor.AssetDatabase.IsValidFolder(newPath))
                {
                    UnityEditor.AssetDatabase.CreateFolder(currentPath, folders[i]);
                    Debug.Log("Created folder: " + newPath);
                }
                currentPath = newPath;
            }
        }

        // Delete existing asset if it exists
        if (UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(materialPath) != null)
        {
            UnityEditor.AssetDatabase.DeleteAsset(materialPath);
        }

        UnityEditor.AssetDatabase.CreateAsset(newMat, materialPath);
        UnityEditor.AssetDatabase.SaveAssets();
        UnityEditor.AssetDatabase.Refresh();
        Debug.Log("Test material created at: " + materialPath);

        // Ping and select the material (same as editor version)
        UnityEditor.EditorGUIUtility.PingObject(newMat);
        UnityEditor.Selection.activeObject = newMat;
#else
    Debug.LogWarning("Cannot save .mat file at runtime. Material object created but not saved to disk.");
#endif

        return newMat;
    }

    // Helper method to convert absolute paths to Assets-relative paths
    private static string ConvertToRelativePath(string path)
    {
        if (path.Contains(Application.dataPath))
        {
            return "Assets" + path.Substring(Application.dataPath.Length).Replace("\\", "/");
        }
        return path.Replace("\\", "/");
    }

    private static Texture2D LoadTextureFromFile(string path)
    {
        if (!File.Exists(path))
        {
            Debug.LogError("Texture file not found: " + path);
            return null;
        }

        byte[] fileData = File.ReadAllBytes(path);
        Texture2D tex = new Texture2D(2, 2);

        if (tex.LoadImage(fileData))
        {
            return tex;
        }

        Debug.LogError("Failed to load texture: " + path);
        return null;
    }

    private static string BakeBaseColor(Material mat, int res, string folder, string filename)
    {
        Texture2D tex = RenderMaterialUnlit(mat, res);
        string path = SaveTexture(tex, folder, filename);
        Destroy(tex);
        return path;
    }

    private static string BakeNormalMap(Material mat, int res, string folder, string filename)
    {
        Texture2D tex = CreateFlatNormalMap(res);
        string path = SaveTexture(tex, folder, filename);
        Destroy(tex);
        return path;
    }

    private static string BakeMetallicMap(Material mat, int res, string folder, string filename, float metallicValue)
    {
        Debug.Log("Baking metallic value: " + metallicValue);
        Texture2D tex = CreateSolidColorTexture(res, metallicValue);
        string path = SaveTexture(tex, folder, filename);
        Destroy(tex);
        return path;
    }

    private static string BakeSmoothnessMap(Material mat, int res, string folder, string filename, float smoothnessValue)
    {
        Debug.Log("Baking smoothness value: " + smoothnessValue);
        Texture2D tex = CreateSolidColorTexture(res, smoothnessValue);
        string path = SaveTexture(tex, folder, filename);
        Destroy(tex);
        return path;
    }

    private static Texture2D RenderMaterialUnlit(Material mat, int res)
    {
        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.transform.position = Vector3.zero;
        quad.transform.rotation = Quaternion.identity;
        quad.transform.localScale = Vector3.one * 2;

        MeshRenderer renderer = quad.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = mat;

        GameObject camObj = new GameObject("BakeCam_Runtime");
        Camera cam = camObj.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 1;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 10f;
        cam.enabled = false;

        // Save previous render settings (same as Built-in version)
        Color previousAmbient = RenderSettings.ambientLight;
        AmbientMode previousAmbientMode = RenderSettings.ambientMode;

        // Set ambient lighting (same as Built-in version)
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = Color.white * 1.5f;

        // Create directional lights (same as Built-in version)
        System.Collections.Generic.List<GameObject> lights = new System.Collections.Generic.List<GameObject>();

        GameObject frontLight = new GameObject("FrontLight");
        Light fl = frontLight.AddComponent<Light>();
        fl.type = LightType.Directional;
        fl.intensity = 1.0f; // Change if material is too bright or dark
        fl.color = Color.white;
        frontLight.transform.rotation = Quaternion.Euler(0, 0, 0);
        lights.Add(frontLight);

        GameObject topLight = new GameObject("TopLight");
        Light tl = topLight.AddComponent<Light>();
        tl.type = LightType.Directional;
        tl.intensity = 1.0f;
        tl.color = Color.white;
        topLight.transform.rotation = Quaternion.Euler(90, 0, 0);
        lights.Add(topLight);

        // Use sRGB RenderTexture (same as Built-in version)
        RenderTexture rt = new RenderTexture(res, res, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        rt.antiAliasing = 1;
        cam.targetTexture = rt;

        cam.transform.position = new Vector3(0, 0, -1);
        cam.transform.LookAt(quad.transform);

        cam.Render();

        RenderTexture.active = rt;
        // Use matching texture format (false, false) for sRGB (same as Built-in version)
        Texture2D tex = new Texture2D(res, res, TextureFormat.RGBA32, false, false);
        tex.ReadPixels(new Rect(0, 0, res, res), 0, 0);
        tex.Apply();
        RenderTexture.active = null;

        // Restore previous render settings (same as Built-in version)
        RenderSettings.ambientLight = previousAmbient;
        RenderSettings.ambientMode = previousAmbientMode;

        // Cleanup
        cam.targetTexture = null;
        rt.Release();
        Destroy(rt);
        Destroy(quad);
        Destroy(camObj);

        // Cleanup lights (same as Built-in version)
        foreach (GameObject light in lights)
        {
            Destroy(light);
        }

        return tex;
    }

    private static Texture2D CreateSolidColorTexture(int res, float value)
    {
        Texture2D tex = new Texture2D(res, res, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[res * res];
        Color c = new Color(value, value, value, 1);

        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = c;

        tex.SetPixels(pixels);
        tex.Apply();

        return tex;
    }

    private static Texture2D CreateFlatNormalMap(int res)
    {
        Texture2D tex = new Texture2D(res, res, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[res * res];
        Color normalColor = new Color(0.5f, 0.5f, 1f, 1f);

        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = normalColor;

        tex.SetPixels(pixels);
        tex.Apply();

        return tex;
    }

    private static string SaveTexture(Texture2D tex, string folder, string filename)
    {
        if (!Directory.Exists(folder))
        {
            Directory.CreateDirectory(folder);
        }

        string path = Path.Combine(folder, filename);
        byte[] bytes = tex.EncodeToPNG();
        File.WriteAllBytes(path, bytes);

#if UNITY_EDITOR
        // Import and configure texture in editor (same as Built-in version)
        string relativePath = ConvertToRelativePath(path);

        UnityEditor.AssetDatabase.ImportAsset(relativePath);

        UnityEditor.TextureImporter importer = UnityEditor.AssetImporter.GetAtPath(relativePath) as UnityEditor.TextureImporter;
        if (importer != null)
        {
            // Determine if this is sRGB based on filename
            bool sRGB = filename.Contains("BaseColor");

            importer.sRGBTexture = sRGB;
            importer.textureCompression = UnityEditor.TextureImporterCompression.Uncompressed;
            importer.isReadable = true;

            if (filename.Contains("Normal"))
            {
                importer.textureType = UnityEditor.TextureImporterType.NormalMap;
            }

            UnityEditor.AssetDatabase.ImportAsset(relativePath, UnityEditor.ImportAssetOptions.ForceUpdate);
        }
#endif

        Debug.Log("Saved: " + path);
        return path;
    }
}