using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Experimental.GlobalIllumination;
using UnityEngine.Splines;

#region Variables and Enmums
[System.Serializable]
public class BladePreset
{
    public string presetName;
    public CoreSettings coreSettings;
    public WidthSettings widthSettings;
    public CurvatureSettings curvatureSettings;
    public TipSettings tipSettings;
    public EdgeSettings edgeSettings;
    public bool useSymmetry;
}
[System.Serializable]
public class BladePresetCollection
{
    public List<BladePreset> presets = new List<BladePreset>();
}


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
    ForcedLeft,     // Tip leans explicitly to the left
    ForcedRight,    // Tip leans explicitly to the right
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
    RandomChaotic,
    SetHeight
}

public enum BladePresets
{
    None,
    Katana,
    Shotel,
    Scimatar,
    Needle,
    Gladius,
    LongSword,
    Jian,
    Custom
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
    [Range(-2, 1)]
    public float heightOffset = 0f;

    [Tooltip("Curve allows user to control the tip leaning strength")]
    [Range(0, 1)]
    public AnimationCurve tipLeanStrengthCurve = AnimationCurve.EaseInOut(0, 0, 1, 1); // To help force the tip to lean fully or partially left or right based on prefrences (for something like a katana witha tip to one side)
    public void CopyFrom(TipSettings other)
    {
        tipLeanMode = other.tipLeanMode;
        randomHeightOffset = other.randomHeightOffset;
        heightOffset = other.heightOffset;

        tipLeanStrengthCurve = other.tipLeanStrengthCurve != null
            ? new AnimationCurve(other.tipLeanStrengthCurve.keys)
            : new AnimationCurve();
    }

}

[System.Serializable]
public class CoreSettings
{
    [Header("Core Controls")]
    [Tooltip("Defines the amount of segments wanted (More allows for more randomness and detail but also more chaos)")]
    [Range(3, 20)]
    public int splinePointCount = 5;
    [Tooltip("Defines spcaing between blade segments")]
    [Range(0.25f, 2f)]
    public float heightSpacing = 0.5f; // manual height spacing for specific sized blades

    [Tooltip("Fixed(User Defined), Random Uniformerd(Random but consistent through out segments), Rand Chaotic (Random and different between segments)")]

    public HeightSpacingMode heightSpacingMode = HeightSpacingMode.Fixed;
    public float totalBladeHeight = 3;

    [Tooltip("Defines the minimum and maximum spacing between segments (Used for randomness)")]
    public Vector2 minAndMaxHeightSpacing = new Vector2(0.25f, 1f); 
    [Tooltip("Defines the minimum and maximum width of the blade in each segment (used for randomness)")]
    public Vector2 minAndMaxWidth = new Vector2(0.2f, 1f);
    [Tooltip("Defines the minimum and maximum angle for a segment (Curvature of the blades edge)")]
    public Vector2 minAndMaxAngle = new Vector2(-45f, 45f);

    public void CopyFrom(CoreSettings other)
    {
        splinePointCount = other.splinePointCount;
        heightSpacing = other.heightSpacing;
        heightSpacingMode = other.heightSpacingMode;
        totalBladeHeight = other.totalBladeHeight;

        minAndMaxHeightSpacing = other.minAndMaxHeightSpacing;
        minAndMaxWidth = other.minAndMaxWidth;
        minAndMaxAngle = other.minAndMaxAngle;
    }

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
    public float noiseInfluence = 1;
    public float noiseFrequency = 0.123f; //Affects the noise
    public void CopyFrom(WidthSettings other)
    {
        useRandomWidthCurve = other.useRandomWidthCurve;

        userDefinedCurve = other.userDefinedCurve != null
            ? new AnimationCurve(other.userDefinedCurve.keys)
            : new AnimationCurve();

        widthBiasCurve = other.widthBiasCurve != null
            ? new AnimationCurve(other.widthBiasCurve.keys)
            : new AnimationCurve();

        noiseInfluence = other.noiseInfluence;
        noiseFrequency = other.noiseFrequency;
    }

}

[System.Serializable]
public class CurvatureSettings
{
    [Range(0, 5)]
    public int straightSegmentThreshold = 0;
    public CurvatureMode curvatureMode = CurvatureMode.None;
    [Range(0, 2)]
    public float curvature_Max; // max curvature the blade can have
    [Range(0, 1)]
    public float curvature_PeakFactor = 0.3f; // controls the smoothness of the blades edges --- 0.3-0.6 for best smoothness 
    [Range(0, .2f)]
    public float curvature_StepSize = 0.3f; // allows to control how smoot or chaotic the curvature is
    public AnimationCurve curvatureShape = AnimationCurve.Linear(0, 0, 1, 1); // allows for customisation of blades curve, if randomness isnt wanted

    public Vector3 curvatureDirection = new Vector3(1, 0, 0); // direction of the curve made to randomly go left or right
    public void CopyFrom(CurvatureSettings other)
    {
        straightSegmentThreshold = other.straightSegmentThreshold;
        curvatureMode = other.curvatureMode;

        curvature_Max = other.curvature_Max;
        curvature_PeakFactor = other.curvature_PeakFactor;
        curvature_StepSize = other.curvature_StepSize;

        curvatureShape = other.curvatureShape != null
            ? new AnimationCurve(other.curvatureShape.keys)
            : new AnimationCurve();

        curvatureDirection = other.curvatureDirection;
    }

}
[System.Serializable]
public class EdgeSettings
{
    public EdgeCollapseMode edgeCollapseMode = EdgeCollapseMode.None;
    [Tooltip("Defines edge collapse pattern across blade thirds. Use 'L', 'R', or 'N' for None.")]
    public string collapsePattern = "LRL"; // Example: Left, Right, Left

    public void CopyFrom(EdgeSettings other)
    {
        edgeCollapseMode = other.edgeCollapseMode;
        collapsePattern = other.collapsePattern;
    }



}
#endregion

public class SplineAndLineGen : MonoBehaviour
{
    
    [Header("Blade Preset")]
    public string presetName;
    public BladePresets bladePreset = BladePresets.None;

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

    [Header("Presets")]
    public BladePresetCollection presetCollection;
    public List<BladePreset> presets;



    void Start()
    {
        LoadPreset();

        GenerateLinesAndSplines();
        bladePreset = BladePresets.None;
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            if(bladePreset == BladePresets.None)
            {
                GenerateLinesAndSplines();

            }
            else
            {
                LoadPreset();
                bladePreset = BladePresets.None;
            }
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
            activeCurvatureCurve = GenerateCurvatureCurve();

        Vector3 pos = Vector3.zero;
        float totalHeight = 0f;
        float uniformStepSize = Random.Range(coreSettings.minAndMaxHeightSpacing.x, coreSettings.minAndMaxHeightSpacing.y);
        bool alternatingStartsLeft = Random.value < 0.5f;
        Vector3 previousCenter = Vector3.zero;
        float seedOffset = Random.Range(0f, 1000f);

        if (edgeSettings.edgeCollapseMode == EdgeCollapseMode.RandomPatterned)
            edgeSettings.collapsePattern = GenerateRandomCollapsePattern(coreSettings.splinePointCount);

        for (int i = 0; i < coreSettings.splinePointCount; i++)
        {
            float stepSize = i == 0 ? 0f : coreSettings.heightSpacing;

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
                case HeightSpacingMode.SetHeight:
                    stepSize = coreSettings.totalBladeHeight / Mathf.Max(coreSettings.splinePointCount - 1, 1);
                    break;
            }
            if (i == 0)
                stepSize = 0f;

            pos += new Vector3(0, stepSize, 0);
            totalHeight += stepSize;


            float heightRatio = (coreSettings.heightSpacingMode == HeightSpacingMode.SetHeight)
                ? totalHeight / Mathf.Max(coreSettings.totalBladeHeight, 0.0001f)
                : totalHeight / Mathf.Max(coreSettings.heightSpacing * (coreSettings.splinePointCount - 1), 0.0001f);

            if (curvatureSettings.curvatureMode != CurvatureMode.None && i >= curvatureSettings.straightSegmentThreshold)
            {
                int curvedStart = curvatureSettings.straightSegmentThreshold;
                int curvedCount = Mathf.Max(coreSettings.splinePointCount - curvedStart - 1, 1);
                float curvedRatio = (i - curvedStart) / (float)curvedCount;
                pos = GenerateCurvature(pos, curvedRatio, i);
            }

            if (i == coreSettings.splinePointCount - 1 && segments.Count >= 5)
            {
                Vector3 averageDirection = Vector3.zero;
                for (int j = segments.Count - 5; j < segments.Count - 1; j++)
                {
                    Vector3 direction = (segments[j + 1].center - segments[j].center).normalized;
                    averageDirection += direction;
                }
                averageDirection.Normalize();
                pos += averageDirection * tipSettings.heightOffset;
            }

            float bias = widthSettings.useRandomWidthCurve
                ? widthSettings.widthBiasCurve.Evaluate(heightRatio)
                : widthSettings.userDefinedCurve.Evaluate(heightRatio);

            //Width bades on noise
            float noise = Mathf.PerlinNoise(i * widthSettings.noiseFrequency + seedOffset, 0f);
            float combinedBias = Mathf.Lerp(bias, noise, widthSettings.noiseInfluence);
            if (widthSettings.useRandomWidthCurve == false) {
                combinedBias = bias;
            }

            float width = Mathf.Lerp(coreSettings.minAndMaxWidth.x, coreSettings.minAndMaxWidth.y, combinedBias);

            float raw = Random.value;
            float biased = Mathf.Pow(raw, 2f);
            float mid = 0f;
            float range = useSymmetry ? 0f : Mathf.Max(Mathf.Abs(coreSettings.minAndMaxAngle.x), Mathf.Abs(coreSettings.minAndMaxAngle.y));
            float angle = i == 0 ? 0f : Mathf.Lerp(mid - range, mid + range, biased);

            Vector3 dir = Quaternion.Euler(0, 0, angle) * Vector3.right;
            Vector3 left = pos - dir * width * 0.5f;
            Vector3 right = pos + dir * width * 0.5f;

            ApplyTipLean(i, ref pos, ref left, ref right);

            bool collapseLeftSide = false;
            bool collapseRightSide = false;
            bool isEven = (i % 2 == 0);

            switch (edgeSettings.edgeCollapseMode)
            {
                case EdgeCollapseMode.LeftOnly:
                    collapseLeftSide = true;
                    break;
                case EdgeCollapseMode.RightOnly:
                    collapseRightSide = true;
                    break;
                case EdgeCollapseMode.Random:
                    collapseLeftSide = Random.value < 0.5f;
                    collapseRightSide = !collapseLeftSide;
                    break;
                case EdgeCollapseMode.Alternating:
                    collapseLeftSide = (alternatingStartsLeft == isEven);
                    collapseRightSide = !collapseLeftSide;
                    break;
                case EdgeCollapseMode.LooseAlternating:
                    if (Random.value >= 0.3f)
                    {
                        collapseLeftSide = (alternatingStartsLeft == isEven);
                        collapseRightSide = !collapseLeftSide;
                    }
                    break;
                case EdgeCollapseMode.Patterned:
                case EdgeCollapseMode.RandomPatterned:
                    int segmentGroup = Mathf.FloorToInt((float)i / coreSettings.splinePointCount * edgeSettings.collapsePattern.Length);
                    segmentGroup = Mathf.Clamp(segmentGroup, 0, edgeSettings.collapsePattern.Length - 1);
                    char patternChar = edgeSettings.collapsePattern[segmentGroup];
                    switch (patternChar)
                    {
                        case 'L': collapseLeftSide = true; break;
                        case 'R': collapseRightSide = true; break;
                        case 'B': collapseLeftSide = true; collapseRightSide = true; break;
                    }
                    break;
            }

            Vector3 center = pos;

            if (collapseLeftSide && collapseRightSide)
            {
                Vector3 direction = (center - previousCenter).normalized;
                Vector3 perpendicular = Vector3.Cross(direction, Vector3.forward);
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
            spline.Add(new BezierKnot(center));
            segments.Add(new Segment { center = center, left = left, right = right });
        }

        spline.SetTangentMode(TangentMode.AutoSmooth);
    }
   
    void ApplyTipLean(int i, ref Vector3 pos, ref Vector3 left, ref Vector3 right)
    {
        if (i != coreSettings.splinePointCount - 1) return;

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

            case TipLeanMode.ForcedLeft:
            case TipLeanMode.ForcedRight:
            case TipLeanMode.RandomLean:
                float leanStrength = tipSettings.tipLeanStrengthCurve.Evaluate(Random.value);
                Vector3 leanTarget = tipSettings.tipLeanMode switch
                {
                    TipLeanMode.ForcedLeft => left,
                    TipLeanMode.ForcedRight => right,
                    TipLeanMode.RandomLean => Random.value < 0.5f ? left : right,
                    _ => pos
                };
                Vector3 leanedPos = Vector3.Lerp(pos, leanTarget, leanStrength);
                pos = leanedPos;
                left = leanedPos;
                right = leanedPos;
                break;

            case TipLeanMode.None:
            default:
                break;
        }
    }

    AnimationCurve GenerateRandomWidthCurve()
    {
        return new AnimationCurve(
            new Keyframe(0f, Random.Range(.5f, 1f)),
            new Keyframe(0.5f, Random.Range(0.3f, 0.9f)),
            new Keyframe(1f, Random.Range(0.1f, 0.75f))
        );
    }
    public void OnBladePresetChanged(BladePresets presetEnum)
    {
        bladePreset = presetEnum;   // update enum
        LoadPreset();               // load JSON preset
    }


    AnimationCurve GenerateCurvatureCurve()
    {
        // Decide curve direction
        curvatureSettings.curvatureDirection.x = -1;// (Random.value < 0.5f) ? -1f : 1f;

        int keyCount = 12; // more keys = smoother curve
        AnimationCurve curve = new AnimationCurve();

        for (int i = 0; i < keyCount; i++)
        {
            float t = i / (float)(keyCount - 1);

            // Base bell-shaped curve
            float baseValue = Mathf.Sin(t * Mathf.PI)
                              * curvatureSettings.curvature_Max
                              * curvatureSettings.curvature_PeakFactor;

            // Smooth noise (Perlin)
            float noise = Mathf.PerlinNoise(t * 3f, Random.value * 10f) - 0.5f;
            noise *= curvatureSettings.curvature_StepSize;

            float finalValue = Mathf.Clamp(baseValue + noise,
                                           -curvatureSettings.curvature_Max,
                                           curvatureSettings.curvature_Max);

            // Forced center tip
            if (i == keyCount - 1 && tipSettings.tipLeanMode == TipLeanMode.ForcedCenterX)
                finalValue = 0f;

            curve.AddKey(t, finalValue);
        }

        // Smooth tangents
        for (int i = 0; i < curve.keys.Length; i++)
        {
            curve.SmoothTangents(i, 0.5f);
        }

        return curve;
    }
    Vector3 GenerateCurvature(Vector3 pos, float heightRatio, int segmentIndex)
    {
        if (curvatureSettings.curvatureMode == CurvatureMode.None)//|| segmentIndex <= curvatureSettings.straightSegmentThreshold
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


        Vector3 offset = transform.position;
        Gizmos.color = Color.cyan;
        foreach (var seg in segments)
        {
            Gizmos.DrawLine(seg.left + offset, seg.right + offset);
            Gizmos.DrawSphere(seg.center + offset, 0.05f);
        }

        if (splineContainer != null && splineContainer.Spline.Count > 0)
        {
            Vector3 tip = splineContainer.Spline[splineContainer.Spline.Count - 1].Position;
            tip += offset;
            Gizmos.color = Color.magenta;
            Gizmos.DrawSphere(tip, 0.07f);
        }
    }

    public void SaveCurrentPreset(string presetName)
    {
        BladePreset preset = new BladePreset
        {
            presetName = presetName,
            coreSettings = this.coreSettings,
            widthSettings = this.widthSettings,
            curvatureSettings = this.curvatureSettings,
            tipSettings = this.tipSettings,
            edgeSettings = this.edgeSettings,
            useSymmetry = this.useSymmetry
        };

        string folderPath = Path.Combine(Application.dataPath, "Presets");
        Directory.CreateDirectory(folderPath); // ensures folder exists

        string filePath = Path.Combine(folderPath, presetName + ".json");
        string json = JsonUtility.ToJson(preset, true);
        File.WriteAllText(filePath, json);

        Debug.Log("Preset saved to: " + filePath);
#if UNITY_EDITOR
        UnityEditor.AssetDatabase.Refresh();
#endif

    }

    [ContextMenu("Load Preset")]
    public void LoadPreset()
    {
        string chosenPreset = "";
        switch (bladePreset)
        {
            case BladePresets.None:
                Debug.LogWarning("No blade preset selected.");
                return;

            case BladePresets.Katana:
                chosenPreset = "Katana";
                break;
            case BladePresets.Shotel:
                chosenPreset = "Shotel";
                break;
            case BladePresets.Scimatar:
                chosenPreset = "Scimitar";
                break;
            case BladePresets.Needle:
                chosenPreset = "Needle";
                break;
            case BladePresets.Gladius:
                chosenPreset = "Gladius";
                break;
            case BladePresets.LongSword:
                chosenPreset = "LongSword";
                break;
            case BladePresets.Jian:
                chosenPreset = "Jian";
                break;
            case BladePresets.Custom:
                if (string.IsNullOrWhiteSpace(presetName))
                {
                    Debug.LogWarning("Custom preset name is empty.");
                    return;
                }
                chosenPreset = presetName;
                break;

            default:
                Debug.LogWarning("Unknown blade preset.");
                return;
        }

        string folderPath = Path.Combine(Application.dataPath, "Presets");
        string filePath = Path.Combine(folderPath, chosenPreset + ".json");

        if (!File.Exists(filePath))
        {
            Debug.LogWarning("Preset not found: " + filePath);
            return;
        }

        string json = File.ReadAllText(filePath);
        BladePreset loadedPreset = JsonUtility.FromJson<BladePreset>(json);

        ApplyPreset(loadedPreset);

#if UNITY_EDITOR
        UnityEditor.AssetDatabase.Refresh();
#endif

        Debug.Log("Loaded preset: " + presetName);
    }


    public void ApplyPreset(BladePreset preset)
    {
        presetName = preset.presetName;

        coreSettings.CopyFrom(preset.coreSettings);
        widthSettings.CopyFrom(preset.widthSettings);
        curvatureSettings.CopyFrom(preset.curvatureSettings);
        tipSettings.CopyFrom(preset.tipSettings);
        edgeSettings.CopyFrom(preset.edgeSettings);

        useSymmetry = preset.useSymmetry;

        GenerateLinesAndSplines();
    }


    [ContextMenu("Save Current Preset")]
    public void SavePreset()
    {
        SaveCurrentPreset(presetName);
    }

   
}