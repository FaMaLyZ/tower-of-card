/// One real card in this run's deck: a card ID plus what the card is.
/// The ID never changes while the card moves between pile and hand.
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
