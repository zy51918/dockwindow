namespace DockWindow;

public readonly record struct Rect(int X, int Y, int Width, int Height)
{
    public int Left   => X;
    public int Top    => Y;
    public int Right  => X + Width;
    public int Bottom => Y + Height;

    public bool Contains(int px, int py) =>
        px >= Left && px < Right && py >= Top && py < Bottom;
}

public enum Edge { None, Left, Right, Top }
