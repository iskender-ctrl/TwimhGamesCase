using System;
using System.Collections.Generic;
using UnityEngine;
using TwimhGames.Puzzle.Tiles;

namespace TwimhGames.Puzzle.Config
{
    [CreateAssetMenu(menuName = "TwimhGames/Puzzle/Tile Catalog", fileName = "TileCatalog")]
    public sealed class TileCatalogSO : ScriptableObject
    {
        [SerializeField] private List<TileDefinition> _definitions = new List<TileDefinition>();

        private readonly Dictionary<TileKind, TileDefinition> _cache = new Dictionary<TileKind, TileDefinition>();
        private readonly List<TileKind> _randomKinds = new List<TileKind>();

        public IReadOnlyList<TileDefinition> Definitions => _definitions;

        private void OnEnable()
        {
            EnsureDefinitionsPopulated();
            RebuildCache();
        }

        private void OnValidate()
        {
            EnsureDefinitionsPopulated();
            RebuildCache();
        }

        public void RebuildCache()
        {
            EnsureDefinitionsPopulated();
            _cache.Clear();
            _randomKinds.Clear();

            for (var i = 0; i < _definitions.Count; i++)
            {
                var definition = _definitions[i];
                if (_cache.ContainsKey(definition.Kind))
                {
                    continue;
                }

                _cache.Add(definition.Kind, definition);
                _randomKinds.Add(definition.Kind);
            }
        }

        public void SetDefinitions(IEnumerable<TileDefinition> definitions)
        {
            _definitions = definitions != null
                ? new List<TileDefinition>(definitions)
                : new List<TileDefinition>();

            EnsureDefinitionsPopulated();
            RebuildCache();
        }

        public bool TryGetDefinition(TileKind kind, out TileDefinition definition)
        {
            if (_cache.Count == 0)
            {
                RebuildCache();
            }

            return _cache.TryGetValue(kind, out definition);
        }

        public TileKind GetRandomKind(System.Random random)
        {
            if (_randomKinds.Count == 0)
            {
                RebuildCache();
            }

            if (_randomKinds.Count == 0)
            {
                return GetFallbackKind();
            }

            return _randomKinds[random.Next(0, _randomKinds.Count)];
        }

        public static TileCatalogSO CreateRuntimeDefault()
        {
            var catalog = CreateInstance<TileCatalogSO>();
            catalog.EnsureDefinitionsPopulated();
            catalog.RebuildCache();
            return catalog;
        }

        private void EnsureDefinitionsPopulated()
        {
            _definitions ??= new List<TileDefinition>();

            var existingKinds = new HashSet<TileKind>();
            for (var i = 0; i < _definitions.Count; i++)
            {
                existingKinds.Add(_definitions[i].Kind);
            }

            var allKinds = (TileKind[])Enum.GetValues(typeof(TileKind));
            for (var i = 0; i < allKinds.Length; i++)
            {
                var kind = allKinds[i];
                if (existingKinds.Contains(kind))
                {
                    continue;
                }

                _definitions.Add(new TileDefinition(kind, null, Color.white));
            }
        }

        private static TileKind GetFallbackKind()
        {
            var allKinds = (TileKind[])Enum.GetValues(typeof(TileKind));
            if (allKinds.Length == 0)
            {
                return default;
            }

            return allKinds[0];
        }
    }

    [Serializable]
    public struct TileDefinition
    {
        public TileKind Kind;
        public Sprite Sprite;
        public Color Color;

        public TileDefinition(TileKind kind, Sprite sprite, Color color)
        {
            Kind = kind;
            Sprite = sprite;
            Color = color;
        }
    }
}
