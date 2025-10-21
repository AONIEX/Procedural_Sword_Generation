using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;

public class SwordGenerator_V2 : MonoBehaviour
{
    [Header("Spline Settings")]
    public int controlPointCount = 5;
    public float maxLength = 2f;
    public float maxCurveStrength = 0.5f;
    public float minAndMaxY = 0.3f;
    public bool tipIsCentered = false;

    [Header("Blade Settings")]
    public int bladeSegmentCount = 5;
    public Vector2 widthRange = new Vector2(0.1f, 0.3f);
    public Vector2 angleRange = new Vector2(-25f, 25f);

    private SplineContainer splineContainer;
    private float totalLength;

    private struct BladeSegment
    {
        public float distance;
        public float width;
        public float angle;
        public Vector3 worldPos;
        public Vector3 worldTangent;
    }

    private List<BladeSegment> bladeSegments = new List<BladeSegment>();

    void Awake()
    {
        SetupComponents();
        GenerateSplinePoints();
        GenerateBladeMesh();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            GenerateSplinePoints();
            GenerateBladeMesh();
        }

        DrawDebugLines();
    }

    void SetupComponents()
    {
        splineContainer = GetComponent<SplineContainer>();
        if (splineContainer == null)
            splineContainer = gameObject.AddComponent<SplineContainer>();

        if (!TryGetComponent(out MeshFilter filter))
            filter = gameObject.AddComponent<MeshFilter>();

        if (!TryGetComponent(out MeshRenderer renderer))
        {
            renderer = gameObject.AddComponent<MeshRenderer>();
            renderer.material = new Material(Shader.Find("Standard"));
        }
    }

    void GenerateSplinePoints()
    {
        var spline = splineContainer.Spline;
        spline.Clear();

        float curveStrength = Random.Range(0, maxCurveStrength);

        for (int i = 0; i < controlPointCount; i++)
        {
            float x = i * (maxLength / (controlPointCount - 1));
            float normalized = (float)i / (controlPointCount - 1);
            float curve = Mathf.Sin(normalized * Mathf.PI);
            float y = (curve * curveStrength) + Random.Range(-minAndMaxY, minAndMaxY);

            if ((tipIsCentered && i == controlPointCount - 1) || i == 0)
                y = 0;

            spline.Add(new BezierKnot(new Vector3(x, y, 0)));
        }

        spline.SetTangentMode(TangentMode.AutoSmooth);
    }

    void GenerateBladeMesh()
    {
        var spline = splineContainer.Spline;
        var matrix = splineContainer.transform.localToWorldMatrix;

        totalLength = spline.CalculateLength(matrix);
        float step = totalLength / (bladeSegmentCount - 1);

        bladeSegments.Clear();
        List<Vector3> topEdge = new List<Vector3>();
        List<Vector3> bottomEdge = new List<Vector3>();

        for (int i = 0; i < bladeSegmentCount; i++)
        {
            float distance = i * step;

            Vector3 localPos = spline.EvaluatePosition(distance);
            Vector3 localTangent = spline.EvaluateTangent(distance);

            Vector3 worldPos = matrix.MultiplyPoint3x4(localPos);
            Vector3 worldTangent = matrix.MultiplyVector(localTangent).normalized;

            float width = Random.Range(widthRange.x, widthRange.y);
            float angle = Random.Range(angleRange.x, angleRange.y);

            bladeSegments.Add(new BladeSegment
            {
                distance = distance,
                width = width,
                angle = angle,
                worldPos = worldPos,
                worldTangent = worldTangent
            });

            Vector3 normal = Quaternion.AngleAxis(angle, Vector3.forward) * Vector3.up;

            topEdge.Add(worldPos + normal * width * 0.5f);
            bottomEdge.Add(worldPos - normal * width * 0.5f);
        }

        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();

        for (int i = 0; i < bladeSegmentCount - 1; i++)
        {
            int idx = vertices.Count;
            vertices.Add(topEdge[i]);
            vertices.Add(bottomEdge[i]);
            vertices.Add(topEdge[i + 1]);
            vertices.Add(bottomEdge[i + 1]);

            triangles.Add(idx);
            triangles.Add(idx + 2);
            triangles.Add(idx + 1);

            triangles.Add(idx + 1);
            triangles.Add(idx + 2);
            triangles.Add(idx + 3);
        }

        Mesh mesh = new Mesh();
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        GetComponent<MeshFilter>().mesh = mesh;
    }

    void DrawDebugLines()
    {
        foreach (var segment in bladeSegments)
        {
            Vector3 normal = Quaternion.AngleAxis(segment.angle, Vector3.forward) * Vector3.up;

            Vector3 top = segment.worldPos + normal * segment.width * 0.5f;
            Vector3 bottom = segment.worldPos - normal * segment.width * 0.5f;

            Debug.DrawLine(top, bottom, Color.red);
        }
    }
}