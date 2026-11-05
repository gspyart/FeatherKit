using System.Collections.Generic;
using FeatherKit.Attributes;
using UnityEngine;

namespace FeatherKit.LiveBuilder
{
    /// <summary>Что коробка делает с объектом: добавляет объём или вырезает дыру.</summary>
    public enum BoxModelPartMode
    {
        [InspectorName("Добавить")] Add,
        [InspectorName("Вырезать")] Cut
    }


    /// <summary>
    /// Одна коробка объекта из коробок (<see cref="BoxModel"/>): добавляет объём или вырезает дыру.
    /// Опора — угол с наименьшими координатами по всем осям объекта, размер — в метрах. Коробки не поворачиваются: всегда по осям объекта.
    /// Материал можно задать всей коробке или отдельно низу, верху и бокам — у вырезающей это пол, потолок и стены дыры.
    /// У добавляющей рёбра можно скосить (<see cref="BoxModelEdgeChamfer"/>): пандус, склон, скат крыши.
    /// Вырезающая режет стены по линии и здания своего объекта, которые задевает, — порядок в списке объектов не важен; что именно — стены, перекрытия или всё — <see cref="LiveBuilderCutTargets"/>.
    /// В игре не нужна: объект коробки помечен EditorOnly и в сборку не попадает.
    /// </summary>
    [FDescription("Коробка объекта из коробок: добавляет объём или вырезает дыру. Опора — угол с наименьшими координатами, размер — в метрах. " +
                  "Поворачивать нельзя. В игру не попадает — остаётся только собранный меш.")]
    [AddComponentMenu("FeatherKit/Live Builder/Box Model Part")]
    [DisallowMultipleComponent]
    [SelectionBase]
    public class BoxModelPart : MonoBehaviour
    {
        [Tooltip("Добавить объём или вырезать дыру.")]
        [SerializeField] private BoxModelPartMode mode = BoxModelPartMode.Add;

        [Tooltip("Только у вырезающей: что она режет из стен по линии и зданий в этом объекте. Свои коробки объекта режет всегда. " +
                 "Только перекрытия — дыра под лестницу или шахту: стены и перегородки не трогает. Только стены — проём, пол и потолок целы.")]
        [SerializeField] private LiveBuilderCutTargets cutTargets = LiveBuilderCutTargets.Everything;

        [Tooltip("Размер коробки по осям объекта, м.")]
        [SerializeField] private Vector3 size = new(3f, 3f, 3f);

        [Header("Материалы")]
        [Tooltip("Материал граней этой коробки. Пусто — материал объекта. У вырезающей — материал стенок дыры.")]
        [SerializeField] private Material material;

        [Tooltip("Материал низа. У вырезающей коробки — пол комнаты. Пусто — общий материал коробки.")]
        [SerializeField] private Material bottomMaterial;

        [Tooltip("Материал верха. У вырезающей коробки — потолок комнаты. Пусто — общий материал коробки.")]
        [SerializeField] private Material topMaterial;

        [Tooltip("Материал четырёх боков, если у бока нет своего ниже. У вырезающей коробки — стены комнаты. Пусто — общий материал коробки.")]
        [SerializeField] private Material sideMaterial;

        [Space]
        [Header("Бока по отдельности")]
        [Tooltip("Бок справа, по +X объекта. Пусто — материал боков. Перетащил материал на этот бок в окне сцены — он ляжет сюда.")]
        [SerializeField] private Material rightMaterial;

        [Tooltip("Бок слева, по −X объекта. Пусто — материал боков.")]
        [SerializeField] private Material leftMaterial;

        [Tooltip("Бок спереди, по +Z объекта. Пусто — материал боков.")]
        [SerializeField] private Material frontMaterial;

        [Tooltip("Бок сзади, по −Z объекта. Пусто — материал боков.")]
        [SerializeField] private Material backMaterial;

        // Скосы рёбер. Правятся ручками на рёбрах в окне сцены и в окне Live Builder, поэтому в инспекторе не видны.
        [SerializeField, HideInInspector] private List<BoxModelEdgeChamfer> chamfers = new();


        public BoxModelPartMode Mode
        {
            get => mode;
            set => mode = value;
        }

        // Что вырезающая коробка режет из стен и зданий объекта; у добавляющей не важно.
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
