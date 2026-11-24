using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Audio Sources")]
    public AudioSource sfxSource;
    public AudioSource musicSource;
    public AudioSource uiSource;

    [Header("Audio Mixer")]
    public AudioMixerGroup sfxGroup;
    public AudioMixerGroup musicGroup;
    public AudioMixerGroup uiGroup;

    [Header("Sound Database")]
    public SoundDatabase soundDatabase;

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        switch (scene.name)
        {
            case "MainMenu":
                PlayLoopMusic(SoundType.Ost_MainMenu);
                break;

            case "MainGame":
                PlayLoopMusic(SoundType.Ost_TowerOfCard);
                break;

            default:
                musicSource.Stop();
                musicSource.clip = null;
                break;
        }
    }

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
    public void PlayOneShotSFX(SoundType type)
    {
        Sound sound = soundDatabase.GetSound(type);

        if (sound == null || sound.clip == null)
            return;

        sfxSource.PlayOneShot(sound.clip, sound.volume);
    }

    public void PlayOneShotUISFX(SoundType type)
    {
        Sound sound = soundDatabase.GetSound(type);

        if (sound == null || sound.clip == null)
            return;

        uiSource.PlayOneShot(sound.clip, sound.volume);
    }

    public void PlayLoopMusic(SoundType type)
    {
        Sound sound = soundDatabase.GetSound(type);

        if (sound == null || sound.clip == null)
            return;

        // ไม่เริ่มเพลงเดิมซ้ำ ถ้ากำลังเล่นอยู่
        if (musicSource.clip == sound.clip && musicSource.isPlaying)
            return;

        musicSource.Stop();
        musicSource.clip = sound.clip;
        musicSource.loop = true;
        musicSource.volume = sound.volume;
        musicSource.Play();
    }

    public void StopLoopMusic()
    {
        musicSource.Stop();
        musicSource.clip = null;
    }

    public void StopSFX()
    {
        sfxSource.Stop();
    }
}
