using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;

public class HiltGeneration : MonoBehaviour
{
    [System.Serializable]
    public struct Segment
    {
        public Vector3 center;
        public Vector3 left;
        public Vector3 right;
    }

    [Header("Guard Settings")]
    public Material guardMaterial;
    public SplineContainer guardSpline;
    [Range(0.05f, 0.5f)]
    public float guardHalfLength = 0.25f;
    [Range(-0.5f, 0.5f)]
    public float verticalCurve = 0.05f;
    [Range(0.05f, 0.5f)]
    public float guardWidth = 0.1f;
    [Range(0.05f, 0.5f)]
    public float guardThickness = 0.05f;
    [Range(0.05f, 0.5f)]
    public float guardHeight = 0.1f;

    [Header("Guard Thickness Control")]
    [Range(0.05f, 0.5f)]
    public float guardThicknessStart = 0.05f;
    [Range(0.05f, 0.5f)]
    public float guardThicknessEnd = 0.05f;
    [Range(1, 6)]
    public int thicknessSegments = 3;
    public int verticalSegments = 3;

    [Header("Guard Height Curve")]
    public AnimationCurve guardHeightCurve = AnimationCurve.Linear(0, 0.1f, 1, 0.1f);

    [Header("Top Shape Control")]
    [Range(0f, 0.2f)]
    public float topCenterHeight = 0.05f;
    public AnimationCurve topProfile = AnimationCurve.EaseInOut(0, 0, 1, 0);

    [Header("Across Thickness Top Shape")]
    [Range(0f, 0.1f)]
    public float acrossCenterHeight = 0.03f;
    public AnimationCurve acrossProfile = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Range(3, 20)]
    public int controlPoints = 4;
    [Range(20, 200)]
    public int guardSamples = 20;

    [Header("UV Settings")]
    [Range(0.1f, 10f)]
    public float uvScale = 1f;

    [SerializeField] private float guardHalfWidth = 0.25f;
    [SerializeField] private float guardCurveHeight = 0.05f;
    [SerializeField] private int guardCurveResolution = 4;
    [SerializeField] private int guardSegments = 10;

    private MeshFilter meshFilter;

    // Mesh data
    List<Vector3> finalVerts = new();
    List<Vector3> finalNormals = new();
    List<Vector4> finalTangents = new();
    List<Vector2> finalUVs = new();
    List<int> finalTris = new();

    void Start()
    {
        meshFilter = GetComponent<MeshFilter>();
        if (meshFilter == null)
            meshFilter = gameObject.AddComponent<MeshFilter>();

        MeshRenderer mr = GetComponent<MeshRenderer>();
        if (mr == null)
            mr = gameObject.AddComponent<MeshRenderer>();

        GenerateHilt();
    }

    private void Update()
    {
        if (Input.GetKeyUp(KeyCode.Space))
        {
            GenerateHilt();
        }
    }

    public void GenerateHilt()
    {
        GenerateGuardSplinePoints();
        GenerateGuard();
        GenerateGrip();
        GeneratePommel();
    }

    private void GenerateGuardSplinePoints()
    {
        if (guardSpline == null)
        {
            Debug.LogWarning("No SplineContainer assigned.");
            return;
        }

        Spline spline = guardSpline.Spline;
        spline.Clear();

        for (int i = 0; i < controlPoints; i++)
        {
            float t = i / (float)(controlPoints - 1);
            float x = Mathf.Lerp(-guardHalfLength, guardHalfLength, t);
            float y = Mathf.Sin(t * Mathf.PI) * verticalCurve;
            float z = 0f;

            Vector3 pos = new Vector3(x, y, z);
            BezierKnot knot = new BezierKnot(pos);
            spline.Add(knot);
        }
    }

    private List<List<Vector3>> GenerateGuardRings(int samples)
    {
        List<List<Vector3>> rings = new List<List<Vector3>>();
        Spline spline = guardSpline.Spline;

        for (int i = 0; i < samples; i++)
        {
            float t = i / (float)(samples - 1);
            float baseHeight = guardHeight * guardHeightCurve.Evaluate(t);
            float halfH = baseHeight * 0.5f;

            float centerMask = 1f - Mathf.Abs(t - 0.5f) * 2f;
            float crown = topCenterHeight * topProfile.Evaluate(centerMask);

            Vector3 center = SplineUtility.EvaluatePosition(spline, t);
            Vector3 tangent = SplineUtility.EvaluateTangent(spline, t);

            List<Vector3> ring = new List<Vector3>();

            for (int v = 0; v <= verticalSegments; v++)
            {
                float vNorm = v / (float)verticalSegments;
                float y = Mathf.Lerp(-halfH, halfH, vNorm);

                // Top crown
                if (v >= verticalSegments - 1)
                {
                    float blend = (vNorm - (1f - 1f / verticalSegments)) * verticalSegments;
                    y += crown * blend;
                }

                // Bottom crown (mirrored)
                if (v <= 1)
                {
                    float blend = (1f / verticalSegments - vNorm) * verticalSegments;
                    y -= crown * blend;
                }

                Vector3 point = center + Vector3.up * y;
                ring.Add(point);
            }

            rings.Add(ring);
        }

        return rings;
    }

    int AddVert(Vector3 p, Vector3 n, Vector4 tan, Vector2 uv)
    {
        int i = finalVerts.Count;
        finalVerts.Add(p);
        finalNormals.Add(n);
        finalTangents.Add(tan);
        finalUVs.Add(uv);
        return i;
    }

    void AddQuadFace(
        Vector3 a, Vector3 b, Vector3 c, Vector3 d,
        Vector2 uva, Vector2 uvb, Vector2 uvc, Vector2 uvd)
    {
        // Calculate proper normal from the quad
        Vector3 edge1 = b - a;
        Vector3 edge2 = d - a;
        Vector3 normal = Vector3.Cross(edge1, edge2).normalized;

        // Calculate tangent (along the U direction)
        Vector3 tangent = edge1.normalized;

        // Calculate bitangent and ensure proper handedness
        Vector3 bitangent = Vector3.Cross(normal, tangent);
        float handedness = Vector3.Dot(Vector3.Cross(normal, tangent), edge2) > 0 ? 1f : -1f;
        Vector4 tangent4 = new Vector4(tangent.x, tangent.y, tangent.z, handedness);

        int ia = AddVert(a, normal, tangent4, uva);
        int ib = AddVert(b, normal, tangent4, uvb);
        int ic = AddVert(c, normal, tangent4, uvc);
        int id = AddVert(d, normal, tangent4, uvd);

        finalTris.Add(ia); finalTris.Add(ib); finalTris.Add(ic);
        finalTris.Add(ia); finalTris.Add(ic); finalTris.Add(id);
    }

    private Mesh BuildGuardMesh3D(List<List<Vector3>> rings)
    {
        Mesh mesh = new Mesh();

        finalVerts.Clear();
        finalNormals.Clear();
        finalTangents.Clear();
        finalUVs.Clear();
        finalTris.Clear();

        List<Vector3> verts = new List<Vector3>();
        int ringCount = rings.Count;
        int ringSize = rings[0].Count;
        float halfT = guardThickness * 0.5f;

        // thicknessIndex[ring][vertical][thicknessLayer]
        List<List<List<int>>> thicknessIndex = new List<List<List<int>>>();

        // Build all thickness layers
        for (int i = 0; i < ringCount; i++)
        {
            List<List<int>> ringLayers = new List<List<int>>();

            for (int v = 0; v < ringSize; v++)
            {
                float vNorm = v / (float)(ringSize - 1);
                Vector3 basePos = rings[i][v];
                List<int> layerRow = new List<int>();

                for (int t = 0; t <= thicknessSegments; t++)
                {
                    float tNorm = t / (float)thicknessSegments;
                    float zOffset = Mathf.Lerp(halfT, -halfT, tNorm);
                    float acrossMask = 1f - Mathf.Abs(tNorm - 0.5f) * 2f;
                    float yOffset = 0f;

                    // Top across-thickness crown
                    if (v >= ringSize - 2)
                    {
                        float blend = (vNorm - (1f - 1f / (ringSize - 1))) * (ringSize - 1);
                        yOffset += acrossCenterHeight * acrossProfile.Evaluate(acrossMask) * blend;
                    }

                    // Bottom across-thickness crown
                    if (v <= 1)
                    {
                        float blend = (1f / (ringSize - 1) - vNorm) * (ringSize - 1);
                        yOffset -= acrossCenterHeight * acrossProfile.Evaluate(acrossMask) * blend;
                    }

                    layerRow.Add(verts.Count);
                    verts.Add(basePos + Vector3.forward * zOffset + Vector3.up * yOffset);
                }

                ringLayers.Add(layerRow);
            }

            thicknessIndex.Add(ringLayers);
        }

        // ==================================================================
        // PRE-CALCULATE UV COORDINATES
        // ==================================================================

        // Step 1: Calculate U coordinates (distance along spline) - UNCHANGED
        List<float> uCoords = new List<float>();
        uCoords.Add(0f);
        float totalU = 0f;

        for (int i = 1; i < ringCount; i++)
        {
            float ringDist = 0f;
            int count = 0;

            for (int v = 0; v < ringSize; v++)
            {
                for (int t = 0; t <= thicknessSegments; t++)
                {
                    Vector3 prev = verts[thicknessIndex[i - 1][v][t]];
                    Vector3 curr = verts[thicknessIndex[i][v][t]];
                    ringDist += Vector3.Distance(prev, curr);
                    count++;
                }
            }

            ringDist /= count;
            totalU += ringDist;
            uCoords.Add(totalU);
        }

        if (totalU > 0)
        {
            for (int i = 0; i < uCoords.Count; i++)
            {
                uCoords[i] /= totalU;
            }
        }

        // Step 2: Calculate V coordinates (distance along height) - UNCHANGED
        List<List<float>> vCoords = new List<List<float>>();

        for (int i = 0; i < ringCount; i++)
        {
            List<float> ringVCoords = new List<float>();
            ringVCoords.Add(0f);
            float totalV = 0f;

            for (int v = 1; v < ringSize; v++)
            {
                float vDist = 0f;
                for (int t = 0; t <= thicknessSegments; t++)
                {
                    Vector3 prev = verts[thicknessIndex[i][v - 1][t]];
                    Vector3 curr = verts[thicknessIndex[i][v][t]];
                    vDist += Vector3.Distance(prev, curr);
                }
                vDist /= (thicknessSegments + 1);
                totalV += vDist;
                ringVCoords.Add(totalV);
            }

            if (totalV > 0)
            {
                for (int v = 0; v < ringVCoords.Count; v++)
                {
                    ringVCoords[v] /= totalV;
                }
            }

            vCoords.Add(ringVCoords);
        }

        // Step 3: Calculate W coordinates (distance along thickness) - NEW
        List<List<List<float>>> wCoords = new List<List<List<float>>>();

        for (int i = 0; i < ringCount; i++)
        {
            List<List<float>> ringWCoords = new List<List<float>>();

            for (int v = 0; v < ringSize; v++)
            {
                List<float> thicknessCoords = new List<float>();
                thicknessCoords.Add(0f);
                float totalW = 0f;

                for (int t = 1; t <= thicknessSegments; t++)
                {
                    Vector3 prev = verts[thicknessIndex[i][v][t - 1]];
                    Vector3 curr = verts[thicknessIndex[i][v][t]];
                    totalW += Vector3.Distance(prev, curr);
                    thicknessCoords.Add(totalW);
                }

                if (totalW > 0)
                {
                    for (int t = 0; t < thicknessCoords.Count; t++)
                    {
                        thicknessCoords[t] /= totalW;
                    }
                }

                ringWCoords.Add(thicknessCoords);
            }

            wCoords.Add(ringWCoords);
        }

        // Step 4: Build UV lookup table with BOTH U,V and U,W options
        List<List<List<Vector2>>> uvLookupUV = new List<List<List<Vector2>>>();  // For front/back (U,V)
        List<List<List<Vector2>>> uvLookupUW = new List<List<List<Vector2>>>();  // For top/bottom (U,W)
        List<List<List<Vector2>>> uvLookupWV = new List<List<List<Vector2>>>();  // For sides (W,V)

        for (int i = 0; i < ringCount; i++)
        {
            List<List<Vector2>> ringUVsUV = new List<List<Vector2>>();
            List<List<Vector2>> ringUVsUW = new List<List<Vector2>>();
            List<List<Vector2>> ringUVsWV = new List<List<Vector2>>();

            for (int v = 0; v < ringSize; v++)
            {
                List<Vector2> thicknessUVsUV = new List<Vector2>();
                List<Vector2> thicknessUVsUW = new List<Vector2>();
                List<Vector2> thicknessUVsWV = new List<Vector2>();

                for (int t = 0; t <= thicknessSegments; t++)
                {
                    float u = uCoords[i] * uvScale;
                    float vCoord = vCoords[i][v] * uvScale;
                    float w = wCoords[i][v][t] * uvScale;

                    thicknessUVsUV.Add(new Vector2(u, vCoord));  // U,V for front/back
                    thicknessUVsUW.Add(new Vector2(u, w));       // U,W for top/bottom
                    thicknessUVsWV.Add(new Vector2(w, vCoord));  // W,V for sides
                }

                ringUVsUV.Add(thicknessUVsUV);
                ringUVsUW.Add(thicknessUVsUW);
                ringUVsWV.Add(thicknessUVsWV);
            }

            uvLookupUV.Add(ringUVsUV);
            uvLookupUW.Add(ringUVsUW);
            uvLookupWV.Add(ringUVsWV);
        }

        int top = verticalSegments;
        int bottom = 0;
        int front = 0;
        int back = thicknessSegments;

        // ==================================================================
        // BUILD FACES - Using appropriate UV lookup for each face type
        // ==================================================================

        // FRONT FACE - use U,V lookup (UNCHANGED)
        for (int i = 1; i < ringCount; i++)
        {
            for (int v = 0; v < ringSize - 1; v++)
            {
                Vector3 a = verts[thicknessIndex[i - 1][v][front]];
                Vector3 b = verts[thicknessIndex[i][v][front]];
                Vector3 c = verts[thicknessIndex[i][v + 1][front]];
                Vector3 d = verts[thicknessIndex[i - 1][v + 1][front]];

                Vector2 uva = uvLookupUV[i - 1][v][front];
                Vector2 uvb = uvLookupUV[i][v][front];
                Vector2 uvc = uvLookupUV[i][v + 1][front];
                Vector2 uvd = uvLookupUV[i - 1][v + 1][front];

                AddQuadFace(a, b, c, d, uva, uvb, uvc, uvd);
            }
        }

        // BACK FACE - use U,V lookup (UNCHANGED)
        for (int i = 1; i < ringCount; i++)
        {
            for (int v = 0; v < ringSize - 1; v++)
            {
                Vector3 a = verts[thicknessIndex[i - 1][v + 1][back]];
                Vector3 b = verts[thicknessIndex[i][v + 1][back]];
                Vector3 c = verts[thicknessIndex[i][v][back]];
                Vector3 d = verts[thicknessIndex[i - 1][v][back]];

                Vector2 uva = uvLookupUV[i - 1][v + 1][back];
                Vector2 uvb = uvLookupUV[i][v + 1][back];
                Vector2 uvc = uvLookupUV[i][v][back];
                Vector2 uvd = uvLookupUV[i - 1][v][back];

                AddQuadFace(a, b, c, d, uva, uvb, uvc, uvd);
            }
        }

        // TOP FACE - use U,W lookup (CHANGED)
        for (int i = 1; i < ringCount; i++)
        {
            for (int t = 0; t < thicknessSegments; t++)
            {
                Vector3 a = verts[thicknessIndex[i - 1][top][t]];
                Vector3 b = verts[thicknessIndex[i][top][t]];
                Vector3 c = verts[thicknessIndex[i][top][t + 1]];
                Vector3 d = verts[thicknessIndex[i - 1][top][t + 1]];

                Vector2 uva = uvLookupUW[i - 1][top][t];
                Vector2 uvb = uvLookupUW[i][top][t];
                Vector2 uvc = uvLookupUW[i][top][t + 1];
                Vector2 uvd = uvLookupUW[i - 1][top][t + 1];

                AddQuadFace(a, b, c, d, uva, uvb, uvc, uvd);
            }
        }

        // BOTTOM FACE - use U,W lookup (CHANGED)
        for (int i = 1; i < ringCount; i++)
        {
            for (int t = 0; t < thicknessSegments; t++)
            {
                Vector3 a = verts[thicknessIndex[i - 1][bottom][t]];
                Vector3 b = verts[thicknessIndex[i - 1][bottom][t + 1]];
                Vector3 c = verts[thicknessIndex[i][bottom][t + 1]];
                Vector3 d = verts[thicknessIndex[i][bottom][t]];

                Vector2 uva = uvLookupUW[i - 1][bottom][t];
                Vector2 uvb = uvLookupUW[i - 1][bottom][t + 1];
                Vector2 uvc = uvLookupUW[i][bottom][t + 1];
                Vector2 uvd = uvLookupUW[i][bottom][t];

                AddQuadFace(a, b, c, d, uva, uvb, uvc, uvd);
            }
        }

        // LEFT END CAP - use W,V lookup (CHANGED)
        for (int v = 0; v < ringSize - 1; v++)
        {
            for (int t = 0; t < thicknessSegments; t++)
            {
                Vector3 a = verts[thicknessIndex[0][v][t]];
                Vector3 b = verts[thicknessIndex[0][v + 1][t]];
                Vector3 c = verts[thicknessIndex[0][v + 1][t + 1]];
                Vector3 d = verts[thicknessIndex[0][v][t + 1]];

                Vector2 uva = uvLookupWV[0][v][t];
                Vector2 uvb = uvLookupWV[0][v + 1][t];
                Vector2 uvc = uvLookupWV[0][v + 1][t + 1];
                Vector2 uvd = uvLookupWV[0][v][t + 1];

                AddQuadFace(a, b, c, d, uva, uvb, uvc, uvd);
            }
        }

        // RIGHT END CAP - use W,V lookup (CHANGED)
        for (int v = 0; v < ringSize - 1; v++)
        {
            for (int t = 0; t < thicknessSegments; t++)
            {
                Vector3 a = verts[thicknessIndex[ringCount - 1][v][t]];
                Vector3 b = verts[thicknessIndex[ringCount - 1][v][t + 1]];
                Vector3 c = verts[thicknessIndex[ringCount - 1][v + 1][t + 1]];
                Vector3 d = verts[thicknessIndex[ringCount - 1][v + 1][t]];

                Vector2 uva = uvLookupWV[ringCount - 1][v][t];
                Vector2 uvb = uvLookupWV[ringCount - 1][v][t + 1];
                Vector2 uvc = uvLookupWV[ringCount - 1][v + 1][t + 1];
                Vector2 uvd = uvLookupWV[ringCount - 1][v + 1][t];

                AddQuadFace(a, b, c, d, uva, uvb, uvc, uvd);
            }
        }

        mesh.SetVertices(finalVerts);
        mesh.SetNormals(finalNormals);
        mesh.SetTangents(finalTangents);
        mesh.SetUVs(0, finalUVs);
        mesh.SetTriangles(finalTris, 0);
        mesh.RecalculateBounds();

        return mesh;
    }

    private void GenerateGuard()
    {
        List<List<Vector3>> rings = GenerateGuardRings(guardSamples);
        Mesh guardMesh = BuildGuardMesh3D(rings);
        meshFilter.mesh = guardMesh;

        MeshRenderer mr = GetComponent<MeshRenderer>();
        if (mr != null && guardMaterial != null)
        {
            mr.sharedMaterial = guardMaterial;
        }
    }

    private void GenerateGrip() { }
    private void GeneratePommel() { }
}