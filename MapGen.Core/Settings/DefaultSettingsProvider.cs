namespace MapGen.Core.Settings;

public static class DefaultSettingsProvider
{
    public static GenerationSettings BuildDefaults(Era era, Setting setting)
    {
        var s = new GenerationSettings { Era = era, Setting = setting };

        if (era == Era.Industrial && setting == Setting.Building)
        {
            s.BlocksCount = 6; s.TrunksCount = 1; s.CorridorWidthUnits = 2;
            s.TechRoomsMin = 4; s.TechRoomsMax = 10; s.RoomsCount = 28;
        }
        else if (era == Era.Neolithic && setting == Setting.Building)
        {
            s.BlocksCount = 1; s.TrunksCount = 0; s.RoomsCount = 9;
            s.TechRoomsMin = 0; s.TechRoomsMax = 1; s.MinBlockSizeUnits = 20;
        }
        else if (era == Era.Space && setting == Setting.ShipSpace)
        {
            s.BlocksCount = 8; s.TrunksCount = 1; s.GatesPerBlockMin = 2; s.GatesPerBlockMax = 3;
            s.TechRoomsMin = 10; s.TechRoomsMax = 18; s.RoomsCount = 34; s.MapWidthUnits = 140; s.MapHeightUnits = 70;
        }
        else if (setting == Setting.Train)
        {
            s.MapWidthUnits = 180; s.MapHeightUnits = 36; s.TrunksCount = 1; s.BlocksCount = 5;
            s.TrunkWidthUnits = 3; s.MinBlockSizeUnits = 24; s.SplitBias = 0.9;
        }

        return s;
    }
}
