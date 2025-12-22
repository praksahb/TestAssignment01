using UnityEngine;
using System.Collections.Generic;
using System;

[CreateAssetMenu(fileName = "AudioSO", menuName = "Audio/AudioSO")]
public class AudioSO : ScriptableObject
{
    [Serializable]
    public class SoundInfo
    {
        public SoundType type;
        public AudioClip[] clips;
    }


    [SerializeField] private List<SoundInfo> _soundList = new List<SoundInfo>();

    public AudioClip GetClip(SoundType type)
    {
        SoundInfo info = _soundList.Find(x => x.type == type);
        if (info != null && info.clips != null && info.clips.Length > 0)
        {
            return info.clips[UnityEngine.Random.Range(0, info.clips.Length)];
        }
        return null;
    }
}

public enum SoundType
{
    None,
    BusTapPositive,
    BusTapNegative,
    PassengerBoarding,
    BusFull,
    LevelComplete
}
