using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Настройки Live Builder на проект — то, что у каждой игры своё: материал и место новых объектов, папки файлов,
    /// типы стен набора, разрез этажа в шейдере, готовые проёмы и таблица прыжков для линейки.
    /// Общие на проект: лежат в ProjectSettings/FeatherKit и попадают в git; страница — <see cref="LiveBuilderProjectSettingsPage"/>.
    /// Личное (сетка, этаж, выбранный инструмент) — не здесь, а в настройках редактора у каждого человека.
    /// </summary>
    [FilePath("ProjectSettings/FeatherKit/LiveBuilder.asset", FilePathAttribute.Location.ProjectFolder)]
    public class LiveBuilderProjectSettings : ScriptableSingleton<LiveBuilderProjectSettings>
    {
        // Свой серый материал набора — когда в проекте материал не задан.
        private const string KitMaterialPath = "Packages/com.gspy.featherkit/Runtime/FeatherKit/LiveBuilder/Materials/LiveBuilder Gray.mat";

        [Header("Новые объекты")]
        [Tooltip("Материал граней у новых объектов из коробок, стен и зданий. Пусто — серый материал FeatherKit.")]
        [SerializeField] private Material defaultMaterial;

        [Tooltip("Группа на сцене для новых объектов, путь от корня: «Level/Geometry». Группы на сцене нет или поле пустое — объект ложится в корень.")]
        [SerializeField] private string newObjectsGroup = "";

        [Space]
        [Header("Файлы мешей")]
        [Tooltip("Папка файлов мешей, пока сцена не сохранена. У сохранённой сцены меши лежат рядом с ней, в папке «<Сцена>_BoxModels».")]
        [SerializeField] private string unsavedSceneMeshFolder = "Assets/Live Builder Meshes";

        [Tooltip("При сохранении сцены файлы мешей в её папке «<Сцена>_BoxModels», на которые не ссылается ни одна сцена и ни один префаб, " +
                 "сами уходят в корзину системы. Выключено — только кнопкой «Убрать лишние меши…».")]
        [SerializeField] private bool isUnusedMeshCleanupOnSave = true;

        [Space]
        [Header("Типы стен")]
        [Tooltip("Папка, куда кладутся новые типы стен.")]
        [SerializeField] private string wallTypesFolder = "Assets/Live Builder Wall Types";

        [Tooltip("Материал наружной стены в стартовом наборе типов. Пусто — материал новых объектов.")]
        [SerializeField] private Material exteriorWallMaterial;

        [Tooltip("Материал перегородок в стартовом наборе типов. Пусто — материал новых объектов.")]
        [SerializeField] private Material partitionMaterial;

        [Space]
        [Header("Разрез этажа")]
        [Tooltip("Ключевое слово шейдера: включается в окне сцены, когда включён разрез. Шейдер, который его не знает, просто не режется.")]
        [SerializeField] private string sectionKeyword = "LIVE_BUILDER_SECTION_VIEW";

        [Tooltip("Глобальное число шейдера — высота плоскости разреза в мире, м. Выше неё шейдер прячет геометрию.")]
        [SerializeField] private string sectionHeightProperty = "_LiveBuilderSectionHeight";

        [Space]
        [Header("Проёмы")]
        [Tooltip("Готовые размеры для инструмента «Проём»: клик по стене ставит выбранный. Низ над полом: 0 — дверь, 1 — окно.")]
        [SerializeField] private List<LiveBuilderOpeningSize> openingPresets = new()
        {
            new LiveBuilderOpeningSize("Дверь 0,9 × 2,1", 0.9f, 2.1f, 0f),
            new LiveBuilderOpeningSize("Двойная дверь 1,8 × 2,1", 1.8f, 2.1f, 0f),
            new LiveBuilderOpeningSize("Проход 1,2 × 2,4", 1.2f, 2.4f, 0f),
            new LiveBuilderOpeningSize("Окно 1,2 × 1,4", 1.2f, 1.4f, 0.9f)
        };

        [Space]
        [Header("Линейка: прыжки")]
        [Tooltip("Линейка подсказывает, допрыгнет ли игрок: легко, на пределе, нельзя. Выключено — только расстояния.")]
        [SerializeField] private bool isJumpCheckEnabled;

        [Tooltip("Щель с разбега: насколько ниже цель и какая щель легко, а какая на пределе, м. Между строками — по прямой.")]
        [SerializeField] private List<LiveBuilderJumpGap> jumpGaps = new()
        {
            new LiveBuilderJumpGap(0f, 3f, 3.8f),
            new LiveBuilderJumpGap(1f, 3.5f, 4.5f),
            new LiveBuilderJumpGap(4f, 4.5f, 6f)
        };

        [Tooltip("Выше этого над местом прыжка край уже не достать даже в вис, м.")]
        [Min(0f)]
        [SerializeField] private float maxHangReach = 3.7f;

        [Tooltip("Падение до этой высоты — спокойно, выше — жёсткое приземление, м.")]
        [Min(0f)]
        [SerializeField] private float safeFall = 4f;

        [Tooltip("Падение с этой высоты — смерть, м.")]
        [Min(0f)]
        [SerializeField] private float deadlyFall = 12f;


        // ── Новые объекты ────────────────────────────────────────────────

        public Material DefaultMaterial => defaultMaterial != null ? defaultMaterial : AssetDatabase.LoadAssetAtPath<Material>(KitMaterialPath);

        public string NewObjectsGroup => newObjectsGroup.Trim('/', ' ');

        public string UnsavedSceneMeshFolder => unsavedSceneMeshFolder.TrimEnd('/', ' ');

        public bool IsUnusedMeshCleanupOnSave => isUnusedMeshCleanupOnSave;


        // ── Типы стен ────────────────────────────────────────────────────

        public string WallTypesFolder => wallTypesFolder.TrimEnd('/', ' ');

        public Material ExteriorWallMaterial => exteriorWallMaterial != null ? exteriorWallMaterial : DefaultMaterial;

        public Material PartitionMaterial => partitionMaterial != null ? partitionMaterial : DefaultMaterial;


        // ── Разрез, проёмы, прыжки ───────────────────────────────────────

        public string SectionKeyword => sectionKeyword;

        public string SectionHeightProperty => sectionHeightProperty;

        public IReadOnlyList<LiveBuilderOpeningSize> OpeningPresets => openingPresets;

        public bool IsJumpCheckEnabled => isJumpCheckEnabled && jumpGaps.Count > 0;

        public IReadOnlyList<LiveBuilderJumpGap> JumpGaps => jumpGaps;

        public float MaxHangReach => maxHangReach;

        public float SafeFall => safeFall;

        public float DeadlyFall => deadlyFall;


        // ── Сохранение ───────────────────────────────────────────────────

        // Записать в ProjectSettings — зовёт страница настроек после правки.
        public void SaveChanges()
        {
            Save(true);
        }
    }
}
