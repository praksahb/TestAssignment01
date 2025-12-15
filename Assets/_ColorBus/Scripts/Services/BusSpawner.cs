using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BusSpawner : GenericSingleton<BusSpawner>
{
    [Header("Configuration")]
    [SerializeField] private GameObject _busPrefab;
    public GameObject BusPrefab => _busPrefab;

    [SerializeField] private Transform[] _waitingSpots; // 0: FrontLeft, 1: FrontRight...
    public Transform[] WaitingSpots => _waitingSpots;

    [SerializeField] private SimpleSpline _levelPath;
    public SimpleSpline LevelPath => _levelPath;

    [SerializeField] private Transform _exitPoint;
    public Transform ExitPoint => _exitPoint;
    
    [Header("Auto-Schedule Settings")]
    [SerializeField] private List<Node> _levelNodes = new List<Node>(); // All nodes in the level
    public List<Node> LevelNodes => _levelNodes;

    // Defines the ORDER of colors to spawn. 
    // The Spawner will calculate the COUNT needed for each color based on the Nodes.
    // e.g. If specificLevelSequence is [Red, Green], it spawns ALL needed Red, then ALL needed Green.
    // If you need Red, Green, Red, you should add them to the list in that order.
    // BUT checking "Total" vs "Batch" is tricky.
    // For now, let's just aggregate totals per color and strict order?
    // User Update: "bus count should be equivalent to total number... divided by capacity"
    [SerializeField] private List<CharacterColor> _spawnSequencePrototype; 
    public List<CharacterColor> SpawnSequencePrototype => _spawnSequencePrototype;

    private Queue<CharacterColor> _busQueue = new Queue<CharacterColor>();
    private Bus[] _waitingLocationOccupants;
    private int _busCapacity = 5; // Default fallback

    [Header("Limits")]
    [SerializeField] private int _maxActiveBuses = 5;
    public int MaxActiveBuses => _maxActiveBuses;

    private int _currentActiveBuses = 0;
    
    public bool CanLaunchBus => _currentActiveBuses < _maxActiveBuses;

    public bool HasBusesPending => _busQueue.Count > 0;

    private void Start()
    {
        if (_waitingSpots != null)
            _waitingLocationOccupants = new Bus[_waitingSpots.Length];

        CalculateSchedule();
        StartCoroutine(SpawnBusRoutine());
    }
    
    [Header("Debug")]
    [SerializeField] private bool _debugOverride = false;
    [SerializeField] private int _debugBusCount = 4;

    [ContextMenu("Recalculate Schedule")]
    public void CalculateSchedule()
    {
        _busQueue.Clear();
        
        if (_debugOverride)
        {
            Debug.Log("BusSpawner: DEBUG OVERRIDE ENABLED. Spawning test buses.");
            for (int i = 0; i < _debugBusCount; i++)
            {
                CharacterColor c = (CharacterColor)Random.Range(0, 4); // Random color
                if (_spawnSequencePrototype != null && _spawnSequencePrototype.Count > 0)
                {
                     c = _spawnSequencePrototype[i % _spawnSequencePrototype.Count];
                }
                _busQueue.Enqueue(c);
            }
            return;
        }

        // 1. Tally up total characters needed for each color across ALL nodes
        Dictionary<CharacterColor, int> colorTotals = new Dictionary<CharacterColor, int>();
        
        foreach (var node in _levelNodes)
        {
            if(node == null) continue;
            foreach (var batch in node.Batches)
            {
                if (!colorTotals.ContainsKey(batch.BatchColor)) colorTotals[batch.BatchColor] = 0;
                colorTotals[batch.BatchColor] += batch.CharList.Count;
            }
        }
        
        if (colorTotals.Count == 0)
        {
            Debug.LogWarning("BusSpawner: No nodes or empty nodes found. No buses scheduled. Enable Debug Override to test without nodes.");
        }

        // 2. Generate Queue based on Prototype Sequence
        // If the user defines [Green, Red, Blue], we calculate total Green buses needed, add them, then Red, etc.
        // This is a naive implementation but fits "Total / Capacity".
        
        if (_busPrefab != null)
        {
            Bus b = _busPrefab.GetComponent<Bus>();
            if (b != null) _busCapacity = b.Capacity; // Use Property
        }
        
        if (colorTotals.Count > 0)
        {
            // Temporary list to shuffle
            List<CharacterColor> tempSpawnList = new List<CharacterColor>();

            // 2. Generate List based on Prototype Sequence first
            if (_spawnSequencePrototype.Count > 0)
            {
                foreach (var color in _spawnSequencePrototype)
                {
                    if (colorTotals.ContainsKey(color))
                    {
                        int totalChars = colorTotals[color];
                        int busesNeed = Mathf.CeilToInt(totalChars / (float)_busCapacity);

                        for (int i = 0; i < busesNeed; i++)
                        {
                            tempSpawnList.Add(color);
                        }
                        colorTotals[color] = 0;
                    }
                }
            }

            // 3. Fallback for leftovers
            foreach (var kvp in colorTotals)
            {
                if (kvp.Value > 0)
                {
                    int busesNeed = Mathf.CeilToInt(kvp.Value / (float)_busCapacity);
                    for (int i = 0; i < busesNeed; i++) tempSpawnList.Add(kvp.Key);
                }
            }

            // 4. RANDOMIZE
            Shuffle(tempSpawnList);

            // 5. Enqueue
            foreach (var color in tempSpawnList)
            {
                _busQueue.Enqueue(color);
            }
        }

        Debug.Log($"BusSpawner: Scheduled {_busQueue.Count} buses (Randomized) from {_levelNodes.Count} nodes.");
    }
    
    // Fisher-Yates Shuffle
    private void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int r = Random.Range(0, i + 1);
            T temp = list[i];
            list[i] = list[r];
            list[r] = temp;
        }
    }

    IEnumerator SpawnBusRoutine()
    {
        yield return new WaitForSeconds(1.0f); 

        while (_busQueue.Count > 0)
        {
            int targetIndex = GetFirstAvailableSpot();
            
            if (targetIndex != -1)
            {
                CharacterColor c = _busQueue.Dequeue();
                SpawnBus(c, targetIndex);
            }
            yield return new WaitForSeconds(1.0f);
        }
    }

    int GetFirstAvailableSpot()
    {
        if (_waitingLocationOccupants == null) return -1;
        for (int i = 0; i < _waitingLocationOccupants.Length; i++)
        {
            if (_waitingLocationOccupants[i] == null) return i;
        }
        return -1;
    }

    void SpawnBus(CharacterColor color, int index)
    {
        GameObject busObj = Instantiate(_busPrefab);
        Bus bus = busObj.GetComponent<Bus>();
        Transform spawnSpot = _waitingSpots[index];
            
        _waitingLocationOccupants[index] = bus;
            
        bus.InitializeAsQueued(_levelPath, _exitPoint, color, spawnSpot, index);
        bus.OnLeaveQueue += HandleBusLeavingQueue;
        bus.OnBusDeparted += HandleBusFullDeparture;
    }

    public bool RequestBusLaunch()
    {
        if (_currentActiveBuses < _maxActiveBuses)
        {
            _currentActiveBuses++;
            if (UIManager.Instance != null) UIManager.Instance.UpdateActiveBusUI(_currentActiveBuses, _maxActiveBuses);
            return true;
        }
        return false;
    }

    void HandleBusLeavingQueue(Bus bus)
    {
        // Count is already incremented in RequestBusLaunch when the user tapped.
        
        int freedSlot = -1;
        for (int i = 0; i < _waitingLocationOccupants.Length; i++)
        {
            if (_waitingLocationOccupants[i] == bus)
            {
                freedSlot = i;
                _waitingLocationOccupants[i] = null;
                break;
            }
        }
        
        if (freedSlot != -1)
        {
             AdvanceLane(freedSlot);
        }
    }

    void HandleBusFullDeparture(Bus bus)
    {
        _currentActiveBuses--;
        if (_currentActiveBuses < 0) _currentActiveBuses = 0;
        if (UIManager.Instance != null) UIManager.Instance.UpdateActiveBusUI(_currentActiveBuses, _maxActiveBuses);

        StartCoroutine(CheckStatusWithDelay());
    }

    private IEnumerator CheckStatusWithDelay()
    {
        yield return null; // Wait for Destroy to finish
        if(GameManager.Instance != null)
             GameManager.Instance.CheckLevelStatus();
    }
    
    void AdvanceLane(int targetSlot)
    {
        int sourceSlot = targetSlot + 2;
        if (sourceSlot < _waitingLocationOccupants.Length)
        {
            Bus mover = _waitingLocationOccupants[sourceSlot];
            if (mover != null)
            {
                _waitingLocationOccupants[targetSlot] = mover;
                _waitingLocationOccupants[sourceSlot] = null;
                mover.UpdateQueuePosition(_waitingSpots[targetSlot], targetSlot);
                AdvanceLane(sourceSlot);
            }
        }
    }
}
