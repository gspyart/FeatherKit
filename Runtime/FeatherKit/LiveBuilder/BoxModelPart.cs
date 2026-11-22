using System.Collections.Generic;
using FeatherKit.Attributes;
using UnityEngine;

namespace FeatherKit.LiveBuilder
{
    /// <summary>Что коробка делает с объектом: добавляет объём или (старые данные) вырезает дыру — новый вырез только <see cref="GeometryCutter"/>.</summary>
    public enum BoxModelPartMode
    {
        [InspectorName("Add")] Add,
        [InspectorName("Cut (legacy)")] Cut
    }


    /// <summary>
    /// Одна коробка объекта из коробок (<see cref="BoxModel"/>): только добавляет объём, вырез — <see cref="GeometryCutter"/>.
    /// - опора — угол с наименьшими координатами по осям объекта, размер в метрах; не поворачивается
    /// - материал всей коробки или отдельно низу, верху и бокам; рёбра можно скосить (<see cref="BoxModelEdgeChamfer"/>): пандус, скат крыши
    /// - «вырезающий» режим и «Режет» — только у старых данных: читает их старое правило до обновления и само обновление
    /// - в игре не нужна: объект помечен EditorOnly, остаётся только собранный меш
    /// </summary>
    [FDescription("Коробка объекта из коробок: добавляет объём. Опора — угол с наименьшими координатами, размер — в метрах. " +
                  "Поворачивать нельзя. Вырез — компонент Geometry Cutter. В игру не попадает — остаётся только собранный меш.")]
    [AddComponentMenu("FeatherKit/Live Builder/Box Model Part")]
    [DisallowMultipleComponent]
    [SelectionBase]
    public class BoxModelPart : MonoBehaviour
    {
        // Старые данные: вырезающая коробка и что она режет. Новые коробки только добавляют; читают старое правило и обновление данных.
        [SerializeField, HideInInspector] private BoxModelPartMode mode = BoxModelPartMode.Add;
        [SerializeField, HideInInspector] private LiveBuilderCutTargets cutTargets = LiveBuilderCutTargets.Everything;

        [Tooltip("Размер коробки по осям объекта, м.")]
        [SerializeField] private Vector3 size = new(3f, 3f, 3f);

        [Header("Materials")]
        [Tooltip("Материал граней этой коробки. Пусто — материал объекта.")]
        [SerializeField] private Material material;

        [Tooltip("Материал низа. Пусто — общий материал коробки.")]
        [SerializeField] private Material bottomMaterial;

        [Tooltip("Материал верха. Пусто — общий материал коробки.")]
        [SerializeField] private Material topMaterial;

        [Tooltip("Материал четырёх боков, если у бока нет своего ниже. Пусто — общий материал коробки.")]
        [SerializeField] private Material sideMaterial;

        [Space]
        [Header("Each Side")]
        [Tooltip("Бок справа, по +X объекта. Пусто — материал боков. Перетащил материал на этот бок в окне сцены — он ляжет сюда.")]
        [SerializeField] private Material rightMaterial;

        [Tooltip("Бок слева, по −X объекта. Пусто — материал боков.")]
        [SerializeField] private Material leftMaterial;

        [Tooltip("Бок спереди, по +Z объекта. Пусто — материал боков.")]
        [SerializeField] private Material frontMaterial;

        [Tooltip("Бок сзади, по −Z объекта. Пусто — материал боков.")]
        [SerializeField] private Material backMaterial;

        [Space]
        [Header("Texture Fit")]
        [Tooltip("Сдвиг, масштаб и поворот текстуры на сторонах коробки — те же места, что у материалов: All, низ, верх, бока, каждый бок. " +
                 "Нет строки — как есть. Своя у стороны главнее, чем у боков, а та — чем у всей коробки.")]
        [SerializeField] private LiveBuilderTextureFitList<BoxModelSideSlot> textureFits = new();

        // Скосы рёбер. Правятся ручками на рёбрах в окне сцены и в окне Live Builder, поэтому в инспекторе не видны.
        [SerializeField, HideInInspector] private List<BoxModelEdgeChamfer> chamfers = new();


        public BoxModelPartMode Mode
        {
            get => mode;
            set => mode = value;
        }

        // Старые данные: что вырезающая коробка режет из стен и зданий объекта.
        public LiveBuilderCutTargets CutTargets
        {
            get => cutTargets;
            set => cutTargets = value;
        }

        public Vector3 Size
        {
            get => size;
            set => size = Vector3.Max(value, Vector3.zero);
        }

        public Material Material
        {
            get => material;
            set => material = value;
        }

        public Material BottomMaterial
        {
            get => bottomMaterial;
            set => bottomMaterial = value;
        }

        public Material TopMaterial
        {
            get => topMaterial;
            set => topMaterial = value;
        }

        public Material SideMaterial
        {
            get => sideMaterial;
            set => sideMaterial = value;
        }

        public Material RightMaterial
        {
            get => rightMaterial;
            set => rightMaterial = value;
        }

        public Material LeftMaterial
        {
            get => leftMaterial;
            set => leftMaterial = value;
        }

        public Material FrontMaterial
        {
            get => frontMaterial;
            set => frontMaterial = value;
        }

        public Material BackMaterial
        {
            get => backMaterial;
            set => backMaterial = value;
        }

        public IReadOnlyList<BoxModelEdgeChamfer> Chamfers => chamfers;

        public LiveBuilderTextureFitList<BoxModelSideSlot> TextureFits => textureFits ??= new LiveBuilderTextureFitList<BoxModelSideSlot>();


        // ── Скосы ────────────────────────────────────────────────────────

        // Скос ребра: заменяет прежний на том же ребре; без среза (одна из сторон — ноль) — ребро снова прямое.
        public void SetChamfer(BoxModelEdgeChamfer chamfer)
        {
            RemoveChamfer(chamfer.FirstFace, chamfer.SecondFace);

            if (chamfer.HasCut)
                chamfers.Add(chamfer);
        }


        public void RemoveChamfer(int firstFace, int secondFace)
        {
            chamfers.RemoveAll(chamfer => chamfer.IsOnEdge(firstFace, secondFace));
        }


        public bool TryGetChamfer(int firstFace, int secondFace, out BoxModelEdgeChamfer chamfer)
        {
            foreach (var candidate in chamfers)
                if (candidate.IsOnEdge(firstFace, secondFace))
                {
                    chamfer = candidate;
                    return true;
                }

            chamfer = default;
            return false;
        }
    }
}
