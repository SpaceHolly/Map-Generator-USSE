using MapGen.Core.Generation;
using MapGen.Core.Model;
using MapGen.Core.Settings;

namespace MapGen.Core.Tests;

public class MapGeneratorTests
{
    [Fact]
    public void Generate_AssignsUniqueIdAndUidToEveryRoom()
    {
        var generator = new MapGenerator();
        var settings = new GenerationSettings
        {
            RoomsTotalMin = 10,
            RoomsTotalMax = 10,
            TechRoomsMin = 2,
            TechRoomsMax = 2
        };

        var result = generator.Generate(settings, seed: 42);

        Assert.NotEmpty(result.Map.Rooms);
        Assert.Equal(result.Map.Rooms.Count, result.Map.Rooms.Select(r => r.Id).Distinct().Count());
        Assert.All(result.Map.Rooms, r => Assert.NotEqual(Guid.Empty, r.Uid));
    }

    [Fact]
    public void Generate_RoomsAreInSingleConnectedComponentByDoorsAndCorridors()
    {
        var generator = new MapGenerator();
        var settings = new GenerationSettings { RoomsTotalMin = 12, RoomsTotalMax = 12 };
        var map = generator.Generate(settings, seed: 1337).Map;

        Assert.True(AllRoomsReachable(map));
    }

    [Fact]
    public void Generate_CorridorsDoNotPassThroughRoomFloorCells()
    {
        var generator = new MapGenerator();
        var settings = new GenerationSettings { RoomsTotalMin = 15, RoomsTotalMax = 15 };
        var map = generator.Generate(settings, seed: 77).Map;

        var roomCells = new HashSet<(int x, int y)>();
        foreach (var room in map.Rooms)
        {
            for (var y = (int)room.RectUnits.Y; y < (int)room.RectUnits.Bottom; y++)
            for (var x = (int)room.RectUnits.X; x < (int)room.RectUnits.Right; x++)
            {
                roomCells.Add((x, y));
            }
        }

        foreach (var corridor in map.Corridors)
        {
            foreach (var point in corridor.Polyline)
            {
                Assert.DoesNotContain(((int)Math.Round(point.X), (int)Math.Round(point.Y)), roomCells);
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
}
