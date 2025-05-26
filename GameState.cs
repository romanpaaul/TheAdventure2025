namespace TheAdventure;

public class GameState
{
    public int Score { get; private set; } = 0;
    public int Lives { get; private set; } = 3;
    public bool IsGameOver => Lives <= 0;
    
    public void AddScore(int points)
    {
        Score += points;
    }
    
    public void LoseLife()
    {
        if (Lives > 0)
        {
            Lives--;
        }
    }
    
    public void Reset()
    {
        Score = 0;
        Lives = 3;
    }
    
    public void AddLife()
    {
        Lives++;
    }
}