using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;

public class SplineAndLineGen : MonoBehaviour
{
    private struct Segment
    {
        public Vector3 center;
        public Vector3 left;
        public Vector3 right;
    }

    public int pointCount = 5;
    public float heightSpacing = 0.5f;
    public Vector2 minAndMaxWidth = new Vector2(0.2f, 1f);
    public Vector2 minAndMaxAngle = new Vector2(-45f, 45f);

    private SplineContainer splineContainer;
    private List<Segment> segments = new List<Segment>();
    [Header("Width Control")]
    public bool useRandomCurve = true;
    public AnimationCurve userDefinedCurve;
    public AnimationCurve widthBiasCurve;

    void Start()
    {
        GenerateLinesAndSplines();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            GenerateLinesAndSplines();
        }
    }

    void GenerateLinesAndSplines()
    {
        if (splineContainer == null)
            splineContainer = GetComponent<SplineContainer>() ?? gameObject.AddComponent<SplineContainer>();

        Spline spline = splineContainer.Spline;
        spline.Clear();
        segments.Clear();

        // Randomize width bias curve
        widthBiasCurve = GenerateRandomWidthCurve();

        


        for (int i = 0; i < pointCount; i++)
        {
            Vector3 pos = new Vector3(0, i * heightSpacing, 0);
            spline.Add(new BezierKnot(pos));

            if (i == pointCount - 1)
                continue; // tip

            float heightRatio = i / (float)(pointCount - 1);
            float bias = widthBiasCurve.Evaluate(heightRatio);
            if (!useRandomCurve)
            {
                bias = userDefinedCurve.Evaluate(heightRatio);
            }
            float width = Mathf.Lerp(minAndMaxWidth.x, minAndMaxWidth.y, bias);

            float angle = (i == 0) ? 0f : Random.Range(minAndMaxAngle.x, minAndMaxAngle.y);
            Vector3 dir = Quaternion.Euler(0, 0, angle) * Vector3.right;

            Vector3 left = pos - dir * width * 0.5f;
            Vector3 right = pos + dir * width * 0.5f;

            segments.Add(new Segment { center = pos, left = left, right = right });
        }

        spline.SetTangentMode(TangentMode.AutoSmooth);
    }
    AnimationCurve GenerateRandomWidthCurve()
    {
        return new AnimationCurve(
            new Keyframe(0f, Random.Range(.5f, 1f)),
            new Keyframe(0.5f, Random.Range(0.3f, 0.9f)),
            new Keyframe(1f, Random.Range(0.1f, 0.75f))
        );
    }


    void OnDrawGizmos()
    {
        if (segments == null || segments.Count == 0)
            return;

        Gizmos.color = Color.cyan;
        foreach (var seg in segments)
        {
            Gizmos.DrawLine(seg.left, seg.right);
            Gizmos.DrawSphere(seg.center, 0.05f);
        }

        if (splineContainer != null && splineContainer.Spline.Count > 0)
        {
            Vector3 tip = splineContainer.Spline[splineContainer.Spline.Count - 1].Position;
            Gizmos.color = Color.magenta;
            Gizmos.DrawSphere(tip, 0.07f);
        }
    }
}