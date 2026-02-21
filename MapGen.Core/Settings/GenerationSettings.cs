namespace MapGen.Core.Settings;

public sealed class GenerationSettings
{
    [SettingMetadata(Importance = SettingImportance.Required, Category = SettingCategory.Grid, DisplayName = "Эпоха", Description = "Профиль эпохи")]
    public Era Era { get; set; } = Era.Industrial;

    [SettingMetadata(Importance = SettingImportance.Required, Category = SettingCategory.Grid, DisplayName = "Сеттинг", Description = "Тип локации")]
    public Setting Setting { get; set; } = Setting.Building;

    [SettingMetadata(Importance = SettingImportance.Required, Category = SettingCategory.Grid, DisplayName = "Шаг сетки", Description = "0.5..2.5", Min = 0.5, Max = 2.5, Step = 0.5)]
    public double GridStep { get; set; } = 1.0;

    [SettingMetadata(Importance = SettingImportance.Required, Category = SettingCategory.Grid, DisplayName = "Ширина карты", Description = "В units", Min = 20, Max = 500, Step = 1)]
    public int MapWidthUnits { get; set; } = 120;

    [SettingMetadata(Importance = SettingImportance.Required, Category = SettingCategory.Grid, DisplayName = "Высота карты", Description = "В units", Min = 20, Max = 500, Step = 1)]
    public int MapHeightUnits { get; set; } = 80;

    [SettingMetadata(Importance = SettingImportance.Required, Category = SettingCategory.Grid, DisplayName = "Авторазмер карты", Description = "Подбирать размер автоматически")]
    public bool AutoMapSize { get; set; } = true;

    [SettingMetadata(Importance = SettingImportance.Required, Category = SettingCategory.Trunk, DisplayName = "Кол-во магистралей", Description = "Основные trunk-линии", Min = 0, Max = 5, Step = 1)]
    public int TrunksCount { get; set; } = 1;

    [SettingMetadata(Importance = SettingImportance.Required, Category = SettingCategory.Trunk, DisplayName = "Ширина trunk", Description = "В units", Min = 1, Max = 10, Step = 1)]
    public int TrunkWidthUnits { get; set; } = 4;

    [SettingMetadata(Importance = SettingImportance.Required, Category = SettingCategory.Blocks, DisplayName = "Кол-во блоков", Description = "BSP target", Min = 0, Max = 20, Step = 1)]
    public int BlocksCount { get; set; } = 6;

    [SettingMetadata(Importance = SettingImportance.Required, Category = SettingCategory.Blocks, DisplayName = "Gates на блок (мин)", Description = "Минимум", Min = 0, Max = 5, Step = 1)]
    public int GatesPerBlockMin { get; set; } = 1;

    [SettingMetadata(Importance = SettingImportance.Required, Category = SettingCategory.Blocks, DisplayName = "Gates на блок (макс)", Description = "Максимум", Min = 0, Max = 8, Step = 1)]
    public int GatesPerBlockMax { get; set; } = 2;

    [SettingMetadata(Importance = SettingImportance.Required, Category = SettingCategory.Rooms, DisplayName = "Комнат всего", Description = "На всю карту", Min = 0, Max = 500, Step = 1)]
    public int RoomsCount { get; set; } = 28;

    [SettingMetadata(Importance = SettingImportance.Advanced, Category = SettingCategory.Rooms, DisplayName = "Комнат всего (мин, legacy)", Description = "Совместимость", Min = 0, Max = 500, Step = 1)]
    public int RoomsTotalMin { get; set; } = 28;

    [SettingMetadata(Importance = SettingImportance.Advanced, Category = SettingCategory.Rooms, DisplayName = "Комнат всего (макс, legacy)", Description = "Совместимость", Min = 0, Max = 500, Step = 1)]
    public int RoomsTotalMax { get; set; } = 28;

    [SettingMetadata(Importance = SettingImportance.Required, Category = SettingCategory.Tech, DisplayName = "Техкомнат (мин)", Description = "На карту", Min = 0, Max = 300, Step = 1)]
    public int TechRoomsMin { get; set; } = 3;

    [SettingMetadata(Importance = SettingImportance.Required, Category = SettingCategory.Tech, DisplayName = "Техкомнат (макс)", Description = "На карту", Min = 0, Max = 300, Step = 1)]
    public int TechRoomsMax { get; set; } = 8;

    [SettingMetadata(Importance = SettingImportance.Required, Category = SettingCategory.Trunk, DisplayName = "Ширина коридора", Description = "В units", Min = 1, Max = 8, Step = 1)]
    public int CorridorWidthUnits { get; set; } = 2;

    [SettingMetadata(Importance = SettingImportance.Required, Category = SettingCategory.Grid, DisplayName = "Seed", Description = "Случайное зерно", Min = 0, Max = int.MaxValue, Step = 1)]
    public int Seed { get; set; } = 12345;

    [SettingMetadata(Importance = SettingImportance.Advanced, Category = SettingCategory.Blocks, DisplayName = "Мин размер блока", Description = "BSP ограничение", Min = 8, Max = 80, Step = 1)]
    public int MinBlockSizeUnits { get; set; } = 16;

    [SettingMetadata(Importance = SettingImportance.Advanced, Category = SettingCategory.Blocks, DisplayName = "SplitBias", Description = "0..1: вероятность вертикального реза", Min = 0, Max = 1, Step = 0.05)]
    public double SplitBias { get; set; } = 0.5;

    [SettingMetadata(Importance = SettingImportance.Advanced, Category = SettingCategory.Trunk, DisplayName = "Мин сегмент trunk", Description = "units", Min = 2, Max = 40, Step = 1)]
    public int MinSegmentLenUnits { get; set; } = 8;

    [SettingMetadata(Importance = SettingImportance.Advanced, Category = SettingCategory.Trunk, DisplayName = "Макс поворотов trunk", Description = "Ограничение ломаной", Min = 0, Max = 20, Step = 1)]
    public int MaxTurns { get; set; } = 6;

    [SettingMetadata(Importance = SettingImportance.Advanced, Category = SettingCategory.Trunk, DisplayName = "Штраф поворота", Description = "Влияет на random-walk", Min = 0, Max = 1, Step = 0.05)]
    public double TurnPenalty { get; set; } = 0.35;

    [SettingMetadata(Importance = SettingImportance.Advanced, Category = SettingCategory.Rooms, DisplayName = "Попыток на комнату", Description = "Packing retry", Min = 1, Max = 200, Step = 1)]
    public int AttemptsPerRoom { get; set; } = 30;

    [SettingMetadata(Importance = SettingImportance.Advanced, Category = SettingCategory.Rooms, DisplayName = "Padding комнат", Description = "Отступ от соседей", Min = 0, Max = 5, Step = 1)]
    public int PaddingUnits { get; set; } = 1;

    [SettingMetadata(Importance = SettingImportance.Advanced, Category = SettingCategory.Rooms, DisplayName = "Макс степень комнаты", Description = "Макс. число коридоров от комнаты", Min = 1, Max = 6, Step = 1)]
    public int MaxRoomDegree { get; set; } = 3;

    [SettingMetadata(Importance = SettingImportance.Advanced, Category = SettingCategory.Validation, DisplayName = "Доля доп. связей", Description = "0..0.2 от числа комнат", Min = 0, Max = 0.2, Step = 0.01)]
    public double ExtraConnectionPercent { get; set; } = 0.05;

    [SettingMetadata(Importance = SettingImportance.Advanced, Category = SettingCategory.Grid, DisplayName = "Соотношение сторон", Description = "Ширина/высота", Min = 0.5, Max = 3, Step = 0.05)]
    public double AutoSizeAspectRatio { get; set; } = 4.0 / 3.0;

    [SettingMetadata(Importance = SettingImportance.Advanced, Category = SettingCategory.Grid, DisplayName = "Мин целевой occupancy", Description = "0..1", Min = 0.1, Max = 0.8, Step = 0.01)]
    public double TargetOccupancyMin { get; set; } = 0.25;

    [SettingMetadata(Importance = SettingImportance.Advanced, Category = SettingCategory.Grid, DisplayName = "Макс целевой occupancy", Description = "0..1", Min = 0.1, Max = 0.9, Step = 0.01)]
    public double TargetOccupancyMax { get; set; } = 0.45;

    [SettingMetadata(Importance = SettingImportance.Advanced, Category = SettingCategory.Grid, DisplayName = "Итерации автоподбора", Description = "3..6", Min = 3, Max = 6, Step = 1)]
    public int AutoSizeMaxAttempts { get; set; } = 4;

    [SettingMetadata(Importance = SettingImportance.Advanced, Category = SettingCategory.Validation, DisplayName = "Проверять связность", Description = "BFS от входа")]
    public bool ValidateConnectivity { get; set; } = true;

    [SettingMetadata(Importance = SettingImportance.Advanced, Category = SettingCategory.Validation, DisplayName = "Чинить связность", Description = "Добавлять коридоры при необходимости")]
    public bool AutoFixConnectivity { get; set; } = true;

    [SettingMetadata(Importance = SettingImportance.Advanced, Category = SettingCategory.Cost, DisplayName = "Вес стоимости стен", Description = "MVP заглушка", Min = 0, Max = 10, Step = 0.1)]
    public double CostWallWeight { get; set; } = 1.0;

    [SettingMetadata(Importance = SettingImportance.Advanced, Category = SettingCategory.Materials, DisplayName = "Высота стен", Description = "MVP данные", Min = 2, Max = 10, Step = 0.1)]
    public double WallHeight { get; set; } = 3.0;

    [SettingMetadata(Importance = SettingImportance.Advanced, Category = SettingCategory.Materials, DisplayName = "Толщина стен", Description = "MVP данные", Min = 0.05, Max = 2, Step = 0.05)]
    public double WallThickness { get; set; } = 0.25;

    [SettingMetadata(Importance = SettingImportance.Advanced, Category = SettingCategory.Materials, DisplayName = "Толщина пола", Description = "MVP данные", Min = 0.05, Max = 1, Step = 0.05)]
    public double FloorThickness { get; set; } = 0.2;

    [SettingMetadata(Importance = SettingImportance.Advanced, Category = SettingCategory.Materials, DisplayName = "Цена за м2", Description = "MVP данные", Min = 0, Max = 100000, Step = 10)]
    public double PricePerM2 { get; set; } = 450;

    public GenerationSettings Clone() => (GenerationSettings)MemberwiseClone();
}
