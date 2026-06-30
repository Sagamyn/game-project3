using UnityEngine;

public class BloodSplatter : MonoBehaviour
{
    [Header("Appearance")]
    public Color splatterColor = new Color(0.45f, 0.05f, 0.05f, 0.85f);

    void Awake()
    {
        GenerateSplatterMesh();
    }

    void GenerateSplatterMesh()
    {
        // Create an irregular blob shape using random radius points
        int    pointCount = 10;
        float  baseRadius = 0.12f;

        Mesh mesh = new Mesh();
        Vector3[] vertices = new Vector3[pointCount + 1];
        vertices[0] = Vector3.zero; // center

        UnityEngine.Random.InitState(GetInstanceID());

        for (int i = 0; i < pointCount; i++)
        {
            float angle = (360f / pointCount) * i * Mathf.Deg2Rad;

            // Randomize radius for irregular splatter shape
            float radius = baseRadius *
                UnityEngine.Random.Range(0.6f, 1.3f);

            vertices[i + 1] = new Vector3(
                Mathf.Cos(angle) * radius,
                Mathf.Sin(angle) * radius,
                0
            );
        }

        // Triangle fan from center
        int[] triangles = new int[pointCount * 3];
        for (int i = 0; i < pointCount; i++)
        {
            triangles[i * 3]     = 0;
            triangles[i * 3 + 1] = i + 1;
            triangles[i * 3 + 2] = (i + 1) % pointCount + 1;
        }

        mesh.vertices  = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();

        MeshFilter   mf = gameObject.AddComponent<MeshFilter>();
        MeshRenderer mr = gameObject.AddComponent<MeshRenderer>();

        mf.mesh = mesh;

        // Simple unlit material with our color
        Material mat = new Material(Shader.Find("Sprites/Default"));
        mat.color = splatterColor;
        mr.material = mat;

        mr.sortingOrder = 0; // render below soldiers, above hex grid
    }
}