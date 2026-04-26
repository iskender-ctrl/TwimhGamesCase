using System.Collections.Generic;
using TwimhGames.Puzzle.Config;
using UnityEditor;
using UnityEngine;

namespace TwimhGames.Puzzle.Config.Editor
{
    [CustomEditor(typeof(LevelLayoutSO))]
    public sealed class LevelLayoutSOEditor : UnityEditor.Editor
    {
        private const float CellSize = 22f;
        private const float AxisLabelWidth = 24f;

        private static readonly Color PlayableColor = new Color(0.27f, 0.73f, 0.33f);
        private static readonly Color BlockedColor = new Color(0.35f, 0.35f, 0.35f);

        private SerializedProperty _selectedLevelIndexProp;
        private SerializedProperty _levelsProp;

        private GUIStyle _cellButtonStyle;
        private GUIStyle _axisLabelStyle;
        private GUIStyle _cardHeaderStyle;
        private GUIStyle _cardBadgeStyle;

        private void OnEnable()
        {
            _selectedLevelIndexProp = serializedObject.FindProperty("_selectedLevelIndex");
            _levelsProp = serializedObject.FindProperty("_levels");
        }

        public override void OnInspectorGUI()
        {
            EnsureStyles();
            serializedObject.Update();
            EnsureCollectionExists();

            EditorGUILayout.HelpBox(
                "This asset stores multiple level layouts. Click a level card to expand it and edit inline.",
                MessageType.None);

            DrawTopToolbar();
            EditorGUILayout.Space(8f);
            DrawAccordion();

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawTopToolbar()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Levels", EditorStyles.boldLabel);

            if (GUILayout.Button("Add Level", GUILayout.Width(96f)))
            {
                AddNewLevel();
                serializedObject.ApplyModifiedProperties();
                GUIUtility.ExitGUI();
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawAccordion()
        {
            if (_levelsProp.arraySize == 0)
            {
                EditorGUILayout.HelpBox("No levels defined. Click Add Level.", MessageType.Warning);
                return;
            }

            for (var i = 0; i < _levelsProp.arraySize; i++)
            {
                DrawLevelCard(i);
                EditorGUILayout.Space(6f);
            }
        }

        private void DrawLevelCard(int index)
        {
            var levelProp = _levelsProp.GetArrayElementAtIndex(index);
            var nameProp = levelProp.FindPropertyRelative("_name");
            var widthProp = levelProp.FindPropertyRelative("_width");
            var heightProp = levelProp.FindPropertyRelative("_height");
            var rowsProp = levelProp.FindPropertyRelative("_rowsTopToBottom");

            var width = Mathf.Max(1, widthProp.intValue);
            var height = Mathf.Max(1, heightProp.intValue);
            var mask = ReadMask(rowsProp, width, height);
            var playableCount = CountPlayable(mask, width, height);
            var label = $"{index + 1}. {ResolveDisplayName(nameProp.stringValue, index)}";
            var meta = $"{width}x{height}  |  {playableCount} playable";
            var isExpanded = levelProp.isExpanded;
            var isActiveRuntimeLevel = _selectedLevelIndexProp.intValue == index;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            var headerRect = EditorGUILayout.GetControlRect(false, 24f);
            var foldoutRect = headerRect;
            foldoutRect.width -= 86f;

            EditorGUI.BeginChangeCheck();
            isExpanded = EditorGUI.Foldout(foldoutRect, isExpanded, GUIContent.none, true, _cardHeaderStyle);
            if (EditorGUI.EndChangeCheck())
            {
                SetExpandedIndex(isExpanded ? index : -1);
                serializedObject.ApplyModifiedProperties();
                GUIUtility.ExitGUI();
            }

            var textRect = foldoutRect;
            textRect.x += 14f;
            GUI.Label(textRect, $"{label}    {meta}", _cardHeaderStyle);

            var badgeRect = new Rect(headerRect.xMax - 80f, headerRect.y + 2f, 80f, 20f);
            GUI.Label(badgeRect, isActiveRuntimeLevel ? "Runtime" : "", _cardBadgeStyle);

            if (Event.current.type == EventType.MouseDown && headerRect.Contains(Event.current.mousePosition))
            {
                SetExpandedIndex(isExpanded ? -1 : index);
                serializedObject.ApplyModifiedProperties();
                Event.current.Use();
                GUIUtility.ExitGUI();
            }

            if (levelProp.isExpanded)
            {
                EditorGUILayout.Space(4f);
                DrawLevelCardToolbar(index);
                DrawSelectedLevelEditor(levelProp, index);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawLevelCardToolbar(int index)
        {
            EditorGUILayout.BeginHorizontal();

            using (new EditorGUI.DisabledScope(index <= 0))
            {
                if (GUILayout.Button("Move Up"))
                {
                    MoveLevel(index, -1);
                    serializedObject.ApplyModifiedProperties();
                    GUIUtility.ExitGUI();
                }
            }

            using (new EditorGUI.DisabledScope(index >= _levelsProp.arraySize - 1))
            {
                if (GUILayout.Button("Move Down"))
                {
                    MoveLevel(index, 1);
                    serializedObject.ApplyModifiedProperties();
                    GUIUtility.ExitGUI();
                }
            }

            if (GUILayout.Button("Duplicate"))
            {
                DuplicateLevel(index);
                serializedObject.ApplyModifiedProperties();
                GUIUtility.ExitGUI();
            }

            using (new EditorGUI.DisabledScope(_levelsProp.arraySize <= 1))
            {
                if (GUILayout.Button("Remove"))
                {
                    RemoveLevel(index);
                    serializedObject.ApplyModifiedProperties();
                    GUIUtility.ExitGUI();
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawSelectedLevelEditor(SerializedProperty levelProp, int index)
        {
            var nameProp = levelProp.FindPropertyRelative("_name");
            var widthProp = levelProp.FindPropertyRelative("_width");
            var heightProp = levelProp.FindPropertyRelative("_height");
            var rowsProp = levelProp.FindPropertyRelative("_rowsTopToBottom");

            _selectedLevelIndexProp.intValue = index;

            EditorGUILayout.PropertyField(nameProp, new GUIContent("Name"));

            var previousWidth = Mathf.Max(1, widthProp.intValue);
            var previousHeight = Mathf.Max(1, heightProp.intValue);
            var previousMask = ReadMask(rowsProp, previousWidth, previousHeight);

            EditorGUI.BeginChangeCheck();
            var width = Mathf.Max(1, EditorGUILayout.DelayedIntField("Width", previousWidth));
            var height = Mathf.Max(1, EditorGUILayout.DelayedIntField("Height", previousHeight));
            if (EditorGUI.EndChangeCheck())
            {
                widthProp.intValue = width;
                heightProp.intValue = height;
            }

            width = Mathf.Max(1, widthProp.intValue);
            height = Mathf.Max(1, heightProp.intValue);

            var dimensionsChanged = width != previousWidth || height != previousHeight;
            var mask = dimensionsChanged
                ? ResizeMask(previousMask, previousWidth, previousHeight, width, height)
                : ReadMask(rowsProp, width, height);

            var changed = dimensionsChanged;
            changed |= DrawPresetButtons(mask, width, height);
            changed |= DrawGrid(mask, width, height);

            if (changed)
            {
                WriteMask(rowsProp, mask, width, height);
            }

            DrawValidation(mask, width, height);
            DrawRawRowsSection(rowsProp);
        }

        private void EnsureCollectionExists()
        {
            if (_levelsProp.arraySize > 0)
            {
                _selectedLevelIndexProp.intValue = Mathf.Clamp(_selectedLevelIndexProp.intValue, 0, _levelsProp.arraySize - 1);
                return;
            }

            AddNewLevel();
        }

        private void AddNewLevel()
        {
            var newIndex = _levelsProp.arraySize;
            _levelsProp.arraySize++;
            var levelProp = _levelsProp.GetArrayElementAtIndex(newIndex);
            InitializeLevel(levelProp, $"Level {newIndex + 1}", 6, 6, null);
            SetExpandedIndex(newIndex);
        }

        private void DuplicateLevel(int sourceIndex)
        {
            var sourceProp = _levelsProp.GetArrayElementAtIndex(sourceIndex);
            var sourceName = sourceProp.FindPropertyRelative("_name").stringValue;
            var sourceWidth = Mathf.Max(1, sourceProp.FindPropertyRelative("_width").intValue);
            var sourceHeight = Mathf.Max(1, sourceProp.FindPropertyRelative("_height").intValue);
            var sourceRows = sourceProp.FindPropertyRelative("_rowsTopToBottom");
            var sourceMask = ReadMask(sourceRows, sourceWidth, sourceHeight);

            var newIndex = sourceIndex + 1;
            _levelsProp.InsertArrayElementAtIndex(newIndex);
            var newLevelProp = _levelsProp.GetArrayElementAtIndex(newIndex);
            InitializeLevel(newLevelProp, $"{ResolveDisplayName(sourceName, sourceIndex)} Copy", sourceWidth, sourceHeight, sourceMask);
            SetExpandedIndex(newIndex);
        }

        private void RemoveLevel(int index)
        {
            _levelsProp.DeleteArrayElementAtIndex(index);

            if (_levelsProp.arraySize == 0)
            {
                AddNewLevel();
                return;
            }

            var nextIndex = Mathf.Clamp(index - 1, 0, _levelsProp.arraySize - 1);
            SetExpandedIndex(nextIndex);
        }

        private void MoveLevel(int from, int direction)
        {
            var to = Mathf.Clamp(from + direction, 0, _levelsProp.arraySize - 1);
            if (from == to)
            {
                return;
            }

            _levelsProp.MoveArrayElement(from, to);
            SetExpandedIndex(to);
        }

        private void SetExpandedIndex(int expandedIndex)
        {
            if (_levelsProp.arraySize == 0)
            {
                _selectedLevelIndexProp.intValue = 0;
                return;
            }

            if (expandedIndex < 0)
            {
                for (var i = 0; i < _levelsProp.arraySize; i++)
                {
                    _levelsProp.GetArrayElementAtIndex(i).isExpanded = false;
                }

                _selectedLevelIndexProp.intValue = Mathf.Clamp(_selectedLevelIndexProp.intValue, 0, _levelsProp.arraySize - 1);
                return;
            }

            expandedIndex = Mathf.Clamp(expandedIndex, 0, _levelsProp.arraySize - 1);
            for (var i = 0; i < _levelsProp.arraySize; i++)
            {
                _levelsProp.GetArrayElementAtIndex(i).isExpanded = i == expandedIndex;
            }

            _selectedLevelIndexProp.intValue = expandedIndex;
        }

        private void InitializeLevel(SerializedProperty levelProp, string name, int width, int height, bool[,] sourceMask)
        {
            levelProp.FindPropertyRelative("_name").stringValue = string.IsNullOrWhiteSpace(name) ? "Level" : name.Trim();
            levelProp.FindPropertyRelative("_width").intValue = Mathf.Max(1, width);
            levelProp.FindPropertyRelative("_height").intValue = Mathf.Max(1, height);

            var rowsProp = levelProp.FindPropertyRelative("_rowsTopToBottom");
            if (sourceMask == null)
            {
                sourceMask = new bool[Mathf.Max(1, width), Mathf.Max(1, height)];
                SetAll(sourceMask, Mathf.Max(1, width), Mathf.Max(1, height), true);
            }

            WriteMask(rowsProp, sourceMask, Mathf.Max(1, width), Mathf.Max(1, height));
        }

        private bool DrawPresetButtons(bool[,] mask, int width, int height)
        {
            var changed = false;

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Quick Presets", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Fill"))
            {
                SetAll(mask, width, height, true);
                changed = true;
            }

            if (GUILayout.Button("Clear"))
            {
                SetAll(mask, width, height, false);
                changed = true;
            }

            if (GUILayout.Button("Invert"))
            {
                Invert(mask, width, height);
                changed = true;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Border"))
            {
                ApplyBorder(mask, width, height);
                changed = true;
            }

            if (GUILayout.Button("Cross"))
            {
                ApplyCross(mask, width, height);
                changed = true;
            }

            if (GUILayout.Button("Diamond"))
            {
                ApplyDiamond(mask, width, height);
                changed = true;
            }
            EditorGUILayout.EndHorizontal();

            return changed;
        }

        private bool DrawGrid(bool[,] mask, int width, int height)
        {
            var changed = false;

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Layout Grid", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(AxisLabelWidth);
            for (var x = 0; x < width; x++)
            {
                GUILayout.Label(x.ToString(), _axisLabelStyle, GUILayout.Width(CellSize));
            }
            EditorGUILayout.EndHorizontal();

            for (var y = height - 1; y >= 0; y--)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label(y.ToString(), _axisLabelStyle, GUILayout.Width(AxisLabelWidth));

                for (var x = 0; x < width; x++)
                {
                    var playable = mask[x, y];
                    var oldColor = GUI.backgroundColor;
                    GUI.backgroundColor = playable ? PlayableColor : BlockedColor;

                    if (GUILayout.Button(GUIContent.none, _cellButtonStyle, GUILayout.Width(CellSize), GUILayout.Height(CellSize)))
                    {
                        mask[x, y] = !playable;
                        changed = true;
                    }

                    GUI.backgroundColor = oldColor;
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(AxisLabelWidth);
            GUILayout.Label("Green: playable", EditorStyles.miniLabel);
            GUILayout.Space(8f);
            GUILayout.Label("Gray: blocked", EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();

            return changed;
        }

        private void DrawValidation(bool[,] mask, int width, int height)
        {
            var playableCount = CountPlayable(mask, width, height);
            EditorGUILayout.Space(8f);
            EditorGUILayout.HelpBox($"Playable Cells: {playableCount} / {width * height}", MessageType.Info);

            if (playableCount == 0)
            {
                EditorGUILayout.HelpBox(
                    "No playable cell selected. Runtime currently falls back to full-playable board in this case.",
                    MessageType.Warning);
            }

            var groupCount = CountConnectedGroups(mask, width, height);
            if (groupCount > 1)
            {
                EditorGUILayout.HelpBox(
                    $"Layout has {groupCount} disconnected playable groups. This is valid, but design intent should be checked.",
                    MessageType.Warning);
            }
        }

        private static void DrawRawRowsSection(SerializedProperty rowsProp)
        {
            rowsProp.isExpanded = EditorGUILayout.Foldout(rowsProp.isExpanded, "Raw Rows (Advanced)");
            if (!rowsProp.isExpanded)
            {
                return;
            }

            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(rowsProp, true);
            EditorGUI.indentLevel--;
        }

        private static bool[,] ReadMask(SerializedProperty rowsProp, int width, int height)
        {
            var mask = new bool[width, height];

            for (var y = 0; y < height; y++)
            {
                var rowIndexFromTop = (height - 1) - y;
                var row = rowIndexFromTop >= 0 && rowIndexFromTop < rowsProp.arraySize
                    ? rowsProp.GetArrayElementAtIndex(rowIndexFromTop).stringValue
                    : string.Empty;

                for (var x = 0; x < width; x++)
                {
                    var playable = !string.IsNullOrEmpty(row) &&
                                   x < row.Length &&
                                   LevelLayoutSO.IsPlayableToken(row[x]);
                    mask[x, y] = playable;
                }
            }

            return mask;
        }

        private static void WriteMask(SerializedProperty rowsProp, bool[,] mask, int width, int height)
        {
            rowsProp.arraySize = height;

            for (var rowTop = 0; rowTop < height; rowTop++)
            {
                var y = (height - 1) - rowTop;
                var chars = new char[width];

                for (var x = 0; x < width; x++)
                {
                    chars[x] = LevelLayoutSO.ToToken(mask[x, y]);
                }

                rowsProp.GetArrayElementAtIndex(rowTop).stringValue = new string(chars);
            }
        }

        private static bool[,] ResizeMask(bool[,] source, int sourceWidth, int sourceHeight, int targetWidth, int targetHeight)
        {
            var target = new bool[targetWidth, targetHeight];
            var copyWidth = Mathf.Min(sourceWidth, targetWidth);
            var copyHeight = Mathf.Min(sourceHeight, targetHeight);

            for (var y = 0; y < copyHeight; y++)
            {
                for (var x = 0; x < copyWidth; x++)
                {
                    target[x, y] = source[x, y];
                }
            }

            return target;
        }

        private static int CountPlayable(bool[,] mask, int width, int height)
        {
            var count = 0;
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    if (mask[x, y])
                    {
                        count++;
                    }
                }
            }

            return count;
        }

        private static int CountConnectedGroups(bool[,] mask, int width, int height)
        {
            var visited = new bool[width, height];
            var groups = 0;
            var queue = new Queue<Vector2Int>();
            var directions = new[]
            {
                new Vector2Int(1, 0),
                new Vector2Int(-1, 0),
                new Vector2Int(0, 1),
                new Vector2Int(0, -1)
            };

            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    if (!mask[x, y] || visited[x, y])
                    {
                        continue;
                    }

                    groups++;
                    visited[x, y] = true;
                    queue.Enqueue(new Vector2Int(x, y));

                    while (queue.Count > 0)
                    {
                        var cell = queue.Dequeue();
                        for (var i = 0; i < directions.Length; i++)
                        {
                            var next = cell + directions[i];
                            if (next.x < 0 || next.x >= width || next.y < 0 || next.y >= height)
                            {
                                continue;
                            }

                            if (!mask[next.x, next.y] || visited[next.x, next.y])
                            {
                                continue;
                            }

                            visited[next.x, next.y] = true;
                            queue.Enqueue(next);
                        }
                    }
                }
            }

            return groups;
        }

        private static void SetAll(bool[,] mask, int width, int height, bool value)
        {
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    mask[x, y] = value;
                }
            }
        }

        private static void Invert(bool[,] mask, int width, int height)
        {
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    mask[x, y] = !mask[x, y];
                }
            }
        }

        private static void ApplyBorder(bool[,] mask, int width, int height)
        {
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var isBorder = x == 0 || x == width - 1 || y == 0 || y == height - 1;
                    mask[x, y] = isBorder;
                }
            }
        }

        private static void ApplyCross(bool[,] mask, int width, int height)
        {
            var centerX = width / 2;
            var centerY = height / 2;

            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    mask[x, y] = x == centerX || y == centerY;
                }
            }
        }

        private static void ApplyDiamond(bool[,] mask, int width, int height)
        {
            var centerX = (width - 1) * 0.5f;
            var centerY = (height - 1) * 0.5f;
            var maxDistance = Mathf.Min(centerX, centerY);

            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var distance = Mathf.Abs(x - centerX) + Mathf.Abs(y - centerY);
                    mask[x, y] = distance <= maxDistance + 0.001f;
                }
            }
        }

        private static string ResolveDisplayName(string rawName, int index)
        {
            return string.IsNullOrWhiteSpace(rawName) ? $"Level {index + 1}" : rawName.Trim();
        }

        private void EnsureStyles()
        {
            if (_cellButtonStyle == null)
            {
                _cellButtonStyle = new GUIStyle(GUI.skin.button)
                {
                    margin = new RectOffset(1, 1, 1, 1),
                    padding = new RectOffset(0, 0, 0, 0)
                };
            }

            if (_axisLabelStyle == null)
            {
                _axisLabelStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    alignment = TextAnchor.MiddleCenter
                };
            }

            if (_cardHeaderStyle == null)
            {
                _cardHeaderStyle = new GUIStyle(EditorStyles.foldout)
                {
                    fontStyle = FontStyle.Bold,
                    fixedHeight = 22f
                };
            }

            if (_cardBadgeStyle == null)
            {
                _cardBadgeStyle = new GUIStyle(EditorStyles.miniBoldLabel)
                {
                    alignment = TextAnchor.MiddleRight,
                    normal = { textColor = new Color(0.2f, 0.55f, 0.9f) }
                };
            }
        }
    }
}
