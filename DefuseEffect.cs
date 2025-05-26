using Silk.NET.SDL;
using TheAdventure.Models;

namespace TheAdventure;

public class DefuseEffect : TemporaryGameObject
{
    private readonly int _maxSize = 20;
    private readonly DateTimeOffset _startTime;
    
    public DefuseEffect((int X, int Y) position) 
        : base(null!, 0.5, position) // 0.5 second effect
    {
        _startTime = DateTimeOffset.Now;
    }
    
    public override void Render(GameRenderer renderer)
    {
        var elapsed = (DateTimeOffset.Now - _startTime).TotalSeconds;
        var progress = elapsed / Ttl;
        
        if (progress > 1.0) return;
        
        // Create expanding circle effect
        var currentSize = (int)(_maxSize * progress);
        var alpha = (byte)(255 * (1.0 - progress)); // Fade out
        
        // Render green expanding circle
        renderer.SetDrawColor(0, 255, 0, alpha);
        
        // Draw circle using rectangles (simple approach)
        for (int angle = 0; angle < 360; angle += 10)
        {
            var radians = angle * Math.PI / 180.0;
            var x = Position.X + (int)(currentSize * Math.Cos(radians));
            var y = Position.Y + (int)(currentSize * Math.Sin(radians));
            
            renderer.FillRect(new Silk.NET.Maths.Rectangle<int>(x - 1, y - 1, 2, 2));
        }
    }
}