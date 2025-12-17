using UnityEngine;

public class HapticManager : GenericSingleton<HapticManager>
{
    protected override void Awake()
    {
        base.Awake();
    }

    public void TriggerVibrate()
    {
#if UNITY_EDITOR
        Debug.Log("[HapticManager] Vibrate");
#else
        Handheld.Vibrate();
#endif
    }
}
