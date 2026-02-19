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
    public SettingImportance Importance { get; set; } = SettingImportance.Required;
    public SettingCategory Category { get; set; } = SettingCategory.Grid;
    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    // Для максимальной совместимости компиляторов храним числовые
    // ограничения как строки и парсим в UI.
    public string Min { get; set; } = "";
    public string Max { get; set; } = "";
    public string Step { get; set; } = "";
}
