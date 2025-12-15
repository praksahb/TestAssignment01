using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class RoadMeshGenerator : MonoBehaviour
{
    public SimpleSpline spline;
    public float roadWidth = 2.0f;
    public int resolution = 100; // Segments along the path
    public Material roadMaterial;
    public float textureTiling = 10f; // Controls how often the texture repeats

    [ContextMenu("Generate Road")]
    public void GenerateRoad()
    {
        if (spline == null)
        {
            spline = GetComponent<SimpleSpline>();
            if (spline == null)
            {
                Debug.LogError("No SimpleSpline assigned or found!");
                return;
            }
        }

        MeshFilter mf = GetComponent<MeshFilter>();
        Mesh mesh = new Mesh();
        
        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();
        List<Vector2> uvs = new List<Vector2>();

        // Generate points
        for (int i = 0; i <= resolution; i++)
        {
            float t = i / (float)resolution;
            // Handle looping: if loop, t=1 is same as t=0, but we need the vertex for UV wrapping?
            // If spline loops, SimpleSpline handles t > 1? Wrapped.
            
            Vector3 centerPos = spline.GetPoint(t);
            
            // Calculate forward direction to find right vector
            float tNext = Mathf.Min(t + 0.01f, 1.0f);
            if (t >= 1.0f && spline.loop) tNext = 0.01f; // Wrap lookahead
            
            Vector3 nextPos = spline.GetPoint(tNext);
            Vector3 forward = (nextPos - centerPos).normalized;
            
            // Fallback for end point if not looping
            if (forward == Vector3.zero)
            {
                 // Look back
                 Vector3 prev = spline.GetPoint(t - 0.01f);
                 forward = (centerPos - prev).normalized;
            }
            
            Vector3 right = Vector3.Cross(forward, Vector3.forward).normalized * (roadWidth * 0.5f); // Assuming Z is up/forward? No, 2D usually Z is depth.
            // Wait, this is likely a 2D game (SpriteRenderer).
            // If 2D, road is on XY plane. Forward is along the path. Up is Z (into camera).
            // Cross product of Forward (XY) and Back (Z) gives Right (XY).
            
            // Standard 2D: X is right, Y is up.
            // Path moves in XY. Forward is Vector3(dx, dy, 0).
            // Vector3.back is (0, 0, -1). 
            // Cross(Forward, Back) -> Right.
            
            right = Vector3.Cross(forward, Vector3.back).normalized * (roadWidth * 0.5f);

            Vector3 vLeft = centerPos - right;
            Vector3 vRight = centerPos + right;
            
            vertices.Add(vLeft);
            vertices.Add(vRight);
            
            // UVs
            float v = t * textureTiling; // Use the exposed parameter
            uvs.Add(new Vector2(0, v));
            uvs.Add(new Vector2(1, v));
            
            // Triangles
            if (i < resolution)
            {
                int baseIdx = i * 2;
                
                // 0, 2, 1
                triangles.Add(baseIdx);
                triangles.Add(baseIdx + 2);
                triangles.Add(baseIdx + 1);
                
                // 1, 2, 3
                triangles.Add(baseIdx + 1);
                triangles.Add(baseIdx + 2);
                triangles.Add(baseIdx + 3);
            }
        }
        
        mesh.vertices = vertices.ToArray();
        mesh.triangles = triangles.ToArray();
        mesh.uv = uvs.ToArray();
        mesh.RecalculateBounds();
        mesh.RecalculateNormals();
        
        mf.sharedMesh = mesh;
        if (roadMaterial != null) GetComponent<MeshRenderer>().sharedMaterial = roadMaterial;
    }
}
