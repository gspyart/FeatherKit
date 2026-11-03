using UnityEditor;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>Что выделяет клик инструмента «Live Builder»: коробку целиком или одну её грань, как режимы объекта и граней в ProBuilder.</summary>
    public enum BoxModelSelectionMode
    {
        Parts,
        Faces
    }


    /// <summary>Выбранный режим выделения. Свой у каждого человека, хранится в настройках редактора.</summary>
    public static class BoxModelSelectionModeSetting
    {
        private const string PreferenceKey = "FeatherKit.LiveBuilder.SelectionMode";


        // Ушли из режима граней — выделенные грани сбрасываются, чтобы не висели невидимыми.
        public static BoxModelSelectionMode Current
        {
            get => (BoxModelSelectionMode)EditorPrefs.GetInt(PreferenceKey, (int)BoxModelSelectionMode.Parts);
            set
            {
                EditorPrefs.SetInt(PreferenceKey, (int)value);

                if (value != BoxModelSelectionMode.Faces)
                    BoxModelFaceSelection.Clear();

                SceneView.RepaintAll();
            }
        }

        public static bool IsSelectingFaces => Current == BoxModelSelectionMode.Faces;


        public static string DisplayName(BoxModelSelectionMode mode)
        {
            return mode == BoxModelSelectionMode.Faces ? "Грани" : "Коробки";
        }
    }
}
