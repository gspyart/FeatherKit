using UnityEditor;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Чьи каркасы коробок видно в окне сцены (<see cref="BoxModelPartHandles.DrawWireframes"/>): у всех коробок выделенного объекта
    /// или только у выделенных — в сложном объекте каркасы закрывают всё. Свой у каждого человека, хранится в настройках редактора.
    /// </summary>
    public static class BoxModelWireframesSetting
    {
        private const string PreferenceKey = "FeatherKit.LiveBuilder.ShowAllWireframes";


        public static bool IsShowingAll
        {
            get => EditorPrefs.GetBool(PreferenceKey, true);
            set
            {
                EditorPrefs.SetBool(PreferenceKey, value);
                SceneView.RepaintAll();
            }
        }
    }
}
