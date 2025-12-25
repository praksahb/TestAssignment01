using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public enum CharacterColor
{
    Red,
    Green,
    Blue,
    Yellow
}

public class Character : MonoBehaviour
{
    [Header("Settings")]

    [SerializeField] private CharacterColor _characterColor;
    public CharacterColor CharacterColor { get => _characterColor; set => _characterColor = value; }


    [SerializeField] private float _speed = 5.0f;
    public float Speed => _speed;

    [Header("Jump Settings")]
    [SerializeField] private float _jumpHeight = 0.3f;
    [SerializeField] private float _jumpSpeedMultiplier = 1.75f;

    private Renderer _myRenderer;

    private void Awake()
    {
        _myRenderer = GetComponentInChildren<Renderer>();
    }

    private void OnEnable()
    {
        // OnEnable is tricky if we rely on injection. 
        // We probably shouldn't set color here unless we have the mapping.
        // Or we rely on "SetColor" being called by the spawner/node.
    }

    private void OnValidate()
    {
        // Cannot validate color mapping without reference
    }

    private MaterialPropertyBlock _propBlock;

    public void SetColor(CharacterColor c, ColorMappingSO mapping = null)
    {
        _characterColor = c;
        // visual update
        if (_myRenderer == null) _myRenderer = GetComponentInChildren<Renderer>();
        if (_myRenderer != null)
        {
            if (_propBlock == null) _propBlock = new MaterialPropertyBlock();
            
            _myRenderer.GetPropertyBlock(_propBlock);
            
            Color targetColor = Color.white;
            if (mapping != null)
            {
                targetColor = mapping.GetColor(c);
            }
            else
            {
               // Fallback or maintain existing if just changing enum?
               // If mapping is null, we can't do much unless we have a static default.
            }
            
            if(mapping != null) 
            {
                 _propBlock.SetColor("_BaseColor", targetColor);
                 _myRenderer.SetPropertyBlock(_propBlock);
            }
        }
    }

    public void MoveToBus(Transform targetSeat, Bus bus)
    {
        StartCoroutine(MoveToBusRoutine(targetSeat, bus));
    }
    
    private IEnumerator MoveToBusRoutine(Transform targetSeat, Bus bus)
    {
        // 1. Jump Parabola to Seat
        Vector3 startPos = transform.position;
        // Dynamic duration to match original movement speed
        float dist = Vector3.Distance(startPos, targetSeat.position);
        
        // Speed up the boarding significantly to make it snappy
        float effectiveSpeed = _speed * _jumpSpeedMultiplier; 
        float duration = dist / effectiveSpeed; 
        
        // Minimal safety floor
        if (duration < 0.05f) duration = 0.05f;

        float elapsed = 0f;
        // float jumpHeight = 0.3f; // Use _jumpHeight field

        while (elapsed < duration)
        {
            if (targetSeat == null) yield break; // Safety check

            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            
            // Linear progress
            Vector3 linearPos = Vector3.Lerp(startPos, targetSeat.position, t);
            
            // Add Arc (Sin wave 0->1->0)
            float height = Mathf.Sin(t * Mathf.PI) * _jumpHeight;
            
            transform.position = linearPos + Vector3.up * height;
            
            // Optional: Rotate towards bus?
            transform.LookAt(targetSeat);
            
            yield return null;
        }
        
        if (targetSeat != null)
        {
            transform.position = targetSeat.position;
            transform.SetParent(targetSeat);
            transform.localRotation = Quaternion.identity; // Align with seat
        }
    }
}
