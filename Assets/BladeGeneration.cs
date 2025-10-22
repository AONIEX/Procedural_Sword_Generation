using System.Collections.Generic;
using UnityEngine;

public class BladeGeneration : MonoBehaviour
{
    public SplineAndLineGen splineGen;

    [Header("Detail")]
    [Range(1, 10)] public int subDivisions = 3;
    [Range(1, 10)] public int tipSubdivisions = 5;

    void Start()
    {
        splineGen = GetComponent<SplineAndLineGen>();
        splineGen.GenerateLinesAndSplines();
        SmoothSegmentCenters(); // optional smoothing
        GenerateBladeMesh();
    }

    public void GenerateBladeMesh()
    {
        Mesh bladeMesh = new Mesh();
        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();
        var segments = splineGen.segments;
        if (segments == null || segments.Count < 2) return;

        List<int> ringStarts = new List<int>();

        for (int i = 0; i < segments.Count - 1; i++)
        {
            Segment a = segments[i];
            Segment b = segments[i + 1];
            int currentSubdivisions = (i == segments.Count - 2) ? tipSubdivisions : subDivisions;

            for (int j = 0; j <= currentSubdivisions; j++)
            {
                float t = j / (float)currentSubdivisions;

                Segment p0 = segments[Mathf.Max(i - 1, 0)];
                Segment p1 = segments[i];
                Segment p2 = segments[i + 1];
                Segment p3 = segments[Mathf.Min(i + 2, segments.Count - 1)];

                Vector3 center = CatmullRom(p0.center, p1.center, p2.center, p3.center, t);
                Vector3 left = CatmullRom(p0.left, p1.left, p2.left, p3.left, t);
                Vector3 right = CatmullRom(p0.right, p1.right, p2.right, p3.right, t);

                ringStarts.Add(vertices.Count);
                vertices.Add(transform.InverseTransformPoint(left));
                vertices.Add(transform.InverseTransformPoint(center));
                vertices.Add(transform.InverseTransformPoint(right));
            }
        }

        for (int i = 0; i < ringStarts.Count - 1; i++)
        {
            int baseA = ringStarts[i];
            int baseB = ringStarts[i + 1];

            // Left strip
            triangles.Add(baseA);
            triangles.Add(baseA + 1);
            triangles.Add(baseB);

            triangles.Add(baseA + 1);
            triangles.Add(baseB + 1);
            triangles.Add(baseB);

            // Right strip
            triangles.Add(baseA + 1);
            triangles.Add(baseA + 2);
            triangles.Add(baseB + 1);

            triangles.Add(baseA + 2);
            triangles.Add(baseB + 2);
            triangles.Add(baseB + 1);
        }

        Segment tipSegment = segments[segments.Count - 1];
        Vector3 tipPoint = transform.InverseTransformPoint(tipSegment.center);
        int tipIndex = vertices.Count;
        vertices.Add(tipPoint);

        int finalRingStart = ringStarts[ringStarts.Count - 1];

        triangles.Add(finalRingStart);
        triangles.Add(finalRingStart + 1);
        triangles.Add(tipIndex);

        triangles.Add(finalRingStart + 1);
        triangles.Add(finalRingStart + 2);
        triangles.Add(tipIndex);

        bladeMesh.SetVertices(vertices);
        bladeMesh.SetTriangles(triangles, 0);
        bladeMesh.RecalculateNormals();
        bladeMesh.RecalculateTangents();

        MeshFilter meshFilter = GetComponent<MeshFilter>();
        if (meshFilter == null) meshFilter = gameObject.AddComponent<MeshFilter>();
        meshFilter.mesh = bladeMesh;

        MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
        if (meshRenderer == null) meshRenderer = gameObject.AddComponent<MeshRenderer>();
        meshRenderer.material = new Material(Shader.Find("Standard"));
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

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            splineGen.GenerateLinesAndSplines();
            SmoothSegmentCenters();
            GenerateBladeMesh();
        }
    }

    void OnDrawGizmos()
    {
        if (splineGen?.segments == null) return;

        Gizmos.color = Color.cyan;
        foreach (var seg in splineGen.segments)
        {
            Gizmos.DrawSphere(seg.center, 0.01f);
            Gizmos.DrawLine(seg.left, seg.right);
        }
    }
}