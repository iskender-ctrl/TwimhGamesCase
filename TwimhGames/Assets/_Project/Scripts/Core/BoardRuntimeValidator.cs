using System;
using TwimhGames.Puzzle.Match;

namespace TwimhGames.Puzzle.Core
{
    public static class BoardRuntimeValidator
    {
        public static void ValidateOrThrow(
            BoardManager boardManager,
            MatchFinder matchFinder,
            MoveFinder moveFinder,
            bool allowInitialMatches)
        {
            if (boardManager == null)
            {
                throw new ArgumentNullException(nameof(boardManager));
            }

            if (boardManager.Model == null)
            {
                throw new InvalidOperationException("Board model is not initialized.");
            }

            var hasAnyValidMove = moveFinder.HasAnyValidMove(boardManager.Model, matchFinder);
            if (!hasAnyValidMove)
            {
                throw new InvalidOperationException("Board contains no valid moves after initialization.");
            }

            if (allowInitialMatches)
            {
                return;
            }

            var initialMatches = matchFinder.FindMatches(boardManager.Model);
            if (initialMatches.Count > 0)
            {
                throw new InvalidOperationException(
                    "Board initialized with starting matches while AvoidInitialMatches is enabled.");
            }
        }
    }
}
