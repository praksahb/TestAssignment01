using UnityEngine;
using TMPro;
using System.Collections;

public class CoinCollector : MonoBehaviour
{
    [Header("UI Reference")]
    [Header("UI Reference")]

    [SerializeField] private TextMeshProUGUI _coinText;
    

    [SerializeField] private TextMeshProUGUI _rewardText;

    [Header("Animation Settings")]

    [SerializeField] private float _animationDuration = 1.0f;
    

    [SerializeField] private float _animationDelay = 0.5f;

    private const string COIN_PREF_KEY = "TotalCoins";

    private void Start()
    {
        UpdateCoinDisplay(GetTotalCoins());
        if (_rewardText != null) _rewardText.gameObject.SetActive(false);
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
        if (_rewardText != null)
        {
            _rewardText.text = "+" + amount;
            _rewardText.gameObject.SetActive(true);
        }
        
        Debug.Log($"Collected {amount} coins! Animate {oldTotal} -> {newTotal}");
        StartCoroutine(AnimateCoins(oldTotal, newTotal));
    }

    private IEnumerator AnimateCoins(int startValue, int endValue)
    {
        if (_animationDelay > 0)
        {
            yield return new WaitForSeconds(_animationDelay);
        }

        float elapsed = 0f;

        while (elapsed < _animationDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / _animationDuration);
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
        if (_coinText != null)
        {
            _coinText.text = value.ToString();
        }
    }
}
