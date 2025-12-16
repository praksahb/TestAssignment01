using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class Node : MonoBehaviour
{
    private void Reset()
    {
        // Auto-configure collider when script is added
        BoxCollider col = GetComponent<BoxCollider>(); // Changed from BoxCollider2D to BoxCollider
        if (col == null) col = gameObject.AddComponent<BoxCollider>(); // Changed from BoxCollider2D to BoxCollider
        col.isTrigger = true;
        col.size = new Vector3(2, 2, 2); // Changed from Vector2 to Vector3 for 3D collider
    }

    private void Awake()
    {
        // Ensure runtime safety
        BoxCollider col = GetComponent<BoxCollider>(); // Changed from BoxCollider2D to BoxCollider
        if (col == null) col = gameObject.AddComponent<BoxCollider>(); // Changed from BoxCollider2D to BoxCollider
        if (!col.isTrigger) col.isTrigger = true;
    }

    private void Start()
    {
       // If using 3D primitives (like Capsules), they might already have a renderer we can reuse or check
    }


    [Header("Node Configuration")]
    // Replaced flat list with Batches
    [UnityEngine.Serialization.FormerlySerializedAs("batches")]
    [SerializeField] private List<CharacterBatch> _batches = new List<CharacterBatch>();
    public List<CharacterBatch> Batches => _batches;
    
    public enum QueueDirection { Vertical, Horizontal } // Vertical = Up (Stack Down), Horizontal = Left (Stack Right)? 
    
    [Header("Grid Layout")]
    [UnityEngine.Serialization.FormerlySerializedAs("queueDirection")]
    [SerializeField] private QueueDirection _queueDirection = QueueDirection.Vertical;
    public QueueDirection Direction => _queueDirection;

    [UnityEngine.Serialization.FormerlySerializedAs("cols")]
    [SerializeField] private int _cols = 2; // Width of the batch (how many columns wide)
    public int Cols => _cols;
    
    [UnityEngine.Serialization.FormerlySerializedAs("batchSpacing")]
    [SerializeField] private float _batchSpacing = 4.0f; // Gap between batches
    public float BatchSpacing => _batchSpacing;

    [UnityEngine.Serialization.FormerlySerializedAs("spacingX")]
    [SerializeField] private float _spacingX = 0.8f;
    public float SpacingX => _spacingX;

    [UnityEngine.Serialization.FormerlySerializedAs("spacingZ")]
    [SerializeField] private float _spacingZ = 0.8f; // Changed from spacingY
    public float SpacingZ => _spacingZ;

    [UnityEngine.Serialization.FormerlySerializedAs("nextNodes")]
    [SerializeField] private Node[] _nextNodes; 
    public Node[] NextNodes => _nextNodes; 

    [UnityEngine.Serialization.FormerlySerializedAs("currentPathIndex")]
    [SerializeField] private int _currentPathIndex = 0;
    public int CurrentPathIndex => _currentPathIndex;

    [Header("Visuals")]
    // characterSlots is now legacy or used for a single batch if needed, 
    // but the batch system generates its own slots. 
    [UnityEngine.Serialization.FormerlySerializedAs("characterSlots")]
    [SerializeField] private Transform[] _characterSlots; 
    public Transform[] CharacterSlots => _characterSlots;
    
    [Header("Debugging")]
    [UnityEngine.Serialization.FormerlySerializedAs("testCharacterPrefab")]
    [SerializeField] private GameObject _testCharacterPrefab; // User requested prefab reference
    public GameObject TestCharacterPrefab => _testCharacterPrefab;

    [UnityEngine.Serialization.FormerlySerializedAs("stopPoint")]
    [SerializeField] private Transform _stopPoint; // Where the bus should actually stop
    public Transform StopPoint => _stopPoint;

    [System.Serializable]
    public struct BatchSetup
    {
        public CharacterColor color;
        public int count;
    }

    [Header("Level Configuration")]
    [UnityEngine.Serialization.FormerlySerializedAs("batchConfig")]
    [SerializeField] private List<BatchSetup> _batchConfig = new List<BatchSetup>(); // Define the sequence here
    public List<BatchSetup> BatchConfig => _batchConfig;

    // Switch Mechanic
    public void TogglePath()
    {
        if (_nextNodes.Length > 1)
        {
            _currentPathIndex = (_currentPathIndex + 1) % _nextNodes.Length;
            UpdateSwitchVisuals();
        }
    }

    private void UpdateSwitchVisuals()
    {
        // TODO: Rotate arrow or change color to indicate direction
    }

    public Node GetNextNode()
    {
        if (_nextNodes == null || _nextNodes.Length == 0) return null;
        return _nextNodes[_currentPathIndex];
    }
    
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, 1.5f); // Matches Bus detection radius
        
        // Visual connection to batches
        if(_batches.Count > 0 && _batches[0].BatchRoot != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(transform.position, _batches[0].BatchRoot.position);
        }
        
        // Stop Point Visual
        if (_stopPoint != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(_stopPoint.position, 0.5f);
            Gizmos.DrawLine(transform.position, _stopPoint.position);
        }
    }
    
    // --- BATCH INTERFACE ---

    public bool HasCharacter(CharacterColor color)
    {
        // Only check the FRONT batch
        if (_batches.Count > 0)
        {
            var frontBatch = _batches[0];
            if (!frontBatch.IsEmpty && frontBatch.BatchColor == color)
            {
                return true;
            }
        }
        return false;
    }

    public Character GetNextCharacter(CharacterColor color)
    {
        if (_batches.Count > 0)
        {
            var frontBatch = _batches[0];
            if (!frontBatch.IsEmpty && frontBatch.BatchColor == color)
            {
                // Return first char and remove it
            Character c = frontBatch.CharList[0];
            
            // Critical Fix: Unparent so it doesn't get destroyed if Batch object is destroyed
            c.transform.SetParent(null); 
            
            RemoveCharacter(c);
            return c;
            }
        }
        return null;
    }

    public void RemoveCharacter(Character character)
    {
        // We assume it's in the front batch if we just asked for it
        if (_batches.Count > 0)
        {
            var frontBatch = _batches[0];
            frontBatch.RemoveCharacter(character);
            
            // Check if batch is done
            if (frontBatch.IsEmpty)
            {
                 RemoveFrontBatch();
            }
        }
    }
    
    private void RemoveFrontBatch()
    {
        if (_batches.Count == 0) return;
        
        CharacterBatch oldBatch = _batches[0];
        _batches.RemoveAt(0);
        
        // Cleanup visuals for old batch root?
        if (oldBatch.BatchRoot != null)
        {
            Destroy(oldBatch.BatchRoot.gameObject);
        }
        
        // Move remaining batches forward
        UpdateBatchPositions();
    }
    
    // Simplified UpdateBatchPositions - We need to recalculate? 
    // Actually, UpdateBatchPositions was based on index * fixed size.
    // If we want runtime updates (removing batches), we need to shift them back.
    // This is tricky if sizes vary. We need to know the size of EACH batch to know where it sits.
    private void UpdateBatchPositions()
    {
        float currentOffset = 0f;
        for (int i = 0; i < _batches.Count; i++)
        {
            CharacterBatch batch = _batches[i];
            
            // Calculate size of this batch
            int count = batch.CharList.Count; 
            if(count == 0) count = 1; // avoid infinite collapse?
            // Note: CharList might diminish as they enter bus? 
            // Ideally we track "Initial Count" or "Rows Occupied".
            // Since we destroy the BatchObj when empty, it's fine.
            // But wait, if chars leave, count drops. Do we want the batch to shrink visually? 
            // Usually no, the "container" size stays.
            // For now, let's assume size stays fixed until batch is gone?
            // Actually, we can just grab the existing localPosition of slots to guess size? Hard.
            
            // BETTER: Since we removed _rows field, we must rely on CharList count to estimate size OR store it in Batch.
            // Let's assume we stack them based on current count.
            int rowsNeeded = Mathf.CeilToInt((float)count / _cols);
            
            Vector3 targetPos;
             if (_queueDirection == QueueDirection.Vertical) 
                targetPos = new Vector3(0, 0, currentOffset);
            else 
                targetPos = new Vector3(-currentOffset, 0, 0);

            if (batch.BatchRoot != null)
            {
                // batch.BatchRoot.localPosition = targetPos; // Old Snap
                StartCoroutine(MoveBatchTo(batch.BatchRoot, targetPos)); // New Smooth Slide
            }
            
            float usedSpace = rowsNeeded * ((_queueDirection == QueueDirection.Vertical) ? _spacingZ : _spacingX);
            currentOffset += usedSpace + _batchSpacing;
        }
    }
    
    private IEnumerator MoveBatchTo(Transform batchRoot, Vector3 localTarget)
    {
        float duration = 0.3f;
        float elapsed = 0f;
        Vector3 startPos = batchRoot.localPosition;
        
        while (elapsed < duration)
        {
            if (batchRoot == null) yield break;
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            batchRoot.localPosition = Vector3.Lerp(startPos, localTarget, t);
            yield return null;
        }
        if(batchRoot != null) batchRoot.localPosition = localTarget;
    }
    
    // --- GENERATION TOOLS ---

    [ContextMenu("Generate Grid Batches")]
    public void GenerateGridBatches()
    {
        _batches.Clear();
        
        // Cleanup old children
        List<GameObject> childrenToDestroy = new List<GameObject>();
        for(int i=0; i<transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            if(child.name.StartsWith("Batch_")) childrenToDestroy.Add(child.gameObject);
        }
        foreach(var g in childrenToDestroy) DestroyImmediate(g);
        
        // Use Config if available, else Default
        if (_batchConfig != null && _batchConfig.Count > 0)
        {
            float currentOffset = 0f;

            for (int i = 0; i < _batchConfig.Count; i++)
            {
                currentOffset = CreateBatchDynamic(i, _batchConfig[i], currentOffset);
            }
        }
        else
        {
            // Default Test
            float currentOffset = 0f;
            currentOffset = CreateBatchDynamic(0, new BatchSetup { color = CharacterColor.Red, count = 6 }, currentOffset);
            currentOffset = CreateBatchDynamic(1, new BatchSetup { color = CharacterColor.Green, count = 4 }, currentOffset);
            currentOffset = CreateBatchDynamic(2, new BatchSetup { color = CharacterColor.Blue, count = 5 }, currentOffset);
        }
    }
    
    private float CreateBatchDynamic(int index, BatchSetup setup, float startOffset)
    {
        CharacterBatch batch = new CharacterBatch();
        batch.BatchColor = setup.color;
        
        GameObject batchObj = new GameObject($"Batch_{index}_{setup.color}");
        batchObj.transform.SetParent(transform);
        
        // Position Root based on Start Offset
        Vector3 batchRootPos = Vector3.zero;
        if (_queueDirection == QueueDirection.Vertical) 
            batchRootPos = new Vector3(0, 0, startOffset); // Z-Axis
        else 
            batchRootPos = new Vector3(-startOffset, 0, 0); // X-Axis (Inverted)

        batchObj.transform.localPosition = batchRootPos;
        batchObj.transform.localRotation = Quaternion.identity;
        
        batch.BatchRoot = batchObj.transform;
        
        // Calculate Rows needed
        int count = setup.count > 0 ? setup.count : 4; // Default safety
        int rowsNeeded = Mathf.CeilToInt((float)count / _cols);
        
        for (int i = 0; i < count; i++)
        {
            int r = i / _cols; // Row index
            int c = i % _cols; // Col index
            
            GameObject slot = new GameObject($"Slot_{r}_{c}");
            slot.transform.SetParent(batchObj.transform);
            
            Vector3 slotPos = Vector3.zero;
            
            if (_queueDirection == QueueDirection.Vertical)
            {
                // Vertical Queue: Fills +Z
                float xPos = (c - (_cols - 1) * 0.5f) * _spacingX;
                float zPos = r * _spacingZ; 
                slotPos = new Vector3(xPos, 0, zPos);
            }
            else
            {
                // Horizontal Queue: Fills -X
                float xPos = -(r * _spacingX); 
                float zPos = (c - (_cols - 1) * 0.5f) * _spacingZ;
                slotPos = new Vector3(xPos, 0, zPos);
            }
            
            slot.transform.localPosition = slotPos;
            slot.transform.localRotation = Quaternion.identity;
            
            Character newChar = SpawnCharacterAt(slot.transform, setup.color);
            batch.CharList.Add(newChar);
        }
        
        _batches.Add(batch);
        
        // Return next offset
        float usedSpace = (rowsNeeded * ((_queueDirection == QueueDirection.Vertical) ? _spacingZ : _spacingX));
        // Add Batch Spacing
        // Note: For horizontal using SpacingX is safer contextually, but SpacingX is declared as 'width' between cols usually?
        // Wait, _spacingX is typically lateral spacing. But in Horizontal mode, Rows EXTEND along X. So we use spacingX for row separation?
        // Let's re-read line 304 in previous code: `float xPos = (r * _spacingX);` -> Yes, X spacing handles row depth in horizontal.
        
        return startOffset + usedSpace + _batchSpacing;
    }
    
    private Character SpawnCharacterAt(Transform slot, CharacterColor cType)
    {
        GameObject charObj;
        
        if (_testCharacterPrefab != null)
        {
            charObj = (GameObject)Instantiate(_testCharacterPrefab, slot.position, slot.rotation, slot);
        }
        else
        {
            // Fallback to primitive if no prefab assigned
            charObj = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            if(charObj.GetComponent<Collider>()) DestroyImmediate(charObj.GetComponent<Collider>());
            charObj.transform.localScale = Vector3.one * 0.4f;
            charObj.transform.SetParent(slot);
            charObj.transform.localPosition = Vector3.zero;
            charObj.transform.localRotation = Quaternion.identity;
        }
        
        charObj.name = "Character_Visual";
        
        // Ensure Script
        Character c = charObj.GetComponent<Character>();
        if (c == null) c = charObj.AddComponent<Character>();
        
        // Remove direct Renderer access, Character handles it in SetColor
        c.SetColor(cType); 
        
        return c;
    }

    void OnMouseDown()
    {
        TogglePath();
    }
}
