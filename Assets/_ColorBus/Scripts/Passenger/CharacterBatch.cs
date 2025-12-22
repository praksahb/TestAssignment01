using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class CharacterBatch
{

    [SerializeField] private CharacterColor _batchColor;
    public CharacterColor BatchColor { get => _batchColor; set => _batchColor = value; }


    [SerializeField] private List<Character> _charList = new List<Character>();
    public List<Character> CharList => _charList;


    [SerializeField] private Transform _batchRoot; // Parent for this batch's slots
    public Transform BatchRoot { get => _batchRoot; set => _batchRoot = value; }

    public bool IsEmpty => _charList == null || _charList.Count == 0;

    public void RemoveCharacter(Character c)
    {
        if (_charList.Contains(c))
        {
            _charList.Remove(c);
        }
    }
}
