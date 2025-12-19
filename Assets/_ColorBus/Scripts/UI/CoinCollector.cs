using UnityEngine;
using TMPro;
using System.Collections;

public class CoinCollector : MonoBehaviour
{
    [Header("UI Reference")]
    public TextMeshProUGUI coinText;
    public TextMeshProUGUI rewardText;

    [Header("Animation Settings")]
    [SerializeField] private float animationDuration = 1.0f;
    [SerializeField] private float animationDelay = 0.5f;

    private const string COIN_PREF_KEY = "TotalCoins";

    private void Start()
    {
        UpdateCoinDisplay(GetTotalCoins());
        if (rewardText != null) rewardText.gameObject.SetActive(false);
    }

    private void OnEnable()
    {
        GameManager.OnLevelCompleted += CollectCoins;
    }

    private void OnDisable()
    {
        GameManager.OnLevelCompleted -= CollectCoins;
    }

    private void CollectCoins()
    {
        int amount = UnityEngine.Random.Range(150, 251);
        int oldTotal = GetTotalCoins();
        int newTotal = oldTotal + amount;
        
        SaveCoins(newTotal);
        
        // Show reward text
        if (rewardText != null)
        {
            rewardText.text = "+" + amount;
            rewardText.gameObject.SetActive(true);
        }
        
        Debug.Log($"Collected {amount} coins! Animate {oldTotal} -> {newTotal}");
        StartCoroutine(AnimateCoins(oldTotal, newTotal));
    }

    private IEnumerator AnimateCoins(int startValue, int endValue)
    {
        if (animationDelay > 0)
        {
            yield return new WaitForSeconds(animationDelay);
        }

        float elapsed = 0f;

        while (elapsed < animationDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / animationDuration);
            // Linear interpolation for simple counting
            int currentValue = (int)Mathf.Lerp(startValue, endValue, t);
            UpdateCoinDisplay(currentValue);
            yield return null;
        }
        
        UpdateCoinDisplay(endValue);
    }

    private int GetTotalCoins()
    {
        return PlayerPrefs.GetInt(COIN_PREF_KEY, 0);
    }

    private void SaveCoins(int total)
    {
        PlayerPrefs.SetInt(COIN_PREF_KEY, total);
        PlayerPrefs.Save();
    }

    private void UpdateCoinDisplay(int value)
    {
        if (coinText != null)
        {
            coinText.text = value.ToString();
        }
    }
}
