using System.Collections.Generic;
using UnityEngine;

public class RuntimeEditing : MonoBehaviour
{
    [Header("Shape Settings")]
    [Range(0.01f, 1f)]
    public float thinAmount = 0.2f;


    [Tooltip("How many segments on each side of the hit get affected")]
    [Range(1,10)]
    public int segmentInfluence = 2;

    [Header("Smoothing")]
    public bool useSmoothing = true;

    [Range(0, 5)]
    public int smoothingPasses = 3;

    private Camera cam;


    [Header("Cursor")]
    public Texture2D normalTex;
    public Texture2D hammerTex;
    private bool usingHammerTex = false;

    [Header("Highlight Visual")]
    public Material highlightMaterial;

    GameObject highlightObj;
    Mesh highlightMesh;


    [Header("Hover Highlight")]
    public bool showSegmentHighlight = true;
    public Color highlightColor = Color.cyan;
    [Header("Highlight Box")]
    public float highlightHeight = 0.05f;    // along blade length
    public float highlightThickness = 0.02f; // out of blade surface


    int hoveredSegmentIndex = -1;
    BladeGeneration hoveredBlade = null;

    [Header("Symmetry")]
    public bool symmetricalEditing = true;
    public float minEdgeDistance = 0.05f; // absolute minimum distance from center
    public float maxEdgeDistance = 1f;    // optional max distance from cente


    [Header("Spline Center Visualization")]
    public bool showCenterPoints = true;
    public float centerPointSize = 0.015f;
    public Material centerPointMaterial;
    public Material centerPointHoverMaterial;

    private List<GameObject> centerPointObjects = new List<GameObject>();
    private int hoveredCenterIndex = -1;

    private SplineAndLineGen splineGen;


    void Start()
    {
        cam = Camera.main;

        if (normalTex != null)
        {
            Vector2 hotspot = new Vector2(normalTex.width * 0.5f, normalTex.height * 0.5f);
            Cursor.SetCursor(normalTex, hotspot, CursorMode.Auto);
        }

        CreateHighlightObject();

    }

    void Update()
    {
        Ray ray = cam.ScreenPointToRay(Input.mousePosition);

        hoveredBlade = null;
        hoveredSegmentIndex = -1;

        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            BladeGeneration blade = hit.collider.GetComponent<BladeGeneration>();

            if (blade != null)
            {

                hoveredBlade = blade;
                splineGen = blade.GetComponent<SplineAndLineGen>();
                hoveredSegmentIndex = FindClosestSegmentIndex(blade, hit.point);


                // Switch to hammer cursor
                if (hammerTex != null && !usingHammerTex)
                {
                    Vector2 hotspot = new Vector2(hammerTex.width * 0.5f, hammerTex.height * 0.5f);
                    Cursor.SetCursor(hammerTex, hotspot, CursorMode.Auto);
                    usingHammerTex = true;
                }

                // Editing
                if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1))
                {
                    bool expand = Input.GetMouseButtonDown(1);
                    EditBladeAtHit(blade, hit, expand);
                }

                if (hoveredCenterIndex != -1 && Input.GetMouseButton(0) && splineGen != null)
                {
                    Vector3 planeNormal = hoveredBlade.transform.forward;
                    Plane dragPlane = new Plane(planeNormal, hoveredBlade.transform.position);

                    if (dragPlane.Raycast(ray, out float enter))
                    {
                        Vector3 worldHit = ray.GetPoint(enter);

                        // LOCK height so blade doesn't stretch
                        Vector3 local = hoveredBlade.transform.InverseTransformPoint(worldHit);
                        local.y = splineGen.segments[hoveredCenterIndex].center.y;

                        worldHit = hoveredBlade.transform.TransformPoint(local);

                        splineGen.MoveSplinePoint(hoveredCenterIndex, worldHit);
                    }
                }

            }


            UpdateHighlightMesh(hit.point);

        }
        if (hoveredBlade == null && normalTex != null && usingHammerTex)
        {
            Vector2 hotspot = new Vector2(normalTex.width * 0.5f, normalTex.height * 0.5f);
            Cursor.SetCursor(normalTex, hotspot, CursorMode.Auto);
            usingHammerTex = false;
        }

        if (hoveredBlade != null)
            UpdateCenterPointPositions(hoveredBlade);
        else
            SetCenterPointsActive(false);
    }

    void CreateHighlightObject()
    {
        highlightObj = new GameObject("SegmentHighlight");
        highlightObj.transform.SetParent(null);

        MeshFilter mf = highlightObj.AddComponent<MeshFilter>();
        MeshRenderer mr = highlightObj.AddComponent<MeshRenderer>();

        highlightMesh = new Mesh();
        mf.mesh = highlightMesh;

        mr.material = highlightMaterial;
        highlightObj.SetActive(false);
    }

    void EditBladeAtHit(BladeGeneration blade, RaycastHit hit, bool expand)
    {
        var segments = blade.segments;
        if (segments == null || segments.Count == 0) return;

        int closestIndex = FindClosestSegmentIndex(blade, hit.point);
        if (closestIndex < 0) return;

        for (int i = 0; i < segments.Count; i++)
        {
            int indexDist = Mathf.Abs(i - closestIndex);
            if (indexDist > segmentInfluence)
                continue;

            float falloff = 1f - (indexDist / (float)(segmentInfluence + 1));
            falloff = Mathf.SmoothStep(0f, 1f, falloff);

            Segment s = segments[i];

            Vector3 leftDir = s.left - s.center;
            Vector3 rightDir = s.right - s.center;

            float direction = expand ? 1f : -1f;
            float scale = 1f + (thinAmount * falloff * direction);

            // ---- Determine affected side per segment ----
            Vector3 worldSegCenter = blade.transform.TransformPoint(s.center);
            Vector3 worldSegLeft = blade.transform.TransformPoint(s.left);
            Vector3 worldSegRight = blade.transform.TransformPoint(s.right);

            Vector3 segWidthDir = (worldSegRight - worldSegLeft).normalized;
            float hitAlongSegWidth = Vector3.Dot(hit.point - worldSegCenter, segWidthDir);
            bool affectLeft = hitAlongSegWidth < 0f;

            // ---- Apply scaling and clamp edges ----
            if (symmetricalEditing)
            {
                Vector3 newLeft = s.center + leftDir * scale;
                Vector3 newRight = s.center + rightDir * scale;

                newLeft = ClampEdge(s.center, newLeft);
                newRight = ClampEdge(s.center, newRight);

                s.left = newLeft;
                s.right = newRight;
            }
            else
            {
                if (affectLeft)
                {
                    Vector3 newLeft = s.center + leftDir * scale;
                    newLeft = ClampEdge(s.center, newLeft);
                    s.left = newLeft;
                }
                else
                {
                    Vector3 newRight = s.center + rightDir * scale;
                    newRight = ClampEdge(s.center, newRight);
                    s.right = newRight;
                }
            }

            segments[i] = s;
        }

        if (useSmoothing && smoothingPasses > 0)
            SmoothSegments(segments, closestIndex, symmetricalEditing, true); // 'true' here means smoothing will follow left/right selection per segment

        blade.Generate3DBlade(false);
    }

    // ---- Helper function to keep edges within min/max ----
    Vector3 ClampEdge(Vector3 center, Vector3 edge)
    {
        Vector3 dir = edge - center;
        float mag = dir.magnitude;

        if (mag < minEdgeDistance)
            return center + dir.normalized * minEdgeDistance;
        if (mag > maxEdgeDistance)
            return center + dir.normalized * maxEdgeDistance;

        return edge;
    }


    int FindClosestSegmentIndex(BladeGeneration blade, Vector3 worldHitPoint)
    {
        var segments = blade.segments;
        int closestIndex = -1;
        float minDist = float.MaxValue;

        for (int i = 0; i < segments.Count; i++)
        {
            Vector3 worldCenter = blade.transform.TransformPoint(segments[i].center);
            float dist = Vector3.SqrMagnitude(worldCenter - worldHitPoint);

            if (dist < minDist)
            {
                minDist = dist;
                closestIndex = i;
            }
        }

        return closestIndex;
    }


    void SmoothSegments(List<Segment> segments, int centerIndex, bool symmetrical, bool affectLeft)
    {
        for (int pass = 0; pass < smoothingPasses; pass++)
        {
            for (int i = 1; i < segments.Count - 1; i++)
            {
                int indexDist = Mathf.Abs(i - centerIndex);
                if (indexDist > segmentInfluence + 1)
                    continue;

                Segment prev = segments[i - 1];
                Segment curr = segments[i];
                Segment next = segments[i + 1];

                if (symmetrical)
                {
                    curr.left = (prev.left + curr.left + next.left) / 3f;
                    curr.right = (prev.right + curr.right + next.right) / 3f;
                }
                else
                {
                    if (affectLeft)
                        curr.left = (prev.left + curr.left + next.left) / 3f;
                    else
                        curr.right = (prev.right + curr.right + next.right) / 3f;
                }

                segments[i] = curr;
            }
        }
    }

    void UpdateHighlightMesh(Vector3? optionalHitPoint = null)
    {
        if (!showSegmentHighlight || hoveredBlade == null || hoveredSegmentIndex < 0)
        {
            if (highlightObj != null)
                highlightObj.SetActive(false);
            return;
        }

        var segments = hoveredBlade.segments;
        if (segments == null || hoveredSegmentIndex >= segments.Count)
            return;

        Segment s = segments[hoveredSegmentIndex];

        Vector3 worldCenter = hoveredBlade.transform.TransformPoint(s.center);
        Vector3 worldLeft = hoveredBlade.transform.TransformPoint(s.left);
        Vector3 worldRight = hoveredBlade.transform.TransformPoint(s.right);

        // ===== AXES =====
        Vector3 widthDir = (worldRight - worldLeft).normalized;

        Vector3 lengthDir;
        if (hoveredSegmentIndex < segments.Count - 1)
        {
            Vector3 nextCenter = hoveredBlade.transform.TransformPoint(segments[hoveredSegmentIndex + 1].center);
            lengthDir = (nextCenter - worldCenter).normalized;
        }
        else
        {
            Vector3 prevCenter = hoveredBlade.transform.TransformPoint(segments[hoveredSegmentIndex - 1].center);
            lengthDir = (worldCenter - prevCenter).normalized;
        }

        Vector3 thicknessDir = Vector3.Cross(lengthDir, widthDir).normalized;

        // ===== Determine which side to highlight =====
        bool affectLeft = true; // default to left if no hit passed
        if (optionalHitPoint.HasValue)
        {
            Vector3 hitPoint = optionalHitPoint.Value;
            float hitAlongWidth = Vector3.Dot(hitPoint - worldCenter, widthDir);
            affectLeft = hitAlongWidth < 0f;
        }

        // Choose which edges to show
        Vector3 displayLeft = (symmetricalEditing || affectLeft) ? worldLeft : worldCenter;
        Vector3 displayRight = (symmetricalEditing || !affectLeft) ? worldRight : worldCenter;

        // ===== HALF SIZES =====
        Vector3 halfHeight = lengthDir * (highlightHeight * 0.5f);
        Vector3 halfThick = thicknessDir * (highlightThickness * 0.5f);

        // ===== Box corners =====
        Vector3 v0 = displayLeft - halfHeight - halfThick;
        Vector3 v1 = displayRight - halfHeight - halfThick;
        Vector3 v2 = displayRight + halfHeight - halfThick;
        Vector3 v3 = displayLeft + halfHeight - halfThick;

        Vector3 v4 = displayLeft - halfHeight + halfThick;
        Vector3 v5 = displayRight - halfHeight + halfThick;
        Vector3 v6 = displayRight + halfHeight + halfThick;
        Vector3 v7 = displayLeft + halfHeight + halfThick;

        highlightMesh.Clear();
        highlightMesh.vertices = new Vector3[] { v0, v1, v2, v3, v4, v5, v6, v7 };
        highlightMesh.triangles = new int[]
        {
        // Bottom
        0,1,2, 0,2,3,
        // Top
        4,6,5, 4,7,6,
        // Sides
        0,4,5, 0,5,1,
        1,5,6, 1,6,2,
        2,6,7, 2,7,3,
        3,7,4, 3,4,0
        };

        highlightMesh.RecalculateNormals();
        highlightObj.SetActive(true);
    }

    void BuildCenterPointObjects(BladeGeneration blade)
    {
        // Clear old
        foreach (var go in centerPointObjects)
            Destroy(go);
        centerPointObjects.Clear();

        if (blade == null || blade.segments == null) return;

        for (int i = 0; i < blade.segments.Count; i++)
        {
            GameObject p = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            p.name = "CenterPoint_" + i;
            p.transform.localScale = Vector3.one * centerPointSize;
            p.GetComponent<Collider>().enabled = false; // visual only

            var mr = p.GetComponent<MeshRenderer>();
            mr.material = centerPointMaterial;

            centerPointObjects.Add(p);
        }
    }

    void UpdateCenterPointPositions(BladeGeneration blade)
    {
        if (!showCenterPoints || blade == null || blade.segments == null)
        {
            SetCenterPointsActive(false);
            return;
        }

        if (centerPointObjects.Count != blade.segments.Count)
            BuildCenterPointObjects(blade);

        SetCenterPointsActive(true);

        float minDist = float.MaxValue;
        hoveredCenterIndex = -1;

        for (int i = 0; i < blade.segments.Count; i++)
        {
            Vector3 worldPos = blade.transform.TransformPoint(blade.segments[i].center);
            centerPointObjects[i].transform.position = worldPos;

            // Convert to screen space
            Vector3 screenPos = cam.WorldToScreenPoint(worldPos);

            // Ignore if behind camera
            if (screenPos.z < 0f) continue;

            float dist = Vector2.Distance(Input.mousePosition, screenPos);

            if (dist < minDist)
            {
                minDist = dist;
                hoveredCenterIndex = i;
            }
        }

        // Only allow highlight if mouse is actually near a point
        float hoverRadius = 50; // pixels
        if (minDist > hoverRadius)
            hoveredCenterIndex = -1;

        for (int i = 0; i < centerPointObjects.Count; i++)
        {
            var mr = centerPointObjects[i].GetComponent<MeshRenderer>();
            mr.material = (i == hoveredCenterIndex)
                ? centerPointHoverMaterial
                : centerPointMaterial;
        }

    }


    void SetCenterPointsActive(bool state)
    {
        foreach (var go in centerPointObjects)
            if (go != null)
                go.SetActive(state);
    }
}
