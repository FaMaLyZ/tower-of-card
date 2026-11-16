/// Money-style maths for chips. Chips are whole numbers (long); percent maths uses decimal
/// because float gives off-by-one chips.
/// Round ONCE at the end of each formula, never in the middle.
public static class PercentMath
{
    /// 10 -> 0.10m, 120 -> 1.20m. Used to turn a GameConfig whole-number percent into a rate.
    public static decimal ToRate(int percent) => percent / 100m;

    /// Round down to whole chips.
    public static long FloorToChips(decimal value) => (long)decimal.Floor(value);
}
