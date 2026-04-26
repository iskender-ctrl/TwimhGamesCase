using System;
using TwimhGames.Puzzle.Grid;
using TwimhGames.Puzzle.StateMachine;

namespace TwimhGames.Puzzle.Events
{
    public sealed class GameEventBus
    {
        public event Action<GameState> StateChanged;
        public event Action<GridPosition, GridPosition> SwapStarted;
        public event Action<bool> SwapFinished;
        public event Action<int, int> MatchResolved;
        public event Action<GridPosition> SpecialTriggered;
        public event Action BoardStable;

        public void RaiseStateChanged(GameState state)
        {
            StateChanged?.Invoke(state);
        }

        public void RaiseSwapStarted(GridPosition from, GridPosition to)
        {
            SwapStarted?.Invoke(from, to);
        }

        public void RaiseSwapFinished(bool isLegal)
        {
            SwapFinished?.Invoke(isLegal);
        }

        public void RaiseMatchResolved(int cascadeStep, int clearCount)
        {
            MatchResolved?.Invoke(cascadeStep, clearCount);
        }

        public void RaiseSpecialTriggered(GridPosition position)
        {
            SpecialTriggered?.Invoke(position);
        }

        public void RaiseBoardStable()
        {
            BoardStable?.Invoke();
        }
    }
}
