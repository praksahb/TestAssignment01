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




    [Header("Node Configuration")]
    // Replaced flat list with Batches

    [SerializeField] private List<CharacterBatch> _batches = new List<CharacterBatch>();
    public List<CharacterBatch> Batches => _batches;
    
    public enum QueueDirection { Vertical, Horizontal } // Vertical = Up (Stack Down), Horizontal = Left (Stack Right)? 
    
    [Header("Grid Layout")]

    [SerializeField] private QueueDirection _queueDirection = QueueDirection.Vertical;
    public QueueDirection Direction => _queueDirection;


    [SerializeField] private int _cols = 2; // Width of the batch (how many columns wide)
    public int Cols => _cols;
    

    [SerializeField] private float _batchSpacing = 4.0f; // Gap between batches
    public float BatchSpacing => _batchSpacing;


    [SerializeField] private float _spacingX = 0.8f;
    public float SpacingX => _spacingX;


    [SerializeField] private float _spacingZ = 0.8f; // Changed from spacingY
    public float SpacingZ => _spacingZ;


    [SerializeField] private Node[] _nextNodes; 
    public Node[] NextNodes => _nextNodes; 


    [SerializeField] private int _currentPathIndex = 0;
    public int CurrentPathIndex => _currentPathIndex;

    [Header("Visuals")]
    // characterSlots is now legacy or used for a single batch if needed, 
    // but the batch system generates its own slots. 

    [SerializeField] private Transform[] _characterSlots; 
    public Transform[] CharacterSlots => _characterSlots;
    
    [Header("Debugging")]

    [SerializeField] private GameObject _testCharacterPrefab; // User requested prefab reference
    public GameObject TestCharacterPrefab => _testCharacterPrefab;


    [SerializeField] private Transform _stopPoint; // Where the bus should actually stop
    public Transform StopPoint => _stopPoint;

    [System.Serializable]
    public struct BatchSetup
    {
        public CharacterColor color;
        public int count;
    }

    [Header("Level Configuration")]

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
    
    private ColorMappingSO _colorMapping;

    public void Initialize(ColorMappingSO mapping)
    {
        _colorMapping = mapping;
        
        // Propagate to existing characters
        foreach (var batch in _batches)
        {
            if (batch.CharList != null)
            {
                foreach (var character in batch.CharList)
                {
                    if (character != null)
                    {
                        character.SetColor(character.CharacterColor, _colorMapping);
                    }
                }
            }
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
            
            // Trigger update immediately so others slide forward
            UpdateCharacterPositions();

            // Check if batch is done
            if (frontBatch.IsEmpty)
            {
                 RemoveFrontBatch();
            }
        }
    }
    
    // --- ANCHOR & QUEUE SYSTEM ---

    // --- ANCHOR & QUEUE SYSTEM ---

    // Note: We no longer pre-calculate a global list because spacing depends on dynamic batch sizes.

    private void Start()
    {
        // Initial Snap
        UpdateCharacterPositions(true);
    }

    public void UpdateCharacterPositions(bool snap = false)
    {
        // Start from origin
        float currentOffset = 0f;
        int globalMoverIndex = 0;

        foreach (var batch in _batches)
        {
            if (batch.CharList == null) continue;

            // 1. Position Characters within this batch
            int charIndex = 0;
            foreach (var chara in batch.CharList)
            {
                if (chara == null) continue;

                // Calculate local slot position based on compressed index
                int r = charIndex / _cols; // Row
                int c = charIndex % _cols; // Col

                Vector3 localSlotPos = Vector3.zero;

                if (_queueDirection == QueueDirection.Vertical)
                {
                    // Vertical Queue: Fills +Z
                    // OffsetZ = currentBatchStart + (row * spacing)
                    float xPos = (c - (_cols - 1) * 0.5f) * _spacingX;
                    float zPos = currentOffset + (r * _spacingZ);
                    localSlotPos = new Vector3(xPos, 0, zPos);
                }
                else
                {
                    // Horizontal Queue: Fills -X
                    float xPos = -(currentOffset + (r * _spacingX));
                    float zPos = (c - (_cols - 1) * 0.5f) * _spacingZ;
                    localSlotPos = new Vector3(xPos, 0, zPos);
                }

                Vector3 targetWorldPos = transform.TransformPoint(localSlotPos);
                
                // Stagger logic: 0.05s per character index
                float delay = snap ? 0f : (globalMoverIndex * 0.05f);

                if (snap)
                {
                    chara.transform.position = targetWorldPos;
                    chara.transform.rotation = Quaternion.identity;
                    chara.MoveToTarget(targetWorldPos, 0f); // No delay on snap
                }
                else
                {
                    chara.MoveToTarget(targetWorldPos, delay);
                }

                charIndex++;
                globalMoverIndex++;
            }

            // 2. Calculate Gap for NEXT batch
            // Size of this batch is determined by how many rows it CURRENTLY uses
            int count = batch.CharList.Count; 
            if (count == 0) 
            {
                // If empty, it adds 0 size, just the spacing? 
                // Usually empty batches are removed, but if one lingers:
                // It should take 0 space.
            }
            else
            {
                int rowsUsed = Mathf.CeilToInt((float)count / _cols);
                float batchLength = rowsUsed * ((_queueDirection == QueueDirection.Vertical) ? _spacingZ : _spacingX);
                currentOffset += batchLength + _batchSpacing;
            }
        }
    }



    private void RemoveFrontBatch()
    {
        if (_batches.Count == 0) return;
        
        CharacterBatch oldBatch = _batches[0];
        _batches.RemoveAt(0);
        
        // Cleanup visuals for old batch root? 
        // With new system, characters might still be visually "in" the batch object hierarchy 
        // but we are controlling their world position.
        // If we destroy the Root, we destroy the characters!
        // So we should NOT destroy the root if characters are still alive?
        // Actually, RemoveFrontBatch is only called when batch.IsEmpty is true.
        // So it is safe to destroy the root.
        
        if (oldBatch.BatchRoot != null)
        {
            Destroy(oldBatch.BatchRoot.gameObject);
        }
        
        // Trigger update for everyone else
        UpdateCharacterPositions();
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
        
        // Final Snap to Anchors
        // Anchor positions are now calculated inside UpdateCharacterPositions dynamically
        UpdateCharacterPositions(true);
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
        // If _colorMapping is not yet set (e.g. at editor time gen), we pass null?
        // Or we just set the color enum and let the Char sort it out on Start using a global SO reference? 
        // No, we want to inject.
        // If this is running in Editor (ContextMenu), _colorMapping might be null.
        
        c.SetColor(cType, _colorMapping); 
        
        return c;
    }

    void OnMouseDown()
    {
        TogglePath();
    }
}
