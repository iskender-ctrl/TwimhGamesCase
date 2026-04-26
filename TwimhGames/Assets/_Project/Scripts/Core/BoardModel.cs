using TwimhGames.Puzzle.Grid;
using TwimhGames.Puzzle.Tiles;

namespace TwimhGames.Puzzle.Core
{
    public sealed class BoardModel
    {
        private readonly TileModel[,] _tiles;
        private readonly bool[,] _playableMask;

        public int Width { get; }
        public int Height { get; }

        public BoardModel(int width, int height, bool[,] playableMask = null)
        {
            Width = width;
            Height = height;
            _tiles = new TileModel[width, height];
            _playableMask = new bool[width, height];

            var hasCompatibleMask = playableMask != null &&
                                    playableMask.GetLength(0) == width &&
                                    playableMask.GetLength(1) == height;

            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    _playableMask[x, y] = hasCompatibleMask ? playableMask[x, y] : true;
                }
            }
        }

        public bool IsInside(GridPosition position)
        {
            return position.X >= 0 && position.X < Width && position.Y >= 0 && position.Y < Height;
        }

        public bool IsPlayable(GridPosition position)
        {
            return IsInside(position) && _playableMask[position.X, position.Y];
        }

        public TileModel GetTile(GridPosition position)
        {
            return IsPlayable(position) ? _tiles[position.X, position.Y] : null;
        }

        public void SetTile(GridPosition position, TileModel tile)
        {
            if (!IsPlayable(position))
            {
                return;
            }

            _tiles[position.X, position.Y] = tile;
            if (tile != null)
            {
                tile.Position = position;
            }
        }
    }
}
