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
        var context = new GenerationContext();

        var map = BuildBaseArea(s);

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
        }

        ConnectRooms(map, s, rng);
        ValidateAndFix(map, s, warnings);

        var result = new GenerationResult { Map = map };
        result.Warnings.AddRange(warnings);
        return result;
    }

    private static Map BuildBaseArea(GenerationSettings s)
    {
        return new Map
        {
            WidthUnits = s.MapWidthUnits,
            HeightUnits = s.MapHeightUnits,
            GridStep = s.GridStep,
            Cells = new CellType[s.MapWidthUnits, s.MapHeightUnits],
            Entrance = new PointUnits(0, s.MapHeightUnits / 2.0)
        };
    }

    private static void GenerateTrunks(Map map, GenerationSettings s, Random rng)
    {
        for (int i = 0; i < s.TrunksCount; i++)
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

        for (var i = 0; i < rects.Count; i++)
        {
            map.Blocks.Add(new Block { Id = i + 1, Bounds = rects[i] });
        }
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

        var targetRooms = rng.Next(s.RoomsTotalMin, s.RoomsTotalMax + 1);
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

                if (!TryPlaceRoomInBlock(block, localRooms[block.Id], s, rng, out var candidate))
                {
                    continue;
                }

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

            bool hasIntersection = false;
            foreach (var r in localRooms)
            {
                if (Expand(r, s.PaddingUnits).Intersects(candidate))
                {
                    hasIntersection = true;
                    break;
                }
            }
            
            if (hasIntersection)
                continue;

            localRooms.Add(candidate);
            return true;
        }

        return false;
    }

    private static RectUnits Expand(RectUnits r, int pad) => new(r.X - pad, r.Y - pad, r.Width + pad * 2, r.Height + pad * 2);

    private static void ConnectRooms(Map map, GenerationSettings s, Random rng)
    {
        if (map.Rooms.Count <= 1) return;

        var graphEdges = BuildMstWithExtraEdges(map.Rooms, rng);
        var corridors = new List<Corridor>();

        foreach (var (roomA, roomB) in graphEdges)
        {
            var start = PickConnectorPoint(roomA, roomB);
            var end = PickConnectorPoint(roomB, roomA);

            var startCell = ((int)Math.Round(start.X), (int)Math.Round(start.Y));
            var endCell = ((int)Math.Round(end.X), (int)Math.Round(end.Y));

            if (!TryBuildRoute(map, startCell, endCell, out var path))
            {
                continue;
            }

            var routePoints = path.Select(p => new PointUnits(p.x, p.y)).ToList();
            corridors.Add(new Corridor { Polyline = routePoints, WidthUnits = s.CorridorWidthUnits, IsTech = roomA.RoomType == RoomType.TechRoom || roomB.RoomType == RoomType.TechRoom });
            RasterizePolyline(map, routePoints, s.CorridorWidthUnits, CellType.Corridor, false);

            AddDoorForRoute(map, roomA, path[0], path.Count > 1 ? path[1] : path[0]);
            AddDoorForRoute(map, roomB, path[^1], path.Count > 1 ? path[^2] : path[^1]);
        }

        map.Corridors.AddRange(corridors);
    }

    private static List<(Room a, Room b)> BuildMstWithExtraEdges(List<Room> rooms, Random rng)
    {
        var connected = new HashSet<int> { 0 };
        var edges = new List<(int a, int b)>();

        while (connected.Count < rooms.Count)
        {
            var best = (-1, -1);
            var bestDistance = double.MaxValue;
            foreach (var a in connected)
            {
                for (var b = 0; b < rooms.Count; b++)
                {
                    if (connected.Contains(b)) continue;
                    var d = ManhattanDistance(rooms[a].RectUnits.Center, rooms[b].RectUnits.Center);
                    if (d < bestDistance)
                    {
                        bestDistance = d;
                        best = (a, b);
                    }
                }
            }

            if (best.Item1 < 0) break;
            edges.Add(best);
            connected.Add(best.Item2);
        }

        var extras = new List<(int a, int b)>();
        for (var i = 0; i < rooms.Count; i++)
        {
            for (var j = i + 1; j < rooms.Count; j++)
            {
                if (edges.Any(e => (e.a == i && e.b == j) || (e.a == j && e.b == i))) continue;
                extras.Add((i, j));
            }
        }

        var extraCount = (int)Math.Ceiling(extras.Count * (0.1 + rng.NextDouble() * 0.2));
        foreach (var extra in extras.OrderBy(_ => rng.Next()).Take(extraCount)) edges.Add(extra);

        return edges.Select(e => (rooms[e.a], rooms[e.b])).ToList();
    }

    private static PointUnits PickConnectorPoint(Room source, Room target)
    {
        var sourceCenter = source.RectUnits.Center;
        var targetCenter = target.RectUnits.Center;

        var dx = targetCenter.X - sourceCenter.X;
        var dy = targetCenter.Y - sourceCenter.Y;

        if (Math.Abs(dx) >= Math.Abs(dy))
        {
            var x = dx >= 0 ? source.RectUnits.Right : source.RectUnits.X;
            return new PointUnits(x, Math.Clamp(Math.Round(sourceCenter.Y), source.RectUnits.Y, source.RectUnits.Bottom - 1));
        }

        var y = dy >= 0 ? source.RectUnits.Bottom : source.RectUnits.Y;
        return new PointUnits(Math.Clamp(Math.Round(sourceCenter.X), source.RectUnits.X, source.RectUnits.Right - 1), y);
    }

    private static bool TryBuildRoute(Map map, (int x, int y) start, (int x, int y) end, out List<(int x, int y)> path)
    {
        if (TryBuildLRoute(map, start, end, out path)) return true;
        return TryAStar(map, start, end, out path);
    }

    private static bool TryBuildLRoute(Map map, (int x, int y) start, (int x, int y) end, out List<(int x, int y)> path)
    {
        var pivots = new[] { (start.x, end.y), (end.x, start.y) };
        foreach (var pivot in pivots)
        {
            if (!TryBuildStraightSegment(map, start, pivot, out var first)) continue;
            if (!TryBuildStraightSegment(map, pivot, end, out var second)) continue;

            first.AddRange(second.Skip(1));
            path = first;
            return true;
        }

        path = [];
        return false;
    }

    private static bool TryBuildStraightSegment(Map map, (int x, int y) a, (int x, int y) b, out List<(int x, int y)> segment)
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
            if (!IsWalkable(map, cx, cy, allowDoor: true)) return false;
            segment.Add((cx, cy));
        }

        return true;
    }

    private static bool TryAStar(Map map, (int x, int y) start, (int x, int y) end, out List<(int x, int y)> path)
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
                if (!IsWalkable(map, nx, ny, allowDoor: true)) continue;

                var nextDir = DirectionFromDelta(dx, dy);
                var neighbor = new RouteNode(nx, ny, nextDir);
                var turnPenalty = node.Direction != 0 && node.Direction != nextDir ? 0.2 : 0;
                var existingPenalty = map.Cells[nx, ny] == CellType.Corridor ? -0.1 : 0;
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

    private static int DirectionFromDelta(int dx, int dy)
    {
        return (dx, dy) switch
        {
            (0, -1) => 1,
            (1, 0) => 2,
            (0, 1) => 3,
            (-1, 0) => 4,
            _ => 0
        };
    }

    private static bool IsWalkable(Map map, int x, int y, bool allowDoor)
    {
        if (x < 0 || y < 0 || x >= map.WidthUnits || y >= map.HeightUnits) return false;
        var cell = map.Cells[x, y];
        return cell switch
        {
            CellType.Empty => true,
            CellType.Corridor => true,
            CellType.Gate => true,
            CellType.Door => allowDoor,
            _ => false
        };
    }

    private static void AddDoorForRoute(Map map, Room room, (int x, int y) doorCell, (int x, int y) nextCell)
    {
        if (room.Doors.Any(d => (int)d.Position.X == doorCell.x && (int)d.Position.Y == doorCell.y)) return;

        var direction = ComputeDirection(doorCell, nextCell);
        var door = new Door
        {
            BlockId = room.BlockId,
            RoomId = room.Id,
            Position = new PointUnits(doorCell.x, doorCell.y),
            Direction = direction
        };

        room.Doors.Add(door);
        map.Doors.Add(door);
        MarkCell(map, door.Position, CellType.Door, force: true);
    }

    private static ConnectorDirection ComputeDirection((int x, int y) from, (int x, int y) to)
    {
        var dx = to.x - from.x;
        var dy = to.y - from.y;
        return (dx, dy) switch
        {
            (0, -1) => ConnectorDirection.North,
            (1, 0) => ConnectorDirection.East,
            (0, 1) => ConnectorDirection.South,
            (-1, 0) => ConnectorDirection.West,
            _ => ConnectorDirection.None
        };
    }

    private static double ManhattanDistance(PointUnits a, PointUnits b) => Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);

    private static void ValidateAndFix(Map map, GenerationSettings s, List<string> warnings)
    {
        if (!s.ValidateConnectivity || map.Rooms.Count == 0) return;

        var roomById = map.Rooms.ToDictionary(r => r.Id);
        var doorMap = map.Doors.Where(d => roomById.ContainsKey(d.RoomId)).GroupBy(d => d.RoomId).ToDictionary(g => g.Key, g => g.ToList());
        var visitedRooms = new HashSet<int>();
        var queue = new Queue<(int x, int y)>();
        var visitedCells = new HashSet<(int x, int y)>();

        var seedDoors = doorMap.Values.SelectMany(d => d).ToList();
        if (seedDoors.Count == 0)
        {
            warnings.Add("Нет дверей для проверки связности комнат.");
            return;
        }

        var startDoor = seedDoors[0];
        queue.Enqueue(((int)startDoor.Position.X, (int)startDoor.Position.Y));

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (!visitedCells.Add(current)) continue;

            foreach (var door in map.Doors)
            {
                if ((int)door.Position.X == current.x && (int)door.Position.Y == current.y)
                {
                    visitedRooms.Add(door.RoomId);
                }
            }

            foreach (var (dx, dy, _) in CardinalDirections)
            {
                var nx = current.x + dx;
                var ny = current.y + dy;
                if (!IsWalkable(map, nx, ny, allowDoor: true)) continue;
                queue.Enqueue((nx, ny));
            }
        }

        if (visitedRooms.Count == map.Rooms.Count) return;

        warnings.Add($"Связность комнат нарушена: достижимо {visitedRooms.Count} из {map.Rooms.Count}.");
        if (!s.AutoFixConnectivity) return;

        var unreachable = map.Rooms.Where(r => !visitedRooms.Contains(r.Id)).ToList();
        foreach (var room in unreachable)
        {
            var nearestReachable = map.Rooms.Where(r => visitedRooms.Contains(r.Id)).OrderBy(r => ManhattanDistance(r.RectUnits.Center, room.RectUnits.Center)).FirstOrDefault();
            if (nearestReachable is null) continue;

            var start = PickConnectorPoint(room, nearestReachable);
            var end = PickConnectorPoint(nearestReachable, room);
            if (!TryBuildRoute(map, ((int)start.X, (int)start.Y), ((int)end.X, (int)end.Y), out var path)) continue;

            var poly = path.Select(p => new PointUnits(p.x, p.y)).ToList();
            map.Corridors.Add(new Corridor { Polyline = poly, WidthUnits = s.CorridorWidthUnits, IsTech = room.RoomType == RoomType.TechRoom });
            RasterizePolyline(map, poly, s.CorridorWidthUnits, CellType.Corridor, false);
            AddDoorForRoute(map, room, path[0], path.Count > 1 ? path[1] : path[0]);
            AddDoorForRoute(map, nearestReachable, path[^1], path.Count > 1 ? path[^2] : path[^1]);
            visitedRooms.Add(room.Id);
        }
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
            var doorY = (int)room.RectUnits.Y;
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
