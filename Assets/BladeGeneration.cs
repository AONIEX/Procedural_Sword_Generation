using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BladeGeneration : MonoBehaviour
{
    public SplineAndLineGen splineGen;
    [Header("Detail")]
    [Range(1,10)]
    public int subDivisions = 3; // to decide how detailed the blade mesh is 
    [Range(1, 10)]
    public int tipSubdivisions = 5;// to decide how detailed the tip of the blade mesh is 


    // Start is called before the first frame update
    void Start()
    {
        splineGen = GetComponent<SplineAndLineGen>();
        splineGen.GenerateLinesAndSplines();
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

        // 1. Subdivide between each segment
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
                vertices.Add(transform.InverseTransformPoint(left));   // 0
                vertices.Add(transform.InverseTransformPoint(center)); // 1
                vertices.Add(transform.InverseTransformPoint(right));  // 2
            }
        }

        // 2. Generate triangles between rings
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

        // 3. Add tip vertex and connect final ring
        Segment tipSegment = segments[segments.Count - 1];
        Vector3 tipPoint = transform.InverseTransformPoint(tipSegment.center);
        int tipIndex = vertices.Count;
        vertices.Add(tipPoint);

        int finalRingStartIndex = ringStarts.Count - (tipSubdivisions + 1);

        int finalRingStart = ringStarts[ringStarts.Count - 1];

        triangles.Add(finalRingStart);
        triangles.Add(finalRingStart + 1);
        triangles.Add(tipIndex);

        triangles.Add(finalRingStart + 1);
        triangles.Add(finalRingStart + 2);
        triangles.Add(tipIndex);


        // 4. Finalize mesh
        bladeMesh.SetVertices(vertices);
        bladeMesh.SetTriangles(triangles, 0);
        bladeMesh.RecalculateNormals();
        bladeMesh.RecalculateTangents();

        // 5. Apply to MeshFilter and Renderer
        MeshFilter meshFilter = GetComponent<MeshFilter>();
        if (meshFilter == null) meshFilter = gameObject.AddComponent<MeshFilter>();
        meshFilter.mesh = bladeMesh;

        MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
        if (meshRenderer == null) meshRenderer = gameObject.AddComponent<MeshRenderer>();
        meshRenderer.material = new Material(Shader.Find("Standard"));
    }

    //https://www.cs.cmu.edu/~fp/courses/graphics/asst5/catmullRom.pdf
    Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        return 0.5f * (
            2f * p1 +
            (-p0 + p2) * t +
            (2f * p0 - 5f * p1 + 4f * p2 - p3) * t * t +
            (-p0 + 3f * p1 - 3f * p2 + p3) * t * t * t
        );
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            GenerateBladeMesh();
        }
    }
}
