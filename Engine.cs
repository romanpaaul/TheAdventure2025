using System.Reflection;
using System.Text.Json;
using Silk.NET.Maths;
using TheAdventure.Models;
using TheAdventure.Models.Data;
using TheAdventure.Scripting;

namespace TheAdventure;

public class Engine
{
    private readonly GameRenderer _renderer;
    private readonly Input _input;
    private readonly ScriptEngine _scriptEngine = new();

    private readonly Dictionary<int, GameObject> _gameObjects = new();
    private readonly Dictionary<string, TileSet> _loadedTileSets = new();
    private readonly Dictionary<int, Tile> _tileIdMap = new();

    private Level _currentLevel = new();
    private PlayerObject? _player;
    private InfiniteTerrainManager? _terrainManager;

    private DateTimeOffset _lastUpdate = DateTimeOffset.Now;
    private bool _isPaused = false;
    private DateTimeOffset _lastPauseToggle = DateTimeOffset.MinValue;

    public Engine(GameRenderer renderer, Input input)
    {
        _renderer = renderer;
        _input = input;

        _input.OnMouseClick += (_, coords) => 
        {
            if (!_isPaused)
            {
                AddBomb(coords.x, coords.y);
            }
        };
    }

    public void SetupWorld()
    {
        _player = new(SpriteSheet.Load(_renderer, "Player.json", "Assets"), 100, 100);
        SetupInfiniteTerrain();

        _scriptEngine.LoadAll(Path.Combine("Assets", "Scripts"));
    }

    private void SetupInfiniteTerrain()
    {
        // Load existing level data first for tile information
        var levelContent = File.ReadAllText(Path.Combine("Assets", "terrain.tmj"));
        var level = JsonSerializer.Deserialize<Level>(levelContent);
        if (level == null)
        {
            throw new Exception("Failed to load level");
        }

        foreach (var tileSetRef in level.TileSets)
        {
            var tileSetContent = File.ReadAllText(Path.Combine("Assets", tileSetRef.Source));
            var tileSet = JsonSerializer.Deserialize<TileSet>(tileSetContent);
            if (tileSet == null)
            {
                throw new Exception("Failed to load tile set");
            }

            foreach (var tile in tileSet.Tiles)
            {
                tile.TextureId = _renderer.LoadTexture(Path.Combine("Assets", tile.Image), out _);
                _tileIdMap.Add(tile.Id!.Value, tile);
            }

            _loadedTileSets.Add(tileSet.Name, tileSet);
        }

        _currentLevel = level;
        
        int tileSize = level.TileWidth ?? 32;
        
        _terrainManager = new InfiniteTerrainManager(_tileIdMap, level, chunkSize: 32, tileSize: tileSize, renderDistance: 3);
        
        _renderer.SetWorldBounds(new Rectangle<int>(-10000000, -10000000, 20000000, 20000000));
    }



    public void ProcessFrame()
    {
        // Handle pause input first with debouncing
        if (_input.IsEscapePressed() && (DateTimeOffset.Now - _lastPauseToggle).TotalMilliseconds > 200)
        {
            _isPaused = !_isPaused;
            _lastPauseToggle = DateTimeOffset.Now;
        }

        // Don't process game logic if paused
        if (_isPaused)
        {
            return;
        }

        var currentTime = DateTimeOffset.Now;
        var msSinceLastFrame = (currentTime - _lastUpdate).TotalMilliseconds;
        _lastUpdate = currentTime;

        if (_player == null)
        {
            return;
        }

        double up = _input.IsUpPressed() ? 1.0 : 0.0;
        double down = _input.IsDownPressed() ? 1.0 : 0.0;
        double left = _input.IsLeftPressed() ? 1.0 : 0.0;
        double right = _input.IsRightPressed() ? 1.0 : 0.0;
        bool isAttacking = _input.IsKeyAPressed() && (up + down + left + right <= 1);
        bool addBomb = _input.IsKeyBPressed();

        _player.UpdatePosition(up, down, left, right, 48, 48, msSinceLastFrame);
        if (isAttacking)
        {
            _player.Attack();
        }
        
        _terrainManager?.UpdateChunks(_player.Position.X, _player.Position.Y);
        
        if (!_isPaused)
        {
            _scriptEngine.ExecuteAll(this);
        }

        if (addBomb)
        {
            AddBomb(_player.Position.X, _player.Position.Y, false);
        }
    }

    public void RenderFrame()
    {
        _renderer.SetDrawColor(0, 0, 0, 255);
        _renderer.ClearScreen();

        if (_player == null) return;

        var playerPosition = _player.Position;
        
        _renderer.CameraLookAt(playerPosition.X, playerPosition.Y);

        RenderInfiniteTerrain();
        RenderAllObjects();

        if (_isPaused)
        {
            RenderPauseOverlay();
        }

        _renderer.PresentFrame();
    }

    private void RenderInfiniteTerrain()
    {
        _terrainManager?.RenderTerrain(_renderer);
    }

    private void RenderPauseOverlay()
    {
        var screenSize = _renderer.GetScreenSize();
        var screenBounds = new Rectangle<int>(0, 0, screenSize.Width, screenSize.Height);
        
        _renderer.SetDrawColor(0, 0, 0, 128);
        _renderer.FillRect(screenBounds);
        
        RenderPauseText(screenSize.Width, screenSize.Height);
    }
    
    private void RenderPauseText(int screenWidth, int screenHeight)
    {
        int centerX = screenWidth / 2;
        int centerY = screenHeight / 2;
        
        _renderer.SetDrawColor(255, 255, 255, 255); // White text
        
        DrawSimpleText("GAME PAUSED", centerX - 60, centerY - 10);
        DrawSimpleText("Press ESC to resume", centerX - 80, centerY + 20);
    }
    
    private void DrawSimpleText(string text, int x, int y)
    {
        int charWidth = 8;
        int charHeight = 12;
        int spacing = 2;
        
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            int charX = x + i * (charWidth + spacing);
            
            switch (char.ToUpper(c))
            {
                case 'G':
                    DrawCharG(charX, y, charWidth, charHeight);
                    break;
                case 'A':
                    DrawCharA(charX, y, charWidth, charHeight);
                    break;
                case 'M':
                    DrawCharM(charX, y, charWidth, charHeight);
                    break;
                case 'E':
                    DrawCharE(charX, y, charWidth, charHeight);
                    break;
                case 'P':
                    DrawCharP(charX, y, charWidth, charHeight);
                    break;
                case 'U':
                    DrawCharU(charX, y, charWidth, charHeight);
                    break;
                case 'S':
                    DrawCharS(charX, y, charWidth, charHeight);
                    break;
                case 'D':
                    DrawCharD(charX, y, charWidth, charHeight);
                    break;
                case 'R':
                    DrawCharR(charX, y, charWidth, charHeight);
                    break;
                case 'T':
                    DrawCharT(charX, y, charWidth, charHeight);
                    break;
                case 'O':
                    DrawCharO(charX, y, charWidth, charHeight);
                    break;
                case 'C':
                    DrawCharC(charX, y, charWidth, charHeight);
                    break;
                case 'N':
                    DrawCharN(charX, y, charWidth, charHeight);
                    break;
                case 'I':
                    DrawCharI(charX, y, charWidth, charHeight);
                    break;
                case ' ':
                    break;
                default:
                    _renderer.FillRect(new Rectangle<int>(charX, y, charWidth, charHeight));
                    break;
            }
        }
    }
    
    private void DrawCharG(int x, int y, int w, int h)
    {
        _renderer.FillRect(new Rectangle<int>(x, y, w, 2)); // Top
        _renderer.FillRect(new Rectangle<int>(x, y, 2, h)); // Left
        _renderer.FillRect(new Rectangle<int>(x, y + h - 2, w, 2)); // Bottom
        _renderer.FillRect(new Rectangle<int>(x + w - 2, y + h/2, 2, h/2)); // Right bottom
        _renderer.FillRect(new Rectangle<int>(x + w/2, y + h/2, w/2, 2)); // Middle
    }
    
    private void DrawCharA(int x, int y, int w, int h)
    {
        _renderer.FillRect(new Rectangle<int>(x, y, w, 2)); // Top
        _renderer.FillRect(new Rectangle<int>(x, y, 2, h)); // Left
        _renderer.FillRect(new Rectangle<int>(x + w - 2, y, 2, h)); // Right
        _renderer.FillRect(new Rectangle<int>(x, y + h/2, w, 2)); // Middle
    }
    
    private void DrawCharM(int x, int y, int w, int h)
    {
        _renderer.FillRect(new Rectangle<int>(x, y, 2, h)); // Left
        _renderer.FillRect(new Rectangle<int>(x + w - 2, y, 2, h)); // Right
        _renderer.FillRect(new Rectangle<int>(x, y, w, 2)); // Top
        _renderer.FillRect(new Rectangle<int>(x + w/2 - 1, y, 2, h/2)); // Middle
    }
    
    private void DrawCharE(int x, int y, int w, int h)
    {
        _renderer.FillRect(new Rectangle<int>(x, y, 2, h)); // Left
        _renderer.FillRect(new Rectangle<int>(x, y, w, 2)); // Top
        _renderer.FillRect(new Rectangle<int>(x, y + h/2, w-2, 2)); // Middle
        _renderer.FillRect(new Rectangle<int>(x, y + h - 2, w, 2)); // Bottom
    }
    
    private void DrawCharP(int x, int y, int w, int h)
    {
        _renderer.FillRect(new Rectangle<int>(x, y, 2, h)); // Left
        _renderer.FillRect(new Rectangle<int>(x, y, w, 2)); // Top
        _renderer.FillRect(new Rectangle<int>(x + w - 2, y, 2, h/2)); // Right top
        _renderer.FillRect(new Rectangle<int>(x, y + h/2, w-2, 2)); // Middle
    }
    
    private void DrawCharU(int x, int y, int w, int h)
    {
        _renderer.FillRect(new Rectangle<int>(x, y, 2, h)); // Left
        _renderer.FillRect(new Rectangle<int>(x + w - 2, y, 2, h)); // Right
        _renderer.FillRect(new Rectangle<int>(x, y + h - 2, w, 2)); // Bottom
    }
    
    private void DrawCharS(int x, int y, int w, int h)
    {
        _renderer.FillRect(new Rectangle<int>(x, y, w, 2)); // Top
        _renderer.FillRect(new Rectangle<int>(x, y, 2, h/2)); // Left top
        _renderer.FillRect(new Rectangle<int>(x, y + h/2, w, 2)); // Middle
        _renderer.FillRect(new Rectangle<int>(x + w - 2, y + h/2, 2, h/2)); // Right bottom
        _renderer.FillRect(new Rectangle<int>(x, y + h - 2, w, 2)); // Bottom
    }
    
    private void DrawCharD(int x, int y, int w, int h)
    {
        _renderer.FillRect(new Rectangle<int>(x, y, 2, h)); // Left
        _renderer.FillRect(new Rectangle<int>(x, y, w-2, 2)); // Top
        _renderer.FillRect(new Rectangle<int>(x + w - 2, y + 2, 2, h-4)); // Right
        _renderer.FillRect(new Rectangle<int>(x, y + h - 2, w-2, 2)); // Bottom
    }
    
    private void DrawCharR(int x, int y, int w, int h)
    {
        _renderer.FillRect(new Rectangle<int>(x, y, 2, h)); // Left
        _renderer.FillRect(new Rectangle<int>(x, y, w, 2)); // Top
        _renderer.FillRect(new Rectangle<int>(x + w - 2, y, 2, h/2)); // Right top
        _renderer.FillRect(new Rectangle<int>(x, y + h/2, w-2, 2)); // Middle
        _renderer.FillRect(new Rectangle<int>(x + w/2, y + h/2, 2, h/2)); // Diagonal
    }
    
    private void DrawCharT(int x, int y, int w, int h)
    {
        _renderer.FillRect(new Rectangle<int>(x, y, w, 2)); // Top
        _renderer.FillRect(new Rectangle<int>(x + w/2 - 1, y, 2, h)); // Middle vertical
    }
    
    private void DrawCharO(int x, int y, int w, int h)
    {
        _renderer.FillRect(new Rectangle<int>(x, y, w, 2)); // Top
        _renderer.FillRect(new Rectangle<int>(x, y, 2, h)); // Left
        _renderer.FillRect(new Rectangle<int>(x + w - 2, y, 2, h)); // Right
        _renderer.FillRect(new Rectangle<int>(x, y + h - 2, w, 2)); // Bottom
    }
    
    private void DrawCharC(int x, int y, int w, int h)
    {
        _renderer.FillRect(new Rectangle<int>(x, y, w, 2)); // Top
        _renderer.FillRect(new Rectangle<int>(x, y, 2, h)); // Left
        _renderer.FillRect(new Rectangle<int>(x, y + h - 2, w, 2)); // Bottom
    }
    
    private void DrawCharN(int x, int y, int w, int h)
    {
        _renderer.FillRect(new Rectangle<int>(x, y, 2, h)); // Left
        _renderer.FillRect(new Rectangle<int>(x + w - 2, y, 2, h)); // Right
        _renderer.FillRect(new Rectangle<int>(x + 2, y + h/4, w/2, 2)); // Diagonal
    }
    
    private void DrawCharI(int x, int y, int w, int h)
    {
        _renderer.FillRect(new Rectangle<int>(x, y, w, 2)); // Top
        _renderer.FillRect(new Rectangle<int>(x + w/2 - 1, y, 2, h)); // Middle vertical
        _renderer.FillRect(new Rectangle<int>(x, y + h - 2, w, 2)); // Bottom
    }

    public void RenderAllObjects()
    {
        var toRemove = new List<int>();
        foreach (var gameObject in GetRenderables())
        {
            gameObject.Render(_renderer);
            
            if (!_isPaused && gameObject is TemporaryGameObject { IsExpired: true } tempGameObject)
            {
                toRemove.Add(tempGameObject.Id);
            }
        }

        if (!_isPaused)
        {
            foreach (var id in toRemove)
            {
                _gameObjects.Remove(id, out var gameObject);

                if (_player == null)
                {
                    continue;
                }

                var tempGameObject = (TemporaryGameObject)gameObject!;
                var deltaX = Math.Abs(_player.Position.X - tempGameObject.Position.X);
                var deltaY = Math.Abs(_player.Position.Y - tempGameObject.Position.Y);
                if (deltaX < 32 && deltaY < 32)
                {
                    _player.GameOver();
                }
            }
        }

        _player?.Render(_renderer);
    }

    public void RenderTerrain()
    {
        foreach (var currentLayer in _currentLevel.Layers)
        {
            for (int i = 0; i < _currentLevel.Width; ++i)
            {
                for (int j = 0; j < _currentLevel.Height; ++j)
                {
                    int? dataIndex = j * currentLayer.Width + i;
                    if (dataIndex == null)
                    {
                        continue;
                    }

                    var currentTileId = currentLayer.Data[dataIndex.Value] - 1;
                    if (currentTileId == null)
                    {
                        continue;
                    }

                    var currentTile = _tileIdMap[currentTileId.Value];

                    var tileWidth = currentTile.ImageWidth ?? 0;
                    var tileHeight = currentTile.ImageHeight ?? 0;

                    var sourceRect = new Rectangle<int>(0, 0, tileWidth, tileHeight);
                    var destRect = new Rectangle<int>(i * tileWidth, j * tileHeight, tileWidth, tileHeight);
                    _renderer.RenderTexture(currentTile.TextureId, sourceRect, destRect);
                }
            }
        }
    }

    public IEnumerable<RenderableGameObject> GetRenderables()
    {
        foreach (var gameObject in _gameObjects.Values)
        {
            if (gameObject is RenderableGameObject renderableGameObject)
            {
                yield return renderableGameObject;
            }
        }
    }

    public (int X, int Y) GetPlayerPosition()
    {
        return _player!.Position;
    }

    public void AddBomb(int X, int Y, bool translateCoordinates = true)
    {
        var worldCoords = translateCoordinates ? _renderer.ToWorldCoordinates(X, Y) : new Vector2D<int>(X, Y);

        SpriteSheet spriteSheet = SpriteSheet.Load(_renderer, "BombExploding.json", "Assets");
        spriteSheet.ActivateAnimation("Explode");

        TemporaryGameObject bomb = new(spriteSheet, 2.1, (worldCoords.X, worldCoords.Y));
        _gameObjects.Add(bomb.Id, bomb);
    }

    public bool IsPaused => _isPaused;
    
    public int GetLoadedChunkCount()
    {
        return _terrainManager?.GetLoadedChunkCount() ?? 0;
    }
}