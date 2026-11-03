using FeatherKit.Attributes;
using UnityEngine;

namespace FeatherKit.LiveBuilder
{
    /// <summary>Какая часть здания по контуру: стены этажа, пол этажа (плита под ним) или крыша.</summary>
    public enum SplineBuildingPartKind
    {
        Walls,
        Floor,
        Roof
    }


    /// <summary>
    /// Часть здания по контуру (<see cref="SplineBuilding"/>): стены этажа, пол этажа или крыша — отдельный объект со своим мешем и коллайдером.
    /// Создаёт и пересобирает здание: руками часть не правится, правки затрутся. Прятать в окне сцены — «глазиком» Unity в иерархии.
    /// </summary>
    [FDescription("Часть здания: стены этажа, пол или крыша. Собирается зданием — меняй числа здания, а не часть. Спрятать в окне сцены — «глазик» в иерархии.")]
    [AddComponentMenu("")]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class SplineBuildingPart : MonoBehaviour
    {
        [SerializeField] private SplineBuildingPartKind kind;

        [Tooltip("Номер этажа с нуля; у крыши не важен.")]
        [SerializeField] private int storey;

        // Чем собран меш части: подпись здания и вырезы, что режут эту часть, — поменялись только они, пересобирается только она. Пишет редактор.
        [SerializeField, HideInInspector] private string builtSignature;

        // Габарит части до вырезов, в её осях: по нему ищутся вырезы, которые её режут.
        [SerializeField, HideInInspector] private Bounds uncutBounds;


        public SplineBuildingPartKind Kind
        {
            get => kind;
            set => kind = value;
        }

        public int Storey
        {
            get => storey;
            set => storey = value;
        }

        public string BuiltSignature
        {
            get => builtSignature;
            set => builtSignature = value;
        }

        public Bounds UncutBounds
        {
            get => uncutBounds;
            set => uncutBounds = value;
        }
    }
}
