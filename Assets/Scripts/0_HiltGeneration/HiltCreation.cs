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

        CenterSplineVertically(s);

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

            scaledWidth = Mathf.Max(scaledWidth, width * 0.3f);      // Never go below 30% of base width
            scaledThickness = Mathf.Max(scaledThickness, thickness * 0.3f); // Never go below 30% of base thickness

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

    public void RandomiseGuard(float bladeWidth, float bladeThickness)
    {
        // 90% realistic, 10% experimental
        bool isRealistic = UnityEngine.Random.value < 0.9f;

        // === SPLINE SHAPE ===
        // Point count: realistic guards usually have 3-7 points
        pointCount = isRealistic ? UnityEngine.Random.Range(4, 8) : UnityEngine.Random.Range(4, 12);

        // Spline length: realistic crossguards are 0.2-0.5 units wide
        //splineLength = isRealistic ?
        //    UnityEngine.Random.Range(0.5f, 1.0f) :
        //    UnityEngine.Random.Range(0.4f, 1.2f);
        float widthMultiplier = isRealistic ?
       UnityEngine.Random.Range(1.5f, 2.5f) :
       UnityEngine.Random.Range(2.0f, 3.0f);

        splineLength = bladeWidth * widthMultiplier;

        // Clamp to reasonable limits
        splineLength = Mathf.Clamp(splineLength, 0.6f, 2.5f);

        // Max height: subtle curves for realistic, more dramatic for experimental
        maxHeight = isRealistic ?
            UnityEngine.Random.Range(0.075f, 0.12f) :
            UnityEngine.Random.Range(0.06f, 0.15f);

        // Shape curve: choose from preset curve types
        float curveRoll = UnityEngine.Random.value;

        //if (isRealistic)
        //{
        // Shape curve: FLAT, U, or V only (no inverted shapes)

        if (curveRoll < 0.5f)
        {
            // FLAT (50%)
            shapeCurve = AnimationCurve.Linear(0, 0, 1, 0);
        }
        else if (curveRoll < 0.8f)
        {
            // U-SHAPE (30%) – smooth downward bow
            float depth = UnityEngine.Random.Range(0.5f, 1.0f);

            shapeCurve = new AnimationCurve(
                new Keyframe(0, 0),
                new Keyframe(0.5f, -depth),
                new Keyframe(1, 0)
            );

            for (int i = 0; i < shapeCurve.keys.Length; i++)
                shapeCurve.SmoothTangents(i, 0.35f);
        }
        else
        {
            // V-SHAPE (20%) – sharper downward point
            float depth = UnityEngine.Random.Range(0.7f, 1.0f);

            shapeCurve = new AnimationCurve(
                new Keyframe(0, 0),
                new Keyframe(0.5f, -depth),
                new Keyframe(1, 0)
            );

            for (int i = 0; i < shapeCurve.keys.Length; i++)
                shapeCurve.SmoothTangents(i, 0f);
        }

        //}
        //else
        //{
        //    // Experimental: more varied curves
        //    int keyCount = UnityEngine.Random.Range(3, 6);
        //    Keyframe[] keys = new Keyframe[keyCount];

        //    keys[0] = new Keyframe(0, 0);
        //    keys[keyCount - 1] = new Keyframe(1, 0);

        //    for (int i = 1; i < keyCount - 1; i++)
        //    {
        //        float time = i / (float)(keyCount - 1);
        //        keys[i] = new Keyframe(time, UnityEngine.Random.Range(-1f, 1f));
        //    }

        //    shapeCurve = new AnimationCurve(keys);

        //    for (int i = 0; i < keyCount; i++)
        //        shapeCurve.SmoothTangents(i, 0.5f);
        //}

        // === MESH QUALITY ===
        samples = 20;
        //isRealistic ?
        //    UnityEngine.Random.Range(10, 20) :
        //    UnityEngine.Random.Range(10, 30);

        // === CROSS-SECTION SIZE ===
        // Width: horizontal size of the guard
        //width = isRealistic ?
        //    UnityEngine.Random.Range(0.07f, 0.09f) :
        //    UnityEngine.Random.Range(0.08f, 0.12f);

        // Thickness: vertical size of the guard
        thickness = isRealistic ?
            UnityEngine.Random.Range(0.02f, 0.035f) :
            UnityEngine.Random.Range(0.02f, 0.055f);

            float widthMult = isRealistic ?
           UnityEngine.Random.Range(0.9f, 1.2f) :
           UnityEngine.Random.Range(0.85f, 1.3f);

        width = bladeThickness * widthMult;

        // === TAPER CURVES ===
        // How width/thickness change along the guard

        float widthRoll = UnityEngine.Random.value;
        float thickRoll = UnityEngine.Random.value;

        if (isRealistic && thickRoll < 0.5f)
        {
            thicknessCurve = GenerateWaistedWidthCurve();
        } else
        {
            thicknessCurve = GenerateGuardTaperCurve(isRealistic);

        }


        if (isRealistic && widthRoll < 0.5f)
        {
            // 50%: waisted guard (most common historically)
            widthCurve = GenerateWaistedWidthCurve();

        }
        else
        {
            // Remaining cases: U / V / flat logic
            widthCurve = GenerateGuardTaperCurve(isRealistic);

        }

        // === RIDGE DEPTH ===
        ridgeDepth = isRealistic ?
            UnityEngine.Random.Range(0.003f, 0.008f) :
            UnityEngine.Random.Range(0.0f, 0.015f);

        // Regenerate with new parameters
        GenerateSplinePoints();
        GenerateGuard();
    }

    AnimationCurve GenerateWaistedWidthCurve()
    {
        // INCREASED minimum end value to prevent pinching
        float endValue = UnityEngine.Random.Range(0.5f, 0.65f); // CHANGED from 0.25f-0.4f
        float shoulderTimeL = UnityEngine.Random.Range(0.15f, 0.25f);
        float shoulderTimeR = UnityEngine.Random.Range(0.75f, 0.85f);

        // Shoulder should be noticeably larger than ends
        float shoulderValue = endValue + UnityEngine.Random.Range(0.15f, 0.25f); // CHANGED from 0.0f-0.05f
        float centerValue = UnityEngine.Random.Range(0.95f, 1.1f); // CHANGED from 0.9f-1.15f

        AnimationCurve curve = new AnimationCurve(
            new Keyframe(0f, endValue),
            new Keyframe(shoulderTimeL, shoulderValue),
            new Keyframe(0.5f, centerValue),
            new Keyframe(shoulderTimeR, shoulderValue),
            new Keyframe(1f, endValue)
        );

        // Smooth everything for a forged look
        for (int i = 0; i < curve.keys.Length; i++)
            curve.SmoothTangents(i, 0.35f);

        return curve;
    }


    AnimationCurve GenerateGuardTaperCurve(bool isRealistic)
    {
        if (!isRealistic)
            return GenerateRandomTaperCurve();

        float roll = UnityEngine.Random.value;

        // FLAT (very common)
        if (roll < 0.35f)
        {
            return AnimationCurve.Linear(0, 1f, 1, 1f);
        }
        // U SHAPE (thin ends, thick middle)
        else if (roll < 0.60f)
        {
            float end = UnityEngine.Random.Range(0.7f, 0.9f); // CHANGED from 0.6f-0.85f
            return new AnimationCurve(
                new Keyframe(0, end),
                new Keyframe(0.5f, 1f),
                new Keyframe(1, end)
            );
        }
        // INVERTED U (thick ends)
        else if (roll < 0.75f)
        {
            float end = UnityEngine.Random.Range(1.05f, 1.25f); // CHANGED from 1.1f-1.35f
            return new AnimationCurve(
                new Keyframe(0, end),
                new Keyframe(0.5f, 1f),
                new Keyframe(1, end)
            );
        }
        // V SHAPE (sharp middle taper)
        else if (roll < 0.90f)
        {
            float mid = UnityEngine.Random.Range(0.65f, 0.85f); // CHANGED from 0.5f-0.8f
            AnimationCurve curve = new AnimationCurve(
                new Keyframe(0, 1f),
                new Keyframe(0.5f, mid),
                new Keyframe(1, 1f)
            );

            for (int i = 0; i < curve.keys.Length; i++)
                curve.SmoothTangents(i, 0f);

            return curve;
        }
        // INVERTED V (sharp middle bulge)
        else
        {
            float mid = UnityEngine.Random.Range(1.15f, 1.4f); // CHANGED from 1.2f-1.5f
            AnimationCurve curve = new AnimationCurve(
                new Keyframe(0, 1f),
                new Keyframe(0.5f, mid),
                new Keyframe(1, 1f)
            );

            for (int i = 0; i < curve.keys.Length; i++)
                curve.SmoothTangents(i, 0f);

            return curve;
        }
    }

    // Also update the random taper curve to be less extreme:
    private AnimationCurve GenerateRandomTaperCurve()
    {
        int keyCount = UnityEngine.Random.Range(3, 5);
        Keyframe[] keys = new Keyframe[keyCount];

        for (int i = 0; i < keyCount; i++)
        {
            float time = i / (float)(keyCount - 1);
            float value = UnityEngine.Random.Range(0.7f, 1.3f); // CHANGED from 0.5f-1.5f
            keys[i] = new Keyframe(time, value);
        }

        AnimationCurve curve = new AnimationCurve(keys);

        // Smooth the curve
        for (int i = 0; i < keyCount; i++)
            curve.SmoothTangents(i, 0.5f);

        return curve;
    }

    void CenterSplineVertically(Spline s)
    {
        // Option A: use midpoint (best for guards)
        float midT = 0.5f;
        SplineUtility.Evaluate(s, midT, out float3 midPos, out _, out _);

        float yOffset = midPos.y;

        // Move every knot so midpoint sits at y = 0
        for (int i = 0; i < s.Count; i++)
        {
            BezierKnot knot = s[i];
            float3 pos = knot.Position;
            pos.y -= yOffset;
            knot.Position = pos;
            s[i] = knot;
        }
    }

}