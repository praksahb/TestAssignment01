using UnityEngine;

public class SimpleSpline : MonoBehaviour
{
    public enum InterpolationMode { Linear, CatmullRom }

    public InterpolationMode interpolation = InterpolationMode.Linear;
    public bool loop = false;
    public Color pathColor = Color.yellow;

    [Header("Editor Tools")]
    public float gridSize = 1f;

    public int PointCount => transform.childCount;

    [ContextMenu("Snap Waypoints to Grid")]
    public void SnapWaypoints()
    {
        foreach (Transform child in transform)
        {
            Vector3 p = child.position;
            p.x = Mathf.Round(p.x / gridSize) * gridSize;
            p.y = Mathf.Round(p.y / gridSize) * gridSize;
            p.z = Mathf.Round(p.z / gridSize) * gridSize;
            child.position = p;
        }
    }

    public Vector3 GetPoint(float t)
    {
        int count = PointCount;
        if (count < 2) return transform.position;

        t = Mathf.Clamp01(t);

        int segmentCount = loop ? count : count - 1;
        float scaledT = t * segmentCount;
        int segIndex = Mathf.FloorToInt(scaledT);
        float localT = scaledT - segIndex;

        if (segIndex >= segmentCount)
        {
            segIndex = segmentCount - 1;
            localT = 1f;
        }

        int i0 = GetIndex(segIndex - 1);
        int i1 = GetIndex(segIndex);
        int i2 = GetIndex(segIndex + 1);
        int i3 = GetIndex(segIndex + 2);

        Vector3 p0 = transform.GetChild(i0).position;
        Vector3 p1 = transform.GetChild(i1).position;
        Vector3 p2 = transform.GetChild(i2).position;
        Vector3 p3 = transform.GetChild(i3).position;

        if (interpolation == InterpolationMode.Linear)
            return Vector3.Lerp(p1, p2, localT);

        return CatmullRom(localT, p0, p1, p2, p3);
    }

    int GetIndex(int i)
    {
        int count = PointCount;
        if (loop)
            return (i + count) % count;

        return Mathf.Clamp(i, 0, count - 1);
    }

    Vector3 CatmullRom(float t, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
    {
        float t2 = t * t;
        float t3 = t2 * t;

        return 0.5f * (
            (2f * p1) +
            (-p0 + p2) * t +
            (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
            (-p0 + 3f * p1 - 3f * p2 + p3) * t3
        );
    }

    void OnDrawGizmos()
    {
        if (PointCount < 2) return;

        Gizmos.color = pathColor;
        const int steps = 64;

        Vector3 prev = GetPoint(0f);
        for (int i = 1; i <= steps; i++)
        {
            float t = i / (float)steps;
            Vector3 p = GetPoint(t);
            Gizmos.DrawLine(prev, p);
            prev = p;
        }

        foreach (Transform child in transform)
            Gizmos.DrawSphere(child.position, 0.15f);
    }
}
