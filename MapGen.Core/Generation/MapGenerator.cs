using MapGen.Core.Model;
using MapGen.Core.Settings;

namespace MapGen.Core.Generation;

public sealed class GenerationResult
{
    public required Map Map { get; init; }
    public List<string> Warnings { get; } = new();
}

public sealed class MapGenerator
{
    public GenerationResult Generate(GenerationSettings settings, int? seed = null)
    {
        var (s, warnings) = SettingsValidator.ValidateAndClamp(settings);
        var rng = new Random(seed ?? s.Seed);

        var map = BuildBaseArea(s);
        GenerateTrunks(map, s, rng);
        SplitBlocks(map, s, rng);
        PlaceGates(map, s, rng);
        PlaceRooms(map, s, rng);
        ConnectRooms(map, s);
        ValidateAndFix(map, s, warnings);

        var result = new GenerationResult { Map = map };
        result.Warnings.AddRange(warnings);
        return result;
    }

    private static Map BuildBaseArea(GenerationSettings s)
    {
        var map = new Map
        {
            WidthUnits = s.MapWidthUnits,
            HeightUnits = s.MapHeightUnits,
            GridStep = s.GridStep,
            Cells = new CellType[s.MapWidthUnits, s.MapHeightUnits],
            Entrance = new PointUnits(0, s.MapHeightUnits / 2.0)
        };
        return map;
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
            RasterizePolyline(map, c.Polyline, c.WidthUnits, CellType.Corridor);
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
                    _ => new PointUnits(rng.Next((int)block.Bounds.X, (int)block.Bounds.Right), block.Bounds.Bottom),
                };
                var gate = new Gate { BlockId = block.Id, Position = p, GateType = "Standard" };
                block.Gates.Add(gate);
                map.Gates.Add(gate);
                MarkCell(map, p, CellType.Gate);
            }
        }
    }

    private static void PlaceRooms(Map map, GenerationSettings s, Random rng)
    {
        if (map.Blocks.Count == 0) return;

        var targetRooms = rng.Next(s.RoomsTotalMin, s.RoomsTotalMax + 1);
        var techTarget = rng.Next(s.TechRoomsMin, s.TechRoomsMax + 1);
        var roomId = 1;

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

                var type = roomId <= techTarget ? RoomType.TechRoom : RoomType.Generic;
                var room = new Room { Id = roomId++, BlockId = block.Id, RectUnits = candidate, RoomType = type };
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

            var cand = candidate;
            if (localRooms.Any(r => Expand(r, s.PaddingUnits).Intersects(cand))) continue;

            localRooms.Add(candidate);
            return true;
        }

        return false;
    }

    private static RectUnits Expand(RectUnits r, int pad) => new(r.X - pad, r.Y - pad, r.Width + pad * 2, r.Height + pad * 2);

    private static void ConnectRooms(Map map, GenerationSettings s)
    {
        foreach (var block in map.Blocks)
        {
            if (block.Gates.Count == 0) continue;
            var gate = block.Gates[0];
            foreach (var room in block.Rooms)
            {
                var doorPos = new PointUnits(room.RectUnits.Center.X, room.RectUnits.Y);
                var door = new Door { BlockId = block.Id, RoomId = room.Id, Position = doorPos };
                room.Doors.Add(door);
                map.Doors.Add(door);
                MarkCell(map, doorPos, CellType.Door);

                // TODO: заменить на полноценный A*.
                var polyline = new List<PointUnits> { doorPos, new(gate.Position.X, doorPos.Y), gate.Position };
                map.Corridors.Add(new Corridor { Polyline = polyline, WidthUnits = s.CorridorWidthUnits, IsTech = room.RoomType == RoomType.TechRoom });
                RasterizePolyline(map, polyline, s.CorridorWidthUnits, CellType.Corridor);
            }
        }
    }

    private static void ValidateAndFix(Map map, GenerationSettings s, List<string> warnings)
    {
        if (!s.ValidateConnectivity) return;
        // MVP: проверка, что есть хотя бы один коридор и gate.
        if (map.Corridors.Count == 0)
        {
            warnings.Add("Нет коридоров после генерации.");
            if (s.AutoFixConnectivity)
            {
                var p1 = map.Entrance;
                var p2 = new PointUnits(map.WidthUnits - 1, map.Entrance.Y);
                var poly = new List<PointUnits> { p1, p2 };
                map.Corridors.Add(new Corridor { Polyline = poly, WidthUnits = 2, IsTech = false });
                RasterizePolyline(map, poly, 2, CellType.Corridor);
                warnings.Add("Добавлен аварийный коридор связности.");
            }
        }
    }

    private static void RasterizePolyline(Map map, IReadOnlyList<PointUnits> polyline, double width, CellType type)
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
                FillRadius(map, x, y, width / 2.0, type);
            }
        }
    }

    private static void FillRadius(Map map, double cx, double cy, double radius, CellType type)
    {
        var r = (int)Math.Ceiling(radius);
        for (int y = -r; y <= r; y++)
        for (int x = -r; x <= r; x++)
        {
            var px = (int)Math.Round(cx + x);
            var py = (int)Math.Round(cy + y);
            if (px < 0 || py < 0 || px >= map.WidthUnits || py >= map.HeightUnits) continue;
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

    private static void MarkCell(Map map, PointUnits p, CellType type)
    {
        var x = (int)Math.Round(p.X);
        var y = (int)Math.Round(p.Y);
        if (x >= 0 && y >= 0 && x < map.WidthUnits && y < map.HeightUnits) map.Cells[x, y] = type;
    }
}
