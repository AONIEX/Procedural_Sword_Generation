using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ProceduralSwordGen : MonoBehaviour
{
    [System.Serializable]
    public class SwordData
    {
        public float bladeLength;
        public float bladeWidth;
        public float bladeThickness;
        public Color bladeColor;
        public string hiltStyle;
        public float damage;
        public float swingSpeed;
    }

    public GameObject bladeObject;

    public SwordData GenerateRandomSword()
    {
        SwordData sword = new SwordData();
        sword.bladeLength = Random.Range(1.0f, 3.0f);
        sword.bladeWidth = Random.Range(0.1f, 0.3f);
        sword.bladeThickness = Random.Range(0.02f, 0.1f); // NEW: depth
        sword.bladeColor = new Color(Random.value, Random.value, Random.value);
        sword.hiltStyle = Random.Range(0, 2) == 0 ? "Crossguard" : "Katana";
        sword.damage = Random.Range(10, 50);
        sword.swingSpeed = Random.Range(1.0f, 3.0f);
        return sword;
    }

    void Start()
    {
        StartGenerating();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            Destroy(bladeObject);
            StartGenerating();
        }
    }

    public void StartGenerating()
    {
        bladeObject = new GameObject("ProceduralBlade");
        bladeObject.AddComponent<MeshFilter>();
        bladeObject.AddComponent<MeshRenderer>();

        SwordData swordData = GenerateRandomSword();
        Mesh bladeMesh = GenerateBladeMesh(swordData);

        bladeObject.GetComponent<MeshFilter>().mesh = bladeMesh;

        Material bladeMaterial = new Material(Shader.Find("Standard"));
        bladeMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);

        bladeMaterial.color = swordData.bladeColor;
        bladeObject.GetComponent<MeshRenderer>().material = bladeMaterial;

        bladeObject.transform.position = Vector3.zero;
    }

    public Mesh GenerateBladeMesh(SwordData data)
    {
        Mesh mesh = new Mesh();

        int verticalLines = 5;           // 5 vertical lines = 4 segments
        int horizontalSteps = 20;        // smooth taper
        float curveAmount = 0.5f;        // how much the blade curves sideways

        float bladeLength = data.bladeLength;
        float bladeWidth = data.bladeWidth;
        float bladeThickness = data.bladeThickness;

        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();

        // Generate vertices
        for (int y = 0; y <= horizontalSteps; y++)
        {
            float height = (bladeLength / horizontalSteps) * y;

            // Smooth taper using sine curve
            float taper = Mathf.Sin((1f - y / (float)horizontalSteps) * Mathf.PI * 0.5f);
            float width = bladeWidth * taper;
            float depth = bladeThickness * taper;

            // Sideways curvature along X-axis
            float curveX = Mathf.Sin((height / bladeLength) * Mathf.PI) * curveAmount;

            for (int x = 0; x < verticalLines; x++)
            {
                float t = x / (float)(verticalLines - 1);
                float xPos = Mathf.Lerp(-width, width, t);

                // Sharpen outer edges by reducing depth
                float zOffset = (x == 0 || x == verticalLines - 1) ? depth * 0.1f : depth;

                // Apply sideways curve
                float curvedX = xPos + curveX;

                // Front face vertex
                vertices.Add(new Vector3(curvedX, height, zOffset));
                // Back face vertex
                vertices.Add(new Vector3(curvedX, height, -zOffset));
            }
        }

        // Connect vertices into quads
        for (int y = 0; y < horizontalSteps; y++)
        {
            for (int x = 0; x < verticalLines - 1; x++)
            {
                int rowStart = y * verticalLines * 2;
                int nextRowStart = (y + 1) * verticalLines * 2;

                int i0 = rowStart + x * 2;
                int i1 = i0 + 2;
                int i2 = nextRowStart + x * 2;
                int i3 = i2 + 2;

                // Front face (clockwise)
                triangles.Add(i0);
                triangles.Add(i1);
                triangles.Add(i2);
                triangles.Add(i1);
                triangles.Add(i3);
                triangles.Add(i2);

                // Back face (clockwise)
                triangles.Add(i2 + 1);
                triangles.Add(i3 + 1);
                triangles.Add(i1 + 1);
                triangles.Add(i2 + 1);
                triangles.Add(i1 + 1);
                triangles.Add(i0 + 1);
            }
        }

        mesh.vertices = vertices.ToArray();
        mesh.triangles = triangles.ToArray();
        mesh.RecalculateNormals();

        return mesh;
    }
}