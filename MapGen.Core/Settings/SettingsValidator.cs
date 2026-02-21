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

        s.MapWidthUnits = ClampInt(s.MapWidthUnits, 20, 500, w, nameof(s.MapWidthUnits));
        s.MapHeightUnits = ClampInt(s.MapHeightUnits, 20, 500, w, nameof(s.MapHeightUnits));
        s.TrunksCount = ClampInt(s.TrunksCount, 0, 5, w, nameof(s.TrunksCount));
        s.BlocksCount = ClampInt(s.BlocksCount, 0, 20, w, nameof(s.BlocksCount));
        s.GatesPerBlockMin = ClampInt(s.GatesPerBlockMin, 0, 5, w, nameof(s.GatesPerBlockMin));
        s.GatesPerBlockMax = ClampInt(s.GatesPerBlockMax, 0, 8, w, nameof(s.GatesPerBlockMax));
        s.RoomsCount = ClampInt(s.RoomsCount, 0, 500, w, nameof(s.RoomsCount));
        s.RoomsTotalMin = ClampInt(s.RoomsTotalMin, 0, 500, w, nameof(s.RoomsTotalMin));
        s.RoomsTotalMax = ClampInt(s.RoomsTotalMax, 0, 500, w, nameof(s.RoomsTotalMax));
        s.TechRoomsMin = ClampInt(s.TechRoomsMin, 0, 300, w, nameof(s.TechRoomsMin));
        s.TechRoomsMax = ClampInt(s.TechRoomsMax, 0, 300, w, nameof(s.TechRoomsMax));
        s.MinBlockSizeUnits = ClampInt(s.MinBlockSizeUnits, 8, 80, w, nameof(s.MinBlockSizeUnits));
        s.MaxRoomDegree = ClampInt(s.MaxRoomDegree, 1, 6, w, nameof(s.MaxRoomDegree));
        s.AutoSizeMaxAttempts = ClampInt(s.AutoSizeMaxAttempts, 3, 6, w, nameof(s.AutoSizeMaxAttempts));

        if (s.GatesPerBlockMax < s.GatesPerBlockMin) { s.GatesPerBlockMax = s.GatesPerBlockMin; w.Add("GatesPerBlockMax поднят до min."); }
        if (s.RoomsTotalMax < s.RoomsTotalMin) { s.RoomsTotalMax = s.RoomsTotalMin; w.Add("RoomsTotalMax поднят до min."); }
        if (s.TechRoomsMax < s.TechRoomsMin) { s.TechRoomsMax = s.TechRoomsMin; w.Add("TechRoomsMax поднят до min."); }

        // Legacy compatibility: keep range fields aligned to single room count input.
        if (s.RoomsTotalMin != s.RoomsCount || s.RoomsTotalMax != s.RoomsCount)
        {
            s.RoomsTotalMin = s.RoomsCount;
            s.RoomsTotalMax = s.RoomsCount;
        }

        s.SplitBias = Math.Clamp(s.SplitBias, 0, 1);
        s.TurnPenalty = Math.Clamp(s.TurnPenalty, 0, 1);
        s.ExtraConnectionPercent = Math.Clamp(s.ExtraConnectionPercent, 0, 0.2);
        s.AutoSizeAspectRatio = Math.Clamp(s.AutoSizeAspectRatio, 0.5, 3);
        s.TargetOccupancyMin = Math.Clamp(s.TargetOccupancyMin, 0.1, 0.8);
        s.TargetOccupancyMax = Math.Clamp(s.TargetOccupancyMax, 0.1, 0.9);
        if (s.TargetOccupancyMax < s.TargetOccupancyMin)
        {
            s.TargetOccupancyMax = s.TargetOccupancyMin;
            w.Add("TargetOccupancyMax поднят до min.");
        }

        return (s, w);
    }

    private static int ClampInt(int value, int min, int max, List<string> warnings, string field)
    {
        var old = value;
        var clamped = Math.Clamp(value, min, max);
        if (old != clamped) warnings.Add($"{field} исправлен: {old} -> {clamped}");
        return clamped;
    }
}
