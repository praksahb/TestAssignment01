using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ColorMapping", menuName = "ScriptableObjects/ColorMapping", order = 1)]
public class ColorMappingSO : ScriptableObject
{
    [System.Serializable]
    public struct ColorEntry
    {
        public CharacterColor type;
        public Color color;
    }

    [SerializeField] private List<ColorEntry> _mappings;
    
    // Quick lookup for runtime
    private Dictionary<CharacterColor, Color> _lookup;

    private void OnEnable()
    {
        BuildLookup();
    }

    private void BuildLookup()
    {
        _lookup = new Dictionary<CharacterColor, Color>();
        if (_mappings != null)
        {
            foreach (var entry in _mappings)
            {
                if (!_lookup.ContainsKey(entry.type))
                {
                    _lookup.Add(entry.type, entry.color);
                }
            }
        }
    }

    public Color GetColor(CharacterColor type)
    {
        if (_lookup == null) BuildLookup();

        if (_lookup.TryGetValue(type, out Color c))
        {
            return c;
        }
        return Color.white; // Fallback
    }
}
