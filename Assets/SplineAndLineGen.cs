using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;


public struct Segment
{
    public Vector3 center;
    public Vector3 left;
    public Vector3 right;
}

public class SplineAndLineGen : MonoBehaviour
{
 
    [Range(3,10)]
    public int pointCount = 5;
    [Range(0.25f, 2f)]
    public float heightSpacing = 0.5f;
    public bool randomHeightSpacing = false;
    public bool multipleSpacingValues = false;
    public Vector2 minAndMaxHeightSpacing = new Vector2(0.25f, 1f);
    public Vector2 minAndMaxWidth = new Vector2(0.2f, 1f);
    public Vector2 minAndMaxAngle = new Vector2(-45f, 45f);

    private SplineContainer splineContainer;
    public List<Segment> segments = new List<Segment>();
    [Header("Width Control")]
    public bool useRandomWidthCurve = true;
    public AnimationCurve userDefinedCurve;
    public AnimationCurve widthBiasCurve;

    [Header("Curvature Control")]
    public bool smoothCurvature;
    public bool applyCurvature;
    public bool useRandomCurvatureCurve = true;
    [Range(0, 1)]
    public float curvature_Max;
    [Range(0,1)]
    public float curvature_PeakFactor = 0.3f; //0.3-0.6 for best smoothness
    [Range(0, 1)]
    public float curvature_StepSize = 0.3f;
    public AnimationCurve curvatureShape = AnimationCurve.Linear(0, 0, 1, 1);

    public Vector3 curvatureDirection = new Vector3(1,0,0);

    [Header("Tip Control")]
    public bool centeredTip = false;
    public float tipEdgeCollapseChance = 0.2f;
    [Header("Testing")]
    public AnimationCurve activeCurvatureCurve;


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

    public void GenerateLinesAndSplines()
    {

        bool collapseLeftSide = false;
        bool collapseRightSide = false;

        float collapseChance = Random.value;
        if (collapseChance < 0.1f)
        {
            collapseLeftSide = true;
            Debug.Log("Left side Collapsed");
        }
        else if (collapseChance < 0.2f)
        {
            collapseRightSide = true;
            Debug.Log("Right side Collapsed");

        }


        if (splineContainer == null)
            splineContainer = GetComponent<SplineContainer>() ?? gameObject.AddComponent<SplineContainer>();

        Spline spline = splineContainer.Spline;
        spline.Clear();
        segments.Clear();

        // Randomize width bias curve
        widthBiasCurve = GenerateRandomWidthCurve();

        if (useRandomCurvatureCurve)
        {
            activeCurvatureCurve = GenerateCurvatureCurve();
        }

        Vector3 pos = Vector3.zero;
        float totalHeight = 0f;

        for (int i = 0; i < pointCount; i++)
        {
            float stepSize = heightSpacing;

            if (i == 0)
            {
                stepSize = 0f; // force first point to start at height 0
            }
            else if (randomHeightSpacing)
            {
                if (multipleSpacingValues)
                {
                    stepSize = Random.Range(minAndMaxHeightSpacing.x, minAndMaxHeightSpacing.y);
                }
                else if (i == 0)
                {
                    stepSize = Random.Range(minAndMaxHeightSpacing.x, minAndMaxHeightSpacing.y);
                }
            }

            // Accumulate vertical position
            pos += new Vector3(0, stepSize, 0);
            totalHeight += stepSize;

            float heightRatio = totalHeight / (heightSpacing * (pointCount - 1));

            if (applyCurvature)
            {
                pos = GenerateCurvature(pos, heightRatio);
            }

            if (i == pointCount - 1 && centeredTip)
            {
                pos.x = 0f; // only center the tip if centeredTip is true
            }


            spline.Add(new BezierKnot(pos));

            // Width bias
            float bias = widthBiasCurve.Evaluate(heightRatio);
            if (!useRandomWidthCurve)
            {
                bias = userDefinedCurve.Evaluate(heightRatio);
            }
            float width = Mathf.Lerp(minAndMaxWidth.x, minAndMaxWidth.y, bias);

            // Angle bias toward 0
            float raw = Random.value;
            float biased = Mathf.Pow(raw, 2f);
            float mid = 0f;
            float range = Mathf.Max(Mathf.Abs(minAndMaxAngle.x), Mathf.Abs(minAndMaxAngle.y));

            float angle = 0f;
            if (i != 0)
            {
                angle = Mathf.Lerp(mid - range, mid + range, biased);
            }

            Vector3 dir = Quaternion.Euler(0, 0, angle) * Vector3.right;
            Vector3 left = pos - dir * width * 0.5f;
            Vector3 right = pos + dir * width * 0.5f;
            if (collapseLeftSide)
            {
                left = pos;
            }
            if (collapseRightSide)
            {
                right = pos;
            }
          

            if (i == pointCount - 1)
            {
                left = pos;     // collapse width
                right = pos;
            }

           



            //if (i == pointCount - 2)
            //{
            //    // Pre-tip segment: collapse its right side to avoid drawing a line to the tip
            //    right = pos;
            //}


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

    AnimationCurve GenerateCurvatureCurve()
    {
        float randomValue = Random.value;

        //Decides if the curve is left or right
        if (randomValue < 0.5f) 
        {
            curvatureDirection.x = -1f;
        }
        else
        {
            curvatureDirection.x = 1f;
        }

        AnimationCurve curve = new AnimationCurve();

        int keyCount = 5; // 0, 0.25, 0.5, 0.75, 1
        float[] values = new float[keyCount];

        //Generate smooth-ish values
        values[0] = 0f;


        values[0] = 0f;

        for (int i = 1; i < keyCount - 1; i++)
        {
            float t = i / (float)(keyCount - 1);  //height
            // scales down the peak to make sure the blade isnt always at the max curvature should create an almost bell shaped curve/baseline
            float targetValue = Mathf.Sin(t * Mathf.PI) * curvature_Max * curvature_PeakFactor;  //Ideal value based on a peak in the middle and tapper at the end of the blade
            //adds subtle variation to keep the blade organic
            float variation = Random.Range(-curvature_StepSize, curvature_StepSize);
            values[i] = Mathf.Clamp(targetValue + variation, -curvature_Max, curvature_Max);
        }

        //Simple version
        //for (int i = 1; i < keyCount - 1; i++)
        //{
        //    float prev = values[i - 1];
        //    float next = Random.Range(prev - curvatureStepSize, prev + curvatureStepSize); // small change
        //    values[i] = Mathf.Clamp(next, -curvatureMax, curvatureMax);
        //}

        // Makes sure the tip is centered
        if (centeredTip)
        {
            values[keyCount - 1] = 0f;
        }

        //Keyframes
        for (int i = 0; i < keyCount; i++)
        {
            float time = i / (float)(keyCount - 1);
            curve.AddKey(new Keyframe(time, values[i]));
        }



        return curve;
    }

    Vector3 GenerateCurvature(Vector3 pos, float heightRatio)
    {
        //user created curve
        float curveStrength = curvatureShape.Evaluate(heightRatio);

        if (useRandomCurvatureCurve) { 
            //randomly generated curve
           curveStrength = activeCurvatureCurve.Evaluate(heightRatio);
        }

        return pos + curvatureDirection.normalized * curveStrength * curvature_Max;
    }

    //Gets the spline points for mesh generation
    public List<Vector3> GetSplinePoints()
    {
        List<Vector3> points = new List<Vector3>();
        foreach (var knot in splineContainer.Spline)
        {
            points.Add(knot.Position);
        }
        return points;
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