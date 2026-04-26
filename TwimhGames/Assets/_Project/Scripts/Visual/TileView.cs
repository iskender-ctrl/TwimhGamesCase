using TwimhGames.Puzzle.Config;
using TwimhGames.Puzzle.Grid;
using TwimhGames.Puzzle.Tiles;
using UnityEngine;

namespace TwimhGames.Puzzle.Visual
{
    [RequireComponent(typeof(SpriteRenderer))]
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class TileView : MonoBehaviour
    {
        private static Sprite _runtimeSprite;

        private SpriteRenderer _spriteRenderer;
        private BoxCollider2D _boxCollider;
        private BoardVisualSettings _visualSettings;
        private Transform _lightningHorizontalMarker;
        private Transform _lightningVerticalMarker;
        private Transform _bombMarker;
        private Transform _colorMarkerA;
        private Transform _colorMarkerB;
        private float _targetWorldSize = 1f;
        private Vector3 _baseScale = Vector3.one;
        private Sprite _baseTileSprite;
        private Color _baseTileColor = Color.white;

        public GridPosition GridPosition { get; private set; }

        private void Awake()
        {
            _spriteRenderer = GetComponent<SpriteRenderer>();
            _boxCollider = GetComponent<BoxCollider2D>();

            EnsureRuntimeSprite();
            _baseTileSprite = _runtimeSprite;
            _spriteRenderer.sprite = _runtimeSprite;
            _spriteRenderer.sortingOrder = 0;
        }

        public void Configure(float tileScale, BoardVisualSettings visualSettings)
        {
            EnsureRuntimeSprite();
            _targetWorldSize = Mathf.Max(0.01f, tileScale);
            _visualSettings = visualSettings;

            if (_baseTileSprite == null)
            {
                _baseTileSprite = _runtimeSprite;
            }

            _spriteRenderer.sprite = _baseTileSprite;
            _spriteRenderer.color = _baseTileColor;
            UpdateScaleAndCollider();

            _boxCollider.offset = Vector2.zero;
        }

        public void Apply(GridPosition gridPosition, Sprite sprite, Color color, SpecialTileKind specialKind)
        {
            GridPosition = gridPosition;
            _baseTileSprite = sprite != null ? sprite : _runtimeSprite;
            _baseTileColor = color;

            _spriteRenderer.sprite = _baseTileSprite;
            _spriteRenderer.color = _baseTileColor;

            SetSpecialMarker(specialKind);
            UpdateScaleAndCollider();
        }

        public void SetGridPosition(GridPosition position)
        {
            GridPosition = position;
        }

        public void SetHighlight(bool highlighted)
        {
            transform.localScale = highlighted ? _baseScale * 1.1f : _baseScale;
        }

        private void SetSpecialMarker(SpecialTileKind specialKind)
        {
            var isLightning = specialKind == SpecialTileKind.Lightning;
            var isBomb = specialKind == SpecialTileKind.Bomb;
            var isColor = specialKind == SpecialTileKind.Color;

            var specialIcon = ResolveSpecialIcon(specialKind);
            var useSpecialIconAsMainSprite = specialKind != SpecialTileKind.None && specialIcon != null;

            if (useSpecialIconAsMainSprite)
            {
                _spriteRenderer.sprite = specialIcon;
                _spriteRenderer.color = _visualSettings.SpecialIconTint;
            }
            else
            {
                _spriteRenderer.sprite = _baseTileSprite;
                _spriteRenderer.color = _baseTileColor;
            }

            var showFallbackMarkers = specialKind != SpecialTileKind.None && !useSpecialIconAsMainSprite;
            if (showFallbackMarkers)
            {
                EnsureSpecialMarkers();
            }

            if (_lightningHorizontalMarker != null)
            {
                _lightningHorizontalMarker.gameObject.SetActive(showFallbackMarkers && isLightning);
            }

            if (_lightningVerticalMarker != null)
            {
                _lightningVerticalMarker.gameObject.SetActive(showFallbackMarkers && isLightning);
            }

            if (_bombMarker != null)
            {
                _bombMarker.gameObject.SetActive(showFallbackMarkers && isBomb);
            }

            if (_colorMarkerA != null)
            {
                _colorMarkerA.gameObject.SetActive(showFallbackMarkers && isColor);
            }

            if (_colorMarkerB != null)
            {
                _colorMarkerB.gameObject.SetActive(showFallbackMarkers && isColor);
            }
        }

        private void EnsureSpecialMarkers()
        {
            if (_lightningHorizontalMarker != null &&
                _lightningVerticalMarker != null &&
                _bombMarker != null &&
                _colorMarkerA != null &&
                _colorMarkerB != null)
            {
                return;
            }

            _lightningHorizontalMarker = CreateMarker(
                "LightningHorizontalMarker",
                new Vector3(0.52f, 0.11f, 1f),
                Quaternion.identity,
                new Color(0.08f, 0.08f, 0.08f, 0.92f));

            _lightningVerticalMarker = CreateMarker(
                "LightningVerticalMarker",
                new Vector3(0.11f, 0.52f, 1f),
                Quaternion.identity,
                new Color(0.08f, 0.08f, 0.08f, 0.92f));

            _bombMarker = CreateMarker(
                "BombMarker",
                new Vector3(0.34f, 0.34f, 1f),
                Quaternion.identity,
                new Color(0.07f, 0.07f, 0.07f, 0.94f));

            _colorMarkerA = CreateMarker(
                "ColorMarkerA",
                new Vector3(0.6f, 0.12f, 1f),
                Quaternion.Euler(0f, 0f, 45f),
                new Color(0.18f, 0.86f, 0.95f, 0.92f));

            _colorMarkerB = CreateMarker(
                "ColorMarkerB",
                new Vector3(0.6f, 0.12f, 1f),
                Quaternion.Euler(0f, 0f, -45f),
                new Color(0.97f, 0.32f, 0.69f, 0.92f));
        }

        private Transform CreateMarker(string markerName, Vector3 scale, Quaternion localRotation, Color color)
        {
            var markerObject = new GameObject(markerName);
            markerObject.transform.SetParent(transform, false);
            markerObject.transform.localPosition = new Vector3(0f, 0f, -0.01f);
            markerObject.transform.localRotation = localRotation;
            markerObject.transform.localScale = scale;

            var renderer = markerObject.AddComponent<SpriteRenderer>();
            renderer.sprite = _runtimeSprite;
            renderer.color = color;
            renderer.sortingOrder = 0;

            markerObject.SetActive(false);
            return markerObject.transform;
        }

        private void UpdateScaleAndCollider()
        {
            var currentSprite = _spriteRenderer.sprite != null ? _spriteRenderer.sprite : _runtimeSprite;
            var spriteSize = currentSprite != null ? currentSprite.bounds.size : Vector3.one;
            var maxDimension = Mathf.Max(0.0001f, Mathf.Max(spriteSize.x, spriteSize.y));
            var uniformScale = _targetWorldSize / maxDimension;

            _baseScale = Vector3.one * uniformScale;
            transform.localScale = _baseScale;

            _boxCollider.size = new Vector2(
                Mathf.Max(0.01f, spriteSize.x),
                Mathf.Max(0.01f, spriteSize.y));
        }

        private Sprite ResolveSpecialIcon(SpecialTileKind specialKind)
        {
            switch (specialKind)
            {
                case SpecialTileKind.Color:
                    return _visualSettings.ColorSpecialIcon;
                case SpecialTileKind.Bomb:
                    return _visualSettings.BombSpecialIcon;
                case SpecialTileKind.Lightning:
                    return _visualSettings.LightningSpecialIcon;
                default:
                    return null;
            }
        }

        private static void EnsureRuntimeSprite()
        {
            if (_runtimeSprite != null)
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

            _runtimeSprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, 1f, 1f),
                new Vector2(0.5f, 0.5f),
                1f
            );
            _runtimeSprite.name = "PuzzleTileRuntimeSprite";
        }
    }
}

