using MapGen.Core.Generation;
using MapGen.Core.Model;
using MapGen.Core.Settings;

namespace MapGen.Core.Tests;

public class MapGeneratorTests
{
    [Fact]
    public void Generate_AllRoomsBelongToSingleConnectedComponent()
    {
        var generator = new MapGenerator();
        var settings = new GenerationSettings
        {
            RoomsCount = 16,
            CorridorWidthUnits = 3,
            Seed = 1337
        };

        var map = generator.Generate(settings, seed: settings.Seed).Map;
        Assert.True(AllRoomsReachable(map));
    }

    [Fact]
    public void Generate_NoCorridorCellsInsideRoomOrRoomBuffer()
    {
        var generator = new MapGenerator();
        var settings = new GenerationSettings
        {
            RoomsCount = 14,
            CorridorWidthUnits = 3,
            Seed = 77
        };

        var map = generator.Generate(settings, seed: settings.Seed).Map;
        var radius = Math.Max(0, (settings.CorridorWidthUnits - 1) / 2);

        var blocked = new HashSet<(int x, int y)>();
        foreach (var room in map.Rooms)
        {
            for (var y = (int)room.RectUnits.Y - radius; y < (int)room.RectUnits.Bottom + radius; y++)
            for (var x = (int)room.RectUnits.X - radius; x < (int)room.RectUnits.Right + radius; x++)
            {
                if (x < 0 || y < 0 || x >= map.WidthUnits || y >= map.HeightUnits) continue;
                blocked.Add((x, y));
            }
        }

        for (var y = 0; y < map.HeightUnits; y++)
        for (var x = 0; x < map.WidthUnits; x++)
        {
            if (map.Cells[x, y] != CellType.Corridor) continue;
            Assert.DoesNotContain((x, y), blocked);
        }
    }

    [Fact]
    public void Generate_DoorsAreUsedForRoomConnections()
    {
        var generator = new MapGenerator();
        var settings = new GenerationSettings
        {
            RoomsCount = 12,
            CorridorWidthUnits = 2,
            Seed = 2024
        };

        var map = generator.Generate(settings, seed: settings.Seed).Map;

        Assert.All(map.Rooms, room => Assert.NotEmpty(room.Doors));

        foreach (var room in map.Rooms)
        {
            foreach (var door in room.Doors)
            {
                var x = (int)door.Position.X;
                var y = (int)door.Position.Y;
                Assert.False(IsInsideRoom(room, x, y));
                Assert.True(IsAdjacentToRoomWall(room, x, y));

                var outward = door.Direction switch
                {
                    ConnectorDirection.North => (x, y - 1),
                    ConnectorDirection.South => (x, y + 1),
                    ConnectorDirection.West => (x - 1, y),
                    ConnectorDirection.East => (x + 1, y),
                    _ => (x, y)
                };

                Assert.True(outward.Item1 >= 0 && outward.Item2 >= 0 && outward.Item1 < map.WidthUnits && outward.Item2 < map.HeightUnits);
                Assert.Contains(map.Cells[outward.Item1, outward.Item2], new[] { CellType.Corridor, CellType.Door, CellType.Gate });
            }
        }
    }

    private static bool AllRoomsReachable(Map map)
    {
        if (map.Doors.Count == 0) return false;

        var queue = new Queue<(int x, int y)>();
        var seen = new HashSet<(int x, int y)>();
        var seenRooms = new HashSet<int>();
        queue.Enqueue(((int)map.Doors[0].Position.X, (int)map.Doors[0].Position.Y));

        while (queue.Count > 0)
        {
            var cell = queue.Dequeue();
            if (!seen.Add(cell)) continue;

            foreach (var door in map.Doors)
            {
                if ((int)door.Position.X == cell.x && (int)door.Position.Y == cell.y)
                {
                    seenRooms.Add(door.RoomId);
                }
            }

            foreach (var (dx, dy) in new[] { (0, -1), (1, 0), (0, 1), (-1, 0) })
            {
                var nx = cell.x + dx;
                var ny = cell.y + dy;
                if (nx < 0 || ny < 0 || nx >= map.WidthUnits || ny >= map.HeightUnits) continue;
                var t = map.Cells[nx, ny];
                if (t is CellType.Corridor or CellType.Door or CellType.Gate)
                {
                    queue.Enqueue((nx, ny));
                }
            }
        }

        return seenRooms.Count == map.Rooms.Count;
    }

    private static bool IsInsideRoom(Room room, int x, int y)
        => x >= room.RectUnits.X && x < room.RectUnits.Right && y >= room.RectUnits.Y && y < room.RectUnits.Bottom;

    private static bool IsAdjacentToRoomWall(Room room, int x, int y)
    {
        return IsInsideRoom(room, x + 1, y)
               || IsInsideRoom(room, x - 1, y)
               || IsInsideRoom(room, x, y + 1)
               || IsInsideRoom(room, x, y - 1);
    }
}
