using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

public class UIManager : GenericSingleton<UIManager>
{
    [Header("HUD")]

    [SerializeField] private TextMeshProUGUI _activeBusCountText;


    [SerializeField] private TextMeshProUGUI _levelText;

    [Header("Level Data")]

    [SerializeField] private LevelData _levelData; // Optional override

    [Header("Panels")]

    [SerializeField] private GameObject _levelCompletePanel;


    [SerializeField] private GameObject _levelFailedPanel;

    [Header("Buttons")]

    [SerializeField] private Button _nextLevelButton;
    

    [SerializeField] private Button _retryButton;

    private void Start()
    {
        // Ensure panels are hidden at start
        if (_levelCompletePanel != null) _levelCompletePanel.SetActive(false);
        if (_levelFailedPanel != null) _levelFailedPanel.SetActive(false);

        // Hook up buttons
        if (_nextLevelButton != null) _nextLevelButton.onClick.AddListener(OnNextLevelClicked);
        if (_retryButton != null) _retryButton.onClick.AddListener(OnRetryClicked);

        UpdateLevelText();
    }

    private void UpdateLevelText()
    {
        if (_levelText != null)
        {
            int levelNum = SceneManager.GetActiveScene().buildIndex;
            if (_levelData != null)
            {
                levelNum = _levelData.LevelNumber;
            }
            _levelText.SetText(levelNum.ToString());
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
    
    public void UpdateBusCount(int current, int max)
    {
        if (_activeBusCountText != null)
        {
            _activeBusCountText.text = $"{current} / {max}";
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
