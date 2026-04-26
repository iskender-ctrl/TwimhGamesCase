using System;
using TwimhGames.Puzzle.Config;
using TwimhGames.Puzzle.StateMachine;
using UnityEngine.UI;

namespace TwimhGames.Puzzle.Core
{
    public sealed class LevelNavigationController
    {
        private readonly BoardConfigSO _boardConfig;
        private readonly GameStateMachine _stateMachine;
        private readonly Action _reloadSelectedLevel;
        private Button _previousButton;
        private Button _nextButton;

        public LevelNavigationController(
            BoardConfigSO boardConfig,
            GameStateMachine stateMachine,
            Action reloadSelectedLevel)
        {
            _boardConfig = boardConfig;
            _stateMachine = stateMachine;
            _reloadSelectedLevel = reloadSelectedLevel;
        }

        public void Attach(Button previousButton, Button nextButton)
        {
            Detach();

            _previousButton = previousButton;
            _nextButton = nextButton;

            if (_previousButton != null)
            {
                _previousButton.onClick.AddListener(HandlePreviousClicked);
            }

            if (_nextButton != null)
            {
                _nextButton.onClick.AddListener(HandleNextClicked);
            }

            Refresh();
        }

        public void Detach()
        {
            if (_previousButton != null)
            {
                _previousButton.onClick.RemoveListener(HandlePreviousClicked);
            }

            if (_nextButton != null)
            {
                _nextButton.onClick.RemoveListener(HandleNextClicked);
            }

            _previousButton = null;
            _nextButton = null;
        }

        public bool TryGoPreviousLevel()
        {
            return TryNavigate(selectPrevious: true);
        }

        public bool TryGoNextLevel()
        {
            return TryNavigate(selectPrevious: false);
        }

        public void Refresh()
        {
            var levelLayout = _boardConfig != null ? _boardConfig.LevelLayout : null;
            var canNavigate = levelLayout != null && _stateMachine != null && _stateMachine.IsIn(GameState.Idle);

            if (_previousButton != null)
            {
                _previousButton.interactable = canNavigate && levelLayout.CanSelectPreviousLevel;
            }

            if (_nextButton != null)
            {
                _nextButton.interactable = canNavigate && levelLayout.CanSelectNextLevel;
            }
        }

        private void HandlePreviousClicked()
        {
            if (!TryGoPreviousLevel())
            {
                Refresh();
            }
        }

        private void HandleNextClicked()
        {
            if (!TryGoNextLevel())
            {
                Refresh();
            }
        }

        private bool TryNavigate(bool selectPrevious)
        {
            var levelLayout = _boardConfig != null ? _boardConfig.LevelLayout : null;
            if (levelLayout == null || _stateMachine == null || !_stateMachine.IsIn(GameState.Idle))
            {
                return false;
            }

            var changed = selectPrevious
                ? levelLayout.SelectPreviousLevel()
                : levelLayout.SelectNextLevel();

            if (!changed)
            {
                return false;
            }

            _reloadSelectedLevel?.Invoke();
            return true;
        }
    }
}
