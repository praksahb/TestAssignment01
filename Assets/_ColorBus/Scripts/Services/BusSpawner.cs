using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BusSpawner : MonoBehaviour
{
    [Header("Configuration")]
    [SerializeField] private Bus _busPrefab;
    [SerializeField] private Transform[] _waitingSpots; // 0: FrontLeft, 1: FrontRight...
    [SerializeField] private SimpleSpline _levelPath;
    [SerializeField] private Transform _exitPoint;

    [Header("Auto-Schedule Settings")]
    [SerializeField] private List<Node> _levelNodes = new List<Node>(); // All nodes in the level
    // Defines the ORDER of colors to spawn. 
    // The Spawner will calculate the COUNT needed for each color based on the Nodes.
    // e.g. If specificLevelSequence is [Red, Green], it spawns ALL needed Red, then ALL needed Green.
    // If you need Red, Green, Red, you should add them to the list in that order.
    // BUT checking "Total" vs "Batch" is tricky.
    // For now, let's just aggregate totals per color and strict order?
    // User Update: "bus count should be equivalent to total number... divided by capacity"
    [SerializeField] private List<CharacterColor> _spawnSequencePrototype;

    [Header("Debug Settings")]
    [SerializeField] private bool _debugMode = false;
    [SerializeField] private int _debugSpawnCount = 10;
    [SerializeField] private List<CharacterColor> _debugSpawnSequence;

    private Queue<CharacterColor> _busQueue = new Queue<CharacterColor>();
    private Bus[] _waitingLocationOccupants;
    private int _busCapacity;

    public bool HasBusesPending => _busQueue.Count > 0;

    // Critical Section Lock for Entry Point merge
    private bool _entryLocked = false;

    // Limits
    [SerializeField] private int _maxActiveBuses = 5;
    private int _currentOnPathBuses = 0;
    public int ActiveBusCount => _currentOnPathBuses;

    public bool RequestBusLaunch()
    {
        if (_currentOnPathBuses >= _maxActiveBuses) return false;

        _currentOnPathBuses++;
        if (UIManager.Instance != null) UIManager.Instance.UpdateBusCount(_currentOnPathBuses, _maxActiveBuses);

        return true;
    }

    public bool TryLockEntry()
    {
        if (_entryLocked) return false;
        _entryLocked = true;
        return true;
    }

    public void UnlockEntry()
    {
        _entryLocked = false;
    }

    private void Awake()
    {
        if (_waitingSpots != null)
            _waitingLocationOccupants = new Bus[_waitingSpots.Length];
    }

    private void OnEnable()
    {
        GameManager.OnLevelStart += OnLevelStartRef;
    }

    private void OnDisable()
    {
        GameManager.OnLevelStart -= OnLevelStartRef;
    }


    private void OnLevelStartRef()
    {
        // Init UI
        if (UIManager.Instance != null) UIManager.Instance.UpdateBusCount(_currentOnPathBuses, _maxActiveBuses);

        CalculateSchedule();
        StartCoroutine(SpawnBusRoutine());
    }

    [ContextMenu("Recalculate Schedule")]
    public void CalculateSchedule()
    {
        _busQueue.Clear();

        if (_debugMode)
        {
            Debug.Log($"BusSpawner: Generating Debug Schedule for {_debugSpawnCount} buses.");
            for (int i = 0; i < _debugSpawnCount; i++)
            {
                CharacterColor c = CharacterColor.Red;
                if (_debugSpawnSequence != null && _debugSpawnSequence.Count > 0)
                {
                    c = _debugSpawnSequence[i % _debugSpawnSequence.Count];
                }
                else
                {
                    // Fallback to random if no sequence provided
                    // We cast to int, assuming Red=0, Blue=1 etc. Adjust range if needed or use Enum.GetValues
                    c = (CharacterColor)Random.Range(0, 4);
                }
                _busQueue.Enqueue(c);
            }
            return;
        }

        // 1. Tally up total characters needed for each color across ALL nodes
        Dictionary<CharacterColor, int> colorTotals = new Dictionary<CharacterColor, int>();

        foreach (var node in _levelNodes)
        {
            if (node == null) continue;
            foreach (var batch in node.Batches)
            {
                if (!colorTotals.ContainsKey(batch.BatchColor)) colorTotals[batch.BatchColor] = 0;
                colorTotals[batch.BatchColor] += batch.CharList.Count;
            }
        }

        // 2. Generate Queue based on Prototype Sequence
        // If the user defines [Green, Red, Blue], we calculate total Green buses needed, add them, then Red, etc.
        // This is a naive implementation but fits "Total / Capacity".

        if (_busPrefab != null)
        {
            _busCapacity = _busPrefab.Capacity;
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

    private IEnumerator SpawnBusRoutine()
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

    private int GetFirstAvailableSpot()
    {
        if (_waitingLocationOccupants == null) return -1;
        for (int i = 0; i < _waitingLocationOccupants.Length; i++)
        {
            if (_waitingLocationOccupants[i] == null) return i;
        }
        return -1;
    }

    private void SpawnBus(CharacterColor color, int index)
    {
        Bus bus = Instantiate(_busPrefab);
        Transform spawnSpot = _waitingSpots[index];

        _waitingLocationOccupants[index] = bus;

        bus.InitializeAsQueued(_levelPath, _exitPoint, color, spawnSpot, index, this);
        bus.OnLeaveQueue += HandleBusLeavingQueue;
        bus.OnBusDeparted += HandleBusFullDeparture;
    }

    private void HandleBusLeavingQueue(Bus bus)
    {
        int freedSlot = -1;

        bus.OnLeaveQueue -= HandleBusLeavingQueue;

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
            // Identify Lane (0=Left, 1=Right) -> assuming interleaved slots 0,1,2,3...
            // Slot 0, 2, 4 = Lane 0
            // Slot 1, 3, 5 = Lane 1
            int lane = freedSlot % 2;
            CompactLane(lane);
        }
    }

    private void HandleBusFullDeparture(Bus bus)
    {
        _currentOnPathBuses--;        
        bus.OnBusDeparted -= HandleBusFullDeparture;
        if (_currentOnPathBuses < 0) _currentOnPathBuses = 0;

        if (UIManager.Instance != null) UIManager.Instance.UpdateBusCount(_currentOnPathBuses, _maxActiveBuses);

        GameManager.Instance.CheckLevelStatus();
    }

    // Robust Iterative Compaction
    // Simplified Collection-Based Compaction (O(N) per lane)
    private void CompactLane(int laneRemainder)
    {
        // 1. Collect all valid buses in this lane
        List<Bus> laneBuses = new List<Bus>();
        for (int i = laneRemainder; i < _waitingLocationOccupants.Length; i += 2)
        {
            if (_waitingLocationOccupants[i] != null)
            {
                laneBuses.Add(_waitingLocationOccupants[i]);
                _waitingLocationOccupants[i] = null; // Clear current spot
            }
        }

        // 2. Place them back into the lane starting from the front
        int targetSlot = laneRemainder;
        foreach (Bus bus in laneBuses)
        {
            _waitingLocationOccupants[targetSlot] = bus;
            // Always update position; if it's the same slot, the Bus script should handle the no-op or short move
            bus.UpdateQueuePosition(_waitingSpots[targetSlot], targetSlot);
            targetSlot += 2;
        }
    }
}
