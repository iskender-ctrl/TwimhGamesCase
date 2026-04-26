namespace TwimhGames.Puzzle.StateMachine
{
    public enum GameState
    {
        Locked = 0,
        Idle = 1,
        Swapping = 2,
        Resolving = 3,
        Dropping = 4,
        Refilling = 5,
        Cascading = 6,
        Shuffling = 7
    }
}
