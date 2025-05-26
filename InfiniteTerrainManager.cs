using TheAdventure.Models.Data;
using TheAdventure.Models;

namespace TheAdventure;

public class InfiniteTerrainManager
{
    private readonly Dictionary<(int, int), TerrainChunk> _loadedChunks = new();
    private readonly Dictionary<int, Tile> _tileIdMap;
    private readonly Level _originalLevel;
    private readonly int _chunkSize;
    private readonly int _tileSize;
    private readonly int _renderDistance;
    
    public InfiniteTerrainManager(Dictionary<int, Tile> tileIdMap, Level originalLevel, int chunkSize = 32, int tileSize = 32, int renderDistance = 3)
    {
        _tileIdMap = tileIdMap;
        _originalLevel = originalLevel;
        _chunkSize = chunkSize;
        _tileSize = tileSize;
        _renderDistance = renderDistance;
    }
    
    public void UpdateChunks(int playerX, int playerY)
    {
        int playerChunkX = GetChunkCoordinate(playerX);
        int playerChunkY = GetChunkCoordinate(playerY);
        
        var chunksToKeep = new HashSet<(int, int)>();
        
        for (int chunkX = playerChunkX - _renderDistance; chunkX <= playerChunkX + _renderDistance; chunkX++)
        {
            for (int chunkY = playerChunkY - _renderDistance; chunkY <= playerChunkY + _renderDistance; chunkY++)
            {
                var chunkKey = (chunkX, chunkY);
                chunksToKeep.Add(chunkKey);
                
                if (!_loadedChunks.ContainsKey(chunkKey))
                {
                    var chunk = new TerrainChunk(chunkX, chunkY, _chunkSize);
                    chunk.GenerateTerrain(_tileIdMap, _originalLevel);
                    _loadedChunks[chunkKey] = chunk;
                }
            }
        }
        
        var chunksToRemove = _loadedChunks.Keys.Where(key => !chunksToKeep.Contains(key)).ToList();
        foreach (var chunkKey in chunksToRemove)
        {
            _loadedChunks.Remove(chunkKey);
        }
    }
    
    public void RenderTerrain(GameRenderer renderer)
    {
        foreach (var chunk in _loadedChunks.Values)
        {
            RenderChunk(chunk, renderer);
        }
    }
    
    private void RenderChunk(TerrainChunk chunk, GameRenderer renderer)
    {
        for (int layer = 0; layer < chunk.TileData.Count; layer++)
        {
            var layerData = chunk.TileData[layer];
            
            for (int i = 0; i < layerData.Count; i++)
            {
                var tileId = layerData[i];
                if (!tileId.HasValue) continue;
                
                int localX = i % chunk.ChunkSize;
                int localY = i / chunk.ChunkSize;
                
                int worldX = chunk.ChunkX * chunk.ChunkSize + localX;
                int worldY = chunk.ChunkY * chunk.ChunkSize + localY;
                
                if (_tileIdMap.TryGetValue(tileId.Value, out var tile))
                {
                    var actualTileWidth = tile.ImageWidth ?? (_originalLevel.TileWidth ?? _tileSize);
                    var actualTileHeight = tile.ImageHeight ?? (_originalLevel.TileHeight ?? _tileSize);
                    
                    var sourceRect = new Silk.NET.Maths.Rectangle<int>(0, 0, actualTileWidth, actualTileHeight);
                    var destRect = new Silk.NET.Maths.Rectangle<int>(
                        worldX * actualTileWidth, 
                        worldY * actualTileHeight, 
                        actualTileWidth, 
                        actualTileHeight
                    );
                    
                    renderer.RenderTexture(tile.TextureId, sourceRect, destRect);
                }
            }
        }
    }
    
    private int GetChunkCoordinate(int worldCoordinate)
    {
        int actualTileSize = _originalLevel.TileWidth ?? _tileSize;
        return (int)Math.Floor((double)worldCoordinate / (_chunkSize * actualTileSize));
    }
    
    public int GetLoadedChunkCount()
    {
        return _loadedChunks.Count;
    }
}