using System.Collections.Generic;
using System.Linq;
using TwimhGames.Puzzle.Config;
using TwimhGames.Puzzle.Grid;
using TwimhGames.Puzzle.Tiles;
using UnityEngine;

namespace TwimhGames.Puzzle.Core
{
    public sealed class SpecialTileResolver
    {
        private readonly Vector2Int _bombAreaSize;
        private readonly SpecialComboSettings _comboSettings;

        public SpecialTileResolver(Vector2Int bombAreaSize, SpecialComboSettings comboSettings)
        {
            _bombAreaSize = new Vector2Int(
                Mathf.Max(1, bombAreaSize.x),
                Mathf.Max(1, bombAreaSize.y));
            _comboSettings = comboSettings;
        }

        public DirectSpecialActivationPlan BuildDirectSwapActivation(BoardModel board, GridPosition from, GridPosition to)
        {
            var clearPositions = new HashSet<GridPosition>();
            var triggeredSourceSpecials = new List<GridPosition>(2);

            var fromTile = board.GetTile(from);
            var toTile = board.GetTile(to);

            if (fromTile != null)
            {
                clearPositions.Add(from);
                if (fromTile.IsSpecial)
                {
                    triggeredSourceSpecials.Add(from);
                }
            }

            if (toTile != null)
            {
                clearPositions.Add(to);
                if (toTile.IsSpecial)
                {
                    triggeredSourceSpecials.Add(to);
                }
            }

            if (fromTile == null || toTile == null)
            {
                return new DirectSpecialActivationPlan(clearPositions, triggeredSourceSpecials);
            }

            if (fromTile.IsSpecial && toTile.IsSpecial)
            {
                IncludeSpecialComboArea(board, from, to, fromTile, toTile, clearPositions);
                return new DirectSpecialActivationPlan(clearPositions, triggeredSourceSpecials);
            }

            var sourcePosition = fromTile.IsSpecial ? from : to;
            var sourceSpecialTile = fromTile.IsSpecial ? fromTile : toTile;
            var swappedTargetTile = fromTile.IsSpecial ? toTile : fromTile;

            switch (sourceSpecialTile.SpecialKind)
            {
                case SpecialTileKind.Lightning:
                    AddLightningArea(sourcePosition, board, clearPositions);
                    break;
                case SpecialTileKind.Bomb:
                    AddBombArea(sourcePosition, board, clearPositions, _bombAreaSize);
                    break;
                case SpecialTileKind.Color:
                    AddAllTilesOfKind(swappedTargetTile.Kind, board, clearPositions);
                    break;
            }

            return new DirectSpecialActivationPlan(clearPositions, triggeredSourceSpecials);
        }

        public SpecialResolutionResult ExpandWithChainReactions(
            BoardModel board,
            HashSet<GridPosition> initialClearPositions,
            IEnumerable<GridPosition> preTriggeredSpecialTiles = null)
        {
            var clearPositions = new HashSet<GridPosition>(initialClearPositions);
            var triggeredSpecials = preTriggeredSpecialTiles != null
                ? new List<GridPosition>(preTriggeredSpecialTiles)
                : new List<GridPosition>();

            var queued = new HashSet<GridPosition>();
            var processed = preTriggeredSpecialTiles != null
                ? new HashSet<GridPosition>(preTriggeredSpecialTiles)
                : new HashSet<GridPosition>();
            var queue = new Queue<GridPosition>();

            var initialSpecials = clearPositions
                .Where(pos =>
                {
                    var tile = board.GetTile(pos);
                    return tile != null && tile.SpecialKind != SpecialTileKind.None;
                })
                .OrderBy(pos => pos.Y)
                .ThenBy(pos => pos.X);

            foreach (var position in initialSpecials)
            {
                queue.Enqueue(position);
                queued.Add(position);
            }

            while (queue.Count > 0)
            {
                var specialPos = queue.Dequeue();
                if (!processed.Add(specialPos))
                {
                    continue;
                }

                var sourceSpecialTile = board.GetTile(specialPos);
                if (sourceSpecialTile == null || sourceSpecialTile.SpecialKind == SpecialTileKind.None)
                {
                    continue;
                }

                triggeredSpecials.Add(specialPos);

                switch (sourceSpecialTile.SpecialKind)
                {
                    case SpecialTileKind.Lightning:
                        IncludeLightningArea(specialPos, board, clearPositions, queued, processed, queue);
                        break;
                    case SpecialTileKind.Bomb:
                        IncludeBombArea(specialPos, board, clearPositions, queued, processed, queue, _bombAreaSize);
                        break;
                    case SpecialTileKind.Color:
                        IncludeColorArea(sourceSpecialTile.Kind, board, clearPositions, queued, processed, queue);
                        break;
                }
            }

            return new SpecialResolutionResult(clearPositions, triggeredSpecials);
        }

        private void IncludeSpecialComboArea(
            BoardModel board,
            GridPosition from,
            GridPosition to,
            TileModel fromTile,
            TileModel toTile,
            HashSet<GridPosition> clearPositions)
        {
            if (fromTile.SpecialKind == SpecialTileKind.Color || toTile.SpecialKind == SpecialTileKind.Color)
            {
                IncludeColorComboArea(board, from, to, fromTile, toTile, clearPositions);
                return;
            }

            if (fromTile.SpecialKind == SpecialTileKind.Bomb && toTile.SpecialKind == SpecialTileKind.Bomb)
            {
                var boostedBombArea = _comboSettings.ResolveBombBombArea(_bombAreaSize);
                AddBombArea(from, board, clearPositions, boostedBombArea);
                AddBombArea(to, board, clearPositions, boostedBombArea);
                return;
            }

            if (fromTile.SpecialKind == SpecialTileKind.Lightning && toTile.SpecialKind == SpecialTileKind.Lightning)
            {
                var lightningBands = _comboSettings.ResolveLightningLightningBands();
                AddCrossBand(from, board, clearPositions, lightningBands.x, lightningBands.y);
                AddCrossBand(to, board, clearPositions, lightningBands.x, lightningBands.y);
                return;
            }

            var mixedBands = _comboSettings.ResolveMixedCrossBands(_bombAreaSize);
            AddCrossBand(from, board, clearPositions, mixedBands.x, mixedBands.y);
            AddCrossBand(to, board, clearPositions, mixedBands.x, mixedBands.y);
        }

        private void IncludeColorComboArea(
            BoardModel board,
            GridPosition from,
            GridPosition to,
            TileModel fromTile,
            TileModel toTile,
            HashSet<GridPosition> clearPositions)
        {
            if (fromTile.SpecialKind == SpecialTileKind.Color && toTile.SpecialKind == SpecialTileKind.Color)
            {
                AddAllBoardTiles(board, clearPositions);
                return;
            }

            var colorIsFromSide = fromTile.SpecialKind == SpecialTileKind.Color;
            var targetTile = colorIsFromSide ? toTile : fromTile;
            var targetKind = targetTile.Kind;
            var targetPositions = CollectTilesOfKind(targetKind, board);

            AddPositions(targetPositions, clearPositions);

            switch (targetTile.SpecialKind)
            {
                case SpecialTileKind.Lightning:
                    for (var i = 0; i < targetPositions.Count; i++)
                    {
                        AddLightningArea(targetPositions[i], board, clearPositions);
                    }
                    break;
                case SpecialTileKind.Bomb:
                    for (var i = 0; i < targetPositions.Count; i++)
                    {
                        AddBombArea(targetPositions[i], board, clearPositions, _bombAreaSize);
                    }
                    break;
                case SpecialTileKind.Color:
                    AddAllBoardTiles(board, clearPositions);
                    break;
            }
        }

        private static void IncludeLightningArea(
            GridPosition sourcePosition,
            BoardModel board,
            HashSet<GridPosition> clearPositions,
            HashSet<GridPosition> queued,
            HashSet<GridPosition> processed,
            Queue<GridPosition> queue)
        {
            for (var x = 0; x < board.Width; x++)
            {
                IncludePosition(new GridPosition(x, sourcePosition.Y), board, clearPositions, queued, processed, queue);
            }

            for (var y = 0; y < board.Height; y++)
            {
                IncludePosition(new GridPosition(sourcePosition.X, y), board, clearPositions, queued, processed, queue);
            }
        }

        private static void AddLightningArea(
            GridPosition sourcePosition,
            BoardModel board,
            HashSet<GridPosition> clearPositions)
        {
            for (var x = 0; x < board.Width; x++)
            {
                AddIfOccupied(new GridPosition(x, sourcePosition.Y), board, clearPositions);
            }

            for (var y = 0; y < board.Height; y++)
            {
                AddIfOccupied(new GridPosition(sourcePosition.X, y), board, clearPositions);
            }
        }

        private static void IncludeBombArea(
            GridPosition sourcePosition,
            BoardModel board,
            HashSet<GridPosition> clearPositions,
            HashSet<GridPosition> queued,
            HashSet<GridPosition> processed,
            Queue<GridPosition> queue,
            Vector2Int bombAreaSize)
        {
            var horizontalRadius = bombAreaSize.x / 2;
            var verticalRadius = bombAreaSize.y / 2;

            for (var dy = -verticalRadius; dy <= verticalRadius; dy++)
            {
                for (var dx = -horizontalRadius; dx <= horizontalRadius; dx++)
                {
                    var target = new GridPosition(sourcePosition.X + dx, sourcePosition.Y + dy);
                    IncludePosition(target, board, clearPositions, queued, processed, queue);
                }
            }
        }

        private static void AddBombArea(
            GridPosition sourcePosition,
            BoardModel board,
            HashSet<GridPosition> clearPositions,
            Vector2Int bombAreaSize)
        {
            var horizontalRadius = bombAreaSize.x / 2;
            var verticalRadius = bombAreaSize.y / 2;

            for (var dy = -verticalRadius; dy <= verticalRadius; dy++)
            {
                for (var dx = -horizontalRadius; dx <= horizontalRadius; dx++)
                {
                    AddIfOccupied(new GridPosition(sourcePosition.X + dx, sourcePosition.Y + dy), board, clearPositions);
                }
            }
        }

        private static void IncludeColorArea(
            TileKind targetKind,
            BoardModel board,
            HashSet<GridPosition> clearPositions,
            HashSet<GridPosition> queued,
            HashSet<GridPosition> processed,
            Queue<GridPosition> queue)
        {
            for (var y = 0; y < board.Height; y++)
            {
                for (var x = 0; x < board.Width; x++)
                {
                    var position = new GridPosition(x, y);
                    var tile = board.GetTile(position);
                    if (tile == null || tile.Kind != targetKind)
                    {
                        continue;
                    }

                    IncludePosition(position, board, clearPositions, queued, processed, queue);
                }
            }
        }

        private static List<GridPosition> CollectTilesOfKind(TileKind targetKind, BoardModel board)
        {
            var positions = new List<GridPosition>();

            for (var y = 0; y < board.Height; y++)
            {
                for (var x = 0; x < board.Width; x++)
                {
                    var position = new GridPosition(x, y);
                    var tile = board.GetTile(position);
                    if (tile == null || tile.Kind != targetKind)
                    {
                        continue;
                    }

                    positions.Add(position);
                }
            }

            return positions;
        }

        private static void AddAllTilesOfKind(
            TileKind targetKind,
            BoardModel board,
            HashSet<GridPosition> clearPositions)
        {
            AddPositions(CollectTilesOfKind(targetKind, board), clearPositions);
        }

        private static void AddAllBoardTiles(BoardModel board, HashSet<GridPosition> clearPositions)
        {
            for (var y = 0; y < board.Height; y++)
            {
                for (var x = 0; x < board.Width; x++)
                {
                    AddIfOccupied(new GridPosition(x, y), board, clearPositions);
                }
            }
        }

        private static void AddCrossBand(
            GridPosition sourcePosition,
            BoardModel board,
            HashSet<GridPosition> clearPositions,
            int horizontalRadius,
            int verticalRadius)
        {
            for (var dy = -verticalRadius; dy <= verticalRadius; dy++)
            {
                var targetY = sourcePosition.Y + dy;
                for (var x = 0; x < board.Width; x++)
                {
                    AddIfOccupied(new GridPosition(x, targetY), board, clearPositions);
                }
            }

            for (var dx = -horizontalRadius; dx <= horizontalRadius; dx++)
            {
                var targetX = sourcePosition.X + dx;
                for (var y = 0; y < board.Height; y++)
                {
                    AddIfOccupied(new GridPosition(targetX, y), board, clearPositions);
                }
            }
        }

        private static void AddPositions(IEnumerable<GridPosition> positions, HashSet<GridPosition> clearPositions)
        {
            foreach (var position in positions)
            {
                clearPositions.Add(position);
            }
        }

        private static void AddIfOccupied(
            GridPosition position,
            BoardModel board,
            HashSet<GridPosition> clearPositions)
        {
            if (board.GetTile(position) == null)
            {
                return;
            }

            clearPositions.Add(position);
        }

        private static void IncludePosition(
            GridPosition position,
            BoardModel board,
            HashSet<GridPosition> clearPositions,
            HashSet<GridPosition> queued,
            HashSet<GridPosition> processed,
            Queue<GridPosition> queue)
        {
            var tile = board.GetTile(position);
            if (tile == null)
            {
                return;
            }

            clearPositions.Add(position);
            if (tile.SpecialKind == SpecialTileKind.None)
            {
                return;
            }

            if (processed.Contains(position))
            {
                return;
            }

            if (!queued.Add(position))
            {
                return;
            }

            queue.Enqueue(position);
        }
    }

    public sealed class SpecialResolutionResult
    {
        public HashSet<GridPosition> ClearPositions { get; }
        public List<GridPosition> TriggeredSpecialTiles { get; }

        public SpecialResolutionResult(HashSet<GridPosition> clearPositions, List<GridPosition> triggeredSpecialTiles)
        {
            ClearPositions = clearPositions;
            TriggeredSpecialTiles = triggeredSpecialTiles;
        }
    }

    public sealed class DirectSpecialActivationPlan
    {
        public HashSet<GridPosition> ClearPositions { get; }
        public List<GridPosition> TriggeredSourceSpecialTiles { get; }

        public DirectSpecialActivationPlan(
            HashSet<GridPosition> clearPositions,
            List<GridPosition> triggeredSourceSpecialTiles)
        {
            ClearPositions = clearPositions;
            TriggeredSourceSpecialTiles = triggeredSourceSpecialTiles;
        }
    }
}
