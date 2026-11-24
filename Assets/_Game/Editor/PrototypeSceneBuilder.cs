using System.Collections.Generic;
using System.Linq;
using FreshCatch.Core;
using FreshCatch.Input;
using FreshCatch.View;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace FreshCatch.EditorTools
{
    /// <summary>Builds (or rebuilds) the greybox prototype scene. Menu: FreshCatch/Build Prototype Scene.</summary>
    public static class PrototypeSceneBuilder
    {
        public const string ScenePath = "Assets/_Game/Scenes/Prototype.unity";
        private const string MaterialPath = "Assets/_Game/Materials/Greybox.mat";

        [MenuItem("FreshCatch/Build Prototype Scene")]
        public static void Build()
        {
            var material = GetOrCreateMaterial();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Camera: orthographic, angled; GameManager refits it to the screen at runtime.
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 8.5f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.62f, 0.80f, 0.90f);
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 100f;
            camGo.transform.rotation = Quaternion.Euler(52f, 0f, 0f);
            camGo.transform.position = new Vector3(0f, 0f, 1.6f) - camGo.transform.forward * 30f;
            camGo.AddComponent<AudioListener>();

            // Warm morning sun.
            var lightGo = new GameObject("Sun");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.96f, 0.88f);
            light.intensity = 1.2f;
            light.shadows = LightShadows.Soft;
            lightGo.transform.rotation = Quaternion.Euler(55f, -30f, 0f);

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.55f, 0.58f, 0.62f);

            var game = new GameObject("Game");
            var board = game.AddComponent<BoardView>();
            var tap = game.AddComponent<TapController>();
            var gm = game.AddComponent<GameManager>();

            SetRef(board, "baseMaterial", material);
            SetRef(tap, "cam", cam);
            SetRef(gm, "boardView", board);
            SetRef(gm, "tapController", tap);
            SetRef(gm, "cam", cam);

            EditorSceneManager.SaveScene(scene, ScenePath);

            var list = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).ToList();
            list.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = list.ToArray();

            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;

            Debug.Log($"FreshCatch: prototype scene built at {ScenePath}");
        }

        private static Material GetOrCreateMaterial()
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (mat != null) return mat;

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            mat = new Material(shader) { name = "Greybox" };
            mat.SetFloat("_Smoothness", 0.25f);
            AssetDatabase.CreateAsset(mat, MaterialPath);
            AssetDatabase.SaveAssets();
            return mat;
        }

        private static void SetRef(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field);
            if (prop == null)
            {
                Debug.LogError($"FreshCatch: field '{field}' not found on {target.GetType().Name}");
                return;
            }
            prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
