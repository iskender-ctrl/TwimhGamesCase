using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TwimhGames.Puzzle.Config;
using TwimhGames.Puzzle.Events;
using TwimhGames.Puzzle.Grid;
using TwimhGames.Puzzle.Match;
using TwimhGames.Puzzle.StateMachine;
using TwimhGames.Puzzle.Tiles;
using UnityEngine;

namespace TwimhGames.Puzzle.Core
{
    public sealed class BoardResolver
    {
        private readonly BoardManager _boardManager;
        private readonly MatchFinder _matchFinder;
        private readonly MoveFinder _moveFinder;
        private readonly SpecialTileResolver _specialTileResolver;
        private readonly GameStateMachine _stateMachine;
        private readonly GameEventBus _eventBus;
        private readonly BoardConfigSO _config;

        public BoardResolver(
            BoardManager boardManager,
            MatchFinder matchFinder,
            MoveFinder moveFinder,
            SpecialTileResolver specialTileResolver,
            GameStateMachine stateMachine,
            GameEventBus eventBus,
            BoardConfigSO config)
        {
            _boardManager = boardManager;
            _matchFinder = matchFinder;
            _moveFinder = moveFinder;
            _specialTileResolver = specialTileResolver;
            _stateMachine = stateMachine;
            _eventBus = eventBus;
            _config = config;
        }

        public IEnumerator ResolveBoardRoutine(
            List<MatchGroup> initialMatches,
            GridPosition? preferredSpecialSpawn,
            HashSet<GridPosition> forcedInitialClearPositions = null,
            IReadOnlyCollection<GridPosition> forcedTriggeredSpecialTiles = null)
        {
            var matches = initialMatches ?? new List<MatchGroup>();
            var firstCycle = true;
            var cascadeStep = 0;

            if (forcedInitialClearPositions != null && forcedInitialClearPositions.Count > 0)
            {
                _stateMachine.SetState(GameState.Resolving);

                var specialResult = _specialTileResolver.ExpandWithChainReactions(
                    _boardManager.Model,
                    new HashSet<GridPosition>(forcedInitialClearPositions),
                    forcedTriggeredSpecialTiles);

                for (var i = 0; i < specialResult.TriggeredSpecialTiles.Count; i++)
                {
                    _eventBus.RaiseSpecialTriggered(specialResult.TriggeredSpecialTiles[i]);
                }

                yield return _boardManager.ClearPositionsAnimated(
                    specialResult.ClearPositions,
                    _config.Timings.ClearAnimationDuration);
                _eventBus.RaiseMatchResolved(cascadeStep, specialResult.ClearPositions.Count);

                if (_config.Timings.ClearDelay > 0f)
                {
                    yield return new WaitForSeconds(_config.Timings.ClearDelay);
                }

                yield return RunDropRefillStep();

                matches = _matchFinder.FindMatches(_boardManager.Model);
                firstCycle = false;
                cascadeStep++;
            }

            while (matches.Count > 0)
            {
                _stateMachine.SetState(firstCycle ? GameState.Resolving : GameState.Cascading);

                var allowSpecialSpawns = firstCycle || _config.AllowCascadeSpecialSpawns;
                var resolvePlan = BuildResolvePlan(
                    matches,
                    firstCycle ? preferredSpecialSpawn : null,
                    allowSpecialSpawns: allowSpecialSpawns);

                for (var i = 0; i < resolvePlan.TriggeredSpecialTiles.Count; i++)
                {
                    _eventBus.RaiseSpecialTriggered(resolvePlan.TriggeredSpecialTiles[i]);
                }

                yield return _boardManager.ClearPositionsAnimated(
                    resolvePlan.ClearPositions,
                    _config.Timings.ClearAnimationDuration);

                for (var i = 0; i < resolvePlan.SpecialSpawns.Count; i++)
                {
                    var spawn = resolvePlan.SpecialSpawns[i];
                    _boardManager.SpawnSpecialTile(spawn.Position, spawn.TileKind, spawn.SpecialKind);
                }

                _eventBus.RaiseMatchResolved(cascadeStep, resolvePlan.ClearPositions.Count);

                if (_config.Timings.ClearDelay > 0f)
                {
                    yield return new WaitForSeconds(_config.Timings.ClearDelay);
                }

                yield return RunDropRefillStep();

                matches = _matchFinder.FindMatches(_boardManager.Model);
                firstCycle = false;
                cascadeStep++;
            }

            yield return EnsureValidMovesOrShuffle();

            _stateMachine.SetState(GameState.Idle);
            _eventBus.RaiseBoardStable();
        }

        private IEnumerator RunDropRefillStep()
        {
            _stateMachine.SetState(GameState.Dropping);
            var dropMoves = _boardManager.ApplyGravity();
            yield return _boardManager.AnimateMoves(dropMoves, _config.Timings.DropDuration, _config.Timings.DropDurationPerCell, _config.Timings.DropDurationMax);

            _stateMachine.SetState(GameState.Refilling);
            var refillMoves = _boardManager.Refill();
            yield return _boardManager.AnimateMoves(refillMoves, _config.Timings.RefillDuration, _config.Timings.RefillDurationPerCell, _config.Timings.RefillDurationMax);

            if (_config.Timings.CascadePause > 0f)
            {
                yield return new WaitForSeconds(_config.Timings.CascadePause);
            }
        }

        private IEnumerator EnsureValidMovesOrShuffle()
        {
            if (_moveFinder.HasAnyValidMove(_boardManager.Model, _matchFinder))
            {
                yield break;
            }

            if (_config.Timings.NoMoveShuffleDelay > 0f)
            {
                yield return new WaitForSeconds(_config.Timings.NoMoveShuffleDelay);
            }

            _stateMachine.SetState(GameState.Shuffling);
            var shuffleMoves = _boardManager.ShuffleBoard();
            yield return _boardManager.AnimateMoves(shuffleMoves, _config.Timings.ShuffleDuration);

            var matches = _matchFinder.FindMatches(_boardManager.Model);
            if (matches.Count > 0)
            {
                throw new System.InvalidOperationException(
                    "Shuffle generated immediate matches. Board generation must return a stable board.");
            }

            if (!_moveFinder.HasAnyValidMove(_boardManager.Model, _matchFinder))
            {
                throw new System.InvalidOperationException(
                    "Shuffle failed to produce a valid move. Check level layout and tile catalog.");
            }
        }

        private ResolvePlan BuildResolvePlan(
            List<MatchGroup> matches,
            GridPosition? preferredSpecialSpawn,
            bool allowSpecialSpawns)
        {
            var clearPositions = new HashSet<GridPosition>();
            for (var i = 0; i < matches.Count; i++)
            {
                var match = matches[i];
                for (var j = 0; j < match.Positions.Count; j++)
                {
                    clearPositions.Add(match.Positions[j]);
                }
            }

            var specialSpawns = allowSpecialSpawns
                ? BuildSpecialSpawns(matches, preferredSpecialSpawn)
                : new List<SpecialSpawnRequest>();
            var specialResult = _specialTileResolver.ExpandWithChainReactions(_boardManager.Model, clearPositions);

            return new ResolvePlan(
                specialResult.ClearPositions,
                specialSpawns,
                specialResult.TriggeredSpecialTiles
            );
        }

        private static List<SpecialSpawnRequest> BuildSpecialSpawns(List<MatchGroup> matches, GridPosition? preferredSpecialSpawn)
        {
            var spawns = new List<SpecialSpawnRequest>();
            var reservedPositions = new HashSet<GridPosition>();
            var bombPositions = new HashSet<GridPosition>();

            AddBombSpawns(matches, preferredSpecialSpawn, reservedPositions, spawns, bombPositions);
            AddColorSpawns(matches, preferredSpecialSpawn, reservedPositions, spawns, bombPositions);
            AddLightningSpawns(matches, preferredSpecialSpawn, reservedPositions, spawns, bombPositions);

            return spawns;
        }

        private static void AddBombSpawns(
            List<MatchGroup> matches,
            GridPosition? preferredSpecialSpawn,
            HashSet<GridPosition> reservedPositions,
            List<SpecialSpawnRequest> spawns,
            HashSet<GridPosition> bombPositions)
        {
            var axisMap = new Dictionary<GridPosition, MatchAxisFlags>();

            for (var i = 0; i < matches.Count; i++)
            {
                var match = matches[i];
                for (var j = 0; j < match.Positions.Count; j++)
                {
                    var position = match.Positions[j];
                    if (!axisMap.TryGetValue(position, out var flags))
                    {
                        flags = new MatchAxisFlags(match.TileKind);
                    }

                    if (flags.TileKind != match.TileKind)
                    {
                        continue;
                    }

                    if (match.Orientation == MatchOrientation.Horizontal)
                    {
                        flags.HasHorizontal = true;
                    }
                    else
                    {
                        flags.HasVertical = true;
                    }

                    axisMap[position] = flags;
                }
            }

            var bombCandidates = axisMap
                .Where(pair => pair.Value.HasHorizontal && pair.Value.HasVertical)
                .OrderBy(pair => pair.Key.Y)
                .ThenBy(pair => pair.Key.X)
                .ToList();

            if (bombCandidates.Count == 0)
            {
                return;
            }

            if (preferredSpecialSpawn.HasValue)
            {
                for (var i = 0; i < bombCandidates.Count; i++)
                {
                    var candidate = bombCandidates[i];
                    if (candidate.Key != preferredSpecialSpawn.Value)
                    {
                        continue;
                    }

                    if (!reservedPositions.Contains(candidate.Key))
                    {
                        reservedPositions.Add(candidate.Key);
                        bombPositions.Add(candidate.Key);
                        spawns.Add(new SpecialSpawnRequest(
                            candidate.Key,
                            candidate.Value.TileKind,
                            SpecialTileKind.Bomb));
                    }

                    break;
                }
            }

            for (var i = 0; i < bombCandidates.Count; i++)
            {
                var candidate = bombCandidates[i];
                if (reservedPositions.Contains(candidate.Key))
                {
                    continue;
                }

                reservedPositions.Add(candidate.Key);
                bombPositions.Add(candidate.Key);
                spawns.Add(new SpecialSpawnRequest(candidate.Key, candidate.Value.TileKind, SpecialTileKind.Bomb));
            }
        }

        private static void AddColorSpawns(
            List<MatchGroup> matches,
            GridPosition? preferredSpecialSpawn,
            HashSet<GridPosition> reservedPositions,
            List<SpecialSpawnRequest> spawns,
            HashSet<GridPosition> bombPositions)
        {
            for (var i = 0; i < matches.Count; i++)
            {
                var match = matches[i];
                if (match.Positions.Count < 5)
                {
                    continue;
                }

                if (ContainsAny(match.Positions, bombPositions))
                {
                    continue;
                }

                var spawnPosition = SelectSpawnPosition(match, preferredSpecialSpawn, reservedPositions);
                if (!spawnPosition.HasValue)
                {
                    continue;
                }

                reservedPositions.Add(spawnPosition.Value);
                spawns.Add(new SpecialSpawnRequest(spawnPosition.Value, match.TileKind, SpecialTileKind.Color));
            }
        }

        private static void AddLightningSpawns(
            List<MatchGroup> matches,
            GridPosition? preferredSpecialSpawn,
            HashSet<GridPosition> reservedPositions,
            List<SpecialSpawnRequest> spawns,
            HashSet<GridPosition> bombPositions)
        {
            for (var i = 0; i < matches.Count; i++)
            {
                var match = matches[i];
                if (match.Positions.Count != 4)
                {
                    continue;
                }

                if (ContainsAny(match.Positions, bombPositions))
                {
                    continue;
                }

                var spawnPosition = SelectSpawnPosition(match, preferredSpecialSpawn, reservedPositions);
                if (!spawnPosition.HasValue)
                {
                    continue;
                }

                reservedPositions.Add(spawnPosition.Value);
                spawns.Add(new SpecialSpawnRequest(spawnPosition.Value, match.TileKind, SpecialTileKind.Lightning));
            }
        }

        private static GridPosition? SelectSpawnPosition(
            MatchGroup match,
            GridPosition? preferredSpecialSpawn,
            HashSet<GridPosition> reservedPositions)
        {
            if (preferredSpecialSpawn.HasValue &&
                match.Contains(preferredSpecialSpawn.Value) &&
                !reservedPositions.Contains(preferredSpecialSpawn.Value))
            {
                return preferredSpecialSpawn.Value;
            }

            for (var i = 0; i < match.Positions.Count; i++)
            {
                var candidate = match.Positions[i];
                if (!reservedPositions.Contains(candidate))
                {
                    return candidate;
                }
            }

            return null;
        }

        private static bool ContainsAny(IReadOnlyList<GridPosition> positions, HashSet<GridPosition> blocked)
        {
            for (var i = 0; i < positions.Count; i++)
            {
                if (blocked.Contains(positions[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private sealed class ResolvePlan
        {
            public HashSet<GridPosition> ClearPositions { get; }
            public List<SpecialSpawnRequest> SpecialSpawns { get; }
            public List<GridPosition> TriggeredSpecialTiles { get; }

            public ResolvePlan(
                HashSet<GridPosition> clearPositions,
                List<SpecialSpawnRequest> specialSpawns,
                List<GridPosition> triggeredSpecialTiles)
            {
                ClearPositions = clearPositions;
                SpecialSpawns = specialSpawns;
                TriggeredSpecialTiles = triggeredSpecialTiles;
            }
        }

        private readonly struct SpecialSpawnRequest
        {
            public GridPosition Position { get; }
            public TileKind TileKind { get; }
            public SpecialTileKind SpecialKind { get; }

            public SpecialSpawnRequest(GridPosition position, TileKind tileKind, SpecialTileKind specialKind)
            {
                Position = position;
                TileKind = tileKind;
                SpecialKind = specialKind;
            }
        }

        private struct MatchAxisFlags
        {
            public TileKind TileKind;
            public bool HasHorizontal;
            public bool HasVertical;

            public MatchAxisFlags(TileKind tileKind)
            {
                TileKind = tileKind;
                HasHorizontal = false;
                HasVertical = false;
            }
        }
    }
}




