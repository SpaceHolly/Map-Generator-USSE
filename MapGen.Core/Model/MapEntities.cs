namespace MapGen.Core.Model;

public enum CellType { Empty, Wall, Floor, Corridor, Door, Gate }
public enum RoomType { Generic, TechRoom, Storage, Workshop, Hangar, Transition, CorridorSpace }
public enum ConnectorDirection { None, North, East, South, West }

public sealed class Map
{
    public required int WidthUnits { get; init; }
    public required int HeightUnits { get; init; }
    public required double GridStep { get; init; }
    public required CellType[,] Cells { get; init; }
    public List<Block> Blocks { get; } = [];
    public List<Room> Rooms { get; } = [];
    public List<Corridor> Corridors { get; } = [];
    public List<Gate> Gates { get; } = [];
    public List<Door> Doors { get; } = [];
    public PointUnits Entrance { get; set; }
}

public sealed class Block
{
    public int Id { get; set; }
    public RectUnits Bounds { get; set; }
    public List<Gate> Gates { get; } = [];
    public List<Room> Rooms { get; } = [];
}

public sealed class Room
{
    public int Id { get; internal set; }
    public Guid Uid { get; internal set; }
    public int BlockId { get; set; }
    public RoomType RoomType { get; set; }
    public RectUnits RectUnits { get; set; }
    public List<Door> Doors { get; } = [];
}

public sealed class Gate
{
    public int BlockId { get; set; }
    public PointUnits Position { get; set; }
    public string GateType { get; set; } = "Main";
}

public sealed class Door
{
    public int BlockId { get; set; }
    public int RoomId { get; set; }
    public PointUnits Position { get; set; }
    public ConnectorDirection Direction { get; set; } = ConnectorDirection.None;
}

public sealed class Corridor
{
    public List<PointUnits> Polyline { get; set; } = [];
    public double WidthUnits { get; set; }
    public bool IsTech { get; set; }
}
