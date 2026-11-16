/// The Glass: the run's lives. A failed blind cracks it; a Boss win or a Skip repairs one crack.
/// At 0 lives the run is over. A worker: it never decides when to crack or repair.
public class GlassLives
{
    public int MaxLives { get; }
    public int LivesRemaining { get; private set; }

    public GlassLives(int maxLives)
    {
        MaxLives = maxLives < 1 ? 1 : maxLives;
        LivesRemaining = MaxLives;
    }

    public bool IsShattered => LivesRemaining <= 0;

    /// Takes one life. Returns false (and changes nothing) if the Glass is already shattered.
    public bool Crack()
    {
        if (IsShattered) return false;
        LivesRemaining--;
        return true;
    }

    /// Gives one life back. Returns false (and changes nothing) if the Glass is already whole.
    public bool Repair()
    {
        if (LivesRemaining >= MaxLives) return false;
        LivesRemaining++;
        return true;
    }
}
