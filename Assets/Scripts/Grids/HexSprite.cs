using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class HexSprite : MonoBehaviour
{
    public float size = 0.95f; // slightly smaller than cell to show grid gaps

    void Awake()
    {
        GenerateHexMesh();
    }

    void GenerateHexMesh()
    {
        Mesh mesh = new Mesh();

        Vector3[] vertices = new Vector3[7]; // center + 6 corners
        vertices[0] = Vector3.zero; // center

        for (int i = 0; i < 6; i++)
        {
            float angle = Mathf.Deg2Rad * (60 * i + 30); // flat-top hex
            vertices[i + 1] = new Vector3(
                size * Mathf.Cos(angle),
                size * Mathf.Sin(angle),
                0
            );
        }

        // 6 triangles from center to each edge
        int[] triangles = new int[]
        {
            0,1,2,  0,2,3,  0,3,4,
            0,4,5,  0,5,6,  0,6,1
        };

        mesh.vertices  = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();

        GetComponent<MeshFilter>().mesh = mesh;
    }
}