using TwimhGames.Puzzle.Config;
using TwimhGames.Puzzle.Core;
using TwimhGames.Puzzle.Grid;
using TwimhGames.Puzzle.Match;
using TwimhGames.Puzzle.StateMachine;
using TwimhGames.Puzzle.Visual;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
#if DOTWEEN || DOTWEEN_ENABLED
using DG.Tweening;
#endif

namespace TwimhGames.Puzzle.Input
{
    public sealed class BoardInputController : MonoBehaviour
    {
        [SerializeField, Min(2f)] private float _dragThresholdPixels = 24f;
#if DOTWEEN || DOTWEEN_ENABLED
        [SerializeField, Min(1.01f)] private float _hintScaleMultiplier = 1.14f;
        [SerializeField, Min(0.05f)] private float _hintScaleDuration = 0.16f;
        [SerializeField, Min(0.05f)] private float _hintSwapDuration = 0.2f;
        [SerializeField, Min(0f)] private float _hintReturnPause = 0.08f;
        [SerializeField, Min(0f)] private float _hintRepeatDelay = 1.1f;
#endif

        private Camera _camera;
        private BoardManager _boardManager;
        private SwapController _swapController;
        private MatchFinder _matchFinder;
        private MoveFinder _moveFinder;
        private GameStateMachine _stateMachine;
        private float _hintDelaySeconds = 3f;
        private float _hintBlinkInterval = 0.35f;
        private GridPosition? _selectedPosition;
        private bool _pointerTracking;
        private Vector2 _pointerDownScreenPosition;
        private GridPosition? _pointerDownGridPosition;
        private bool _dragSwapTriggered;
        private float _idleTime;
        private float _hintBlinkTimer;
        private bool _hintActive;
        private bool _hintVisible;
        private GridPosition _hintFrom;
        private GridPosition _hintTo;
        private bool _isInitialized;
#if DOTWEEN || DOTWEEN_ENABLED
        private Sequence _hintSequence;
        private TileView _hintFromView;
        private TileView _hintToView;
        private Vector3 _hintFromBaseScale;
        private Vector3 _hintToBaseScale;
#endif

        public void Initialize(
            Camera gameplayCamera,
            BoardManager boardManager,
            SwapController swapController,
            GameStateMachine stateMachine,
            MatchFinder matchFinder,
            MoveFinder moveFinder,
            BoardConfigSO boardConfig)
        {
            _camera = gameplayCamera;
            _boardManager = boardManager;
            _swapController = swapController;
            _stateMachine = stateMachine;
            _matchFinder = matchFinder;
            _moveFinder = moveFinder;
            _hintDelaySeconds = Mathf.Max(0f, boardConfig.Timings.HintDelay);
            _hintBlinkInterval = Mathf.Max(0.05f, boardConfig.Timings.HintBlinkInterval);
            _isInitialized = true;
        }

        public void ResetRuntimeState()
        {
            ClearSelection();
            ResetPointerTracking();
            ResetHintState();
        }

        private void Update()
        {
            if (!_isInitialized)
            {
                return;
            }

            if (!_stateMachine.IsIn(GameState.Idle))
            {
                ResetPointerTracking();
                ResetHintState();
                return;
            }

            HandlePointerInput();
            UpdateHint();
        }

        private void OnDisable()
        {
            ResetPointerTracking();
            ResetHintState();
        }

        private void HandlePointerInput()
        {
            if (TryGetPrimaryPressPosition(out var pressPosition))
            {
                OnPointerDown(pressPosition);
            }

            if (_pointerTracking && TryGetPrimaryHeldPosition(out var heldPosition))
            {
                TryHandleDrag(heldPosition);
            }

            if (_pointerTracking && TryGetPrimaryReleasePosition(out var releasePosition))
            {
                OnPointerUp(releasePosition);
            }
        }

        private void OnPointerDown(Vector2 screenPosition)
        {
            RegisterInteraction();

            _pointerTracking = true;
            _pointerDownScreenPosition = screenPosition;
            _dragSwapTriggered = false;

            if (TryGetTilePositionAtScreen(screenPosition, out var gridPosition))
            {
                _pointerDownGridPosition = gridPosition;
                return;
            }

            _pointerDownGridPosition = null;
        }

        private void TryHandleDrag(Vector2 screenPosition)
        {
            if (_dragSwapTriggered || !_pointerDownGridPosition.HasValue)
            {
                return;
            }

            if (!HasDraggedBeyondThreshold(screenPosition))
            {
                return;
            }

            var from = _pointerDownGridPosition.Value;
            var delta = screenPosition - _pointerDownScreenPosition;
            var to = ResolveDragTarget(from, delta);

            _dragSwapTriggered = true;

            if (!_boardManager.Model.IsInside(to))
            {
                ClearSelection();
                return;
            }

            ClearSelection();
            StartCoroutine(_swapController.TrySwapRoutine(from, to, to));
            _pointerTracking = false;
        }

        private void OnPointerUp(Vector2 screenPosition)
        {
            RegisterInteraction();

            var dragged = HasDraggedBeyondThreshold(screenPosition);
            var dragSwapTriggered = _dragSwapTriggered;
            ResetPointerTracking();

            if (dragSwapTriggered)
            {
                return;
            }

            if (dragged)
            {
                ClearSelection();
                return;
            }

            ProcessClick(screenPosition);
        }

        private void ProcessClick(Vector2 screenPosition)
        {
            if (!TryGetTilePositionAtScreen(screenPosition, out var clickedPosition))
            {
                ClearSelection();
                return;
            }

            if (!_selectedPosition.HasValue)
            {
                Select(clickedPosition);
                return;
            }

            var first = _selectedPosition.Value;
            if (first == clickedPosition)
            {
                ClearSelection();
                return;
            }

            ClearSelection();

            if (!first.IsAdjacentTo(clickedPosition))
            {
                Select(clickedPosition);
                return;
            }

            StartCoroutine(_swapController.TrySwapRoutine(first, clickedPosition, clickedPosition));
        }

        private static bool TryGetPrimaryReleasePosition(out Vector2 screenPosition)
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null && Mouse.current.leftButton.wasReleasedThisFrame)
            {
                screenPosition = Mouse.current.position.ReadValue();
                return true;
            }

            if (Touchscreen.current != null)
            {
                var touch = Touchscreen.current.primaryTouch;
                if (touch.press.wasReleasedThisFrame)
                {
                    screenPosition = touch.position.ReadValue();
                    return true;
                }
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            if (UnityEngine.Input.GetMouseButtonUp(0))
            {
                screenPosition = UnityEngine.Input.mousePosition;
                return true;
            }
#endif

            screenPosition = default;
            return false;
        }

        private static bool TryGetPrimaryHeldPosition(out Vector2 screenPosition)
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null && Mouse.current.leftButton.isPressed)
            {
                screenPosition = Mouse.current.position.ReadValue();
                return true;
            }

            if (Touchscreen.current != null)
            {
                var touch = Touchscreen.current.primaryTouch;
                if (touch.press.isPressed)
                {
                    screenPosition = touch.position.ReadValue();
                    return true;
                }
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            if (UnityEngine.Input.GetMouseButton(0))
            {
                screenPosition = UnityEngine.Input.mousePosition;
                return true;
            }
#endif

            screenPosition = default;
            return false;
        }

        private static bool TryGetPrimaryPressPosition(out Vector2 screenPosition)
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                screenPosition = Mouse.current.position.ReadValue();
                return true;
            }

            if (Touchscreen.current != null)
            {
                var touch = Touchscreen.current.primaryTouch;
                if (touch.press.wasPressedThisFrame)
                {
                    screenPosition = touch.position.ReadValue();
                    return true;
                }
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            if (UnityEngine.Input.GetMouseButtonDown(0))
            {
                screenPosition = UnityEngine.Input.mousePosition;
                return true;
            }
#endif

            screenPosition = default;
            return false;
        }

        private bool HasDraggedBeyondThreshold(Vector2 screenPosition)
        {
            var delta = screenPosition - _pointerDownScreenPosition;
            return delta.sqrMagnitude >= (_dragThresholdPixels * _dragThresholdPixels);
        }

        private GridPosition ResolveDragTarget(GridPosition from, Vector2 dragDelta)
        {
            if (Mathf.Abs(dragDelta.x) >= Mathf.Abs(dragDelta.y))
            {
                var x = dragDelta.x > 0f ? from.X + 1 : from.X - 1;
                return new GridPosition(x, from.Y);
            }

            var y = dragDelta.y > 0f ? from.Y + 1 : from.Y - 1;
            return new GridPosition(from.X, y);
        }

        private bool TryGetTilePositionAtScreen(Vector2 screenPosition, out GridPosition gridPosition)
        {
            var world = _camera.ScreenToWorldPoint(screenPosition);
            world.z = 0f;

            var hit = Physics2D.OverlapPoint(world);
            if (hit != null && hit.TryGetComponent<TileView>(out var tileView))
            {
                gridPosition = tileView.GridPosition;
                return true;
            }

            gridPosition = default;
            return false;
        }

        private void ResetPointerTracking()
        {
            _pointerTracking = false;
            _pointerDownGridPosition = null;
            _dragSwapTriggered = false;
            _pointerDownScreenPosition = default;
        }

        private void UpdateHint()
        {
            if (_selectedPosition.HasValue || _pointerTracking)
            {
                ClearHintVisuals();
                return;
            }

            _idleTime += Time.deltaTime;

            if (!_hintActive && _idleTime >= _hintDelaySeconds)
            {
                TryActivateHint();
            }

            if (!_hintActive)
            {
                return;
            }

#if DOTWEEN || DOTWEEN_ENABLED
            if (_hintSequence != null && _hintSequence.IsActive())
            {
                return;
            }

            _hintBlinkTimer += Time.deltaTime;
            if (_hintBlinkTimer < _hintRepeatDelay)
            {
                return;
            }

            StartHintSequence();
#else
            _hintBlinkTimer += Time.deltaTime;
            if (_hintBlinkTimer < _hintBlinkInterval)
            {
                return;
            }

            _hintBlinkTimer = 0f;
            SetHintVisible(!_hintVisible);
#endif
        }

        private void TryActivateHint()
        {
            if (_moveFinder == null || _matchFinder == null || _boardManager?.Model == null)
            {
                return;
            }

            if (!_moveFinder.TryGetAnyValidMove(_boardManager.Model, _matchFinder, out _hintFrom, out _hintTo))
            {
                return;
            }

            _hintActive = true;
            _hintBlinkTimer = 0f;
#if DOTWEEN || DOTWEEN_ENABLED
            _hintBlinkTimer = _hintRepeatDelay;
            StartHintSequence();
#else
            SetHintVisible(true);
#endif
        }

        private void RegisterInteraction()
        {
            _idleTime = 0f;
            _hintBlinkTimer = 0f;
            ClearHintVisuals();
        }

        private void ResetHintState()
        {
            _idleTime = 0f;
            _hintBlinkTimer = 0f;
            ClearHintVisuals();
        }

        private void ClearHintVisuals()
        {
#if DOTWEEN || DOTWEEN_ENABLED
            StopHintSequenceAndRestore();
#else
            if (_hintVisible && _hintActive)
            {
                _boardManager.SetHighlight(_hintFrom, false);
                _boardManager.SetHighlight(_hintTo, false);
            }
#endif
            _hintVisible = false;
            _hintActive = false;
        }

        private void SetHintVisible(bool visible)
        {
            if (!_hintActive)
            {
                _hintVisible = false;
                return;
            }

            _boardManager.SetHighlight(_hintFrom, visible);
            _boardManager.SetHighlight(_hintTo, visible);
            _hintVisible = visible;
        }

#if DOTWEEN || DOTWEEN_ENABLED
        private void StartHintSequence()
        {
            if (!_hintActive)
            {
                return;
            }

            if (!TryResolveHintViews(out _hintFromView, out _hintToView))
            {
                _hintActive = false;
                return;
            }

            var fromTargetPosition = _boardManager.GridToWorld(_hintFrom);
            var toTargetPosition = _boardManager.GridToWorld(_hintTo);

            _hintFromView.transform.position = fromTargetPosition;
            _hintToView.transform.position = toTargetPosition;
            _hintFromView.SetHighlight(false);
            _hintToView.SetHighlight(false);

            _hintFromBaseScale = _hintFromView.transform.localScale;
            _hintToBaseScale = _hintToView.transform.localScale;

            var fromGrowScale = _hintFromBaseScale * _hintScaleMultiplier;
            var toGrowScale = _hintToBaseScale * _hintScaleMultiplier;

            _hintSequence = DOTween.Sequence().SetRecyclable(true).SetLink(gameObject);
            _hintSequence.Append(_hintFromView.transform.DOScale(fromGrowScale, _hintScaleDuration).SetEase(Ease.OutQuad).SetRecyclable(true));
            _hintSequence.Join(_hintToView.transform.DOScale(toGrowScale, _hintScaleDuration).SetEase(Ease.OutQuad).SetRecyclable(true));
            _hintSequence.Append(_hintFromView.transform.DOMove(toTargetPosition, _hintSwapDuration).SetEase(Ease.InOutSine).SetRecyclable(true));
            _hintSequence.Join(_hintToView.transform.DOMove(fromTargetPosition, _hintSwapDuration).SetEase(Ease.InOutSine).SetRecyclable(true));

            if (_hintReturnPause > 0f)
            {
                _hintSequence.AppendInterval(_hintReturnPause);
            }

            _hintSequence.Append(_hintFromView.transform.DOMove(fromTargetPosition, _hintSwapDuration).SetEase(Ease.InOutSine).SetRecyclable(true));
            _hintSequence.Join(_hintToView.transform.DOMove(toTargetPosition, _hintSwapDuration).SetEase(Ease.InOutSine).SetRecyclable(true));
            _hintSequence.Append(_hintFromView.transform.DOScale(_hintFromBaseScale, _hintScaleDuration).SetEase(Ease.InOutSine).SetRecyclable(true));
            _hintSequence.Join(_hintToView.transform.DOScale(_hintToBaseScale, _hintScaleDuration).SetEase(Ease.InOutSine).SetRecyclable(true));

            if (_hintBlinkInterval > 0f)
            {
                _hintSequence.AppendInterval(_hintBlinkInterval);
            }

            _hintSequence.OnComplete(() =>
            {
                RestoreHintViews();
                _hintBlinkTimer = 0f;
                _hintSequence = null;
            });
            _hintSequence.OnKill(() => _hintSequence = null);
        }

        private bool TryResolveHintViews(out TileView fromView, out TileView toView)
        {
            fromView = _boardManager.GetView(_hintFrom);
            toView = _boardManager.GetView(_hintTo);
            return fromView != null && toView != null;
        }

        private void StopHintSequenceAndRestore()
        {
            if (_hintSequence != null && _hintSequence.IsActive())
            {
                _hintSequence.Kill();
            }

            RestoreHintViews();
        }

        private void RestoreHintViews()
        {
            if (_boardManager?.Model == null)
            {
                _hintFromView = null;
                _hintToView = null;
                return;
            }

            if (_hintFromView != null)
            {
                _hintFromView.transform.DOKill();
                _hintFromView.transform.position = _boardManager.GridToWorld(_hintFrom);
                _hintFromView.transform.localScale = _hintFromBaseScale != Vector3.zero ? _hintFromBaseScale : _hintFromView.transform.localScale;
                _hintFromView.SetHighlight(false);
            }

            if (_hintToView != null)
            {
                _hintToView.transform.DOKill();
                _hintToView.transform.position = _boardManager.GridToWorld(_hintTo);
                _hintToView.transform.localScale = _hintToBaseScale != Vector3.zero ? _hintToBaseScale : _hintToView.transform.localScale;
                _hintToView.SetHighlight(false);
            }

            _hintFromView = null;
            _hintToView = null;
            _hintFromBaseScale = Vector3.zero;
            _hintToBaseScale = Vector3.zero;
        }
#endif

        private void Select(GridPosition position)
        {
            _selectedPosition = position;
            _boardManager.SetHighlight(position, true);
        }

        private void ClearSelection()
        {
            if (_selectedPosition.HasValue)
            {
                _boardManager.SetHighlight(_selectedPosition.Value, false);
            }

            _selectedPosition = null;
        }
    }
}




