using FeatherKit.Attributes;
using UnityEngine;
using UnityEngine.UI;

namespace FeatherKit.UI
{
    /// <summary>
    /// Подгоняет ячейку сетки под её ширину: в ряд встаёт ровно столько штук, сколько задано
    /// самой сетке, с равными промежутками и без остатка по краям — пока ячейка не упёрлась в предел.
    /// Число колонок и отступы берёт у <see cref="GridLayoutGroup"/> — свои такие же поля
    /// разъехались бы с ними.
    /// </summary>
    [FDescription("Подгоняет размер ячейки сетки под её ширину: в ряд встаёт ровно столько штук, сколько задано сетке.")]
    [RequireComponent(typeof(GridLayoutGroup))]
    [DisallowMultipleComponent]
    [ExecuteAlways]
    public class GridCellFitter : MonoBehaviour
    {
        [Tooltip("Высота ячейки относительно ширины. 1 — квадрат; 1.2 — выше, чем шире")]
        [Min(0.01f)]
        [SerializeField] private float heightToWidth = 1f;

        [Tooltip("Самая широкая ячейка: на широком экране сетка дальше не растёт, а встаёт, " +
                 "как велит выравнивание сетки. 0 — без предела")]
        [Min(0f)]
        [SerializeField] private float maxCellWidth;

        private GridLayoutGroup grid;
        private RectTransform area;


        // ── Подгонка ──────────────────────────────────────────────────────

        /// <summary>Пересчитать размер ячейки по нынешней ширине.</summary>
        public void Fit()
        {
            Cache();

            if (grid == null || area == null)
                return;

            var columns = Mathf.Max(1, grid.constraintCount);
            var widthForCells = area.rect.width - grid.padding.horizontal - grid.spacing.x * (columns - 1);

            if (widthForCells <= 0f)
                return;

            var width = maxCellWidth > 0f
                ? Mathf.Min(widthForCells / columns, maxCellWidth)
                : widthForCells / columns;
            var cell = new Vector2(width, width * heightToWidth);

            // Только на смену: присваивание метит раскладку грязной, и молчаливое повторение
            // каждого кадра гоняло бы пересборку впустую.
            if (grid.cellSize != cell)
                grid.cellSize = cell;
        }

        private void Cache()
        {
            if (grid == null)
                grid = GetComponent<GridLayoutGroup>();

            if (area == null)
                area = transform as RectTransform;
        }

        // Колонки задаются сетке, а не нам: раскладка по другому правилу оставила бы ряд
        // неровным, и заметить это можно было бы только глазами.
        private void WarnIfNotColumns()
        {
            Cache();

            if (grid != null && grid.constraint != GridLayoutGroup.Constraint.FixedColumnCount)
                Debug.LogError($"GridCellFitter: у сетки «{name}» ограничение не по числу колонок — ряд не сойдётся.", this);
        }


        // ── Unity ─────────────────────────────────────────────────────────

        private void OnEnable()
        {
            WarnIfNotColumns();
            Fit();
        }

        // Ширина меняется от поворота экрана и от чужой раскладки — пересчитываем по факту,
        // а не каждый кадр.
        private void OnRectTransformDimensionsChange()
        {
            Fit();
        }

        private void OnValidate()
        {
            Fit();
        }
    }
}
