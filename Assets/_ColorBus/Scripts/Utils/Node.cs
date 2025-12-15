using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
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

    [UnityEngine.Serialization.FormerlySerializedAs("rows")]
    [SerializeField] private int _rows = 5; // Depth of the batch (how many char rows per batch)
    public int Rows => _rows;

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

    [Header("Level Configuration")]
    [UnityEngine.Serialization.FormerlySerializedAs("batchConfig")]
    [SerializeField] private List<CharacterColor> _batchConfig = new List<CharacterColor>(); // Define the sequence here
    public List<CharacterColor> BatchConfig => _batchConfig;

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
    
    private void UpdateBatchPositions()
    {
        for (int i = 0; i < _batches.Count; i++)
        {
            CharacterBatch batch = _batches[i];
            Vector3 targetPos = GetBatchPosition(i);
            
            if (batch.BatchRoot != null)
            {
                batch.BatchRoot.localPosition = targetPos; 
            }
        }
    }
    
    private Vector3 GetBatchPosition(int index)
    {
        // Batch 0 is at (0,0,0).
        // Subsequent batches are "Behind" based on direction.
        
        // Assumption: 
        // Vertical Queue flows UP -> Batches stack DOWN (-Y).
        // Horizontal Queue flows LEFT -> Batches stack RIGHT (+X) or Left? 
        // Looking at the image, the horizontal queue flows LEFT (into the bus). So batches accept from Right. 
        // So batches stack to the Right (+X).
        
        float offset = index * _batchSpacing;
        
        switch (_queueDirection)
        {
            case QueueDirection.Vertical:
                return new Vector3(0, -offset, 0);
            case QueueDirection.Horizontal:
                return new Vector3(offset, 0, 0); // Stacking to Right
            default:
                return new Vector3(0, -offset, 0);
        }
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
            for (int i = 0; i < _batchConfig.Count; i++)
            {
                CreateBatch(i, _batchConfig[i]);
            }
        }
        else
        {
            // Default Test
            CreateBatch(0, CharacterColor.Red);
            CreateBatch(1, CharacterColor.Green);
            CreateBatch(2, CharacterColor.Blue);
        }
    }
    
    private void CreateBatch(int index, CharacterColor cColor)
    {
        CharacterBatch batch = new CharacterBatch();
        batch.BatchColor = cColor;
        
        GameObject batchObj = new GameObject($"Batch_{index}_{cColor}");
        batchObj.transform.SetParent(transform);
        batchObj.transform.localPosition = GetBatchPosition(index);
        batchObj.transform.localRotation = Quaternion.identity;
        
        batch.BatchRoot = batchObj.transform;
        
        // Create Slots
        for (int r = 0; r < _rows; r++)
        {
            for (int c = 0; c < _cols; c++)
            {
                GameObject slot = new GameObject($"Slot_{r}_{c}");
                slot.transform.SetParent(batchObj.transform);
                
                Vector3 slotPos = Vector3.zero;
                
                if (_queueDirection == QueueDirection.Vertical)
                {
                    // Vertical Queue (Flows Up):
                    // Rows go Backwards (-Y) -> Now (-Z)
                    // Cols go Sideways (X) centered
                    float xPos = (c - (_cols - 1) * 0.5f) * _spacingX;
                    float zPos = -(r * _spacingZ); 
                    slotPos = new Vector3(xPos, 0, zPos);
                }
                else
                {
                    // Horizontal Queue (Flows Left):
                    float xPos = (r * _spacingX); // Extending Right
                    float zPos = (c - (_cols - 1) * 0.5f) * _spacingZ;
                    slotPos = new Vector3(xPos, 0, zPos);
                }
                
                slot.transform.localPosition = slotPos;
                slot.transform.localRotation = Quaternion.identity;
                
                // Spawn
                Character newChar = SpawnCharacterAt(slot.transform, cColor);
                batch.CharList.Add(newChar);
            }
        }
        
        _batches.Add(batch);
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
