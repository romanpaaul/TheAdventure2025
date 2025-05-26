using TheAdventure.Models.Data;

namespace TheAdventure.Models;

public class TerrainChunk
{
    public int ChunkX { get; }
    public int ChunkY { get; }
    public int ChunkSize { get; }
    public List<List<int?>> TileData { get; private set; }
    public bool IsGenerated { get; private set; }
    
    public TerrainChunk(int chunkX, int chunkY, int chunkSize)
    {
        ChunkX = chunkX;
        ChunkY = chunkY;
        ChunkSize = chunkSize;
        TileData = new List<List<int?>>();
        IsGenerated = false;
    }
    
    public void GenerateTerrain(Dictionary<int, Tile> tileIdMap, Level originalLevel)
    {
        if (IsGenerated) return;
        
        TileData.Clear();

        bool useOriginalData = IsInOriginalLevel(originalLevel);
        
        if (useOriginalData)
        {
            GenerateFromOriginalLevel(originalLevel);
        }
        else
        {
            GenerateProceduralTerrain(tileIdMap);
        }
        
        IsGenerated = true;
    }
    
    private bool IsInOriginalLevel(Level originalLevel)
    {
        if (originalLevel.Width == null || originalLevel.Height == null || 
            originalLevel.TileWidth == null || originalLevel.TileHeight == null)
            return false;
            
        int originalWidthInTiles = originalLevel.Width.Value;
        int originalHeightInTiles = originalLevel.Height.Value;
        
        int chunkStartX = ChunkX * ChunkSize;
        int chunkEndX = chunkStartX + ChunkSize;
        int chunkStartY = ChunkY * ChunkSize;
        int chunkEndY = chunkStartY + ChunkSize;
        
        return chunkStartX < originalWidthInTiles && chunkEndX > 0 &&
               chunkStartY < originalHeightInTiles && chunkEndY > 0;
    }
    
    private void GenerateFromOriginalLevel(Level originalLevel)
    {
        foreach (var layer in originalLevel.Layers)
        {
            var layerData = new List<int?>();
            
            for (int y = 0; y < ChunkSize; y++)
            {
                for (int x = 0; x < ChunkSize; x++)
                {
                    int worldX = ChunkX * ChunkSize + x;
                    int worldY = ChunkY * ChunkSize + y;
                    
                    int? tileId = null;
                    
                    if (worldX >= 0 && worldX < originalLevel.Width && 
                        worldY >= 0 && worldY < originalLevel.Height)
                    {
                        int dataIndex = worldY * layer.Width.GetValueOrDefault() + worldX;
                        if (dataIndex < layer.Data.Count)
                        {
                            var originalTileId = layer.Data[dataIndex];
                            if (originalTileId.HasValue && originalTileId.Value > 0)
                            {
                                tileId = originalTileId.Value - 1;
                            }
                        }
                    }
                    
                    layerData.Add(tileId);
                }
            }
            
            TileData.Add(layerData);
        }
    }
    
    private void GenerateProceduralTerrain(Dictionary<int, Tile> tileIdMap)
    {
        var random = new Random(GetChunkSeed());
        
        for (int layer = 0; layer < 2; layer++)
        {
            var layerData = new List<int?>();
            
            for (int y = 0; y < ChunkSize; y++)
            {
                for (int x = 0; x < ChunkSize; x++)
                {
                    int? tileId = GenerateTileAt(x, y, layer, random, tileIdMap);
                    layerData.Add(tileId);
                }
            }
            
            TileData.Add(layerData);
        }
    }
    
    private int GetChunkSeed()
    {
        return (ChunkX * 73856093) ^ (ChunkY * 19349663);
    }
    
    private int? GenerateTileAt(int localX, int localY, int layer, Random random, Dictionary<int, Tile> tileIdMap)
    {
        int worldX = ChunkX * ChunkSize + localX;
        int worldY = ChunkY * ChunkSize + localY;
        
        if (layer == 0)
        {
            double noise = SimplexNoise(worldX * 0.05, worldY * 0.05);
            
            if (noise < -0.3)
            {
                return GetRandomTileId(random, new[] { 0, 1 }, tileIdMap);
            }
            else if (noise < 0.0)
            {
                return GetRandomTileId(random, new[] { 2, 3 }, tileIdMap);
            }
            else if (noise < 0.3)
            {
                return GetRandomTileId(random, new[] { 4, 5 }, tileIdMap);
            }
            else
            {
                return GetRandomTileId(random, new[] { 6, 7 }, tileIdMap);
            }
        }
        else
        {
            if (random.NextDouble() < 0.1)
            {
                return GetRandomTileId(random, new[] { 8, 9, 10 }, tileIdMap);
            }
            return null;
        }
    }
    
    private int? GetRandomTileId(Random random, int[] preferredIds, Dictionary<int, Tile> tileIdMap)
    {
        foreach (int id in preferredIds)
        {
            if (tileIdMap.ContainsKey(id))
                return id;
        }
        
        return tileIdMap.Keys.FirstOrDefault();
    }
    private double SimplexNoise(double x, double y)
    {
        return Math.Sin(x * 1.5) * Math.Cos(y * 1.2) + 
               Math.Sin(x * 2.1 + y * 1.8) * 0.5 +
               Math.Sin(x * 0.8 + y * 2.3) * 0.3;
    }
}