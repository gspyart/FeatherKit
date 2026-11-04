using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>Настройки инструмента «Комната»: форма, куда встают стены, толщина стен и перекрытий, материалы пола, стен и потолка.</summary>
    public static class LiveBuilderRoomSettings
    {
        // Сменилась форма — начатое рисование отменяется, как при смене инструмента.
        public static event System.Action ShapeChanged;


        public static LiveBuilderRoomShape Shape
        {
            get => LiveBuilderPreferences.GetEnum("Room.Shape", LiveBuilderRoomShape.Rectangle);
            set
            {
                if (Shape == value)
                    return;

                LiveBuilderPreferences.SetInt("Room.Shape", (int)value);
                ShapeChanged?.Invoke();
                SceneView.RepaintAll();
            }
        }

        // Куда встают стены от нарисованного. Прямоугольнику в доме из коробок всё равно: он вырезается, своих стен у него нет.
        public static LiveBuilderRoomWallSide WallSide
        {
            get => LiveBuilderPreferences.GetEnum("Room.WallSide", LiveBuilderRoomWallSide.Outward);
            set
            {
                if (WallSide == value)
                    return;

                LiveBuilderPreferences.SetInt("Room.WallSide", (int)value);
                SceneView.RepaintAll();
            }
        }

        // Толщина стен вокруг комнаты в пустоте, м. Внутри дома стены — то, что осталось от его объёма.
        public static float WallThickness
        {
            get => LiveBuilderPreferences.GetFloat("Room.WallThickness", 0.3f);
            set => LiveBuilderPreferences.SetFloat("Room.WallThickness", Mathf.Max(0.05f, value));
        }

        // Толщина пола и потолка, м. Высота комнаты — этаж минус перекрытие: 4 − 0,2 = 3,8.
        public static float SlabThickness
        {
            get => LiveBuilderPreferences.GetFloat("Room.SlabThickness", 0.2f);
            set => LiveBuilderPreferences.SetFloat("Room.SlabThickness", Mathf.Clamp(value, 0.05f, 1f));
        }

        public static Material FloorMaterial
        {
            get => LiveBuilderPreferences.GetMaterial("Room.FloorMaterial");
            set => LiveBuilderPreferences.SetMaterial("Room.FloorMaterial", value);
        }

        public static Material WallMaterial
        {
            get => LiveBuilderPreferences.GetMaterial("Room.WallMaterial");
            set => LiveBuilderPreferences.SetMaterial("Room.WallMaterial", value);
        }

        public static Material CeilingMaterial
        {
            get => LiveBuilderPreferences.GetMaterial("Room.CeilingMaterial");
            set => LiveBuilderPreferences.SetMaterial("Room.CeilingMaterial", value);
        }

        // Высота комнаты от пола до потолка, м.
        public static float ClearHeight => Mathf.Max(0.5f, LiveBuilderStoreys.StoreyHeight - SlabThickness);
    }
}
