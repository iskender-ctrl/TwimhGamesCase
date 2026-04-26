using System;

namespace TwimhGames.Puzzle.StateMachine
{
    public sealed class GameStateMachine
    {
        public event Action<GameState> StateChanged;

        public GameState CurrentState { get; private set; } = GameState.Locked;

        public bool IsIn(GameState state)
        {
            return CurrentState == state;
        }

        public void SetState(GameState newState)
        {
            if (CurrentState == newState)
            {
                return;
            }

            CurrentState = newState;
            StateChanged?.Invoke(newState);
        }
    }
}
