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
    public CharacterColor color;
    public float speed = 5.0f;
    private Renderer myRenderer;

    private void Awake()
    {
        myRenderer = GetComponentInChildren<Renderer>();
    }

    private void OnEnable()
    {
        SetColor(color);
    }

    private void OnValidate()
    {
        SetColor(color);
    }

    public void SetColor(CharacterColor c)
    {
        this.color = c;
        // visual update
        if (myRenderer != null)
        {
            // Assuming the shader has a standard Color property (BaseColor or _Color)
            // For standard material:
            myRenderer.material.color = GetColorFromEnum(c);
        }
    }
    
    private Color GetColorFromEnum(CharacterColor c)
    {
        switch (c)
        {
            case CharacterColor.Red: return Color.red;
            case CharacterColor.Green: return Color.green;
            case CharacterColor.Blue: return Color.blue;
            case CharacterColor.Yellow: return Color.yellow;
        }
        return Color.white;
    }

    public void MoveToBus(Transform targetSeat, Bus bus)
    {
        StartCoroutine(MoveToBusRoutine(targetSeat, bus));
    }
    
    private IEnumerator MoveToBusRoutine(Transform targetSeat, Bus bus)
    {
        // 1. Move to Entry point (optional, but good for visuals)
        // 2. Move to Seat
        
        while (Vector3.Distance(transform.position, targetSeat.position) > 0.1f)
        {
            transform.position = Vector3.MoveTowards(transform.position, targetSeat.position, speed * Time.deltaTime);
            yield return null;
        }
        
        transform.position = targetSeat.position;
        transform.SetParent(targetSeat);
        
        // Notify Bus we arrived? Or Bus handles it? 
        // Bus handles visual counting usually.
    }
}
