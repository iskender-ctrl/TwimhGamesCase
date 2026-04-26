using System;
using System.Collections.Generic;
using System.IO;
using TwimhGames.Puzzle.Tiles;
using UnityEditor;
using UnityEngine;

namespace TwimhGames.Puzzle.Config.Editor
{
    public static class PuzzleCaseSetupMenu
    {
        [MenuItem("TwimhGames/Puzzle/Create Default Case Assets")]
        public static void CreateDefaultCaseAssets()
        {
            if (!TrySelectTargetFolder(out var targetFolder))
            {
                return;
            }

            var levelLayout = FindOrCreateAsset<LevelLayoutSO>(targetFolder);
            var tileCatalog = FindOrCreateAsset<TileCatalogSO>(targetFolder);
            var boardConfig = FindOrCreateAsset<BoardConfigSO>(targetFolder);

            boardConfig.AssignLevelLayout(levelLayout);
            boardConfig.SetSpecialIcons(
                FindSpriteByToken(SpecialTileKind.Color.ToString()),
                FindSpriteByToken(SpecialTileKind.Bomb.ToString()),
                FindSpriteByToken(SpecialTileKind.Lightning.ToString()),
                Color.white);
            EditorUtility.SetDirty(boardConfig);

            tileCatalog.SetDefinitions(BuildTileDefinitions());
            EditorUtility.SetDirty(tileCatalog);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = boardConfig;
            EditorGUIUtility.PingObject(boardConfig);

            Debug.Log($"Puzzle case assets ready in folder: {targetFolder}");
        }

        private static bool TrySelectTargetFolder(out string targetFolder)
        {
            targetFolder = null;

            var selectedAbsolutePath = EditorUtility.SaveFolderPanel(
                "Select target folder",
                Application.dataPath,
                "ScriptableObjects");

            if (string.IsNullOrEmpty(selectedAbsolutePath))
            {
                return false;
            }

            selectedAbsolutePath = selectedAbsolutePath.Replace('\\', '/');
            var relativePath = FileUtil.GetProjectRelativePath(selectedAbsolutePath);

            if (string.IsNullOrEmpty(relativePath) || !relativePath.StartsWith("Assets", StringComparison.Ordinal))
            {
                EditorUtility.DisplayDialog(
                    "Invalid Folder",
                    "Please select a folder inside this project's Assets directory.",
                    "OK");
                return false;
            }

            AssetDatabase.Refresh();
            if (!AssetDatabase.IsValidFolder(relativePath))
            {
                EditorUtility.DisplayDialog(
                    "Invalid Folder",
                    "Selected folder could not be resolved as a valid project folder.",
                    "OK");
                return false;
            }

            targetFolder = relativePath;
            return true;
        }

        private static IEnumerable<TileDefinition> BuildTileDefinitions()
        {
            var kinds = (TileKind[])Enum.GetValues(typeof(TileKind));
            var definitions = new List<TileDefinition>(kinds.Length);

            for (var i = 0; i < kinds.Length; i++)
            {
                var kind = kinds[i];
                definitions.Add(new TileDefinition(kind, FindSpriteByToken(kind.ToString()), Color.white));
            }

            return definitions;
        }

        private static Sprite FindSpriteByToken(string token)
        {
            var guids = AssetDatabase.FindAssets($"t:Sprite {token}");
            if (guids == null || guids.Length == 0)
            {
                return null;
            }

            var normalizedToken = token.Trim().ToLowerInvariant();
            Sprite fallback = null;

            for (var i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite == null)
                {
                    continue;
                }

                fallback ??= sprite;

                var fileName = Path.GetFileNameWithoutExtension(path);
                if (string.Equals(fileName, token, StringComparison.OrdinalIgnoreCase))
                {
                    return sprite;
                }

                if (!string.IsNullOrEmpty(fileName) && fileName.ToLowerInvariant().Contains(normalizedToken))
                {
                    return sprite;
                }
            }

            return fallback;
        }

        private static T FindOrCreateAsset<T>(string folderPath) where T : ScriptableObject
        {
            var guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { folderPath });
            if (guids != null && guids.Length > 0)
            {
                var existingPath = AssetDatabase.GUIDToAssetPath(guids[0]);
                var existingAsset = AssetDatabase.LoadAssetAtPath<T>(existingPath);
                if (existingAsset != null)
                {
                    return existingAsset;
                }
            }

            var createdAsset = ScriptableObject.CreateInstance<T>();
            var assetName = $"{typeof(T).Name}.asset";
            var assetPath = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(folderPath, assetName).Replace('\\', '/'));
            AssetDatabase.CreateAsset(createdAsset, assetPath);
            return createdAsset;
        }
    }
}
