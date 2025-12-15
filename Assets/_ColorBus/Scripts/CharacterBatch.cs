using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class CharacterBatch
{
    public CharacterColor color;
    public List<Character> charList = new List<Character>();
    public Transform batchRoot; // Parent for this batch's slots
    public bool IsEmpty => charList == null || charList.Count == 0;

    public void RemoveCharacter(Character c)
    {
        if (charList.Contains(c))
        {
            charList.Remove(c);
        }
    }
}
