using System.Collections.Generic;
using UnityEngine;

public class SimpleSpline : MonoBehaviour
{
    // Use Transforms for easier editing in the Scene
    [UnityEngine.Serialization.FormerlySerializedAs("waypoints")]
    [SerializeField] private List<Transform> _waypoints = new List<Transform>();
    public List<Transform> Waypoints { get => _waypoints; set => _waypoints = value; } // Public setter needed? Maybe strictly for editor scripts.
    
    [UnityEngine.Serialization.FormerlySerializedAs("loop")]
    [SerializeField] private bool _loop = true;
    public bool Loop => _loop;

    // Helper: Context Menu to Auto-Fill from Children
    [ContextMenu("Auto-Fill from Children")]
    public void FillFromChildren()
    {
        _waypoints.Clear();
        foreach (Transform child in transform)
        {
            _waypoints.Add(child);
        }
    }

    private void OnDrawGizmos()
    {
        if (_waypoints == null || _waypoints.Count < 2) return;

        Gizmos.color = Color.cyan;
        for (int i = 0; i < _waypoints.Count - 1; i++)
        {
            if(_waypoints[i] != null && _waypoints[i+1] != null)
                Gizmos.DrawLine(_waypoints[i].position, _waypoints[i+1].position);
        }
        if (_loop && _waypoints.Count > 1)
        {
            if(_waypoints[_waypoints.Count - 1] != null && _waypoints[0] != null)
                Gizmos.DrawLine(_waypoints[_waypoints.Count - 1].position, _waypoints[0].position);
        }
        
        // Draw Points
        Gizmos.color = Color.blue;
        foreach(var p in _waypoints)
        {
            if(p != null) Gizmos.DrawWireSphere(p.position, 0.3f);
        }
    }

    public Vector3 GetPointOnPath(float t)
    {
        if (_waypoints == null || _waypoints.Count == 0) return transform.position;

        int count = _waypoints.Count;
        int numSegments = _loop ? count : count - 1;
        
        float segmentT = t * numSegments;
        int currentPointIndex = Mathf.FloorToInt(segmentT);
        float localT = segmentT - currentPointIndex;

        currentPointIndex = currentPointIndex % count;
        int nextPointIndex = (currentPointIndex + 1) % count;

        if (!_loop && currentPointIndex >= count - 1)
        {
            return _waypoints[count - 1].position;
        }
        
        Vector3 p0 = _waypoints[currentPointIndex].position;
        Vector3 p1 = _waypoints[nextPointIndex].position;

        return Vector3.Lerp(p0, p1, localT);
    }

    public float GetClosestT(Vector3 position)
    {
        if (_waypoints == null || _waypoints.Count < 2) return 0f;

        float bestT = 0f;
        float minDstSqr = float.MaxValue;

        int count = _waypoints.Count;
        int numSegments = _loop ? count : count - 1;

        for (int i = 0; i < numSegments; i++)
        {
            Vector3 p0 = _waypoints[i].position;
            Vector3 p1 = _waypoints[(i + 1) % count].position;

            Vector3 segmentDir = p1 - p0;
            float segmentLenSqr = segmentDir.sqrMagnitude;
            
            float localT = 0f;
            if (segmentLenSqr > 0.0001f)
            {
                // Project point onto line segment
                Vector3 toPos = position - p0;
                float dot = Vector3.Dot(toPos, segmentDir);
                localT = Mathf.Clamp01(dot / segmentLenSqr);
            }

            Vector3 closestPoint = Vector3.Lerp(p0, p1, localT);
            float dstSqr = (position - closestPoint).sqrMagnitude;

            if (dstSqr < minDstSqr)
            {
                minDstSqr = dstSqr;
                bestT = (i + localT) / numSegments;
            }
        }
        
        return bestT;
    }
}
