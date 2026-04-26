using System;
using System.Collections.Generic;
using TwimhGames.Puzzle.Config;
using TwimhGames.Puzzle.Grid;
using TwimhGames.Puzzle.Match;
using TwimhGames.Puzzle.Tiles;

namespace TwimhGames.Puzzle.Core
{
    public sealed class PlayableBoardGenerator
    {
        private readonly TileCatalogSO _tileCatalog;
        private readonly MatchFinder _matchFinder;
        private readonly MoveFinder _moveFinder;
        private readonly BoardGenerationSettings _generationSettings;
        private readonly List<TileKind> _candidateKinds = new List<TileKind>();
        private readonly HashSet<TileKind> _blockedKinds = new HashSet<TileKind>();

        public PlayableBoardGenerator(
            TileCatalogSO tileCatalog,
            MatchFinder matchFinder,
            MoveFinder moveFinder,
            BoardGenerationSettings generationSettings)
        {
            _tileCatalog = tileCatalog;
            _matchFinder = matchFinder;
            _moveFinder = moveFinder;
            _generationSettings = generationSettings;
        }

        public TileKind[,] Generate(BoardModel boardTemplate, Random random, bool avoidImmediateMatches)
        {
            if (boardTemplate == null)
            {
                throw new ArgumentNullException(nameof(boardTemplate));
            }

            if (random == null)
            {
                throw new ArgumentNullException(nameof(random));
            }

            _tileCatalog.RebuildCache();
            if (_tileCatalog.Definitions.Count == 0)
            {
                throw new InvalidOperationException("Tile catalog must contain at least one tile definition.");
            }

            var playableMask = CopyPlayableMask(boardTemplate);
            var maxAttempts = Math.Max(1, _generationSettings.MaxGenerationAttempts);

            for (var attempt = 0; attempt < maxAttempts; attempt++)
            {
                var tempBoard = new BoardModel(boardTemplate.Width, boardTemplate.Height, playableMask);
                var generatedKinds = new TileKind[boardTemplate.Width, boardTemplate.Height];

                FillBoard(tempBoard, generatedKinds, random, avoidImmediateMatches);

                if (_moveFinder.HasAnyValidMove(tempBoard, _matchFinder))
                {
                    return generatedKinds;
                }
            }

            throw new InvalidOperationException(
                "Unable to generate a playable board with the current level layout and tile catalog.");
        }

        private void FillBoard(BoardModel board, TileKind[,] generatedKinds, Random random, bool avoidImmediateMatches)
        {
            for (var y = 0; y < board.Height; y++)
            {
                for (var x = 0; x < board.Width; x++)
                {
                    var position = new GridPosition(x, y);
                    if (!board.IsPlayable(position))
                    {
                        continue;
                    }

                    var kind = avoidImmediateMatches
                        ? ChooseKindAvoidingImmediateMatches(board, x, y, random)
                        : _tileCatalog.GetRandomKind(random);

                    generatedKinds[x, y] = kind;
                    board.SetTile(position, new TileModel(kind));
                }
            }
        }

        private TileKind ChooseKindAvoidingImmediateMatches(BoardModel board, int x, int y, Random random)
        {
            _blockedKinds.Clear();
            _candidateKinds.Clear();

            if (x >= 2)
            {
                var left1 = board.GetTile(new GridPosition(x - 1, y));
                var left2 = board.GetTile(new GridPosition(x - 2, y));
                if (left1 != null && left2 != null && left1.Kind == left2.Kind)
                {
                    _blockedKinds.Add(left1.Kind);
                }
            }

            if (y >= 2)
            {
                var down1 = board.GetTile(new GridPosition(x, y - 1));
                var down2 = board.GetTile(new GridPosition(x, y - 2));
                if (down1 != null && down2 != null && down1.Kind == down2.Kind)
                {
                    _blockedKinds.Add(down1.Kind);
                }
            }

            for (var i = 0; i < _tileCatalog.Definitions.Count; i++)
            {
                var kind = _tileCatalog.Definitions[i].Kind;
                if (_blockedKinds.Contains(kind))
                {
                    continue;
                }

                _candidateKinds.Add(kind);
            }

            if (_candidateKinds.Count == 0)
            {
                return _tileCatalog.GetRandomKind(random);
            }

            return _candidateKinds[random.Next(0, _candidateKinds.Count)];
        }

        private static bool[,] CopyPlayableMask(BoardModel board)
        {
            var mask = new bool[board.Width, board.Height];

            for (var y = 0; y < board.Height; y++)
            {
                for (var x = 0; x < board.Width; x++)
                {
                    mask[x, y] = board.IsPlayable(new GridPosition(x, y));
                }
            }

            return mask;
        }
    }
}
