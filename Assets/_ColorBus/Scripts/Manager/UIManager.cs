using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

public class UIManager : GenericSingleton<UIManager>
{
    [Header("HUD")]
    public TextMeshProUGUI activeBusCountText;
    public TextMeshProUGUI levelText;

    [Header("Level Data")]
    public LevelData levelData; // Optional override

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

        UpdateLevelText();
    }

    private void UpdateLevelText()
    {
        if (levelText != null)
        {
            int levelNum = SceneManager.GetActiveScene().buildIndex;
            if (levelData != null)
            {
                levelNum = levelData.levelNumber;
            }
            levelText.SetText(levelNum.ToString());
        }
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
    
    public void UpdateBusCount(int current, int max)
    {
        if (activeBusCountText != null)
        {
            activeBusCountText.text = $"{current} / {max}";
        }
    }

    private void OnNextLevelClicked()
    {
        Debug.Log("Next Level Clicked. (Reloading current for demo)");
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private void OnRetryClicked()
    {
         Debug.Log("Retry Clicked - Restarting Level");
         SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
