using System.Collections.Generic;
using UnityEngine;

public class SimpleSpline : MonoBehaviour
{
    // Use Transforms for easier editing in the Scene
    public List<Transform> waypoints = new List<Transform>();
    public bool loop = true;

    // Helper: Context Menu to Auto-Fill from Children
    [ContextMenu("Auto-Fill from Children")]
    public void FillFromChildren()
    {
        waypoints.Clear();
        foreach (Transform child in transform)
        {
            waypoints.Add(child);
        }
    }

    private void OnDrawGizmos()
    {
        if (waypoints == null || waypoints.Count < 2) return;

        Gizmos.color = Color.cyan;
        for (int i = 0; i < waypoints.Count - 1; i++)
        {
            if(waypoints[i] != null && waypoints[i+1] != null)
                Gizmos.DrawLine(waypoints[i].position, waypoints[i+1].position);
        }
        if (loop && waypoints.Count > 1)
        {
            if(waypoints[waypoints.Count - 1] != null && waypoints[0] != null)
                Gizmos.DrawLine(waypoints[waypoints.Count - 1].position, waypoints[0].position);
        }
        
        // Draw Points
        Gizmos.color = Color.blue;
        foreach(var p in waypoints)
        {
            if(p != null) Gizmos.DrawWireSphere(p.position, 0.3f);
        }
    }

    public Vector3 GetPointOnPath(float t)
    {
        if (waypoints == null || waypoints.Count == 0) return transform.position;

        int count = waypoints.Count;
        int numSegments = loop ? count : count - 1;
        
        float segmentT = t * numSegments;
        int currentPointIndex = Mathf.FloorToInt(segmentT);
        float localT = segmentT - currentPointIndex;

        currentPointIndex = currentPointIndex % count;
        int nextPointIndex = (currentPointIndex + 1) % count;

        if (!loop && currentPointIndex >= count - 1)
        {
            return waypoints[count - 1].position;
        }
        
        Vector3 p0 = waypoints[currentPointIndex].position;
        Vector3 p1 = waypoints[nextPointIndex].position;

        return Vector3.Lerp(p0, p1, localT);
    }
}
