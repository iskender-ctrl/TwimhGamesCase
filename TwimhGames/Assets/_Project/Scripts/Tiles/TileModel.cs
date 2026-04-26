using TwimhGames.Puzzle.Grid;

namespace TwimhGames.Puzzle.Tiles
{
    public sealed class TileModel
    {
        public TileKind Kind { get; }
        public SpecialTileKind SpecialKind { get; }
        public bool IsSpecial => SpecialKind != SpecialTileKind.None;
        public GridPosition Position { get; set; }

        public TileModel(TileKind kind, SpecialTileKind specialKind = SpecialTileKind.None)
        {
            Kind = kind;
            SpecialKind = specialKind;
        }
    }
}
