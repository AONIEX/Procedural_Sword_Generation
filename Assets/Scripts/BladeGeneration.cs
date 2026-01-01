using System.Collections.Generic;
using UnityEngine;

public class BladeGeneration : MonoBehaviour
{
    public SplineAndLineGen splineGen;

    [Header("Detail")]
    [Range(1, 20)] public int subDivisions = 3;
    [Range(1, 20)] public int tipSubdivisions = 5;
    [Range(2, 20)] public int spineResolution = 5;

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

    [Header("Rendering")]
    public Material bladeMaterial;

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
        List<int> triangles = new List<int>();

        var segments = splineGen.segments;
        if (segments == null) return;

        List<Vector3> smoothLefts = new List<Vector3>();
        List<Vector3> smoothRights = new List<Vector3>();
        List<Vector3> smoothCenters = new List<Vector3>();

        GenerateSmoothSegments(segments, smoothLefts, smoothRights, smoothCenters);

        //
        //Generating The Front Faces
        //
        List<int> frontTriangels = new List<int>();
        GenerateEdgeGeometry(segments, smoothLefts, smoothRights, vertices, frontTriangels);

        triangles.AddRange(frontTriangels);

        int frontVertexCount = vertices.Count;
       
        //
        //Calculating Blade Normals
        //
        Vector3 widthDir = (smoothRights[0] - smoothLefts[0]).normalized;
        Vector3 forwardDir = (smoothCenters[1] - smoothCenters[0]).normalized;
        Vector3 BladeNormal = Vector3.Cross(widthDir, forwardDir).normalized;
        float halfThickness = bladeThickness * 0.5f;

        //Moves front face forward half of the blades thickness
        for (int i = 0; i < frontVertexCount; i++)
        {
            vertices[i] += BladeNormal * halfThickness;
        }

        //
        //Duplicate Back Vertices 
        //
        for (int i = 0; i < frontVertexCount; i++) {
            vertices.Add(vertices[i] - BladeNormal * bladeThickness);
        }

        //
        //Genertating Back Faces
        //
        for (int i = 0; i < frontTriangels.Count; i += 3) {

            int a = frontTriangels[i] + frontVertexCount;
            int b = frontTriangels[i + 1] + frontVertexCount;
            int c = frontTriangels[i + 2] + frontVertexCount;

            triangles.Add(c);
            triangles.Add(b);
            triangles.Add(a);
        }

        //
        //Side Walls (This is where i will create sharp edges later)
        //
        int ringSize = spineResolution;
        int ringCount = smoothLefts.Count;
        
        for (int i = 0; i < ringCount - 1; i++)
        {
            int frontA = i * ringSize;
            int frontB = (i + 1) * ringSize;

            int backA = frontA + frontVertexCount;
            int backB = frontB + frontVertexCount;

            triangles.Add(frontA);
            triangles.Add(backA);
            triangles.Add(frontB);

            triangles.Add(frontB);
            triangles.Add(backA);
            triangles.Add(backB);

            int fr = frontA + ringSize - 1;
            int frNext = frontB + ringSize - 1;

            int br = fr + frontVertexCount;
            int brNext = frNext + frontVertexCount;

            triangles.Add(frNext);
            triangles.Add(br);
            triangles.Add(fr);

            triangles.Add(frNext);
            triangles.Add(brNext);
            triangles.Add(br);
        }

        //
        //Create Mesh
        //
        mesh3D.SetVertices(vertices);
        mesh3D.SetTriangles(triangles, 0);
        mesh3D.RecalculateNormals();
        mesh3D.RecalculateTangents();

        MeshFilter meshFilter = GetComponent<MeshFilter>();
        if (meshFilter == null) meshFilter = gameObject.AddComponent<MeshFilter>();
        meshFilter.mesh = mesh3D;

        MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
        if (meshRenderer == null) meshRenderer = gameObject.AddComponent<MeshRenderer>();
        meshRenderer.material = new Material(Shader.Find("Standard"));

        if (bladeMaterial != null)
        {
            meshRenderer.material = bladeMaterial;
        }
        else
        {
            meshRenderer.material = new Material(Shader.Find("Standard"));
        }
    }

    public void GenerateBladeMesh2D()
    {
        Mesh Mesh2D = new Mesh();
        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();
        var segments = splineGen.segments;
        if (segments == null || segments.Count < 2) return;

        List<Vector3> smoothLefts = new List<Vector3>();
        List<Vector3> smoothRights = new List<Vector3>();
        List<Vector3> smoothCenters = new List<Vector3>();

        GenerateSmoothSegments(segments, smoothLefts, smoothRights, smoothCenters);
        GenerateEdgeGeometry(segments, smoothLefts, smoothRights, vertices, triangles);



        Mesh2D.SetVertices(vertices);
        Mesh2D.SetTriangles(triangles, 0);
        Mesh2D.RecalculateNormals();
        Mesh2D.RecalculateTangents();

        MeshFilter meshFilter = GetComponent<MeshFilter>();
        if (meshFilter == null) meshFilter = gameObject.AddComponent<MeshFilter>();
        meshFilter.mesh = Mesh2D;

        MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
        if (meshRenderer == null) meshRenderer = gameObject.AddComponent<MeshRenderer>();

        if (bladeMaterial != null)
        {
            meshRenderer.material = bladeMaterial;
        }
        else
        {
            meshRenderer.material = new Material(Shader.Find("Standard"));
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
            int currentSubdivisions = isTipSegment ? tipSubdivisions : subDivisions;

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

    public void GenerateEdgeGeometry(List<Segment> segments,List<Vector3> smoothLefts, List<Vector3> smoothRights, List<Vector3> vertices, List<int> triangles)
    {
        List<int> ringStarts = new List<int>();

        int ringCount = smoothLefts.Count;

        for (int i = 0; i < ringCount; i++)
        {
            Vector3 left = smoothLefts[i];
            Vector3 right = smoothRights[i];

            ringStarts.Add(vertices.Count);

            for (int s = spineResolution - 1; s >= 0; s--)
            {
                float t = s / (float)(spineResolution - 1);
                Vector3 point = Vector3.Lerp(left, right, t);
                vertices.Add(point);
            }
        }


        for (int i = 0; i < ringStarts.Count - 1; i++)
        {
            int baseA = ringStarts[i];
            int baseB = ringStarts[i + 1];

            for (int j = 0; j < spineResolution - 1; j++)
            {
                int a = baseA + j;
                int b = baseA + j + 1;
                int c = baseB + j;
                int d = baseB + j + 1;

                triangles.Add(a);
                triangles.Add(c);
                triangles.Add(b);

                triangles.Add(b);
                triangles.Add(c);
                triangles.Add(d);
            }
        }

        Segment tipSegment = segments[segments.Count - 1];
        Vector3 tipPoint = tipSegment.center;
        int tipIndex = vertices.Count;
        vertices.Add(tipPoint);

        int finalRingStart = ringStarts[ringStarts.Count - 1];
        for (int i = 0; i < spineResolution - 1; i++)
        {
            triangles.Add(finalRingStart + i);
            triangles.Add(tipIndex);
            triangles.Add(finalRingStart + i + 1);
        }
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