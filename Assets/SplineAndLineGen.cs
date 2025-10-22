using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.GlobalIllumination;
using UnityEngine.Splines;


public struct Segment //Points for creating the mesh
{
    public Vector3 center;
    public Vector3 left;
    public Vector3 right;
}

public enum CurvatureMode
{
    None,               // No curvature applied
    UserDefinedCurve,   // Uses curvatureShape
    RandomCurve,         // Uses activeCurvatureCurve
    SickleCurve //Used for extremely curved blades like a shotel
}
public enum TipLeanMode
{
    Centered,       // Tip is centered (X = 0)
    RandomLean,     // Tip leans left or right randomly
    ForcedCenterX,  // Tip X is forced to 0 regardless of curvature
    None            // No special tip logic
}

public enum EdgeCollapseMode //for edge collapsing and unique blade shapes
{
    None,
    Random,
    LeftOnly,
    RightOnly,
    Alternating, // Alternates left/right collapse per segment
    LooseAlternating,
    Patterned,
    RandomPatterned
}
public enum HeightSpacingMode { 
    Fixed, 
    RandomUniform, 
    RandomChaotic 
}

public enum BladePresets
{
    Katana,
    Shotel,
    Scimatar,
    Needle,
    Gladius,
    LongSword,
    Jian
}

[System.Serializable]
public class TipSettings
{
    [Tooltip("Controls if and how blade tip leans. Centered (keeps it straight), RandomLean (tilts left or right), ForcedCenterX (locks X to 0), None (disables tip logic, Creating a flat tip)")]

    public TipLeanMode tipLeanMode = TipLeanMode.Centered;

    [Tooltip("Defines the random minimum and maximum the tip can be offset height wise")]
    [Range(0, 1)]
    public float randomHeightOffset = 0.1f;
    [Tooltip("Defines the offset for the tip of the blade (Allows for shorter or longer tips)")]
    [Range(-1, 1)]
    public float heightOffset = 0f;

    [Tooltip("Curve allows user to control the tip leaning strength")]
    [Range(0, 1)]
    public AnimationCurve tipLeanStrengthCurve = AnimationCurve.EaseInOut(0, 0, 1, 1); // To help force the tip to lean fully or partially left or right based on prefrences (for something like a katana witha tip to one side)
}

[System.Serializable]
public class CoreSettings
{
    [Header("Core Controls")]
    [Tooltip("Defines the amount of segments wanted (More allows for more randomness and detail but also more chaos)")]
    [Range(3, 10)]
    public int splinePointCount = 5;
    [Tooltip("Defines spcaing between blade segments")]
    [Range(0.25f, 2f)]
    public float heightSpacing = 0.5f; // manual height spacing for specific sized blades

    [Tooltip("Fixed(User Defined), Random Uniformerd(Random but consistent through out segments), Rand Chaotic (Random and different between segments)")]

    public HeightSpacingMode heightSpacingMode = HeightSpacingMode.Fixed;

    [Tooltip("Defines the minimum and maximum spacing between segments (Used for randomness)")]
    public Vector2 minAndMaxHeightSpacing = new Vector2(0.25f, 1f); 
    [Tooltip("Defines the minimum and maximum width of the blade in each segment (used for randomness)")]
    public Vector2 minAndMaxWidth = new Vector2(0.2f, 1f);
    [Tooltip("Defines the minimum and maximum angle for a segment (Curvature of the blades edge)")]
    public Vector2 minAndMaxAngle = new Vector2(-45f, 45f); 
}

[System.Serializable]
public class WidthSettings
{
    [Tooltip("Allows choice for defining your own width curve or a random one")]
    public bool useRandomWidthCurve = true; 
    [Tooltip("Allows creating of own width curve")]
    public AnimationCurve userDefinedCurve;
    [Tooltip("Allows the user to see the random width curve")]
    public AnimationCurve widthBiasCurve; 
}

[System.Serializable]
public class CurvatureSettings
{
    public CurvatureMode curvatureMode = CurvatureMode.None;
    [Range(0, 2)]
    public float curvature_Max; // max curvature the blade can have
    [Range(0, 1)]
    public float curvature_PeakFactor = 0.3f; // controls the smoothness of the blades edges --- 0.3-0.6 for best smoothness 
    [Range(0, 1)]
    public float curvature_StepSize = 0.3f; // allows to control how smoot or chaotic the curvature is
    public AnimationCurve curvatureShape = AnimationCurve.Linear(0, 0, 1, 1); // allows for customisation of blades curve, if randomness isnt wanted

    public Vector3 curvatureDirection = new Vector3(1, 0, 0); // direction of the curve made to randomly go left or right
}
[System.Serializable]
public class EdgeSettings
{
    public EdgeCollapseMode edgeCollapseMode = EdgeCollapseMode.None;
    [Tooltip("Defines edge collapse pattern across blade thirds. Use 'L', 'R', or 'N' for None.")]
    public string collapsePattern = "LRL"; // Example: Left, Right, Left
}


public class SplineAndLineGen : MonoBehaviour
{


    [Header("Symmetry")]
    public bool useSymmetry; // Stops the use of angles and make sure a straight blade is symmetri

    [Header("Core Controls")]
    public CoreSettings coreSettings = new CoreSettings();
    [Header("Width Control")]
    public WidthSettings widthSettings = new WidthSettings();
    [Header("Tip Control")]
    public TipSettings tipSettings = new TipSettings();
    [Header("Curvature Control")]
    public CurvatureSettings curvatureSettings = new CurvatureSettings();
    [Header("Edge Controls")]
    public EdgeSettings edgeSettings = new EdgeSettings();
    private SplineContainer splineContainer; // spline creation script
    public List<Segment> segments = new List<Segment>(); // sword segments

    

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
        if (splineContainer == null)
            splineContainer = GetComponent<SplineContainer>() ?? gameObject.AddComponent<SplineContainer>();

        Spline spline = splineContainer.Spline;
        spline.Clear();
        segments.Clear();

        widthSettings.widthBiasCurve = GenerateRandomWidthCurve();

        if (curvatureSettings.curvatureMode == CurvatureMode.RandomCurve)
        {
            activeCurvatureCurve = GenerateCurvatureCurve();
        }

        Vector3 pos = Vector3.zero;
        float totalHeight = 0f;
        float uniformStepSize = Random.Range(coreSettings.minAndMaxHeightSpacing.x, coreSettings.minAndMaxHeightSpacing.y);
        bool alternatingStartsLeft = Random.value < 0.5f;
        Vector3 previousCenter = Vector3.zero;

        if (edgeSettings.edgeCollapseMode == EdgeCollapseMode.RandomPatterned)
        {
            edgeSettings.collapsePattern = GenerateRandomCollapsePattern(coreSettings.splinePointCount); // or any length you want
        }


        for (int i = 0; i < coreSettings.splinePointCount; i++)
        {
            float stepSize = coreSettings.heightSpacing;

            if (i == 0)
            {
                stepSize = 0f;
            }
            else
            {
                switch (coreSettings.heightSpacingMode)
                {
                    case HeightSpacingMode.Fixed:
                        stepSize = coreSettings.heightSpacing;
                        break;
                    case HeightSpacingMode.RandomUniform:
                        stepSize = uniformStepSize;
                        break;
                    case HeightSpacingMode.RandomChaotic:
                        stepSize = Random.Range(coreSettings.minAndMaxHeightSpacing.x, coreSettings.minAndMaxHeightSpacing.y);
                        break;
                }
            }

            if (i == coreSettings.splinePointCount - 1)
            {
                stepSize += tipSettings.heightOffset;
            }

            pos += new Vector3(0, stepSize, 0);
            totalHeight += stepSize;

            float heightRatio = totalHeight / (coreSettings.heightSpacing * (coreSettings.splinePointCount - 1));

            if (curvatureSettings.curvatureMode != CurvatureMode.None)
            {
                pos = GenerateCurvature(pos, heightRatio, i);
            }


            float bias = widthSettings.useRandomWidthCurve
                ? widthSettings.widthBiasCurve.Evaluate(heightRatio)
                : widthSettings.userDefinedCurve.Evaluate(heightRatio);

            float width = Mathf.Lerp(coreSettings.minAndMaxWidth.x, coreSettings.minAndMaxWidth.y, bias);

            float raw = Random.value;
            float biased = Mathf.Pow(raw, 2f);
            float mid = 0f;
            float range = useSymmetry ? 0f : Mathf.Max(Mathf.Abs(coreSettings.minAndMaxAngle.x), Mathf.Abs(coreSettings.minAndMaxAngle.y));

            float angle = i == 0 ? 0f : Mathf.Lerp(mid - range, mid + range, biased);

            Vector3 dir = Quaternion.Euler(0, 0, angle) * Vector3.right;
            Vector3 left = pos - dir * width * 0.5f;
            Vector3 right = pos + dir * width * 0.5f;

            bool collapseLeftSide = false;
            bool collapseRightSide = false;
            bool isEven = (i % 2 == 0);

            switch (edgeSettings.edgeCollapseMode)
            {
                case EdgeCollapseMode.None:
                    break;

                case EdgeCollapseMode.LeftOnly:
                    collapseLeftSide = true;
                    break;

                case EdgeCollapseMode.RightOnly:
                    collapseRightSide = true;
                    break;

                case EdgeCollapseMode.Random:
                    float collapseChance = Random.value;
                    collapseLeftSide = collapseChance < 0.5f;
                    collapseRightSide = !collapseLeftSide;
                    break;

                case EdgeCollapseMode.Alternating:
                    collapseLeftSide = (alternatingStartsLeft == isEven);
                    collapseRightSide = !collapseLeftSide;
                    break;

                case EdgeCollapseMode.LooseAlternating:
                    {
                        // Random chance to skip collapse entirely
                        bool skipCollapse = Random.value < 0.3f; // ~30% chance to skip
                        if (!skipCollapse)
                        {
                            bool startsLeft = alternatingStartsLeft; // defined before loop
                            collapseLeftSide = (startsLeft == isEven);
                            collapseRightSide = !collapseLeftSide;
                        }
                    }
                    break;

                case EdgeCollapseMode.Patterned:
                case EdgeCollapseMode.RandomPatterned:
                    {
                        int segmentGroup = Mathf.FloorToInt((float)i / coreSettings.splinePointCount * edgeSettings.collapsePattern.Length);
                        segmentGroup = Mathf.Clamp(segmentGroup, 0, edgeSettings.collapsePattern.Length - 1);

                        char patternChar = edgeSettings.collapsePattern[segmentGroup];

                        switch (patternChar)
                        {
                            case 'L':
                                collapseLeftSide = true;
                                break;
                            case 'R':
                                collapseRightSide = true;
                                break;
                            case 'B':
                                collapseLeftSide = true;
                                collapseRightSide = true;
                                break;
                            case 'N':
                            default:
                                break;
                        }
                    }
                    break;



            }


            if (i == coreSettings.splinePointCount - 1)
            {
                switch (tipSettings.tipLeanMode)
                {
                    case TipLeanMode.Centered:
                        left = pos;
                        right = pos;
                        break;

                    case TipLeanMode.ForcedCenterX:
                        pos.x = 0f;
                        left = pos;
                        right = pos;
                        break;

                    case TipLeanMode.RandomLean:
                        float tipBiasChance = Random.value;
                        float leanStrength = tipSettings.tipLeanStrengthCurve.Evaluate(Random.value);

                        if (tipBiasChance < 0.5f)
                        {
                            Debug.Log("TIP LEANING LEFT");
                            pos = Vector3.Lerp(pos, left, leanStrength);
                        }
                        else
                        {
                            Debug.Log("TIP LEANING RIGHT");
                            pos = Vector3.Lerp(pos, right, leanStrength);
                        }

                        left = pos;
                        right = pos;
                        break;


                    case TipLeanMode.None:
                    default:
                        break;

                }
            }

            Vector3 center = pos;


            if (collapseLeftSide && collapseRightSide)
            {
                // Create a thin visible segment perpendicular to the direction
                Vector3 direction = (center - previousCenter).normalized;
                Vector3 perpendicular = Vector3.Cross(direction, Vector3.forward); // Z-forward for 2D

                float collapseWidth = Mathf.Lerp(coreSettings.minAndMaxWidth.x, coreSettings.minAndMaxWidth.y, bias) * 0.5f;

                left = center - perpendicular * collapseWidth;
                right = center + perpendicular * collapseWidth;
            }

            else
            {
                if (collapseLeftSide) left = center;
                if (collapseRightSide) right = center;
            }
            previousCenter = center;

            spline.Add(new BezierKnot(pos));

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
            curvatureSettings.curvatureDirection.x = -1f;
        }
        else
        {
            curvatureSettings.curvatureDirection.x = 1f;
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
            float targetValue = Mathf.Sin(t * Mathf.PI) * curvatureSettings.curvature_Max * curvatureSettings.curvature_PeakFactor;  //Ideal value based on a peak in the middle and tapper at the end of the blade
            //adds subtle variation to keep the blade organic
            float variation = Random.Range(-curvatureSettings.curvature_StepSize, curvatureSettings.curvature_StepSize);
            values[i] = Mathf.Clamp(targetValue + variation, -curvatureSettings.curvature_Max, curvatureSettings.curvature_Max);
        }

        //Simple version
        //for (int i = 1; i < keyCount - 1; i++)
        //{
        //    float prev = values[i - 1];
        //    float next = Random.Range(prev - curvatureStepSize, prev + curvatureStepSize); // small change
        //    values[i] = Mathf.Clamp(next, -curvatureMax, curvatureMax);
        //}

        // Makes sure the tip is centered
        if (tipSettings.tipLeanMode == TipLeanMode.ForcedCenterX)
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

    Vector3 GenerateCurvature(Vector3 pos, float heightRatio, int segmentIndex)
    {
        if (curvatureSettings.curvatureMode == CurvatureMode.None || segmentIndex <= 1)
            return pos;

        float curveStrength = 0f;

        switch (curvatureSettings.curvatureMode)
        {
            case CurvatureMode.UserDefinedCurve:
                curveStrength = curvatureSettings.curvatureShape.Evaluate(heightRatio);
                break;

            case CurvatureMode.RandomCurve:
                curveStrength = activeCurvatureCurve.Evaluate(heightRatio);
                break;

            case CurvatureMode.SickleCurve:
                float arc = Mathf.Sin(heightRatio * Mathf.PI);
                curveStrength = arc;
                break;
        }

        pos.x = curveStrength * curvatureSettings.curvature_Max * Mathf.Sign(curvatureSettings.curvatureDirection.x);
        return pos;
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


    public string GenerateRandomCollapsePattern(int length)
    {
        char[] options = { 'L', 'R', 'B', 'N' }; // Left, Right, Both, None
        char[] baseOptions = { 'L', 'R', 'N' }; // excludes b from the choice (Both) as this creates a  bad start for the blade
        System.Text.StringBuilder pattern = new System.Text.StringBuilder();

        for (int i = 0; i < length; i++)
        {
            if(i == length - 1)
            {
                pattern.Append('N');
            }
            else if (i == 0)
            {
                // First character: exclude 'B'
                char firstChoice = baseOptions[Random.Range(0, baseOptions.Length)];
                pattern.Append(firstChoice);
            }
            else
            {
                char choice = options[Random.Range(0, options.Length)];
                pattern.Append(choice);
            }
        }

        return pattern.ToString();
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