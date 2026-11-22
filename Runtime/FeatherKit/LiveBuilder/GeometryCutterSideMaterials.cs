using System;
using UnityEngine;

namespace FeatherKit.LiveBuilder
{
    /// <summary>
    /// Материалы стенок выреза (<see cref="GeometryCutter"/>) по сторонам, в осях выреза: пол, потолок, стены — все или каждая отдельно.
    /// Пусто — общий материал выреза, а нет и его — материал того, что режем. Так же устроены стороны вырезающей коробки.
    /// </summary>
    [Serializable]
    public sealed class GeometryCutterSideMaterials
    {
        [Tooltip("Пол дыры — низ выреза. Пусто — общий материал выреза.")]
        [SerializeField] private Material bottom;

        [Tooltip("Потолок дыры — верх выреза. Пусто — общий материал выреза.")]
        [SerializeField] private Material top;

        [Tooltip("Все четыре стены дыры, если у стены нет своего ниже. Пусто — общий материал выреза.")]
        [SerializeField] private Material sides;

        [Tooltip("Стена дыры по +X выреза. Пусто — материал всех стен.")]
        [SerializeField] private Material right;

        [Tooltip("Стена дыры по −X выреза. Пусто — материал всех стен.")]
        [SerializeField] private Material left;

        [Tooltip("Стена дыры по +Z выреза. Пусто — материал всех стен.")]
        [SerializeField] private Material front;

        [Tooltip("Стена дыры по −Z выреза. Пусто — материал всех стен.")]
        [SerializeField] private Material back;


        public Material Bottom
        {
            get => bottom;
            set => bottom = value;
        }

        public Material Top
        {
            get => top;
            set => top = value;
        }

        public Material Sides
        {
            get => sides;
            set => sides = value;
        }

        public Material Right
        {
            get => right;
            set => right = value;
        }

        public Material Left
        {
            get => left;
            set => left = value;
        }

        public Material Front
        {
            get => front;
            set => front = value;
        }

        public Material Back
        {
            get => back;
            set => back = value;
        }

        public bool HasAny => bottom != null || top != null || sides != null || right != null || left != null || front != null || back != null;


        public void Clear()
        {
            bottom = null;
            top = null;
            sides = null;
            right = null;
            left = null;
            front = null;
            back = null;
        }
    }
}
