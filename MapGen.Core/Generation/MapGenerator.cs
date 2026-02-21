using MapGen.Core.Model;
using MapGen.Core.Settings;

namespace MapGen.Core.Generation;

public sealed class GenerationResult
{
    public required Map Map { get; init; }
    public List<string> Warnings { get; } = new();
}

internal sealed class GenerationContext
{
    public int NextRoomId { get; private set; } = 1;

    public void AssignRoomIds(Room room)
    {
        room.Id = NextRoomId++;
        room.Uid = Guid.NewGuid();
    }
}

internal readonly record struct RouteNode(int X, int Y, int Direction);
internal readonly record struct DoorCandidate(int X, int Y, ConnectorDirection Direction, int NormalDx, int NormalDy);

public sealed class MapGenerator
{
    private static readonly (int dx, int dy, ConnectorDirection dir)[] CardinalDirections =
    [
        (0, -1, ConnectorDirection.North),
        (1, 0, ConnectorDirection.East),
        (0, 1, ConnectorDirection.South),
        (-1, 0, ConnectorDirection.West)
    ];

    public GenerationResult Generate(GenerationSettings settings, int? seed = null)
    {
        var (s, warnings) = SettingsValidator.ValidateAndClamp(settings);
        var rng = new Random(seed ?? s.Seed);

        var (width, height) = s.AutoMapSize && s.Setting != Setting.Train
            ? EstimateInitialMapSize(s)
            : (s.MapWidthUnits, s.MapHeightUnits);

        Map? bestMap = null;
        double bestDistance = double.MaxValue;

        var attempts = s.AutoMapSize && s.Setting != Setting.Train ? s.AutoSizeMaxAttempts : 1;
        for (var attempt = 0; attempt < attempts; attempt++)
        {
            var context = new GenerationContext();
            var map = BuildBaseArea(s, width, height);

            if (s.Setting == Setting.Train)
            {
                GenerateTrainLayout(map, s, rng, context);
            }
            else
            {
                GenerateTrunks(map, s, rng);
                SplitBlocks(map, s, rng);
                PlaceGates(map, s, rng);
                PlaceRooms(map, s, rng, context);
                ConnectRooms(map, s, rng);
            }

            ValidateAndFix(map, s, warnings, rng);
            var occupancy = ComputeOccupancy(map);
            var occupancyDistance = OccupancyDistance(occupancy, s.TargetOccupancyMin, s.TargetOccupancyMax);
            if (bestMap is null || occupancyDistance < bestDistance)
            {
                bestMap = map;
                bestDistance = occupancyDistance;
            }

            if (!s.AutoMapSize || s.Setting == Setting.Train)
            {
                break;
            }

            var tooDense = occupancy > s.TargetOccupancyMax || map.Rooms.Any(r => r.Doors.Count == 0);
            var tooSparse = occupancy < s.TargetOccupancyMin;
            if (!tooDense && !tooSparse)
            {
                break;
            }

            var scale = tooDense ? 1.15 : 0.9;
            width = Math.Clamp((int)Math.Round(width * scale), 20, 500);
            height = Math.Clamp((int)Math.Round(height * scale), 20, 500);
        }

        var result = new GenerationResult { Map = bestMap ?? BuildBaseArea(s, s.MapWidthUnits, s.MapHeightUnits) };
        if (s.AutoMapSize)
        {
            warnings.Add($"AutoMapSize: итоговый размер {result.Map.WidthUnits}x{result.Map.HeightUnits}.");
        }

        result.Warnings.AddRange(warnings);
        return result;
    }

    private static (int width, int height) EstimateInitialMapSize(GenerationSettings s)
    {
        var expectedRooms = Math.Max(1, s.RoomsCount);
        var avgRoomArea = 48.0;
        var roomArea = expectedRooms * avgRoomArea;
        var corridorReserve = roomArea * 0.35;
        var bufferReserve = expectedRooms * Math.Pow(s.CorridorWidthUnits + s.PaddingUnits + 1, 2);
        var targetOccupancy = Math.Clamp((s.TargetOccupancyMin + s.TargetOccupancyMax) / 2.0, 0.2, 0.7);
        var totalArea = (roomArea + corridorReserve + bufferReserve) / targetOccupancy;

        var ratio = Math.Clamp(s.AutoSizeAspectRatio, 0.5, 3.0);
        var width = (int)Math.Round(Math.Sqrt(totalArea * ratio));
        var height = (int)Math.Round(width / ratio);

        return (Math.Clamp(width, 20, 500), Math.Clamp(height, 20, 500));
    }

    private static double ComputeOccupancy(Map map)
    {
        var used = 0;
        for (var y = 0; y < map.HeightUnits; y++)
        for (var x = 0; x < map.WidthUnits; x++)
        {
            var c = map.Cells[x, y];
            if (c is CellType.Floor or CellType.Corridor or CellType.Door) used++;
        }

        return used / (double)(map.WidthUnits * map.HeightUnits);
    }

    private static double OccupancyDistance(double occupancy, double min, double max)
    {
        if (occupancy < min) return min - occupancy;
        if (occupancy > max) return occupancy - max;
        return 0;
    }

    private static Map BuildBaseArea(GenerationSettings s, int width, int height)
    {
        return new Map
        {
            WidthUnits = width,
            HeightUnits = height,
            GridStep = s.GridStep,
            Cells = new CellType[width, height],
            Entrance = new PointUnits(0, height / 2.0)
        };
    }

    private static void GenerateTrunks(Map map, GenerationSettings s, Random rng)
    {
        var trunks = Math.Min(s.TrunksCount, 1);
        for (int i = 0; i < trunks; i++)
        {
            var polyline = new List<PointUnits> { new(0, map.HeightUnits / 2.0 + i * 2) };
            var cur = polyline[0];
            var turns = 0;
            var horizontal = true;

            while (cur.X < map.WidthUnits - 2 && cur.Y > 1 && cur.Y < map.HeightUnits - 2)
            {
                var len = rng.Next(s.MinSegmentLenUnits, s.MinSegmentLenUnits + 8);
                var nx = horizontal ? Math.Min(map.WidthUnits - 1, cur.X + len) : cur.X;
                var ny = horizontal ? cur.Y : Math.Clamp(cur.Y + rng.Next(-len, len + 1), 1, map.HeightUnits - 2);
                var next = new PointUnits(nx, ny);
                polyline.Add(next);
                cur = next;

                if (turns < s.MaxTurns && rng.NextDouble() > s.TurnPenalty)
                {
                    horizontal = !horizontal;
                    turns++;
                }
                else
                {
                    horizontal = true;
                }
            }

            var c = new Corridor { Polyline = polyline, WidthUnits = s.TrunkWidthUnits, IsTech = false };
            map.Corridors.Add(c);
            RasterizePolyline(map, c.Polyline, c.WidthUnits, CellType.Corridor, false);
        }
    }

    private static void SplitBlocks(Map map, GenerationSettings s, Random rng)
    {
        if (s.BlocksCount <= 0) return;

        var rects = new List<RectUnits> { new(2, 2, map.WidthUnits - 4, map.HeightUnits - 4) };
        while (rects.Count < s.BlocksCount)
        {
            var idx = rects.FindIndex(r => r.Width >= s.MinBlockSizeUnits * 2 || r.Height >= s.MinBlockSizeUnits * 2);
            if (idx < 0) break;

            var src = rects[idx];
            rects.RemoveAt(idx);

            var vertical = rng.NextDouble() < s.SplitBias;
            if (src.Width < s.MinBlockSizeUnits * 2) vertical = false;
            if (src.Height < s.MinBlockSizeUnits * 2) vertical = true;

            if (vertical)
            {
                var split = rng.Next(s.MinBlockSizeUnits, (int)src.Width - s.MinBlockSizeUnits);
                rects.Add(new RectUnits(src.X, src.Y, split, src.Height));
                rects.Add(new RectUnits(src.X + split, src.Y, src.Width - split, src.Height));
            }
            else
            {
                var split = rng.Next(s.MinBlockSizeUnits, (int)src.Height - s.MinBlockSizeUnits);
                rects.Add(new RectUnits(src.X, src.Y, src.Width, split));
                rects.Add(new RectUnits(src.X, src.Y + split, src.Width, src.Height - split));
            }
        }

        for (var i = 0; i < rects.Count; i++) map.Blocks.Add(new Block { Id = i + 1, Bounds = rects[i] });
    }

    private static void PlaceGates(Map map, GenerationSettings s, Random rng)
    {
        foreach (var block in map.Blocks)
        {
            var count = rng.Next(s.GatesPerBlockMin, s.GatesPerBlockMax + 1);
            for (int i = 0; i < count; i++)
            {
                var side = rng.Next(0, 4);
                var p = side switch
                {
                    0 => new PointUnits(block.Bounds.X, rng.Next((int)block.Bounds.Y, (int)block.Bounds.Bottom)),
                    1 => new PointUnits(block.Bounds.Right, rng.Next((int)block.Bounds.Y, (int)block.Bounds.Bottom)),
                    2 => new PointUnits(rng.Next((int)block.Bounds.X, (int)block.Bounds.Right), block.Bounds.Y),
                    _ => new PointUnits(rng.Next((int)block.Bounds.X, (int)block.Bounds.Right), block.Bounds.Bottom)
                };
                var gate = new Gate { BlockId = block.Id, Position = p, GateType = "Standard" };
                block.Gates.Add(gate);
                map.Gates.Add(gate);
                MarkCell(map, p, CellType.Gate, force: true);
            }
        }
    }

    private static void PlaceRooms(Map map, GenerationSettings s, Random rng, GenerationContext context)
    {
        if (map.Blocks.Count == 0) return;

        var targetRooms = s.RoomsCount;
        var techTarget = rng.Next(s.TechRoomsMin, s.TechRoomsMax + 1);

        var blockOrder = map.Blocks.OrderBy(_ => rng.Next()).ToList();
        var localRooms = map.Blocks.ToDictionary(block => block.Id, _ => new List<RectUnits>());
        var placedTotal = 0;

        while (placedTotal < targetRooms)
        {
            var placedInRound = false;
            foreach (var block in blockOrder)
            {
                if (placedTotal >= targetRooms) break;
                if (!TryPlaceRoomInBlock(block, localRooms[block.Id], s, rng, out var candidate)) continue;

                var room = new Room
                {
                    BlockId = block.Id,
                    RectUnits = candidate,
                    RoomType = placedTotal < techTarget ? RoomType.TechRoom : RoomType.Generic
                };
                context.AssignRoomIds(room);
                block.Rooms.Add(room);
                map.Rooms.Add(room);
                PaintRect(map, candidate, CellType.Floor);
                placedTotal++;
                placedInRound = true;
            }

            if (!placedInRound) break;
        }
    }

    private static bool TryPlaceRoomInBlock(Block block, List<RectUnits> localRooms, GenerationSettings s, Random rng, out RectUnits candidate)
    {
        candidate = default;
        var maxWidth = (int)Math.Min(14, block.Bounds.Width - 2);
        var maxHeight = (int)Math.Min(12, block.Bounds.Height - 2);
        if (maxWidth < 4 || maxHeight < 4) return false;

        for (int a = 0; a < s.AttemptsPerRoom; a++)
        {
            var w = rng.Next(4, maxWidth + 1);
            var h = rng.Next(4, maxHeight + 1);
            var xStart = (int)block.Bounds.X + 1;
            var xEndExclusive = (int)block.Bounds.Right - w;
            var yStart = (int)block.Bounds.Y + 1;
            var yEndExclusive = (int)block.Bounds.Bottom - h;
            if (xStart >= xEndExclusive || yStart >= yEndExclusive) continue;

            var x = rng.Next(xStart, xEndExclusive);
            var y = rng.Next(yStart, yEndExclusive);
            candidate = new RectUnits(x, y, w, h);

            if (localRooms.Any(r => Expand(r, s.PaddingUnits).Intersects(candidate))) continue;
            localRooms.Add(candidate);
            return true;
        }

        return false;
    }

    private static RectUnits Expand(RectUnits r, int pad) => new(r.X - pad, r.Y - pad, r.Width + pad * 2, r.Height + pad * 2);

    private static void ConnectRooms(Map map, GenerationSettings s, Random rng)
    {
        if (map.Rooms.Count <= 1) return;

        var roomDoors = map.Rooms.ToDictionary(r => r.Id, r => BuildDoorCandidates(map, r));
        var roomBuffer = BuildRoomBufferMap(map, s.CorridorWidthUnits);
        var edges = BuildConnectionGraph(map, s, rng);

        foreach (var (roomA, roomB) in edges)
        {
            if (roomDoors[roomA.Id].Count == 0 || roomDoors[roomB.Id].Count == 0) continue;

            if (!TryConnectRoomsDoorToDoor(map, s, roomA, roomB, roomDoors[roomA.Id], roomDoors[roomB.Id], roomBuffer)) continue;
        }
    }

    private static bool[,] BuildRoomBufferMap(Map map, int corridorWidth)
    {
        var buffer = new bool[map.WidthUnits, map.HeightUnits];
        var radius = Math.Max(0, (corridorWidth - 1) / 2);
        foreach (var room in map.Rooms)
        {
            for (var y = (int)room.RectUnits.Y - radius; y < (int)room.RectUnits.Bottom + radius; y++)
            for (var x = (int)room.RectUnits.X - radius; x < (int)room.RectUnits.Right + radius; x++)
            {
                if (!IsInside(map, x, y)) continue;
                buffer[x, y] = true;
            }
        }

        return buffer;
    }

    private static List<(Room a, Room b)> BuildConnectionGraph(Map map, GenerationSettings s, Random rng)
    {
        var rooms = map.Rooms;
        var roomIndex = rooms.Select((room, index) => (room, index)).ToDictionary(x => x.room.Id, x => x.index);
        var degree = new int[rooms.Count];
        var edges = new List<(int a, int b)>();
        var connected = new HashSet<int> { 0 };

        while (connected.Count < rooms.Count)
        {
            (int a, int b, double d)? best = null;
            foreach (var a in connected)
            {
                for (var b = 0; b < rooms.Count; b++)
                {
                    if (connected.Contains(b)) continue;
                    var d = ManhattanDistance(rooms[a].RectUnits.Center, rooms[b].RectUnits.Center);
                    var degreeOk = degree[a] < s.MaxRoomDegree && degree[b] < s.MaxRoomDegree;
                    if (!degreeOk && connected.Count < rooms.Count - 1) continue;
                    if (best is null || d < best.Value.d) best = (a, b, d);
                }
            }

            if (best is null) break;
            edges.Add((best.Value.a, best.Value.b));
            degree[best.Value.a]++;
            degree[best.Value.b]++;
            connected.Add(best.Value.b);
        }

        var allPairs = new List<(int a, int b, double d)>();
        for (var i = 0; i < rooms.Count; i++)
        for (var j = i + 1; j < rooms.Count; j++)
        {
            if (edges.Any(e => (e.a == i && e.b == j) || (e.a == j && e.b == i))) continue;
            var d = ManhattanDistance(rooms[i].RectUnits.Center, rooms[j].RectUnits.Center);
            allPairs.Add((i, j, d));
        }

        var density = rooms.Count / (double)(map.WidthUnits * map.HeightUnits);
        var densityScale = Math.Clamp(1.0 - density * 20.0, 0.15, 1.0);
        var percentLimit = (int)Math.Floor(rooms.Count * s.ExtraConnectionPercent);
        var baseLimit = Math.Max(1, rooms.Count / 20);
        if (rooms.Count < 10) baseLimit = 0;
        var extraLimit = Math.Min(baseLimit, percentLimit == 0 ? baseLimit : percentLimit);
        extraLimit = (int)Math.Floor(extraLimit * densityScale);

        foreach (var edge in allPairs.OrderBy(p => p.d).ThenBy(_ => rng.Next()))
        {
            if (extraLimit <= 0) break;
            if (degree[edge.a] >= s.MaxRoomDegree || degree[edge.b] >= s.MaxRoomDegree) continue;
            edges.Add((edge.a, edge.b));
            degree[edge.a]++;
            degree[edge.b]++;
            extraLimit--;
        }

        return edges.Select(e => (rooms[e.a], rooms[e.b])).ToList();
    }

    private static bool TryConnectRoomsDoorToDoor(
        Map map,
        GenerationSettings s,
        Room roomA,
        Room roomB,
        List<DoorCandidate> candidatesA,
        List<DoorCandidate> candidatesB,
        bool[,] roomBuffer)
    {
        var pairs =
            (from doorA in candidatesA
             from doorB in candidatesB
             let distance = Math.Abs(doorA.X - doorB.X) + Math.Abs(doorA.Y - doorB.Y)
             orderby distance
             select (doorA, doorB)).Take(16);

        foreach (var (doorA, doorB) in pairs)
        {
            var start = (doorA.X + doorA.NormalDx, doorA.Y + doorA.NormalDy);
            var end = (doorB.X + doorB.NormalDx, doorB.Y + doorB.NormalDy);
            if (!IsInside(map, start.Item1, start.Item2) || !IsInside(map, end.Item1, end.Item2)) continue;

            if (!TryBuildRoute(map, start, end, roomBuffer, (doorA.X, doorA.Y), (doorB.X, doorB.Y), out var corePath)) continue;

            var fullPath = new List<(int x, int y)> { (doorA.X, doorA.Y) };
            fullPath.AddRange(corePath);
            fullPath.Add((doorB.X, doorB.Y));

            if (!TryCarveCorridor(map, fullPath, s.CorridorWidthUnits, roomBuffer, (doorA.X, doorA.Y), (doorB.X, doorB.Y)))
            {
                var forbidden = BuildCarveFootprint(fullPath, s.CorridorWidthUnits);
                if (!TryAStar(map, start, end, roomBuffer, (doorA.X, doorA.Y), (doorB.X, doorB.Y), forbidden, out corePath)) continue;
                fullPath = new List<(int x, int y)> { (doorA.X, doorA.Y) };
                fullPath.AddRange(corePath);
                fullPath.Add((doorB.X, doorB.Y));
                if (!TryCarveCorridor(map, fullPath, s.CorridorWidthUnits, roomBuffer, (doorA.X, doorA.Y), (doorB.X, doorB.Y))) continue;
            }

            var routePoints = fullPath.Select(p => new PointUnits(p.x, p.y)).ToList();
            map.Corridors.Add(new Corridor
            {
                Polyline = routePoints,
                WidthUnits = s.CorridorWidthUnits,
                IsTech = roomA.RoomType == RoomType.TechRoom || roomB.RoomType == RoomType.TechRoom
            });

            AddDoor(map, roomA, doorA);
            AddDoor(map, roomB, doorB);
            return true;
        }

        return false;
    }

    private static HashSet<(int x, int y)> BuildCarveFootprint(List<(int x, int y)> path, int width)
    {
        var blocked = new HashSet<(int x, int y)>();
        var radius = Math.Max(0, (width - 1) / 2);
        foreach (var (x, y) in path)
        for (var oy = -radius; oy <= radius; oy++)
        for (var ox = -radius; ox <= radius; ox++)
            blocked.Add((x + ox, y + oy));

        return blocked;
    }

    private static bool TryCarveCorridor(Map map, List<(int x, int y)> path, int width, bool[,] roomBuffer, params (int x, int y)[] allowedDoorCells)
    {
        var doorSet = allowedDoorCells.ToHashSet();
        var footprint = BuildCarveFootprint(path, width);

        foreach (var (x, y) in footprint)
        {
            if (!IsInside(map, x, y)) return false;
            if (doorSet.Contains((x, y))) continue;
            var cell = map.Cells[x, y];
            if (cell is CellType.Floor or CellType.Wall or CellType.Gate or CellType.Door) return false;
            if (roomBuffer[x, y]) return false;
        }

        foreach (var (x, y) in footprint)
        {
            if (doorSet.Contains((x, y))) continue;
            map.Cells[x, y] = CellType.Corridor;
        }

        return true;
    }

    private static List<DoorCandidate> BuildDoorCandidates(Map map, Room room)
    {
        var candidates = new List<DoorCandidate>();

        for (var x = (int)room.RectUnits.X; x < (int)room.RectUnits.Right; x++)
        {
            TryAddDoor(map, room, x, (int)room.RectUnits.Y - 1, ConnectorDirection.North, 0, -1, candidates);
            TryAddDoor(map, room, x, (int)room.RectUnits.Bottom, ConnectorDirection.South, 0, 1, candidates);
        }

        for (var y = (int)room.RectUnits.Y; y < (int)room.RectUnits.Bottom; y++)
        {
            TryAddDoor(map, room, (int)room.RectUnits.X - 1, y, ConnectorDirection.West, -1, 0, candidates);
            TryAddDoor(map, room, (int)room.RectUnits.Right, y, ConnectorDirection.East, 1, 0, candidates);
        }

        return candidates.Distinct().ToList();
    }

    private static void TryAddDoor(Map map, Room room, int x, int y, ConnectorDirection direction, int nx, int ny, List<DoorCandidate> candidates)
    {
        if (!IsInside(map, x, y)) return;
        if (ContainsRoomCell(room, x, y)) return;
        if (map.Rooms.Any(r => r.Id != room.Id && ContainsRoomCell(r, x, y))) return;
        if (map.Cells[x, y] == CellType.Wall) return;

        candidates.Add(new DoorCandidate(x, y, direction, nx, ny));
    }

    private static bool ContainsRoomCell(Room room, int x, int y)
        => x >= room.RectUnits.X && x < room.RectUnits.Right && y >= room.RectUnits.Y && y < room.RectUnits.Bottom;

    private static bool TryBuildRoute(
        Map map,
        (int x, int y) start,
        (int x, int y) end,
        bool[,] roomBuffer,
        (int x, int y) startDoor,
        (int x, int y) endDoor,
        out List<(int x, int y)> path)
    {
        if (TryBuildLRoute(map, start, end, roomBuffer, startDoor, endDoor, out path)) return true;
        return TryAStar(map, start, end, roomBuffer, startDoor, endDoor, null, out path);
    }

    private static bool TryBuildLRoute(
        Map map,
        (int x, int y) start,
        (int x, int y) end,
        bool[,] roomBuffer,
        (int x, int y) startDoor,
        (int x, int y) endDoor,
        out List<(int x, int y)> path)
    {
        var pivots = new[] { (start.x, end.y), (end.x, start.y) };
        foreach (var pivot in pivots)
        {
            if (!TryBuildStraightSegment(map, start, pivot, roomBuffer, startDoor, endDoor, out var first)) continue;
            if (!TryBuildStraightSegment(map, pivot, end, roomBuffer, startDoor, endDoor, out var second)) continue;

            first.AddRange(second.Skip(1));
            path = first;
            return true;
        }

        path = [];
        return false;
    }

    private static bool TryBuildStraightSegment(
        Map map,
        (int x, int y) a,
        (int x, int y) b,
        bool[,] roomBuffer,
        (int x, int y) startDoor,
        (int x, int y) endDoor,
        out List<(int x, int y)> segment)
    {
        segment = [];
        if (a.x != b.x && a.y != b.y) return false;

        var dx = Math.Sign(b.x - a.x);
        var dy = Math.Sign(b.y - a.y);
        var cx = a.x;
        var cy = a.y;
        segment.Add((cx, cy));

        while (cx != b.x || cy != b.y)
        {
            cx += dx;
            cy += dy;
            if (!IsWalkable(map, cx, cy, roomBuffer, startDoor, endDoor)) return false;
            segment.Add((cx, cy));
        }

        return true;
    }

    private static bool TryAStar(
        Map map,
        (int x, int y) start,
        (int x, int y) end,
        bool[,] roomBuffer,
        (int x, int y) startDoor,
        (int x, int y) endDoor,
        HashSet<(int x, int y)>? temporaryBlocked,
        out List<(int x, int y)> path)
    {
        var open = new PriorityQueue<RouteNode, double>();
        var startNode = new RouteNode(start.x, start.y, 0);
        open.Enqueue(startNode, 0);

        var cameFrom = new Dictionary<RouteNode, RouteNode>();
        var gScore = new Dictionary<RouteNode, double> { [startNode] = 0 };

        while (open.TryDequeue(out var node, out _))
        {
            if (node.X == end.x && node.Y == end.y)
            {
                path = ReconstructPath(cameFrom, node).Select(n => (n.X, n.Y)).ToList();
                return true;
            }

            foreach (var (dx, dy, _) in CardinalDirections)
            {
                var nx = node.X + dx;
                var ny = node.Y + dy;
                if (temporaryBlocked?.Contains((nx, ny)) == true) continue;
                if (!IsWalkable(map, nx, ny, roomBuffer, startDoor, endDoor)) continue;

                var nextDir = DirectionFromDelta(dx, dy);
                var neighbor = new RouteNode(nx, ny, nextDir);
                var turnPenalty = node.Direction != 0 && node.Direction != nextDir ? 0.2 : 0;
                var existingPenalty = map.Cells[nx, ny] == CellType.Corridor ? -0.2 : 0;
                var tentative = gScore[node] + 1 + turnPenalty + existingPenalty;

                if (gScore.TryGetValue(neighbor, out var known) && tentative >= known) continue;

                cameFrom[neighbor] = node;
                gScore[neighbor] = tentative;
                var priority = tentative + Math.Abs(end.x - nx) + Math.Abs(end.y - ny);
                open.Enqueue(neighbor, priority);
            }
        }

        path = [];
        return false;
    }

    private static bool IsWalkable(Map map, int x, int y, bool[,] roomBuffer, (int x, int y) startDoor, (int x, int y) endDoor)
    {
        if (!IsInside(map, x, y)) return false;
        if ((x, y) == startDoor || (x, y) == endDoor) return true;
        if (roomBuffer[x, y]) return false;

        var cell = map.Cells[x, y];
        return cell switch
        {
            CellType.Empty => true,
            CellType.Corridor => true,
            CellType.Gate => true,
            _ => false
        };
    }

    private static bool IsInside(Map map, int x, int y)
        => x >= 0 && y >= 0 && x < map.WidthUnits && y < map.HeightUnits;

    private static List<RouteNode> ReconstructPath(Dictionary<RouteNode, RouteNode> cameFrom, RouteNode current)
    {
        var path = new List<RouteNode> { current };
        while (cameFrom.TryGetValue(current, out var prev))
        {
            current = prev;
            path.Add(current);
        }

        path.Reverse();
        return path;
    }

    private static int DirectionFromDelta(int dx, int dy) => (dx, dy) switch
    {
        (0, -1) => 1,
        (1, 0) => 2,
        (0, 1) => 3,
        (-1, 0) => 4,
        _ => 0
    };

    private static void AddDoor(Map map, Room room, DoorCandidate candidate)
    {
        if (room.Doors.Any(d => (int)d.Position.X == candidate.X && (int)d.Position.Y == candidate.Y)) return;

        var door = new Door
        {
            BlockId = room.BlockId,
            RoomId = room.Id,
            Position = new PointUnits(candidate.X, candidate.Y),
            Direction = candidate.Direction
        };

        room.Doors.Add(door);
        map.Doors.Add(door);
        map.Cells[candidate.X, candidate.Y] = CellType.Door;
    }

    private static double ManhattanDistance(PointUnits a, PointUnits b) => Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);

    private static void ValidateAndFix(Map map, GenerationSettings s, List<string> warnings, Random rng)
    {
        if (!s.ValidateConnectivity || map.Rooms.Count == 0) return;

        var reachable = GetReachableRooms(map);
        if (reachable.Count == map.Rooms.Count) return;

        warnings.Add($"Связность комнат нарушена: достижимо {reachable.Count} из {map.Rooms.Count}.");
        if (!s.AutoFixConnectivity) return;

        var roomBuffer = BuildRoomBufferMap(map, s.CorridorWidthUnits);
        var roomDoors = map.Rooms.ToDictionary(r => r.Id, r => BuildDoorCandidates(map, r));

        var pending = map.Rooms.Where(r => !reachable.Contains(r.Id)).ToList();
        foreach (var room in pending)
        {
            var nearestReachable = map.Rooms
                .Where(r => reachable.Contains(r.Id))
                .OrderBy(r => ManhattanDistance(r.RectUnits.Center, room.RectUnits.Center))
                .FirstOrDefault();

            if (nearestReachable is null) continue;
            if (TryConnectRoomsDoorToDoor(map, s, room, nearestReachable, roomDoors[room.Id], roomDoors[nearestReachable.Id], roomBuffer))
            {
                reachable.Add(room.Id);
            }
        }
    }

    private static HashSet<int> GetReachableRooms(Map map)
    {
        var visitedRooms = new HashSet<int>();
        if (map.Doors.Count == 0) return visitedRooms;

        var queue = new Queue<(int x, int y)>();
        var seen = new HashSet<(int x, int y)>();
        queue.Enqueue(((int)map.Doors[0].Position.X, (int)map.Doors[0].Position.Y));

        while (queue.Count > 0)
        {
            var (x, y) = queue.Dequeue();
            if (!seen.Add((x, y))) continue;

            foreach (var door in map.Doors)
            {
                if ((int)door.Position.X == x && (int)door.Position.Y == y) visitedRooms.Add(door.RoomId);
            }

            foreach (var (dx, dy, _) in CardinalDirections)
            {
                var nx = x + dx;
                var ny = y + dy;
                if (!IsInside(map, nx, ny)) continue;
                var cell = map.Cells[nx, ny];
                if (cell is CellType.Corridor or CellType.Door or CellType.Gate) queue.Enqueue((nx, ny));
            }
        }

        return visitedRooms;
    }

    private static void GenerateTrainLayout(Map map, GenerationSettings s, Random rng, GenerationContext context)
    {
        var carriageHeight = Math.Max(12, map.HeightUnits - 4);
        var carriageWidth = Math.Max(20, map.WidthUnits - 8);
        var carriage = new RectUnits(2, 2, carriageWidth, carriageHeight);
        var block = new Block { Id = 1, Bounds = carriage };
        map.Blocks.Add(block);

        var corridorY = (int)carriage.Y + 2;
        var corridorStartX = (int)carriage.X + 1;
        var corridorEndX = (int)carriage.Right - 2;

        var trunk = new List<PointUnits>
        {
            new(corridorStartX, corridorY),
            new(corridorEndX, corridorY)
        };
        map.Corridors.Add(new Corridor { Polyline = trunk, WidthUnits = s.CorridorWidthUnits, IsTech = false });
        RasterizePolyline(map, trunk, s.CorridorWidthUnits, CellType.Corridor, false);

        var gateLeft = new Gate { BlockId = block.Id, GateType = "Vestibule", Position = new PointUnits(corridorStartX, corridorY) };
        var gateRight = new Gate { BlockId = block.Id, GateType = "Vestibule", Position = new PointUnits(corridorEndX, corridorY) };
        map.Gates.Add(gateLeft);
        map.Gates.Add(gateRight);
        block.Gates.Add(gateLeft);
        block.Gates.Add(gateRight);

        var x = corridorStartX + 2;
        var roomWidth = 5;
        var roomHeight = Math.Max(4, carriageHeight - 6);
        while (x + roomWidth < corridorEndX)
        {
            var room = new Room
            {
                BlockId = block.Id,
                RectUnits = new RectUnits(x, corridorY + 2, roomWidth, roomHeight),
                RoomType = rng.NextDouble() < 0.2 ? RoomType.TechRoom : RoomType.Generic
            };
            context.AssignRoomIds(room);
            block.Rooms.Add(room);
            map.Rooms.Add(room);
            PaintRect(map, room.RectUnits, CellType.Floor);

            var doorX = (int)Math.Round(room.RectUnits.Center.X);
            var doorY = (int)room.RectUnits.Y - 1;
            var door = new Door
            {
                BlockId = block.Id,
                RoomId = room.Id,
                Position = new PointUnits(doorX, doorY),
                Direction = ConnectorDirection.North
            };
            room.Doors.Add(door);
            map.Doors.Add(door);
            MarkCell(map, door.Position, CellType.Door, force: true);

            x += roomWidth + 1;
        }
    }

    private static void RasterizePolyline(Map map, IReadOnlyList<PointUnits> polyline, double width, CellType type, bool allowOverwriteRooms)
    {
        if (polyline.Count < 2) return;
        for (int i = 0; i < polyline.Count - 1; i++)
        {
            var a = polyline[i];
            var b = polyline[i + 1];
            var steps = (int)Math.Max(Math.Abs(b.X - a.X), Math.Abs(b.Y - a.Y));
            for (int s = 0; s <= steps; s++)
            {
                var t = steps == 0 ? 0 : (double)s / steps;
                var x = a.X + (b.X - a.X) * t;
                var y = a.Y + (b.Y - a.Y) * t;
                FillRadius(map, x, y, width / 2.0, type, allowOverwriteRooms);
            }
        }
    }

    private static void FillRadius(Map map, double cx, double cy, double radius, CellType type, bool allowOverwriteRooms)
    {
        var r = (int)Math.Ceiling(radius);
        for (int y = -r; y <= r; y++)
        for (int x = -r; x <= r; x++)
        {
            var px = (int)Math.Round(cx + x);
            var py = (int)Math.Round(cy + y);
            if (px < 0 || py < 0 || px >= map.WidthUnits || py >= map.HeightUnits) continue;
            if (!allowOverwriteRooms && map.Cells[px, py] == CellType.Floor) continue;
            map.Cells[px, py] = type;
        }
    }

    private static void PaintRect(Map map, RectUnits rect, CellType type)
    {
        for (int y = (int)rect.Y; y < (int)rect.Bottom; y++)
        for (int x = (int)rect.X; x < (int)rect.Right; x++)
        {
            if (x <= 0 || y <= 0 || x >= map.WidthUnits || y >= map.HeightUnits) continue;
            map.Cells[x, y] = type;
        }
    }

    private static void MarkCell(Map map, PointUnits p, CellType type, bool force)
    {
        var x = (int)Math.Round(p.X);
        var y = (int)Math.Round(p.Y);
        if (x < 0 || y < 0 || x >= map.WidthUnits || y >= map.HeightUnits) return;
        if (!force && map.Cells[x, y] == CellType.Floor) return;
        map.Cells[x, y] = type;
    }
}
