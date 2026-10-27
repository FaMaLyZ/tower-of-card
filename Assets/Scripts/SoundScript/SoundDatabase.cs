using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "Sound Database",
    menuName = "Audio/Sound Database")]
public class SoundDatabase : ScriptableObject
{
    public List<Sound> sounds;
}