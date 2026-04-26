using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace TwimhGames.Puzzle.Config
{
    [CreateAssetMenu(menuName = "TwimhGames/Puzzle/Board Config", fileName = "BoardConfig")]
    public sealed class BoardConfigSO : ScriptableObject
    {
        [SerializeField, Min(1)] private int _width = 6;
        [SerializeField, Min(1)] private int _height = 6;
        [SerializeField] private LevelLayoutSO _levelLayout;
        [SerializeField, HideInInspector, Min(0.25f)] private float _cellSize = 1.1f;
        [SerializeField, HideInInspector, Min(0.1f)] private float _tileVisualScale = 0.9f;

        [Header("Layout")]
        [SerializeField, Min(0.25f), Tooltip("Size of the square slot under each fruit.")]
        private float _slotSize = 0.968f;

        [SerializeField, Min(0f), Tooltip("Empty space between neighboring slots.")]
        private float _cellGap = 0.132f;

        [SerializeField, Min(0.1f), Tooltip("Size of the fruit sprite itself.")]
        private float _fruitSize = 0.99f;

        [SerializeField, HideInInspector] private bool _layoutSettingsMigrated;
        [SerializeField, HideInInspector] private bool _advancedSettingsMigrated;
        [SerializeField, HideInInspector] private bool _cascadeSpecialSpawnSettingMigrated;
        [SerializeField, HideInInspector] private bool _boardBackgroundVisibilityMigrated;

        [Header("Specials")]
        [SerializeField] private BombAreaSettings _bombArea = BombAreaSettings.Default();
        [SerializeField] private SpecialComboSettings _specialCombos = SpecialComboSettings.Default();
        [SerializeField, Tooltip("If enabled, cascades can also spawn new special tiles.")]
        private bool _allowCascadeSpecialSpawns = true;

        [SerializeField] private bool _avoidInitialMatches = true;
        [SerializeField, Min(1)] private int _refillSpawnBufferRows = 2;

        [Header("Generation")]
        [SerializeField] private BoardGenerationSettings _generation = BoardGenerationSettings.Default();
        [SerializeField] private BoardTimings _timings = BoardTimings.Default();

        [Header("Camera")]
        [SerializeField] private BoardCameraSettings _camera = BoardCameraSettings.Default();

        [Header("Visuals")]
        [SerializeField] private BoardVisualSettings _visual = BoardVisualSettings.Default();

        public int Width => _levelLayout != null ? _levelLayout.Width : _width;
        public int Height => _levelLayout != null ? _levelLayout.Height : _height;
        public LevelLayoutSO LevelLayout => _levelLayout;
        public float SlotSize => _slotSize;
        public float CellGap => _cellGap;
        public float FruitSize => _fruitSize;
        public float CellStep => _slotSize + _cellGap;
        public float BoardWorldWidth => CalculateBoardSpan(Width);
        public float BoardWorldHeight => CalculateBoardSpan(Height);
        public BombAreaSettings BombArea => _bombArea;
        public Vector2Int BombAreaSize => _bombArea.ResolveSize();
        public SpecialComboSettings SpecialCombos => _specialCombos;
        public bool AllowCascadeSpecialSpawns => _allowCascadeSpecialSpawns;
        public bool AvoidInitialMatches => _avoidInitialMatches;
        public int RefillSpawnBufferRows => _refillSpawnBufferRows;
        public BoardGenerationSettings Generation => _generation;
        public BoardTimings Timings => _timings;
        public BoardCameraSettings Camera => _camera;
        public BoardVisualSettings Visual => _visual;

        private void OnEnable()
        {
            EnsureLayoutSettingsMigrated();
            EnsureAdvancedSettingsMigrated();
            EnsureCascadeSpecialSpawnSettingMigrated();
            EnsureBoardBackgroundVisibilityMigrated();
        }

        private void OnValidate()
        {
            EnsureLayoutSettingsMigrated();
            EnsureAdvancedSettingsMigrated();
            EnsureCascadeSpecialSpawnSettingMigrated();
            EnsureBoardBackgroundVisibilityMigrated();

            _slotSize = Mathf.Max(0.25f, _slotSize);
            _cellGap = Mathf.Max(0f, _cellGap);
            _fruitSize = Mathf.Max(0.1f, _fruitSize);

            _bombArea.Validate();
            _specialCombos.Validate();
            _generation.Validate();
            _timings.Validate();
            _camera.Validate();
        }

        public bool[,] BuildPlayableMask()
        {
            if (_levelLayout != null)
            {
                return _levelLayout.BuildPlayableMask();
            }

            var mask = new bool[Width, Height];
            for (var y = 0; y < Height; y++)
            {
                for (var x = 0; x < Width; x++)
                {
                    mask[x, y] = true;
                }
            }

            return mask;
        }

        public void AssignLevelLayout(LevelLayoutSO levelLayout)
        {
            _levelLayout = levelLayout;
        }

        public void SetSpecialIcons(Sprite colorIcon, Sprite bombIcon, Sprite lightningIcon, Color tint)
        {
            _visual.ColorSpecialIcon = colorIcon;
            _visual.BombSpecialIcon = bombIcon;
            _visual.LightningSpecialIcon = lightningIcon;
            _visual.SpecialIconTint = tint;
        }

        public static BoardConfigSO CreateRuntimeDefault()
        {
            return CreateInstance<BoardConfigSO>();
        }

        private void EnsureLayoutSettingsMigrated()
        {
            if (_layoutSettingsMigrated)
            {
                return;
            }

            var legacyFillRatio = Mathf.Clamp(_visual.CellFillRatio, 0.1f, 1f);
            _slotSize = Mathf.Max(0.25f, _cellSize * legacyFillRatio);
            _cellGap = Mathf.Max(0f, _cellSize - _slotSize);
            _fruitSize = Mathf.Max(0.1f, _cellSize * _tileVisualScale);
            _layoutSettingsMigrated = true;
        }

        private void EnsureAdvancedSettingsMigrated()
        {
            if (_advancedSettingsMigrated)
            {
                return;
            }

            _specialCombos = SpecialComboSettings.Default();
            _generation = BoardGenerationSettings.Default();
            _camera = BoardCameraSettings.Default();
            _advancedSettingsMigrated = true;
        }

        private void EnsureCascadeSpecialSpawnSettingMigrated()
        {
            if (_cascadeSpecialSpawnSettingMigrated)
            {
                return;
            }

            _allowCascadeSpecialSpawns = true;
            _cascadeSpecialSpawnSettingMigrated = true;
        }

        private void EnsureBoardBackgroundVisibilityMigrated()
        {
            if (_boardBackgroundVisibilityMigrated)
            {
                return;
            }

            _visual.ShowBoardBackground = true;
            _boardBackgroundVisibilityMigrated = true;

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                UnityEditor.EditorUtility.SetDirty(this);
            }
#endif
        }

        private float CalculateBoardSpan(int cellCount)
        {
            if (cellCount <= 0)
            {
                return 0f;
            }

            return ((cellCount - 1) * CellStep) + SlotSize;
        }
    }

    [Serializable]
    public struct BombAreaSettings
    {
        [Tooltip("Preset bomb area shape.")]
        public BombAreaMode Mode;

        [Min(1), Tooltip("Custom bomb area width. Even values are rounded up to odd.")]
        public int CustomWidth;

        [Min(1), Tooltip("Custom bomb area height. Even values are rounded up to odd.")]
        public int CustomHeight;

        public Vector2Int ResolveSize()
        {
            switch (Mode)
            {
                case BombAreaMode.Size5x5:
                    return new Vector2Int(5, 5);
                case BombAreaMode.Custom:
                    return new Vector2Int(
                        MakeOdd(Mathf.Max(1, CustomWidth)),
                        MakeOdd(Mathf.Max(1, CustomHeight)));
                default:
                    return new Vector2Int(3, 3);
            }
        }

        public void Validate()
        {
            CustomWidth = MakeOdd(Mathf.Max(1, CustomWidth));
            CustomHeight = MakeOdd(Mathf.Max(1, CustomHeight));
        }

        public static BombAreaSettings Default()
        {
            return new BombAreaSettings
            {
                Mode = BombAreaMode.Size3x3,
                CustomWidth = 3,
                CustomHeight = 3
            };
        }

        private static int MakeOdd(int value)
        {
            return value % 2 == 0 ? value + 1 : value;
        }
    }

    public enum BombAreaMode
    {
        Size3x3 = 0,
        Size5x5 = 1,
        Custom = 2
    }

    [Serializable]
    public struct BoardGenerationSettings
    {
        [Min(1)] public int MaxGenerationAttempts;

        public void Validate()
        {
            MaxGenerationAttempts = Mathf.Max(1, MaxGenerationAttempts);
        }

        public static BoardGenerationSettings Default()
        {
            return new BoardGenerationSettings
            {
                MaxGenerationAttempts = 256
            };
        }
    }

    [Serializable]
    public struct BoardTimings
    {
        [Min(0f)] public float SwapDuration;
        [Min(0f)] public float IllegalSwapReturnDuration;
        [Min(0f)] public float ClearAnimationDuration;
        [Min(0f)] public float ClearDelay;
        [Min(0f)] public float DropDuration;
        [Min(0f), Tooltip("Extra drop animation duration added per traveled cell.")] public float DropDurationPerCell;
        [Min(0f), Tooltip("Clamp for long-distance drop animation durations.")] public float DropDurationMax;
        [Min(0f)] public float RefillDuration;
        [Min(0f), Tooltip("Extra refill animation duration added per traveled cell.")] public float RefillDurationPerCell;
        [Min(0f), Tooltip("Clamp for long-distance refill animation durations.")] public float RefillDurationMax;
        [Min(0f)] public float NoMoveShuffleDelay;
        [Min(0f)] public float ShuffleDuration;
        [Min(0f)] public float HintDelay;
        [Min(0.05f)] public float HintBlinkInterval;
        [Min(0f)] public float CascadePause;

        public void Validate()
        {
            SwapDuration = Mathf.Max(0f, SwapDuration);
            IllegalSwapReturnDuration = Mathf.Max(0f, IllegalSwapReturnDuration);
            ClearAnimationDuration = Mathf.Max(0f, ClearAnimationDuration);
            ClearDelay = Mathf.Max(0f, ClearDelay);
            DropDuration = Mathf.Max(0f, DropDuration);
            DropDurationPerCell = Mathf.Max(0f, DropDurationPerCell);
            DropDurationMax = Mathf.Max(0f, DropDurationMax);
            RefillDuration = Mathf.Max(0f, RefillDuration);
            RefillDurationPerCell = Mathf.Max(0f, RefillDurationPerCell);
            RefillDurationMax = Mathf.Max(0f, RefillDurationMax);
            NoMoveShuffleDelay = Mathf.Max(0f, NoMoveShuffleDelay);
            ShuffleDuration = Mathf.Max(0f, ShuffleDuration);
            HintDelay = Mathf.Max(0f, HintDelay);
            HintBlinkInterval = Mathf.Max(0.05f, HintBlinkInterval);
            CascadePause = Mathf.Max(0f, CascadePause);
        }

        public static BoardTimings Default()
        {
            return new BoardTimings
            {
                SwapDuration = 0.14f,
                IllegalSwapReturnDuration = 0.12f,
                ClearAnimationDuration = 0.1f,
                ClearDelay = 0.045f,
                DropDuration = 0.1f,
                DropDurationPerCell = 0.03f,
                DropDurationMax = 0.28f,
                RefillDuration = 0.1f,
                RefillDurationPerCell = 0.028f,
                RefillDurationMax = 0.26f,
                NoMoveShuffleDelay = 0.45f,
                ShuffleDuration = 0.22f,
                HintDelay = 3f,
                HintBlinkInterval = 0.35f,
                CascadePause = 0.04f
            };
        }
    }

    [Serializable]
    public struct BoardCameraSettings
    {
        [Min(0f)] public float FramingPadding;
        public Color BackgroundColor;

        public void Validate()
        {
            FramingPadding = Mathf.Max(0f, FramingPadding);
        }

        public static BoardCameraSettings Default()
        {
            return new BoardCameraSettings
            {
                FramingPadding = 1.25f,
                BackgroundColor = new Color(0.95f, 0.96f, 0.98f)
            };
        }
    }

    [Serializable]
    public struct SpecialComboSettings
    {
        [Min(0), Tooltip("Extra bomb rings added when Bomb + Bomb is swapped.")]
        public int BombBombExtraRings;

        [Min(0), Tooltip("Horizontal radius for Lightning + Lightning cross-band clear.")]
        public int LightningLightningHorizontalRadius;

        [Min(0), Tooltip("Vertical radius for Lightning + Lightning cross-band clear.")]
        public int LightningLightningVerticalRadius;

        [Tooltip("If enabled, Bomb + Lightning swap thickness is derived from Bomb Area.")]
        public bool UseBombAreaForMixedCrossBands;

        [Min(0), Tooltip("Horizontal radius for Bomb + Lightning mixed cross-band clear.")]
        public int MixedCrossHorizontalRadius;

        [Min(0), Tooltip("Vertical radius for Bomb + Lightning mixed cross-band clear.")]
        public int MixedCrossVerticalRadius;

        public Vector2Int ResolveBombBombArea(Vector2Int baseBombAreaSize)
        {
            return new Vector2Int(
                baseBombAreaSize.x + (BombBombExtraRings * 2),
                baseBombAreaSize.y + (BombBombExtraRings * 2));
        }

        public Vector2Int ResolveLightningLightningBands()
        {
            return new Vector2Int(
                Mathf.Max(0, LightningLightningHorizontalRadius),
                Mathf.Max(0, LightningLightningVerticalRadius));
        }

        public Vector2Int ResolveMixedCrossBands(Vector2Int baseBombAreaSize)
        {
            if (UseBombAreaForMixedCrossBands)
            {
                return new Vector2Int(
                    Mathf.Max(1, baseBombAreaSize.x / 2),
                    Mathf.Max(1, baseBombAreaSize.y / 2));
            }

            return new Vector2Int(
                Mathf.Max(0, MixedCrossHorizontalRadius),
                Mathf.Max(0, MixedCrossVerticalRadius));
        }

        public void Validate()
        {
            BombBombExtraRings = Mathf.Max(0, BombBombExtraRings);
            LightningLightningHorizontalRadius = Mathf.Max(0, LightningLightningHorizontalRadius);
            LightningLightningVerticalRadius = Mathf.Max(0, LightningLightningVerticalRadius);
            MixedCrossHorizontalRadius = Mathf.Max(0, MixedCrossHorizontalRadius);
            MixedCrossVerticalRadius = Mathf.Max(0, MixedCrossVerticalRadius);
        }

        public static SpecialComboSettings Default()
        {
            return new SpecialComboSettings
            {
                BombBombExtraRings = 1,
                LightningLightningHorizontalRadius = 1,
                LightningLightningVerticalRadius = 1,
                UseBombAreaForMixedCrossBands = true,
                MixedCrossHorizontalRadius = 1,
                MixedCrossVerticalRadius = 1
            };
        }
    }

    [Serializable]
    public struct BoardVisualSettings
    {
        [Min(0f)] public float PanelPadding;

        [HideInInspector, FormerlySerializedAs("CellFillRatio")]
        [Range(0.1f, 1f)] public float CellFillRatio;

        [Tooltip("If disabled, board panel and slot grid visuals are not generated at runtime.")]
        public bool ShowBoardBackground;
        public bool ShowBlockedCells;
        public Color PanelColor;
        public Color PlayableCellColor;
        public Color BlockedCellColor;
        public Color SpecialIconTint;
        public Sprite ColorSpecialIcon;
        public Sprite BombSpecialIcon;
        public Sprite LightningSpecialIcon;

        public static BoardVisualSettings Default()
        {
            return new BoardVisualSettings
            {
                PanelPadding = 0.45f,
                CellFillRatio = 0.88f,
                ShowBoardBackground = true,
                ShowBlockedCells = false,
                PanelColor = new Color(0.15f, 0.18f, 0.24f, 0.95f),
                PlayableCellColor = new Color(0.29f, 0.34f, 0.43f, 0.95f),
                BlockedCellColor = new Color(0.2f, 0.22f, 0.28f, 0.7f),
                SpecialIconTint = Color.white,
                ColorSpecialIcon = null,
                BombSpecialIcon = null,
                LightningSpecialIcon = null
            };
        }
    }
}
