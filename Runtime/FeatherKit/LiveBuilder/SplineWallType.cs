using FeatherKit.Attributes;
using UnityEngine;

namespace FeatherKit.LiveBuilder
{
    /// <summary>
    /// Тип стены — общий набор для многих стен, как типы стен в Revit: толщина и материал.
    /// Стена по линии (<see cref="SplineWall"/>) или стены здания (<see cref="SplineBuilding"/>) с типом берут числа из него:
    /// поменял тип — перестроились все стены этого типа. Имя файла — имя типа в списках Live Builder.
    /// </summary>
    [FDescription("Тип стены: толщина и материал, общие для всех стен с этим типом. Поменял здесь — перестроились все.")]
    [CreateAssetMenu(menuName = "FeatherKit/Live Builder/Wall Type", fileName = "Wall Type")]
    public class SplineWallType : ScriptableObject
    {
        [Tooltip("Толщина стены, м. Наружная — 0,3, перегородка — 0,15.")]
        [Min(0.02f)]
        [SerializeField] private float thickness = 0.3f;

        [Tooltip("Материал стены. Пусто — серый материал набора.")]
        [SerializeField] private Material material;


        public float Thickness
        {
            get => thickness;
            set => thickness = Mathf.Max(0.02f, value);
        }

        public Material Material
        {
            get => material;
            set => material = value;
        }
    }
}
