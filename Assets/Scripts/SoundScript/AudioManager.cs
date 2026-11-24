using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

public class AudioManager : MonoBehaviour
{
    static AudioManager instance;
    static bool quitting;

    /// Never null while playing. The real AudioManager lives in the MainMenu scene; if a scene is started
    /// directly (e.g. Pound), a silent stand-in is made so sound calls don't crash. Start from MainMenu to hear sound.
    public static AudioManager Instance
    {
        get
        {
            if (instance == null && Application.isPlaying && !quitting) MakeSilentStandIn();
            return instance;
        }
    }

    /// True for the stand-in (no sound database): it plays nothing
    bool IsSilent => soundDatabase == null;

    static void MakeSilentStandIn()
    {
        var standIn = new GameObject("AudioManager (silent: start from MainMenu for sound)");
        standIn.AddComponent<AudioManager>();      // its Awake makes it the Instance
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        instance = null;
        quitting = false;
        Application.quitting -= OnQuitting;
        Application.quitting += OnQuitting;
    }

    static void OnQuitting() => quitting = true;

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
        // The old scenes ("MainMenu", "MainGame") still get their music too
        if (scene.name == SceneLoader.MenuScene || scene.name == "MainMenu")
        {
            PlayLoopMusic(SoundType.Ost_MainMenu);
        }
        else if (scene.name == SceneLoader.GameplayScene || scene.name == "MainGame")
        {
            PlayLoopMusic(SoundType.Ost_TowerOfCard);
        }
        else
        {
            StopLoopMusic();
        }
    }

    private void Awake()
    {
        // A real AudioManager (from MainMenu) takes over from a silent stand-in
        if (instance != null && instance != this && instance.IsSilent && !IsSilent)
        {
            Destroy(instance.gameObject);
            instance = null;
        }

        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (instance != this)
        {
            Destroy(gameObject);
            return;
        }

        if (sfxSource != null) sfxSource.outputAudioMixerGroup = sfxGroup;
        if (musicSource != null) musicSource.outputAudioMixerGroup = musicGroup;
    }

    // The clip for a sound type, or null if there is nothing to play
    Sound FindPlayable(SoundType type)
    {
        if (IsSilent) return null;
        Sound sound = soundDatabase.GetSound(type);
        return sound != null && sound.clip != null ? sound : null;
    }



    // Method
    public void PlayOneShotSFX(SoundType type)
    {
        Sound sound = FindPlayable(type);
        if (sound == null || sfxSource == null)
            return;

        sfxSource.PlayOneShot(sound.clip, sound.volume);
    }

    public void PlayOneShotUISFX(SoundType type)
    {
        Sound sound = FindPlayable(type);
        if (sound == null || uiSource == null)
            return;

        uiSource.PlayOneShot(sound.clip, sound.volume);
    }

    public void PlayLoopMusic(SoundType type)
    {
        Sound sound = FindPlayable(type);
        if (sound == null || musicSource == null)
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
        if (musicSource == null) return;
        musicSource.Stop();
        musicSource.clip = null;
    }

    public void StopSFX()
    {
        if (sfxSource != null) sfxSource.Stop();
    }
}
