using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Готовые виды окна сцены — «Всё», «Комнаты» (срез на 3 м над полом этажа), «Стены вниз» (на 1 м): какой сейчас и включить.
    /// Общие у панели над сценой (<see cref="LiveBuilderSceneOverlay"/>) и зоны «Общее» окна (<see cref="LiveBuilderGlobalZone"/>).
    /// Сам срез — <see cref="LiveBuilderStoreys"/>. Игру не меняет.
    /// </summary>
    public static class LiveBuilderViewPresets
    {
        // Своя высота среза (панель «Уровень») — ViewCustom: не горит ни одна кнопка, даже «Всё», ведь срез включён.
        public const int ViewCustom = -1;
        public const int ViewAll = 0;
        public const int ViewRooms = 1;
        public const int ViewWallsDown = 2;

        // Срез «Комнаты» и «Стены вниз» — на сколько выше пола этажа, м.
        private const float RoomsSectionHeight = 3f;
        private const float WallsDownSectionHeight = 1f;


        // Какой из видов сейчас: Всё, Комнаты, Стены вниз или своя высота среза.
        public static int Current()
        {
            if (!LiveBuilderStoreys.IsSectionEnabled)
                return ViewAll;

            var height = LiveBuilderStoreys.SectionHeightAboveFloor;

            if (Mathf.Approximately(height, RoomsSectionHeight))
                return ViewRooms;

            return Mathf.Approximately(height, WallsDownSectionHeight) ? ViewWallsDown : ViewCustom;
        }


        public static void Apply(int preset)
        {
            if (preset == ViewRooms || preset == ViewWallsDown)
                LiveBuilderStoreys.SectionHeightAboveFloor = preset == ViewRooms ? RoomsSectionHeight : WallsDownSectionHeight;

            LiveBuilderStoreys.IsSectionEnabled = preset != ViewAll;
        }
    }
}
