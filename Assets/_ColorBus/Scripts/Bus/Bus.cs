using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class Bus : MonoBehaviour, IPointerDownHandler, IPointerClickHandler
{
    [Header("Bus Stats")]
    [UnityEngine.Serialization.FormerlySerializedAs("color")]
    [SerializeField] private CharacterColor _busColor;
    public CharacterColor BusColor { get => _busColor; set => _busColor = value; }

    [UnityEngine.Serialization.FormerlySerializedAs("capacity")]
    [SerializeField] private int _capacity = 5;
    public int Capacity => _capacity;

    [UnityEngine.Serialization.FormerlySerializedAs("speed")]
    [SerializeField] private float _speed = 5f;
    public float Speed => _speed;

    [UnityEngine.Serialization.FormerlySerializedAs("passengers")]
    [SerializeField] private List<Character> _passengers = new List<Character>();
    public List<Character> Passengers => _passengers;

    [UnityEngine.Serialization.FormerlySerializedAs("seatPositions")]
    [SerializeField] private Transform[] _seatPositions; // Restored
    public Transform[] SeatPositions => _seatPositions;
    
    [Header("Visuals")]
    [UnityEngine.Serialization.FormerlySerializedAs("busRenderer")]
    [SerializeField] private Renderer _busRenderer; // Inspector Reference
    public Renderer BusRenderer => _busRenderer;
    
    [Header("Path Settings")]
    [UnityEngine.Serialization.FormerlySerializedAs("currentPath")]
    [SerializeField] private SimpleSpline _currentPath;
    public SimpleSpline CurrentPath { get => _currentPath; set => _currentPath = value; }

    [UnityEngine.Serialization.FormerlySerializedAs("exitPoint")]
    [SerializeField] private Transform _exitPoint; // Point to drive to when full
    public Transform ExitPoint { get => _exitPoint; set => _exitPoint = value; }

    [SerializeField] private float _forwardCheckDistance = 1.25f;
    [SerializeField] private float _detectionRadius = .75f;

    private bool _isMoving = false;
    private bool _tapToStart = false;
    private float _currentT = 0f;
    private bool _isExiting = false;
    private bool _isLoadingPassengers = false;

    public System.Action<Bus> OnBusDeparted;
    public System.Action<Bus> OnLeaveQueue; // New event for when bus leaves the waiting spot
    
    private int _queueIndex = -1;
    [SerializeField] private bool _isInQueue = false;

    public bool IsInQueue => _isInQueue; // Expose for checks

    public void OnPointerDown(PointerEventData eventData)
    {
        Debug.Log("OnPointerDown Event Received!");
        TryLaunchBus();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        Debug.Log("OnPointerClick Event Received!");
        TryLaunchBus();
    }

    // Fallback for direct Unity Input without Raycaster
    private void OnMouseDown()
    {
        Debug.Log("OnMouseDown Event Received!");
        TryLaunchBus();
    }

    private void TryLaunchBus()
    {
        if (_isInQueue && _queueIndex <= 1)
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

            Debug.Log("Bus Tap Received. Queuing start...");
            _tapToStart = true;
        }
    }

    public void InitializeAsQueued(SimpleSpline path, Transform exit, CharacterColor busColor, Transform spawnSpot, int index)
    {
        _currentPath = path;
        _exitPoint = exit;
        _busColor = busColor;
        _queueIndex = index;
        _isInQueue = true;
        
        // Visual Tint
        if (_busRenderer == null) _busRenderer = GetComponentInChildren<Renderer>();
        if (_busRenderer != null)
        {
            _busRenderer.material.color = GetColorValue(busColor);
        }
        
        // Spawn Logic: 
        // Enters from "Below" (-Z) and moves UP to the spot (+Z direction relative to spawn).
        // User requested Y Rotation 180 (Facing Camera/Down).
        Vector3 startOffset = new Vector3(0, 0, 10f); 
        transform.position = spawnSpot.position + startOffset;
        
        // Force Rotation 180
        transform.rotation = Quaternion.Euler(0, 180, 0); 
        
        // Move to Spot WITHOUT rotating (Keep facing 180)
        StartCoroutine(QueueEntryRoutine(spawnSpot.position));
    }
    
    public void UpdateQueuePosition(Transform newSpot, int newIndex)
    {
        _queueIndex = newIndex;
        // Slide to next spot, keeping rotation (180)
        StartCoroutine(MoveToQueueSpot(newSpot.position));
    }

    [Header("Animation Settings")]
    [UnityEngine.Serialization.FormerlySerializedAs("spawnEntryDuration")]
    [SerializeField] private float _spawnEntryDuration = 1.5f;
    public float SpawnEntryDuration => _spawnEntryDuration;

    [UnityEngine.Serialization.FormerlySerializedAs("queueShiftDuration")]
    [SerializeField] private float _queueShiftDuration = 0.5f;
    public float QueueShiftDuration => _queueShiftDuration;

    private UnityEngine.Color GetColorValue(CharacterColor type)
    {
        switch (type)
        {
            case CharacterColor.Red: return UnityEngine.Color.red;
            case CharacterColor.Blue: return UnityEngine.Color.blue;
            case CharacterColor.Yellow: return UnityEngine.Color.yellow;
            case CharacterColor.Green: return UnityEngine.Color.green;
            default: return UnityEngine.Color.white;
        }
    }

    private IEnumerator QueueEntryRoutine(Vector3 targetPos)
    {
        yield return StartCoroutine(MoveToPosition(targetPos, _spawnEntryDuration, false));
        StartCoroutine(LifeCycleRoutine());
    }
    
    private IEnumerator MoveToQueueSpot(Vector3 targetPos)
    {
        yield return StartCoroutine(MoveToPosition(targetPos, _queueShiftDuration, false));
    }
    
    private IEnumerator MoveToPosition(Vector3 target, float duration, bool lookAtTarget = true)
    {
        Vector3 start = transform.position;
        Quaternion startRot = transform.rotation;
        
        Quaternion targetRot = startRot;
        if (lookAtTarget)
        {
             Vector3 dir = (target - start).normalized;
             if(dir != Vector3.zero) targetRot = Quaternion.LookRotation(dir);
        }
        
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            transform.position = Vector3.Lerp(start, target, t);
            if (lookAtTarget)
            {
               transform.rotation = Quaternion.Slerp(startRot, targetRot, t * 5f);
            }
            yield return null;
        }
        transform.position = target;
        if (lookAtTarget) transform.rotation = targetRot;
    }

    // MAIN LOOP
    private IEnumerator LifeCycleRoutine()
    {
        // 1. Wait for Tap
        while (!_tapToStart)
        {
            yield return null;
        }

        // 2. Wait for Clear Path (Front check)
        while (IsPathBlocked())
        {
            yield return new WaitForSeconds(0.2f);
        }
        
        if (_currentPath == null)
        {
            Debug.LogError("Bus: Cannot start moving, CurrentPath is NULL! Assign LevelPath in BusSpawner.");
            yield break;
        }
        
        // 3. Move to Spline Start
        // We assume the Spline starts near the front waiting spot. 
        // We just snap/lerp to T=0.
        _isInQueue = false;
        OnLeaveQueue?.Invoke(this); // Notify Manager to advance queue
        
        yield return StartCoroutine(MoveToPosition(_currentPath.GetPointOnPath(0), 0.5f));
        
        // 4. Follow Path Loop
        _isMoving = true;
        while (_isMoving)
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
        if (_isLoadingPassengers) return;
        
        // Dynamic Obstacle Check (Collision Avoidance)
        if (IsPathBlocked())
        {
            return; // Brake/Wait
        }

        // Move T
        // Calculate speed relative to path length? 
        // For SimpleSpline, T is 0..1. 
        // Distance approx = Speed * DT. 
        // T_delta = Distance / TotalLength. 
        // Let's assume a fixed increment for now or estimate length.
        float pathLengthApprox = 50f; // Mock length
        float tIncrement = (_speed * Time.deltaTime) / pathLengthApprox;
        
        _currentT += tIncrement;
        if (_currentT > 1.0f) _currentT = 0f; // Loop
        
        Vector3 pos = _currentPath.GetPointOnPath(_currentT);
        
        // Rotation: Look at next point
        Vector3 nextPos = _currentPath.GetPointOnPath(_currentT + 0.01f);
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
        if (_passengers.Count >= _capacity && _currentT > 0.8f) // Near end of loop
        {
            // Break loop and drive to exit
            _isMoving = false;
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
                if (_passengers.Count < _capacity && node.HasCharacter(this._busColor))
                {
                    // Check Stop Point proximity
                    if (node.StopPoint != null)
                    {
                        float dist = Vector3.Distance(transform.position, node.StopPoint.position);
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
        _isLoadingPassengers = true; 
        
        // 1. Move to Stop Point (Fine adjustment)
        if (node.StopPoint != null)
        {
            yield return StartCoroutine(MoveToPosition(node.StopPoint.position, 0.5f)); 
        }
        
        // 2. Load Passengers
        yield return StartCoroutine(LoadPassengersRoutine(node));
        
        // 3. Resume
        _isLoadingPassengers = false;
    }

    private IEnumerator LoadPassengersRoutine(Node node)
    {
        while (_passengers.Count < _capacity && node.HasCharacter(this._busColor))
        {
            Character c = node.GetNextCharacter(this._busColor);
            if (c != null)
            {
                _passengers.Add(c);
                // Animate Character to Seat
                int seatIdx = _passengers.Count - 1;
                Transform seat = transform; // Fallback
                if (seatIdx < _seatPositions.Length && _seatPositions[seatIdx] != null) seat = _seatPositions[seatIdx];

                c.MoveToBus(seat, this);
            }
            yield return new WaitForSeconds(0.2f); // Load speed
        }
        yield return new WaitForSeconds(0.5f); // Wait a bit before leaving
    }
    
    // Front Collision Check
    // Front Collision Check
    // Front Collision Check
    private bool IsPathBlocked()
    {
        // Cast ray forward
        // In 3D, forward is transform.forward
        // Ignore Triggers (Nodes) so we don't get blocked by them and miss the bus behind them.
        if (Physics.Raycast(transform.position, transform.forward, out RaycastHit hit, _forwardCheckDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            // Use GetComponentInParent in case we hit a child collider (wheels, body mesh)
            Bus otherBus = hit.collider.GetComponentInParent<Bus>();
            // Check if it is valid, not me, and NOT in the queue (parked buses shouldn't block the road)
            if (otherBus != null && otherBus != this && !otherBus.IsInQueue) 
            {
                Debug.DrawLine(transform.position, hit.point, Color.red);
                Debug.Log($"Bus {name} Blocked by {otherBus.name}. InQueue: {otherBus.IsInQueue}");
                return true;
            }
        }
        return false;
    }

    private IEnumerator DriveToExitRoutine()
    {
        // Simple move to exit point
        if (_exitPoint != null)
        {
            while (Vector3.Distance(transform.position, _exitPoint.position) > 0.5f)
            {
                // Move towards exit
                transform.position = Vector3.MoveTowards(transform.position, _exitPoint.position, _speed * Time.deltaTime);
                
                // Rot
                Vector3 dir = (_exitPoint.position - transform.position).normalized;
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
        if (_currentPath != null)
        {
            Gizmos.color = Color.cyan;
            // GetPointOnPath(0) is the start
            Gizmos.DrawWireSphere(_currentPath.GetPointOnPath(0f), 0.5f);
        }
    }
}
