using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public class GameManager : GenericSingleton<GameManager>
{
    public static event Action OnLevelCompleted;
    public static event Action OnLevelStart;


    [SerializeField] private BusSpawner _busSpawner;
    [SerializeField] private CharacterColor[] _debugSpawnSequence; // Just for reference if needed

    private void Start()
    {
        // Invoke Level Start after a frame to ensure all subscribers (like BusSpawner) have initialized their Listeners in OnEnable/Start
        StartCoroutine(LevelStartRoutine());
    }

    private IEnumerator LevelStartRoutine()
    {
        yield return null; 
        Debug.Log("GameManager: Level Started");
        OnLevelStart?.Invoke();
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
        if(_busSpawner != null && _busSpawner.HasBusesPending)
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
            OnLevelCompleted?.Invoke();
        }
        // FAIL CONDITION: No Pending Buses, No Active Buses, But Chars REMAIN
        else if (!spawnerActive && !anyBusActive && allChars.Length > 0)
        {
            Debug.Log("LEVEL FAILED: People stranded!");
            if (UIManager.Instance != null) UIManager.Instance.ShowLevelFailed();
        }
    }
}
