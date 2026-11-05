using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Поля настроек, общие у нескольких инструментов Live Builder: что выделяет клик, угол или плавно у новой точки, где линия относительно стен, перегородка в здании
    /// (и изменена ли она — для «Ещё»), карточка «В здании». Свои настройки каждый инструмент рисует сам (<see cref="LiveBuilderTool.DrawSettings"/>).
    /// Числа хранятся у каждого человека свои — настройки инструментов, например <see cref="LiveBuilderRoomSettings"/>.
    /// </summary>
    public static class LiveBuilderToolSettingsFields
    {
        // Что выделяет клик — у «Выделения» и «Коробки»: коробку целиком или кусок грани.
        public static void DrawSelectionModeSwitch()
        {
            var selectionMode = (BoxModelSelectionMode)LiveBuilderWindowControls.Switch(
                new GUIContent("Клик выделяет", "Коробки — коробку целиком, тянешь точки на её гранях. " +
                                                "Грани — видимый кусок грани, двойной клик — грань целиком, Shift — несколько; ручка двигает все"),
                (int)BoxModelSelectionModeSetting.Current, ("Коробки", LiveBuilderOptionIcon.Boxes), ("Грани", LiveBuilderOptionIcon.Faces));

            if (selectionMode != BoxModelSelectionModeSetting.Current)
                BoxModelSelectionModeSetting.Current = selectionMode;
        }


        // Угол или плавно — общий для стены, здания и комнаты по контуру.
        public static void DrawKnotModeSwitch()
        {
            LiveBuilderWallSettings.IsSmooth = LiveBuilderWindowControls.Switch(
                new GUIContent("Новая точка", "Угол — прямые отрезки. Плавно — кривая через точки. Shift на клике — наоборот"),
                LiveBuilderWallSettings.IsSmooth, ("Угол", LiveBuilderOptionIcon.Corner), ("Плавно", LiveBuilderOptionIcon.Smooth));
        }


        // Где нарисованная линия относительно стен — одними словами у здания и комнаты: 0 — снаружи стен, 1 — изнутри, 2 — по их оси.
        public static int DrawLineSideSwitch(int lineSide)
        {
            return LiveBuilderWindowControls.Switch(
                new GUIContent("Линия по стенам", "Где нарисованная линия относительно стен. Снаружи — линия это габарит, стены встают внутрь от неё. " +
                                                  "Изнутри — линия это комната изнутри, стены встают наружу. По оси — линия идёт по середине стен"),
                lineSide, "Снаружи", "Изнутри", "По оси");
        }


        // Перегородка внутри здания — у стены и комнаты общая: тип или своя толщина.
        public static void DrawPartitionThickness()
        {
            LiveBuilderWallSettings.PartitionWallType = LiveBuilderWallTypes.Popup(
                new GUIContent("Перегородка в здании", "Тип стен внутри здания по контуру. Высота у них — до потолка его этажа, сама"),
                LiveBuilderWallSettings.PartitionWallType);

            if (LiveBuilderWallSettings.PartitionWallType != null)
                return;

            LiveBuilderWallSettings.PartitionThickness = EditorGUILayout.FloatField(
                new GUIContent("Толщина перегородки, м", "Толщина стен внутри здания по контуру. Высота у них — до потолка его этажа, сама"),
                LiveBuilderWallSettings.PartitionThickness);
        }

        // Перегородка не как по умолчанию: выбран тип или своя толщина другая.
        public static bool IsPartitionChanged()
        {
            return LiveBuilderWallSettings.PartitionWallType != null
                || !Mathf.Approximately(LiveBuilderWallSettings.PartitionThickness, LiveBuilderWallSettings.FactoryPartitionThickness);
        }


        // «В здании» — у инструментов, которые ставят своё в здание (<see cref="LiveBuilderTool.PlacesIntoBuilding"/>): этот этаж или «И этажи выше».
        public static void DrawBuildingStoreysCard()
        {
            using (LiveBuilderWindowControls.Card("В здании"))
                LiveBuilderBuildingContext.IsOnAllStoreys = LiveBuilderWindowControls.Switch(
                    new GUIContent("Этажи", "Наведёшь на здание — поставленное встанет на этот этаж здания, до его потолка, с его толщинами. " +
                                            "И этажи выше — то же на каждом этаже выше: перегородки, лестничная клетка, окна по фасаду. После перезапуска Unity — снова «только этот»"),
                    LiveBuilderBuildingContext.IsOnAllStoreys, ("Только этот", LiveBuilderOptionIcon.ThisStorey), ("И этажи выше", LiveBuilderOptionIcon.AllStoreysAbove));
        }
    }
}
