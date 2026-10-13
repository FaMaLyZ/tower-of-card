using UnityEngine;

// Right click in Project > Create > CardTower > Card Data
[CreateAssetMenu(menuName = "CardTower/Card Data", fileName = "NewCard")]
public class CardData : ScriptableObject
{
    public string cardName = "Card";
    public Color tint = Color.white;
}