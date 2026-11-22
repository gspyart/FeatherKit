using System;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Кадр окна сцены для замера: камера сцены (или своя, если её нет) смотрит на весь уровень сверху-сбоку и рисует в текстуру 1920 × 1080.
    /// Время кадра — вместе с видеокартой: на Mac у Metal нет таймеров проходов, поэтому после отрисовки ждём чтения одного пикселя.
    /// </summary>
    public sealed class LiveBuilderSceneRender : IDisposable
    {
        private const int Width = 1920;
        private const int Height = 1080;
        private const int WarmUpFrames = 3;

        // Откуда камера смотрит на середину уровня.
        private static readonly Vector3 ViewFrom = new(-1f, 0.8f, -1f);

        private readonly Camera camera;
        private readonly GameObject ownCameraObject;
        private readonly RenderTexture texture;


        public LiveBuilderSceneRender(Transform level)
        {
            camera = Camera.main;

            if (camera == null)
            {
                ownCameraObject = new GameObject("Live Builder Measurement Camera") { hideFlags = HideFlags.HideAndDontSave };
                camera = ownCameraObject.AddComponent<Camera>();
            }

            texture = new RenderTexture(Width, Height, 24) { name = "Live Builder Measurement" };
            camera.targetTexture = texture;
            Frame(BoundsOf(level));

            // Первые кадры компилируют шейдеры — в замер не идут.
            for (var frame = 0; frame < WarmUpFrames; frame++)
                RenderMilliseconds();
        }


        // ── Кадр ─────────────────────────────────────────────────────────

        public double RenderMilliseconds()
        {
            var start = Stopwatch.GetTimestamp();
            camera.Render();
            AsyncGPUReadback.Request(texture, 0, 0, 1, 0, 1, 0, 1, null).WaitForCompletion();
            return (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency;
        }


        // ── Камера ───────────────────────────────────────────────────────

        private void Frame(Bounds bounds)
        {
            var radius = bounds.extents.magnitude;
            var distance = radius / Mathf.Sin(camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            camera.transform.position = bounds.center + ViewFrom.normalized * distance;
            camera.transform.LookAt(bounds.center);
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = distance + radius * 2f;
        }

        private static Bounds BoundsOf(Transform level)
        {
            var renderers = level.GetComponentsInChildren<Renderer>();
            var bounds = renderers.Length > 0 ? renderers[0].bounds : new Bounds(level.position, Vector3.one);

            foreach (var renderer in renderers)
                bounds.Encapsulate(renderer.bounds);

            return bounds;
        }


        // IDisposable
        public void Dispose()
        {
            camera.targetTexture = null;
            texture.Release();
            Object.DestroyImmediate(texture);

            if (ownCameraObject != null)
                Object.DestroyImmediate(ownCameraObject);
        }
    }
}
