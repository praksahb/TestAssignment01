using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BusSpawner : MonoBehaviour
{
    public static BusSpawner Instance; // Singleton-ish for easy access if needed, or linked via GameManager

    [Header("Configuration")]
    public GameObject busPrefab;
    public Transform[] waitingSpots; // 0: FrontLeft, 1: FrontRight...
    public SimpleSpline levelPath;
    public Transform exitPoint;
    
    [Header("Auto-Schedule Settings")]
    public List<Node> levelNodes = new List<Node>(); // All nodes in the level
    // Defines the ORDER of colors to spawn. 
    // The Spawner will calculate the COUNT needed for each color based on the Nodes.
    // e.g. If specificLevelSequence is [Red, Green], it spawns ALL needed Red, then ALL needed Green.
    // If you need Red, Green, Red, you should add them to the list in that order.
    // BUT checking "Total" vs "Batch" is tricky.
    // For now, let's just aggregate totals per color and strict order?
    // User Update: "bus count should be equivalent to total number... divided by capacity"
    public List<CharacterColor> spawnSequencePrototype; 

    private Queue<CharacterColor> busQueue = new Queue<CharacterColor>();
    private Bus[] waitingLocationOccupants;
    private int busCapacity = 5; // Default fallback

    [Header("Limits")]
    public int maxActiveBuses = 5;
    private int currentActiveBuses = 0;
    
    public bool CanLaunchBus => currentActiveBuses < maxActiveBuses;

    public bool HasBusesPending => busQueue.Count > 0;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        if (waitingSpots != null)
            waitingLocationOccupants = new Bus[waitingSpots.Length];

        CalculateSchedule();
        StartCoroutine(SpawnBusRoutine());
    }
    
    [ContextMenu("Recalculate Schedule")]
    public void CalculateSchedule()
    {
        busQueue.Clear();
        
        // 1. Tally up total characters needed for each color across ALL nodes
        Dictionary<CharacterColor, int> colorTotals = new Dictionary<CharacterColor, int>();
        
        foreach (var node in levelNodes)
        {
            if(node == null) continue;
            foreach (var batch in node.batches)
            {
                if (!colorTotals.ContainsKey(batch.color)) colorTotals[batch.color] = 0;
                colorTotals[batch.color] += batch.charList.Count;
            }
        }
        
        // 2. Generate Queue based on Prototype Sequence
        // If the user defines [Green, Red, Blue], we calculate total Green buses needed, add them, then Red, etc.
        // This is a naive implementation but fits "Total / Capacity".
        
        if (busPrefab != null)
        {
            Bus b = busPrefab.GetComponent<Bus>();
            if (b != null) busCapacity = b.capacity;
        }
        
        if (colorTotals.Count > 0)
        {
            // Temporary list to shuffle
            List<CharacterColor> tempSpawnList = new List<CharacterColor>();

            // 2. Generate List based on Prototype Sequence first
            if (spawnSequencePrototype.Count > 0)
            {
                foreach (var color in spawnSequencePrototype)
                {
                    if (colorTotals.ContainsKey(color))
                    {
                        int totalChars = colorTotals[color];
                        int busesNeed = Mathf.CeilToInt(totalChars / (float)busCapacity);

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
                    int busesNeed = Mathf.CeilToInt(kvp.Value / (float)busCapacity);
                    for (int i = 0; i < busesNeed; i++) tempSpawnList.Add(kvp.Key);
                }
            }

            // 4. RANDOMIZE
            Shuffle(tempSpawnList);

            // 5. Enqueue
            foreach (var color in tempSpawnList)
            {
                busQueue.Enqueue(color);
            }
        }

        Debug.Log($"BusSpawner: Scheduled {busQueue.Count} buses (Randomized) from {levelNodes.Count} nodes.");
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

        while (busQueue.Count > 0)
        {
            int targetIndex = GetFirstAvailableSpot();
            
            if (targetIndex != -1)
            {
                CharacterColor c = busQueue.Dequeue();
                SpawnBus(c, targetIndex);
            }
            yield return new WaitForSeconds(1.0f);
        }
    }

    int GetFirstAvailableSpot()
    {
        if (waitingLocationOccupants == null) return -1;
        for (int i = 0; i < waitingLocationOccupants.Length; i++)
        {
            if (waitingLocationOccupants[i] == null) return i;
        }
        return -1;
    }

    void SpawnBus(CharacterColor color, int index)
    {
        GameObject busObj = Instantiate(busPrefab);
        Bus bus = busObj.GetComponent<Bus>();
        Transform spawnSpot = waitingSpots[index];
            
        waitingLocationOccupants[index] = bus;
            
        bus.InitializeAsQueued(levelPath, exitPoint, color, spawnSpot, index);
        bus.OnLeaveQueue += HandleBusLeavingQueue;
        bus.OnBusDeparted += HandleBusFullDeparture;
    }

    public bool RequestBusLaunch()
    {
        if (currentActiveBuses < maxActiveBuses)
        {
            currentActiveBuses++;
            if (UIManager.Instance != null) UIManager.Instance.UpdateActiveBusUI(currentActiveBuses, maxActiveBuses);
            return true;
        }
        return false;
    }

    void HandleBusLeavingQueue(Bus bus)
    {
        // Count is already incremented in RequestBusLaunch when the user tapped.
        
        int freedSlot = -1;
        for (int i = 0; i < waitingLocationOccupants.Length; i++)
        {
            if (waitingLocationOccupants[i] == bus)
            {
                freedSlot = i;
                waitingLocationOccupants[i] = null;
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
        currentActiveBuses--;
        if (currentActiveBuses < 0) currentActiveBuses = 0;
        if (UIManager.Instance != null) UIManager.Instance.UpdateActiveBusUI(currentActiveBuses, maxActiveBuses);

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
        if (sourceSlot < waitingLocationOccupants.Length)
        {
            Bus mover = waitingLocationOccupants[sourceSlot];
            if (mover != null)
            {
                waitingLocationOccupants[targetSlot] = mover;
                waitingLocationOccupants[sourceSlot] = null;
                mover.UpdateQueuePosition(waitingSpots[targetSlot], targetSlot);
                AdvanceLane(sourceSlot);
            }
        }
    }
}
