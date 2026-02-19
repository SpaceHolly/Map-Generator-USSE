namespace MapGen.Core.Settings;

public static class SettingsValidator
{
    public static (GenerationSettings Clamped, List<string> Warnings) ValidateAndClamp(GenerationSettings input)
    {
        var s = input.Clone();
        var w = new List<string>();

        double[] allowed = [0.5, 1, 1.5, 2, 2.5];
        if (!allowed.Contains(s.GridStep))
        {
            s.GridStep = allowed.OrderBy(x => Math.Abs(x - s.GridStep)).First();
            w.Add($"GridStep округлен до {s.GridStep}.");
        }

        ClampInt(ref s.MapWidthUnits, 20, 500, w, nameof(s.MapWidthUnits));
        ClampInt(ref s.MapHeightUnits, 20, 500, w, nameof(s.MapHeightUnits));
        ClampInt(ref s.TrunksCount, 0, 5, w, nameof(s.TrunksCount));
        ClampInt(ref s.BlocksCount, 0, 20, w, nameof(s.BlocksCount));
        ClampInt(ref s.GatesPerBlockMin, 0, 5, w, nameof(s.GatesPerBlockMin));
        ClampInt(ref s.GatesPerBlockMax, 0, 8, w, nameof(s.GatesPerBlockMax));
        ClampInt(ref s.RoomsTotalMin, 0, 500, w, nameof(s.RoomsTotalMin));
        ClampInt(ref s.RoomsTotalMax, 0, 500, w, nameof(s.RoomsTotalMax));
        ClampInt(ref s.TechRoomsMin, 0, 300, w, nameof(s.TechRoomsMin));
        ClampInt(ref s.TechRoomsMax, 0, 300, w, nameof(s.TechRoomsMax));
        ClampInt(ref s.MinBlockSizeUnits, 8, 80, w, nameof(s.MinBlockSizeUnits));

        if (s.GatesPerBlockMax < s.GatesPerBlockMin) { s.GatesPerBlockMax = s.GatesPerBlockMin; w.Add("GatesPerBlockMax поднят до min."); }
        if (s.RoomsTotalMax < s.RoomsTotalMin) { s.RoomsTotalMax = s.RoomsTotalMin; w.Add("RoomsTotalMax поднят до min."); }
        if (s.TechRoomsMax < s.TechRoomsMin) { s.TechRoomsMax = s.TechRoomsMin; w.Add("TechRoomsMax поднят до min."); }

        s.SplitBias = Math.Clamp(s.SplitBias, 0, 1);
        s.TurnPenalty = Math.Clamp(s.TurnPenalty, 0, 1);

        return (s, w);
    }

    private static void ClampInt(ref int value, int min, int max, List<string> warnings, string field)
    {
        var old = value;
        value = Math.Clamp(value, min, max);
        if (old != value) warnings.Add($"{field} исправлен: {old} -> {value}");
    }
}
