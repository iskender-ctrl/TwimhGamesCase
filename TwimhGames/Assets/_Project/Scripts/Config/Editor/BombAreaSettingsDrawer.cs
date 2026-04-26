using TwimhGames.Puzzle.Config;
using UnityEditor;
using UnityEngine;

namespace TwimhGames.Puzzle.Config.Editor
{
    [CustomPropertyDrawer(typeof(BombAreaSettings))]
    public sealed class BombAreaSettingsDrawer : PropertyDrawer
    {
        private const float VerticalSpacing = 2f;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            var modeProperty = property.FindPropertyRelative("Mode");
            var customWidthProperty = property.FindPropertyRelative("CustomWidth");
            var customHeightProperty = property.FindPropertyRelative("CustomHeight");

            var lineRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            EditorGUI.PropertyField(lineRect, modeProperty, label);

            if ((BombAreaMode)modeProperty.enumValueIndex == BombAreaMode.Custom)
            {
                lineRect.y += EditorGUIUtility.singleLineHeight + VerticalSpacing;
                var halfWidth = (lineRect.width - 6f) * 0.5f;
                var widthRect = new Rect(lineRect.x, lineRect.y, halfWidth, lineRect.height);
                var heightRect = new Rect(lineRect.x + halfWidth + 6f, lineRect.y, halfWidth, lineRect.height);

                EditorGUI.PropertyField(widthRect, customWidthProperty, new GUIContent("Custom Width"));
                EditorGUI.PropertyField(heightRect, customHeightProperty, new GUIContent("Custom Height"));
            }

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var modeProperty = property.FindPropertyRelative("Mode");
            var lineCount = (BombAreaMode)modeProperty.enumValueIndex == BombAreaMode.Custom ? 2 : 1;
            return (lineCount * EditorGUIUtility.singleLineHeight) + ((lineCount - 1) * VerticalSpacing);
        }
    }
}
