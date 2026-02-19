namespace MapGen.Core.Settings;

public record struct RangeInt(int Min, int Max)
{
    public RangeInt Clamp(int absoluteMin, int absoluteMax)
    {
        var min = Math.Clamp(Min, absoluteMin, absoluteMax);
        var max = Math.Clamp(Max, absoluteMin, absoluteMax);
        if (max < min) max = min;
        return new RangeInt(min, max);
    }
}
