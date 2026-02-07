using System;
using System.Collections.Generic;
using UnityEngine;
using System.Reflection;
using System.IO;
using System.Security.Cryptography.X509Certificates;



//FOR SAVING DATA
[Serializable]
public class BladeGenerationData
{
    public List<BladeProfileLayer> baseProfiles;

    public float profileOverlapBlendAmount;
    public MeshQuality meshQuality;

    public float handleXPosition;
    public float bladeThickness;
    public float edgeSharpness;
    public float spineThickness;

    public BladeGeneration.SharpSide sharpSide;

    public List<BladeGeneration.FullerSettings> fullers;
}



[System.Serializable]
public class BladeProfileLayer
{
    [DisplayName("Cross Section Type", "Blade Profile", 2, "")]
    public BladeBaseProfile profile;

    [Range(0, 1f), DisplayName("Start Height", "Blade Profile", 2, "")]
    public float startHeight;

    [Range(0, 1f), DisplayName("End Height", "Blade Profile", 2, "")]
    public float endHeight;

    [DisplayName("Transition Curve", "Blade Profile", 2, "")]
    public AnimationCurve influenceCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

    [Range(0, 2), DisplayName("Profile Scale", "Blade Profile", 2, "")]
    public float scale = 1;
}

public class BladeGeneration : MonoBehaviour
{
    [HideInUI]
    public List<Segment> segments;
    [HideInUI]
    public List<Vector3> smoothCenters;

    public SplineAndLineGen splineGen;

    [Header("Blade Base Profiles")]
    [DisplayName("Cross section ", "Blade Profile", 2, "Cross Sections")]
    public List<BladeProfileLayer> baseProfiles = new List<BladeProfileLayer>()
    {
        new BladeProfileLayer
        {
            profile = BladeBaseProfile.Lenticular,
            startHeight = 0f,
            endHeight = 1f
        }
    };

    [Range(0.1f, 3f)]
    [DisplayName("Profile Blend Strenght", "Blade Profile", 3, "Blending")]
    public float profileOverlapBlendAmount = 0.5f;

    [Header("Mesh Quality")]
    [DisplayName("Mesh Quality", "General", 3, "Quality")]
    public MeshQuality meshQuality = MeshQuality.Medium;

    private int segmentSubdivisions = 3;
    private int tipSubdivisions = 5;
    private int widthSubdivisions = 5;

    private const int curvatureWindow = 5;
    private const float curvatureBlend = 0.5f;
    private const int smoothWindow = 5;

    private const float COLLAPSE_THRESHOLD = 1e-6f;

    private float baseWidth = 0f;

    public GameObject guard;
    public GameObject handle;
    public GameObject holder;

    [Range(-0.5f, 0.5f), DisplayName("Handle X Position", "General", 2, "Position")]
    public float HandleXPosition;

    [Range(0.01f, .2f), DisplayName("Blade Thickness", "Blade Geometry", 7, "Width")]
    public float bladeThickness = 0.1f;

    [Range(0f, 0.15f), DisplayName("Edge Sharpness", "Edge & Spine", 0, "Edge")]
    public float edgeSharpness = 0.05f;

    [Range(0.001f, 0.1f), DisplayName("Bevel Thickness", "Edge & Spine", 2, "Spine")]
    public float spineThickness = 0.005f;

    [DisplayName("Blade Material", "Rendering", 0, "Materials")]
    public Material bladeMaterial;

    [DisplayName("Sharp Edge Material", "Rendering", 1, "Materials")]
    public Material sharpEdgeMaterial;


    public enum FullerType
    {
        None,
        Basic,
        Hollow,
        Hollow_Circular
    }



    [System.Serializable]
    public class FullerSettings
    {

        [DisplayName("Fuller Type", "Fullers", 9, "General")]
        public FullerType fullerType = FullerType.Basic;

        [Range(0f, 1f), DisplayName("Fuller Start", "Fullers", 10, "Position")]
        public float start = 0.1f;

        [Range(0f, 1f), DisplayName("Fuller End", "Fullers", 11, "Position")]
        public float end = 0.8f;

        [Range(0.0f, 0.9f), DisplayName("Fuller Depth", "Fullers", 12, "Shape")]
        public float fullerDepth = 0.3f;

        [Range(0.05f, 1.1f), DisplayName("Fuller Width", "Fullers", 13, "Shape")]
        public float fullerWidth = 0.3f;

        [Range(0f, 1f), DisplayName("Fuller X Position", "Fullers", 14, "Position")]
        public float fullerCenter = 0.5f;

        [DisplayName("Fuller Falloff", "Fullers", 15, "Shape")]
        public AnimationCurve fullerFalloff = AnimationCurve.EaseInOut(0, 1, 1, 0);


        [Range(0.05f, 1.1f), DisplayName("Circle Radias", "Fullers", 13, "Shape")]
        public float circleRadius = 0.3f;
    }
   


    [DisplayName("Fuller Settings", "Fullers", 20, "General")]
    public List<FullerSettings> fullers;


    [HideInUI]
    public List<Vector3> smoothGeometricCenters;

    private float[,] holeMask;
    private bool hollowHitsLeft;
    private bool hollowHitsRight;
    private int hollowStartRing;
    private int hollowEndRing;

    bool[] hollowHitsLeftPerRing;
    bool[] hollowHitsRightPerRing;
    bool[] circularHitsRightPerRing;
    bool[] circularHitsLeftPerRing;

    public enum SharpSide
    {
        Left,
        Right,
        Both
    }

    [DisplayName("Sharp Edge", "Edge & Spine", 6, "Edge")]
    public SharpSide sharpSide = SharpSide.Both;

    //Used for circular hollow for slight smoothness
    private float bevelAmount = 0.1f;


    private List<Vector2> uvs = new List<Vector2>();


    [Header("Other Control")]
    public SwordShaderControl swordShaderControl;
    public HiltCreation hiltCreation;
    public TextureExporter textureExporter;

    private MeshFilter meshFilter;
    void Start()
    {
        HandleXPosition = holder.transform.localPosition.x;
        splineGen = GetComponent<SplineAndLineGen>();
        RegenerateBlade(true);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            RegenerateBlade(true);
        }

        if (Input.GetKeyDown(KeyCode.E))
        {
            ExportMesh();
        }

        if (handle.transform.localPosition.x != HandleXPosition)
        {
            holder.transform.localPosition = new Vector3(HandleXPosition, holder.transform.localPosition.y, holder.transform.localPosition.z);
        }
    }


    public void ExportMesh()
    {
        if (meshFilter == null)
        {

            meshFilter = GetComponent<MeshFilter>();
        }

        if (meshFilter != null)
        {

            string folderPath = Application.dataPath + "/Exported/" + "blade_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + "/";

            // --- Create a folder for this sword ---
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }

            // Full path to the .obj file
            string meshFilePath = Path.Combine(folderPath, "_blade_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".obj");

            // Export the mesh using the correct file path
            RuntimeObjExporter.ExportMesh(meshFilter.mesh, meshFilePath);
            Debug.Log("Exported Mesh to: " + meshFilePath);

            // Export the texture to the same folder
            textureExporter.ExportTexture(folderPath);
            Debug.Log("Exported Mesh");

        }


    }


    
    private void ApplyMeshQualitySettings()
    {
        switch (meshQuality)
        {
            case MeshQuality.Low:
                segmentSubdivisions = 5;
                tipSubdivisions = 3;
                widthSubdivisions = 5;
                bevelAmount = 0.0f;
                break;
            case MeshQuality.Medium:
                segmentSubdivisions = 13;
                tipSubdivisions = 9;
                widthSubdivisions = 13;
                bevelAmount = 0.04f; //Setting for smooth-ish Circular Hollow Fullers
                break;
            case MeshQuality.High:
                segmentSubdivisions = 26;
                tipSubdivisions = 20;
                widthSubdivisions = 26;
                bevelAmount = 0.025f; //Setting for smooth-ish Circular Hollow Fullers
                break;
            case MeshQuality.Ultra:
                segmentSubdivisions = 50;
                tipSubdivisions = 40;
                widthSubdivisions = 50;
                bevelAmount = 0.015f; //Setting for smooth-ish Circular Hollow Fullers
                break;
        }
    }

    public void Generate()
    {
        RegenerateBlade(true);
    }

    public void Generate3DBlade(bool smoothSegements)
    {
        Mesh mesh3D = new Mesh();
        mesh3D.name = "Blade Mesh"; // <--- ADDED: Name the mesh
        List<Vector3> vertices = new List<Vector3>();
        List<int> trianglesFrontBack = new List<int>();
        List<int> trianglesSharp = new List<int>();

        uvs.Clear();

   

        List<Vector3> smoothLefts = new List<Vector3>();
        List<Vector3> smoothRights = new List<Vector3>();
        smoothCenters = new List<Vector3>();
        smoothGeometricCenters = new List<Vector3>();

        GenerateSmoothSegments(segments, smoothLefts, smoothRights, smoothCenters, smoothGeometricCenters);

        baseWidth = CalculateMaxBladeWidth(smoothLefts, smoothRights);

        // 1. Front face
        int frontVertexCount;
        List<int> frontTriangles;
        GenerateFrontFace(
            segments,
            smoothLefts,
            smoothRights,
            vertices,
            out frontTriangles,
            out frontVertexCount,
            smoothCenters
        );
        trianglesFrontBack.AddRange(frontTriangles);

        // 2. Back face + thickness
        GenerateBackFace(
            vertices,
            frontTriangles,
            frontVertexCount,
            smoothLefts,
            smoothRights,
            smoothCenters,
            smoothGeometricCenters,
            trianglesFrontBack
        );

        Vector3 widthDir = (smoothRights[0] - smoothLefts[0]).normalized;
        Vector3 forwardDir = (smoothCenters[1] - smoothCenters[0]).normalized;
        Vector3 bladeNormal = Vector3.Cross(widthDir, forwardDir).normalized;
        int ringCount = smoothLefts.Count;

        circularHitsLeftPerRing = new bool[ringCount];
        circularHitsRightPerRing = new bool[ringCount];

        if (hollowHitsLeftPerRing == null || hollowHitsLeftPerRing.Length != ringCount)
        {
            hollowHitsLeftPerRing = new bool[ringCount];
            hollowHitsRightPerRing = new bool[ringCount];
        }


       


        // 3. Bevel / ridge vertices
        int sharpStartFront, sharpStartBack;
        GenerateBevelVertices(
            vertices,
            smoothLefts,
            smoothRights,
            smoothCenters,
            bladeNormal,
            out sharpStartFront,
            out sharpStartBack
        );

       




        GenerateUVs(vertices, smoothLefts, smoothRights, smoothCenters,
              frontVertexCount, sharpStartFront, sharpStartBack, ringCount);

        ApplyFullers(
               vertices,
               smoothLefts,
               smoothRights,
               smoothCenters,
               bladeNormal,
               frontVertexCount,
               trianglesFrontBack
           );



        // 4. Connect bevels to blade faces
        ConnectBevels(
            trianglesFrontBack,
            trianglesSharp,
            frontVertexCount,
            sharpStartFront,
            sharpStartBack,
            ringCount
        );
        // 5. Final mesh
        mesh3D.SetVertices(vertices);
        mesh3D.SetUVs(0, uvs); // Apply UVs
        mesh3D.subMeshCount = 2;
        mesh3D.SetTriangles(trianglesFrontBack, 0);
        mesh3D.SetTriangles(trianglesSharp, 1);

        RecalculateNormalsSmooth(mesh3D);
        mesh3D.RecalculateTangents();

        MeshFilter meshFilter = GetComponent<MeshFilter>();
        if (meshFilter == null)
            meshFilter = gameObject.AddComponent<MeshFilter>();
        meshFilter.mesh = mesh3D;

        MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
        if (meshRenderer == null)
            meshRenderer = gameObject.AddComponent<MeshRenderer>();
        meshRenderer.materials = new Material[] { bladeMaterial, sharpEdgeMaterial };

        MeshCollider meshCollider = GetComponent<MeshCollider>();
        if (meshCollider == null)
            meshCollider = gameObject.AddComponent<MeshCollider>();
        meshCollider.sharedMesh = mesh3D;
        meshCollider.convex = false;
    }


    private void GenerateUVs(List<Vector3> vertices,
                     List<Vector3> smoothLefts,
                     List<Vector3> smoothRights,
                     List<Vector3> smoothCenters,
                     int frontVertexCount,
                     int sharpStartFront,
                     int sharpStartBack,
                     int ringCount)
    {
        float totalLength = 0f;
        List<float> cumulativeLengths = new List<float> { 0f };

        for (int i = 1; i < ringCount; i++)
        {
            totalLength += Vector3.Distance(smoothCenters[i], smoothCenters[i - 1]);
            cumulativeLengths.Add(totalLength);
        }

        uvs = new List<Vector2>(vertices.Count);
        for (int i = 0; i < vertices.Count; i++)
            uvs.Add(Vector2.zero);

        // 1. Front face UVs
        for (int ring = 0; ring < ringCount; ring++)
        {
            float v = totalLength > 0f ? cumulativeLengths[ring] / totalLength : 0f;
            int ringStart = ring * widthSubdivisions;

            for (int w = 0; w < widthSubdivisions; w++)
            {
                float u = w / (float)(widthSubdivisions - 1);
                int index = ringStart + w;

                if (index < frontVertexCount)
                    uvs[index] = new Vector2(u, v);
            }
        }

        // 2. Back face UVs
        for (int i = 0; i < frontVertexCount; i++)
        {
            int backIndex = i + frontVertexCount;
            if (backIndex < vertices.Count)
                uvs[backIndex] = new Vector2(1f - uvs[i].x, uvs[i].y);
        }

        // 3. Bevel UVs
        for (int ring = 0; ring < ringCount; ring++)
        {
            float v = totalLength > 0f ? cumulativeLengths[ring] / totalLength : 0f;

            int leftIndex = sharpStartFront + ring * 2;
            int rightIndex = leftIndex + 1;

            if (leftIndex < sharpStartBack)
            {
                uvs[leftIndex] = new Vector2(0f, v);
                uvs[rightIndex] = new Vector2(1f, v);
            }
        }

        for (int ring = 0; ring < ringCount; ring++)
        {
            float v = totalLength > 0f ? cumulativeLengths[ring] / totalLength : 0f;

            int leftIndex = sharpStartBack + ring * 2;
            int rightIndex = leftIndex + 1;

            if (leftIndex < vertices.Count && rightIndex < vertices.Count)
            {
                uvs[leftIndex] = new Vector2(1f, v);
                uvs[rightIndex] = new Vector2(0f, v);
            }
        }

        // Note: Circular hollow fuller updates UVs for its vertices inside ApplyCircularHollowFuller
    }




    private void ApplyFullers(List<Vector3> vertices,
        List<Vector3> smoothLefts,
        List<Vector3> smoothRights,
        List<Vector3> smoothCenters,
        Vector3 bladeNormal,
        int frontVertexCount,
        List<int> trianglesFrontBack)
    {

        bool hollowFullerExists = false;
        bool circularHollowFullerExists = false;

        for (int i = 0; i < fullers.Count; i++)
        {
            //Apply X Fullers can be different types
            switch (fullers[i].fullerType)
            {
                case FullerType.Basic:
                    ApplyGrooveFuller(
                    vertices,
                    smoothLefts,
                    smoothRights,
                    smoothCenters,
                    bladeNormal,
                    frontVertexCount,
                    fullers[i]
                );
                    break;
                case FullerType.Hollow:

                    hollowFullerExists = true;
                    break;
                case FullerType.Hollow_Circular:
                    Debug.Log("Cirular Hollow Exists");
                    circularHollowFullerExists = true;
                    break;
                case FullerType.None:
                    break;
                default:
                    break;
            }
        }
        if (hollowFullerExists)
        {
            ApplyHollowFuller(
                 vertices,
                 trianglesFrontBack,
                 frontVertexCount,
                 smoothCenters,
                 smoothLefts,
                 smoothRights

             );
        }
        if (circularHollowFullerExists)
        {
            Debug.Log("Applying circular hollow fuller");
            ApplyCircularHollowFuller(
               vertices,
               trianglesFrontBack,
               frontVertexCount,
               smoothCenters,
               smoothLefts,
               smoothRights

           );
        }
       
    }

    private void ApplyCircularHollowFuller(
        List<Vector3> vertices,
        List<int> trianglesFrontBack,
        int frontVertexCount,
        List<Vector3> smoothCenters,
        List<Vector3> smoothLefts,
        List<Vector3> smoothRights)
    {
      

        const int thicknessSegments = 3;

        int ringCount = smoothCenters.Count;
        int width = widthSubdivisions;
        circularHitsLeftPerRing = new bool[ringCount];
        circularHitsRightPerRing = new bool[ringCount];
        bool[,] holeMask = new bool[ringCount, width];
        bool[,] deleteMask = new bool[ringCount, width];
        bool[,] capSnapped = new bool[ringCount, width]; 

        int[] holeMin = new int[ringCount];
        int[] holeMax = new int[ringCount];

        hollowStartRing = ringCount;
        hollowEndRing = -1;

        #region Length metrics

        float totalLength = 0f;
        float[] cumulative = new float[ringCount];

        for (int i = 1; i < ringCount; i++)
        {
            totalLength += Vector3.Distance(smoothCenters[i], smoothCenters[i - 1]);
            cumulative[i] = totalLength;
        }

        float avgWidth = 0f;
        for (int i = 0; i < ringCount; i++)
            avgWidth += Vector3.Distance(smoothLefts[i], smoothRights[i]);
        avgWidth /= ringCount;

        #endregion

        #region Hole mask

        foreach (var fuller in fullers)
        {
            if (fuller.fullerType != FullerType.Hollow_Circular)
                continue;

            float centerLen = fuller.start * totalLength;
            float centerU = fuller.fullerCenter * 2f - 1f;
            float radius = fuller.circleRadius * Mathf.Min(totalLength, avgWidth);

            for (int r = 0; r < ringCount; r++)
            {
                Vector3 left = smoothLefts[r];
                Vector3 right = smoothRights[r];
                Vector3 widthDir = (right - left).normalized;

                Vector3 holeCenter = Vector3.Lerp(left, right, (centerU + 1f) * 0.5f);
                float lenDist = Mathf.Abs(cumulative[r] - centerLen);

                for (int v = 0; v < width; v++)
                {
                    float t = v / (float)(width - 1);
                    Vector3 p = Vector3.Lerp(left, right, t);

                    float widthDist = Vector3.Dot(p - holeCenter, widthDir);
                    float dist = Mathf.Sqrt(lenDist * lenDist + widthDist * widthDist);

                    if (dist <= radius)
                    {
                        holeMask[r, v] = true;

                        // Track circular edge hits for bevel suppression
                        if (v == 0)
                            circularHitsLeftPerRing[r] = true;
                        else if (v == width - 1)
                            circularHitsRightPerRing[r] = true;

                        hollowStartRing = Mathf.Min(hollowStartRing, r);
                        hollowEndRing = Mathf.Max(hollowEndRing, r);
                    }

                }
            }
        }

        if (hollowEndRing < 0)
            return;

        #endregion

        #region Hole min/max

        for (int r = 0; r < ringCount; r++)
        {
            int min = width;
            int max = -1;

            for (int v = 0; v < width; v++)
            {
                if (!holeMask[r, v]) continue;
                min = Mathf.Min(min, v);
                max = Mathf.Max(max, v);
            }

            holeMin[r] = (max >= min) ? min : -1;
            holeMax[r] = (max >= min) ? max : -1;
        }

        #endregion

        #region Delete mask

        for (int r = hollowStartRing; r <= hollowEndRing; r++)
        {
            if (holeMin[r] < 0) continue;

            for (int v = Mathf.Max(0, holeMin[r] - 2);
                     v <= Mathf.Min(width - 1, holeMax[r] + 2);
                     v++)
                deleteMask[r, v] = true;
        }

        #endregion

        #region Rebuild front/back triangles

        List<int> newTris = new List<int>(trianglesFrontBack.Count);

        for (int i = 0; i < trianglesFrontBack.Count; i += 3)
        {
            int a = trianglesFrontBack[i];
            int b = trianglesFrontBack[i + 1];
            int c = trianglesFrontBack[i + 2];

            int fa = a >= frontVertexCount ? a - frontVertexCount : a;
            int fb = b >= frontVertexCount ? b - frontVertexCount : b;
            int fc = c >= frontVertexCount ? c - frontVertexCount : c;

            int ra = fa / width;
            int rb = fb / width;
            int rc = fc / width;

            if (ra < 0 || ra >= ringCount ||
                rb < 0 || rb >= ringCount ||
                rc < 0 || rc >= ringCount)
            {
                newTris.Add(a); newTris.Add(b); newTris.Add(c);
                continue;
            }

            int va = fa % width;
            int vb = fb % width;
            int vc = fc % width;

            int del = 0;
            if (deleteMask[ra, va]) del++;
            if (deleteMask[rb, vb]) del++;
            if (deleteMask[rc, vc]) del++;

            if (del <= 2)
            {
                newTris.Add(a);
                newTris.Add(b);
                newTris.Add(c);
            }
        }

        trianglesFrontBack.Clear();
        trianglesFrontBack.AddRange(newTris);

        #endregion

        #region Snapping helpers

        void Snap(int from, int to)
        {
            vertices[to] = vertices[from];
            uvs[to] = uvs[from];

            vertices[to + frontVertexCount] = vertices[from + frontVertexCount];
            uvs[to + frontVertexCount] = uvs[from + frontVertexCount];
        }

        #endregion

        #region Edge snapping

        for (int r = hollowStartRing; r <= hollowEndRing; r++)
        {
            if (holeMin[r] < 0) continue;

            int baseIdx = r * width;
            int left = baseIdx + holeMin[r];
            int right = baseIdx + holeMax[r];

            if (holeMin[r] > 0) Snap(left, left - 1);
            if (holeMin[r] > 1) Snap(left, left - 2);

            if (holeMax[r] < width - 1) Snap(right, right + 1);
            if (holeMax[r] < width - 2) Snap(right, right + 2);
        }

        #endregion

        #region Cap snapping (TRACKED)

        for (int r = hollowStartRing; r <= hollowEndRing; r++)
        {
            if (holeMin[r] < 0) continue;

            bool startCap = r == 0 || holeMin[r - 1] < 0;
            bool endCap = r == ringCount - 1 || holeMin[r + 1] < 0;

            void SnapCap(int fromRing, int toRing, int v)
            {
                Snap(fromRing * width + v, toRing * width + v);
                capSnapped[toRing, v] = true;
            }

            if (startCap && r > 0)
            {
                int prev = r - 1;
                for (int v = holeMin[r]; v <= holeMax[r]; v++)
                    SnapCap(r, prev, v);
            }

            if (endCap && r < ringCount - 1)
            {
                int next = r + 1;
                for (int v = holeMin[r]; v <= holeMax[r]; v++)
                    SnapCap(r, next, v);
            }
        }

        #endregion

        #region Walls

        List<int> wallTris = new List<int>();

        void AddWall(int a0, int a1, bool flip)
        {
            // --- duplicate endpoints so walls never share snapped verts ---
            int a0f = vertices.Count;
            vertices.Add(vertices[a0]);
            uvs.Add(uvs[a0]);

            int a1f = vertices.Count;
            vertices.Add(vertices[a1]);
            uvs.Add(uvs[a1]);

            int a0b = vertices.Count;
            vertices.Add(vertices[a0 + frontVertexCount]);
            uvs.Add(uvs[a0 + frontVertexCount]);

            int a1b = vertices.Count;
            vertices.Add(vertices[a1 + frontVertexCount]);
            uvs.Add(uvs[a1 + frontVertexCount]);

            int[] c0 = new int[thicknessSegments + 1];
            int[] c1 = new int[thicknessSegments + 1];

            c0[0] = a0f;
            c1[0] = a1f;
            c0[thicknessSegments] = a0b;
            c1[thicknessSegments] = a1b;

            // --- thickness interpolation ---
            for (int i = 1; i < thicknessSegments; i++)
            {
                float t = i / (float)thicknessSegments;

                c0[i] = vertices.Count;
                vertices.Add(Vector3.Lerp(vertices[a0f], vertices[a0b], t));
                uvs.Add(Vector2.Lerp(uvs[a0f], uvs[a0b], t));

                c1[i] = vertices.Count;
                vertices.Add(Vector3.Lerp(vertices[a1f], vertices[a1b], t));
                uvs.Add(Vector2.Lerp(uvs[a1f], uvs[a1b], t));
            }

            // --- build wall quads ---
            for (int i = 0; i < thicknessSegments; i++)
            {
                int v00 = c0[i];
                int v01 = c1[i];
                int v10 = c0[i + 1];
                int v11 = c1[i + 1];

                if (!flip)
                {
                    wallTris.Add(v00); wallTris.Add(v01); wallTris.Add(v11);
                    wallTris.Add(v00); wallTris.Add(v11); wallTris.Add(v10);
                }
                else
                {
                    wallTris.Add(v01); wallTris.Add(v00); wallTris.Add(v10);
                    wallTris.Add(v01); wallTris.Add(v10); wallTris.Add(v11);
                }
            }
        }


        // ---- CAP WALLS (culled when snapped)
        for (int r = hollowStartRing; r <= hollowEndRing; r++)
        {
            if (holeMin[r] < 0) continue;

            bool startCap = r == 0 || holeMin[r - 1] < 0;
            bool endCap = r == ringCount - 1 || holeMin[r + 1] < 0;

            if (!startCap && !endCap) continue;

            int ringBase = r * width;

            for (int v = holeMin[r]; v < holeMax[r]; v++)
            {
                if (!holeMask[r, v] || !holeMask[r, v + 1])
                    continue;

                if (capSnapped[r, v] && capSnapped[r, v + 1])
                    continue;

                int a = ringBase + v;
                int b = ringBase + v + 1;

                if (startCap) AddWall(a, b, false);
                if (endCap) AddWall(a, b, true);
            }
        }
        // ===== SIDE WALLS (ROBUST, NO OVERLAP) =====
        for (int r = hollowStartRing; r < hollowEndRing; r++)
        {
            if (holeMin[r] < 0 || holeMin[r + 1] < 0)
                continue;

            int base0 = r * width;
            int base1 = (r + 1) * width;

            // LEFT EDGE
            int v0 = holeMin[r];
            int v1 = holeMin[r + 1];

            if (holeMask[r, v0] && holeMask[r + 1, v1])
            {
                // Only if the edge is exposed (not interior)
                if (v0 == 0 || !holeMask[r, v0 - 1] ||
                    v1 == 0 || !holeMask[r + 1, v1 - 1])
                {
                    AddWall(base0 + v0, base1 + v1, true);
                }
            }

            // RIGHT EDGE
            v0 = holeMax[r];
            v1 = holeMax[r + 1];

            if (holeMask[r, v0] && holeMask[r + 1, v1])
            {
                if (v0 == width - 1 || !holeMask[r, v0 + 1] ||
                    v1 == width - 1 || !holeMask[r + 1, v1 + 1])
                {
                    AddWall(base0 + v0, base1 + v1, false);
                }
            }
        }

        trianglesFrontBack.AddRange(wallTris);

        #endregion
    }


    private void ApplyGrooveFuller(
    List<Vector3> vertices,
    List<Vector3> smoothLefts,
    List<Vector3> smoothRights,
    List<Vector3> smoothCenters,
    Vector3 bladeNormal,
    int frontVertexCount,
    FullerSettings fuller)
    {
        if (fuller == null)
            return;

        int ringCount = smoothLefts.Count;

        float totalLength = 0f;
        List<float> cumulativeLengths = new List<float> { 0f };

        for (int i = 1; i < ringCount; i++)
        {
            totalLength += Vector3.Distance(
                smoothCenters[i],
                smoothCenters[i - 1]
            );
            cumulativeLengths.Add(totalLength);
        }

        // FIX: Calculate average width to scale fuller depth appropriately
        float avgWidth = 0f;
        for (int i = 0; i < ringCount; i++)
        {
            avgWidth += Vector3.Distance(smoothLefts[i], smoothRights[i]);
        }
        avgWidth /= ringCount;

        // FIX: Scale depth based on blade dimensions
        // Larger blades need proportionally shallower fullers relative to blade thickness
        float bladeSizeScale = Mathf.Clamp(avgWidth / 0.5f, 0.5f, 2f);

        float[,] depthMap = new float[ringCount, widthSubdivisions];

        float fadeInLength = 0.05f;
        float fadeOutLength = 0.05f;

        // FIX: Set maximum depth relative to blade dimensions
        float maxAllowedDepth = bladeThickness * 0.35f; // Reduced from 0.4f

        for (int ringIdx = 0; ringIdx < ringCount; ringIdx++)
        {
            float bladeT = totalLength > 0f
                ? cumulativeLengths[ringIdx] / totalLength
                : 0f;

            float lengthMask = 1f;

            if (fuller.start > 0f)
            {
                float inStart = fuller.start;
                float inEnd = Mathf.Min(fuller.start + fadeInLength, fuller.end);

                float inT = Mathf.InverseLerp(inStart, inEnd, bladeT);
                inT = Mathf.Clamp01(inT);
                lengthMask *= inT * inT * (3f - 2f * inT);
            }

            float outStart = Mathf.Max(0f, fuller.end - fadeOutLength);
            float outEnd = fuller.end;

            float outT = Mathf.InverseLerp(outEnd, outStart, bladeT);
            outT = Mathf.Clamp01(outT);
            lengthMask *= outT * outT * (3f - 2f * outT);

            if (bladeT < fuller.start || bladeT > fuller.end)
                lengthMask = 0f;

            if (lengthMask <= 0f)
                continue;

            // FIX: Apply size scaling to depth calculation
            float depth = fuller.fullerDepth * (bladeThickness * 0.5f) * lengthMask;
            depth = depth / bladeSizeScale; // Scale down for larger blades
            depth = Mathf.Min(depth, maxAllowedDepth); // Hard cap

            float width = fuller.fullerWidth;

            if (depth <= 0f || width <= 0f)
                continue;

            float centerPos = fuller.fullerCenter * 2f - 1f;

            for (int vertIdx = 0; vertIdx < widthSubdivisions; vertIdx++)
            {
                float widthT = vertIdx / (float)(widthSubdivisions - 1);
                float widthPos = widthT * 2f - 1f;

                float dist = Mathf.Abs(widthPos - centerPos) / width;

                if (dist > 1f)
                    continue;

                float falloff = fuller.fullerFalloff.Evaluate(dist);
                float fullerDepth = depth * falloff;

                // Don't let fullers stack infinitely
                float existingDepth = depthMap[ringIdx, vertIdx];
                depthMap[ringIdx, vertIdx] = Mathf.Min(existingDepth + fullerDepth, maxAllowedDepth);
            }
        }

        float[,] smoothedDepth = SmoothDepthMap(depthMap, ringCount);

        // Apply with final safety clamp
        for (int ringIdx = 0; ringIdx < ringCount; ringIdx++)
        {
            Vector3 left = smoothLefts[ringIdx];
            Vector3 right = smoothRights[ringIdx];
            Vector3 widthDir = (right - left).normalized;

            Vector3 forwardDir = GetForwardDir(smoothCenters, ringIdx);

            Vector3 ringNormal = Vector3.Cross(widthDir, forwardDir).normalized;
            int ringStart = ringIdx * widthSubdivisions;

            for (int v = 0; v < widthSubdivisions; v++)
            {
                int front = ringStart + v;
                int back = front + frontVertexCount;

                float d = Mathf.Min(smoothedDepth[ringIdx, v], maxAllowedDepth);

                vertices[front] -= ringNormal * d;
                vertices[back] += ringNormal * d;
            }
        }
    }

    private void GenerateFrontFace(
        List<Segment> segments,
        List<Vector3> smoothLefts,
        List<Vector3> smoothRights,
        List<Vector3> vertices,
        out List<int> frontTriangles,
        out int frontVertexCount, List<Vector3> smoothCenters)
    {
        frontTriangles = new List<int>();
        GenerateEdgeGeometry(segments, smoothLefts, smoothRights, vertices, frontTriangles, smoothCenters);
        frontVertexCount = vertices.Count;
    }
    private void ConnectBevels(
     List<int> trianglesFrontBack,
     List<int> trianglesSharp,
     int frontVertexCount,
     int sharpStartFront,
     int sharpStartBack,
     int ringCount)
    {
        for (int i = 0; i < ringCount - 1; i++)
        {
            // ----- LEFT EDGE -----
            bool leftThis =
              (hollowHitsLeftPerRing != null &&
               i < hollowHitsLeftPerRing.Length &&
               hollowHitsLeftPerRing[i])
              ||
              (circularHitsLeftPerRing != null &&
               i < circularHitsLeftPerRing.Length &&
               circularHitsLeftPerRing[i]);

            bool leftNext =
                (hollowHitsLeftPerRing != null &&
                 i + 1 < hollowHitsLeftPerRing.Length &&
                 hollowHitsLeftPerRing[i + 1])
                ||
                (circularHitsLeftPerRing != null &&
                 i + 1 < circularHitsLeftPerRing.Length &&
                 circularHitsLeftPerRing[i + 1]);

            bool leftBevelActive = !(leftThis && leftNext);

            // ----- RIGHT EDGE -----

            bool rightThis =
                (hollowHitsRightPerRing != null &&
                 i < hollowHitsRightPerRing.Length &&
                 hollowHitsRightPerRing[i])
                ||
                (circularHitsRightPerRing != null &&
                 i < circularHitsRightPerRing.Length &&
                 circularHitsRightPerRing[i]);

            bool rightNext =
                (hollowHitsRightPerRing != null &&
                 i + 1 < hollowHitsRightPerRing.Length &&
                 hollowHitsRightPerRing[i + 1])
                ||
                (circularHitsRightPerRing != null &&
                 i + 1 < circularHitsRightPerRing.Length &&
                 circularHitsRightPerRing[i + 1]);

            bool rightBevelActive = !(rightThis && rightNext);

  

            // ----- INDICES -----
            int frontA = i * widthSubdivisions;
            int frontB = (i + 1) * widthSubdivisions;

            int backA = frontA + frontVertexCount;
            int backB = frontB + frontVertexCount;

            int sharpLeftFrontA = sharpStartFront + i * 2;
            int sharpLeftFrontB = sharpStartFront + (i + 1) * 2;

            int sharpLeftBackA = sharpStartBack + i * 2;
            int sharpLeftBackB = sharpStartBack + (i + 1) * 2;

            int frontARight = frontA + (widthSubdivisions - 1);
            int frontBRight = frontB + (widthSubdivisions - 1);

            int backARight = frontARight + frontVertexCount;
            int backBRight = frontBRight + frontVertexCount;

            int sharpRightFrontA = sharpLeftFrontA + 1;
            int sharpRightFrontB = sharpLeftFrontB + 1;

            int sharpRightBackA = sharpLeftBackA + 1;
            int sharpRightBackB = sharpLeftBackB + 1;

            List<int> leftList =
                (sharpSide == SharpSide.Left || sharpSide == SharpSide.Both)
                ? trianglesSharp
                : trianglesFrontBack;

            List<int> rightList =
                (sharpSide == SharpSide.Right || sharpSide == SharpSide.Both)
                ? trianglesSharp
                : trianglesFrontBack;

            // ----- LEFT BEVEL -----
            if (leftBevelActive)
            {
                leftList.Add(frontA); leftList.Add(frontB); leftList.Add(sharpLeftFrontA);
                leftList.Add(frontB); leftList.Add(sharpLeftFrontB); leftList.Add(sharpLeftFrontA);

                leftList.Add(backB); leftList.Add(backA); leftList.Add(sharpLeftBackA);
                leftList.Add(backB); leftList.Add(sharpLeftBackA); leftList.Add(sharpLeftBackB);

                leftList.Add(sharpLeftFrontA); leftList.Add(sharpLeftFrontB); leftList.Add(sharpLeftBackA);
                leftList.Add(sharpLeftFrontB); leftList.Add(sharpLeftBackB); leftList.Add(sharpLeftBackA);
            }

            // ----- RIGHT BEVEL -----
            if (rightBevelActive)
            {
                rightList.Add(frontARight); rightList.Add(sharpRightFrontA); rightList.Add(frontBRight);
                rightList.Add(frontBRight); rightList.Add(sharpRightFrontA); rightList.Add(sharpRightFrontB);

                rightList.Add(backARight); rightList.Add(backBRight); rightList.Add(sharpRightBackA);
                rightList.Add(backBRight); rightList.Add(sharpRightBackB); rightList.Add(sharpRightBackA);

                rightList.Add(sharpRightFrontA); rightList.Add(sharpRightBackA); rightList.Add(sharpRightFrontB);
                rightList.Add(sharpRightFrontB); rightList.Add(sharpRightBackA); rightList.Add(sharpRightBackB);
            }
        }
    }



    private void GenerateBackFace(
     List<Vector3> vertices,
     List<int> frontTriangles,
     int frontVertexCount,
     List<Vector3> smoothLefts,
     List<Vector3> smoothRights,
     List<Vector3> smoothCenters,
     List<Vector3> smoothGeometricCenters,
     List<int> trianglesFrontBack)
    {
        int ringCount = smoothLefts.Count;
        float spineOffset = splineGen.edgeSettings.spineOffset;

        for (int ring = 0; ring < ringCount; ring++)
        {
            float bladeT = ring / (float)(ringCount - 1);

            // Fade profile thickness near the tip
            int tipProfileFadeRings = tipSubdivisions;
            float profileTipFade = 1f;

            if (ring >= ringCount - tipProfileFadeRings)
            {
                float t = (ringCount - 1 - ring) / (float)(tipProfileFadeRings - 1);
                profileTipFade = Mathf.Clamp01(t);
                profileTipFade = Mathf.Pow(profileTipFade, 2.2f);
            }

            Vector3 left = smoothLefts[ring];
            Vector3 right = smoothRights[ring];

            // KEY FIX: Use the ORIGINAL segment center for thickness distribution
            Vector3 segmentCenter = smoothGeometricCenters[ring];

            // The spine center (with offset applied) - for reference only
            Vector3 spineCenter = smoothCenters[ring];

            Vector3 widthDir = (right - left).normalized;
            Vector3 forwardDir = GetForwardDir(smoothCenters, ring);
            Vector3 bladeNormal = Vector3.Cross(widthDir, forwardDir).normalized;

            int ringStart = ring * widthSubdivisions;

            for (int v = 0; v < widthSubdivisions; v++)
            {
                int frontIndex = ringStart + v;

                // Standard t from left (0) to right (1)
                float t = v / (float)(widthSubdivisions - 1);

                // Get the vertex position
                Vector3 vertexPos = Vector3.Lerp(left, right, t);

                // Calculate where the segment center is along the left-right line
                // This finds where the original segment center sits in the 0-1 range
                float segmentCenterT;
                float leftToRight = Vector3.Distance(left, right);
                if (leftToRight > 0.0001f)
                {
                    float leftToSegment = Vector3.Distance(left, segmentCenter);
                    segmentCenterT = leftToSegment / leftToRight;
                }
                else
                {
                    segmentCenterT = 0.5f; // Fallback to middle
                }

                // Calculate widthT relative to the SEGMENT CENTER
                float widthT;
                if (t < segmentCenterT)
                {
                    // Left of segment center: map to -1..0
                    widthT = (t / Mathf.Max(segmentCenterT, 0.0001f)) - 1f;
                }
                else
                {
                    // Right of segment center: map to 0..1
                    widthT = (t - segmentCenterT) / Mathf.Max(1f - segmentCenterT, 0.0001f);
                }

                float baseHalf = bladeThickness * 0.5f;

                // Use the blend function with profiles (widthT is now relative to segment center)
                float shaped = BlendThicknessAtOverlap(baseProfiles, bladeT, widthT, baseHalf);

                // Apply tip fade
                float halfThickness = shaped * profileTipFade;

                vertices[frontIndex] += bladeNormal * halfThickness;
                vertices.Add(vertices[frontIndex] - bladeNormal * (halfThickness * 2f));
            }
        }

        for (int i = 0; i < frontTriangles.Count; i += 3)
        {
            int a = frontTriangles[i];
            int b = frontTriangles[i + 1];
            int c = frontTriangles[i + 2];

            int ringA = a / widthSubdivisions;
            int ringB = b / widthSubdivisions;
            int ringC = c / widthSubdivisions;

            int vA = a % widthSubdivisions;
            int vB = b % widthSubdivisions;
            int vC = c % widthSubdivisions;

            trianglesFrontBack.Add(c + frontVertexCount);
            trianglesFrontBack.Add(b + frontVertexCount);
            trianglesFrontBack.Add(a + frontVertexCount);
        }
    }
    private void GenerateBevelVertices(
        List<Vector3> vertices,
        List<Vector3> smoothLefts,
        List<Vector3> smoothRights,
        List<Vector3> smoothCenters,
        Vector3 bladeNormal,
        out int sharpStartFront,
        out int sharpStartBack)
    {
        int ringCount = smoothLefts.Count;
        sharpStartFront = vertices.Count;

        Vector3 tipDir = (smoothCenters[ringCount - 1] - smoothCenters[ringCount - 2]).normalized;

        AddBevelVertices(
            vertices,
            smoothLefts,
            smoothRights,
            smoothCenters,
            tipDir,
            bladeNormal,
            +1f
        );

        sharpStartBack = vertices.Count;

        AddBevelVertices(
            vertices,
            smoothLefts,
            smoothRights,
            smoothCenters,
            tipDir,
            bladeNormal,
            -1f
        );
    }
  
    private void AddBevelVertices(
    List<Vector3> vertices,
    List<Vector3> smoothLefts,
    List<Vector3> smoothRights,
    List<Vector3> smoothCenters,
    Vector3 tipDir,
    Vector3 bladeNormal,
    float normalSign)
    {
        int ringCount = smoothLefts.Count;

        for (int i = 0; i < ringCount; i++)
        {
            Vector3 center = smoothCenters[i];
            Vector3 left = smoothLefts[i];
            Vector3 right = smoothRights[i];

            Vector3 leftOffset = left - center;
            Vector3 rightOffset = right - center;

            bool leftValid = leftOffset.sqrMagnitude > COLLAPSE_THRESHOLD;
            bool rightValid = rightOffset.sqrMagnitude > COLLAPSE_THRESHOLD;

            Vector3 widthDir;

            if (i < ringCount - 1)
            {
                if (leftValid && rightValid)
                    widthDir = (right - left).normalized;
                else if (rightValid)
                    widthDir = rightOffset.normalized;
                else if (leftValid)
                    widthDir = -leftOffset.normalized;
                else
                    widthDir = Vector3.right;
            }
            else
            {
                widthDir = Vector3.Cross(bladeNormal, tipDir).normalized;
                if (widthDir.sqrMagnitude < COLLAPSE_THRESHOLD)
                    widthDir = Vector3.right;
            }

            Vector3 toLeft = -widthDir;
            Vector3 toRight = widthDir;

            if (i == ringCount - 1)
            {
                toLeft = (left - center).normalized;
                toRight = (right - center).normalized;
            }

            float leftThickness =
                (sharpSide == SharpSide.Left || sharpSide == SharpSide.Both)
                    ? edgeSharpness
                    : spineThickness;

            float rightThickness =
                (sharpSide == SharpSide.Right || sharpSide == SharpSide.Both)
                    ? edgeSharpness
                    : spineThickness;

            Vector3 leftRidge = left + toLeft * leftThickness;
            Vector3 rightRidge = right + toRight * rightThickness;

            if (i < hollowHitsLeftPerRing.Length && hollowHitsLeftPerRing[i])
                leftRidge = left;

            if (i < hollowHitsRightPerRing.Length && hollowHitsRightPerRing[i])
                rightRidge = right;
            if (i == ringCount - 1)
            {
                leftRidge += tipDir * edgeSharpness;
                rightRidge += tipDir * edgeSharpness;
            }

            vertices.Add(leftRidge + normalSign * bladeNormal * spineThickness);
            vertices.Add(rightRidge + normalSign * bladeNormal * spineThickness);
        }
    }


    private float CalculateMaxBladeWidth(List<Vector3> smoothLefts, List<Vector3> smoothRights)
    {
        float maxWidth = 0f;

        // Check first 30% of blade (base area where guard sits)
        int checkCount = Mathf.Min(smoothLefts.Count, Mathf.CeilToInt(smoothLefts.Count * 0.3f));

        for (int i = 0; i < checkCount; i++)
        {
            float width = Vector3.Distance(smoothLefts[i], smoothRights[i]);
            maxWidth = Mathf.Max(maxWidth, width);
        }

        return maxWidth;
    }
    public void GenerateSmoothSegments(
    List<Segment> segments,
    List<Vector3> smoothLefts,
    List<Vector3> smoothRights,
    List<Vector3> smoothCenters,
    List<Vector3> smoothGeometricCenters,
    bool symmetry = true)
    {
        // Compute total number of rings across the whole blade
        int totalRings = 0;
        for (int i = 0; i < segments.Count - 1; i++)
        {
            bool isTipSegment = (i == segments.Count - 2);
            int currentSubdivisions = isTipSegment ? tipSubdivisions : segmentSubdivisions;
            totalRings += currentSubdivisions;
        }

        int ringIndex = 0;
        const float TRANSITION_BLEND_RANGE = 0.3f; // Smooth collapse transitions over 30% of segment

        for (int i = 0; i < segments.Count - 1; i++)
        {
            Segment p0 = segments[Mathf.Max(i - 1, 0)];
            Segment p1 = segments[i];
            Segment p2 = segments[i + 1];
            Segment p3 = segments[Mathf.Min(i + 2, segments.Count - 1)];

            bool isTipSegment = (i == segments.Count - 2);
            int currentSubdivisions = isTipSegment ? tipSubdivisions : segmentSubdivisions;
            bool isLastSegment = (i == segments.Count - 2);

            bool p1LeftCollapsed = false;
            bool p1RightCollapsed = false;
            bool p2LeftCollapsed = false;
            bool p2RightCollapsed = false;

            if (!isTipSegment)
            {
                p1LeftCollapsed = Vector3.Distance(p1.left, p1.center) < 0.001f;
                p1RightCollapsed = Vector3.Distance(p1.right, p1.center) < 0.001f;
                p2LeftCollapsed = Vector3.Distance(p2.left, p2.center) < 0.001f;
                p2RightCollapsed = Vector3.Distance(p2.right, p2.center) < 0.001f;
            }

            for (int j = 0; j <= currentSubdivisions; j++)
            {
                if (!isLastSegment && j == currentSubdivisions)
                    continue;

                float t = j / (float)currentSubdivisions;

                // IMPORTANT: Save the original segment center from Catmull-Rom interpolation
                Vector3 originalSegmentCenter = CatmullRom(p0.center, p1.center, p2.center, p3.center, t);
                Vector3 center = originalSegmentCenter; // Start with the segment center
                Vector3 left = CatmullRom(p0.left, p1.left, p2.left, p3.left, t);
                Vector3 right = CatmullRom(p0.right, p1.right, p2.right, p3.right, t);

                float bladeT = ringIndex / (float)(totalRings - 1);
                ringIndex++;

                // ===== IMPROVED COLLAPSE BLEND CALCULATION =====
                float leftCollapseBlend = 0f;
                float rightCollapseBlend = 0f;

                if (p1LeftCollapsed && p2LeftCollapsed)
                {
                    leftCollapseBlend = 1f;
                }
                else if (p1LeftCollapsed && !p2LeftCollapsed)
                {
                    // Smooth fade out using SmoothStep
                    float smoothT = Mathf.SmoothStep(0f, 1f, 1f - t);
                    leftCollapseBlend = smoothT;
                }
                else if (!p1LeftCollapsed && p2LeftCollapsed)
                {
                    // Smooth fade in using SmoothStep
                    float smoothT = Mathf.SmoothStep(0f, 1f, t);
                    leftCollapseBlend = smoothT;
                }

                if (p1RightCollapsed && p2RightCollapsed)
                {
                    rightCollapseBlend = 1f;
                }
                else if (p1RightCollapsed && !p2RightCollapsed)
                {
                    float smoothT = Mathf.SmoothStep(0f, 1f, 1f - t);
                    rightCollapseBlend = smoothT;
                }
                else if (!p1RightCollapsed && p2RightCollapsed)
                {
                    float smoothT = Mathf.SmoothStep(0f, 1f, t);
                    rightCollapseBlend = smoothT;
                }

                // Width scaling with edge collapse awareness
                Vector3 widthDir = (right - left).normalized;
                float rawWidth = Vector3.Distance(left, right);

                float widthScale = BlendWidthScale(baseProfiles, bladeT);

                // Calculate the full width (what it should be without collapse)
                float fullWidth = rawWidth * widthScale;

                // Apply width scaling BASED ON COLLAPSE STATE
                if (leftCollapseBlend > 0.99f && rightCollapseBlend < 0.01f)
                {
                    left = center;
                    right = center + widthDir * fullWidth;
                }
                else if (rightCollapseBlend > 0.99f && leftCollapseBlend < 0.01f)
                {
                    right = center;
                    left = center - widthDir * fullWidth;
                }
                else if (splineGen.edgeSettings.edgeCollapseMode != EdgeCollapseMode.None &&
                         (leftCollapseBlend < 0.01f && rightCollapseBlend < 0.01f))
                {
                    // No collapse - symmetric width
                    float halfWidth = fullWidth * 0.5f;
                    left = center - widthDir * halfWidth;
                    right = center + widthDir * halfWidth;
                }
                else
                {
                    // Transitioning between collapse states - blend smoothly
                    float halfWidth = fullWidth * 0.5f;

                    Vector3 symmetricLeft = center - widthDir * halfWidth;
                    Vector3 symmetricRight = center + widthDir * halfWidth;

                    Vector3 leftCollapsedPos = center;
                    Vector3 leftCollapsedRightPos = center + widthDir * fullWidth;

                    Vector3 rightCollapsedPos = center;
                    Vector3 rightCollapsedLeftPos = center - widthDir * fullWidth;

                    if (leftCollapseBlend > 0f)
                    {
                        left = Vector3.Lerp(symmetricLeft, leftCollapsedPos, leftCollapseBlend);
                        right = Vector3.Lerp(symmetricRight, leftCollapsedRightPos, leftCollapseBlend);
                    }

                    if (rightCollapseBlend > 0f)
                    {
                        right = Vector3.Lerp(
                            leftCollapseBlend > 0f ? right : symmetricRight,
                            rightCollapsedPos,
                            rightCollapseBlend
                        );
                        left = Vector3.Lerp(
                            leftCollapseBlend > 0f ? left : symmetricLeft,
                            rightCollapsedLeftPos,
                            rightCollapseBlend
                        );
                    }
                }

                float spineOffset = splineGen.edgeSettings.spineOffset;

                // Store geometric center (before spine offset)
                if (splineGen.edgeSettings.edgeCollapseMode != EdgeCollapseMode.None)
                {
                    float offsetT = (spineOffset + 1f) * 0.5f;
                    center = Vector3.Lerp(left, right, offsetT);
                    smoothGeometricCenters.Add(center);
                }
                else
                {
                    smoothGeometricCenters.Add(originalSegmentCenter);
                }

                // Apply spine offset to create the spine center (used for structure, not thickness)
                if (Mathf.Abs(spineOffset) > 0.001f && leftCollapseBlend < 0.01f && rightCollapseBlend < 0.01f)
                {
                    float offsetT = (spineOffset + 1f) * 0.5f;
                    center = Vector3.Lerp(left, right, offsetT);
                }
                else
                {
                    center = originalSegmentCenter;
                }

                // Curvature smoothing (now uses the offset center)
                Vector3 leftOffset = left - center;
                Vector3 rightOffset = right - center;

                float tipFalloff = isTipSegment ? Mathf.Clamp01(1f - t) : 1f;
                float adjustedBlend = curvatureBlend * tipFalloff;

                ApplyCurvatureSmoothingWithCenter(
                    ref leftOffset,
                    ref rightOffset,
                    center,
                    smoothLefts,
                    smoothRights,
                    smoothCenters,
                    adjustedBlend
                );

                left = center + leftOffset;
                right = center + rightOffset;

                // ===== KINK DETECTION (OPTIONAL DEBUG) =====
                if (smoothCenters.Count > 2)
                {
                    Vector3 prevDir = (smoothCenters[smoothCenters.Count - 1] - smoothCenters[smoothCenters.Count - 2]).normalized;
                    Vector3 currDir = (center - smoothCenters[smoothCenters.Count - 1]).normalized;
                    float angle = Vector3.Angle(prevDir, currDir);

                    if (angle > 45f) // Sharp turn detected
                    {
                        Debug.LogWarning($"Sharp angle detected: {angle}° at ring {ringIndex}, segment {i}, t={t:F3}");
                        Debug.LogWarning($"Left collapse: {leftCollapseBlend:F3}, Right collapse: {rightCollapseBlend:F3}");
                    }
                }

                //if (smoothLefts.Count == 0 && i == 0 && j == 0)
                //{
                //    baseWidth = Vector3.Distance(left, right);
                //}

                smoothCenters.Add(center);
                smoothLefts.Add(left);
                smoothRights.Add(right);
            }
        }

        // ===== POST-GENERATION SMOOTHING PASS =====
        // This helps eliminate any remaining kinks from collapse transitions
        if (smoothCenters.Count > 2)
        {
            List<Vector3> originalCenters = new List<Vector3>(smoothCenters);
            List<Vector3> originalLefts = new List<Vector3>(smoothLefts);
            List<Vector3> originalRights = new List<Vector3>(smoothRights);

            const int SMOOTH_WINDOW = 2;
            const float SMOOTH_STRENGTH = 0.4f;

            for (int i = SMOOTH_WINDOW; i < smoothCenters.Count - SMOOTH_WINDOW; i++)
            {
                Vector3 avgCenter = Vector3.zero;
                Vector3 avgLeft = Vector3.zero;
                Vector3 avgRight = Vector3.zero;
                int count = 0;

                for (int k = -SMOOTH_WINDOW; k <= SMOOTH_WINDOW; k++)
                {
                    avgCenter += originalCenters[i + k];
                    avgLeft += originalLefts[i + k];
                    avgRight += originalRights[i + k];
                    count++;
                }

                avgCenter /= count;
                avgLeft /= count;
                avgRight /= count;

                smoothCenters[i] = Vector3.Lerp(originalCenters[i], avgCenter, SMOOTH_STRENGTH);
                smoothLefts[i] = Vector3.Lerp(originalLefts[i], avgLeft, SMOOTH_STRENGTH);
                smoothRights[i] = Vector3.Lerp(originalRights[i], avgRight, SMOOTH_STRENGTH);
            }
        }
    }
    public void GenerateEdgeGeometry(
        List<Segment> segments,
        List<Vector3> smoothLefts,
        List<Vector3> smoothRights,
        List<Vector3> vertices,
        List<int> triangles, List<Vector3> smoothCenters)
    {
        List<int> ringStarts = new List<int>();
        int ringCount = smoothLefts.Count;

        for (int i = 0; i < ringCount; i++)
        {
            Vector3 left = smoothLefts[i];
            Vector3 right = smoothRights[i];

            ringStarts.Add(vertices.Count);

            for (int s = 0; s < widthSubdivisions; s++)
            {
                float t = s / (float)(widthSubdivisions - 1);
                Vector3 point = Vector3.Lerp(left, right, t);

             
                vertices.Add(point);
            }


        }

        for (int i = 0; i < ringStarts.Count - 1; i++)
        {
            int baseA = ringStarts[i];
            int baseB = ringStarts[i + 1];

            for (int j = 0; j < widthSubdivisions - 1; j++)
            {
                int a = baseA + j;
                int b = baseA + j + 1;
                int c = baseB + j;
                int d = baseB + j + 1;

           


                    triangles.Add(a);
                    triangles.Add(b);
                    triangles.Add(c);

                    triangles.Add(b);
                    triangles.Add(d);
                    triangles.Add(c);


            }
        }

        Segment tipSegment = segments[segments.Count - 1];
        Vector3 tipPoint = tipSegment.center;
        int tipIndex = vertices.Count;
        vertices.Add(tipPoint);

        int finalRingStart = ringStarts[ringStarts.Count - 1];

        for (int i = 0; i < widthSubdivisions - 1; i++)
        {
            triangles.Add(finalRingStart + i);
            triangles.Add(tipIndex);
            triangles.Add(finalRingStart + i + 1);
        }
    }

    private void RecalculateNormalsSmooth(Mesh mesh)
    {
        Vector3[] vertices = mesh.vertices;
        Vector3[] normals = new Vector3[vertices.Length];

        for (int sub = 0; sub < mesh.subMeshCount; sub++)
        {
            int[] triangles = mesh.GetTriangles(sub);

            for (int i = 0; i < triangles.Length; i += 3)
            {
                int i0 = triangles[i];
                int i1 = triangles[i + 1];
                int i2 = triangles[i + 2];

                Vector3 v0 = vertices[i0];
                Vector3 v1 = vertices[i1];
                Vector3 v2 = vertices[i2];

                Vector3 faceNormal = Vector3.Cross(v1 - v0, v2 - v0);

                normals[i0] += faceNormal;
                normals[i1] += faceNormal;
                normals[i2] += faceNormal;
            }
        }

        for (int i = 0; i < normals.Length; i++)
            normals[i] = normals[i].normalized;

        mesh.normals = normals;
    }

    void ApplyCurvatureSmoothingWithCenter(
        ref Vector3 leftOffset,
        ref Vector3 rightOffset,
        Vector3 center,
        List<Vector3> smoothLefts,
        List<Vector3> smoothRights,
        List<Vector3> smoothCenters,
        float blendOverride)
    {
        int count = 0;
        Vector3 avgLeftOffset = Vector3.zero;
        Vector3 avgRightOffset = Vector3.zero;

        for (int k = Mathf.Max(0, smoothCenters.Count - curvatureWindow); k < smoothCenters.Count; k++)
        {
            Vector3 prevCenter = smoothCenters[k];
            Vector3 prevLeftOffset = smoothLefts[k] - prevCenter;
            Vector3 prevRightOffset = smoothRights[k] - prevCenter;

            avgLeftOffset += prevLeftOffset;
            avgRightOffset += prevRightOffset;
            count++;
        }

        if (count > 0)
        {
            avgLeftOffset /= count;
            avgRightOffset /= count;

            leftOffset = Vector3.Lerp(leftOffset, avgLeftOffset, blendOverride);
            rightOffset = Vector3.Lerp(rightOffset, avgRightOffset, blendOverride);
        }
    }

    Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        Vector3 result = 0.5f * (
            2f * p1 +
            (-p0 + p2) * t +
            (2f * p0 - 5f * p1 + 4f * p2 - p3) * t * t +
            (-p0 + 3f * p1 - 3f * p2 + p3) * t * t * t
        );

        // Clamp deviation from linear interpolation
        Vector3 linear = Vector3.Lerp(p1, p2, t);
        float maxDeviation = Vector3.Distance(p1, p2) * 0.5f; // 50% of segment length

        if (Vector3.Distance(result, linear) > maxDeviation)
        {
            result = linear + (result - linear).normalized * maxDeviation;
        }

        return result;
    }

    void SmoothSegmentCenters()
    {
        var segments = splineGen.segments;
        if (segments == null || segments.Count < 3) return;

        for (int i = 1; i < segments.Count - 1; i++)
        {
            Segment s = segments[i];
            s.center = Vector3.Lerp(s.center, (segments[i - 1].center + segments[i + 1].center) * 0.5f, 0.25f);
            segments[i] = s;
        }
    }

    public void CalculateHandandGuardSize()
    {
        if (guard != null)
            guard.transform.localScale = new Vector3(
                baseWidth * 2,
                guard.transform.localScale.y,
                Mathf.Max(bladeThickness, 0.05f) * 1.2f
            );

        if (handle != null)
            handle.transform.localScale = new Vector3(
                baseWidth,
                handle.transform.localScale.y,
                handle.transform.localScale.z
            );

        // CRITICAL FIX: Always align holder with first segment center
        if (holder != null && splineGen != null &&
            splineGen.segments != null && splineGen.segments.Count > 0)
        {
            // Get the ACTUAL first segment center
            Segment firstSegment = splineGen.segments[0];
            Vector3 trueGeometricCenter = (firstSegment.left + firstSegment.right) * 0.5f;

            // Move holder to match blade base center
            holder.transform.localPosition = new Vector3(
                trueGeometricCenter.x,
                holder.transform.localPosition.y,
                holder.transform.localPosition.z
            );

            // Sync HandleXPosition with actual position
            HandleXPosition = trueGeometricCenter.x;
        }
    }


    float EvaluateBladeProfile(
        BladeBaseProfile profile,
        float widthT,
        float halfThickness)
    {
        float x = Mathf.Abs(widthT);

        switch (profile)
        {
            case BladeBaseProfile.Lenticular:
                return Mathf.Cos(x * Mathf.PI * 0.5f) * halfThickness;

            case BladeBaseProfile.Diamond:
                return (1f - x) * halfThickness;

            case BladeBaseProfile.HollowGround:
                return Mathf.Pow(1f - x, 2.2f) * halfThickness;

            case BladeBaseProfile.Flat:
            default:
                return halfThickness;
        }
    }

    List<BladeProfileLayer> GetActiveLayers(List<BladeProfileLayer> layers, float bladeT)
    {
        List<BladeProfileLayer> active = new List<BladeProfileLayer>();

        foreach (var layer in layers)
        {
            if (bladeT >= layer.startHeight && bladeT <= layer.endHeight)
                active.Add(layer);
        }

        return active;
    }
    float BlendThicknessAtOverlap(
        List<BladeProfileLayer> layers,
        float bladeT,
        float widthT,
        float halfThickness)
    {
        var active = GetActiveLayers(layers, bladeT);

        if (active.Count == 0)
            return halfThickness;

        if (active.Count == 1)
        {
            var layer = active[0];
            float layerThickness = EvaluateBladeProfile(layer.profile, widthT, halfThickness);
            return layerThickness * layer.scale;
        }

        active.Sort((a, b) => a.startHeight.CompareTo(b.startHeight));

        // Natural blending with automatic overlap detection
        float totalWeight = 0f;
        float blendedThickness = 0f;

        // Use profileOverlapBlendAmount to control blend zone size
        float blendZone = 0.1f * profileOverlapBlendAmount; // 10% of range * blend strength

        foreach (var layer in active)
        {
            float layerStart = layer.startHeight;
            float layerEnd = layer.endHeight;
            float layerSpan = layerEnd - layerStart;

            // Define blend zones at start and end
            float blendInEnd = layerStart + blendZone;
            float blendOutStart = layerEnd - blendZone;

            float weight = 1f;

            // Fade in at start
            if (bladeT < blendInEnd && bladeT > layerStart)
            {
                float t = (bladeT - layerStart) / blendZone;
                weight *= Mathf.SmoothStep(0f, 1f, t);
            }

            // Fade out at end
            if (bladeT > blendOutStart && bladeT < layerEnd)
            {
                float t = (layerEnd - bladeT) / blendZone;
                weight *= Mathf.SmoothStep(0f, 1f, t);
            }

            float layerThickness = EvaluateBladeProfile(layer.profile, widthT, halfThickness);

            blendedThickness += layerThickness * layer.scale * weight;
            totalWeight += weight;
        }

        return totalWeight > 0f ? blendedThickness / totalWeight : halfThickness;
    }

    float GetWidthScale(BladeBaseProfile profile)
    {
        switch (profile)
        {
            case BladeBaseProfile.Lenticular:
                return 1.0f;   // stays full width

            case BladeBaseProfile.Diamond:
                return 0.85f;  // slightly narrower

            case BladeBaseProfile.HollowGround:
                return 0.75f;  // noticeably narrower

            case BladeBaseProfile.Hexagonal:
                return 1.15f;  // slightly wider

            case BladeBaseProfile.Triangular:
                return 0.3f;   // fully converges to a point at the tip

            case BladeBaseProfile.Flat:
            default:
                return 1.0f;
        }
    }
    float BlendWidthScale(List<BladeProfileLayer> layers, float bladeT)
    {
        float totalScale = 1f;
        float totalInfluence = 0f;

        foreach (var layer in layers)
        {
            if (bladeT < layer.startHeight || bladeT > layer.endHeight)
                continue;

            float tLocal = Mathf.InverseLerp(layer.startHeight, layer.endHeight, bladeT);

            float targetScale = GetWidthScale(layer.profile) * layer.scale;
            float fade = Mathf.SmoothStep(1f, targetScale, tLocal);

            float curveValue = layer.influenceCurve.Evaluate(tLocal);
            float influenced = Mathf.Lerp(1f, fade, curveValue);

            totalScale += (influenced - 1f) * curveValue;
            totalInfluence += curveValue;
        }

        if (totalInfluence > 0f)
            totalScale = Mathf.Lerp(1f, totalScale, Mathf.Clamp01(totalInfluence));

        return Mathf.Max(0f, totalScale);
    }

    //Used for degubbing
    void OnDrawGizmos()
    {
        if (splineGen?.segments == null || splineGen.segments.Count < 2) return;

        Gizmos.color = Color.cyan;
        foreach (var seg in splineGen.segments)
        {
            Gizmos.DrawSphere(transform.TransformPoint(seg.center), 0.005f);
            Gizmos.DrawLine(transform.TransformPoint(seg.left), transform.TransformPoint(seg.right));
        }

        if (Application.isPlaying)
        {
            MeshFilter mf = GetComponent<MeshFilter>();
            if (mf?.mesh == null) return;
            var verts = mf.mesh.vertices;
        }
    }

    private Vector3 GetForwardDir(List<Vector3> smoothCenters, int index)
    {
        if (index < smoothCenters.Count - 1)
            return (smoothCenters[index + 1] - smoothCenters[index]).normalized;

        return (smoothCenters[index] - smoothCenters[index - 1]).normalized;
    }
    private void RegenerateBlade(bool recalcHandle = false)
    {
        ApplyMeshQualitySettings();

        // FIX: Clear existing mesh data
        MeshFilter meshFilter = GetComponent<MeshFilter>();
        if (meshFilter != null && meshFilter.mesh != null)
        {
            meshFilter.mesh.Clear();
        }

        splineGen.GenerateLinesAndSplines();
        SmoothSegmentCenters();
        segments = splineGen.segments;
        if (segments == null) return;
        Generate3DBlade(true);

        if (recalcHandle)
            CalculateHandandGuardSize();

        CalculateHandandGuardSize();
    }

    private float[,] SmoothDepthMap(float[,] depthMap, int ringCount)
    {
       
        float[,] smoothed = new float[ringCount, widthSubdivisions];

        for (int r = 0; r < ringCount; r++)
        {
            for (int v = 0; v < widthSubdivisions; v++)
            {
                float sum = 0f;
                float weightSum = 0f;

                for (int k = Mathf.Max(0, r - smoothWindow);
                     k <= Mathf.Min(ringCount - 1, r + smoothWindow);
                     k++)
                {
                    float w = 1f - Mathf.Abs(k - r) / (smoothWindow + 1f);
                    sum += depthMap[k, v] * w;
                    weightSum += w;
                }

                smoothed[r, v] = sum / weightSum;
            }
        }

        return smoothed;
    }


    private void ApplyHollowFuller(
      List<Vector3> vertices,
      List<int> trianglesFrontBack,
      int frontVertexCount,
      List<Vector3> smoothCenters,
      List<Vector3> smoothLefts,
      List<Vector3> smoothRights)
    {

        int ringCount = smoothCenters.Count;
        hollowHitsLeftPerRing = new bool[ringCount];
        hollowHitsRightPerRing = new bool[ringCount];
        // --- Merge all hollow fullers into a single mask ---
        bool[,] holeMask = new bool[ringCount, widthSubdivisions];

        hollowHitsLeft = false;
        hollowHitsRight = false;
        hollowStartRing = ringCount;
        hollowEndRing = -1;

        // Compute blade length for mapping
        float totalLength = 0f;
        float[] cumulative = new float[ringCount];
        cumulative[0] = 0f;

        for (int i = 1; i < ringCount; i++)
        {
            totalLength += Vector3.Distance(smoothCenters[i], smoothCenters[i - 1]);
            cumulative[i] = totalLength;
        }

        // Build merged hole mask
        foreach (var fuller in fullers)
        {
            if (fuller.fullerType != FullerType.Hollow) continue;

            for (int r = 0; r < ringCount; r++)
            {
                float bladeT = totalLength > 0f ? cumulative[r] / totalLength : 0f;
                if (bladeT < fuller.start || bladeT > fuller.end) continue;

                float centerPos = fuller.fullerCenter * 2f - 1f;

                for (int v = 0; v < widthSubdivisions; v++)
                {
                    float t = v / (float)(widthSubdivisions - 1);
                    float widthPos = t * 2f - 1f;

                    float dist = Mathf.Abs(widthPos - centerPos) / fuller.fullerWidth;
                    if (dist <= 1f)
                    {
                        holeMask[r, v] = true;

                        //if (v == 0)
                        //    hollowHitsLeftPerRing[r] = true;

                        //if (v == widthSubdivisions - 1)
                        //    hollowHitsRightPerRing[r] = true;

                        hollowStartRing = Mathf.Min(hollowStartRing, r);
                        hollowEndRing = Mathf.Max(hollowEndRing, r);
                    }
                   
                }
            }
        }

        // If no hollow was actually marked, bail early
        if (hollowEndRing < 0 || hollowStartRing >= ringCount)
            return;

        // --- Remove front/back triangles inside holes ---
        List<int> newTriangles = new List<int>();

        for (int i = 0; i < trianglesFrontBack.Count; i += 3)
        {
            int a = trianglesFrontBack[i];
            int b = trianglesFrontBack[i + 1];
            int c = trianglesFrontBack[i + 2];

            bool isBack = a >= frontVertexCount;
            int fa = isBack ? a - frontVertexCount : a;
            int fb = isBack ? b - frontVertexCount : b;
            int fc = isBack ? c - frontVertexCount : c;

            int ra = fa / widthSubdivisions;
            int rb = fb / widthSubdivisions;
            int rc = fc / widthSubdivisions;

            int va = fa % widthSubdivisions;
            int vb = fb % widthSubdivisions;
            int vc = fc % widthSubdivisions;

            // Skip triangles that don't map to the grid
            if (ra < 0 || ra >= ringCount || va < 0 || va >= widthSubdivisions ||
                rb < 0 || rb >= ringCount || vb < 0 || vb >= widthSubdivisions ||
                rc < 0 || rc >= ringCount || vc < 0 || vc >= widthSubdivisions)
            {
                newTriangles.Add(a);
                newTriangles.Add(b);
                newTriangles.Add(c);
                continue;
            }

            if (!(holeMask[ra, va] && holeMask[rb, vb] && holeMask[rc, vc]))
            {
                newTriangles.Add(a);
                newTriangles.Add(b);
                newTriangles.Add(c);
            }
        }

        trianglesFrontBack.Clear();
        trianglesFrontBack.AddRange(newTriangles);

        // --- Build inner walls ---
        List<int> wallTris = new List<int>();

        void AddWall(int a0, int a1, bool flip = false)
        {
            int b0 = a0 + frontVertexCount;
            int b1 = a1 + frontVertexCount;

            int a0w = vertices.Count;
            vertices.Add(vertices[a0]);
            uvs.Add(uvs[a0]);

            int a1w = vertices.Count;
            vertices.Add(vertices[a1]);
            uvs.Add(uvs[a1]);

            int b0w = vertices.Count;
            vertices.Add(vertices[a0 + frontVertexCount]);
            uvs.Add(uvs[a0 + frontVertexCount]);

            int b1w = vertices.Count;
            vertices.Add(vertices[a1 + frontVertexCount]);
            uvs.Add(uvs[a1 + frontVertexCount]);


            if (!flip)
            {
                wallTris.Add(a0); wallTris.Add(a1); wallTris.Add(b0);
                wallTris.Add(a1); wallTris.Add(b1); wallTris.Add(b0);
            }
            else
            {
                wallTris.Add(a0); wallTris.Add(b0); wallTris.Add(a1);
                wallTris.Add(a1); wallTris.Add(b0); wallTris.Add(b1);
            }
        }

        // Compute per-ring hole ranges
        int[] holeMin = new int[ringCount];
        int[] holeMax = new int[ringCount];

        for (int r = 0; r < ringCount; r++)
        {
            int minV = widthSubdivisions;
            int maxV = -1;

            for (int v = 0; v < widthSubdivisions; v++)
            {
                if (holeMask[r, v])
                {
                    minV = Mathf.Min(minV, v);
                    maxV = Mathf.Max(maxV, v);
                }
            }

            if (maxV >= minV)
            {
                holeMin[r] = minV;
                holeMax[r] = Mathf.Clamp(maxV, 0, widthSubdivisions - 1);
            }
            else
            {
                holeMin[r] = -1;
                holeMax[r] = -1;
            }
        }
        // --- Build horizontal caps at ALL discontinuities ---
        // --- Build vertical walls between ALL consecutive rings with holes ---
        for (int r = hollowStartRing; r < hollowEndRing && r + 1 < ringCount; r++)
        {
            // Only build walls if BOTH this ring and next ring have holes
            if (holeMin[r] < 0 || holeMin[r + 1] < 0) continue;

            // Left wall
            AddWall(r * widthSubdivisions + holeMin[r],
                    (r + 1) * widthSubdivisions + holeMin[r + 1], true);

            // Right wall
            AddWall(r * widthSubdivisions + holeMax[r],
                    (r + 1) * widthSubdivisions + holeMax[r + 1], false);
        }

        for (int r = hollowStartRing; r <= hollowEndRing; r++)
        {
            if (holeMin[r] < 0) continue;

            bool startCap = r == 0 || holeMin[r - 1] < 0;
            bool endCap = r == ringCount - 1 || holeMin[r + 1] < 0;

            // Compute correct blade plane
            Vector3 widthDir = (smoothRights[r] - smoothLefts[r]).normalized;
            Vector3 forwardDir = GetForwardDir(smoothCenters, r);
            Vector3 ringNormal = Vector3.Cross(widthDir, forwardDir).normalized;
            Vector3 planePoint = smoothCenters[r];


            for (int v = holeMin[r]; v < holeMax[r]; v++)
            {
                int a = r * widthSubdivisions + v;
                int b = r * widthSubdivisions + v + 1;

                // PROJECT FRONT + BACK VERTICES
                vertices[a] = ProjectToPlane(vertices[a], planePoint, ringNormal);
                vertices[b] = ProjectToPlane(vertices[b], planePoint, ringNormal);

                vertices[a + frontVertexCount] =
                    ProjectToPlane(vertices[a + frontVertexCount], planePoint, ringNormal);

                vertices[b + frontVertexCount] =
                    ProjectToPlane(vertices[b + frontVertexCount], planePoint, ringNormal);

                if (startCap)
                    AddWall(a, b, false);

                if (endCap)
                    AddWall(a, b, true);
            }
        }

        for (int i = 0; i < ringCount; i++)
        {
            if (holeMin[i] >= 0)
            {
                hollowHitsLeftPerRing[i] = (holeMin[i] == 0);
                hollowHitsRightPerRing[i] = (holeMax[i] == widthSubdivisions - 1);
            }
        }

        // Additionally mark the ring BEFORE the hollow starts
        if (hollowStartRing > 0)
        {
            if (holeMin[hollowStartRing] == 0)
                hollowHitsLeftPerRing[hollowStartRing - 1] = true;

            if (holeMax[hollowStartRing] == widthSubdivisions - 1)
                hollowHitsRightPerRing[hollowStartRing - 1] = true;
        }
        trianglesFrontBack.AddRange(wallTris);
    }

    Vector3 ProjectToPlane(Vector3 p, Vector3 planePoint, Vector3 planeNormal)
    {
        float d = Vector3.Dot(p - planePoint, planeNormal);
        return p - planeNormal * d;
    }

    //FOR SAVING DATA 
    public BladeGenerationData GetData()
    {
        return new BladeGenerationData
        {
            baseProfiles = new List<BladeProfileLayer>(baseProfiles),
            profileOverlapBlendAmount = profileOverlapBlendAmount,
            meshQuality = meshQuality,
            handleXPosition = HandleXPosition,
            bladeThickness = bladeThickness,
            edgeSharpness = edgeSharpness,
            spineThickness = spineThickness,
            sharpSide = sharpSide,
            fullers = fullers != null
                ? new List<FullerSettings>(fullers)
                : new List<FullerSettings>()
        };
    }

    void CopyFields(object src, object dst)
    {
        if (src == null || dst == null) return;

        var fields = src.GetType()
            .GetFields(BindingFlags.Public | BindingFlags.Instance);

        foreach (var f in fields)
        {
            if (f.FieldType == typeof(AnimationCurve))
            {
                AnimationCurve c = (AnimationCurve)f.GetValue(src);
                f.SetValue(dst, c != null ? new AnimationCurve(c.keys) : null);
            }
            else
            {
                f.SetValue(dst, f.GetValue(src));
            }
        }
    }

    public void ApplyData(BladeGenerationData data)
    {
        if (data == null) return;

        // ---------- Base profiles ----------
        if (baseProfiles == null)
            baseProfiles = new List<BladeProfileLayer>();

        while (baseProfiles.Count < data.baseProfiles.Count)
            baseProfiles.Add(new BladeProfileLayer());

        while (baseProfiles.Count > data.baseProfiles.Count)
            baseProfiles.RemoveAt(baseProfiles.Count - 1);

        for (int i = 0; i < data.baseProfiles.Count; i++)
            CopyFields(data.baseProfiles[i], baseProfiles[i]);

        // ---------- Fullers ----------
        if (fullers == null)
            fullers = new List<FullerSettings>();

        while (fullers.Count < data.fullers.Count)
            fullers.Add(new FullerSettings());

        while (fullers.Count > data.fullers.Count)
            fullers.RemoveAt(fullers.Count - 1);

        for (int i = 0; i < data.fullers.Count; i++)
            CopyFields(data.fullers[i], fullers[i]);

        // ---------- Simple fields ----------
        profileOverlapBlendAmount = data.profileOverlapBlendAmount;
        meshQuality = data.meshQuality;
        HandleXPosition = data.handleXPosition;
        bladeThickness = data.bladeThickness;
        edgeSharpness = data.edgeSharpness;
        spineThickness = data.spineThickness;
        sharpSide = data.sharpSide;

        RegenerateBlade(true);
    }

    public void RandomGeneration()
    {
        splineGen.SetRandomParamaters();
        SetRandomParamaters();
        RegenerateBlade(true);

        if (swordShaderControl != null)
            swordShaderControl.RandomizeShader();
        if(hiltCreation != null)  
            hiltCreation.RandomiseGuard(baseWidth, bladeThickness);


    }

    private void SetRandomParamaters()
    {
        // 90% chance of realistic sword, 10% chance of experimental/fantasy
        bool isRealistic = UnityEngine.Random.value < 0.9f;

        // Define ranges based on realism
        float thicknessMin = isRealistic ? 0.04f : 0.03f;
        float thicknessMax = isRealistic ? 0.08f : 0.15f;

        float sharpnessMin = isRealistic ? 0.02f : 0.01f;
        float sharpnessMax = isRealistic ? 0.02f : 0.05f;

        //float spineMin = isRealistic ? 0.003f : 0.001f;
        //float spineMax = isRealistic ? 0.015f : 0.05f;

        float handleMin = isRealistic ? -0.1f : -0.3f;
        float handleMax = isRealistic ? 0.1f : 0.3f;

        // Apply random values within ranges
        bladeThickness = UnityEngine.Random.Range(thicknessMin, thicknessMax);
        edgeSharpness = UnityEngine.Random.Range(sharpnessMin, sharpnessMax);
        //spineThickness = UnityEngine.Random.Range(spineMin, spineMax);
        HandleXPosition = UnityEngine.Random.Range(handleMin, handleMax);

        // Profile overlap blend
        profileOverlapBlendAmount = UnityEngine.Random.Range(0.3f, 1.0f);

        // Sharp side
        float sharpRoll = UnityEngine.Random.value;

        if (splineGen.useSymmetry)
        {
            sharpSide = SharpSide.Both;
        }
        else
        {
            sharpSide = isRealistic && sharpRoll < 0.5f ? SharpSide.Both :
                        (UnityEngine.Random.value < 0.5f ? SharpSide.Left : SharpSide.Right);
        }
            

        // Mesh quality
        //float qualityRoll = UnityEngine.Random.value;
        //meshQuality = qualityRoll < 0.1f ? MeshQuality.Low :
        //              qualityRoll < 0.5f ? MeshQuality.Medium :
        //              qualityRoll < 0.9f ? MeshQuality.High : MeshQuality.Ultra;

        // Generate profiles and fullers
        GenerateRandomProfiles(isRealistic);
        GenerateRandomFullers(isRealistic);
    }

    private void GenerateRandomProfiles(bool isRealistic)
    {
        baseProfiles.Clear();

        float countRoll = UnityEngine.Random.value;
        int profileCount = isRealistic ? (countRoll < 0.8f ? 1 : countRoll < 0.9f ? 2 : 3) :
                                         UnityEngine.Random.Range(1, 4);

        BladeBaseProfile[] availableProfiles = isRealistic ?
            new BladeBaseProfile[] { BladeBaseProfile.Lenticular, BladeBaseProfile.Diamond,
                          BladeBaseProfile.HollowGround } :
            (BladeBaseProfile[])System.Enum.GetValues(typeof(BladeBaseProfile));

        float currentStart = 0f;
        BladeBaseProfile lastProfile = BladeBaseProfile.Lenticular;
        bool isFirstProfile = true;

        for (int i = 0; i < profileCount; i++)
        {
            bool isLast = i == profileCount - 1;

            float sectionLength = (1f - currentStart) / (profileCount - i);
            float endHeight = isLast ? 1f : currentStart +
                UnityEngine.Random.Range(sectionLength * (isRealistic ? 0.8f : 0.5f),
                                         sectionLength * (isRealistic ? 1.2f : 1.5f));

            endHeight = Mathf.Min(endHeight, 0.95f);

            float overlapAmount = isRealistic ? UnityEngine.Random.Range(0.1f, 0.2f) :
                                               UnityEngine.Random.Range(0.05f, 0.3f);

            float finalEndHeight = isLast ? 1f : Mathf.Min(endHeight + overlapAmount, 1f);

            if (finalEndHeight <= currentStart)
            {
                Debug.LogWarning($"Invalid profile range: start={currentStart}, end={finalEndHeight}");
                continue;
            }

            // ========== ENHANCED SELECTION WITH VISUAL CONTRAST ==========
            BladeBaseProfile selectedProfile;

            if (profileCount == 1 || isFirstProfile)
            {
                selectedProfile = availableProfiles[UnityEngine.Random.Range(0, availableProfiles.Length)];
                isFirstProfile = false;
            }
            else
            {
                // Create weighted list - profiles that contrast more with previous get higher weight
                List<BladeBaseProfile> candidates = new List<BladeBaseProfile>();
                List<float> weights = new List<float>();

                foreach (var p in availableProfiles)
                {
                    if (p == lastProfile)
                        continue; // Skip identical

                    // Calculate "visual contrast" - how different the shapes are
                    float contrast = GetProfileContrast(lastProfile, p);

                    candidates.Add(p);
                    weights.Add(contrast);
                }

                if (candidates.Count == 0)
                    candidates = new List<BladeBaseProfile>(availableProfiles);

                // Weighted random selection
                selectedProfile = WeightedRandomProfile(candidates, weights);
            }
            // ============================================================

            baseProfiles.Add(new BladeProfileLayer
            {
                profile = selectedProfile,
                startHeight = currentStart,
                endHeight = finalEndHeight,
                scale = UnityEngine.Random.Range(isRealistic ? 0.9f : 0.5f,
                                                isRealistic ? 1.15f : 1.5f),
                influenceCurve = isRealistic ? AnimationCurve.EaseInOut(0f, 0f, 1f, 1f) :
                                              GenerateRandomCurve()
            });

            lastProfile = selectedProfile;
            currentStart = endHeight;
        }

        if (baseProfiles.Count == 0)
        {
            baseProfiles.Add(new BladeProfileLayer
            {
                profile = BladeBaseProfile.Lenticular,
                startHeight = 0f,
                endHeight = 1f,
                scale = 1f,
                influenceCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f)
            });
        }
    }

    // Helper: Calculate visual contrast between two profiles
    private float GetProfileContrast(BladeBaseProfile a, BladeBaseProfile b)
    {
        // Define shape "families" - similar shapes have low contrast
        // Diamond/Triangular are angular
        // Lenticular/HollowGround are curved
        // Flat/Hexagonal are geometric

        if (a == b) return 0f;

        // High contrast pairs (1.0)
        if ((a == BladeBaseProfile.Diamond && b == BladeBaseProfile.Lenticular) ||
            (a == BladeBaseProfile.Lenticular && b == BladeBaseProfile.Diamond) ||
            (a == BladeBaseProfile.Triangular && b == BladeBaseProfile.HollowGround) ||
            (a == BladeBaseProfile.HollowGround && b == BladeBaseProfile.Triangular))
            return 1.0f;

        // Medium contrast (0.7f)
        if ((a == BladeBaseProfile.Flat && (b == BladeBaseProfile.Lenticular || b == BladeBaseProfile.Diamond)) ||
            (b == BladeBaseProfile.Flat && (a == BladeBaseProfile.Lenticular || a == BladeBaseProfile.Diamond)))
            return 0.7f;

        // Default medium-high contrast
        return 0.8f;
    }

    // Helper: Weighted random selection
    private BladeBaseProfile WeightedRandomProfile(List<BladeBaseProfile> profiles, List<float> weights)
    {
        if (profiles.Count == 0)
            return BladeBaseProfile.Lenticular;

        if (weights == null || weights.Count != profiles.Count)
        {
            // Fallback to uniform random
            return profiles[UnityEngine.Random.Range(0, profiles.Count)];
        }

        float totalWeight = 0f;
        foreach (float w in weights)
            totalWeight += w;

        float random = UnityEngine.Random.Range(0f, totalWeight);
        float cumulative = 0f;

        for (int i = 0; i < profiles.Count; i++)
        {
            cumulative += weights[i];
            if (random <= cumulative)
                return profiles[i];
        }

        return profiles[profiles.Count - 1];
    }
    private void GenerateRandomFullers(bool isRealistic)
    {
        fullers = fullers ?? new List<FullerSettings>();
        fullers.Clear();

        const int TOTAL_FULLER_SLOTS = 4;
        bool hasCircularFuller = false;

        // 80% chance of exactly one basic fuller, 20% chance of other configurations
        float configRoll = UnityEngine.Random.value;
        int activeFullerCount = 0;

        if (configRoll < 0.8f)
        {
            // STANDARD CASE: Single basic fuller
            float fullerStart = UnityEngine.Random.value < 0.8f ? 0f : UnityEngine.Random.Range(0.05f, 0.2f);
            float fullerEnd = UnityEngine.Random.Range(0.6f, 0.9f);

            fullers.Add(new FullerSettings
            {
                fullerType = FullerType.Basic,
                start = fullerStart,
                end = fullerEnd,
                fullerDepth = UnityEngine.Random.Range(0.15f, isRealistic ? 0.3f : 0.4f),
                fullerWidth = UnityEngine.Random.Range(0.15f, 0.35f),
                fullerCenter = UnityEngine.Random.Range(0.4f, 0.6f),
                circleRadius = 0.3f,
                fullerFalloff = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f)
            });

            activeFullerCount = 1;
        }
        else
        {
            // ALTERNATIVE CASES
            float altRoll = UnityEngine.Random.value;

            if (altRoll < 0.25f)
            {
                activeFullerCount = 0; // no fullers
            }
            else if (altRoll < 0.75f)
            {
                // Single non-basic fuller
                FullerType type = UnityEngine.Random.value < 0.5f ? FullerType.Hollow : FullerType.Hollow_Circular;

                if (type == FullerType.Hollow_Circular)
                    hasCircularFuller = true;

                fullers.Add(GenerateRandomFuller(type, null, isRealistic));
                activeFullerCount = 1;
            }
            else
            {
                // TWO OR MORE FULLERS
                bool twoBasics = UnityEngine.Random.value < 0.3f;

                // First fuller - always basic
                fullers.Add(GenerateRandomFuller(FullerType.Basic, null, isRealistic));
                activeFullerCount = 1;

                // Second fuller
                FullerType secondType = twoBasics ? FullerType.Basic :
                    (UnityEngine.Random.value < 0.5f ? FullerType.Hollow : FullerType.Hollow_Circular);

                if (secondType == FullerType.Hollow_Circular)
                    hasCircularFuller = true;

                fullers.Add(GenerateRandomFuller(secondType, fullers[0], isRealistic));
                activeFullerCount++;

                // POSSIBLE 3rd and 4th FULLERS
                while (activeFullerCount < 4)
                {
                    // 50% chance to add another fuller
                    if (UnityEngine.Random.value < 0.5f)
                    {
                        // Random type but respect the "one circular only" rule
                        FullerType type;
                        if (hasCircularFuller)
                            type = UnityEngine.Random.value < 0.5f ? FullerType.Basic : FullerType.Hollow;
                        else
                            type = UnityEngine.Random.value < 0.33f ? FullerType.Basic :
                                   (UnityEngine.Random.value < 0.66f ? FullerType.Hollow : FullerType.Hollow_Circular);

                        if (type == FullerType.Hollow_Circular)
                            hasCircularFuller = true;

                        fullers.Add(GenerateRandomFuller(type, fullers[activeFullerCount - 1], isRealistic));
                        activeFullerCount++;
                    }
                    else break;
                }
            }
        }

        // Fill remaining slots with None
        while (fullers.Count < TOTAL_FULLER_SLOTS)
        {
            fullers.Add(new FullerSettings
            {
                fullerType = FullerType.None,
                start = 0.1f,
                end = 0.8f,
                fullerDepth = 0.3f,
                fullerWidth = 0.3f,
                fullerCenter = 0.5f,
                circleRadius = 0.3f,
                fullerFalloff = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f)
            });
        }
    }

    // Helper method unchanged except adding "previous" logic
    private FullerSettings GenerateRandomFuller(FullerType type, FullerSettings previous, bool isRealistic)
    {
        float start = UnityEngine.Random.Range(0.1f, 0.8f);
        float end = UnityEngine.Random.Range(start + 0.1f, 0.95f);

        float center;
        float radius;

        if (type == FullerType.Hollow_Circular)
        {
            float positionRoll = UnityEngine.Random.value;
            if (positionRoll < 0.33f)
            {
                center = 0.5f;
                radius = UnityEngine.Random.Range(0.3f, 0.5f);
            }
            else if (positionRoll < 0.66f)
            {
                center = UnityEngine.Random.Range(0.05f, 0.2f);
                radius = UnityEngine.Random.Range(0.25f, 0.65f);
            }
            else
            {
                center = UnityEngine.Random.Range(0.8f, 0.95f);
                radius = UnityEngine.Random.Range(0.25f, 0.65f);
            }
        }
        else
        {
            center = previous != null && previous.fullerCenter > 0.5f ? UnityEngine.Random.Range(0.4f, 0.6f) : UnityEngine.Random.Range(0.3f, 0.7f);
            radius = UnityEngine.Random.Range(0.12f, 0.35f);
        }

        return new FullerSettings
        {
            fullerType = type,
            start = start,
            end = end,
            fullerDepth = UnityEngine.Random.Range(0.15f, isRealistic ? 0.3f : 0.4f),
            fullerWidth = UnityEngine.Random.Range(0.1f, 0.35f),
            fullerCenter = center,
            circleRadius = radius,
            fullerFalloff = isRealistic ? AnimationCurve.EaseInOut(0f, 1f, 1f, 0f) : GenerateRandomCurve()
        };
    }


    private AnimationCurve GenerateRandomCurve()
    {
        // Generate smooth random curves
        int keyCount = UnityEngine.Random.Range(3, 10);
        Keyframe[] keys = new Keyframe[keyCount];

        keys[0] = new Keyframe(0f, UnityEngine.Random.Range(0f, 1f));
        keys[keyCount - 1] = new Keyframe(1f, UnityEngine.Random.Range(0f, 1f));

        for (int i = 1; i < keyCount - 1; i++)
        {
            float time = i / (float)(keyCount - 1);
            keys[i] = new Keyframe(time, UnityEngine.Random.Range(0f, 1f));
        }

        AnimationCurve curve = new AnimationCurve(keys);

        // Smooth the curve
        for (int i = 0; i < keyCount; i++)
        {
            curve.SmoothTangents(i, 0.5f);
        }

        return curve;
    }

 
}