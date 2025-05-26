using Silk.NET.Maths;

namespace TheAdventure;

public class Camera
{
    private int _x;
    private int _y;
    private Rectangle<int> _worldBounds = new();
    private bool _hasWorldBounds = false;

    public int X => _x;
    public int Y => _y;

    public readonly int Width;
    public readonly int Height;
    
    public Camera(int width, int height)
    {
        Width = width;
        Height = height;
    }
    
    public void SetWorldBounds(Rectangle<int> bounds)
    {
        var marginLeft = Width / 2;
        var marginTop = Height / 2;

        if (bounds.Size.X > Width && bounds.Size.Y > Height)
        {
            _worldBounds = new Rectangle<int>(
                bounds.Origin.X + marginLeft, 
                bounds.Origin.Y + marginTop, 
                bounds.Size.X - marginLeft * 2,
                bounds.Size.Y - marginTop * 2
            );
            _hasWorldBounds = true;
        }
        else
        {
            _hasWorldBounds = false;
        }
        
        _x = marginLeft;
        _y = marginTop;
    }
    
    public void LookAt(int x, int y)
    {
        if (_hasWorldBounds)
        {
            if (_worldBounds.Contains(new Vector2D<int>(x, y)))
            {
                _x = x;
                _y = y;
            }
            else
            {
                _x = Math.Max(_worldBounds.Origin.X, Math.Min(x, _worldBounds.Origin.X + _worldBounds.Size.X));
                _y = Math.Max(_worldBounds.Origin.Y, Math.Min(y, _worldBounds.Origin.Y + _worldBounds.Size.Y));
            }
        }
        else
        {
            _x = x;
            _y = y;
        }
    }

    public Rectangle<int> ToScreenCoordinates(Rectangle<int> rect)
    {
        return rect.GetTranslated(new Vector2D<int>(Width / 2 - X, Height / 2 - Y));
    }

    public Vector2D<int> ToWorldCoordinates(Vector2D<int> point)
    {
        return point - new Vector2D<int>(Width / 2 - X, Height / 2 - Y);
    }
}