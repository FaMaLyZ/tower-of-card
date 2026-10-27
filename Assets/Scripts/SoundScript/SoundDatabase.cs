using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "SoundDatabase",
    menuName = "Audio/Sound Database")]
public class SoundDatabase : ScriptableObject
{
    public Sound[] sounds;

    public Sound GetSound(SoundType type)
    {
        foreach (Sound sound in sounds)
        {
            if (sound.type == type)
                return sound;
        }

        Debug.LogWarning($"Sound not found: {type}");
        return null;
    }
}