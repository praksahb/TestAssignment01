using UnityEngine;

[CreateAssetMenu(fileName = "Level4", menuName = "Levels/LevelData")]
public class LevelData : ScriptableObject
{
    // We might not use this if we hardcode the scene for the prototype
    // But good practice.
    
    [System.Serializable]
    public struct NodeSetup
    {
        public int nodeId; // Maps to hierarchy index maybe?
        public CharacterColor[] characters;
    }
    

    [SerializeField] private NodeSetup[] _nodes;
    public NodeSetup[] Nodes => _nodes;


    [SerializeField] private CharacterColor[] _busSequence;
    public CharacterColor[] BusSequence => _busSequence;
    
    [Header("Level Info")]

    [SerializeField] private int _levelNumber;
    public int LevelNumber => _levelNumber;
}
