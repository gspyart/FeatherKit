using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Где «мир» для текстуры постройки и насколько её оси сдвинуты от него — для выравнивания World (<see cref="LiveBuilderTextureAlignment"/>).
    /// - мир — сцена, а внутри префаба — его корень: экземпляры префаба в разных местах делят один меш
    /// - сдвиг по оси постройки — проекция её точки опоры в мире на эту ось; Object — сдвигов нет
    /// </summary>
    public readonly struct LiveBuilderTextureSpace
    {
        private readonly Matrix4x4 constructionToRoot;


        private LiveBuilderTextureSpace(Matrix4x4 constructionToRoot)
        {
            this.constructionToRoot = constructionToRoot;
            IsWorld = true;
        }


        // Object — false: развёртка по осям постройки, как до этапа 5.
        public bool IsWorld { get; }


        // ── Пространство ─────────────────────────────────────────────────

        // Постройка с её выравниванием; меш которой в осях construction.
        public static LiveBuilderTextureSpace Of(Transform construction, LiveBuilderTextureAlignment alignment)
        {
            if (alignment != LiveBuilderTextureAlignment.World || construction == null)
                return default;

            var root = RootOf(construction);
            var toRoot = root != null ? root.worldToLocalMatrix * construction.localToWorldMatrix : construction.localToWorldMatrix;
            return new LiveBuilderTextureSpace(toRoot);
        }


        // Чей меш у owner: часть здания — здания, стена, колонна, объект из коробок. Нет постройки — Object.
        public static LiveBuilderTextureSpace OfMeshOwner(Transform owner)
        {
            return Of(owner, AlignmentOf(owner));
        }


        // Выравнивание постройки, которой принадлежит меш owner.
        public static LiveBuilderTextureAlignment AlignmentOf(Transform owner)
        {
            if (owner == null)
                return LiveBuilderTextureAlignment.Object;

            if (owner.TryGetComponent<BoxModel>(out var model))
                return model.TextureAlignment;

            if (owner.TryGetComponent<SplineWall>(out var wall))
                return wall.TextureAlignment;

            if (owner.TryGetComponent<LiveBuilderColumn>(out var column))
                return column.TextureAlignment;

            var building = owner.GetComponentInParent<SplineBuilding>(true);
            return building != null ? building.TextureAlignment : LiveBuilderTextureAlignment.Object;
        }


        // Корень файла префаба, открытого или загруженного префаба, самый внешний экземпляр префаба в сцене; просто в сцене — null (мир).
        // Внутри префаба — его корень, а не вложенного: так ключ тот же, что у экземпляра этого префаба в сцене.
        public static Transform RootOf(Transform construction)
        {
            var gameObject = construction.gameObject;
            var stage = PrefabStageUtility.GetPrefabStage(gameObject);

            if (stage != null)
                return stage.prefabContentsRoot.transform;

            if (EditorUtility.IsPersistent(gameObject) || EditorSceneManager.IsPreviewScene(gameObject.scene))
                return construction.root;

            return PrefabUtility.IsPartOfPrefabInstance(gameObject) ? PrefabUtility.GetOutermostPrefabInstanceRoot(gameObject).transform : null;
        }


        // ── Сдвиги ───────────────────────────────────────────────────────

        // Сдвиг развёртки по направлению постройки: где её точка опоры в мире вдоль этого направления, м. Нулевое направление — 0.
        public float ShiftAlong(Vector3 localDirection)
        {
            if (!IsWorld || localDirection == Vector3.zero)
                return 0f;

            var direction = constructionToRoot.MultiplyVector(localDirection).normalized;
            return Vector3.Dot(constructionToRoot.GetColumn(3), direction);
        }


        // То же по оси постройки: 0 — X, 1 — Y, 2 — Z.
        public float ShiftAlongAxis(int axis)
        {
            var direction = Vector3.zero;
            direction[axis] = 1f;
            return ShiftAlong(direction);
        }
    }
}
