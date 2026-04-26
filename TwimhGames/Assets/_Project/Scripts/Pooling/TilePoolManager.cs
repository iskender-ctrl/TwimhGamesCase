using System.Collections.Generic;
using TwimhGames.Puzzle.Config;
using TwimhGames.Puzzle.Visual;
using UnityEngine;

namespace TwimhGames.Puzzle.Pooling
{
    public sealed class TilePoolManager : MonoBehaviour
    {
        [SerializeField, Min(8)] private int _initialCapacity = 48;

        private readonly Queue<TileView> _pool = new Queue<TileView>();
        private Transform _poolRoot;
        private float _tileScale = 1f;
        private BoardVisualSettings _visualSettings;

        public void Initialize(float tileScale, BoardVisualSettings visualSettings)
        {
            _tileScale = tileScale;
            _visualSettings = visualSettings;

            if (_poolRoot == null)
            {
                var root = new GameObject("TilePool");
                root.transform.SetParent(transform, false);
                _poolRoot = root.transform;
            }

            while (_pool.Count < _initialCapacity)
            {
                _pool.Enqueue(CreateNewTileView());
            }
        }

        public TileView Acquire(Transform activeParent)
        {
            if (_pool.Count == 0)
            {
                _pool.Enqueue(CreateNewTileView());
            }

            var view = _pool.Dequeue();
            view.gameObject.SetActive(true);
            view.transform.SetParent(activeParent, false);
            view.Configure(_tileScale, _visualSettings);
            return view;
        }

        public void Release(TileView view)
        {
            if (view == null)
            {
                return;
            }

            view.SetHighlight(false);
            view.gameObject.SetActive(false);
            view.transform.SetParent(_poolRoot, false);
            _pool.Enqueue(view);
        }

        private TileView CreateNewTileView()
        {
            var tileObject = new GameObject("TileView");
            tileObject.transform.SetParent(_poolRoot, false);

            var view = tileObject.AddComponent<TileView>();
            view.Configure(_tileScale, _visualSettings);
            tileObject.SetActive(false);
            return view;
        }
    }
}
