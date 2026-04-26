using System;
using System.Collections.Generic;
using UnityEngine;

namespace TwimhGames.Puzzle.Config
{
    [CreateAssetMenu(menuName = "TwimhGames/Puzzle/Level Layouts", fileName = "LevelLayout")]
    public sealed class LevelLayoutSO : ScriptableObject
    {
        public const char PlayableToken = '1';
        public const char BlockedToken = '0';

        [SerializeField] private int _selectedLevelIndex;
        [SerializeField] private List<LevelLayoutDefinition> _levels = new List<LevelLayoutDefinition>
        {
            LevelLayoutDefinition.CreateDefault("Level 1", 6, 6)
        };

        [SerializeField, HideInInspector] private bool _migratedToLevelCollection;
        [SerializeField, HideInInspector, Min(1)] private int _width = 6;
        [SerializeField, HideInInspector, Min(1)] private int _height = 6;
        [SerializeField, HideInInspector, Tooltip("Top-to-bottom row strings. Playable: 1, X, O, #. Blocked: 0, ., -, _")]
        private List<string> _rowsTopToBottom = new List<string>
        {
            "111111",
            "111111",
            "111111",
            "111111",
            "111111",
            "111111"
        };

        public int Width => GetSelectedLevel().Width;
        public int Height => GetSelectedLevel().Height;
        public int LevelCount => _levels != null ? _levels.Count : 0;
        public int SelectedLevelIndex => Mathf.Clamp(_selectedLevelIndex, 0, Mathf.Max(0, LevelCount - 1));
        public string SelectedLevelName => GetSelectedLevel().Name;
        public bool CanSelectPreviousLevel => SelectedLevelIndex > 0;
        public bool CanSelectNextLevel => SelectedLevelIndex < LevelCount - 1;

        public static bool IsPlayableToken(char token)
        {
            switch (token)
            {
                case PlayableToken:
                case 'X':
                case 'x':
                case 'O':
                case 'o':
                case '#':
                    return true;
                default:
                    return false;
            }
        }

        public static char ToToken(bool playable)
        {
            return playable ? PlayableToken : BlockedToken;
        }

        public bool[,] BuildPlayableMask()
        {
            return GetSelectedLevel().BuildPlayableMask();
        }

        public bool TrySelectLevel(int levelIndex)
        {
            EnsureInitialized();

            if (levelIndex < 0 || levelIndex >= _levels.Count)
            {
                return false;
            }

            if (_selectedLevelIndex == levelIndex)
            {
                return true;
            }

            _selectedLevelIndex = levelIndex;
            return true;
        }

        public bool SelectPreviousLevel()
        {
            return TrySelectLevel(SelectedLevelIndex - 1);
        }

        public bool SelectNextLevel()
        {
            return TrySelectLevel(SelectedLevelIndex + 1);
        }

        private void OnEnable()
        {
            EnsureInitialized();
        }

        private void OnValidate()
        {
            EnsureInitialized();
        }

        private void EnsureInitialized()
        {
            MigrateLegacyLevelIfNeeded();
            EnsureLevelListExists();
            NormalizeLevels();
            _selectedLevelIndex = Mathf.Clamp(_selectedLevelIndex, 0, Mathf.Max(0, _levels.Count - 1));
        }

        private void MigrateLegacyLevelIfNeeded()
        {
            if (_migratedToLevelCollection)
            {
                return;
            }

            if (_levels == null)
            {
                _levels = new List<LevelLayoutDefinition>();
            }

            if (_levels.Count == 0)
            {
                var migratedLevel = new LevelLayoutDefinition(
                    "Level 1",
                    Mathf.Max(1, _width),
                    Mathf.Max(1, _height),
                    _rowsTopToBottom);
                migratedLevel.Normalize();
                _levels.Add(migratedLevel);
            }

            _migratedToLevelCollection = true;
        }

        private void EnsureLevelListExists()
        {
            if (_levels == null)
            {
                _levels = new List<LevelLayoutDefinition>();
            }

            if (_levels.Count == 0)
            {
                _levels.Add(LevelLayoutDefinition.CreateDefault("Level 1", Mathf.Max(1, _width), Mathf.Max(1, _height)));
            }
        }

        private void NormalizeLevels()
        {
            for (var i = 0; i < _levels.Count; i++)
            {
                if (_levels[i] == null)
                {
                    _levels[i] = LevelLayoutDefinition.CreateDefault($"Level {i + 1}", 6, 6);
                }

                _levels[i].Normalize();

                if (string.IsNullOrWhiteSpace(_levels[i].Name))
                {
                    _levels[i].SetName($"Level {i + 1}");
                }
            }
        }

        private LevelLayoutDefinition GetSelectedLevel()
        {
            EnsureInitialized();
            return _levels[_selectedLevelIndex];
        }
    }

    [Serializable]
    public sealed class LevelLayoutDefinition
    {
        [SerializeField] private string _name = "Level 1";
        [SerializeField, Min(1)] private int _width = 6;
        [SerializeField, Min(1)] private int _height = 6;
        [SerializeField, Tooltip("Top-to-bottom row strings. Playable: 1, X, O, #. Blocked: 0, ., -, _")]
        private List<string> _rowsTopToBottom = new List<string>
        {
            "111111",
            "111111",
            "111111",
            "111111",
            "111111",
            "111111"
        };

        public string Name => string.IsNullOrWhiteSpace(_name) ? "Level" : _name.Trim();
        public int Width => Mathf.Max(1, _width);
        public int Height => Mathf.Max(1, _height);

        public LevelLayoutDefinition(string name, int width, int height, List<string> rowsTopToBottom)
        {
            _name = string.IsNullOrWhiteSpace(name) ? "Level" : name.Trim();
            _width = Mathf.Max(1, width);
            _height = Mathf.Max(1, height);
            _rowsTopToBottom = rowsTopToBottom != null ? new List<string>(rowsTopToBottom) : new List<string>();
        }

        public static LevelLayoutDefinition CreateDefault(string name, int width, int height)
        {
            var rows = new List<string>(height);
            var row = new string(LevelLayoutSO.PlayableToken, Mathf.Max(1, width));
            for (var i = 0; i < Mathf.Max(1, height); i++)
            {
                rows.Add(row);
            }

            return new LevelLayoutDefinition(name, width, height, rows);
        }

        public bool[,] BuildPlayableMask()
        {
            var width = Width;
            var height = Height;
            var mask = new bool[width, height];

            if (_rowsTopToBottom == null || _rowsTopToBottom.Count == 0)
            {
                FillAllPlayable(mask);
                return mask;
            }

            var hasAnyPlayableCell = false;

            for (var y = 0; y < height; y++)
            {
                var rowIndexFromTop = (height - 1) - y;
                var row = rowIndexFromTop >= 0 && rowIndexFromTop < _rowsTopToBottom.Count
                    ? _rowsTopToBottom[rowIndexFromTop]
                    : string.Empty;

                for (var x = 0; x < width; x++)
                {
                    var playable = ParsePlayable(row, x);
                    mask[x, y] = playable;
                    hasAnyPlayableCell |= playable;
                }
            }

            if (!hasAnyPlayableCell)
            {
                FillAllPlayable(mask);
            }

            return mask;
        }

        public void Normalize()
        {
            _name = string.IsNullOrWhiteSpace(_name) ? "Level" : _name.Trim();
            _width = Mathf.Max(1, _width);
            _height = Mathf.Max(1, _height);

            if (_rowsTopToBottom == null)
            {
                _rowsTopToBottom = new List<string>();
            }

            var normalized = new List<string>(_height);
            for (var rowTop = 0; rowTop < _height; rowTop++)
            {
                var source = rowTop < _rowsTopToBottom.Count ? _rowsTopToBottom[rowTop] : string.Empty;
                var rowChars = new char[_width];

                for (var x = 0; x < _width; x++)
                {
                    var playable = !string.IsNullOrEmpty(source) && x < source.Length && LevelLayoutSO.IsPlayableToken(source[x]);
                    rowChars[x] = LevelLayoutSO.ToToken(playable);
                }

                normalized.Add(new string(rowChars));
            }

            _rowsTopToBottom = normalized;
        }

        public void SetName(string value)
        {
            _name = string.IsNullOrWhiteSpace(value) ? "Level" : value.Trim();
        }

        private static bool ParsePlayable(string row, int x)
        {
            if (string.IsNullOrEmpty(row) || x >= row.Length)
            {
                return false;
            }

            return LevelLayoutSO.IsPlayableToken(row[x]);
        }

        private static void FillAllPlayable(bool[,] mask)
        {
            var width = mask.GetLength(0);
            var height = mask.GetLength(1);

            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    mask[x, y] = true;
                }
            }
        }
    }
}


