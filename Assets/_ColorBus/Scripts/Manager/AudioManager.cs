using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class AudioManager : GenericSingleton<AudioManager>
{
    [Header("Settings")]
    [SerializeField] private bool _isMute = false;
    [SerializeField] private AudioSource _sfxSource;
    [SerializeField] private AudioSource _bgSource; // Included for future use if needed

    [Header("Audio Data")]
    [SerializeField] private AudioSO _audioData;

    protected override void Awake()
    {
        base.Awake();
        // Ensure AudioSources are assigned or create them
        if (_sfxSource == null)
        {
            GameObject sfxObj = new GameObject("SFX_Source");
            sfxObj.transform.SetParent(transform);
            _sfxSource = sfxObj.AddComponent<AudioSource>();
        }
        if (_bgSource == null)
        {
            GameObject bgObj = new GameObject("BG_Source");
            bgObj.transform.SetParent(transform);
            _bgSource = bgObj.AddComponent<AudioSource>();
        }
    }

    public void PlaySFX(SoundType type)
    {
        if (_isMute || _audioData == null || _sfxSource == null) return;

        AudioClip clip = _audioData.GetClip(type);
        if (clip != null)
        {
            _sfxSource.PlayOneShot(clip);
        }
        else
        {
            Debug.LogWarning($"AudioManager: No clip found for SoundType.{type}");
        }
    }

    public void ToggleMute()
    {
        _isMute = !_isMute;
        if (_sfxSource) _sfxSource.mute = _isMute;
        if (_bgSource) _bgSource.mute = _isMute;
    }
}
