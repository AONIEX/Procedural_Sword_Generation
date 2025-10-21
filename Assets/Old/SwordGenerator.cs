using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;

public class SwordGenerator : MonoBehaviour
{

    [Header("Base Needs")]

    [Range(20, 500)]
    public int detailCount = 20;
    private SplineContainer splineContainer;
    [Range(3, 50)]
    public int controlPointCount = 5;

    [Header("Changable Variables")]
    [Range(1, 3)]
    public float maxHeight;
    [Range(0, 1)]
    public float minAndMaxX = 0.3f;

    [Header("Curves")]
    [Range(0,1)]
    public float maxCurveStrength = 0.5f;
    public float curveStrength = 0.5f;

    [Header("Width")]
    public float bladeWidth = .1f;

    [Header("Thickness")]
    public float bladeThickness = .1f;

    [Header("Tip Height")]
    public float tipHeight = .2f;

    [Header("Bools")]
    public bool tipIsCentered = false;
    public bool showBackBone = false;


    void Awake()
    {
        // Add and initialize the SplineContainer
        splineContainer = gameObject.AddComponent<SplineContainer>();
        GenerateSplinePoints();
        GenerateMesh();
    }

    public void GenerateSplinePoints()
    {
        var spline = splineContainer.Spline;
        spline.Clear();

        // Define control points for a simple curved midline

         float lastPoitionX = 0;
        curveStrength = Random.Range(0, maxCurveStrength);
        //Generate x,y,z for each control point with some randomness;
        for (int i = 0; i < controlPointCount; i++)
        {
            float heightStep = maxHeight / (controlPointCount - 1);
            float y = i  * heightStep;

            float normalized = (float)i / (controlPointCount - 1); // 0 to 1
            float curve = Mathf.Sin(normalized * Mathf.PI);
            float x = (curve * curveStrength) + Random.Range(-minAndMaxX, minAndMaxX); // slight noise

            //Variation for curvature
            if ((tipIsCentered && i == controlPointCount - 1) || i==0)
            {
                x = 0;
            }
            lastPoitionX = x;
            //Variation for Thicknesss
            float z = 0;
            spline.Add(new BezierKnot(new Vector3(x, y, z)));
        }
        if (showBackBone)
        {
            spline.Add(new BezierKnot(new Vector3(0, 0, 0)));
        }

        spline.SetTangentMode(TangentMode.AutoSmooth);

    }


    public void GenerateMesh()
    {
        var spline = splineContainer.Spline;
        var matrix = splineContainer.transform.localToWorldMatrix;

        float totalLength = spline.CalculateLength(matrix);
        float step = totalLength / detailCount;

        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();

        // Build segment vertices
        for (int i = 0; i < detailCount; i++)
        {
            float distance = i * step;

            Vector3 localCentre = spline.EvaluatePosition(distance);
            Vector3 localTangent = spline.EvaluateTangent(distance);

            Vector3 centre = matrix.MultiplyPoint3x4(localCentre);
            Vector3 tangent = matrix.MultiplyVector(localTangent).normalized;
            Vector3 normal = Vector3.Cross(tangent, -Vector3.forward).normalized;

            Vector3 left = centre - normal * bladeWidth;
            Vector3 right = centre + normal * bladeWidth;

            vertices.Add(left);   // index 0
            vertices.Add(centre); // index 1
            vertices.Add(right);  // index 2
        }

        // Final tip vertex — sharp and directional
        Vector3 finalLocalCentre = spline.EvaluatePosition(totalLength);
        Vector3 finalLocalTangent = spline.EvaluateTangent(totalLength);

        Vector3 finalCentre = matrix.MultiplyPoint3x4(finalLocalCentre);
        Vector3 finalTangent = matrix.MultiplyVector(finalLocalTangent).normalized;

        Vector3 tip = finalCentre + finalTangent * tipHeight;
        vertices.Add(tip); // tipIndex = vertices.Count - 1

        // Build triangles between segments
        for (int i = 0; i < detailCount - 1; i++)
        {
            int index = i * 3;

            // Triangle 1: left, next left, center
            triangles.Add(index);
            triangles.Add(index + 3);
            triangles.Add(index + 1);

            // Triangle 2: center, next left, next center
            triangles.Add(index + 1);
            triangles.Add(index + 3);
            triangles.Add(index + 4);

            // Triangle 3: center, next center, right
            triangles.Add(index + 1);
            triangles.Add(index + 4);
            triangles.Add(index + 2);

            // Triangle 4: right, next center, next right
            triangles.Add(index + 2);
            triangles.Add(index + 4);
            triangles.Add(index + 5);
        }

        // Final tip triangles
        int last = (detailCount - 1) * 3;
        int tipIndex = vertices.Count - 1;

        triangles.Add(last);       // left
        triangles.Add(tipIndex);   // tip
        triangles.Add(last + 1);   // center

        triangles.Add(last + 1);
        triangles.Add(tipIndex);
        triangles.Add(last + 2);   // right

        // Create mesh
        Mesh mesh = new Mesh();
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();

        // Assign mesh
        MeshFilter filter = GetComponent<MeshFilter>();
        if (filter == null) filter = gameObject.AddComponent<MeshFilter>();
        filter.mesh = mesh;

        MeshRenderer renderer = GetComponent<MeshRenderer>();
        if (renderer == null) renderer = gameObject.AddComponent<MeshRenderer>();
        if (renderer.material == null)
            renderer.material = new Material(Shader.Find("Standard"));

        renderer.material.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
    }
    void Update()
    {
        // Visualise blade in debug with detail differences
        var spline = splineContainer.Spline;
        var matrix = splineContainer.transform.localToWorldMatrix;

        float totalLength = spline.CalculateLength(matrix);
        float step = totalLength / detailCount;

        Vector3 prev = matrix.MultiplyPoint3x4(spline.EvaluatePosition(0));

        for (int i = 1; i <= detailCount; i++)
        {
            float distance = i * step;
            Vector3 current = matrix.MultiplyPoint3x4(spline.EvaluatePosition(distance));
            Debug.DrawLine(prev, current, Color.green, 0f);

            if (i == detailCount - 1)
            {
                // Final center and tangent
                Vector3 finalLocalCentre = spline.EvaluatePosition(totalLength);
                Vector3 finalLocalTangent = spline.EvaluateTangent(totalLength);

                Vector3 finalCentre = matrix.MultiplyPoint3x4(finalLocalCentre);
                Vector3 finalTangent = matrix.MultiplyVector(finalLocalTangent).normalized;
                Vector3 finalNormal = Vector3.Cross(finalTangent, -Vector3.forward).normalized;

                
                Vector3 tip = finalCentre + finalTangent * tipHeight;

                Vector3 left = finalCentre - finalNormal * bladeWidth;
                Vector3 right = finalCentre + finalNormal * bladeWidth;

                // Draw tip direction and connections
                Debug.DrawLine(finalCentre, tip, Color.magenta, 0f);     // Tip direction
                Debug.DrawLine(left, tip, Color.magenta, 0f);            // Left edge to tip
                Debug.DrawLine(right, tip, Color.magenta, 0f);           // Right edge to tip
            }

            else
            {
                Debug.DrawLine(prev + new Vector3(bladeWidth, 0, 0), current + new Vector3(bladeWidth, 0, 0), Color.red, 0f);
                Debug.DrawLine(prev + new Vector3(-bladeWidth, 0, 0), current + new Vector3(-bladeWidth, 0, 0), Color.red, 0f);
            }

            prev = current;
        }

        if (Input.GetKeyDown(KeyCode.Space))
        {
            GenerateSplinePoints();

            int z = 0;
            foreach (var knot in spline.Knots)
            {
                Vector3 localPos = knot.Position;
                Vector3 worldPos = matrix.MultiplyPoint3x4(localPos);
                Debug.Log($"Point: {z}, Local: {localPos}, World: {worldPos}");
                z++;
            }

            GenerateMesh();
        }
    }
}