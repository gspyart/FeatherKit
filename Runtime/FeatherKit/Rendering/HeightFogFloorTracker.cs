using System.Collections.Generic;
using UnityEngine;

namespace FeatherKit.Rendering
{
    /// <summary>
    /// Решает, на какой высоте лежит пол тумана для данной камеры. Обычно это просто
    /// число из <see cref="HeightFogVolume"/>; с галкой «следовать за камерой» — высота
    /// камеры плюс это число, причём туман догоняет камеру с задержкой, а не висит на ней
    /// жёстко: иначе слой дёргался бы на каждом прыжке и приседе.
    ///
    /// Догоняемая высота своя у каждой камеры — окно сцены и игра не тянут туман друг у друга.
    /// </summary>
    internal sealed class HeightFogFloorTracker
    {
        private readonly Dictionary<Camera, FollowedHeight> followedHeights = new Dictionary<Camera, FollowedHeight>();


        // ── Высота пола ───────────────────────────────────────────────────────

        public float GetFloorHeight(Camera camera, HeightFogVolume settings)
        {
            if (!settings.followCamera.value)
                return settings.floorHeight.value;

            return FollowCameraHeight(camera, settings.followDelay.value) + settings.floorHeight.value;
        }

        // Высота, которая плавно догоняет камеру. Окно сцены и игра вне плей-мода догоняют
        // сразу: там камеру двигают руками, и ждать тумана при настройке незачем.
        private float FollowCameraHeight(Camera camera, float delay)
        {
            var target = camera.transform.position.y;
            var frame = Time.frameCount;

            var isFollowingSmoothly = delay > 0f && Application.isPlaying && camera.cameraType == CameraType.Game;

            if (!followedHeights.TryGetValue(camera, out var followed))
            {
                ForgetDestroyedCameras();
                followed = new FollowedHeight { height = target, frame = frame };
            }
            else if (!isFollowingSmoothly)
            {
                followed.height = target;
            }
            else if (followed.frame != frame)
            {
                // Экспонента, а не шаг на постоянную долю: так догон не зависит от частоты
                // кадров. За время задержки туман проходит примерно две трети пути.
                followed.height = Mathf.Lerp(followed.height, target, 1f - Mathf.Exp(-Time.deltaTime / delay));
            }

            // Кадр запоминаем, потому что фича может спросить про одну камеру дважды за кадр,
            // и догон не должен шагнуть дважды.
            followed.frame = frame;
            followedHeights[camera] = followed;

            return followed.height;
        }

        // Уничтоженные камеры остаются ключами словаря, пока их не убрать. Чистим, когда
        // приходит новая камера: случается это редко, а словарь не растёт.
        private void ForgetDestroyedCameras()
        {
            var destroyed = new List<Camera>();

            foreach (var camera in followedHeights.Keys)
            {
                if (camera == null)
                    destroyed.Add(camera);
            }

            foreach (var camera in destroyed)
                followedHeights.Remove(camera);
        }


        // Догоняемая высота одной камеры и кадр, в котором её последний раз сдвигали.
        private struct FollowedHeight
        {
            public float height;
            public int frame;
        }
    }
}
