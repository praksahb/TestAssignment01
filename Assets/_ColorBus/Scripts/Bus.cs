using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class Bus : MonoBehaviour, IPointerDownHandler
{
    [Header("Bus Stats")]
    public CharacterColor color;
    public int capacity = 5;
    public float speed = 5f;
    public List<Character> passengers = new List<Character>();
    public Transform[] seatPositions; // Restored
    
    [Header("Visuals")]
    public Renderer busRenderer; // Inspector Reference
    
    [Header("Path Settings")]
    public SimpleSpline currentPath;
    public Transform exitPoint; // Point to drive to when full
    [SerializeField] private float _forwardCheckDistance = 1.25f;
    [SerializeField] private float _detectionRadius = .75f;

    private bool isMoving = false;
    private bool tapToStart = false;
    private float currentT = 0f;
    private bool isExiting = false;
    private bool isLoadingPassengers = false;

    public System.Action<Bus> OnBusDeparted;
    public System.Action<Bus> OnLeaveQueue; // New event for when bus leaves the waiting spot
    
    private int queueIndex = -1;
    private bool isInQueue = false;

    public bool IsInQueue => isInQueue; // Expose for checks

    public void OnPointerDown(PointerEventData eventData)
    {
        Debug.Log($"Bus Clicked via EventSystem! InQueue: {isInQueue}, Index: {queueIndex}");
        // Only allow start if in first two spots (index 0 or 1)
        if (isInQueue && queueIndex <= 1)
        {
            // Check Limits
            if (BusSpawner.Instance != null)
            {
                if (!BusSpawner.Instance.RequestBusLaunch())
                {
                    Debug.Log("Bus Tap Ignored: Max Active Buses Reached.");
                    return;
                }
            }

            // Just mark as tapped. The LifeCycleRoutine will wait for clear path.
            Debug.Log("Bus Tap Received. Queuing start...");
            tapToStart = true;
        }
        else
        {
             Debug.Log("Bus Tap Ignored (Wrong State/Index).");
        }
    }

    public void InitializeAsQueued(SimpleSpline path, Transform exit, CharacterColor busColor, Transform spawnSpot, int index)
    {
        currentPath = path;
        exitPoint = exit;
        color = busColor;
        queueIndex = index;
        isInQueue = true;
        
        // Visual Tint
        if (busRenderer == null) busRenderer = GetComponentInChildren<Renderer>();
        
        if (busRenderer != null)
        {
            busRenderer.material.color = GetColorValue(busColor);
        }
        
        // Start Entry
        transform.position = spawnSpot.position - new Vector3(0, 5f, 0); // Start below spot
        StartCoroutine(QueueEntryRoutine(spawnSpot.position));
    }
    
    public void UpdateQueuePosition(Transform newSpot, int newIndex)
    {
        queueIndex = newIndex;
        // Animate to new spot
        StartCoroutine(MoveToQueueSpot(newSpot.position));
    }

    private Color GetColorValue(CharacterColor type)
    {
        switch (type)
        {
            case CharacterColor.Red: return Color.red;
            case CharacterColor.Blue: return Color.blue;
            case CharacterColor.Yellow: return Color.yellow;
            case CharacterColor.Green: return Color.green;
            default: return Color.white;
        }
    }

    private IEnumerator QueueEntryRoutine(Vector3 targetPos)
    {
        // Use generic move
        yield return StartCoroutine(MoveToPosition(targetPos, 0.5f));
        // Wait for loop trigger
        StartCoroutine(LifeCycleRoutine());
    }
    
    private IEnumerator MoveToQueueSpot(Vector3 targetPos)
    {
        yield return StartCoroutine(MoveToPosition(targetPos, 0.5f));
    }
    
    private IEnumerator MoveToPosition(Vector3 target, float duration)
    {
        Vector3 start = transform.position;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            transform.position = Vector3.Lerp(start, target, t);
            yield return null;
        }
        transform.position = target;
    }

    // MAIN LOOP
    private IEnumerator LifeCycleRoutine()
    {
        // 1. Wait for Tap
        while (!tapToStart)
        {
            yield return null;
        }

        // 2. Wait for Clear Path (Front check)
        while (IsPathBlocked())
        {
            yield return new WaitForSeconds(0.2f);
        }
        
        // 3. Move to Spline Start
        // We assume the Spline starts near the front waiting spot. 
        // We just snap/lerp to T=0.
        isInQueue = false;
        OnLeaveQueue?.Invoke(this); // Notify Manager to advance queue
        
        yield return StartCoroutine(MoveToPosition(currentPath.GetPointOnPath(0), 0.5f));
        
        // 4. Follow Path Loop
        isMoving = true;
        while (isMoving)
        {
            // Drive
            LoopOnPath();
            yield return null;
        }
        
        // 5. Exit (Handled in LoopOnPath logic when full/end)
        // Cleanup
        OnBusDeparted?.Invoke(this);
        Destroy(gameObject);
    }

    private void LoopOnPath()
    {
        if (isLoadingPassengers) return;

        // Move T
        // Calculate speed relative to path length? 
        // For SimpleSpline, T is 0..1. 
        // Distance approx = Speed * DT. 
        // T_delta = Distance / TotalLength. 
        // Let's assume a fixed increment for now or estimate length.
        float pathLengthApprox = 50f; // Mock length
        float tIncrement = (speed * Time.deltaTime) / pathLengthApprox;
        
        currentT += tIncrement;
        if (currentT > 1.0f) currentT = 0f; // Loop
        
        Vector3 pos = currentPath.GetPointOnPath(currentT);
        
        // Rotation: Look at next point
        Vector3 nextPos = currentPath.GetPointOnPath(currentT + 0.01f);
        Vector3 dir = (nextPos - pos).normalized;
        if (dir != Vector3.zero)
        {
             transform.rotation = Quaternion.Lerp(transform.rotation, Quaternion.LookRotation(dir), 10f * Time.deltaTime);
        }
        
        transform.position = pos;
        
        // Check for Stops
        CheckForStopNode();
        
        // Exit Condition: If Full and near exit? 
        // Or if Full and loop completed X times?
        // Let's say: If Full, we detach from Spline and move to ExitPoint
        if (passengers.Count >= capacity && currentT > 0.8f) // Near end of loop
        {
            // Break loop and drive to exit
            isMoving = false;
            StartCoroutine(DriveToExitRoutine());
        }
    }

    private void CheckForStopNode()
    {
        // Check for Nodes with characters of my Color
        // Using 3D Physics Overlap
        Collider[] limits = Physics.OverlapSphere(transform.position, _detectionRadius); 
        foreach (var hit in limits)
        {
            Node node = hit.GetComponent<Node>();
            if (node != null)
            {
                if (passengers.Count < capacity && node.HasCharacter(this.color))
                {
                    // Check Stop Point proximity
                    if (node.stopPoint != null)
                    {
                        float dist = Vector3.Distance(transform.position, node.stopPoint.position);
                        if (dist > 0.5f) // Keep driving until close
                        {
                             continue; 
                        }
                    }
                    
                    StartCoroutine(StopAndLoadRoutine(node));
                    break; 
                }
            }
        }
    }

    private IEnumerator StopAndLoadRoutine(Node node)
    {
        isLoadingPassengers = true; 
        
        // 1. Move to Stop Point (Fine adjustment)
        if (node.stopPoint != null)
        {
            yield return StartCoroutine(MoveToPosition(node.stopPoint.position, 0.5f)); 
        }
        
        // 2. Load Passengers
        yield return StartCoroutine(LoadPassengersRoutine(node));
        
        // 3. Resume
        isLoadingPassengers = false;
    }

    private IEnumerator LoadPassengersRoutine(Node node)
    {
        while (passengers.Count < capacity && node.HasCharacter(this.color))
        {
            Character c = node.GetNextCharacter(this.color);
            if (c != null)
            {
                passengers.Add(c);
                // Animate Character to Seat
                int seatIdx = passengers.Count - 1;
                Transform seat = transform; // Fallback
                if (seatIdx < seatPositions.Length && seatPositions[seatIdx] != null) seat = seatPositions[seatIdx];

                c.MoveToBus(seat, this);
            }
            yield return new WaitForSeconds(0.2f); // Load speed
        }
        yield return new WaitForSeconds(0.5f); // Wait a bit before leaving
    }
    
    // Front Collision Check
    private bool IsPathBlocked()
    {
        // Cast ray forward
        // In 3D, forward is transform.forward
        if (Physics.Raycast(transform.position, transform.forward, out RaycastHit hit, _forwardCheckDistance))
        {
            Bus otherBus = hit.collider.GetComponent<Bus>();
            if (otherBus != null) return true;
        }
        return false;
    }

    private IEnumerator DriveToExitRoutine()
    {
        // Simple move to exit point
        if (exitPoint != null)
        {
            while (Vector3.Distance(transform.position, exitPoint.position) > 0.5f)
            {
                // Move towards exit
                transform.position = Vector3.MoveTowards(transform.position, exitPoint.position, speed * Time.deltaTime);
                
                // Rot
                Vector3 dir = (exitPoint.position - transform.position).normalized;
                if(dir != Vector3.zero) transform.rotation = Quaternion.LookRotation(dir);
                
                yield return null;
            }
        }
        
        // Done
        OnBusDeparted?.Invoke(this);
        Destroy(gameObject);
    }

    // Debug Gizmos
    // Debug Gizmos
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        // Check forward
        Gizmos.DrawLine(transform.position, transform.position + transform.forward * _forwardCheckDistance);
        
        // Detection Radius
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, _detectionRadius);

        // Entry point visualization if enabled
        if (currentPath != null)
        {
            Gizmos.color = Color.cyan;
            // GetPointOnPath(0) is the start
            Gizmos.DrawWireSphere(currentPath.GetPointOnPath(0f), 0.5f);
        }
    }
}
