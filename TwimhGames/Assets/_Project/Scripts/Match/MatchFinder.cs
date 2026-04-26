using System.Collections.Generic;
using TwimhGames.Puzzle.Core;
using TwimhGames.Puzzle.Grid;
using TwimhGames.Puzzle.Tiles;

namespace TwimhGames.Puzzle.Match
{
    public sealed class MatchFinder
    {
        public List<MatchGroup> FindMatches(BoardModel board)
        {
            var matches = new List<MatchGroup>();
            FindHorizontalMatches(board, matches);
            FindVerticalMatches(board, matches);
            return matches;
        }

        private static void FindHorizontalMatches(BoardModel board, List<MatchGroup> results)
        {
            for (var y = 0; y < board.Height; y++)
            {
                var x = 0;
                while (x < board.Width)
                {
                    var tile = board.GetTile(new GridPosition(x, y));
                    if (tile == null || tile.IsSpecial)
                    {
                        x++;
                        continue;
                    }

                    var kind = tile.Kind;
                    var startX = x;
                    x++;

                    while (x < board.Width)
                    {
                        var next = board.GetTile(new GridPosition(x, y));
                        if (next == null || next.IsSpecial || next.Kind != kind)
                        {
                            break;
                        }

                        x++;
                    }

                    var length = x - startX;
                    if (length < 3)
                    {
                        continue;
                    }

                    var positions = new List<GridPosition>(length);
                    for (var i = startX; i < x; i++)
                    {
                        positions.Add(new GridPosition(i, y));
                    }

                    results.Add(new MatchGroup(kind, MatchOrientation.Horizontal, positions));
                }
            }
        }

        private static void FindVerticalMatches(BoardModel board, List<MatchGroup> results)
        {
            for (var x = 0; x < board.Width; x++)
            {
                var y = 0;
                while (y < board.Height)
                {
                    var tile = board.GetTile(new GridPosition(x, y));
                    if (tile == null || tile.IsSpecial)
                    {
                        y++;
                        continue;
                    }

                    var kind = tile.Kind;
                    var startY = y;
                    y++;

                    while (y < board.Height)
                    {
                        var next = board.GetTile(new GridPosition(x, y));
                        if (next == null || next.IsSpecial || next.Kind != kind)
                        {
                            break;
                        }

                        y++;
                    }

                    var length = y - startY;
                    if (length < 3)
                    {
                        continue;
                    }

                    var positions = new List<GridPosition>(length);
                    for (var i = startY; i < y; i++)
                    {
                        positions.Add(new GridPosition(x, i));
                    }

                    results.Add(new MatchGroup(kind, MatchOrientation.Vertical, positions));
                }
            }
        }
    }

    public sealed class MoveFinder
    {
        public bool HasAnyValidMove(BoardModel board, MatchFinder matchFinder)
        {
            for (var y = 0; y < board.Height; y++)
            {
                for (var x = 0; x < board.Width; x++)
                {
                    var from = new GridPosition(x, y);
                    if (board.GetTile(from) == null)
                    {
                        continue;
                    }

                    if (TryEvaluateMove(board, matchFinder, from, new GridPosition(x + 1, y), out _))
                    {
                        return true;
                    }

                    if (TryEvaluateMove(board, matchFinder, from, new GridPosition(x, y + 1), out _))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public bool TryGetAnyValidMove(BoardModel board, MatchFinder matchFinder, out GridPosition from, out GridPosition to)
        {
            var hasBestMove = false;
            var bestScore = default(MoveScore);

            for (var y = 0; y < board.Height; y++)
            {
                for (var x = 0; x < board.Width; x++)
                {
                    var candidateFrom = new GridPosition(x, y);
                    var fromTile = board.GetTile(candidateFrom);
                    if (fromTile == null)
                    {
                        continue;
                    }

                    TrySelectBestNeighbor(
                        board,
                        matchFinder,
                        candidateFrom,
                        new GridPosition(x + 1, y),
                        ref hasBestMove,
                        ref bestScore,
                        out from,
                        out to);

                    TrySelectBestNeighbor(
                        board,
                        matchFinder,
                        candidateFrom,
                        new GridPosition(x, y + 1),
                        ref hasBestMove,
                        ref bestScore,
                        out from,
                        out to);
                }
            }

            if (hasBestMove)
            {
                from = bestScore.From;
                to = bestScore.To;
                return true;
            }

            from = default;
            to = default;
            return false;
        }

        private static void TrySelectBestNeighbor(
            BoardModel board,
            MatchFinder matchFinder,
            GridPosition from,
            GridPosition to,
            ref bool hasBestMove,
            ref MoveScore bestScore,
            out GridPosition bestFrom,
            out GridPosition bestTo)
        {
            bestFrom = hasBestMove ? bestScore.From : default;
            bestTo = hasBestMove ? bestScore.To : default;

            if (!TryEvaluateMove(board, matchFinder, from, to, out var candidateScore))
            {
                return;
            }

            if (hasBestMove && !candidateScore.IsBetterThan(bestScore))
            {
                return;
            }

            hasBestMove = true;
            bestScore = candidateScore;
            bestFrom = candidateScore.From;
            bestTo = candidateScore.To;
        }

        private static bool TryEvaluateMove(
            BoardModel board,
            MatchFinder matchFinder,
            GridPosition from,
            GridPosition to,
            out MoveScore score)
        {
            score = default;

            if (!board.IsPlayable(to))
            {
                return false;
            }

            var fromTile = board.GetTile(from);
            var toTile = board.GetTile(to);
            if (fromTile == null || toTile == null)
            {
                return false;
            }

            if (fromTile.IsSpecial || toTile.IsSpecial)
            {
                score = MoveScore.ForSpecialActivation(from, to, fromTile, toTile);
                return true;
            }

            if (fromTile.Kind == toTile.Kind)
            {
                return false;
            }

            board.SetTile(from, toTile);
            board.SetTile(to, fromTile);
            var matches = matchFinder.FindMatches(board);
            board.SetTile(from, fromTile);
            board.SetTile(to, toTile);

            if (matches.Count == 0)
            {
                return false;
            }

            score = MoveScore.ForMatchCreation(from, to, matches);
            return true;
        }

        private static MovePotential EvaluateMatchPotential(List<MatchGroup> matches)
        {
            var axisMap = new Dictionary<GridPosition, MatchAxisFlags>();
            var bombPositions = new HashSet<GridPosition>();
            var distinctPositions = new HashSet<GridPosition>();
            var colorCount = 0;
            var lightningCount = 0;

            for (var i = 0; i < matches.Count; i++)
            {
                var match = matches[i];
                for (var j = 0; j < match.Positions.Count; j++)
                {
                    var position = match.Positions[j];
                    distinctPositions.Add(position);

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

            foreach (var pair in axisMap)
            {
                if (pair.Value.HasHorizontal && pair.Value.HasVertical)
                {
                    bombPositions.Add(pair.Key);
                }
            }

            for (var i = 0; i < matches.Count; i++)
            {
                var match = matches[i];
                if (ContainsAny(match.Positions, bombPositions))
                {
                    continue;
                }

                if (match.Positions.Count >= 5)
                {
                    colorCount++;
                    continue;
                }

                if (match.Positions.Count == 4)
                {
                    lightningCount++;
                }
            }

            return new MovePotential(
                colorCount,
                bombPositions.Count,
                lightningCount,
                distinctPositions.Count,
                matches.Count);
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

        private readonly struct MovePotential
        {
            public int ColorCount { get; }
            public int BombCount { get; }
            public int LightningCount { get; }
            public int DistinctMatchTileCount { get; }
            public int MatchGroupCount { get; }

            public MovePotential(
                int colorCount,
                int bombCount,
                int lightningCount,
                int distinctMatchTileCount,
                int matchGroupCount)
            {
                ColorCount = colorCount;
                BombCount = bombCount;
                LightningCount = lightningCount;
                DistinctMatchTileCount = distinctMatchTileCount;
                MatchGroupCount = matchGroupCount;
            }
        }

        private readonly struct MoveScore
        {
            public GridPosition From { get; }
            public GridPosition To { get; }
            private int CreatedSpecialTier { get; }
            private int CreatedSpecialCount { get; }
            private int ActivatedSpecialCount { get; }
            private int DistinctMatchTileCount { get; }
            private int MatchGroupCount { get; }

            private MoveScore(
                GridPosition from,
                GridPosition to,
                int createdSpecialTier,
                int createdSpecialCount,
                int activatedSpecialCount,
                int distinctMatchTileCount,
                int matchGroupCount)
            {
                From = from;
                To = to;
                CreatedSpecialTier = createdSpecialTier;
                CreatedSpecialCount = createdSpecialCount;
                ActivatedSpecialCount = activatedSpecialCount;
                DistinctMatchTileCount = distinctMatchTileCount;
                MatchGroupCount = matchGroupCount;
            }

            public static MoveScore ForSpecialActivation(
                GridPosition from,
                GridPosition to,
                TileModel fromTile,
                TileModel toTile)
            {
                var activatedSpecialCount = 0;
                if (fromTile != null && fromTile.IsSpecial)
                {
                    activatedSpecialCount++;
                }

                if (toTile != null && toTile.IsSpecial)
                {
                    activatedSpecialCount++;
                }

                return new MoveScore(
                    from,
                    to,
                    createdSpecialTier: 0,
                    createdSpecialCount: 0,
                    activatedSpecialCount: activatedSpecialCount,
                    distinctMatchTileCount: 0,
                    matchGroupCount: 0);
            }

            public static MoveScore ForMatchCreation(GridPosition from, GridPosition to, List<MatchGroup> matches)
            {
                var potential = EvaluateMatchPotential(matches);
                var highestSpecialTier = 0;

                if (potential.ColorCount > 0)
                {
                    highestSpecialTier = 3;
                }
                else if (potential.BombCount > 0)
                {
                    highestSpecialTier = 2;
                }
                else if (potential.LightningCount > 0)
                {
                    highestSpecialTier = 1;
                }

                return new MoveScore(
                    from,
                    to,
                    createdSpecialTier: highestSpecialTier,
                    createdSpecialCount: potential.ColorCount + potential.BombCount + potential.LightningCount,
                    activatedSpecialCount: 0,
                    distinctMatchTileCount: potential.DistinctMatchTileCount,
                    matchGroupCount: potential.MatchGroupCount);
            }

            public bool IsBetterThan(MoveScore other)
            {
                if (CreatedSpecialTier != other.CreatedSpecialTier)
                {
                    return CreatedSpecialTier > other.CreatedSpecialTier;
                }

                if (CreatedSpecialCount != other.CreatedSpecialCount)
                {
                    return CreatedSpecialCount > other.CreatedSpecialCount;
                }

                if (ActivatedSpecialCount != other.ActivatedSpecialCount)
                {
                    return ActivatedSpecialCount > other.ActivatedSpecialCount;
                }

                if (DistinctMatchTileCount != other.DistinctMatchTileCount)
                {
                    return DistinctMatchTileCount > other.DistinctMatchTileCount;
                }

                if (MatchGroupCount != other.MatchGroupCount)
                {
                    return MatchGroupCount > other.MatchGroupCount;
                }

                return false;
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


