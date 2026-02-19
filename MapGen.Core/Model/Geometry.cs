namespace MapGen.Core.Model;

public readonly record struct PointUnits(double X, double Y);

public readonly record struct RectUnits(double X, double Y, double Width, double Height)
{
    public double Right => X + Width;
    public double Bottom => Y + Height;
    public PointUnits Center => new(X + Width / 2.0, Y + Height / 2.0);

    public bool Intersects(RectUnits other) => !(other.X >= Right || other.Right <= X || other.Y >= Bottom || other.Bottom <= Y);
}
