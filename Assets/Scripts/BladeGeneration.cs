using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class BladeProfileLayer
{
    [DisplayName("Cross Section Type", "Profile", 2)]

    public BladeBaseProfile profile;

    [Range(0, 1f), DisplayName("Start Height", "Profile", 2)]
    public float startHeight;

    [Range(0, 1f), DisplayName("End Height", "Profile", 2)]
    public float endHeight;

    [DisplayName("Transition Curve", "Profile", 2)]
    public AnimationCurve influenceCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

    [Range(0,2),DisplayName("Profile Scale", "Profile", 2)]
    public float scale = 1;
}

public class BladeGeneration : MonoBehaviour
{
    public SplineAndLineGen splineGen;

    [Header("Blade Base Profiles")]
    [DisplayName("Cross section ", "Profile", 2)]
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
    [DisplayName("Profile Blend Strenght", "Profile", 3)]
    public float profileOverlapBlendAmount = 0.5f;

    [Header("Mesh Quality")]
    [DisplayName("Mesh Quality", "General", 3)]
    public MeshQuality meshQuality = MeshQuality.Medium;

    private int segmentSubdivisions = 3;
    private int tipSubdivisions = 5;
    private int widthSubdivisions = 5;

    //[Range(1, 10), DisplayName("Curvature Window", "Curvature", 5)]
    private int curvatureWindow = 5;

    //[Range(0f, 1f), DisplayName("Curvature Blend", "Curvature", 6)]
    private float curvatureBlend = 0.5f;

    private float baseWidth = 0f;

    public GameObject guard;
    public GameObject handle;
    public GameObject holder;

    [Range(-2, 2), DisplayName("Handle X Position", "General", 2)]
    public float HandleXPosition;

    [Range(0.01f, .2f), DisplayName("Blade Thickness", "Blade Dimensions", 7)]
    public float bladeThickness = 0.1f;

    [Range(0f, 0.15f), DisplayName("Edge Sharpness", "Blade Edges", 0)]
    public float edgeSharpness = 0.05f;


    [Range(0.001f, 0.1f), DisplayName("Spine Thickness", "Blade Edges", 2)]
    public float spineThickness = 0.005f;


    [DisplayName("Blade Material", "Rendering", 0)]
    public Material bladeMaterial;

    [DisplayName("Sharp Edge Material", "Rendering", 1)]
    public Material sharpEdgeMaterial;

    [System.Serializable]
    public class FullerSettings
    {
        [Range(0f, 1f), DisplayName("Fuller Start", "Fuller", 10)]
        public float start = 0.1f;

        [Range(0f, 1f), DisplayName("Fuller End", "Fuller", 11)]
        public float end = 0.8f;

        [Range(0.0f, 0.9f), DisplayName("Fuller Depth", "Fuller", 12)]
        public float fullerDepth = 0.3f;

        [Range(0.05f, 0.9f), DisplayName("Fuller Width", "Fuller", 13)]
        public float fullerWidth = 0.3f;

        [Range(0f, 1f), DisplayName("Fuller Center Position", "Fuller", 14)]
        public float fullerCenter = 0.5f;

        [DisplayName("Fuller Falloff", "Fuller", 15)]
        public AnimationCurve fullerFalloff = AnimationCurve.EaseInOut(0, 1, 1, 0);

        [Range(0, 7), DisplayName("Number of Fullers", "Fuller", 16)]
        public int numberOfFullers = 1;

        [Range(1.0f, 3f), DisplayName("Fuller Spacing Multiplier", "Fuller", 17)]
        public float spacingMultiplier = 1.2f;
    }

    [DisplayName("Fuller Settings", "Fuller", 20)]
    public FullerSettings fuller;

    public enum SharpSide
    {
        Left,
        Right,
        Both
    }

    [DisplayName("Sharp Side", "General", 3)]
    public SharpSide sharpSide = SharpSide.Both;

    void Start()
    {
        HandleXPosition = holder.transform.localPosition.x;
        splineGen = GetComponent<SplineAndLineGen>();
        ApplyMeshQualitySettings();
        splineGen.GenerateLinesAndSplines();
        SmoothSegmentCenters();
        Generate3DBlade();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            ApplyMeshQualitySettings();
            splineGen.GenerateLinesAndSplines();
            SmoothSegmentCenters();
            Generate3DBlade();
            CalculateHandandGuardSize();
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
        ApplyMeshQualitySettings();
        splineGen.GenerateLinesAndSplines();
        SmoothSegmentCenters();
        Generate3DBlade();
        CalculateHandandGuardSize();
    }

    public void Generate3DBlade()
    {
        Mesh mesh3D = new Mesh();
        List<Vector3> vertices = new List<Vector3>();
        List<int> trianglesFrontBack = new List<int>();
        List<int> trianglesSharp = new List<int>();

        var segments = splineGen.segments;
        if (segments == null) return;

        // Smooth spline
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
            out frontVertexCount
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

        // Compute blade normal again (needed for bevels)
        Vector3 widthDir = (smoothRights[0] - smoothLefts[0]).normalized;
        Vector3 forwardDir = (smoothCenters[1] - smoothCenters[0]).normalized;
        Vector3 bladeNormal = Vector3.Cross(widthDir, forwardDir).normalized;

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
        int ringCount = smoothLefts.Count;
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
             frontVertexCount
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

    private void ApplyFullers(
        List<Vector3> vertices,
        List<Vector3> smoothLefts,
        List<Vector3> smoothRights,
        List<Vector3> smoothCenters,
        Vector3 bladeNormal,
        int frontVertexCount)
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
            int count = fuller.numberOfFullers;

            if (depth <= 0f || width <= 0f || count <= 0)
                continue;

            // NEW: Improved fuller positioning
            float centerPos = fuller.fullerCenter * 2f - 1f;
            float autoSpacing = fuller.fullerWidth * fuller.spacingMultiplier;
            float centerIndex = (count - 1) * 0.5f;

            for (int vertIdx = 0; vertIdx < widthSubdivisions; vertIdx++)
            {
                float widthT = vertIdx / (float)(widthSubdivisions - 1);
                float widthPos = widthT * 2f - 1f;

                float maxDepth = 0f;

                for (int f = 0; f < count; f++)
                {
                    float relativePos = (f - centerIndex) * autoSpacing;
                    float fullerPos = centerPos + relativePos;

                    float dist = Mathf.Abs(widthPos - fullerPos) / width;

                    if (dist > 1f)
                        continue;

                    float falloff = fuller.fullerFalloff.Evaluate(dist);
                    maxDepth = Mathf.Max(maxDepth, depth * falloff);
                }

                depthMap[ringIdx, vertIdx] = maxDepth;
            }
        }

        int smoothWindow = 5;
        float[,] smoothedDepth = new float[ringCount, widthSubdivisions];

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

                smoothedDepth[r, v] = sum / weightSum;
            }
        }

        for (int ringIdx = 0; ringIdx < ringCount; ringIdx++)
        {
            Vector3 left = smoothLefts[ringIdx];
            Vector3 right = smoothRights[ringIdx];
            Vector3 widthDir = (right - left).normalized;

            Vector3 forwardDir =
                ringIdx < smoothCenters.Count - 1
                    ? (smoothCenters[ringIdx + 1] - smoothCenters[ringIdx]).normalized
                    : (smoothCenters[ringIdx] - smoothCenters[ringIdx - 1]).normalized;

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
        out int frontVertexCount)
    {
        frontTriangles = new List<int>();
        GenerateEdgeGeometry(segments, smoothLefts, smoothRights, vertices, frontTriangles);
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

            leftList.Add(frontA); leftList.Add(frontB); leftList.Add(sharpLeftFrontA);
            leftList.Add(frontB); leftList.Add(sharpLeftFrontB); leftList.Add(sharpLeftFrontA);
            leftList.Add(backB); leftList.Add(backA); leftList.Add(sharpLeftBackA);
            leftList.Add(backB); leftList.Add(sharpLeftBackA); leftList.Add(sharpLeftBackB);
            leftList.Add(sharpLeftFrontA); leftList.Add(sharpLeftFrontB); leftList.Add(sharpLeftBackA);
            leftList.Add(sharpLeftFrontB); leftList.Add(sharpLeftBackB); leftList.Add(sharpLeftBackA);

            rightList.Add(frontARight); rightList.Add(sharpRightFrontA); rightList.Add(frontBRight);
            rightList.Add(frontBRight); rightList.Add(sharpRightFrontA); rightList.Add(sharpRightFrontB);
            rightList.Add(backARight); rightList.Add(backBRight); rightList.Add(sharpRightBackA);
            rightList.Add(backBRight); rightList.Add(sharpRightBackB); rightList.Add(sharpRightBackA);
            rightList.Add(sharpRightFrontA); rightList.Add(sharpRightBackA); rightList.Add(sharpRightFrontB);
            rightList.Add(sharpRightFrontB); rightList.Add(sharpRightBackA); rightList.Add(sharpRightBackB);
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

            Vector3 forwardDir =
                ring < smoothCenters.Count - 1
                    ? (smoothCenters[ring + 1] - smoothCenters[ring]).normalized
                    : (smoothCenters[ring] - smoothCenters[ring - 1]).normalized;

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

                // Apply thickness perpendicular to blade normal from the offset spine
                vertices[frontIndex] += bladeNormal * halfThickness;
                vertices.Add(vertices[frontIndex] - bladeNormal * (halfThickness * 2f));
            }
        }

        for (int i = 0; i < frontTriangles.Count; i += 3)
        {
            trianglesFrontBack.Add(frontTriangles[i + 2] + frontVertexCount);
            trianglesFrontBack.Add(frontTriangles[i + 1] + frontVertexCount);
            trianglesFrontBack.Add(frontTriangles[i] + frontVertexCount);
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
            Vector3 center = smoothCenters[i]; // Already offset spine
            Vector3 left = smoothLefts[i];
            Vector3 right = smoothRights[i];

            Vector3 leftOffset = left - center;
            Vector3 rightOffset = right - center;

            bool leftValid = leftOffset.sqrMagnitude > 1e-6f;
            bool rightValid = rightOffset.sqrMagnitude > 1e-6f;

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
                if (widthDir.sqrMagnitude < 1e-6f)
                    widthDir = Vector3.right;
            }

            Vector3 toLeft = -widthDir;
            Vector3 toRight = widthDir;

            if (i == ringCount - 1)
            {
                toLeft = (left - center).normalized;
                toRight = (right - center).normalized;
            }

            // Just use normal sharp/spine thickness based on SharpSide setting
            float leftThickness = (sharpSide == SharpSide.Left || sharpSide == SharpSide.Both) ? edgeSharpness : spineThickness;
            float rightThickness = (sharpSide == SharpSide.Right || sharpSide == SharpSide.Both) ? edgeSharpness : spineThickness;

            Vector3 leftRidge = left + toLeft * leftThickness;
            Vector3 rightRidge = right + toRight * rightThickness;

            if (i == ringCount - 1)
            {
                leftRidge += tipDir * edgeSharpness;
                rightRidge += tipDir * edgeSharpness;
            }

            float leftBevel = spineThickness;
            float rightBevel = spineThickness;

            vertices.Add(leftRidge + normalSign * bladeNormal * leftBevel);
            vertices.Add(rightRidge + normalSign * bladeNormal * rightBevel);
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

                // Width scaling
                Vector3 widthDir = (right - left).normalized;
                float rawWidth = Vector3.Distance(left, right);
                float halfWidth = rawWidth * 0.5f;

                float widthScale = BlendWidthScale(baseProfiles, bladeT);
                float shapedHalfWidth = halfWidth * widthScale;

                left = center - widthDir * shapedHalfWidth;
                right = center + widthDir * shapedHalfWidth;

                // *** SPINE OFFSET: Move the center/spine independently ***
                float spineOffset = splineGen.edgeSettings.spineOffset;
                if (Mathf.Abs(spineOffset) > 0.001f)
                {
                    // -1 = spine at left edge, 0 = centered, 1 = spine at right edge
                    float offsetT = (spineOffset + 1f) * 0.5f; // Convert -1..1 to 0..1
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
        List<int> triangles)
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

    BladeBaseProfile GetProfileAtHeight(float bladeT)
    {
        for (int i = 0; i < baseProfiles.Count; i++)
        {
            if (bladeT >= baseProfiles[i].startHeight &&
                bladeT <= baseProfiles[i].endHeight)
            {
                return baseProfiles[i].profile;
            }
        }

        return BladeBaseProfile.Lenticular;
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

    //Used for degubbign
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
}