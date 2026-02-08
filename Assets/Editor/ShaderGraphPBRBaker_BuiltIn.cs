using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using UnityEngine.Rendering;

public class ShaderGraphPBRBaker_BuiltIn : EditorWindow
{
    public Material materialToBake;
    public int resolution = 1024;
    public bool createTestMaterial = true;

    private const string NORMAL_VISUALIZER_SHADER = "Hidden/NormalVisualizer";
    private const string ALBEDO_EXTRACTOR_SHADER = "Hidden/AlbedoExtractor";
    private const string BAKED_PBR_SHADER = "Custom/BakedPBR";
    private string outputFolder = "Assets/BakedTextures";

    private float bakedMetallicValue = 0f;
    private float bakedSmoothnessValue = 0f;

    // PUBLIC API - Call this from anywhere!
    public class BakeResult
    {
        public string baseColorPath;
        public string normalPath;
        public string metallicPath;
        public string smoothnessPath;
        public string materialPath;
        public bool success;
    }

    /// <summary>
    /// Bakes PBR maps from a material to textures
    /// </summary>
    /// <param name="material">The material to bake</param>
    /// <param name="outputFolder">Folder path to save textures (e.g. "Assets/MyTextures")</param>
    /// <param name="resolution">Texture resolution (default 1024)</param>
    /// <param name="createMaterial">Create a test material with baked textures (default true)</param>
    /// <returns>BakeResult containing paths to all created assets</returns>
    public static BakeResult BakePBRMaps(Material material, string outputFolder, int resolution = 1024, bool createMaterial = true)
    {
        BakeResult result = new BakeResult();

        if (material == null)
        {
            Debug.LogError("Cannot bake: material is null");
            result.success = false;
            return result;
        }

        Debug.Log($"Starting PBR bake for material: {material.name}");

        // Create instance to use non-static methods
        ShaderGraphPBRBaker_BuiltIn baker = CreateInstance<ShaderGraphPBRBaker_BuiltIn>();

        // Ensure output folder exists
        baker.EnsureOutputFolderExistsStatic(outputFolder);

        // Bake all maps
        result.baseColorPath = baker.BakeBaseColorStatic(material, resolution, outputFolder, material.name + "_BaseColor.png");
        result.normalPath = baker.BakeNormalMapStatic(material, resolution, outputFolder, material.name + "_Normal.png");
        result.metallicPath = baker.BakeMetallicMapStatic(material, resolution, outputFolder, material.name + "_Metallic.png");
        result.smoothnessPath = baker.BakeSmoothnessMapStatic(material, resolution, outputFolder, material.name + "_Smoothness.png");

        AssetDatabase.Refresh();

        // Create test material if requested
        if (createMaterial)
        {
            result.materialPath = baker.CreateTestMaterialStatic(
                material.name,
                outputFolder,
                result.baseColorPath,
                result.normalPath,
                result.metallicPath,
                result.smoothnessPath
            );
        }

        result.success = true;
        DestroyImmediate(baker);

        Debug.Log($"Finished baking PBR maps for {material.name}");
        return result;
    }

    [MenuItem("Tools/Bake ShaderGraph PBR Maps (Built-in)")]
    public static void ShowWindow()
    {
        GetWindow<ShaderGraphPBRBaker_BuiltIn>("Bake PBR Maps (Built-in)");
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Shader Graph PBR Baker", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        materialToBake = (Material)EditorGUILayout.ObjectField("Material", materialToBake, typeof(Material), false);
        resolution = EditorGUILayout.IntField("Resolution", resolution);
        outputFolder = EditorGUILayout.TextField("Output Folder", outputFolder);
        createTestMaterial = EditorGUILayout.Toggle("Create Test Material", createTestMaterial);

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox("Bakes: Base Color, Normals (tangent-space), Metallic, Smoothness", MessageType.Info);

        GUI.enabled = materialToBake != null;

        if (GUILayout.Button("Bake All Maps", GUILayout.Height(30)))
        {
            BakeAll(materialToBake, resolution);
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Individual Bakes", EditorStyles.boldLabel);

        if (GUILayout.Button("Bake Base Color Only"))
        {
            string path = BakeBaseColorStatic(materialToBake, resolution, outputFolder, materialToBake.name + "_BaseColor.png");
            AssetDatabase.Refresh();
        }

        if (GUILayout.Button("Bake Normal Map Only"))
        {
            string path = BakeNormalMapStatic(materialToBake, resolution, outputFolder, materialToBake.name + "_Normal.png");
            AssetDatabase.Refresh();
        }

        if (GUILayout.Button("Bake Metallic Only"))
        {
            string path = BakeMetallicMapStatic(materialToBake, resolution, outputFolder, materialToBake.name + "_Metallic.png");
            AssetDatabase.Refresh();
        }

        if (GUILayout.Button("Bake Smoothness Only"))
        {
            string path = BakeSmoothnessMapStatic(materialToBake, resolution, outputFolder, materialToBake.name + "_Smoothness.png");
            AssetDatabase.Refresh();
        }

        GUI.enabled = true;
    }

    private void BakeAll(Material mat, int res)
    {
        Debug.Log("Starting PBR bake process...");

        EnsureOutputFolderExists();

        Debug.Log("Baking Base Color...");
        string baseColorPath = BakeBaseColorStatic(mat, res, outputFolder, mat.name + "_BaseColor.png");

        Debug.Log("Baking Normal Map...");
        string normalPath = BakeNormalMapStatic(mat, res, outputFolder, mat.name + "_Normal.png");

        Debug.Log("Baking Metallic...");
        string metallicPath = BakeMetallicMapStatic(mat, res, outputFolder, mat.name + "_Metallic.png");

        Debug.Log("Baking Smoothness...");
        string smoothnessPath = BakeSmoothnessMapStatic(mat, res, outputFolder, mat.name + "_Smoothness.png");

        AssetDatabase.Refresh();

        if (createTestMaterial)
        {
            CreateTestMaterialStatic(mat.name, outputFolder, baseColorPath, normalPath, metallicPath, smoothnessPath);
        }

        Debug.Log("Finished baking all PBR maps!");
        EditorUtility.DisplayDialog("Bake Complete", "All PBR maps have been baked successfully!", "OK");
    }

    private void EnsureOutputFolderExists()
    {
        EnsureOutputFolderExistsStatic(outputFolder);
    }

    private void EnsureOutputFolderExistsStatic(string folderPath)
    {
        if (!AssetDatabase.IsValidFolder(folderPath))
        {
            string[] folders = folderPath.Split('/');
            string currentPath = folders[0];

            for (int i = 1; i < folders.Length; i++)
            {
                string newPath = currentPath + "/" + folders[i];
                if (!AssetDatabase.IsValidFolder(newPath))
                {
                    AssetDatabase.CreateFolder(currentPath, folders[i]);
                    Debug.Log("Created folder: " + newPath);
                }
                currentPath = newPath;
            }
        }
    }

    private string CreateTestMaterialStatic(string baseName, string folderPath, string baseColorPath, string normalPath, string metallicPath, string smoothnessPath)
    {
        Debug.Log("Creating test material...");

        Shader shader = Shader.Find(BAKED_PBR_SHADER);
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

        if (!string.IsNullOrEmpty(baseColorPath))
        {
            Texture2D baseColor = AssetDatabase.LoadAssetAtPath<Texture2D>(baseColorPath);
            if (baseColor != null)
            {
                newMat.SetTexture("_MainTex", baseColor);
                newMat.SetColor("_Color", Color.white);
                Debug.Log("Assigned base color texture");
            }
        }

        if (!string.IsNullOrEmpty(normalPath))
        {
            Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);
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
            Texture2D metallic = AssetDatabase.LoadAssetAtPath<Texture2D>(metallicPath);
            if (metallic != null)
            {
                newMat.SetTexture("_MetallicGlossMap", metallic);
                Debug.Log("Assigned metallic map");
            }
        }

        newMat.SetFloat("_Metallic", bakedMetallicValue);
        newMat.SetFloat("_Glossiness", bakedSmoothnessValue);

        Debug.Log("Material values - Metallic: " + bakedMetallicValue + ", Smoothness: " + bakedSmoothnessValue);

        EnsureOutputFolderExistsStatic(folderPath);

        string materialPath = Path.Combine(folderPath, baseName + "_Baked.mat");

        if (File.Exists(materialPath))
        {
            AssetDatabase.DeleteAsset(materialPath);
        }

        AssetDatabase.CreateAsset(newMat, materialPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("Test material created at: " + materialPath);

        EditorGUIUtility.PingObject(newMat);
        Selection.activeObject = newMat;

        return materialPath;
    }

    private string BakeBaseColorStatic(Material mat, int res, string folder, string filename)
    {
        Texture2D tex = RenderMaterialUnlit(mat, res);
        string path = SaveTextureToFolderStatic(tex, folder, filename, true);
        DestroyImmediate(tex);
        return path;
    }

    private string BakeNormalMapStatic(Material mat, int res, string folder, string filename)
    {
        Texture2D tex = RenderMaterial(mat, res, RenderMode.Normals);
        string path = SaveTextureToFolderStatic(tex, folder, filename, false);
        DestroyImmediate(tex);
        return path;
    }

    private string BakeMetallicMapStatic(Material mat, int res, string folder, string filename)
    {
        bakedMetallicValue = mat.HasProperty("_Metalicness") ? mat.GetFloat("_Metalicness") : 1.0f;
        Debug.Log("Baking metallic value: " + bakedMetallicValue);

        Texture2D tex = CreateSolidColorTexture(res, bakedMetallicValue);
        string path = SaveTextureToFolderStatic(tex, folder, filename, false);
        DestroyImmediate(tex);
        return path;
    }

    private string BakeSmoothnessMapStatic(Material mat, int res, string folder, string filename)
    {
        bakedSmoothnessValue = mat.HasProperty("_Smoothness") ? mat.GetFloat("_Smoothness") : 0.5f;
        Debug.Log("Baking smoothness value: " + bakedSmoothnessValue);

        Texture2D tex = CreateSolidColorTexture(res, bakedSmoothnessValue);
        string path = SaveTextureToFolderStatic(tex, folder, filename, false);
        DestroyImmediate(tex);
        return path;
    }

    private enum RenderMode
    {
        BaseColor,
        Normals
    }

    private Texture2D RenderMaterialUnlit(Material mat, int res)
    {
        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.transform.position = Vector3.zero;
        quad.transform.rotation = Quaternion.identity;
        quad.transform.localScale = Vector3.one * 2;

        MeshRenderer renderer = quad.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = mat;

        GameObject camObj = new GameObject("BakeCam");
        Camera cam = camObj.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 1;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 10f;
        cam.enabled = false;

        Color previousAmbient = RenderSettings.ambientLight;
        AmbientMode previousAmbientMode = RenderSettings.ambientMode;

        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = Color.white * 1.5f;

        List<GameObject> lights = new List<GameObject>();

        GameObject frontLight = new GameObject("FrontLight");
        Light fl = frontLight.AddComponent<Light>();
        fl.type = LightType.Directional;
        fl.intensity = 1.0f; //Change if material is too bright or dark
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

        RenderTexture rt = new RenderTexture(res, res, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        rt.antiAliasing = 1;
        cam.targetTexture = rt;

        cam.transform.position = new Vector3(0, 0, -1);
        cam.transform.LookAt(quad.transform);

        cam.Render();

        RenderTexture.active = rt;
        Texture2D tex = new Texture2D(res, res, TextureFormat.RGBA32, false, false);
        tex.ReadPixels(new Rect(0, 0, res, res), 0, 0);
        tex.Apply();
        RenderTexture.active = null;

        RenderSettings.ambientLight = previousAmbient;
        RenderSettings.ambientMode = previousAmbientMode;

        cam.targetTexture = null;
        rt.Release();
        DestroyImmediate(rt);
        DestroyImmediate(quad);
        DestroyImmediate(camObj);

        foreach (GameObject light in lights)
        {
            DestroyImmediate(light);
        }

        return tex;
    }

    private Texture2D RenderMaterial(Material mat, int res, RenderMode mode)
    {
        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.transform.position = Vector3.zero;
        quad.transform.rotation = Quaternion.identity;
        quad.transform.localScale = Vector3.one * 2;

        MeshRenderer renderer = quad.GetComponent<MeshRenderer>();

        Material renderMaterial = mat;
        if (mode == RenderMode.Normals)
        {
            Shader normalShader = Shader.Find(NORMAL_VISUALIZER_SHADER);
            if (normalShader == null)
            {
                Debug.LogError("Normal visualizer shader not found!");
                DestroyImmediate(quad);
                return new Texture2D(res, res);
            }
            renderMaterial = new Material(normalShader);
        }

        renderer.sharedMaterial = renderMaterial;

        GameObject camObj = new GameObject("BakeCam");
        Camera cam = camObj.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 1;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = (mode == RenderMode.Normals) ? new Color(0.5f, 0.5f, 1f, 1f) : Color.black;
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 10f;
        cam.enabled = false;

        RenderTexture rt = new RenderTexture(res, res, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
        rt.antiAliasing = 1;
        cam.targetTexture = rt;

        cam.transform.position = new Vector3(0, 0, -1);
        cam.transform.LookAt(quad.transform);

        cam.Render();

        RenderTexture.active = rt;
        Texture2D tex = new Texture2D(res, res, TextureFormat.RGBA32, false, true);
        tex.ReadPixels(new Rect(0, 0, res, res), 0, 0);
        tex.Apply();
        RenderTexture.active = null;

        cam.targetTexture = null;
        rt.Release();
        DestroyImmediate(rt);
        DestroyImmediate(quad);
        DestroyImmediate(camObj);

        if (mode == RenderMode.Normals)
            DestroyImmediate(renderMaterial);

        return tex;
    }

    private Texture2D CreateSolidColorTexture(int res, float value)
    {
        Texture2D tex = new Texture2D(res, res, TextureFormat.RGBA32, false, true);
        Color[] pixels = new Color[res * res];
        Color c = new Color(value, value, value, 1);

        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = c;

        tex.SetPixels(pixels);
        tex.Apply();

        return tex;
    }

    private string SaveTextureToFolderStatic(Texture2D tex, string folder, string filename, bool sRGB)
    {
        EnsureOutputFolderExistsStatic(folder);

        string path = Path.Combine(folder, filename);

        byte[] bytes = tex.EncodeToPNG();
        File.WriteAllBytes(path, bytes);
        AssetDatabase.ImportAsset(path);

        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null)
        {
            importer.sRGBTexture = sRGB;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.isReadable = true;

            if (filename.Contains("Normal"))
            {
                importer.textureType = TextureImporterType.NormalMap;
            }

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        }

        Debug.Log("Saved: " + path);
        return path;
    }
}