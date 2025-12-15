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
    
    public NodeSetup[] nodes;
    public CharacterColor[] busSequence;
}
