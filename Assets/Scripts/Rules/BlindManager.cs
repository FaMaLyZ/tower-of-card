/// The blind being played: the points that are already safe (Master Plan 2.4).
///
/// A3b: only the Safe Points counter. A3c adds the target, the hands left and the win / fail result.
/// A worker: it keeps the numbers and returns results. It never starts the next blind.
public class BlindManager
{
    /// Safe Points: every banked payout of this blind. A collapse never touches them.
    public long BlindScore { get; private set; }

    /// A new blind always starts from 0.
    public void StartBlind()
    {
        BlindScore = 0;
    }

    public void AddSafePoints(long points)
    {
        if (points > 0) BlindScore += points;
    }
}
