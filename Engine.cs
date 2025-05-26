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
    private readonly GameState _gameState = new();

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
            // Don't add bombs when game is paused
            if (!_isPaused)
            {
                AddBomb(coords.x, coords.y);
            }
        };
    }

    public void SetupWorld()
    {
        _player = new(SpriteSheet.Load(_renderer, "Player.json", "Assets"), 100, 100);

        // Load basic tileset for infinite terrain
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

        // Load existing tilesets (keep the original terrain loading logic)
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

        // Store the original level for fallback
        _currentLevel = level;
        
        // Use the original level's tile size for consistency
        int tileSize = level.TileWidth ?? 32;
        
        // Initialize infinite terrain manager with the loaded tiles and original level
        _terrainManager = new InfiniteTerrainManager(_tileIdMap, level, chunkSize: 32, tileSize: tileSize, renderDistance: 3);
        
        // Set unlimited world bounds for infinite terrain (remove restrictive camera bounds)
        _renderer.SetWorldBounds(new Rectangle<int>(-10000000, -10000000, 20000000, 20000000));
    }



    public void ProcessFrame()
    {
        // Handle restart when game is over
        if (_gameState.IsGameOver && _input.IsKeyRPressed())
        {
            RestartGame();
            return;
        }
        
        // Handle pause input first with debouncing
        if (_input.IsEscapePressed() && (DateTimeOffset.Now - _lastPauseToggle).TotalMilliseconds > 200)
        {
            _isPaused = !_isPaused;
            _lastPauseToggle = DateTimeOffset.Now;
        }

        // Don't process game logic if paused or game over
        if (_isPaused || _gameState.IsGameOver)
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
        bool defuseBomb = _input.IsKeyEPressed();

        _player.UpdatePosition(up, down, left, right, 48, 48, msSinceLastFrame);
        if (isAttacking)
        {
            _player.Attack();
        }
        
        // Handle bomb defusing
        if (defuseBomb)
        {
            TryDefuseBomb();
        }
        
        // Update infinite terrain based on player position
        _terrainManager?.UpdateChunks(_player.Position.X, _player.Position.Y);
        
        // Only execute scripts if not paused
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
        
        // Make sure camera follows player
        _renderer.CameraLookAt(playerPosition.X, playerPosition.Y);

        // Render infinite terrain instead of static terrain
        RenderInfiniteTerrain();
        RenderAllObjects();
        
        // Render UI elements (score, lives)
        RenderUI();

        // Render pause overlay if paused
        if (_isPaused)
        {
            RenderPauseOverlay();
        }
        
        // Render game over screen if needed
        if (_gameState.IsGameOver)
        {
            RenderGameOverScreen();
        }

        _renderer.PresentFrame();
    }

    private void RenderInfiniteTerrain()
    {
        _terrainManager?.RenderTerrain(_renderer);
    }

    private void RenderPauseOverlay()
    {
        // Get actual screen dimensions for centering
        var screenSize = _renderer.GetScreenSize();
        var screenBounds = new Rectangle<int>(0, 0, screenSize.Width, screenSize.Height);
        
        // Render semi-transparent dark overlay
        _renderer.SetDrawColor(0, 0, 0, 128); // Semi-transparent black
        _renderer.FillRect(screenBounds);
        
        // Render "GAME PAUSED" text
        RenderPauseText(screenSize.Width, screenSize.Height);
    }
    
    private void RenderPauseText(int screenWidth, int screenHeight)
    {
        // Get center of screen
        int centerX = screenWidth / 2;
        int centerY = screenHeight / 2;
        
        // Create simple "PAUSED" text using rectangles (pixel art style)
        _renderer.SetDrawColor(255, 255, 255, 255); // White text
        
        // This is a very basic approach - you could load actual text sprites
        // or implement proper font rendering for better results
        DrawSimpleText("GAME PAUSED", centerX - 60, centerY - 10);
        DrawSimpleText("PRESS ESC TO RESUME", centerX - 90, centerY + 20);
    }
    
    private void DrawSimpleText(string text, int x, int y)
    {
        // Very basic text rendering using rectangles
        // Each character is represented as a simple pattern
        int charWidth = 8;
        int charHeight = 12;
        int spacing = 2;
        
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            int charX = x + i * (charWidth + spacing);
            
            // Draw simple character patterns
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
                case 'B':
                    DrawCharB(charX, y, charWidth, charHeight);
                    break;
                case 'F':
                    DrawCharF(charX, y, charWidth, charHeight);
                    break;
                case 'H':
                    DrawCharH(charX, y, charWidth, charHeight);
                    break;
                case 'L':
                    DrawCharL(charX, y, charWidth, charHeight);
                    break;
                case 'V':
                    DrawCharV(charX, y, charWidth, charHeight);
                    break;
                case 'W':
                    DrawCharW(charX, y, charWidth, charHeight);
                    break;
                case 'Y':
                    DrawCharY(charX, y, charWidth, charHeight);
                    break;
                case '0':
                    DrawChar0(charX, y, charWidth, charHeight);
                    break;
                case '1':
                    DrawChar1(charX, y, charWidth, charHeight);
                    break;
                case '2':
                    DrawChar2(charX, y, charWidth, charHeight);
                    break;
                case '3':
                    DrawChar3(charX, y, charWidth, charHeight);
                    break;
                case '4':
                    DrawChar4(charX, y, charWidth, charHeight);
                    break;
                case '5':
                    DrawChar5(charX, y, charWidth, charHeight);
                    break;
                case '6':
                    DrawChar6(charX, y, charWidth, charHeight);
                    break;
                case '7':
                    DrawChar7(charX, y, charWidth, charHeight);
                    break;
                case '8':
                    DrawChar8(charX, y, charWidth, charHeight);
                    break;
                case '9':
                    DrawChar9(charX, y, charWidth, charHeight);
                    break;
                case ' ':
                    // Space - do nothing
                    break;
                default:
                    // Unknown character - draw a simple rectangle
                    _renderer.FillRect(new Rectangle<int>(charX, y, charWidth, charHeight));
                    break;
            }
        }
    }
    
    // Simple character drawing methods using rectangles
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
    
    private void DrawCharB(int x, int y, int w, int h)
    {
        _renderer.FillRect(new Rectangle<int>(x, y, 2, h)); // Left
        _renderer.FillRect(new Rectangle<int>(x, y, w-2, 2)); // Top
        _renderer.FillRect(new Rectangle<int>(x, y + h/2, w-2, 2)); // Middle
        _renderer.FillRect(new Rectangle<int>(x, y + h - 2, w-2, 2)); // Bottom
        _renderer.FillRect(new Rectangle<int>(x + w - 2, y + 2, 2, h/2 - 4)); // Right top
        _renderer.FillRect(new Rectangle<int>(x + w - 2, y + h/2 + 2, 2, h/2 - 4)); // Right bottom
    }
    
    private void DrawCharF(int x, int y, int w, int h)
    {
        _renderer.FillRect(new Rectangle<int>(x, y, 2, h)); // Left
        _renderer.FillRect(new Rectangle<int>(x, y, w, 2)); // Top
        _renderer.FillRect(new Rectangle<int>(x, y + h/2, w-2, 2)); // Middle
    }
    
    private void DrawCharH(int x, int y, int w, int h)
    {
        _renderer.FillRect(new Rectangle<int>(x, y, 2, h)); // Left
        _renderer.FillRect(new Rectangle<int>(x + w - 2, y, 2, h)); // Right
        _renderer.FillRect(new Rectangle<int>(x, y + h/2, w, 2)); // Middle
    }
    
    private void DrawCharL(int x, int y, int w, int h)
    {
        _renderer.FillRect(new Rectangle<int>(x, y, 2, h)); // Left
        _renderer.FillRect(new Rectangle<int>(x, y + h - 2, w, 2)); // Bottom
    }
    
    private void DrawCharV(int x, int y, int w, int h)
    {
        _renderer.FillRect(new Rectangle<int>(x, y, 2, h * 3/4)); // Left
        _renderer.FillRect(new Rectangle<int>(x + w - 2, y, 2, h * 3/4)); // Right
        _renderer.FillRect(new Rectangle<int>(x + 2, y + h * 3/4, w/2 - 2, 2)); // Bottom left diagonal
        _renderer.FillRect(new Rectangle<int>(x + w/2, y + h * 3/4, w/2 - 2, 2)); // Bottom right diagonal
    }
    
    private void DrawCharW(int x, int y, int w, int h)
    {
        _renderer.FillRect(new Rectangle<int>(x, y, 2, h)); // Left
        _renderer.FillRect(new Rectangle<int>(x + w - 2, y, 2, h)); // Right
        _renderer.FillRect(new Rectangle<int>(x + w/2 - 1, y + h/2, 2, h/2)); // Middle
        _renderer.FillRect(new Rectangle<int>(x, y + h - 2, w, 2)); // Bottom
    }
    
    private void DrawCharY(int x, int y, int w, int h)
    {
        _renderer.FillRect(new Rectangle<int>(x, y, 2, h/2)); // Left top
        _renderer.FillRect(new Rectangle<int>(x + w - 2, y, 2, h/2)); // Right top
        _renderer.FillRect(new Rectangle<int>(x + w/2 - 1, y + h/2, 2, h/2)); // Middle bottom
        _renderer.FillRect(new Rectangle<int>(x + 2, y + h/2, w/2 - 3, 2)); // Left diagonal
        _renderer.FillRect(new Rectangle<int>(x + w/2 + 1, y + h/2, w/2 - 3, 2)); // Right diagonal
    }
    
    // Number drawing methods
    private void DrawChar0(int x, int y, int w, int h)
    {
        _renderer.FillRect(new Rectangle<int>(x, y, w, 2)); // Top
        _renderer.FillRect(new Rectangle<int>(x, y, 2, h)); // Left
        _renderer.FillRect(new Rectangle<int>(x + w - 2, y, 2, h)); // Right
        _renderer.FillRect(new Rectangle<int>(x, y + h - 2, w, 2)); // Bottom
    }
    
    private void DrawChar1(int x, int y, int w, int h)
    {
        _renderer.FillRect(new Rectangle<int>(x + w/2 - 1, y, 2, h)); // Middle vertical
        _renderer.FillRect(new Rectangle<int>(x, y + h - 2, w, 2)); // Bottom
    }
    
    private void DrawChar2(int x, int y, int w, int h)
    {
        _renderer.FillRect(new Rectangle<int>(x, y, w, 2)); // Top
        _renderer.FillRect(new Rectangle<int>(x + w - 2, y, 2, h/2)); // Right top
        _renderer.FillRect(new Rectangle<int>(x, y + h/2, w, 2)); // Middle
        _renderer.FillRect(new Rectangle<int>(x, y + h/2, 2, h/2)); // Left bottom
        _renderer.FillRect(new Rectangle<int>(x, y + h - 2, w, 2)); // Bottom
    }
    
    private void DrawChar3(int x, int y, int w, int h)
    {
        _renderer.FillRect(new Rectangle<int>(x, y, w, 2)); // Top
        _renderer.FillRect(new Rectangle<int>(x + w - 2, y, 2, h)); // Right
        _renderer.FillRect(new Rectangle<int>(x, y + h/2, w-2, 2)); // Middle
        _renderer.FillRect(new Rectangle<int>(x, y + h - 2, w, 2)); // Bottom
    }
    
    private void DrawChar4(int x, int y, int w, int h)
    {
        _renderer.FillRect(new Rectangle<int>(x, y, 2, h/2)); // Left top
        _renderer.FillRect(new Rectangle<int>(x + w - 2, y, 2, h)); // Right
        _renderer.FillRect(new Rectangle<int>(x, y + h/2, w, 2)); // Middle
    }
    
    private void DrawChar5(int x, int y, int w, int h)
    {
        _renderer.FillRect(new Rectangle<int>(x, y, w, 2)); // Top
        _renderer.FillRect(new Rectangle<int>(x, y, 2, h/2)); // Left top
        _renderer.FillRect(new Rectangle<int>(x, y + h/2, w, 2)); // Middle
        _renderer.FillRect(new Rectangle<int>(x + w - 2, y + h/2, 2, h/2)); // Right bottom
        _renderer.FillRect(new Rectangle<int>(x, y + h - 2, w, 2)); // Bottom
    }
    
    private void DrawChar6(int x, int y, int w, int h)
    {
        _renderer.FillRect(new Rectangle<int>(x, y, w, 2)); // Top
        _renderer.FillRect(new Rectangle<int>(x, y, 2, h)); // Left
        _renderer.FillRect(new Rectangle<int>(x, y + h/2, w, 2)); // Middle
        _renderer.FillRect(new Rectangle<int>(x + w - 2, y + h/2, 2, h/2)); // Right bottom
        _renderer.FillRect(new Rectangle<int>(x, y + h - 2, w, 2)); // Bottom
    }
    
    private void DrawChar7(int x, int y, int w, int h)
    {
        _renderer.FillRect(new Rectangle<int>(x, y, w, 2)); // Top
        _renderer.FillRect(new Rectangle<int>(x + w - 2, y, 2, h)); // Right
    }
    
    private void DrawChar8(int x, int y, int w, int h)
    {
        _renderer.FillRect(new Rectangle<int>(x, y, w, 2)); // Top
        _renderer.FillRect(new Rectangle<int>(x, y, 2, h)); // Left
        _renderer.FillRect(new Rectangle<int>(x + w - 2, y, 2, h)); // Right
        _renderer.FillRect(new Rectangle<int>(x, y + h/2, w, 2)); // Middle
        _renderer.FillRect(new Rectangle<int>(x, y + h - 2, w, 2)); // Bottom
    }
    
    private void DrawChar9(int x, int y, int w, int h)
    {
        _renderer.FillRect(new Rectangle<int>(x, y, w, 2)); // Top
        _renderer.FillRect(new Rectangle<int>(x, y, 2, h/2)); // Left top
        _renderer.FillRect(new Rectangle<int>(x + w - 2, y, 2, h)); // Right
        _renderer.FillRect(new Rectangle<int>(x, y + h/2, w, 2)); // Middle
        _renderer.FillRect(new Rectangle<int>(x, y + h - 2, w, 2)); // Bottom
    }

    public void RenderAllObjects()
    {
        var toRemove = new List<int>();
        foreach (var gameObject in GetRenderables())
        {
            gameObject.Render(_renderer);
            
            // Only process expiration and collision when not paused
            if (!_isPaused && gameObject is TemporaryGameObject { IsExpired: true } tempGameObject)
            {
                toRemove.Add(tempGameObject.Id);
            }
        }

        // Only remove expired objects when not paused
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
                    // Player takes damage - lose a life
                    _gameState.LoseLife();
                    
                    if (_gameState.IsGameOver)
                    {
                        _player.GameOver();
                    }
                }
            }
        }

        _player?.Render(_renderer);
    }

    public void RenderTerrain()
    {
        // Legacy method - now handled by RenderInfiniteTerrain()
        // Keeping for backward compatibility
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
    public GameState GameState => _gameState;
    
    public int GetLoadedChunkCount()
    {
        return _terrainManager?.GetLoadedChunkCount() ?? 0;
    }
    
    private void TryDefuseBomb()
    {
        if (_player == null) return;
        
        var playerPos = _player.Position;
        const int defuseRange = 50; // Distance within which player can defuse bombs
        
        // Find bombs within defuse range
        var bombsToDefuse = new List<int>();
        
        foreach (var gameObject in _gameObjects.Values)
        {
            if (gameObject is TemporaryGameObject bomb && !bomb.IsExpired)
            {
                var deltaX = Math.Abs(playerPos.X - bomb.Position.X);
                var deltaY = Math.Abs(playerPos.Y - bomb.Position.Y);
                var distance = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
                
                if (distance <= defuseRange)
                {
                    bombsToDefuse.Add(bomb.Id);
                }
            }
        }
        
        // Remove defused bombs and add score (only if we found bombs to defuse)
        if (bombsToDefuse.Count > 0)
        {
            foreach (var bombId in bombsToDefuse)
            {
                // Get bomb position before removing it
                if (_gameObjects.TryGetValue(bombId, out var bombObject) && bombObject is TemporaryGameObject bomb)
                {
                    // Add visual defuse effect
                    var defuseEffect = new DefuseEffect(bomb.Position);
                    _gameObjects.Add(defuseEffect.Id, defuseEffect);
                    
                    // Remove the bomb and add score
                    _gameObjects.Remove(bombId);
                    _gameState.AddScore(10);
                }
            }
        }
    }
    
    private void RenderUI()
    {
        var screenSize = _renderer.GetScreenSize();
        
        // Render score in top-left
        _renderer.SetDrawColor(255, 255, 255, 255); // White text
        DrawSimpleText($"SCORE {_gameState.Score}", 10, 10);
        
        // Render lives in top-right  
        DrawSimpleText($"LIVES {_gameState.Lives}", screenSize.Width - 100, 10);
        
        // Show defuse instruction
        DrawSimpleText("PRESS E NEAR BOMBS TO DEFUSE", 10, screenSize.Height - 30);
    }
    
    private void RenderGameOverScreen()
    {
        var screenSize = _renderer.GetScreenSize();
        var centerX = screenSize.Width / 2;
        var centerY = screenSize.Height / 2;
        
        // Render semi-transparent red overlay
        _renderer.SetDrawColor(128, 0, 0, 180); // Semi-transparent red
        _renderer.FillRect(new Rectangle<int>(0, 0, screenSize.Width, screenSize.Height));
        
        // Render game over text
        _renderer.SetDrawColor(255, 255, 255, 255); // White text
        DrawSimpleText("GAME OVER", centerX - 50, centerY - 30);
        DrawSimpleText($"FINAL SCORE {_gameState.Score}", centerX - 80, centerY);
        DrawSimpleText("PRESS R TO RESTART", centerX - 90, centerY + 30);
    }
    
    private void RestartGame()
    {
        // Reset game state
        _gameState.Reset();
        
        // Clear all game objects (bombs, etc.)
        _gameObjects.Clear();
        
        // Recreate player object completely to ensure clean state
        _player = new(SpriteSheet.Load(_renderer, "Player.json", "Assets"), 100, 100);
        
        // Unpause if paused
        _isPaused = false;
    }
}