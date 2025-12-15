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
    public List<CharacterBatch> batches = new List<CharacterBatch>();
    
    public enum QueueDirection { Vertical, Horizontal } // Vertical = Up (Stack Down), Horizontal = Left (Stack Right)? 
    
    [Header("Grid Layout")]
    public QueueDirection queueDirection = QueueDirection.Vertical;
    public int rows = 5; // Depth of the batch (how many char rows per batch)
    public int cols = 2; // Width of the batch (how many columns wide)
    
    public float batchSpacing = 4.0f; // Gap between batches
    public float spacingX = 0.8f;
    public float spacingZ = 0.8f; // Changed from spacingY

    public Node[] nextNodes; 
    public int currentPathIndex = 0;

    [Header("Visuals")]
    // characterSlots is now legacy or used for a single batch if needed, 
    // but the batch system generates its own slots. 
    public Transform[] characterSlots; 
    
    [Header("Debugging")]
    public GameObject testCharacterPrefab; // User requested prefab reference
    public Transform stopPoint; // Where the bus should actually stop

    [Header("Level Configuration")]
    public List<CharacterColor> batchConfig = new List<CharacterColor>(); // Define the sequence here

    // Switch Mechanic
    public void TogglePath()
    {
        if (nextNodes.Length > 1)
        {
            currentPathIndex = (currentPathIndex + 1) % nextNodes.Length;
            UpdateSwitchVisuals();
        }
    }

    private void UpdateSwitchVisuals()
    {
        // TODO: Rotate arrow or change color to indicate direction
    }

    public Node GetNextNode()
    {
        if (nextNodes == null || nextNodes.Length == 0) return null;
        return nextNodes[currentPathIndex];
    }
    
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, 1.5f); // Matches Bus detection radius
        
        // Visual connection to batches
        if(batches.Count > 0 && batches[0].batchRoot != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(transform.position, batches[0].batchRoot.position);
        }
        
        // Stop Point Visual
        if (stopPoint != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(stopPoint.position, 0.5f);
            Gizmos.DrawLine(transform.position, stopPoint.position);
        }
    }
    
    // --- BATCH INTERFACE ---

    public bool HasCharacter(CharacterColor color)
    {
        // Only check the FRONT batch
        if (batches.Count > 0)
        {
            var frontBatch = batches[0];
            if (!frontBatch.IsEmpty && frontBatch.color == color)
            {
                return true;
            }
        }
        return false;
    }

    public Character GetNextCharacter(CharacterColor color)
    {
        if (batches.Count > 0)
        {
            var frontBatch = batches[0];
            if (!frontBatch.IsEmpty && frontBatch.color == color)
            {
                // Return first char and remove it
                Character c = frontBatch.charList[0];
                RemoveCharacter(c);
                return c;
            }
        }
        return null;
    }

    public void RemoveCharacter(Character character)
    {
        // We assume it's in the front batch if we just asked for it
        if (batches.Count > 0)
        {
            var frontBatch = batches[0];
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
        if (batches.Count == 0) return;
        
        CharacterBatch oldBatch = batches[0];
        batches.RemoveAt(0);
        
        // Cleanup visuals for old batch root?
        if (oldBatch.batchRoot != null)
        {
            Destroy(oldBatch.batchRoot.gameObject);
        }
        
        // Move remaining batches forward
        UpdateBatchPositions();
    }
    
    private void UpdateBatchPositions()
    {
        for (int i = 0; i < batches.Count; i++)
        {
            CharacterBatch batch = batches[i];
            Vector3 targetPos = GetBatchPosition(i);
            
            if (batch.batchRoot != null)
            {
                batch.batchRoot.localPosition = targetPos; 
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
        
        float offset = index * batchSpacing;
        
        switch (queueDirection)
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
        batches.Clear();
        
        // Cleanup old children
        List<GameObject> childrenToDestroy = new List<GameObject>();
        for(int i=0; i<transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            if(child.name.StartsWith("Batch_")) childrenToDestroy.Add(child.gameObject);
        }
        foreach(var g in childrenToDestroy) DestroyImmediate(g);
        
        // Use Config if available, else Default
        if (batchConfig != null && batchConfig.Count > 0)
        {
            for (int i = 0; i < batchConfig.Count; i++)
            {
                CreateBatch(i, batchConfig[i]);
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
        batch.color = cColor;
        
        GameObject batchObj = new GameObject($"Batch_{index}_{cColor}");
        batchObj.transform.SetParent(transform);
        batchObj.transform.localPosition = GetBatchPosition(index);
        batchObj.transform.localRotation = Quaternion.identity;
        
        batch.batchRoot = batchObj.transform;
        
        // Create Slots
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                GameObject slot = new GameObject($"Slot_{r}_{c}");
                slot.transform.SetParent(batchObj.transform);
                
                Vector3 slotPos = Vector3.zero;
                
                if (queueDirection == QueueDirection.Vertical)
                {
                    // Vertical Queue (Flows Up):
                    // Rows go Backwards (-Y) -> Now (-Z)
                    // Cols go Sideways (X) centered
                    float xPos = (c - (cols - 1) * 0.5f) * spacingX;
                    float zPos = -(r * spacingZ); 
                    slotPos = new Vector3(xPos, 0, zPos);
                }
                else
                {
                    // Horizontal Queue (Flows Left):
                    float xPos = (r * spacingX); // Extending Right
                    float zPos = (c - (cols - 1) * 0.5f) * spacingZ;
                    slotPos = new Vector3(xPos, 0, zPos);
                }
                
                slot.transform.localPosition = slotPos;
                slot.transform.localRotation = Quaternion.identity;
                
                // Spawn
                Character newChar = SpawnCharacterAt(slot.transform, cColor);
                batch.charList.Add(newChar);
            }
        }
        
        batches.Add(batch);
    }
    
    private Character SpawnCharacterAt(Transform slot, CharacterColor cType)
    {
        GameObject charObj;
        
        if (testCharacterPrefab != null)
        {
            charObj = (GameObject)Instantiate(testCharacterPrefab, slot.position, slot.rotation, slot);
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
