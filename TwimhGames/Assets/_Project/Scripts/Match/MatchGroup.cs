using System.Collections.Generic;
using TwimhGames.Puzzle.Grid;
using TwimhGames.Puzzle.Tiles;

namespace TwimhGames.Puzzle.Match
{
    public sealed class MatchGroup
    {
        public TileKind TileKind { get; }
        public MatchOrientation Orientation { get; }
        public IReadOnlyList<GridPosition> Positions { get; }

        public MatchGroup(TileKind tileKind, MatchOrientation orientation, List<GridPosition> positions)
        {
            TileKind = tileKind;
            Orientation = orientation;
            Positions = positions;
        }

        public bool Contains(GridPosition position)
        {
            for (var i = 0; i < Positions.Count; i++)
            {
                if (Positions[i] == position)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
