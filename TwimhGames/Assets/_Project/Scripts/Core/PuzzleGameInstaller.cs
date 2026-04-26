using TwimhGames.Puzzle.Config;
using TwimhGames.Puzzle.Events;
using TwimhGames.Puzzle.Input;
using TwimhGames.Puzzle.Match;
using TwimhGames.Puzzle.Pooling;
using TwimhGames.Puzzle.StateMachine;
using UnityEngine;
using UnityEngine.UI;

namespace TwimhGames.Puzzle.Core
{
    public sealed class PuzzleGameInstaller : MonoBehaviour
    {
        [Header("Optional Config Assets")]
        [SerializeField] private BoardConfigSO _boardConfig;
        [SerializeField] private TileCatalogSO _tileCatalog;

        [Header("Runtime Setup")]
        [SerializeField] private Camera _gameplayCamera;
        [SerializeField] private bool _autoConfigureCamera = true;

        [Header("Level Navigation")]
        [SerializeField] private Button _previousLevelButton;
        [SerializeField] private Button _nextLevelButton;

        private BoardManager _boardManager;
        private BoardInputController _inputController;
        private TilePoolManager _poolManager;
        private PlayableBoardGenerator _boardGenerator;
        private MatchFinder _matchFinder;
        private MoveFinder _moveFinder;
        private LevelNavigationController _navigationController;
        private GameStateMachine _stateMachine;
        private GameEventBus _eventBus;

        private void Awake()
        {
            ApplyRuntimeFramePacing();

            _boardConfig = ResolveBoardConfig();
            _tileCatalog = ResolveTileCatalog();
            _gameplayCamera = PuzzleCameraConfigurator.ResolveOrCreate(_gameplayCamera);
            if (_autoConfigureCamera)
            {
                PuzzleCameraConfigurator.Configure(_gameplayCamera, _boardConfig);
            }

            EnsureSceneComponents();
            InitializeCoreState();
            CreateGameplayServices();

            var swapController = BuildGameplayControllers();
            InitializeBoardRuntime();
            InitializeInput(swapController);
            InitializeNavigation();

            _stateMachine.SetState(GameState.Idle);
            _navigationController?.Refresh();
        }

        private void OnDestroy()
        {
            if (_eventBus != null)
            {
                _eventBus.StateChanged -= OnGameStateChanged;
            }

            _navigationController?.Detach();
        }

        public void GoToPreviousLevel()
        {
            if (_navigationController == null || _navigationController.TryGoPreviousLevel())
            {
                return;
            }

            _navigationController.Refresh();
        }

        public void GoToNextLevel()
        {
            if (_navigationController == null || _navigationController.TryGoNextLevel())
            {
                return;
            }

            _navigationController.Refresh();
        }

        private void EnsureSceneComponents()
        {
            _poolManager = GetComponent<TilePoolManager>();
            if (_poolManager == null)
            {
                _poolManager = gameObject.AddComponent<TilePoolManager>();
            }

            _boardManager = GetComponent<BoardManager>();
            if (_boardManager == null)
            {
                _boardManager = gameObject.AddComponent<BoardManager>();
            }

            _inputController = GetComponent<BoardInputController>();
            if (_inputController == null)
            {
                _inputController = gameObject.AddComponent<BoardInputController>();
            }
        }

        private void InitializeCoreState()
        {
            _stateMachine = new GameStateMachine();
            _eventBus = new GameEventBus();
            _stateMachine.StateChanged += _eventBus.RaiseStateChanged;
            _eventBus.StateChanged += OnGameStateChanged;
        }

        private void CreateGameplayServices()
        {
            _matchFinder = new MatchFinder();
            _moveFinder = new MoveFinder();
            _boardGenerator = new PlayableBoardGenerator(_tileCatalog, _matchFinder, _moveFinder, _boardConfig.Generation);
        }

        private SwapController BuildGameplayControllers()
        {
            var specialTileResolver = new SpecialTileResolver(_boardConfig.BombAreaSize, _boardConfig.SpecialCombos);

            var boardResolver = new BoardResolver(
                _boardManager,
                _matchFinder,
                _moveFinder,
                specialTileResolver,
                _stateMachine,
                _eventBus,
                _boardConfig
            );

            return new SwapController(
                _boardManager,
                _matchFinder,
                boardResolver,
                specialTileResolver,
                _stateMachine,
                _eventBus,
                _boardConfig
            );
        }

        private void InitializeBoardRuntime()
        {
            _boardManager.Initialize(_boardConfig, _tileCatalog, _poolManager, _boardGenerator);
            BoardRuntimeValidator.ValidateOrThrow(
                _boardManager,
                _matchFinder,
                _moveFinder,
                allowInitialMatches: !_boardConfig.AvoidInitialMatches);
        }

        private void InitializeInput(SwapController swapController)
        {
            _inputController.Initialize(
                _gameplayCamera,
                _boardManager,
                swapController,
                _stateMachine,
                _matchFinder,
                _moveFinder,
                _boardConfig);
        }

        private void InitializeNavigation()
        {
            if (_previousLevelButton == null || _nextLevelButton == null)
            {
                Debug.LogWarning(
                    "PuzzleGameInstaller: Level navigation buttons are not fully assigned. Assign both Previous and Next buttons in Inspector.",
                    this);
            }

            _navigationController = new LevelNavigationController(_boardConfig, _stateMachine, ReloadSelectedLevel);
            _navigationController.Attach(_previousLevelButton, _nextLevelButton);
        }

        private void ReloadSelectedLevel()
        {
            _stateMachine.SetState(GameState.Locked);
            _inputController.ResetRuntimeState();
            _boardManager.ResetBoard();
            if (_autoConfigureCamera)
            {
                PuzzleCameraConfigurator.Configure(_gameplayCamera, _boardConfig);
            }

            InitializeBoardRuntime();

            _stateMachine.SetState(GameState.Idle);
            _eventBus.RaiseBoardStable();
            _navigationController?.Refresh();
        }

        private void OnGameStateChanged(GameState state)
        {
            _navigationController?.Refresh();
        }

        private BoardConfigSO ResolveBoardConfig()
        {
            if (_boardConfig != null)
            {
                return _boardConfig;
            }

            Debug.LogWarning("PuzzleGameInstaller: BoardConfig is not assigned. Using runtime default config.", this);
            return BoardConfigSO.CreateRuntimeDefault();
        }

        private TileCatalogSO ResolveTileCatalog()
        {
            if (_tileCatalog != null)
            {
                return _tileCatalog;
            }

            Debug.LogWarning("PuzzleGameInstaller: TileCatalog is not assigned. Using runtime default catalog.", this);
            return TileCatalogSO.CreateRuntimeDefault();
        }

        private static void ApplyRuntimeFramePacing()
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;

#if UNITY_EDITOR
            var refreshRate = (float)Screen.currentResolution.refreshRateRatio.value;
            Debug.Log($"PuzzleGameInstaller: Frame pacing -> vSync={QualitySettings.vSyncCount}, targetFrameRate={Application.targetFrameRate}, refresh={refreshRate:0.##}Hz");
#endif
        }
    }
}