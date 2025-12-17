using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class Bus : MonoBehaviour, IPointerDownHandler, IPointerClickHandler
{
    [Header("Bus Stats")]
    [SerializeField] private CharacterColor _busColor;
    public CharacterColor BusColor { get => _busColor; set => _busColor = value; }

    [SerializeField] private int _capacity = 5;
    public int Capacity => _capacity;

    [SerializeField] private float _speed = 5f;
    public float Speed => _speed;

    [SerializeField] private List<Character> _passengers = new List<Character>();
    public List<Character> Passengers => _passengers;

    [SerializeField] private Transform[] _seatPositions; 
    public Transform[] SeatPositions => _seatPositions;
    
    [Header("Visuals")]
    [SerializeField] private Renderer _busRenderer; 
    public Renderer BusRenderer => _busRenderer;
    
    [Header("Path Settings")]
    [SerializeField] private SimpleSpline _currentPath;
    public SimpleSpline CurrentPath { get => _currentPath; set => _currentPath = value; }

    [SerializeField] private Transform _exitPoint; 
    public Transform ExitPoint { get => _exitPoint; set => _exitPoint = value; }

    [SerializeField] private float _collisionCheckDistance = 1.25f;
    [SerializeField] private float _nodeDetectionRadius = .75f;

    // State Fields 
    private bool _isMoving = false;
    private bool _tapToStart = false;
    private float _currentT = 0f;
    private bool _isExiting = false;
    private bool _isLoadingPassengers = false;
    private bool _isTransformMoving = false; 

    public float CurrentT => _currentT; 

    public System.Action<Bus> OnBusDeparted;
    public System.Action<Bus> OnLeaveQueue; 
    
    private int _queueIndex = -1;

    [SerializeField] private bool _isInQueue = false;

    // Priority Properties
    public bool IsOnPath => _isMoving; // Higher priority (Main loop)
    public bool IsInQueue => _isInQueue; 
    public bool IsDeparting => _tapToStart; 
    public bool IsMoving => _isMoving || _isTransformMoving; 
    public bool IsLoadingPassengers => _isLoadingPassengers;

    private void Awake()
    {
        _capacity = Mathf.Max(1, _capacity);
    }

    private void OnValidate()
    {
        _capacity = Mathf.Max(1, _capacity);
    }

    public void OnPointerDown(PointerEventData eventData) => TryLaunchBus();
    public void OnPointerClick(PointerEventData eventData) => TryLaunchBus();
    private void OnMouseDown() => TryLaunchBus(); // Fallback

    private void TryLaunchBus()
    {
        if (_tapToStart) return; 

        if (_isInQueue && _queueIndex <= 1)
        {
            if (BusSpawner.Instance != null && !BusSpawner.Instance.RequestBusLaunch())
            {
                Debug.Log("Bus Tap Ignored: Max Active Buses Reached.");
                return;
            }
            if (HapticManager.Instance != null) HapticManager.Instance.TriggerVibrate();
            
            if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX(SoundType.BusTapPositive);

            Debug.Log("Bus Tap Received. Queuing start...");
            _tapToStart = true;
        }
        else if (_isInQueue && _queueIndex > 1)
        {
            if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX(SoundType.BusTapNegative);
        }
    }

    public void InitializeAsQueued(SimpleSpline path, Transform exit, CharacterColor busColor, Transform spawnSpot, int index)
    {
        _currentPath = path;
        _exitPoint = exit;
        _busColor = busColor;
        _queueIndex = index;
        _isInQueue = true;
        
  
        if (_busRenderer == null) _busRenderer = GetComponentInChildren<Renderer>();
        if (_busRenderer != null) _busRenderer.material.color = GetColorValue(busColor);
        
        Vector3 startOffset = new Vector3(0, 0, 10f); 
        transform.position = spawnSpot.position + startOffset;
        transform.rotation = Quaternion.Euler(0, 180, 0); 
        
        StartCoroutine(QueueEntryRoutine(spawnSpot.position));
    }
    
    public void UpdateQueuePosition(Transform newSpot, int newIndex)
    {
        _queueIndex = newIndex;
        StartCoroutine(MoveToQueueSpot(newSpot.position));
    }

    [Header("Animation Settings")]
    [SerializeField] private float _spawnEntryDuration = 1.5f;
    [SerializeField] private float _queueShiftDuration = 0.5f;

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
    
    private IEnumerator MoveToPosition(Vector3 target, float duration, bool lookAtTarget = true, bool checkCollisions = false)
    {
        _isTransformMoving = true;
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
            if (checkCollisions && IsPathBlocked())
            {
                yield return null;
                continue; 
            }

            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            transform.position = Vector3.Lerp(start, target, t);
            if (lookAtTarget) transform.rotation = Quaternion.Slerp(startRot, targetRot, t * 5f);
            
            yield return null;
        }
        transform.position = target;
        if (lookAtTarget) transform.rotation = targetRot;
        _isTransformMoving = false;
    }

    // MAIN LOOP
    private IEnumerator LifeCycleRoutine()
    {
        while (!_tapToStart) yield return null;

        while (IsPathBlocked()) yield return new WaitForSeconds(0.2f);
        
        if (_currentPath == null)
        {
            Debug.LogError("Bus: Path NULL!");
            yield break;
        }
        
        // ACQUIRE LOCK (Entry Merge)
        if (BusSpawner.Instance != null)
        {
            while (!BusSpawner.Instance.TryLockEntry()) yield return new WaitForSeconds(0.1f);
        }

        // Calculate natural entry point
        float entryT = _currentPath.GetClosestT(transform.position);
        _currentT = entryT; // Sync internal time
        
        _isInQueue = false; // Status Update: No longer "Parked", now "Entering"
        
        yield return StartCoroutine(MoveToPosition(_currentPath.GetPointOnPath(entryT), 0.5f, true, true));

        // RELEASE LOCK
        if (BusSpawner.Instance != null) BusSpawner.Instance.UnlockEntry();

        OnLeaveQueue?.Invoke(this); 
        
        // Loop
        _isMoving = true;
        while (_isMoving)
        {
            LoopOnPath();
            yield return null;
        }
        
        if (_isExiting) yield return StartCoroutine(DriveToExitRoutine());

        OnBusDeparted?.Invoke(this);
        Destroy(gameObject);
    }
    
    private void LoopOnPath()
    {
        if (_isLoadingPassengers) return;
        
        if (IsPathBlocked()) return; // Brake

        // Move T
        float pathLengthApprox = 50f; 
        float tIncrement = (_speed * Time.deltaTime) / pathLengthApprox;
        
        _currentT += tIncrement;
        if (_currentT > 1.0f) _currentT = 0f; 
        
        Vector3 pos = _currentPath.GetPointOnPath(_currentT);
        Vector3 nextPos = _currentPath.GetPointOnPath(_currentT + 0.01f);
        Vector3 dir = (nextPos - pos).normalized;
        if (dir != Vector3.zero) transform.rotation = Quaternion.Lerp(transform.rotation, Quaternion.LookRotation(dir), 10f * Time.deltaTime);
        
        transform.position = pos;
        
        CheckForStopNode();
        
        if (_passengers.Count >= _capacity && _currentT > 0.8f) 
        {
            Debug.Log($"[Bus] Exiting Path. Capacity Reached ({_passengers.Count}/{_capacity}). Path T: {_currentT:F2}");
            if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX(SoundType.BusFull);
            _isMoving = false;
            _isExiting = true; 
        }
    }

    private void CheckForStopNode()
    {
        if (_nodeDetectionRadius <= 0f) return; // Feature: Set to 0 to disable stopping

        Collider[] limits = Physics.OverlapSphere(transform.position, _nodeDetectionRadius); 
        // Debug.Log($"[Bus] Node Check. Radius: {_nodeDetectionRadius}, Hits: {limits.Length}");
        foreach (var hit in limits)
        {
            Node node = hit.GetComponent<Node>();
            if (node != null)
            {
                if (_passengers.Count < _capacity && node.HasCharacter(this._busColor))
                {
                    if (node.StopPoint != null)
                    {
                        float dist = Vector3.Distance(transform.position, node.StopPoint.position);
                        if (dist > 0.5f) continue; 
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
        if (node.StopPoint != null) yield return StartCoroutine(MoveToPosition(node.StopPoint.position, 0.5f)); 
        yield return StartCoroutine(LoadPassengersRoutine(node));
        if (_currentPath != null) _currentT = _currentPath.GetClosestT(transform.position);
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
                int seatIdx = _passengers.Count - 1;
                Transform seat = transform; 
                if (seatIdx < _seatPositions.Length && _seatPositions[seatIdx] != null) seat = _seatPositions[seatIdx];

                if (HapticManager.Instance != null) HapticManager.Instance.TriggerVibrate();
                if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX(SoundType.PassengerBoarding);

                c.MoveToBus(seat, this);
                
                float timeout = 3.0f;
                while (c != null && Vector3.Distance(c.transform.position, seat.position) > 0.2f && timeout > 0f)
                {
                     timeout -= Time.deltaTime;
                     yield return null;
                }
            }
        }
        yield return new WaitForSeconds(0.5f); 
    }
    
    private bool IsPathBlocked()
    {
        float boxLen = _collisionCheckDistance;
        float boxWidth = 1.5f; 
        Vector3 center = transform.position + (transform.forward * (boxLen * 0.5f)) + (Vector3.up * 0.5f);
        Vector3 halfExtents = new Vector3(boxWidth * 0.5f, 1.0f, boxLen * 0.5f);
        
        Collider[] hits = Physics.OverlapBox(center, halfExtents, transform.rotation, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
        
        foreach (var hit in hits)
        {
            Bus otherBus = hit.GetComponentInParent<Bus>();
            if (otherBus != null && otherBus != this)
            {
                // CRITICAL SAFETY: Always stop for a loading bus
                if (otherBus.IsLoadingPassengers) return true;

                // DEADLOCK FIX: If I am merging (TransformMoving), I have right of way over path buses.
                if (this._isTransformMoving && otherBus.IsOnPath) continue;

                // DEADLOCK FIX 2: Side-by-Side on Path
                // If we are both on the path, the one 'ahead' (higher T) has right of way.
                // We handle wrap-around simplisticly: if diff > 0.5, we assume wrap.
                if (this.IsOnPath && otherBus.IsOnPath && this.CurrentPath == otherBus.CurrentPath)
                {
                    float diff = this._currentT - otherBus.CurrentT;
                    // Fix Wrap-around logic (e.g. 0.01 vs 0.99)
                    if (diff > 0.5f) diff -= 1f;
                    else if (diff < -0.5f) diff += 1f;

                    if (diff > 0) continue; // I am ahead, I keep going.
                }

                // PRIORITY LOGIC:
                // If I am ON THE PATH (_isMoving), I have right of way over buses that are DEPARTING/ENTERING.
                // FIX: Check 'IsMoving' (Property) which includes merging state. If they are merging, they are an obstacle.
                if (this._isMoving && !otherBus.IsMoving && otherBus.IsDeparting) continue; 

                 if (!otherBus.IsInQueue) return true;
            }
        }
        return false;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        float boxLen = _collisionCheckDistance;
        float boxWidth = 1.5f;
        Vector3 center = transform.position + (transform.forward * (boxLen * 0.5f)) + (Vector3.up * 0.5f);
        Vector3 size = new Vector3(boxWidth, 2.0f, boxLen);
        
        Matrix4x4 old = Gizmos.matrix;
        Gizmos.matrix = Matrix4x4.TRS(center, transform.rotation, Vector3.one);
        Gizmos.DrawWireCube(Vector3.zero, size);
        Gizmos.matrix = old;
        
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, _nodeDetectionRadius);
    }

    private IEnumerator DriveToExitRoutine()
    {
        if (_exitPoint != null)
        {
            while (Vector3.Distance(transform.position, _exitPoint.position) > 0.5f)
            {
                transform.position = Vector3.MoveTowards(transform.position, _exitPoint.position, _speed * Time.deltaTime);
                Vector3 dir = (_exitPoint.position - transform.position).normalized;
                if(dir != Vector3.zero) transform.rotation = Quaternion.LookRotation(dir);
                yield return null;
            }
        }
    }
}
