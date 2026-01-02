using System.Collections.Generic;
using UnityEngine;

public class BladeGeneration : MonoBehaviour
{
    public SplineAndLineGen splineGen;

    [Header("Detail")]
    [Range(1, 50)] public int segmentSubdivisions = 3;
    [Range(1, 20)] public int tipSubdivisions = 5;
    [Range(2, 50)] public int widthSubdivisions = 5;

    //https://meshlib.io/feature/mesh-smoothing/
    [Header("Curvature Smoothing")]
    // controls how many previous segments are averaged when applying curvature
    [Range(1, 10)] public int curvatureWindow = 5;
    [Range(0f, 1f)] public float curvatureBlend = 0.5f;

    [Header("Debug")]
    public float baseWidth = 0f;
    public GameObject guard;
    public GameObject handle;


    [Header("3D Creation")]
    [Range(0.01f, 1)] public float bladeThickness = .1f;
    public AnimationCurve taperTowardsTip; //Blade Curve

    [Header("Blade Edges")]
    [Range(0f, 0.5f)] public float sideSharpness = 0.05f; // how much the sides extrude
    [Range(0.001f, 0.01f)] public float edgeBevelWidth = 0.005f; // micro-bevel on the sharp edge
    [Range(-0.1f, 0.1f)] public float nonSharpSideOffset = 0f;
    [Range(0.001f, 0.01f)]  public float nonSharpBevelWidth = 0.05f;

    [Header("Rendering")]
    public Material bladeMaterial;
    public Material sharpEdgeMaterial;

    [Header("Fuller")]
    public bool useFullerNoise;
    public float fullerNoiseScale = 0.123f;
    public float fullerNoiseStrength = 0.123f;


    [System.Serializable]
    public class BladeSectionProfile
    {
        public float position; // 0–1 along blade

        [Range(0.0f, 0.9f)]
        public float fullerDepth;

        [Range(0.0f, 0.9f)]
        public float fullerWidth;

        [Range(0, 1f)]
        public float fullerOffset = 0.5f;

        public AnimationCurve fullerFalloff;

        [Range(1, 7)]
        public int numberOfFullers = 1;

        [Range(0f, 1f)]
        public float spacingPercent = 0.1f; // percent of blade width
    }
    [System.Serializable]
    public class FullerDefinition
    {
        [Range(0f, 1f)] public float widthPercent = 0.2f;
        [Range(0f, 1f)] public float depthPercent = 0.2f;
        [Range(0f, 1f)] public float offsetPercent = 0.5f; // 0 = left, 1 = right
        public AnimationCurve falloff = AnimationCurve.EaseInOut(0, 1, 1, 0);
    }

    public List<BladeSectionProfile> profiles;



    public enum SharpSide
    {
        Left,
        Right,
        Both
    }

    public SharpSide sharpSide = SharpSide.Both;

    void Start()
    {
        splineGen = GetComponent<SplineAndLineGen>();
        splineGen.GenerateLinesAndSplines();
        SmoothSegmentCenters();
        //GenerateBladeMesh2D();
        Generate3DBlade();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            splineGen.GenerateLinesAndSplines();
            SmoothSegmentCenters();
            //GenerateBladeMesh2D();
            Generate3DBlade();
            CalculateHandandGuardSize();
        }
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
        if (profiles == null || profiles.Count == 0)
            return;

        int ringCount = smoothLefts.Count;

        float totalLength = 0f;
        List<float> cumulativeLengths = new List<float> { 0f };

        for (int i = 1; i < ringCount; i++)
        {
            totalLength += Vector3.Distance(smoothCenters[i], smoothCenters[i - 1]);
            cumulativeLengths.Add(totalLength);
        }

        // Step 1: Calculate depth adjustments for all vertices
        float[,] depthMap = new float[ringCount, widthSubdivisions];

        for (int ringIdx = 0; ringIdx < ringCount; ringIdx++)
        {
            float bladePosition = totalLength > 0 ? cumulativeLengths[ringIdx] / totalLength : 0f;

            BladeSectionProfile p0, p1;
            float blend = GetProfileBlend(bladePosition, out p0, out p1);

            float depthPercent = Mathf.Lerp(p0.fullerDepth, p1.fullerDepth, blend);
            float widthPercent = Mathf.Lerp(p0.fullerWidth, p1.fullerWidth, blend);
            float baseOffsetPercent = Mathf.Lerp(p0.fullerOffset, p1.fullerOffset, blend);
            float spacingPercent = Mathf.Lerp(p0.spacingPercent, p1.spacingPercent, blend);
            int count = Mathf.RoundToInt(Mathf.Lerp(p0.numberOfFullers, p1.numberOfFullers, blend));

            if (depthPercent <= 0f || widthPercent <= 0f || count <= 0)
                continue;

            Vector3 left = smoothLefts[ringIdx];
            Vector3 right = smoothRights[ringIdx];
            float ringWidth = Vector3.Distance(left, right);

            float fullerDepth = depthPercent * (bladeThickness * 0.5f);

            float baseOffsetNormalized = baseOffsetPercent * 2f - 1f;
            float spacingNormalized = spacingPercent * 2f;
            float centerIndex = (count - 1) * 0.5f;

            for (int vertIdx = 0; vertIdx < widthSubdivisions; vertIdx++)
            {
                float widthT = vertIdx / (float)(widthSubdivisions - 1);
                float widthPosition = (widthT * 2f) - 1f;

                float maxDepth = 0f;

                // Accumulate depth from all fullers at this vertex
                for (int f = 0; f < count; f++)
                {
                    float offsetNormalized = baseOffsetNormalized + (f - centerIndex) * spacingNormalized;
                    float distFromCenter = widthPosition - offsetNormalized;
                    float normalizedDist = Mathf.Abs(distFromCenter) / widthPercent;

                    if (normalizedDist > 1f)
                        continue;

                    float falloff = Mathf.Lerp(
                        p0.fullerFalloff.Evaluate(normalizedDist),
                        p1.fullerFalloff.Evaluate(normalizedDist),
                        blend
                    );

                    maxDepth = Mathf.Max(maxDepth, fullerDepth * falloff);
                }

                depthMap[ringIdx, vertIdx] = maxDepth;
            }
        }

        // Step 2: Apply longitudinal smoothing
        int smoothWindow = 5; // Adjust this for more/less smoothing
        float[,] smoothedDepthMap = new float[ringCount, widthSubdivisions];

        for (int ringIdx = 0; ringIdx < ringCount; ringIdx++)
        {
            for (int vertIdx = 0; vertIdx < widthSubdivisions; vertIdx++)
            {
                float sum = 0f;
                float weightSum = 0f;

                for (int r = Mathf.Max(0, ringIdx - smoothWindow);
                     r <= Mathf.Min(ringCount - 1, ringIdx + smoothWindow);
                     r++)
                {
                    float distance = Mathf.Abs(r - ringIdx);
                    float weight = 1f - (distance / (smoothWindow + 1f));

                    sum += depthMap[r, vertIdx] * weight;
                    weightSum += weight;
                }

                smoothedDepthMap[ringIdx, vertIdx] = sum / weightSum;
            }
        }

        // Step 3: Apply the smoothed depths to vertices
        for (int ringIdx = 0; ringIdx < ringCount; ringIdx++)
        {
            Vector3 left = smoothLefts[ringIdx];
            Vector3 right = smoothRights[ringIdx];
            Vector3 widthDir = (right - left).normalized;

            Vector3 forwardDir;
            if (ringIdx < smoothCenters.Count - 1)
                forwardDir = (smoothCenters[ringIdx + 1] - smoothCenters[ringIdx]).normalized;
            else
                forwardDir = (smoothCenters[ringIdx] - smoothCenters[ringIdx - 1]).normalized;

            Vector3 ringNormal = Vector3.Cross(widthDir, forwardDir).normalized;

            int ringStartIdx = ringIdx * widthSubdivisions;

            for (int vertIdx = 0; vertIdx < widthSubdivisions; vertIdx++)
            {
                int frontIdx = ringStartIdx + vertIdx;
                int backIdx = frontIdx + frontVertexCount;

                float actualDepth = smoothedDepthMap[ringIdx, vertIdx];

                vertices[frontIdx] -= ringNormal * actualDepth;
                vertices[backIdx] += ringNormal * actualDepth;
            }
        }
    }
    private float GetProfileBlend(float bladeT, out BladeSectionProfile p0, out BladeSectionProfile p1)
    {
        // Default assignments
        p0 = profiles[0];
        p1 = profiles[profiles.Count - 1];

        // Ensure sorted
        var sorted = new List<BladeSectionProfile>(profiles);
        sorted.Sort((a, b) => a.position.CompareTo(b.position));

        // Edge cases
        if (bladeT <= sorted[0].position)
        {
            p0 = sorted[0];
            p1 = sorted[0];
            return 0f;
        }

        if (bladeT >= sorted[^1].position)
        {
            p0 = sorted[^1];
            p1 = sorted[^1];
            return 0f;
        }

        // Find the two profiles around bladeT
        for (int i = 0; i < sorted.Count - 1; i++)
        {
            if (bladeT >= sorted[i].position && bladeT <= sorted[i + 1].position)
            {
                p0 = sorted[i];
                p1 = sorted[i + 1];

                float t = Mathf.InverseLerp(p0.position, p1.position, bladeT);

                // Smoothstep for better transitions
                t = t * t * (3f - 2f * t);

                return t;
            }
        }

        // Should never hit this, but safe fallback
        return 0f;
    }
    private AnimationCurve BlendCurves(AnimationCurve a, AnimationCurve b, float t)
    {
        AnimationCurve result = new AnimationCurve();

        for (int i = 0; i < 20; i++)
        {
            float x = i / 19f;
            float y = Mathf.Lerp(a.Evaluate(x), b.Evaluate(x), t);
            result.AddKey(x, y);
        }

        return result;
    }
    private FullerDefinition CloneFuller(FullerDefinition f)
    {
        return new FullerDefinition
        {
            widthPercent = f.widthPercent,
            depthPercent = f.depthPercent,
            offsetPercent = f.offsetPercent,
            falloff = new AnimationCurve(f.falloff.keys)
        };
    }

    private List<FullerDefinition> BlendFullerLists(
    List<FullerDefinition> a,
    List<FullerDefinition> b,
    float blend)
    {
        List<FullerDefinition> result = new List<FullerDefinition>();

        int count = Mathf.Max(a.Count, b.Count);

        for (int i = 0; i < count; i++)
        {
            FullerDefinition fA = (i < a.Count) ? a[i] : null;
            FullerDefinition fB = (i < b.Count) ? b[i] : null;

            if (fA == null && fB != null)
            {
                result.Add(CloneFuller(fB));
                continue;
            }
            if (fB == null && fA != null)
            {
                result.Add(CloneFuller(fA));
                continue;
            }

            FullerDefinition blended = new FullerDefinition();
            blended.widthPercent = Mathf.Lerp(fA.widthPercent, fB.widthPercent, blend);
            blended.depthPercent = Mathf.Lerp(fA.depthPercent, fB.depthPercent, blend);
            blended.offsetPercent = Mathf.Lerp(fA.offsetPercent, fB.offsetPercent, blend);

            blended.falloff = BlendCurves(fA.falloff, fB.falloff, blend);

            result.Add(blended);
        }

        return result;
    }
    private void ApplyFuller(
     List<Vector3> vertices,
     List<Vector3> smoothLefts,
     List<Vector3> smoothRights,
     List<Vector3> smoothCenters,
     Vector3 bladeNormal,
     int frontVertexCount)
    {
        if (profiles == null || profiles.Count == 0) return;

        int ringCount = smoothLefts.Count;

        // Compute cumulative blade length
        float totalLength = 0f;
        List<float> cumulativeLengths = new List<float> { 0f };

        for (int i = 1; i < ringCount; i++)
        {
            totalLength += Vector3.Distance(smoothCenters[i], smoothCenters[i - 1]);
            cumulativeLengths.Add(totalLength);
        }

        for (int ringIdx = 0; ringIdx < ringCount; ringIdx++)
        {
            float bladePosition = totalLength > 0 ? cumulativeLengths[ringIdx] / totalLength : 0f;

            Vector3 left = smoothLefts[ringIdx];
            Vector3 right = smoothRights[ringIdx];
            float ringWidth = Vector3.Distance(left, right);

            // Percentages to real values
            float fullerDepthPercent = GetFullerDepthAtPosition(bladePosition);
            float fullerWidthPercent = GetFullerWidthAtPosition(bladePosition);
            float fullerOffsetPercent = GetFullerOffsetAtPosition(bladePosition);

            float fullerDepth = fullerDepthPercent * (bladeThickness * 0.5f);
            float fullerWidth = fullerWidthPercent * ringWidth;

            if (fullerDepth <= 0f || fullerWidth <= 0f) continue;

            int ringStartIdx = ringIdx * widthSubdivisions;

            for (int vertIdx = 0; vertIdx < widthSubdivisions; vertIdx++)
            {
                int frontIdx = ringStartIdx + vertIdx;
                int backIdx = frontIdx + frontVertexCount;

                // Normalized width position (-1 to 1)
                float widthT = vertIdx / (float)(widthSubdivisions - 1);
                float widthPosition = (widthT * 2f) - 1f;

                float offsetNormalized = fullerOffsetPercent * 2f - 1f;
                widthPosition -= offsetNormalized;

                // Falloff 
                float depthMultiplier = GetFullerFalloff(widthPosition, fullerWidthPercent, bladePosition);
                float actualDepth = fullerDepth * depthMultiplier;

                if (useFullerNoise)
                {
                    float noiseValue = Mathf.PerlinNoise(
                        bladePosition * fullerNoiseScale * 10f,
                        widthPosition * fullerNoiseScale * 10f
                    );
                    actualDepth += (noiseValue - 0.5f) * fullerNoiseStrength * fullerDepth;
                }

                vertices[frontIdx] -= bladeNormal * actualDepth;
                vertices[backIdx] += bladeNormal * actualDepth;
            }
        }
    }
   
    private float GetFullerDepthAtPosition(float position)
    {
        if (profiles == null || profiles.Count == 0) return 0f;
        if (profiles.Count == 1) return profiles[0].fullerDepth;

        // Sort profiles by position (in case they're not ordered)
        var sortedProfiles = new List<BladeSectionProfile>(profiles);
        sortedProfiles.Sort((a, b) => a.position.CompareTo(b.position));

        // Handle edges
        if (position <= sortedProfiles[0].position)
            return sortedProfiles[0].fullerDepth;
        if (position >= sortedProfiles[sortedProfiles.Count - 1].position)
            return sortedProfiles[sortedProfiles.Count - 1].fullerDepth;

        // Find the two profiles to interpolate between
        for (int i = 0; i < sortedProfiles.Count - 1; i++)
        {
            if (position >= sortedProfiles[i].position && position <= sortedProfiles[i + 1].position)
            {
                float t = (position - sortedProfiles[i].position) /
                         (sortedProfiles[i + 1].position - sortedProfiles[i].position);

                // Use smoothstep for better blending
                t = t * t * (3f - 2f * t);

                return Mathf.Lerp(sortedProfiles[i].fullerDepth, sortedProfiles[i + 1].fullerDepth, t);
            }
        }

        return sortedProfiles[0].fullerDepth;
    }

  
    private float GetFullerWidthAtPosition(float position)
    {
        if (profiles == null || profiles.Count == 0) return 0f;
        if (profiles.Count == 1) return profiles[0].fullerWidth;

        var sortedProfiles = new List<BladeSectionProfile>(profiles);
        sortedProfiles.Sort((a, b) => a.position.CompareTo(b.position));

        if (position <= sortedProfiles[0].position)
            return sortedProfiles[0].fullerWidth;
        if (position >= sortedProfiles[sortedProfiles.Count - 1].position)
            return sortedProfiles[sortedProfiles.Count - 1].fullerWidth;

        for (int i = 0; i < sortedProfiles.Count - 1; i++)
        {
            if (position >= sortedProfiles[i].position && position <= sortedProfiles[i + 1].position)
            {
                float t = (position - sortedProfiles[i].position) /
                         (sortedProfiles[i + 1].position - sortedProfiles[i].position);

                // Use smoothstep for better blending
                t = t * t * (3f - 2f * t);

                return Mathf.Lerp(sortedProfiles[i].fullerWidth, sortedProfiles[i + 1].fullerWidth, t);
            }
        }

        return sortedProfiles[0].fullerWidth;
    }

    private float GetFullerFalloff(float widthPosition, float fullerWidthPercent, float bladePosition)
    {
        if (profiles == null || profiles.Count == 0) return 0f;

        var sorted = new List<BladeSectionProfile>(profiles);
        sorted.Sort((a, b) => a.position.CompareTo(b.position));

        BladeSectionProfile p0 = sorted[0];
        BladeSectionProfile p1 = sorted[^1];

        for (int i = 0; i < sorted.Count - 1; i++)
        {
            if (bladePosition >= sorted[i].position && bladePosition <= sorted[i + 1].position)
            {
                p0 = sorted[i];
                p1 = sorted[i + 1];
                break;
            }
        }

        float t = Mathf.InverseLerp(p0.position, p1.position, bladePosition);
        t = t * t * (3f - 2f * t); // smoothstep

        float normalizedDist = Mathf.Abs(widthPosition) / fullerWidthPercent;
        if (normalizedDist > 1f) return 0f;

        float f0 = p0.fullerFalloff.Evaluate(normalizedDist);
        float f1 = p1.fullerFalloff.Evaluate(normalizedDist);

        return Mathf.Lerp(f0, f1, t);
    }

    private float GetFullerOffsetAtPosition(float position)
    {
        if (profiles == null || profiles.Count == 0) return 0f;
        if (profiles.Count == 1) return profiles[0].fullerOffset;

        var sorted = new List<BladeSectionProfile>(profiles);
        sorted.Sort((a, b) => a.position.CompareTo(b.position));

        if (position <= sorted[0].position) return sorted[0].fullerOffset;
        if (position >= sorted[^1].position) return sorted[^1].fullerOffset;

        for (int i = 0; i < sorted.Count - 1; i++)
        {
            if (position >= sorted[i].position && position <= sorted[i + 1].position)
            {
                float t = Mathf.InverseLerp(sorted[i].position, sorted[i + 1].position, position);
                t = t * t * (3f - 2f * t);
                return Mathf.Lerp(sorted[i].fullerOffset, sorted[i + 1].fullerOffset, t);
            }
        }

        return 0f;
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

            //Decides if the edges should be sharp or not
            List<int> leftList =
                (sharpSide == SharpSide.Left || sharpSide == SharpSide.Both)
                ? trianglesSharp
                : trianglesFrontBack;

            List<int> rightList =
                (sharpSide == SharpSide.Right || sharpSide == SharpSide.Both)
                ? trianglesSharp
                : trianglesFrontBack;

            // left edge
            leftList.Add(frontA); leftList.Add(frontB); leftList.Add(sharpLeftFrontA);
            leftList.Add(frontB); leftList.Add(sharpLeftFrontB); leftList.Add(sharpLeftFrontA);

            leftList.Add(backB); leftList.Add(backA); leftList.Add(sharpLeftBackA);
            leftList.Add(backB); leftList.Add(sharpLeftBackA); leftList.Add(sharpLeftBackB);

            leftList.Add(sharpLeftFrontA); leftList.Add(sharpLeftFrontB); leftList.Add(sharpLeftBackA);
            leftList.Add(sharpLeftFrontB); leftList.Add(sharpLeftBackB); leftList.Add(sharpLeftBackA);

            // right edge
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
        Vector3 widthDir = (smoothRights[0] - smoothLefts[0]).normalized;
        Vector3 forwardDir = (smoothCenters[1] - smoothCenters[0]).normalized;
        Vector3 bladeNormal = Vector3.Cross(widthDir, forwardDir).normalized;

        float halfThickness = bladeThickness * 0.5f;

        // move front vertices
        for (int i = 0; i < frontVertexCount; i++)
            vertices[i] += bladeNormal * halfThickness;

        // Duplicate for back
        for (int i = 0; i < frontVertexCount; i++)
            vertices.Add(vertices[i] - bladeNormal * bladeThickness);

        // Flip triangles
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
        // tront bevel
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

        // back bevel
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

            Vector3 toLeft = (left - center).normalized;
            Vector3 toRight = (right - center).normalized;

            Vector3 leftRidge = left + toLeft * ((sharpSide == SharpSide.Left || sharpSide == SharpSide.Both) ? sideSharpness : nonSharpSideOffset);
            Vector3 rightRidge = right + toRight * ((sharpSide == SharpSide.Right || sharpSide == SharpSide.Both) ? sideSharpness : nonSharpSideOffset);

            if (i == ringCount - 1)
            {
                leftRidge += tipDir * sideSharpness;
                rightRidge += tipDir * sideSharpness;
            }

            float leftBevel = (sharpSide == SharpSide.Left || sharpSide == SharpSide.Both) ? edgeBevelWidth : nonSharpBevelWidth;
            float rightBevel = (sharpSide == SharpSide.Right || sharpSide == SharpSide.Both) ? edgeBevelWidth : nonSharpBevelWidth;

            // FIXED: use bladeNormal, not BladeNormal
            vertices.Add(leftRidge + normalSign * bladeNormal * leftBevel);
            vertices.Add(rightRidge + normalSign * bladeNormal * rightBevel);
        }
    }

    public void GenerateSmoothSegments(List<Segment> segments, List<Vector3> smoothLefts, List<Vector3> smoothRights, List<Vector3> smoothCenters )
    {
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
                    continue; // Stops over lapping mesh generation

                float t = j / (float)currentSubdivisions;

                Vector3 center = CatmullRom(p0.center, p1.center, p2.center, p3.center, t);
                Vector3 left = CatmullRom(p0.left, p1.left, p2.left, p3.left, t);
                Vector3 right = CatmullRom(p0.right, p1.right, p2.right, p3.right, t);

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
                    adjustedBlend // pass this instead of using curvatureBlend directly
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

        // generate rings left to right
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

        //  Connect rings clockwise 
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

        // Tip vertex
        Segment tipSegment = segments[segments.Count - 1];
        Vector3 tipPoint = tipSegment.center;
        int tipIndex = vertices.Count;
        vertices.Add(tipPoint);

        // Tip triangles
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

        // Recalculate normals PER SUBMESH
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
            guard.transform.localScale = new Vector3(baseWidth * 2, guard.transform.localScale.y, guard.transform.localScale.z);
        if (handle != null)
            handle.transform.localScale = new Vector3(baseWidth, handle.transform.localScale.y, handle.transform.localScale.z);
    }

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