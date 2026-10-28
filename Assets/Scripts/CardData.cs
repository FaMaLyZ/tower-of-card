using UnityEngine;

public enum CardSuit { Clubs, Diamonds, Hearts, Spades }

public enum CardRank
{
    Ace = 1, Two = 2, Three = 3, Four = 4, Five = 5, Six = 6, Seven = 7,
    Eight = 8, Nine = 9, Ten = 10, Jack = 11, Queen = 12, King = 13
}

// Right click in Project > Create > CardTower > Card Data
// (or use the menu  CardTower > 1. Create Standard 52 CardData  to make all 52 at once)
[CreateAssetMenu(menuName = "CardTower/Card Data", fileName = "NewCard")]
public class CardData : ScriptableObject
{
    [Header("Identity")]
    public string cardName = "Card";
    public CardSuit suit;
    public CardRank rank = CardRank.Ace;

    // What the player is shown as this card's weight: Ace 1, 2-10 face value, J/Q/K 10.
    // Deliberately separate from "mass" below - that one is the physics number, kept small so the
    // tower stays stable, and it would read as meaningless to a player.
    public int Weight => rank == CardRank.Ace ? 1 : Mathf.Min((int)rank, 10);

    [Header("3D models")]
    [Tooltip("Model used for the card in the tower. Empty = a plain coloured box.")]
    public GameObject model;
    [Tooltip("Optional model used for the card in the hand. Empty = HandManager's Card Prefab.")]
    public GameObject handModel;

    [Header("Physics")]
    [Tooltip("Weight of this card in the tower. Each card can have a different mass.")]
    [Min(0.001f)] public float mass = 0.1f;

    [Header("Fallback colour (only used when there is no model)")]
    public Color tint = Color.white;
}