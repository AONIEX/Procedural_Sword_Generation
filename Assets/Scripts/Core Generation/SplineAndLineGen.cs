using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Experimental.GlobalIllumination;
using UnityEngine.Splines;
using static UnityEngine.CompositeCollider2D;

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

    public BladeGenerationData bladeGenerationData;
    public ShaderParamaters shaderParamaters;
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
    None,
    UserDefinedCurve,
    RandomCurve,
    RandomOutward,
    SickleCurve
}

public enum TipLeanMode
{
    Centered,
    RandomLean,
    ForcedCenterX,
    ForcedLeft,
    ForcedRight
    
}

public enum EdgeCollapseMode
{
    None,
    Random,
    LeftOnly,
    RightOnly,
    Alternating,
    LooseAlternating
}

public enum HeightSpacingMode
{
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
    [Tooltip("Controls if and how blade tip leans.")]
    [DisplayName("Tip Lean Mode", "Blade Tip", 0, "Mode")]
    public TipLeanMode tipLeanMode = TipLeanMode.Centered;

    [Tooltip("Defines the offset for the tip of the blade")]
    [Range(-2, 1), DisplayName("Height Offset", "Blade Tip", 2, "Offset")]
    public float heightOffset = 0f;

    [Tooltip("Curve allows user to control the tip leaning strength")]
    [DisplayName("Tip Lean Strength Curve", "Blade Tip", 3, "Curve")]
    public AnimationCurve tipLeanStrengthCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    public void CopyFrom(TipSettings other)
    {
        tipLeanMode = other.tipLeanMode;
        heightOffset = other.heightOffset;

        tipLeanStrengthCurve = other.tipLeanStrengthCurve != null
            ? new AnimationCurve(other.tipLeanStrengthCurve.keys)
            : new AnimationCurve();
    }
}

[System.Serializable]
public class CoreSettings
{
    [Tooltip("Defines the amount of segments wanted")]
    [Range(3, 12), DisplayName("Spline Point Count", "Blade Geometry", 0, "Segments")]
    public int splinePointCount = 5;

    [Tooltip("Defines spacing between blade segments")]
    [Range(0.25f, 2f), DisplayName("Height Spacing", "Blade Geometry", 1, "Spacing")]
    public float heightSpacing = 0.5f;

    [DisplayName("Height Spacing Mode", "Blade Geometry", 2, "Spacing")]
    public HeightSpacingMode heightSpacingMode = HeightSpacingMode.Fixed;

    [Range(0.5f, 10f), DisplayName("Total Blade Height", "Blade Geometry", 3, "Spacing")]
    public float totalBladeHeight = 3;

    [Vector2Range(0.1f, 1f)]
    [DisplayName("Height Spacing Range", "Blade Geometry", 4, "Spacing")]
    public Vector2 minAndMaxHeightSpacing = new Vector2(0.25f, 1f);

    [Vector2Range(0.2f, 1f)]
    [DisplayName("Blade Width Range", "Blade Geometry", 5, "Width")]
    public Vector2 minAndMaxWidth = new Vector2(0.2f, 1f);

    [Vector2Range(-45f, 45f)]
    [DisplayName("Segment Angle Range", "Curvature & Flow", 6, "Angles")]
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
    [DisplayName("Use Random Width Curve", "Blade Geometry", 0, "Width")]
    public bool useRandomWidthCurve = true;

    [DisplayName("User Defined Curve", "Blade Geometry", 1, "Width")]
    public AnimationCurve userDefinedCurve;

    [HideInUI]
    [DisplayName("Width Bias Curve", "Blade Geometry", 2, "Width")]
    public AnimationCurve randomWidthBiasCurve;

    [Range(0f, 1f), DisplayName("Noise Influence", "Blade Geometry", 3, "Noise")]
    public float noiseInfluence = 1;

    [Range(0.01f, 1f), DisplayName("Noise Frequency", "Blade Geometry", 4, "Noise")]
    public float noiseFrequency = 0.123f;

    public void CopyFrom(WidthSettings other)
    {
        useRandomWidthCurve = other.useRandomWidthCurve;

        userDefinedCurve = other.userDefinedCurve != null
            ? new AnimationCurve(other.userDefinedCurve.keys)
            : new AnimationCurve();

        randomWidthBiasCurve = other.randomWidthBiasCurve != null
            ? new AnimationCurve(other.randomWidthBiasCurve.keys)
            : new AnimationCurve();

        noiseInfluence = other.noiseInfluence;
        noiseFrequency = other.noiseFrequency;
    }
}

[System.Serializable]
public class CurvatureSettings
{
    [Range(0, 5), DisplayName("Straight Segment Threshold", "Curvature & Flow", 0, "Threshold")]
    public int straightSegmentThreshold = 0;

    [DisplayName("Curvature Mode", "Curvature & Flow", 1, "Mode")]
    public CurvatureMode curvatureMode = CurvatureMode.None;

    [Range(0, 2), DisplayName("Max Curvature", "Curvature & Flow", 2, "Curvature")]
    public float curvature_Max;

    [Range(0, 1), DisplayName("Peak Factor", "Curvature & Flow", 3, "Curvature")]
    public float curvature_PeakFactor = 0.3f;

    [Range(0, .2f), DisplayName("Step Size", "Curvature & Flow", 4, "Curvature")]
    public float curvature_StepSize = 0.3f;

    [DisplayName("Curvature Shape", "Curvature & Flow", 5, "Shape")]
    public AnimationCurve curvatureShape = AnimationCurve.Linear(0, 0, 1, 1);

    [DisplayName("Curvature Direction", "Curvature & Flow", 6, "Direction")]
    public Vector3 curvatureDirection = new Vector3(1, 0, 0);

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
    [DisplayName("Edge Collapse Mode", "Edge & Spine", 0, "Collapse")]
    public EdgeCollapseMode edgeCollapseMode = EdgeCollapseMode.None;

    [DisplayName("Collapse Pattern", "Edge & Spine", 1, "Collapse")]
    public string collapsePattern = "LRL";

    [Range(-1f, 1f), DisplayName("Spine Offset", "Edge & Spine", 2, "Spine")]
    public float spineOffset = 0f;

    public void CopyFrom(EdgeSettings other)
    {
        edgeCollapseMode = other.edgeCollapseMode;
        collapsePattern = other.collapsePattern;
        spineOffset = other.spineOffset;
    }
}
#endregion

public class SplineAndLineGen : MonoBehaviour
{
    [Header("Manual Edit Falloff")]
    [DisplayName("Curve Fall Off Radius", "Manual", 2, "Curvature")]
    [Range(1, 5)] public int editFalloffRadius = 2;
    [DisplayName("Curve Fall Off Curve", "Manual", 2, "Curvature")]
    public AnimationCurve editFalloffCurve = AnimationCurve.EaseInOut(0, 1, 1, 0);



    public BladeGeneration bladeGeneration;
    public SwordShaderControl swordShaderControl;

    [Header("Blade Preset")]
    public string presetName;

    [DisplayName("Sword Preset", "General", 2, "Presets")]
    public BladePresets bladePreset = BladePresets.None;

    [Header("Symmetry")]
    [DisplayName("Don't Use Angles", "Curvature & Flow", 11, "Angles")]
    public bool useSymmetry;

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

    private SplineContainer splineContainer;

    [HideInUI]
    public List<Segment> segments = new List<Segment>();

    [HideInUI]
    [Header("Testing")]
    public AnimationCurve activeCurvatureCurve;

    public UIControl uiControl;

    void Start()
    {
        uiControl = GameObject.FindAnyObjectByType<UIControl>();
        bladeGeneration = GetComponent<BladeGeneration>();
        swordShaderControl = GetComponent<SwordShaderControl>();
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
        //if (usingManualSpline) return;

        if (splineContainer == null)
            splineContainer = GetComponent<SplineContainer>() ?? gameObject.AddComponent<SplineContainer>();

        Spline spline = splineContainer.Spline;
        spline.Clear();
        segments.Clear();

        widthSettings.randomWidthBiasCurve = GenerateRandomWidthCurve();

        if (curvatureSettings.curvatureMode == CurvatureMode.RandomCurve)
        {
            activeCurvatureCurve = GenerateCurvatureCurve();
        } 
        
        if(curvatureSettings.curvatureMode == CurvatureMode.RandomOutward) {
            activeCurvatureCurve =  GenerateCurvature_OutwardCurve();
        }

            Vector3 pos = Vector3.zero;
        float totalHeight = 0f;
        float uniformStepSize = Random.Range(coreSettings.minAndMaxHeightSpacing.x, coreSettings.minAndMaxHeightSpacing.y);
        bool alternatingStartsLeft = true; // Random.value < 0.5f;
        Vector3 previousCenter = Vector3.zero;
        float seedOffset = Random.Range(0f, 1000f);

        //if (edgeSettings.edgeCollapseMode == EdgeCollapseMode.RandomPatterned)
        //    edgeSettings.collapsePattern = GenerateRandomCollapsePattern(coreSettings.splinePointCount);

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
                ? widthSettings.randomWidthBiasCurve.Evaluate(heightRatio)
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
            float minAngle = coreSettings.minAndMaxAngle.x;
            float maxAngle = coreSettings.minAndMaxAngle.y;

            float angle;

            if (i == 0 || useSymmetry)
            {
                angle = 0f;
            }
            else
            {
                // true asymmetric range
                angle = Mathf.Lerp(minAngle, maxAngle, biased);
            }
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
                //case EdgeCollapseMode.Patterned:
                //case EdgeCollapseMode.RandomPatterned:
                //    int segmentGroup = Mathf.FloorToInt((float)i / coreSettings.splinePointCount * edgeSettings.collapsePattern.Length);
                //    segmentGroup = Mathf.Clamp(segmentGroup, 0, edgeSettings.collapsePattern.Length - 1);
                //    char patternChar = edgeSettings.collapsePattern[segmentGroup];
                //    switch (patternChar)
                //    {
                //        case 'L': collapseLeftSide = true; break;
                //        case 'R': collapseRightSide = true; break;
                //        case 'B': collapseLeftSide = true; collapseRightSide = true; break;
                //    }
                //    break;
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

            //case TipLeanMode.None:
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

    AnimationCurve GenerateCurvature_OutwardCurve()
    {
        int keyCount = 12;
        AnimationCurve curve = new AnimationCurve();

        // PeakFactor controls WHEN the curve bends
        float exponent = Mathf.Lerp(4f, 0.5f, curvatureSettings.curvature_PeakFactor);

        // PeakFactor controls HOW FAR the tip goes
        float tipStrength = curvatureSettings.curvature_Max * curvatureSettings.curvature_PeakFactor;

        for (int i = 0; i < keyCount; i++)
        {
            float t = i / (float)(keyCount - 1);

            float shaped = Mathf.Pow(t, exponent);

            // Scale so the tip reaches exactly tipStrength
            float baseValue = shaped * tipStrength;

            // Add noise
            float noise = (Mathf.PerlinNoise(t * 3f, Random.value * 10f) - 0.5f)
                          * curvatureSettings.curvature_StepSize
                          * curvatureSettings.curvature_Max;

            float finalValue = baseValue + noise;

            if (i == keyCount - 1 && tipSettings.tipLeanMode == TipLeanMode.ForcedCenterX)
                finalValue = 0f;

            curve.AddKey(t, finalValue);
        }

        for (int i = 0; i < curve.keys.Length; i++)
            curve.SmoothTangents(i, 0.5f);

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

            case CurvatureMode.RandomOutward:
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
            useSymmetry = this.useSymmetry,
            bladeGenerationData = bladeGeneration.GetData(),
            shaderParamaters = swordShaderControl.GetData()
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
        bladePreset = BladePresets.None;

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

        uiControl.RefreshUI();
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




        if (preset.bladeGenerationData != null)
        {
            bladeGeneration.ApplyData(preset.bladeGenerationData);
        }

        if(preset.shaderParamaters != null)
        {
            swordShaderControl.ApplyData(preset.shaderParamaters);
        }

        GenerateLinesAndSplines();

    }


    [ContextMenu("Save Current Preset")]
    public void SavePreset()
    {
        SaveCurrentPreset(presetName);
    }

    public void MoveSplinePoint(int index, Vector3 worldPos)
    {
        if (splineContainer == null) return;
        if (index < 0 || index >= splineContainer.Spline.Count) return;


        Vector3 localPos = transform.InverseTransformPoint(worldPos);

        // Original center before move
        Vector3 originalCenter = splineContainer.Spline[index].Position;

        Vector3 delta = localPos - originalCenter;

        ApplyFalloffMovement(index, delta);
    }

    void ApplyFalloffMovement(int centerIndex, Vector3 delta)
    {
        int count = splineContainer.Spline.Count;

        for (int i = 0; i < count; i++)
        {
            int distance = Mathf.Abs(i - centerIndex);
            if (distance > editFalloffRadius) continue;

            float t = distance / (float)editFalloffRadius;
            float weight = editFalloffCurve.Evaluate(t);

            BezierKnot knot = splineContainer.Spline[i];

            Vector3 pos = (Vector3)knot.Position;

            // Apply weighted movement
            pos += delta * weight;

            // Convert back
            knot.Position = (Unity.Mathematics.float3)pos;
            splineContainer.Spline[i] = knot;
        }

        UpdateSegmentsFromSpline();
    }


    void UpdateSegmentsFromSpline()
    {
        for (int i = 0; i < segments.Count; i++)
        {
            Segment oldSeg = segments[i];

            // Store offsets BEFORE center changes
            Vector3 leftOffset = oldSeg.left - oldSeg.center;
            Vector3 rightOffset = oldSeg.right - oldSeg.center;

            Vector3 newCenter = splineContainer.Spline[i].Position;

            segments[i] = new Segment
            {
                center = newCenter,
                left = newCenter + leftOffset,
                right = newCenter + rightOffset
            };
        }

        bladeGeneration.Generate3DBlade(false);
    }

    public void SetRandomParamaters()
    {
        // 90% chance of realistic sword, 10% chance of experimental/fantasy
        bool isRealistic = UnityEngine.Random.value < 0.9f;

        // === CORE SETTINGS ===
        coreSettings.splinePointCount = isRealistic ?
            UnityEngine.Random.Range(5, 8) :
            UnityEngine.Random.Range(3, 12);

        coreSettings.heightSpacing = isRealistic ?
            UnityEngine.Random.Range(0.2f, 0.5f) :
            UnityEngine.Random.Range(0.25f, 0.5f);

        // Height spacing mode
        float spacingRoll = UnityEngine.Random.value;
        if (isRealistic)
        {
            // 60% Fixed, 30% RandomUniform, 10% SetHeight
            coreSettings.heightSpacingMode = spacingRoll < 0.8f ? HeightSpacingMode.SetHeight :
                                              spacingRoll < 0.9f ? HeightSpacingMode.RandomUniform :
                                              HeightSpacingMode.Fixed;
        }
        else
        {
            coreSettings.heightSpacingMode = (HeightSpacingMode)UnityEngine.Random.Range(0, 2);
        }

        coreSettings.totalBladeHeight = isRealistic ?
            UnityEngine.Random.Range(2f, 5f) :
            UnityEngine.Random.Range(0.5f, 10f);

        coreSettings.minAndMaxHeightSpacing = isRealistic ?
            new Vector2(UnityEngine.Random.Range(0.3f, 0.5f), UnityEngine.Random.Range(0.6f, 1f)) :
            new Vector2(UnityEngine.Random.Range(0.1f, 0.5f), UnityEngine.Random.Range(0.5f, 1f));

        coreSettings.minAndMaxWidth = isRealistic ?
            new Vector2(UnityEngine.Random.Range(0.2f, 0.3f), UnityEngine.Random.Range(0.3f, .6f)) :
            new Vector2(UnityEngine.Random.Range(0.2f, 0.5f), UnityEngine.Random.Range(0.5f, .7f));

        coreSettings.minAndMaxAngle = isRealistic ?
            new Vector2(UnityEngine.Random.Range(-10f, -5f), UnityEngine.Random.Range(5f, 10f)) :
            new Vector2(UnityEngine.Random.Range(-25, -5f), UnityEngine.Random.Range(5f, 25));

        // === SYMMETRY ===
        // 70% use symmetry for realistic blades
        useSymmetry = isRealistic ? UnityEngine.Random.value < 0.7f : UnityEngine.Random.value < 0.4f;


        // === WIDTH SETTINGS ===
        widthSettings.useRandomWidthCurve = isRealistic ?
            UnityEngine.Random.value < 0.8f :
            UnityEngine.Random.value < 0.5f;

        widthSettings.noiseInfluence = isRealistic ?
            UnityEngine.Random.Range(0f, 0.3f) :
            UnityEngine.Random.Range(0f, 1f);

        widthSettings.noiseFrequency = UnityEngine.Random.Range(0.05f, 0.5f);

        // Generate user defined curve if not using random
        if (!widthSettings.useRandomWidthCurve)
        {
            widthSettings.userDefinedCurve = GenerateRandomWidthCurve();
        }

        // === TIP SETTINGS ===
        float tipRoll = UnityEngine.Random.value;
        if (isRealistic)
        {
            // 70% Centered, 20% ForcedCenterX, 10% RandomLean
            tipSettings.tipLeanMode = tipRoll < 0.7f ? TipLeanMode.Centered :
                                       tipRoll < 0.9f ? TipLeanMode.ForcedCenterX :
                                       TipLeanMode.RandomLean;
        }
        else
        {
            tipSettings.tipLeanMode = (TipLeanMode)UnityEngine.Random.Range(0, 6);
        }

        tipSettings.heightOffset = isRealistic ?
            UnityEngine.Random.Range(0, 0.3f) :
            UnityEngine.Random.Range(-.3f, 1f);

        // Generate tip lean strength curve
        tipSettings.tipLeanStrengthCurve = GenerateRealisticCurve(
            isRealistic ? 0.3f : 0f,
            isRealistic ? 0.8f : 1f
        );



        float curvatureRoll = UnityEngine.Random.value;
        if (isRealistic)
        {
            // 40% None, 30% RandomCurve, 20% RandomOutward, 10% SickleCurve
            curvatureSettings.curvatureMode = curvatureRoll < 0.4f ? CurvatureMode.None :
                                               curvatureRoll < 0.7f ? CurvatureMode.RandomCurve :
                                               curvatureRoll < 0.9f ? CurvatureMode.RandomOutward :
                                               CurvatureMode.SickleCurve;
        }
        else
        {
            curvatureSettings.curvatureMode = (CurvatureMode)UnityEngine.Random.Range(0, 5);
        }


        if (curvatureSettings.curvatureMode != CurvatureMode.None)
        {
            // === CURVATURE SETTINGS ===
            curvatureSettings.straightSegmentThreshold = isRealistic ?
                UnityEngine.Random.Range(1, 3) :
                UnityEngine.Random.Range(1, 5);
        }
        else
        {
            curvatureSettings.straightSegmentThreshold = isRealistic ?
               UnityEngine.Random.Range(0, 3) :
               UnityEngine.Random.Range(0, 5);
        }
    

        curvatureSettings.curvature_Max = isRealistic ?
            UnityEngine.Random.Range(0.2f, 0.5f) :
            UnityEngine.Random.Range(0f, 2f);

        curvatureSettings.curvature_PeakFactor = UnityEngine.Random.Range(0.5f, 0.8f);

        curvatureSettings.curvature_StepSize =  0; // isRealistic ?
        //    UnityEngine.Random.Range(0.01f, 0.08f) :
        //    UnityEngine.Random.Range(0f, 0.2f);

        // Generate curvature shape
        curvatureSettings.curvatureShape = GenerateRealisticCurve(
            isRealistic ? 0.3f : 0f,
            isRealistic ? 0.8f : 1f
        );

        // Set curvature direction (mostly horizontal)
        curvatureSettings.curvatureDirection = new Vector3(
            UnityEngine.Random.value < 0.5f ? -1f : 1f,
            0f,
            0f
        );

        // === EDGE SETTINGS ===
        float edgeRoll = UnityEngine.Random.value;
        if (isRealistic)
        {
            // 60% None, 25% Alternating, 10% LooseAlternating, 5% others
            curvatureSettings.curvatureMode = edgeRoll < 0.6f ? CurvatureMode.None :
                                               edgeRoll < 0.85f ? CurvatureMode.RandomCurve :
                                               edgeRoll < 0.95f ? CurvatureMode.RandomOutward :
                                               CurvatureMode.SickleCurve;

            edgeSettings.edgeCollapseMode = edgeRoll < 0.8f ? EdgeCollapseMode.None :
                                            edgeRoll < 0.9f ? EdgeCollapseMode.Alternating :
                                            edgeRoll < 0.95f ? EdgeCollapseMode.LooseAlternating :
                                            (EdgeCollapseMode)UnityEngine.Random.Range(0, 5);
        }
        else
        {
            edgeSettings.edgeCollapseMode = (EdgeCollapseMode)UnityEngine.Random.Range(0, 5);
        }

        // Generate collapse pattern if needed
        if (edgeSettings.edgeCollapseMode != EdgeCollapseMode.None)
        {
            edgeSettings.collapsePattern = GenerateRandomCollapsePattern(coreSettings.splinePointCount);
        }

        edgeSettings.spineOffset = isRealistic ?
            UnityEngine.Random.Range(-0.2f, 0.2f) :
            UnityEngine.Random.Range(-1f, 1f);

        // === REGENERATE ===
        GenerateLinesAndSplines();
    }

    private AnimationCurve GenerateRealisticCurve(float minValue, float maxValue)
    {
        int keyCount = UnityEngine.Random.Range(3, 6);
        Keyframe[] keys = new Keyframe[keyCount];

        keys[0] = new Keyframe(0f, UnityEngine.Random.Range(minValue, maxValue));
        keys[keyCount - 1] = new Keyframe(1f, UnityEngine.Random.Range(minValue, maxValue));

        for (int i = 1; i < keyCount - 1; i++)
        {
            float time = i / (float)(keyCount - 1);
            keys[i] = new Keyframe(time, UnityEngine.Random.Range(minValue, maxValue));
        }

        AnimationCurve curve = new AnimationCurve(keys);

        // Smooth the curve
        for (int i = 0; i < keyCount; i++)
        {
            curve.SmoothTangents(i, 0.5f);
        }

        return curve;
    }
}