using UnityEngine;

namespace TwimhGames.Puzzle.Core
{
    public static class PuzzleGameBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureInstaller()
        {
            var installer = Object.FindFirstObjectByType<PuzzleGameInstaller>();
            if (installer != null)
            {
                return;
            }

            var installerObject = new GameObject("PuzzleGameInstaller");
            installerObject.AddComponent<PuzzleGameInstaller>();
        }
    }
}
