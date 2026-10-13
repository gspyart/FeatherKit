using UnityEngine;

namespace FeatherKit.Rendering
{
    /// <summary>
    /// Выбирает области тумана (<see cref="FogArea"/>), которые надо рисовать для камеры:
    /// только те, что попали в кадр и не растаяли вдали, и не больше <see cref="MaxAreas"/>
    /// ближайших. Отвечает ТОЛЬКО за выбор; в числа для шейдера их переводит
    /// <see cref="HeightFogShaderInputs"/>.
    ///
    /// Отсев здесь, а не в шейдере, потому что каждая область в шейдере стоит проверки
    /// на каждом шаге каждого пикселя, а здесь — одной проверки коробки на кадр.
    /// </summary>
    internal sealed class HeightFogAreasCollector
    {
        /// <summary>
        /// Сколько областей тумана рисуется в одном кадре. Столько же ждёт шейдер.
        /// </summary>
        public const int MaxAreas = 8;

        private readonly Plane[] frustumPlanes = new Plane[6];
        private readonly float[] selectedDistances = new float[MaxAreas];
        private readonly VisibleFogAreas visible = new VisibleFogAreas();


        // ── Выбор областей ────────────────────────────────────────────────────

        /// <summary>
        /// Видимые камерой области, ближние первыми. Ответ живёт до следующего вызова.
        /// </summary>
        public VisibleFogAreas CollectVisible(Camera camera)
        {
            visible.Count = 0;

            var areas = FogArea.Enabled;

            if (areas.Count == 0)
                return visible;

            GeometryUtility.CalculateFrustumPlanes(camera, frustumPlanes);

            var cameraPosition = camera.transform.position;

            foreach (var area in areas)
            {
                var bounds = area.WorldBounds;
                var distance = Mathf.Sqrt(bounds.SqrDistance(cameraPosition));
                var fade = area.CalculateFade(distance);

                if (fade <= 0f || !GeometryUtility.TestPlanesAABB(frustumPlanes, bounds))
                    continue;

                InsertByDistance(area, fade, distance);
            }

            return visible;
        }

        // Вставка в отсортированный по расстоянию список, лишняя дальняя — вылетает.
        // Областей в кадре единицы, поэтому простая вставка дешевле любой сортировки.
        private void InsertByDistance(FogArea area, float fade, float distance)
        {
            var count = visible.Count;
            var index = count;

            while (index > 0 && selectedDistances[index - 1] > distance)
                index--;

            if (index >= MaxAreas)
                return;

            var last = Mathf.Min(count, MaxAreas - 1);

            for (var i = last; i > index; i--)
            {
                visible.Areas[i] = visible.Areas[i - 1];
                visible.Fades[i] = visible.Fades[i - 1];
                selectedDistances[i] = selectedDistances[i - 1];
            }

            visible.Areas[index] = area;
            visible.Fades[index] = fade;
            selectedDistances[index] = distance;

            visible.Count = Mathf.Min(count + 1, MaxAreas);
        }
    }

    /// <summary>
    /// Области тумана, которые видит камера в этом кадре, и насколько каждая из них
    /// растаяла вдали. Заполняет <see cref="HeightFogAreasCollector"/>.
    /// </summary>
    internal sealed class VisibleFogAreas
    {
        public readonly FogArea[] Areas = new FogArea[HeightFogAreasCollector.MaxAreas];

        /// <summary>
        /// Насколько область видна с расстояния до камеры: единица — целиком.
        /// </summary>
        public readonly float[] Fades = new float[HeightFogAreasCollector.MaxAreas];

        public int Count;
    }
}
