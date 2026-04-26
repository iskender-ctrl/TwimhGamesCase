using System;
using System.Collections;
using System.Collections.Generic;
using TwimhGames.Puzzle.Config;
using TwimhGames.Puzzle.Grid;
using TwimhGames.Puzzle.Pooling;
using TwimhGames.Puzzle.Tiles;
using TwimhGames.Puzzle.Visual;
using UnityEngine;
#if DOTWEEN || DOTWEEN_ENABLED
using DG.Tweening;
#endif

namespace TwimhGames.Puzzle.Core
{
    public sealed class BoardManager : MonoBehaviour
    {
        private static Sprite _backgroundSprite;
        private readonly List<int> _playableRowsBuffer = new List<int>();

        private BoardConfigSO _config;
        private PlayableBoardGenerator _boardGenerator;
        private TileCatalogSO _tileCatalog;
        private TilePoolManager _tilePool;
        private System.Random _random;
        private Transform _backgroundRoot;
        private Transform _tilesRoot;
        private BoardModel _boardModel;
        private TileView[,] _views;
        private float _startX;
        private float _startY;

        public BoardModel Model => _boardModel;
        public int Width => _boardModel.Width;
        public int Height => _boardModel.Height;

        public void Initialize(BoardConfigSO config, TileCatalogSO tileCatalog, TilePoolManager tilePool, PlayableBoardGenerator boardGenerator, int? seed = null)
        {
            ResetBoard();
            _config = config;
            _tileCatalog = tileCatalog;
            _tileCatalog.RebuildCache();
            _tilePool = tilePool;
            _boardGenerator = boardGenerator ?? throw new ArgumentNullException(nameof(boardGenerator));
            _random = seed.HasValue ? new System.Random(seed.Value) : new System.Random();
            var cellStep = _config.CellStep;

            _tilePool.Initialize(_config.FruitSize, _config.Visual);
            EnsureTileRoot();

            var playableMask = _config.BuildPlayableMask();
            var width = playableMask.GetLength(0);
            var height = playableMask.GetLength(1);

            _boardModel = new BoardModel(width, height, playableMask);
            _views = new TileView[width, height];

            _startX = -((width - 1) * cellStep) * 0.5f;
            _startY = -((height - 1) * cellStep) * 0.5f;

            RebuildBoardBackground();
            BuildInitialBoard();
        }

        public void ResetBoard()
        {
            StopAllCoroutines();

            if (_tilesRoot != null)
            {
                for (var i = _tilesRoot.childCount - 1; i >= 0; i--)
                {
                    var child = _tilesRoot.GetChild(i);
                    if (!child.TryGetComponent<TileView>(out var view))
                    {
#if UNITY_EDITOR
                        if (!Application.isPlaying)
                        {
                            DestroyImmediate(child.gameObject);
                            continue;
                        }
#endif
                        Destroy(child.gameObject);
                        continue;
                    }

#if DOTWEEN || DOTWEEN_ENABLED
                    child.DOKill();
#endif
                    _tilePool?.Release(view);
                }
            }

            _views = null;
            _boardModel = null;
            _startX = 0f;
            _startY = 0f;
            ClearBackgroundChildren();
        }

        public TileModel GetTile(GridPosition position)
        {
            return _boardModel.GetTile(position);
        }

        public TileView GetView(GridPosition position)
        {
            if (!_boardModel.IsPlayable(position))
            {
                return null;
            }

            return _views[position.X, position.Y];
        }

        public void SetHighlight(GridPosition position, bool highlighted)
        {
            var view = GetView(position);
            if (view == null)
            {
                return;
            }

            view.SetHighlight(highlighted);
        }

        public Vector3 GridToWorld(GridPosition position)
        {
            var cellStep = _config.CellStep;
            return _tilesRoot.position + new Vector3(
                _startX + (position.X * cellStep),
                _startY + (position.Y * cellStep),
                0f
            );
        }

        public void SwapCells(GridPosition a, GridPosition b)
        {
            if (!_boardModel.IsPlayable(a) || !_boardModel.IsPlayable(b))
            {
                return;
            }

            var tileA = _boardModel.GetTile(a);
            var tileB = _boardModel.GetTile(b);
            var viewA = _views[a.X, a.Y];
            var viewB = _views[b.X, b.Y];

            _boardModel.SetTile(a, tileB);
            _boardModel.SetTile(b, tileA);

            _views[a.X, a.Y] = viewB;
            _views[b.X, b.Y] = viewA;

            if (viewA != null)
            {
                viewA.SetGridPosition(b);
            }

            if (viewB != null)
            {
                viewB.SetGridPosition(a);
            }
        }

        public IEnumerator AnimateSwap(GridPosition a, GridPosition b, float duration)
        {
            if (!_boardModel.IsInside(a) || !_boardModel.IsInside(b))
            {
                yield break;
            }

            var moves = new List<TileMove>(2);
            var viewAtA = _views[a.X, a.Y];
            var viewAtB = _views[b.X, b.Y];

            if (viewAtA != null)
            {
                moves.Add(new TileMove(viewAtA, GridToWorld(a)));
            }

            if (viewAtB != null)
            {
                moves.Add(new TileMove(viewAtB, GridToWorld(b)));
            }

            yield return AnimateMoves(moves, duration);
        }

        public IEnumerator AnimateMoves(List<TileMove> moves, float duration, float durationPerCell = 0f, float maxDuration = 0f)
        {
            if (moves == null || moves.Count == 0)
            {
                yield break;
            }

            var useDistanceAwareDuration = durationPerCell > 0f;
            if (duration <= 0f && !useDistanceAwareDuration)
            {
                for (var i = 0; i < moves.Count; i++)
                {
                    var view = moves[i].View;
                    if (view == null)
                    {
                        continue;
                    }

                    view.transform.position = moves[i].TargetWorldPosition;
                }

                yield break;
            }

            var validMoveCount = 0;
            for (var i = 0; i < moves.Count; i++)
            {
                if (moves[i].View != null)
                {
                    validMoveCount++;
                }
            }

            if (validMoveCount == 0)
            {
                yield break;
            }

            var startPositions = new Vector3[validMoveCount];
            var targetPositions = new Vector3[validMoveCount];
            var moveDurations = new float[validMoveCount];
            var views = new TileView[validMoveCount];

            var cursor = 0;
            var longestDuration = 0f;
            for (var i = 0; i < moves.Count; i++)
            {
                var view = moves[i].View;
                if (view == null)
                {
                    continue;
                }

                views[cursor] = view;
                startPositions[cursor] = view.transform.position;
                targetPositions[cursor] = moves[i].TargetWorldPosition;
                moveDurations[cursor] = ResolveMoveDuration(view, targetPositions[cursor], duration, durationPerCell, maxDuration);
                longestDuration = Mathf.Max(longestDuration, moveDurations[cursor]);
                cursor++;
            }

            if (longestDuration <= 0f)
            {
                for (var i = 0; i < views.Length; i++)
                {
                    views[i].transform.position = targetPositions[i];
                }

                yield break;
            }

#if DOTWEEN || DOTWEEN_ENABLED
            var sequence = DOTween.Sequence().SetRecyclable(true);
            var tweenCount = 0;

            for (var i = 0; i < views.Length; i++)
            {
                var view = views[i];
                var transformRef = view.transform;
                transformRef.DOKill();

                sequence.Join(
                    transformRef
                        .DOMove(targetPositions[i], moveDurations[i])
                        .SetEase(Ease.InOutCubic)
                        .SetRecyclable(true)
                        .SetLink(view.gameObject));

                tweenCount++;
            }

            if (tweenCount > 0)
            {
                yield return sequence.WaitForCompletion();
                yield break;
            }
#endif

            var elapsed = 0f;
            while (elapsed < longestDuration)
            {
                elapsed += Time.deltaTime;

                for (var i = 0; i < views.Length; i++)
                {
                    var moveDuration = moveDurations[i];
                    var t = moveDuration <= 0f ? 1f : Mathf.Clamp01(elapsed / moveDuration);
                    var easedT = Mathf.SmoothStep(0f, 1f, t);
                    views[i].transform.position = Vector3.LerpUnclamped(startPositions[i], targetPositions[i], easedT);
                }

                yield return null;
            }

            for (var i = 0; i < views.Length; i++)
            {
                views[i].transform.position = targetPositions[i];
            }
        }


        private float ResolveMoveDuration(
            TileView view,
            Vector3 targetWorldPosition,
            float baseDuration,
            float durationPerCell,
            float maxDuration)
        {
            var resolvedDuration = Mathf.Max(0f, baseDuration);

            if (durationPerCell > 0f && _config != null)
            {
                var cellStep = Mathf.Max(0.0001f, _config.CellStep);
                var travelDistance = Vector3.Distance(view.transform.position, targetWorldPosition);
                var traveledCells = travelDistance / cellStep;
                resolvedDuration += traveledCells * durationPerCell;
            }

            if (maxDuration > 0f)
            {
                resolvedDuration = Mathf.Min(maxDuration, resolvedDuration);
            }

            return Mathf.Max(0f, resolvedDuration);
        }
        public void ClearPositions(IEnumerable<GridPosition> clearPositions)
        {
            var ordered = CollectOrderedPlayablePositions(clearPositions);
            for (var i = 0; i < ordered.Count; i++)
            {
                ClearCell(ordered[i]);
            }
        }

        public IEnumerator ClearPositionsAnimated(IEnumerable<GridPosition> clearPositions, float duration)
        {
            var ordered = CollectOrderedPlayablePositions(clearPositions);
            if (ordered.Count == 0)
            {
                yield break;
            }

#if DOTWEEN || DOTWEEN_ENABLED
            if (duration > 0f)
            {
                var sequence = DOTween.Sequence().SetRecyclable(true);
                var tweenCount = 0;

                for (var i = 0; i < ordered.Count; i++)
                {
                    var position = ordered[i];
                    var view = _views[position.X, position.Y];
                    if (view == null)
                    {
                        continue;
                    }

                    var cachedTransform = view.transform;
                    cachedTransform.DOKill();
                    sequence.Join(
                        cachedTransform
                            .DOScale(Vector3.zero, duration)
                            .SetEase(Ease.InQuad)
                            .SetRecyclable(true)
                            .SetLink(view.gameObject));

                    tweenCount++;
                }

                if (tweenCount > 0)
                {
                    yield return sequence.WaitForCompletion();
                }
            }
#endif

            for (var i = 0; i < ordered.Count; i++)
            {
                ClearCell(ordered[i]);
            }
        }

        public void SpawnSpecialTile(GridPosition position, TileKind kind, SpecialTileKind specialKind)
        {
            if (!_boardModel.IsPlayable(position))
            {
                return;
            }

            if (_boardModel.GetTile(position) != null)
            {
                ClearCell(position);
            }

            SpawnTile(position, kind, specialKind, GridToWorld(position));
        }

        public List<TileMove> ApplyGravity()
        {
            var moves = new List<TileMove>();

            for (var x = 0; x < _boardModel.Width; x++)
            {
                CollectPlayableRows(x, _playableRowsBuffer);
                var nextPlayableIndex = 0;

                for (var i = 0; i < _playableRowsBuffer.Count; i++)
                {
                    var y = _playableRowsBuffer[i];
                    var from = new GridPosition(x, y);
                    var tile = _boardModel.GetTile(from);
                    if (tile == null)
                    {
                        continue;
                    }

                    var targetY = _playableRowsBuffer[nextPlayableIndex];
                    if (y != targetY)
                    {
                        var to = new GridPosition(x, targetY);
                        var view = _views[x, y];

                        _boardModel.SetTile(to, tile);
                        _boardModel.SetTile(from, null);

                        _views[x, targetY] = view;
                        _views[x, y] = null;

                        if (view != null)
                        {
                            view.SetGridPosition(to);
                            moves.Add(new TileMove(view, GridToWorld(to)));
                        }
                    }

                    nextPlayableIndex++;
                }

                for (var i = nextPlayableIndex; i < _playableRowsBuffer.Count; i++)
                {
                    var clearY = _playableRowsBuffer[i];
                    _boardModel.SetTile(new GridPosition(x, clearY), null);
                    _views[x, clearY] = null;
                }
            }

            return moves;
        }

        public List<TileMove> Refill()
        {
            var moves = new List<TileMove>();

            for (var x = 0; x < _boardModel.Width; x++)
            {
                var spawnOffset = 0;
                CollectPlayableRows(x, _playableRowsBuffer);

                for (var i = 0; i < _playableRowsBuffer.Count; i++)
                {
                    var y = _playableRowsBuffer[i];
                    var position = new GridPosition(x, y);
                    if (_boardModel.GetTile(position) != null)
                    {
                        continue;
                    }

                    var spawnGridPos = new GridPosition(x, _boardModel.Height + _config.RefillSpawnBufferRows + spawnOffset);
                    var spawnWorldPosition = GridToWorld(spawnGridPos);
                    var kind = _tileCatalog.GetRandomKind(_random);

                    SpawnTile(position, kind, SpecialTileKind.None, spawnWorldPosition);

                    var view = _views[position.X, position.Y];
                    if (view != null)
                    {
                        moves.Add(new TileMove(view, GridToWorld(position)));
                    }

                    spawnOffset++;
                }
            }

            return moves;
        }

        public List<TileMove> ShuffleBoard()
        {
            var generatedKinds = _boardGenerator.Generate(_boardModel, _random, avoidImmediateMatches: true);
            var positions = new List<GridPosition>();
            var views = new List<TileView>();

            for (var y = 0; y < _boardModel.Height; y++)
            {
                for (var x = 0; x < _boardModel.Width; x++)
                {
                    var position = new GridPosition(x, y);
                    if (!_boardModel.IsPlayable(position))
                    {
                        continue;
                    }

                    positions.Add(position);

                    var view = _views[x, y];
                    if (view != null)
                    {
                        views.Add(view);
                    }
                }
            }

            if (views.Count != positions.Count)
            {
                ReplaceBoardContents(generatedKinds);
                return new List<TileMove>(0);
            }

            for (var i = views.Count - 1; i > 0; i--)
            {
                var j = _random.Next(0, i + 1);
                (views[i], views[j]) = (views[j], views[i]);
            }

            var moves = new List<TileMove>(positions.Count);
            for (var i = 0; i < positions.Count; i++)
            {
                var position = positions[i];
                var view = views[i];
                var kind = generatedKinds[position.X, position.Y];

                _boardModel.SetTile(position, new TileModel(kind));
                _views[position.X, position.Y] = view;

                ApplyTileView(view, position, kind, SpecialTileKind.None);
                moves.Add(new TileMove(view, GridToWorld(position)));
            }

            return moves;
        }
        private void BuildInitialBoard()
        {
            var generatedKinds = _boardGenerator.Generate(_boardModel, _random, _config.AvoidInitialMatches);
            SpawnGeneratedBoard(generatedKinds);
        }

        private void SpawnGeneratedBoard(TileKind[,] generatedKinds)
        {
            for (var y = 0; y < _boardModel.Height; y++)
            {
                for (var x = 0; x < _boardModel.Width; x++)
                {
                    var position = new GridPosition(x, y);
                    if (!_boardModel.IsPlayable(position))
                    {
                        continue;
                    }

                    SpawnTile(position, generatedKinds[x, y], SpecialTileKind.None, GridToWorld(position));
                }
            }
        }
        private void SpawnTile(GridPosition position, TileKind kind, SpecialTileKind specialKind, Vector3 startWorldPosition)
        {
            if (!_boardModel.IsPlayable(position))
            {
                return;
            }

            var tile = new TileModel(kind, specialKind);
            _boardModel.SetTile(position, tile);

            var view = _tilePool.Acquire(_tilesRoot);
            ApplyTileView(view, position, kind, specialKind);
            view.transform.position = startWorldPosition;

            _views[position.X, position.Y] = view;
        }

        private void ApplyTileView(TileView view, GridPosition position, TileKind kind, SpecialTileKind specialKind)
        {
            ResolveVisual(kind, out var sprite, out var color);
            view.Apply(position, sprite, color, specialKind);
        }
        private void ResolveVisual(TileKind kind, out Sprite sprite, out Color color)
        {
            if (_tileCatalog.TryGetDefinition(kind, out var definition))
            {
                sprite = definition.Sprite;
                color = definition.Color;
                return;
            }

            sprite = null;
            color = Color.white;
        }

        private void ReplaceBoardContents(TileKind[,] generatedKinds)
        {
            for (var y = 0; y < _boardModel.Height; y++)
            {
                for (var x = 0; x < _boardModel.Width; x++)
                {
                    var position = new GridPosition(x, y);
                    if (!_boardModel.IsPlayable(position))
                    {
                        continue;
                    }

                    ClearCell(position);
                }
            }

            SpawnGeneratedBoard(generatedKinds);
        }

        private void ClearCell(GridPosition position)
        {
            if (!_boardModel.IsPlayable(position))
            {
                return;
            }

            var view = _views[position.X, position.Y];
            if (view != null)
            {
                _tilePool.Release(view);
            }

            _views[position.X, position.Y] = null;
            _boardModel.SetTile(position, null);
        }

        private List<GridPosition> CollectOrderedPlayablePositions(IEnumerable<GridPosition> clearPositions)
        {
            if (clearPositions == null)
            {
                return new List<GridPosition>(0);
            }

            var uniquePositions = new HashSet<GridPosition>();
            var orderedPositions = new List<GridPosition>();

            foreach (var position in clearPositions)
            {
                if (!_boardModel.IsPlayable(position))
                {
                    continue;
                }

                if (!uniquePositions.Add(position))
                {
                    continue;
                }

                orderedPositions.Add(position);
            }

            orderedPositions.Sort((a, b) =>
            {
                var yCompare = a.Y.CompareTo(b.Y);
                return yCompare != 0 ? yCompare : a.X.CompareTo(b.X);
            });

            return orderedPositions;
        }

        private void CollectPlayableRows(int columnX, List<int> result)
        {
            result.Clear();

            for (var y = 0; y < _boardModel.Height; y++)
            {
                if (_boardModel.IsPlayable(new GridPosition(columnX, y)))
                {
                    result.Add(y);
                }
            }
        }

        private void EnsureTileRoot()
        {
            if (_tilesRoot != null)
            {
                return;
            }

            var rootObject = new GameObject("TilesRoot");
            _tilesRoot = rootObject.transform;
            _tilesRoot.SetParent(transform, false);
        }

        private void EnsureBackgroundRoot()
        {
            if (_backgroundRoot != null)
            {
                return;
            }

            var rootObject = new GameObject("BoardBackgroundRoot");
            _backgroundRoot = rootObject.transform;
            _backgroundRoot.SetParent(transform, false);
        }

        private void RebuildBoardBackground()
        {
            if (!_config.Visual.ShowBoardBackground)
            {
                if (_backgroundRoot != null)
                {
                    ClearBackgroundChildren();
                    _backgroundRoot.gameObject.SetActive(false);
                }

                return;
            }

            EnsureBackgroundRoot();
            _backgroundRoot.gameObject.SetActive(true);
            BuildBoardBackground();
        }

        private void BuildBoardBackground()
        {
            ClearBackgroundChildren();
            EnsureBackgroundSprite();

            var visual = _config.Visual;
            var cellStep = _config.CellStep;

            var panelWidth = CalculateBoardSpan(_boardModel.Width) + (visual.PanelPadding * 2f);
            var panelHeight = CalculateBoardSpan(_boardModel.Height) + (visual.PanelPadding * 2f);
            CreateSquareVisual(
                "BoardPanel",
                new Vector3(0f, 0f, 0.3f),
                new Vector3(panelWidth, panelHeight, 1f),
                visual.PanelColor);

            var cellWorldSize = _config.SlotSize;

            for (var y = 0; y < _boardModel.Height; y++)
            {
                for (var x = 0; x < _boardModel.Width; x++)
                {
                    var position = new GridPosition(x, y);
                    var isPlayable = _boardModel.IsPlayable(position);
                    if (!isPlayable && !visual.ShowBlockedCells)
                    {
                        continue;
                    }

                    var localPosition = new Vector3(
                        _startX + (x * cellStep),
                        _startY + (y * cellStep),
                        0.15f);

                    CreateSquareVisual(
                        $"Cell_{x}_{y}",
                        localPosition,
                        new Vector3(cellWorldSize, cellWorldSize, 1f),
                        isPlayable ? visual.PlayableCellColor : visual.BlockedCellColor);
                }
            }
        }

        private void ClearBackgroundChildren()
        {
            if (_backgroundRoot == null)
            {
                return;
            }

            for (var i = _backgroundRoot.childCount - 1; i >= 0; i--)
            {
                var child = _backgroundRoot.GetChild(i).gameObject;
#if UNITY_EDITOR
                if (!Application.isPlaying)
                {
                    DestroyImmediate(child);
                    continue;
                }
#endif
                Destroy(child);
            }
        }

        private void CreateSquareVisual(string objectName, Vector3 localPosition, Vector3 localScale, Color color)
        {
            var visualObject = new GameObject(objectName);
            visualObject.transform.SetParent(_backgroundRoot, false);
            visualObject.transform.localPosition = localPosition;
            visualObject.transform.localScale = localScale;

            var renderer = visualObject.AddComponent<SpriteRenderer>();
            renderer.sprite = _backgroundSprite;
            renderer.color = color;
            renderer.sortingOrder = 0;
        }

        private float CalculateBoardSpan(int cellCount)
        {
            if (cellCount <= 0)
            {
                return 0f;
            }

            return ((cellCount - 1) * _config.CellStep) + _config.SlotSize;
        }

        private static void EnsureBackgroundSprite()
        {
            if (_backgroundSprite != null)
            {
                return;
            }

            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Point,
                hideFlags = HideFlags.HideAndDontSave
            };
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();

            _backgroundSprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, 1f, 1f),
                new Vector2(0.5f, 0.5f),
                1f);
            _backgroundSprite.name = "BoardBackgroundRuntimeSprite";
        }


        public readonly struct TileMove
        {
            public TileView View { get; }
            public Vector3 TargetWorldPosition { get; }

            public TileMove(TileView view, Vector3 targetWorldPosition)
            {
                View = view;
                TargetWorldPosition = targetWorldPosition;
            }
        }

    }
}











