using System;
using System.Collections.Generic;
using UnityEngine;




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

    [Range(-2, 2), DisplayName("Handle X Position", "General", 2, "Position")]
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
        Hollow
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

    }
   


    [DisplayName("Fuller Settings", "Fullers", 20, "General")]
    public List<FullerSettings> fullers;

    private float[,] holeMask;
    private bool hollowHitsLeft;
    private bool hollowHitsRight;
    private int hollowStartRing;
    private int hollowEndRing;

    bool[] hollowHitsLeftPerRing;
    bool[] hollowHitsRightPerRing;

    public enum SharpSide
    {
        Left,
        Right,
        Both
    }

    [DisplayName("Sharp Edge", "Edge & Spine", 6, "Edge")]
    public SharpSide sharpSide = SharpSide.Both;

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

        if (handle.transform.localPosition.x != HandleXPosition)
        {
            holder.transform.localPosition = new Vector3(HandleXPosition, holder.transform.localPosition.y, holder.transform.localPosition.z);
        }
    }

    private void ApplyMeshQualitySettings()
    {
        switch (meshQuality)
        {
            case MeshQuality.Low:
                segmentSubdivisions = 3;
                tipSubdivisions = 2;
                widthSubdivisions = 3;
                break;
            case MeshQuality.Medium:
                segmentSubdivisions = 8;
                tipSubdivisions = 7;
                widthSubdivisions = 8;
                break;
            case MeshQuality.High:
                segmentSubdivisions = 15;
                tipSubdivisions = 12;
                widthSubdivisions = 15;
                break;
            case MeshQuality.Ultra:
                segmentSubdivisions = 30;
                tipSubdivisions = 20;
                widthSubdivisions = 30;
                break;
        }
    }

    public void Generate()
    {
        RegenerateBlade(true);
    }

    public void Generate3DBlade()
    {
        Mesh mesh3D = new Mesh();
        List<Vector3> vertices = new List<Vector3>();
        List<int> trianglesFrontBack = new List<int>();
        List<int> trianglesSharp = new List<int>();

        var segments = splineGen.segments;
        if (segments == null) return;

        List<Vector3> smoothLefts = new List<Vector3>();
        List<Vector3> smoothRights = new List<Vector3>();
        List<Vector3> smoothCenters = new List<Vector3>();
        GenerateSmoothSegments(segments, smoothLefts, smoothRights, smoothCenters);


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
            trianglesFrontBack
        );

        Vector3 widthDir = (smoothRights[0] - smoothLefts[0]).normalized;
        Vector3 forwardDir = (smoothCenters[1] - smoothCenters[0]).normalized;
        Vector3 bladeNormal = Vector3.Cross(widthDir, forwardDir).normalized;
        int ringCount = smoothLefts.Count;

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

        // 4. Connect bevels to blade faces
        ConnectBevels(
            trianglesFrontBack,
            trianglesSharp,
            frontVertexCount,
            sharpStartFront,
            sharpStartBack,
            ringCount
        );


        ApplyFullers(
               vertices,
               smoothLefts,
               smoothRights,
               smoothCenters,
               bladeNormal,
               frontVertexCount,
               trianglesFrontBack
           );
        




        // 5. Final mesh
        mesh3D.SetVertices(vertices);
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

        float[,] depthMap = new float[ringCount, widthSubdivisions];

        float fadeInLength = 0.05f;
        float fadeOutLength = 0.05f;

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

            float depth = fuller.fullerDepth * (bladeThickness * 0.5f) * lengthMask;
            float width = fuller.fullerWidth;
            int count = 1;

            if (depth <= 0f || width <= 0f)
                continue;

            // FIXED: Fuller positioning - fullerCenter maps directly to blade position
            // 0.0 = left edge, 0.5 = center, 1.0 = right edge
            float centerPos = fuller.fullerCenter * 2f - 1f; // Convert 0-1 to -1 to 1

            for (int vertIdx = 0; vertIdx < widthSubdivisions; vertIdx++)
            {
                float widthT = vertIdx / (float)(widthSubdivisions - 1);
                float widthPos = widthT * 2f - 1f; // -1 (left) to 1 (right)

                // Calculate distance from this vertex to the fuller center
                float dist = Mathf.Abs(widthPos - centerPos) / width;

                if (dist > 1f)
                    continue;

                float falloff = fuller.fullerFalloff.Evaluate(dist);
                float fullerDepth = depth * falloff;

                depthMap[ringIdx, vertIdx] = Mathf.Max(depthMap[ringIdx, vertIdx], fullerDepth);
            }
        }
        float[,] smoothedDepth = SmoothDepthMap(depthMap, ringCount);


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
                float d = smoothedDepth[ringIdx, v];

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
            bool leftBevelActive =
      !(i < hollowHitsLeftPerRing.Length && hollowHitsLeftPerRing[i]);

            bool rightBevelActive =
                !(i < hollowHitsRightPerRing.Length && hollowHitsRightPerRing[i]);

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

            if (leftBevelActive)
            {
                leftList.Add(frontA); leftList.Add(frontB); leftList.Add(sharpLeftFrontA);
                leftList.Add(frontB); leftList.Add(sharpLeftFrontB); leftList.Add(sharpLeftFrontA);
                leftList.Add(backB); leftList.Add(backA); leftList.Add(sharpLeftBackA);
                leftList.Add(backB); leftList.Add(sharpLeftBackA); leftList.Add(sharpLeftBackB);
                leftList.Add(sharpLeftFrontA); leftList.Add(sharpLeftFrontB); leftList.Add(sharpLeftBackA);
                leftList.Add(sharpLeftFrontB); leftList.Add(sharpLeftBackB); leftList.Add(sharpLeftBackA);
            }
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
            Vector3 center = smoothCenters[ring]; // This is the spine position (offset)

            Vector3 widthDir = (right - left).normalized;

            Vector3 forwardDir = GetForwardDir(smoothCenters, ring);

            Vector3 bladeNormal = Vector3.Cross(widthDir, forwardDir).normalized;

            int ringStart = ring * widthSubdivisions;

            for (int v = 0; v < widthSubdivisions; v++)
            {
                int frontIndex = ringStart + v;

                // Standard t from left (0) to right (1)
                float t = v / (float)(widthSubdivisions - 1);

                // Get the actual vertex position
                Vector3 vertexPos = Vector3.Lerp(left, right, t);

                // Calculate where the spine is in the 0-1 range
                float spineT = (spineOffset + 1f) * 0.5f; // Convert -1..1 to 0..1

                // Calculate widthT RELATIVE TO THE SPINE
                // At spine: widthT = 0, At edges: widthT = ±1
                float widthT;
                if (t < spineT) // Left of spine
                {
                    // Map from 0..spineT to -1..0
                    widthT = (t / Mathf.Max(spineT, 0.0001f)) - 1f;
                }
                else // Right of spine
                {
                    // Map from spineT..1 to 0..1
                    widthT = (t - spineT) / Mathf.Max(1f - spineT, 0.0001f);
                }

                float baseHalf = bladeThickness * 0.5f;

                // Use the blend function with profiles
                // widthT is now relative to spine, so spine = 0 (thickest)
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

            //if (holeMask[ringA, vA] > 0.5f &&
            //    holeMask[ringB, vB] > 0.5f &&
            //    holeMask[ringC, vC] > 0.5f)
            //    continue;

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



    public void GenerateSmoothSegments(
     List<Segment> segments,
     List<Vector3> smoothLefts,
     List<Vector3> smoothRights,
     List<Vector3> smoothCenters)
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

                Vector3 center = CatmullRom(p0.center, p1.center, p2.center, p3.center, t);
                Vector3 left = CatmullRom(p0.left, p1.left, p2.left, p3.left, t);
                Vector3 right = CatmullRom(p0.right, p1.right, p2.right, p3.right, t);

                float bladeT = ringIndex / (float)(totalRings - 1);
                ringIndex++;

                // Calculate collapse blend factors for smooth transitions
                float leftCollapseBlend = 0f;
                float rightCollapseBlend = 0f;

                if (p1LeftCollapsed && p2LeftCollapsed)
                {
                    leftCollapseBlend = 1f; // Fully collapsed throughout
                }
                else if (p1LeftCollapsed && !p2LeftCollapsed)
                {
                    leftCollapseBlend = 1f - t; // Fade out collapse
                }
                else if (!p1LeftCollapsed && p2LeftCollapsed)
                {
                    leftCollapseBlend = t; // Fade in collapse
                }

                if (p1RightCollapsed && p2RightCollapsed)
                {
                    rightCollapseBlend = 1f;
                }
                else if (p1RightCollapsed && !p2RightCollapsed)
                {
                    rightCollapseBlend = 1f - t;
                }
                else if (!p1RightCollapsed && p2RightCollapsed)
                {
                    rightCollapseBlend = t;
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
                    // Left fully collapsed - right should be full width
                    left = center;
                    right = center + widthDir * fullWidth;
                }
                else if (rightCollapseBlend > 0.99f && leftCollapseBlend < 0.01f)
                {
                    // Right fully collapsed - left should be full width
                    right = center;
                    left = center - widthDir * fullWidth;
                }
                else if (leftCollapseBlend < 0.01f && rightCollapseBlend < 0.01f)
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

                    // Start with symmetric positions
                    Vector3 symmetricLeft = center - widthDir * halfWidth;
                    Vector3 symmetricRight = center + widthDir * halfWidth;

                    // Calculate collapsed positions (non-collapsed side gets FULL width)
                    Vector3 leftCollapsedPos = center;
                    Vector3 leftCollapsedRightPos = center + widthDir * fullWidth;

                    Vector3 rightCollapsedPos = center;
                    Vector3 rightCollapsedLeftPos = center - widthDir * fullWidth;

                    // Blend between states
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

                // Apply spine offset ONLY if no edge collapse is active at this ring
                float spineOffset = splineGen.edgeSettings.spineOffset;
                if (Mathf.Abs(spineOffset) > 0.001f && leftCollapseBlend < 0.01f && rightCollapseBlend < 0.01f)
                {
                    float offsetT = (spineOffset + 1f) * 0.5f;
                    center = Vector3.Lerp(left, right, offsetT);
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

                if (smoothLefts.Count == 0 && i == 0 && j == 0)
                {
                    baseWidth = Vector3.Distance(left, right);
                }

                smoothCenters.Add(center);
                smoothLefts.Add(left);
                smoothRights.Add(right);
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
        return 0.5f * (
            2f * p1 +
            (-p0 + p2) * t +
            (2f * p0 - 5f * p1 + 4f * p2 - p3) * t * t +
            (-p0 + 3f * p1 - 3f * p2 + p3) * t * t * t
        );
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
            guard.transform.localScale = new Vector3(baseWidth * 2, guard.transform.localScale.y, Mathf.Max(bladeThickness, 0.05f) * 1.2f);
        if (handle != null)
            handle.transform.localScale = new Vector3(baseWidth, handle.transform.localScale.y, handle.transform.localScale.z);
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
            return EvaluateBladeProfile(active[0].profile, widthT, halfThickness);

        active.Sort((a, b) => a.startHeight.CompareTo(b.startHeight));

        BladeProfileLayer lower = active[0];
        BladeProfileLayer upper = active[1];

        float overlapStart = upper.startHeight;
        float overlapEnd = lower.endHeight;

        float tLocal = Mathf.InverseLerp(overlapStart, overlapEnd, bladeT);

        float blendAmount = upper.influenceCurve.Evaluate(tLocal);

        float lowerShape = EvaluateBladeProfile(lower.profile, widthT, halfThickness);
        float upperShape = EvaluateBladeProfile(upper.profile, widthT, halfThickness);


        return Mathf.Lerp(lowerShape, upperShape, blendAmount) * lower.scale;
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
        splineGen.GenerateLinesAndSplines();
        SmoothSegmentCenters();
        Generate3DBlade();

        if (recalcHandle)
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

                        if (v == 0)
                            hollowHitsLeftPerRing[r] = true;

                        if (v == widthSubdivisions - 1)
                            hollowHitsRightPerRing[r] = true;

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

    public void ApplyData(BladeGenerationData data)
    {
        if (data == null) return;

        baseProfiles = new List<BladeProfileLayer>(data.baseProfiles);
        profileOverlapBlendAmount = data.profileOverlapBlendAmount;
        meshQuality = data.meshQuality;
        HandleXPosition = data.handleXPosition;
        bladeThickness = data.bladeThickness;
        edgeSharpness = data.edgeSharpness;
        spineThickness = data.spineThickness;
        sharpSide = data.sharpSide;
        fullers = new List<FullerSettings>(data.fullers);

        RegenerateBlade(true);
    }

}