using UnityEngine;

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
    public Renderer visualRenderer;

    private static MaterialPropertyBlock _propBlock;

    private void OnEnable()
    {
        SetColor(color);
    }

    private void OnValidate()
    {
        SetColor(color);
    }

    public void SetColor(CharacterColor newColor)
    {
        color = newColor;
        if (visualRenderer != null)
        {
            Color c = Color.white;
            switch (color)
            {
                case CharacterColor.Red:
                    c = Color.red;
                    break;
                case CharacterColor.Green:
                    c = Color.green;
                    break;
                case CharacterColor.Blue:
                    c = Color.blue;
                    break;
                case CharacterColor.Yellow:
                    c = Color.yellow;
                    break;
            }
            
            // Handle SpriteRenderer directly (simpler, no material leak usually as it uses vertex color)
            if (visualRenderer is SpriteRenderer sr)
            {
                sr.color = c;
            }
            else
            {
                // For MeshRenderer, use PropertyBlock to avoid creating Material instances in Editor
                if (_propBlock == null) _propBlock = new MaterialPropertyBlock();
                
                visualRenderer.GetPropertyBlock(_propBlock);
                _propBlock.SetColor("_Color", c); // Standard shader property
                visualRenderer.SetPropertyBlock(_propBlock);
            }
        }
    }
}
