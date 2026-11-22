using FeatherKit.Attributes;
using UnityEngine;

namespace FeatherKit.LiveBuilder
{
    /// <summary>
    /// Старая привязка к этажам здания (<see cref="SplineBuilding"/>): объект брал высоты здания, копии «И этажи выше» и «На крыше» — по сверке.
    /// Теперь объект лежит в этаже здания (<see cref="SplineBuildingStorey"/>), копии — <see cref="LiveBuilderArray"/>.
    /// Читают только старое правило (версия 0) и обновление данных; удалить, когда все данные обновлены.
    /// </summary>
    [FDescription("Привязка к этажам здания: высоты — от здания, «И этажи выше» — копии на всех этажах выше, сами следят за числом этажей. «На крыше» — едет за крышей.")]
    [AddComponentMenu("")]
    [DisallowMultipleComponent]
    public class SplineBuildingStoreyLink : MonoBehaviour
    {
        [Tooltip("Здание, к этажам которого привязан объект.")]
        [SerializeField] private SplineBuilding building;

        [Tooltip("Повторять на всех этажах здания выше, до самого верха. Выключено — только на своём этаже.")]
        [SerializeField] private bool isToBuildingTop;

        [Tooltip("Повторять на этажах до этого включительно (с нуля). Работает, если не «И этажи выше».")]
        [SerializeField] private bool hasLastStorey;

        [SerializeField] private int lastStorey;

        [Tooltip("Последний этаж — без копии. Новой лестнице ставится сам: с верхнего этажа обычно идти некуда. Выход на крышу — выключи.")]
        [SerializeField] private bool skipsTopStorey;

        [Tooltip("Стоит на крыше здания: добавил этажей или поменял их высоту — поднимется вместе с крышей. По этажам не повторяется.")]
        [SerializeField] private bool isOnRoof;

        // Где объект относительно здания: этаж и высота над его полом. Пишет редактор: здание поменяло этажи — объект встаёт на свой этаж заново.
        [SerializeField, HideInInspector] private int storey;
        [SerializeField, HideInInspector] private float heightAboveFloor;

        // Какими были высота этажа и пол первого этажа здания при прошлой сверке — так видно, что поменялось само здание.
        [SerializeField, HideInInspector] private float syncedStoreyHeight;
        [SerializeField, HideInInspector] private float syncedFirstFloor;

        // Объект выключен, потому что его этажа у здания больше нет; этаж вернётся — включится сам.
        [SerializeField, HideInInspector] private bool isHiddenByMissingStorey;


        public SplineBuilding Building
        {
            get => building;
            set => building = value;
        }

        public bool IsToBuildingTop
        {
            get => isToBuildingTop;
            set => isToBuildingTop = value;
        }

        // Повтор до своего этажа — «с … по …».
        public bool HasLastStorey
        {
            get => hasLastStorey;
            set => hasLastStorey = value;
        }

        // Последний этаж повтора, с нуля.
        public int LastStorey
        {
            get => lastStorey;
            set => lastStorey = Mathf.Max(0, value);
        }

        public bool SkipsTopStorey
        {
            get => skipsTopStorey;
            set => skipsTopStorey = value;
        }

        // На крыше: этаж — всегда «над верхним», пол этого этажа — верх крыши.
        public bool IsOnRoof
        {
            get => isOnRoof;
            set => isOnRoof = value;
        }

        public int Storey
        {
            get => storey;
            set => storey = value;
        }

        public float HeightAboveFloor
        {
            get => heightAboveFloor;
            set => heightAboveFloor = value;
        }

        public float SyncedStoreyHeight
        {
            get => syncedStoreyHeight;
            set => syncedStoreyHeight = value;
        }

        public float SyncedFirstFloor
        {
            get => syncedFirstFloor;
            set => syncedFirstFloor = value;
        }

        public bool IsHiddenByMissingStorey
        {
            get => isHiddenByMissingStorey;
            set => isHiddenByMissingStorey = value;
        }

        public bool IsValid => building != null;


        // Сколько копий начиная с этажа storey: «И этажи выше» — до последнего этажа (у лестницы — до предпоследнего),
        // «по этаж» — до него включительно, но не выше здания; на крыше и иначе — одна.
        public int CopiesFrom(int storey)
        {
            if (building == null || isOnRoof)
                return 1;

            var available = Mathf.Max(1, building.StoreyCount - storey - (skipsTopStorey ? 1 : 0));

            if (isToBuildingTop)
                return available;

            return hasLastStorey ? Mathf.Clamp(lastStorey - storey + 1, 1, available) : 1;
        }
    }
}
