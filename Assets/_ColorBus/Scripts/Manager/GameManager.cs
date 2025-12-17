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
        StartCoroutine(CheckLevelStatusRoutine());
    }

    private IEnumerator CheckLevelStatusRoutine()
    {
        // specific delay to allow things to settle (e.g. bus destruction)
        yield return new WaitForSeconds(1.0f);
        
        // Check if any characters remain
        Character[] allChars = FindObjectsByType<Character>(FindObjectsSortMode.None);
        
        // We need to check if Spawner is done too.
        bool spawnerActive = false;
        if(BusSpawner.Instance != null && BusSpawner.Instance.HasBusesPending)
        {
            spawnerActive = true;
        }
        
        // Also check if any buses are on the path (using Spawner's reliable count first, but physical check is safer for "Empty" state)
        Bus[] activeBuses = FindObjectsByType<Bus>(FindObjectsSortMode.None);
        // We want to verify no buses are *Active/Moving*.
        
        bool anyBusActive = activeBuses.Length > 0;
        
        // WIN CONDITION: No Chars, No Pending Buses, No Active Buses
        if (allChars.Length == 0 && !spawnerActive && !anyBusActive)
        {
            Debug.Log("LEVEL COMPLETE!");
            if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX(SoundType.LevelComplete);
            if (UIManager.Instance != null) UIManager.Instance.ShowLevelComplete();
        }
        // FAIL CONDITION: No Pending Buses, No Active Buses, But Chars REMAIN
        else if (!spawnerActive && !anyBusActive && allChars.Length > 0)
        {
            Debug.Log("LEVEL FAILED: People stranded!");
            if (UIManager.Instance != null) UIManager.Instance.ShowLevelFailed();
        }
    }
}
