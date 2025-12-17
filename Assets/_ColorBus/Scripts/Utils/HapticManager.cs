using UnityEngine;
using System.Runtime.InteropServices;

public class HapticManager : GenericSingleton<HapticManager>
{

    // Android VibrationEffect constants
    // https://developer.android.com/reference/android/os/VibrationEffect
    private const int DEFAULT_AMPLITUDE = -1;
    private const int EFFECT_CLICK = 0;
    private const int EFFECT_DOUBLE_CLICK = 1;
    private const int EFFECT_TICK = 2;
    private const int EFFECT_HEAVY_CLICK = 5;

    private AndroidJavaObject _vibrator;
    private AndroidJavaClass _vibrationEffectClass;
    private int _apiLevel;

    protected override void Awake()
    {
        base.Awake();
        InitializeAndroid();
    }

    private void InitializeAndroid()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            {
                using (AndroidJavaObject currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                {
                    _vibrator = currentActivity.Call<AndroidJavaObject>("getSystemService", "vibrator");
                }
            }

            using (AndroidJavaClass versionClass = new AndroidJavaClass("android.os.Build$VERSION"))
            {
                _apiLevel = versionClass.GetStatic<int>("SDK_INT");
            }
            
            // VibrationEffect was introduced in API 26
            if (_apiLevel >= 26)
            {
                _vibrationEffectClass = new AndroidJavaClass("android.os.VibrationEffect");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[HapticManager] Failed to initialize Android Haptics: {e.Message}");
        }
#endif
    }

    public void TriggerLight()
    {
#if UNITY_EDITOR
        Debug.Log("[HapticManager] Light Impact");
// #elif UNITY_IOS
//         _HapticLight();
#elif UNITY_ANDROID
        VibrateAndroid(EFFECT_TICK, 20); // Fallback duration if pre-29
#endif
    }

    public void TriggerMedium()
    {
#if UNITY_EDITOR
        Debug.Log("[HapticManager] Medium Impact");
// #elif UNITY_IOS
//         _HapticMedium();
#elif UNITY_ANDROID
        VibrateAndroid(EFFECT_CLICK, 40);
#endif
    }

    public void TriggerHeavy()
    {
#if UNITY_EDITOR
        Debug.Log("[HapticManager] Heavy Impact");
// #elif UNITY_IOS
//         _HapticHeavy();
#elif UNITY_ANDROID
        VibrateAndroid(EFFECT_HEAVY_CLICK, 80);
#endif
    }

    public void TriggerSuccess()
    {
#if UNITY_EDITOR
        Debug.Log("[HapticManager] Success Notification");
// #elif UNITY_IOS
//         _HapticSuccess();
#elif UNITY_ANDROID
        // Pattern: Wait 0, Vibrate 50, Wait 50, Vibrate 100
        long[] pattern = { 0, 50, 50, 100 }; 
        VibrateAndroidPattern(pattern);
#endif
    }

    public void TriggerFailure()
    {
#if UNITY_EDITOR
        Debug.Log("[HapticManager] Failure Notification");
// #elif UNITY_IOS
//         _HapticFailure();
#elif UNITY_ANDROID
        // Pattern: Wait 0, Vibrate 50, Wait 50, Vibrate 50, Wait 50, Vibrate 50
        long[] pattern = { 0, 50, 50, 50, 50, 50 };
        VibrateAndroidPattern(pattern);
#endif
    }

    private void VibrateAndroid(int effectId, long fallbackDuration)
    {
        if (_vibrator == null) return;

        // SDK 29+ (Android 10) supports Predefined Effects directly
        if (_apiLevel >= 29 && _vibrationEffectClass != null)
        {
            try
            {
                using (AndroidJavaObject effect = _vibrationEffectClass.CallStatic<AndroidJavaObject>("createPredefined", effectId))
                {
                    _vibrator.Call("vibrate", effect);
                }
            }
            catch { VibrateAndroidLegacy(fallbackDuration); }
        }
        // SDK 26+ (Android 8) supports OneShot effects
        else if (_apiLevel >= 26 && _vibrationEffectClass != null)
        {
             try
            {
                using (AndroidJavaObject effect = _vibrationEffectClass.CallStatic<AndroidJavaObject>("createOneShot", fallbackDuration, DEFAULT_AMPLITUDE))
                {
                    _vibrator.Call("vibrate", effect);
                }
            }
            catch { VibrateAndroidLegacy(fallbackDuration); }
        }
        else
        {
            VibrateAndroidLegacy(fallbackDuration);
        }
    }

    private void VibrateAndroidPattern(long[] pattern)
    {
        if (_vibrator == null) return;

         if (_apiLevel >= 26 && _vibrationEffectClass != null)
        {
            try
            {
                using (AndroidJavaObject effect = _vibrationEffectClass.CallStatic<AndroidJavaObject>("createWaveform", pattern, -1))
                {
                    _vibrator.Call("vibrate", effect);
                }
            }
            catch { VibrateAndroidLegacyPattern(pattern); }
        }
        else
        {
             VibrateAndroidLegacyPattern(pattern);
        }
    }

    private void VibrateAndroidLegacy(long milliseconds)
    {
        _vibrator.Call("vibrate", milliseconds);
    }
    
    private void VibrateAndroidLegacyPattern(long[] pattern)
    {
        _vibrator.Call("vibrate", pattern, -1);
    }
}
