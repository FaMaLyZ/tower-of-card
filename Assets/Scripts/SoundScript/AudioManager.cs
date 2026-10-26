using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Audio Sources")]
    public AudioSource sfxSource;
    public AudioSource musicSource;

    [Header("Audio Mixer")]
    public AudioMixerGroup sfxGroup;
    public AudioMixerGroup musicGroup;
    public AudioMixerGroup uiGroup;

    [Header("Sound Database")]
    public SoundDatabase soundDatabase;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }

        sfxSource.outputAudioMixerGroup = sfxGroup;
        musicSource.outputAudioMixerGroup = musicGroup;
    }

    // Method 
    public void PlayOneShotSFX(AudioClip clip, float volume)
    {
        if (clip == null) return;

        sfxSource.PlayOneShot(clip, volume);
    }

    public void PlayLoopMusic(AudioClip clip)
    {
        if (clip == null) return;

        musicSource.clip = clip;
        musicSource.loop = true;
        musicSource.Play();
    }
}
