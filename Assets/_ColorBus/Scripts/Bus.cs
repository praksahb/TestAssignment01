using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class Bus : MonoBehaviour, IPointerDownHandler
{
    public CharacterColor color;
    public int capacity = 5;
    public List<Character> passengers = new List<Character>();
    public float speed = 5f;
    
    [Header("Path Settings")]
    public SimpleSpline currentPath;
    public Transform exitPoint; // Point to drive to when full
    [SerializeField] private float _forwardCheckDistance = 1.25f;
    [SerializeField] private float _detectionRadius = .75f;

    private bool isMoving = false;
    private bool tapToStart = false;
    private float currentT = 0f;
    private bool isExiting = false;

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
        GetComponent<SpriteRenderer>().color = GetColorValue(busColor);
        
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
            
            // Rotation - Fix: Face the target while moving
            Vector3 dir = (target - transform.position).normalized;
            if (dir != Vector3.zero)
            {
                float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                transform.rotation = Quaternion.Lerp(transform.rotation, Quaternion.AngleAxis(angle, Vector3.forward), Time.deltaTime * 15f);
            }
            
            yield return null;
        }
        transform.position = target;
    }

    private IEnumerator LifeCycleRoutine()
    {
        // 1. Wait until Tapped AND at front
        yield return new WaitUntil(() => tapToStart);
        
        // 1b. Wait for Clear Entry
        if (currentPath != null)
        {
             Vector3 firstPoint = currentPath.GetPoint(0f);
             while (true)
             {
                 bool blocked = false;
                 Collider2D[] limits = Physics2D.OverlapCircleAll(firstPoint, _detectionRadius);

                 foreach (var hit in limits)
                 {
                     Bus b = hit.GetComponent<Bus>();
                     if (b != null)
                     {
                         if (b == this) continue;
                         if (b.IsInQueue) continue; // Ignore queued neighbors
                         
                         // Blocked by active bus
                         blocked = true;
                         break;
                     }
                 }
                 
                 if (!blocked) break; // Path is clear!
                 
                 // Wait and retry
                 yield return new WaitForSeconds(0.1f);
             }
        }
        
        isInQueue = false; // Leaving queue
        tapToStart = false; // Reset
        
        // 2. Move to Station (First point of path)
        if (currentPath != null)
        {
            Vector3 stationPos = currentPath.GetPoint(0f);
            yield return StartCoroutine(MoveToPosition(stationPos, 1.0f));
        }

        // Notify Manager that I have cleared the area and am effective starting the path
        OnLeaveQueue?.Invoke(this);
        
        // 3. Loop on Spline until full or no matches
        isMoving = true;
        yield return StartCoroutine(LoopOnPath());
        
        // 4. Exit
        isExiting = true;
        isMoving = false;
        yield return StartCoroutine(ExitScene());
        
        // Done
        OnBusDeparted?.Invoke(this);
        Destroy(gameObject);
    }

    private bool isLoadingPassengers = false; // Flag to pause movement

    private IEnumerator LoopOnPath()
    {
        currentT = 0f;
        
        // Infinite loop until full AND passed threshold
        // We want to exit ONLY if we are full AND we are near the end of the loop (e.g. > 0.8)
        // OR if we just want to keep looping until we find passengers.
        
        while (true)
        {
            // Exit Condition: Full AND past a certain point on the track (e.g. 90% of lap)
            // This ensures we don't snap to exit from the start.
            if (passengers.Count >= capacity && currentT > 0.8f)
            {
                break; 
            }

            // 0. Pause if loading
            if (isLoadingPassengers)
            {
                yield return null;
                continue;
            }

            // PHYSICS CHECK FOR OVERLAP
            Vector3 checkPos = transform.position + (transform.right * _forwardCheckDistance);
            bool blocked = false;
            Collider2D[] limits = Physics2D.OverlapCircleAll(checkPos, _detectionRadius);
            foreach (var hit in limits)
            {
                Bus other = hit.GetComponent<Bus>();
                if (other != null && other != this)
                {
                    if (other.IsInQueue) continue;
                    blocked = true;
                    break;
                }
            }
            
            if (blocked)
            {
                yield return null;
                continue; // Blocked! Wait this frame.
            }

            // Move along spline
            // Speed logic: slower if blocked ahead? (Already handled by stop)
            float step = (speed * 0.05f) * Time.deltaTime; 
            currentT += step;
            
            if (currentT >= 1.0f)
            {
                currentT -= 1.0f; // Loop
            }

            Vector3 nextPos = currentPath.GetPoint(currentT);
            
            // Rotation
            Vector3 dir = (nextPos - transform.position).normalized;
            if (dir != Vector3.zero)
            {
                float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                transform.rotation = Quaternion.Lerp(transform.rotation, Quaternion.AngleAxis(angle, Vector3.forward), Time.deltaTime * 10f);
            }
            
            transform.position = nextPos;
            
            // Interaction: Check for Stop Node
            CheckForStopNode();

            yield return null;
        }
    }
    
    private void CheckForStopNode()
    {
        // Scan for nodes to stop at
        Collider2D[] limits = Physics2D.OverlapCircleAll(transform.position, 1.5f); 
        foreach (var hit in limits)
        {
            Node node = hit.GetComponent<Node>();
            if (node != null)
            {
                // If node has matching characters AND we are not already full
                if (passengers.Count < capacity && node.HasCharacter(this.color))
                {
                    // FIX: Don't stop immediately upon entering trigger.
                    // Drive along the path until we are CLOSE to the StopPoint.
                    if (node.stopPoint != null)
                    {
                        float dist = Vector3.Distance(transform.position, node.stopPoint.position);
                        if (dist > 0.5f) // Threshold: Keep driving until close
                        {
                             continue; 
                        }
                    }
                    
                    StartCoroutine(StopAndLoadRoutine(node));
                    break; // Only handle one node at a time
                }
            }
        }
    }

    private IEnumerator StopAndLoadRoutine(Node node)
    {
        isLoadingPassengers = true; // Pauses the LoopOnPath movement
        
        // 1. Move to Stop Point (Fine adjustment)
        if (node.stopPoint != null)
        {
            // We are already close (dist < 0.5f), so a quick lerp is fine to "Park" perfectly.
            yield return StartCoroutine(MoveToPosition(node.stopPoint.position, 0.5f)); 
        }
        
        // 2. Load Passengers
        yield return StartCoroutine(LoadPassengersRoutine(node));
        
        // 3. Resume
        // Since we drove here on the path, 'currentT' is already approximately correct.
        // We might be slightly offset by the parking lerp, but it shouldn't cause a huge snap.
        
        isLoadingPassengers = false;
    }
    
    private IEnumerator LoadPassengersRoutine(Node node)
    {
        while (passengers.Count < capacity && node.HasCharacter(this.color))
        {
            Character c = node.GetFirstMatchingCharacter(this.color);
            if (c != null)
            {
                // Transfer
                node.RemoveCharacter(c);
                c.transform.SetParent(this.transform);
                
                // Animate jump? For now just hide or local pos
                c.transform.localPosition = Vector3.zero; 
                c.gameObject.SetActive(false); 
                
                passengers.Add(c);
                Debug.Log($"Bus {color} picked up passenger. Count: {passengers.Count}/{capacity}");
                
                yield return new WaitForSeconds(0.2f);
            }
            else
            {
                break;
            }
        }
    }

    private IEnumerator ExitScene()
    {
        // Drive off to Exit Point
        if (exitPoint == null)
        {
            // Fallback
            Vector3 startPos = transform.position;
            Vector3 exitDir = transform.right; 
            Vector3 endPos = startPos + (exitDir * 20f); 
            
            float elapsed = 0f;
            float duration = 2.0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                transform.position = Vector3.Lerp(startPos, endPos, elapsed / duration);
                yield return null;
            }
        }
        else
        {
            Vector3 startPos = transform.position;
            Vector3 endPos = exitPoint.position;
            
            float distance = Vector3.Distance(startPos, endPos);
            float duration = distance / speed; 
            float elapsed = 0f;
            
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                transform.position = Vector3.Lerp(startPos, endPos, t);
                 // Rotation
                Vector3 dir = (endPos - startPos).normalized;
                if (dir != Vector3.zero)
                {
                   float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                   transform.rotation = Quaternion.Lerp(transform.rotation, Quaternion.AngleAxis(angle, Vector3.forward), Time.deltaTime * 10f);
                }
                yield return null;
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        // Visualize Forward Collision Check
        Gizmos.color = Color.yellow;
        Vector3 forwardCheckPos = transform.position + (transform.right * _forwardCheckDistance);
        Gizmos.DrawWireSphere(forwardCheckPos, _detectionRadius);
        
        // Visualize Entry Check (only if path is assigned)
        if (currentPath != null)
        {
            Gizmos.color = Color.cyan;
            Vector3 entryPos = currentPath.GetPoint(0f);
            Gizmos.DrawWireSphere(entryPos, _detectionRadius);
        }
        
        // Visualize Pickup Radius
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, 1.5f);
    }
}
