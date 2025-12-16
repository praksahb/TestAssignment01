using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UIManager : GenericSingleton<UIManager>
{
    [Header("Panels")]
    public GameObject levelCompletePanel;
    public GameObject levelFailedPanel;

    [Header("Buttons")]
    public Button nextLevelButton;
    public Button retryButton;

    private void Start()
    {
        // Ensure panels are hidden at start
        if (levelCompletePanel != null) levelCompletePanel.SetActive(false);
        if (levelFailedPanel != null) levelFailedPanel.SetActive(false);

        // Hook up buttons
        if (nextLevelButton != null) nextLevelButton.onClick.AddListener(OnNextLevelClicked);
        if (retryButton != null) retryButton.onClick.AddListener(OnRetryClicked);
    }

    public void ShowLevelComplete()
    {
        Debug.Log("UI: Showing Level Complete");
        if (levelCompletePanel != null) levelCompletePanel.SetActive(true);
    }

    public void ShowLevelFailed()
    {
        Debug.Log("UI: Showing Level Failed");
        if (levelFailedPanel != null) levelFailedPanel.SetActive(true);
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
