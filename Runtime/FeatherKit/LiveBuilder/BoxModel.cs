using FeatherKit.Attributes;
using UnityEngine;

namespace FeatherKit.LiveBuilder
{
    /// <summary>
    /// Объект из коробок — дом, стена, комната: один меш и один коллайдер, собранные из дочерних коробок <see cref="BoxModelPart"/>.
    /// Коробки добавляют объём или вырезают дыры: сначала складываются все добавляющие, потом вычитаются все вырезы; у старых данных (версия 0) — по списку.
    /// «Фаска рёбер» срезает или скругляет выпуклые рёбра готовой оболочки.
    /// Меш собирает редактор и хранит файлом рядом со сценой; в игре компонент — только данные, ничего не считает.
    /// </summary>
    [FDescription("Объект из коробок: дочерние коробки добавляют объём или вырезают дыры, редактор сам собирает из них один меш. " +
                  "Сначала складываются все коробки, потом вычитаются все вырезы — порядок в списке не важен. " +
                  "Колонну внутри вырезанного зала клади во вложенный объект: вырезы снаружи его не режут. " +
                  "Рисовать и тянуть коробки — инструмент Box в окне сцены (Live Builder — клавиша B).")]
    [AddComponentMenu("FeatherKit/Live Builder/Box Model")]
    [DisallowMultipleComponent]
    [SelectionBase]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class BoxModel : MonoBehaviour
    {
        // Версии данных: 0 — вырезы по порядку в списке, что ниже — главнее; 2 — сначала все добавления, потом все вырезы.
        public const int LegacyDataVersion = 0;
        public const int CurrentDataVersion = 2;

        [Tooltip("Материал граней, если у коробки свой не задан.")]
        [SerializeField] private Material defaultMaterial;

        [Tooltip("Собирать коллайдер по мешу — по объекту можно ходить.")]
        [SerializeField] private bool hasCollider = true;

        [Space]
        [Header("Edge Bevel")]
        [Tooltip("Срез всех выпуклых рёбер наружной оболочки, м: угол дома, край плиты, верх стены. 0 — рёбра острые. " +
                 "0,05 — заметная фаска на блокинге. Не больше половины самой тонкой грани рядом.")]
        [Min(0f)]
        [SerializeField] private float bevelSize;

        [Tooltip("Из скольких кусков срез ребра. 1 — плоская фаска под 45°, 4 и больше — скругление.")]
        [Range(1, 8)]
        [SerializeField] private int bevelSegments = 1;

        [Space]
        [Header("Texture")]
        [Tooltip("World — текстура по миру: рисунок идёт через стыки соседних объектов и этажей, сдвинул объект — рисунок остаётся на месте. " +
                 "Object — текстура едет вместе с объектом. Старые объекты — Object, новые — World.")]
        [SerializeField] private LiveBuilderTextureAlignment textureAlignment = LiveBuilderTextureAlignment.Object;

        // Чем собран текущий меш: изменились коробки — пересборка. Пишет редактор.
        [SerializeField, HideInInspector] private string builtSignature;

        // Инициализатор — 0: у старого объекта поля в файле нет, и Unity берёт его отсюда. Новым ставят фабрика и Reset.
        [SerializeField, HideInInspector] private int dataVersion = LegacyDataVersion;


        public Material DefaultMaterial
        {
            get => defaultMaterial;
            set => defaultMaterial = value;
        }

        public bool HasCollider => hasCollider;

        public float BevelSize
        {
            get => bevelSize;
            set => bevelSize = Mathf.Max(0f, value);
        }

        public int BevelSegments
        {
            get => bevelSegments;
            set => bevelSegments = Mathf.Clamp(value, 1, 8);
        }

        public LiveBuilderTextureAlignment TextureAlignment
        {
            get => textureAlignment;
            set => textureAlignment = value;
        }

        public string BuiltSignature
        {
            get => builtSignature;
            set => builtSignature = value;
        }

        public int DataVersion
        {
            get => dataVersion;
            set => dataVersion = value;
        }


        private void Reset()
        {
            dataVersion = CurrentDataVersion;
            textureAlignment = LiveBuilderTextureAlignment.World;
        }
    }
}
