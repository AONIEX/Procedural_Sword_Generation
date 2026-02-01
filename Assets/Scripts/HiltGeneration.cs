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
    public SplineContainer guardSpline;   // Assign in Inspector
    [Range(0.05f, 0.5f)]
    public float guardHalfLength = 0.25f;
    [Range(-0.5f, 0.5f)]
    public float verticalCurve = 0.05f;   // Up/Down curve
    [Range(0.05f, 0.5f)]

    public float guardWidth = 0.1f;       // Thickness in X direction
    [Range(0.05f, 0.5f)]
    public float guardThickness = 0.05f;  // Thickness in Z direction
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
    public float topCenterHeight = 0.05f;   // How much higher the middle is

    public AnimationCurve topProfile = AnimationCurve.EaseInOut(0, 0, 1, 0);

    [Header("Across Thickness Top Shape")]
    [Range(0f, 0.1f)]
    public float acrossCenterHeight = 0.03f;

    public AnimationCurve acrossProfile = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Range(3,20)]
    public int controlPoints = 4;         // Spline resolution
    [Range(20, 200)]
    public int guardSamples = 20;         // Mesh resolution


    [SerializeField] private float guardHalfWidth = 0.25f;
    [SerializeField] private float guardCurveHeight = 0.05f;
    [SerializeField] private int guardCurveResolution = 4;

    [SerializeField] private int guardSegments = 10;

    private MeshFilter meshFilter;

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

    // ---------------------------------------------------------
    // 1. Generate spline control points procedurally (XY plane)
    // ---------------------------------------------------------
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

            // Base guard height along spline
            float baseHeight = guardHeight * guardHeightCurve.Evaluate(t);
            float halfH = baseHeight * 0.5f;

            // Crown mask: 0 at edges, 1 at center
            float centerMask = 1f - Mathf.Abs(t - 0.5f) * 2f;
            float crown = topCenterHeight * topProfile.Evaluate(centerMask);

            Vector3 center = SplineUtility.EvaluatePosition(spline, t);
            Vector3 tangent = SplineUtility.EvaluateTangent(spline, t);

            List<Vector3> ring = new List<Vector3>();

            for (int v = 0; v <= verticalSegments; v++)
            {
                float vNorm = v / (float)verticalSegments;
                float y = Mathf.Lerp(-halfH, halfH, vNorm);

                // Smoothly blend crown into the top area
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

    List<Vector3> finalVerts = new();
    List<Vector3> finalNormals = new();
    List<Vector2> finalUVs = new();
    List<int> finalTris = new();

    int AddVert(Vector3 p, Vector3 n, Vector2 uv)
    {
        int i = finalVerts.Count;
        finalVerts.Add(p);
        finalNormals.Add(n);
        finalUVs.Add(uv);
        return i;
    }

    void AddQuadFace(
        Vector3 a, Vector3 b, Vector3 c, Vector3 d,
        Vector3 normal,
        Vector2 uva, Vector2 uvb, Vector2 uvc, Vector2 uvd)
    {
        int ia = AddVert(a, normal, uva);
        int ib = AddVert(b, normal, uvb);
        int ic = AddVert(c, normal, uvc);
        int id = AddVert(d, normal, uvd);

        finalTris.Add(ia); finalTris.Add(ib); finalTris.Add(ic);
        finalTris.Add(ia); finalTris.Add(ic); finalTris.Add(id);
    }


    // ---------------------------------------------------------
    // 3. Build full 3D guard mesh
    // ---------------------------------------------------------
    private Mesh BuildGuardMesh3D(List<List<Vector3>> rings)
    {
        Mesh mesh = new Mesh();

        List<Vector3> verts = new List<Vector3>();
        List<int> tris = new List<int>();
        List<Vector2> uvs = new List<Vector2>();

        int ringCount = rings.Count;
        int ringSize = rings[0].Count;
        float halfT = guardThickness * 0.5f;

        // thicknessIndex[ring][vertical][thicknessLayer]
        List<List<List<int>>> thicknessIndex = new List<List<List<int>>>();

        // ---------------------------------------------------------
        // BUILD ALL THICKNESS LAYERS (WITH ACROSS-TOP SHAPING)
        // ---------------------------------------------------------
        for (int i = 0; i < ringCount; i++)
        {
            float u = i / (float)(ringCount - 1);
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

                    // Mask: 0 at front/back edges, 1 in middle
                    float acrossMask = 1f - Mathf.Abs(tNorm - 0.5f) * 2f;

                    float yOffset = 0f;

                    // Top across-thickness crown
                    if (v >= ringSize - 2)
                    {
                        float blend = (vNorm - (1f - 1f / (ringSize - 1))) * (ringSize - 1);
                        yOffset += acrossCenterHeight * acrossProfile.Evaluate(acrossMask) * blend;
                    }

                    // Bottom across-thickness crown (mirrored)
                    if (v <= 1)
                    {
                        float blend = (1f / (ringSize - 1) - vNorm) * (ringSize - 1);
                        yOffset -= acrossCenterHeight * acrossProfile.Evaluate(acrossMask) * blend;
                    }

                    layerRow.Add(verts.Count);
                    verts.Add(basePos + Vector3.forward * zOffset + Vector3.up * yOffset);
                    uvs.Add(new Vector2(u, vNorm));
                }

                ringLayers.Add(layerRow);
            }

            thicknessIndex.Add(ringLayers);
        }

        int frontLayer = 0;
        int backLayer = thicknessSegments;
        int top = verticalSegments;
        int bottom = 0;

        // FRONT FACE
        for (int i = 1; i < ringCount; i++)
            for (int v = 0; v < ringSize - 1; v++)
            {
                Vector3 a = verts[thicknessIndex[i - 1][v][0]];
                Vector3 b = verts[thicknessIndex[i][v][0]];
                Vector3 c = verts[thicknessIndex[i][v + 1][0]];
                Vector3 d = verts[thicknessIndex[i - 1][v + 1][0]];

                Vector3 n = Vector3.forward;

                float u0 = (i - 1f) / (ringCount - 1);
                float u1 = i / (float)(ringCount - 1);
                float v0 = v / (float)(ringSize - 1);
                float v1 = (v + 1f) / (ringSize - 1);

                AddQuadFace(a, b, c, d, n,
                    new(u0, v0), new(u1, v0),
                    new(u1, v1), new(u0, v1));
            }


        // BACK FACE
        int back = thicknessSegments;

        for (int i = 1; i < ringCount; i++)
            for (int v = 0; v < ringSize - 1; v++)
            {
                Vector3 a = verts[thicknessIndex[i - 1][v + 1][back]];
                Vector3 b = verts[thicknessIndex[i][v + 1][back]];
                Vector3 c = verts[thicknessIndex[i][v][back]];
                Vector3 d = verts[thicknessIndex[i - 1][v][back]];

                Vector3 n = Vector3.back;

                float u0 = (i - 1f) / (ringCount - 1);
                float u1 = i / (float)(ringCount - 1);
                float v0 = v / (float)(ringSize - 1);
                float v1 = (v + 1f) / (ringSize - 1);

                AddQuadFace(a, b, c, d, n,
                    new(u0, v1), new(u1, v1),
                    new(u1, v0), new(u0, v0));
            }


        // TOP FACE

        for (int i = 1; i < ringCount; i++)
            for (int t = 0; t < thicknessSegments; t++)
            {
                Vector3 a = verts[thicknessIndex[i - 1][top][t]];
                Vector3 b = verts[thicknessIndex[i][top][t]];
                Vector3 c = verts[thicknessIndex[i][top][t + 1]];
                Vector3 d = verts[thicknessIndex[i - 1][top][t + 1]];

                Vector3 n = Vector3.up;

                AddQuadFace(a, b, c, d, n,
                    new(a.x, a.z), new(b.x, b.z),
                    new(c.x, c.z), new(d.x, d.z));
            }


        // BOTTOM FACE

        for (int i = 1; i < ringCount; i++)
            for (int t = 0; t < thicknessSegments; t++)
            {
                Vector3 a = verts[thicknessIndex[i - 1][bottom][t]];
                Vector3 b = verts[thicknessIndex[i - 1][bottom][t + 1]];
                Vector3 c = verts[thicknessIndex[i][bottom][t + 1]];
                Vector3 d = verts[thicknessIndex[i][bottom][t]];

                Vector3 n = Vector3.down;

                AddQuadFace(a, b, c, d, n,
                    new(a.x, a.z), new(b.x, b.z),
                    new(c.x, c.z), new(d.x, d.z));
            }


        // END CAPS
        for (int v = 0; v < ringSize - 1; v++)
            for (int t = 0; t < thicknessSegments; t++)
            {
                // LEFT
                Vector3 a = verts[thicknessIndex[0][v][t]];
                Vector3 b = verts[thicknessIndex[0][v + 1][t]];
                Vector3 c = verts[thicknessIndex[0][v + 1][t + 1]];
                Vector3 d = verts[thicknessIndex[0][v][t + 1]];

                AddQuadFace(a, b, c, d, Vector3.left,
                    new(a.z, a.y), new(b.z, b.y),
                    new(c.z, c.y), new(d.z, d.y));

                // RIGHT
                a = verts[thicknessIndex[ringCount - 1][v][t]];
                b = verts[thicknessIndex[ringCount - 1][v][t + 1]];
                c = verts[thicknessIndex[ringCount - 1][v + 1][t + 1]];
                d = verts[thicknessIndex[ringCount - 1][v + 1][t]];

                AddQuadFace(a, b, c, d, Vector3.right,
                    new(a.z, a.y), new(b.z, b.y),
                    new(c.z, c.y), new(d.z, d.y));
            }


        mesh.SetVertices(finalVerts);
        mesh.SetNormals(finalNormals);
        mesh.SetUVs(0, finalUVs);
        mesh.SetTriangles(finalTris, 0);
        mesh.RecalculateBounds();

        return mesh;
    }


    private void AddQuad(List<int> tris, int a, int b, int c, int d)
    {
        // Flipped winding
        tris.Add(a); tris.Add(c); tris.Add(b);
        tris.Add(a); tris.Add(d); tris.Add(c);
    }

    // ---------------------------------------------------------
    // 4. Generate guard mesh
    // ---------------------------------------------------------
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