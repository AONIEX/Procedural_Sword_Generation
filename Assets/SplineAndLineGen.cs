using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.GlobalIllumination;
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
    public float heightSpacing = 0.5f; // manual height spacing for specific sized blades
    public bool randomHeightSpacing = false; // random height spacing choice for more different sized blaseds
    public bool multipleSpacingValues = false; // multiple space values used for chaotic blades, but allows for more unique ness
    public Vector2 minAndMaxHeightSpacing = new Vector2(0.25f, 1f); // min and max height spacing used for randomness
    public Vector2 minAndMaxWidth = new Vector2(0.2f, 1f); // minimum and maximum width of the blade used for randomness
    public Vector2 minAndMaxAngle = new Vector2(-45f, 45f); // minimum and max angle used for segments and blade shape randomness

    private SplineContainer splineContainer; // spline creation script
    public List<Segment> segments = new List<Segment>(); // sword segments
    [Header("Width Control")]
    public bool useRandomWidthCurve = true; // choice to use random width curve
    public AnimationCurve userDefinedCurve; // allows user to somewhat force the width based on a curve
    public AnimationCurve widthBiasCurve; // allows contorl of width based on curve

    [Header("Curvature Control")]
    public bool smoothCurvature; // curvature as a smaller chance of being zig zaggy or wobbly
    public bool applyCurvature; //want curvature or not
    public bool useRandomCurvatureCurve = true; //use of random curvature compared to own choise
    [Range(0, 0.75f)]
    public float curvature_Max; // max curvature the blade can have
    [Range(0,1)]
    public float curvature_PeakFactor = 0.3f; // controls the smoothness of the blades edges --- 0.3-0.6 for best smoothness 
    [Range(0, 1)]
    public float curvature_StepSize = 0.3f; // allows to control how smoot or chaotic the curvature is
    public AnimationCurve curvatureShape = AnimationCurve.Linear(0, 0, 1, 1); // allows for customisation of blades curve, if randomness isnt wanted

    public Vector3 curvatureDirection = new Vector3(1,0,0); // direction of the curve made to randomly go left or right

    [Header("Tip Control")]
    public bool centeredTip = false; // centered tip no chance of tip being to the left or right
    public bool tipXForcedZero = false; // tip is forced to 0 better for straght blades, or for forcing blades wanting to curve back towards the center
    public bool useRandomTipHeightOffset;
    [Range(0,1)]
    public float randomTipHeightOffsetMinMax = 0.1f;
    [Range(-1, 1)]
    public float tipSegmentHeightOffset = 0f; // Can be positive or negative

    [Range(0,1)]
    public float tipEdgeCollapseChance = 0.2f;// chance of having one side of the blade flat or following the spline for something like a machette with a flat bacl
    [Range(0, 1)]
    public float chanceForTipLeaningToLeftRight = 0.3f; //chance of the tip being crooked or focusing on the left or right side of the blade
    public AnimationCurve tipLeanStrengthCurve = AnimationCurve.EaseInOut(0, 0, 1, 1); // To help force the tip to lean fully or partially left or right based on prefrences (for something like a katana witha tip to one side)


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


            if (i == pointCount - 1)
            {
                if (useRandomTipHeightOffset)
                {
                    stepSize += Random.Range(-randomTipHeightOffsetMinMax, randomTipHeightOffsetMinMax); ;
                }
                else
                {
                    stepSize += tipSegmentHeightOffset;
                }
            }

            // Accumulate vertical position
            pos += new Vector3(0, stepSize, 0);
            totalHeight += stepSize;

            float heightRatio = totalHeight / (heightSpacing * (pointCount - 1));

            int centerIndex = Mathf.FloorToInt(pointCount / 2);

            if (applyCurvature)
            {
                if (tipXForcedZero && i >= centerIndex)
                {
                    // Curve toward center X as we approach the tip to allow for a more natural look for a centered tip
                    float towardCenterBias = Mathf.InverseLerp(centerIndex, pointCount - 1, i); // 0 to 1
                    float curveAmount = activeCurvatureCurve.Evaluate(heightRatio);
                    float xOffset = Mathf.Lerp(pos.x, 0f, curveAmount * towardCenterBias);
                    pos.x = xOffset;
                }
                else
                {
                    pos = GenerateCurvature(pos, heightRatio);
                }
            }


            if (i == pointCount - 1 && tipXForcedZero)
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
                if (!centeredTip)
                {
                    float tipBiasChance = Random.value;

                    if (tipBiasChance < chanceForTipLeaningToLeftRight / 2)
                    {
                        // Lean toward left
                        Debug.Log("TIP LEANING LEFT");

                        float leanStrength = tipLeanStrengthCurve.Evaluate(Random.value); 
                        pos = Vector3.Lerp(pos, left, leanStrength);

                    }
                    else if (tipBiasChance < chanceForTipLeaningToLeftRight)
                    {
                        // Lean toward right
                        Debug.Log("TIP LEANING RIGHT");
                        float leanStrength = tipLeanStrengthCurve.Evaluate(Random.value);
                        pos = Vector3.Lerp(pos, right, leanStrength);

                    }

                    left = pos;
                    right = pos;
                }
                else
                {
                    left = pos;
                    right = pos;
                }
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
        if (tipXForcedZero)
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