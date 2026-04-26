using System.Reflection;
using TwimhGames.Puzzle.Config;
using UnityEngine;

namespace TwimhGames.Puzzle.Core
{
    public static class PuzzleCameraConfigurator
    {
        public static Camera ResolveOrCreate(Camera assignedCamera)
        {
            if (assignedCamera != null)
            {
                return assignedCamera;
            }

            var camera = Camera.main;
            if (camera != null)
            {
                return camera;
            }

            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            return cameraObject.AddComponent<Camera>();
        }

        public static void Configure(Camera camera, BoardConfigSO boardConfig)
        {
            if (camera == null || boardConfig == null)
            {
                return;
            }

            camera.orthographic = true;
            camera.transform.position = new Vector3(0f, 0f, -10f);
            camera.allowHDR = false;
            camera.allowMSAA = false;
            camera.useOcclusionCulling = false;

            var halfWidth = (boardConfig.BoardWorldWidth * 0.5f) + boardConfig.Camera.FramingPadding;
            var halfHeight = (boardConfig.BoardWorldHeight * 0.5f) + boardConfig.Camera.FramingPadding;
            var targetSize = Mathf.Max(halfHeight, halfWidth / Mathf.Max(0.01f, camera.aspect));

            camera.orthographicSize = targetSize;
            camera.backgroundColor = boardConfig.Camera.BackgroundColor;

            TryApplyUniversalCameraOptimizations(camera);
        }

        private static void TryApplyUniversalCameraOptimizations(Camera camera)
        {
            var universalAdditionalCameraData = camera.GetComponent("UniversalAdditionalCameraData");
            if (universalAdditionalCameraData == null)
            {
                return;
            }

            SetBoolMember(universalAdditionalCameraData, "renderPostProcessing", false);
            SetBoolMember(universalAdditionalCameraData, "renderShadows", false);
            SetEnumMember(universalAdditionalCameraData, "requiresDepthOption", "Off");
            SetEnumMember(universalAdditionalCameraData, "requiresColorOption", "Off");
        }

        private static void SetBoolMember(object target, string memberName, bool value)
        {
            var targetType = target.GetType();

            var property = targetType.GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property != null && property.CanWrite && property.PropertyType == typeof(bool))
            {
                property.SetValue(target, value);
                return;
            }

            var field = targetType.GetField(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field != null && field.FieldType == typeof(bool))
            {
                field.SetValue(target, value);
            }
        }

        private static void SetEnumMember(object target, string memberName, string enumValueName)
        {
            var targetType = target.GetType();

            var property = targetType.GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property != null && property.CanWrite && property.PropertyType.IsEnum)
            {
                var enumValue = System.Enum.Parse(property.PropertyType, enumValueName);
                property.SetValue(target, enumValue);
                return;
            }

            var field = targetType.GetField(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field != null && field.FieldType.IsEnum)
            {
                var enumValue = System.Enum.Parse(field.FieldType, enumValueName);
                field.SetValue(target, enumValue);
            }
        }
    }
}