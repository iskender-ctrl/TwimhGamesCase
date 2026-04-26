using System.Collections;
using System.Collections.Generic;
using TwimhGames.Puzzle.Config;
using TwimhGames.Puzzle.Events;
using TwimhGames.Puzzle.Grid;
using TwimhGames.Puzzle.Match;
using TwimhGames.Puzzle.StateMachine;

namespace TwimhGames.Puzzle.Core
{
    public sealed class SwapController
    {
        private readonly BoardManager _boardManager;
        private readonly MatchFinder _matchFinder;
        private readonly BoardResolver _boardResolver;
        private readonly SpecialTileResolver _specialTileResolver;
        private readonly GameStateMachine _stateMachine;
        private readonly GameEventBus _eventBus;
        private readonly BoardConfigSO _config;

        public SwapController(
            BoardManager boardManager,
            MatchFinder matchFinder,
            BoardResolver boardResolver,
            SpecialTileResolver specialTileResolver,
            GameStateMachine stateMachine,
            GameEventBus eventBus,
            BoardConfigSO config)
        {
            _boardManager = boardManager;
            _matchFinder = matchFinder;
            _boardResolver = boardResolver;
            _specialTileResolver = specialTileResolver;
            _stateMachine = stateMachine;
            _eventBus = eventBus;
            _config = config;
        }

        public IEnumerator TrySwapRoutine(GridPosition from, GridPosition to, GridPosition focusPoint)
        {
            if (!_stateMachine.IsIn(GameState.Idle))
            {
                yield break;
            }

            if (!from.IsAdjacentTo(to))
            {
                yield break;
            }

            if (_boardManager.GetTile(from) == null || _boardManager.GetTile(to) == null)
            {
                yield break;
            }

            _stateMachine.SetState(GameState.Swapping);
            _eventBus.RaiseSwapStarted(from, to);

            _boardManager.SwapCells(from, to);
            yield return _boardManager.AnimateSwap(from, to, _config.Timings.SwapDuration);

            var swappedFromTile = _boardManager.GetTile(from);
            var swappedToTile = _boardManager.GetTile(to);
            var hasSpecialActivation = (swappedFromTile != null && swappedFromTile.IsSpecial) ||
                                       (swappedToTile != null && swappedToTile.IsSpecial);

            if (hasSpecialActivation)
            {
                var activationPlan = _specialTileResolver.BuildDirectSwapActivation(_boardManager.Model, from, to);
                var matchesFromSwap = _matchFinder.FindMatches(_boardManager.Model);
                var forcedClearPositions = new HashSet<GridPosition>(activationPlan.ClearPositions);

                for (var i = 0; i < matchesFromSwap.Count; i++)
                {
                    var match = matchesFromSwap[i];
                    for (var j = 0; j < match.Positions.Count; j++)
                    {
                        forcedClearPositions.Add(match.Positions[j]);
                    }
                }

                _eventBus.RaiseSwapFinished(true);
                yield return _boardResolver.ResolveBoardRoutine(
                    initialMatches: null,
                    preferredSpecialSpawn: null,
                    forcedInitialClearPositions: forcedClearPositions,
                    forcedTriggeredSpecialTiles: activationPlan.TriggeredSourceSpecialTiles);
                yield break;
            }

            var matches = _matchFinder.FindMatches(_boardManager.Model);
            if (matches.Count == 0)
            {
                _boardManager.SwapCells(from, to);
                yield return _boardManager.AnimateSwap(from, to, _config.Timings.IllegalSwapReturnDuration);

                _stateMachine.SetState(GameState.Idle);
                _eventBus.RaiseSwapFinished(false);
                yield break;
            }

            _eventBus.RaiseSwapFinished(true);
            var preferredSpecialSpawn = ResolvePreferredSpecialSpawn(matches, from, to, focusPoint);
            yield return _boardResolver.ResolveBoardRoutine(matches, preferredSpecialSpawn);
        }

        private static GridPosition? ResolvePreferredSpecialSpawn(
            List<MatchGroup> matches,
            GridPosition from,
            GridPosition to,
            GridPosition fallbackFocusPoint)
        {
            var fromScore = EvaluateSpecialSpawnScore(matches, from);
            var toScore = EvaluateSpecialSpawnScore(matches, to);

            if (fromScore == 0 && toScore == 0)
            {
                if (ContainsPosition(matches, fallbackFocusPoint))
                {
                    return fallbackFocusPoint;
                }

                if (ContainsPosition(matches, to))
                {
                    return to;
                }

                if (ContainsPosition(matches, from))
                {
                    return from;
                }

                return null;
            }

            if (fromScore != toScore)
            {
                return fromScore > toScore ? from : to;
            }

            if (EvaluateSpecialSpawnScore(matches, fallbackFocusPoint) == fromScore)
            {
                return fallbackFocusPoint;
            }

            return to;
        }

        private static int EvaluateSpecialSpawnScore(List<MatchGroup> matches, GridPosition position)
        {
            var hasHorizontal = false;
            var hasVertical = false;
            var hasColor = false;
            var hasLightning = false;

            for (var i = 0; i < matches.Count; i++)
            {
                var match = matches[i];
                if (!match.Contains(position))
                {
                    continue;
                }

                if (match.Orientation == MatchOrientation.Horizontal)
                {
                    hasHorizontal = true;
                }
                else
                {
                    hasVertical = true;
                }

                if (match.Positions.Count >= 5)
                {
                    hasColor = true;
                }
                else if (match.Positions.Count == 4)
                {
                    hasLightning = true;
                }
            }

            if (hasHorizontal && hasVertical)
            {
                return 3;
            }

            if (hasColor)
            {
                return 2;
            }

            if (hasLightning)
            {
                return 1;
            }

            return 0;
        }

        private static bool ContainsPosition(List<MatchGroup> matches, GridPosition position)
        {
            for (var i = 0; i < matches.Count; i++)
            {
                if (matches[i].Contains(position))
                {
                    return true;
                }
            }

            return false;
        }
    }
}

