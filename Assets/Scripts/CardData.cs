using UnityEngine;

// Right click in Project > Create > CardTower > Card Data
[CreateAssetMenu(menuName = "CardTower/Card Data", fileName = "NewCard")]
public class CardData : ScriptableObject
{
    public string cardName = "Card";
    public float score;
    public float weight;
    public Color tint = Color.white;
    public GameObject cardPrefabs;
}