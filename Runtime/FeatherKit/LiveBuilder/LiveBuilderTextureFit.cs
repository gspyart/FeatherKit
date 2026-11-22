using System;
using UnityEngine;

namespace FeatherKit.LiveBuilder
{
    /// <summary>Текстура постройки привязана к миру (рисунок идёт через стыки соседних построек) или едет вместе с объектом.</summary>
    public enum LiveBuilderTextureAlignment
    {
        [InspectorName("Object")] Object = 0,
        [InspectorName("World")] World = 1
    }


    /// <summary>
    /// Подгонка текстуры на одном месте материала: сторона коробки, наружные стены здания, пол комнаты.
    /// - сдвиг в метрах по осям текстуры, масштаб, поворот на четверти оборота
    /// - всё по умолчанию — текстура ложится как без подгонки
    /// </summary>
    [Serializable]
    public sealed class LiveBuilderTextureFit
    {
        [Tooltip("Сдвиг рисунка по U и V, м: 1,5 — на полпанели 3 × 3 м.")]
        [SerializeField] private Vector2 offsetMeters;

        [Tooltip("Масштаб рисунка по U и V: 2 — плитка вдвое больше. −1 по U — рисунок зеркально.")]
        [SerializeField] private Vector2 scale = Vector2.one;

        [Tooltip("Поворот рисунка: 0 — как есть, 1 — 90°, 2 — 180°, 3 — 270°.")]
        [Range(0, 3)]
        [SerializeField] private int quarterTurns;


        public LiveBuilderTextureFit()
        {
        }

        public LiveBuilderTextureFit(Vector2 offsetMeters, Vector2 scale, int quarterTurns)
        {
            this.offsetMeters = offsetMeters;
            this.scale = scale;
            QuarterTurns = quarterTurns;
        }


        public Vector2 OffsetMeters
        {
            get => offsetMeters;
            set => offsetMeters = value;
        }

        public Vector2 Scale
        {
            get => scale;
            set => scale = value;
        }

        public int QuarterTurns
        {
            get => quarterTurns;
            set => quarterTurns = ((value % 4) + 4) % 4;
        }

        public bool IsDefault => offsetMeters == Vector2.zero && scale == Vector2.one && quarterTurns == 0;


        public LiveBuilderTextureFit Copy()
        {
            return new LiveBuilderTextureFit(offsetMeters, scale, quarterTurns);
        }
    }
}
