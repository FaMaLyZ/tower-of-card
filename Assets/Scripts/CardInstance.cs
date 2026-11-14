/// One real card in this run's deck: a card ID plus what the card is.
///
/// The ID is how the game tells cards apart (Master Plan 2.6). It stays the same while the card
/// moves between the draw pile, the hand and back, so "which card" never depends on a hand position.
/// DeckManager creates every CardInstance when the deck is reset.
public class CardInstance
{
    public int Id { get; }
    public CardData Data { get; }

    public CardInstance(int id, CardData data)
    {
        Id = id;
        Data = data;
    }

    public override string ToString()
    {
        string cardName = Data != null ? Data.cardName : "no data";
        return $"#{Id} {cardName}";
    }
}
