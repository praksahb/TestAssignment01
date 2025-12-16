using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameManager : GenericSingleton<GameManager>
{
    [SerializeField] private CharacterColor[] debugSpawnSequence; // Just for reference if needed

    private void Start()
    {
        // Manager initialization if any
    }

    public void CheckLevelStatus()
    {
        // Check if any characters remain
        Character[] allChars = FindObjectsByType<Character>(FindObjectsSortMode.None);
        
        // We need to check if Spawner is done too.
        bool spawnerActive = false;
        if(BusSpawner.Instance != null && BusSpawner.Instance.HasBusesPending)
        {
            spawnerActive = true;
        }
        
        Bus[] activeBuses = FindObjectsByType<Bus>(FindObjectsSortMode.None);

        if (allChars.Length == 0 && !spawnerActive && activeBuses.Length == 0)
        {
            Debug.Log("LEVEL COMPLETE!");
            if (UIManager.Instance != null) UIManager.Instance.ShowLevelComplete();
        }
        else if (!spawnerActive && activeBuses.Length == 0)
        {
            // No buses left, but people remain
            Debug.Log("LEVEL FAILED: People stranded!");
            if (UIManager.Instance != null) UIManager.Instance.ShowLevelFailed();
        }
    }
}
