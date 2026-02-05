using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class HiltCreation : MonoBehaviour
{
    [Header("Material")]
    public Material guardMaterial;

    [Header("Spline Generation")]
    public SplineContainer spline;
    public int pointCount = 5;
    public float splineLength = 0.3f;

    [Header("Spline Shape Controls")]
    public AnimationCurve shapeCurve = AnimationCurve.Linear(0, 0, 1, 0);
    public float maxHeight = 0.1f;

    [Header("Mesh Extrusion")]
    public int samples = 20;
    public float width = 0.05f;
    public float thickness = 0.05f;

    public AnimationCurve thicknessCurve = AnimationCurve.Linear(0, 1, 1, 1);
    public AnimationCurve widthCurve = AnimationCurve.Linear(0, 1, 1, 1);

    [Header("Ridge Controls")]
    public float ridgeDepth = 0.005f;

    private Mesh mesh;
    private Vector3 lastNormal = Vector3.up;

    void Start()
    {
        GenerateSplinePoints();
        GenerateGuard();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            GenerateSplinePoints();
            GenerateGuard();
        }
    }

    void GenerateSplinePoints()
    {
        Spline s = new Spline();
        float step = 1f / (pointCount - 1);

        for (int i = 0; i < pointCount; i++)
        {
            float t = i * step;
            float x = Mathf.Lerp(-splineLength * 0.5f, splineLength * 0.5f, t);
            float y = shapeCurve.Evaluate(t) * maxHeight;

            s.Add(new BezierKnot(new float3(x, y, 0)));
        }

        spline.Spline = s;
    }

    void GenerateGuard()
    {
        mesh = new Mesh();
        mesh.name = "GuardMesh";

        List<Vector3> verts = new List<Vector3>();
        List<int> tris = new List<int>();
        List<Vector2> uvs = new List<Vector2>();

        float step = 1f / (samples - 1);
        lastNormal = Vector3.up;

        // Each face gets its own copy of the corner vertices
        // 4 faces × (1 corner + 1 ridge + 1 corner) = 12 verts per ring
        int ringVerts = 12;

        for (int i = 0; i < samples; i++)
        {
            float t = i * step;

            SplineUtility.Evaluate(spline.Spline, t, out float3 pos, out float3 tangent, out float3 up);

            Vector3 p = (Vector3)pos;
            Vector3 forward = ((Vector3)tangent).normalized;

            // Stable normal using parallel transport
            Vector3 normal = lastNormal;
            normal = (normal - Vector3.Dot(normal, forward) * forward).normalized;

            if (normal.sqrMagnitude < 0.0001f)
                normal = ((Vector3)up).normalized;

            lastNormal = normal;

            Vector3 binormal = Vector3.Cross(forward, normal).normalized;

            float scaledThickness = thickness * thicknessCurve.Evaluate(t);
            float scaledWidth = width * widthCurve.Evaluate(t);

            // Outer corners
            Vector3 topL = p + normal * scaledThickness + binormal * scaledWidth;
            Vector3 topR = p + normal * scaledThickness - binormal * scaledWidth;
            Vector3 botR = p - normal * scaledThickness - binormal * scaledWidth;
            Vector3 botL = p - normal * scaledThickness + binormal * scaledWidth;

            // Centre ridge points (only these move)
            Vector3 ridgeTop = p + normal * (scaledThickness + ridgeDepth);
            Vector3 ridgeRight = p - binormal * (scaledWidth + ridgeDepth);
            Vector3 ridgeBottom = p - normal * (scaledThickness + ridgeDepth);
            Vector3 ridgeLeft = p + binormal * (scaledWidth + ridgeDepth);

            // --- DUPLICATED VERTICES FOR SHARP UV SEAMS ---

            // Top face (3 verts)
            verts.Add(topL);       // 0
            verts.Add(ridgeTop);   // 1
            verts.Add(topR);       // 2

            // Right face (3 verts)
            verts.Add(topR);       // 3 (duplicate)
            verts.Add(ridgeRight); // 4
            verts.Add(botR);       // 5 (duplicate)

            // Bottom face (3 verts)
            verts.Add(botR);       // 6 (duplicate)
            verts.Add(ridgeBottom);// 7
            verts.Add(botL);       // 8 (duplicate)

            // Left face (3 verts)
            verts.Add(botL);       // 9 (duplicate)
            verts.Add(ridgeLeft);  // 10
            verts.Add(topL);       // 11 (duplicate)

            // --- UVs: SHARP SEAMS BETWEEN FACES ---
            // Each face gets its own UV block with gaps between them

            float[] uFace =
            {
            0.00f, 0.10f, 0.20f,   // top
            0.40f, 0.50f, 0.60f,   // right
            0.80f, 0.90f, 1.00f,   // bottom
            1.20f, 1.30f, 1.40f    // left
        };

            for (int u = 0; u < ringVerts; u++)
                uvs.Add(new Vector2(uFace[u], t));
        }

        // Connect rings (12-sided tube)
        for (int ring = 0; ring < samples - 1; ring++)
        {
            int start = ring * ringVerts;
            int next = (ring + 1) * ringVerts;

            for (int i = 0; i < ringVerts; i++)
            {
                int a = start + i;
                int b = next + i;
                int c = next + ((i + 1) % ringVerts);
                int d = start + ((i + 1) % ringVerts);

                tris.Add(a); tris.Add(b); tris.Add(c);
                tris.Add(a); tris.Add(c); tris.Add(d);
            }
        }

        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.SetUVs(0, uvs);

        // Hard normals now work because vertices are split
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        GetComponent<MeshFilter>().mesh = mesh;

        if (guardMaterial != null)
            GetComponent<MeshRenderer>().material = guardMaterial;
    }
}