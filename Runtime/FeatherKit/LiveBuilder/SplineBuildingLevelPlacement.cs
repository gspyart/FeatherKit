using System;
using UnityEngine;

namespace FeatherKit.LiveBuilder
{
    /// <summary>
    /// Как здание (<see cref="SplineBuilding"/>) стоит на этажах уровня (<see cref="LiveBuilderLevelStoreys"/>): на каком этаже, берёт ли высоту этажа уровня
    /// и по каким числам уровня сверено. Инициализаторы — «как раньше»: свободное здание со своей высотой, у старых данных ничего не меняется.
    /// </summary>
    [Serializable]
    public class SplineBuildingLevelPlacement
    {
        [Tooltip("Здание стоит на этаже уровня: поменяли этажи уровня — встаёт низом на пол своего этажа. Выключено — свободное, стоит, где поставили.")]
        [SerializeField] private bool isOnLevelStorey;

        [Tooltip("На каком этаже уровня стоит низ здания, с нуля: 0 — первый, −1 — подвал.")]
        [SerializeField] private int levelStorey;

        [Tooltip("Высота этажа — как у этажей уровня: поменяли её в разделе Storeys окна Live Builder — здание перестроилось. Выключено — своя.")]
        [SerializeField] private bool usesLevelStoreyHeight;

        // Числа этажей уровня при прошлой сверке: поменялись они — двигается здание; поменялось здание при тех же — это правка руками.
        [SerializeField, HideInInspector] private float syncedLevelStoreyHeight;
        [SerializeField, HideInInspector] private float syncedGroundLevel;


        public bool IsOnLevelStorey
        {
            get => isOnLevelStorey;
            set => isOnLevelStorey = value;
        }

        public int LevelStorey
        {
            get => levelStorey;
            set => levelStorey = value;
        }

        public bool UsesLevelStoreyHeight
        {
            get => usesLevelStoreyHeight;
            set => usesLevelStoreyHeight = value;
        }

        public float SyncedLevelStoreyHeight
        {
            get => syncedLevelStoreyHeight;
            set => syncedLevelStoreyHeight = value;
        }

        public float SyncedGroundLevel
        {
            get => syncedGroundLevel;
            set => syncedGroundLevel = value;
        }

        // Следит ли здание за уровнем хоть как-то: свободное со своей высотой сверять не с чем.
        public bool FollowsLevel => isOnLevelStorey || usesLevelStoreyHeight;
    }
}
