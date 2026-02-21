namespace MapGen.Core.Settings;

public enum SettingImportance { Required, Advanced }

public enum SettingCategory
{
    Grid,
    Blocks,
    Trunk,
    Rooms,
    Tech,
    Validation,
    Cost,
    Materials
}

[AttributeUsage(AttributeTargets.Property)]
public sealed class SettingMetadataAttribute : Attribute
{
    public SettingImportance Importance { get; init; } = SettingImportance.Required;
    public SettingCategory Category { get; init; } = SettingCategory.Grid;
    public string DisplayName { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public double Min { get; set; } = double.NaN;
    public double Max { get; set; } = double.NaN;
    public double Step { get; set; } = double.NaN;

}
