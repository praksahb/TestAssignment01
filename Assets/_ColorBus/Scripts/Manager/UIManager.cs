using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIManager : GenericSingleton<UIManager>
{
    [Header("Panels")]
    [UnityEngine.Serialization.FormerlySerializedAs("levelCompletePanel")]
    [SerializeField] private GameObject _levelCompletePanel;
    
    [UnityEngine.Serialization.FormerlySerializedAs("levelFailedPanel")]
    [SerializeField] private GameObject _levelFailedPanel;
    
    [Header("HUD")]
    [UnityEngine.Serialization.FormerlySerializedAs("activeBusCountText")]
    [SerializeField] private TextMeshProUGUI _activeBusCountText; // Assign in Inspector

    [Header("Buttons")]
    [UnityEngine.Serialization.FormerlySerializedAs("nextLevelButton")]
    [SerializeField] private Button _nextLevelButton;
    
    [UnityEngine.Serialization.FormerlySerializedAs("retryButton")]
    [SerializeField] private Button _retryButton;

    private void Start()
    {
        // Ensure panels are hidden at start
        if (_levelCompletePanel != null) _levelCompletePanel.SetActive(false);
        if (_levelFailedPanel != null) _levelFailedPanel.SetActive(false);

        // Hook up buttons
        if (_nextLevelButton != null) _nextLevelButton.onClick.AddListener(OnNextLevelClicked);
        if (_retryButton != null) _retryButton.onClick.AddListener(OnRetryClicked);
    }
    
    public void UpdateActiveBusUI(int current, int max)
    {
        if (_activeBusCountText != null)
        {
            _activeBusCountText.text = $"{current}/{max}";
        }
    }

    public void ShowLevelComplete()
    {
        Debug.Log("UI: Showing Level Complete");
        if (_levelCompletePanel != null) _levelCompletePanel.SetActive(true);
    }

    public void ShowLevelFailed()
    {
        Debug.Log("UI: Showing Level Failed");
        if (_levelFailedPanel != null) _levelFailedPanel.SetActive(true);
    }

    private void OnNextLevelClicked()
    {
        Debug.Log("Next Level Clicked (Logic to be implemented in SceneLoader)");
        // SceneManager.LoadScene(nextSceneIndex);
    }

    private void OnRetryClicked()
    {
         Debug.Log("Retry Clicked");
         // SceneManager.LoadScene(currentScene);
    }
}
