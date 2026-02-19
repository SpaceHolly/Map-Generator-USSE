using MapGen.Core.Model;

namespace MapGen.WinForms.Rendering;

public sealed class MapCanvas : UserControl
{
    public Map? Map { get; set; }
    public bool ShowGrid { get; set; } = true;

    public MapCanvas()
    {
        DoubleBuffered = true;
        Dock = DockStyle.Fill;
        BackColor = Color.FromArgb(20, 20, 24);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (Map is null) return;

        var g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        var scale = Math.Min((float)Width / Map.WidthUnits, (float)Height / Map.HeightUnits);

        if (ShowGrid)
        {
            using var pen = new Pen(Color.FromArgb(20, 200, 200, 200), 1);
            for (int x = 0; x < Map.WidthUnits; x++) g.DrawLine(pen, x * scale, 0, x * scale, Map.HeightUnits * scale);
            for (int y = 0; y < Map.HeightUnits; y++) g.DrawLine(pen, 0, y * scale, Map.WidthUnits * scale, y * scale);
        }

        foreach (var block in Map.Blocks)
        {
            var r = new RectangleF((float)block.Bounds.X * scale, (float)block.Bounds.Y * scale, (float)block.Bounds.Width * scale, (float)block.Bounds.Height * scale);
            using var fill = new SolidBrush(Color.FromArgb(40, 80, 150, 255));
            g.FillRectangle(fill, r);
            g.DrawRectangle(Pens.SteelBlue, r.X, r.Y, r.Width, r.Height);
        }

        foreach (var room in Map.Rooms)
        {
            var rc = room.RoomType == RoomType.TechRoom ? Color.FromArgb(70, 255, 140, 0) : Color.FromArgb(50, 150, 255, 150);
            using var fill = new SolidBrush(rc);
            var r = new RectangleF((float)room.RectUnits.X * scale, (float)room.RectUnits.Y * scale, (float)room.RectUnits.Width * scale, (float)room.RectUnits.Height * scale);
            g.FillRectangle(fill, r);
            g.DrawRectangle(Pens.WhiteSmoke, r.X, r.Y, r.Width, r.Height);
        }

        foreach (var c in Map.Corridors)
        {
            using var pen = new Pen(c.IsTech ? Color.OrangeRed : Color.Goldenrod, (float)Math.Max(1, c.WidthUnits * scale));
            for (int i = 0; i < c.Polyline.Count - 1; i++)
            {
                var a = c.Polyline[i];
                var b = c.Polyline[i + 1];
                g.DrawLine(pen, (float)a.X * scale, (float)a.Y * scale, (float)b.X * scale, (float)b.Y * scale);
            }
        }

        foreach (var gate in Map.Gates)
        {
            g.FillEllipse(Brushes.Cyan, (float)gate.Position.X * scale - 3, (float)gate.Position.Y * scale - 3, 6, 6);
        }
        foreach (var door in Map.Doors)
        {
            g.FillRectangle(Brushes.Yellow, (float)door.Position.X * scale - 2, (float)door.Position.Y * scale - 2, 4, 4);
        }
    }
}
