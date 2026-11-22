using FeatherKit.Attributes;
using UnityEngine;

namespace FeatherKit.LiveBuilder
{
    /// <summary>
    /// Этаж здания в иерархии (<see cref="SplineBuilding"/>): «Storey N» на высоте пола этажа или «Roof» на верху крыши — дочерний объект здания.
    /// - держит части здания своего этажа и всё, что поставлено на этаж: оно едет с этажом без сверки
    /// - помнит, под какие высоту этажа и перекрытие подогнано содержимое
    /// Создаёт, ставит на место и называет Live Builder.
    /// </summary>
    [FDescription("Этаж здания: части здания этого этажа и всё, что на него поставлено, — едет вместе с этажом. " +
                  "Ставит на место и называет Live Builder; клади сюда интерьер этажа.")]
    [AddComponentMenu("")]
    [DisallowMultipleComponent]
    public class SplineBuildingStorey : MonoBehaviour
    {
        [Tooltip("Номер этажа в здании, с нуля. У крыши не важен.")]
        [SerializeField] private int storey;

        [Tooltip("Это крыша: стоит на её верху, по этажам не считается.")]
        [SerializeField] private bool isRoof;

        // Под какие высоту этажа и толщину перекрытия здания подогнано содержимое — так видно, что поменялось само здание. Пишет редактор.
        [SerializeField, HideInInspector] private float syncedStoreyHeight;
        [SerializeField, HideInInspector] private float syncedSlabThickness;

        // Этажа у здания больше нет — этаж с содержимым выключен; вернётся — включится сам.
        [SerializeField, HideInInspector] private bool isHiddenByMissingStorey;


        public int Storey
        {
            get => storey;
            set => storey = value;
        }

        public bool IsRoof
        {
            get => isRoof;
            set => isRoof = value;
        }

        public float SyncedStoreyHeight
        {
            get => syncedStoreyHeight;
            set => syncedStoreyHeight = value;
        }

        public float SyncedSlabThickness
        {
            get => syncedSlabThickness;
            set => syncedSlabThickness = value;
        }

        public bool IsHiddenByMissingStorey
        {
            get => isHiddenByMissingStorey;
            set => isHiddenByMissingStorey = value;
        }

        // Здание этажа — его прямой родитель; этаж вынесли из здания — null.
        public SplineBuilding Building => transform.parent != null ? transform.parent.GetComponent<SplineBuilding>() : null;
    }
}
