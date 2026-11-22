using FeatherKit.LiveBuilder;
using UnityEditor;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Инспектор объекта из коробок: поля и число коробок; он же — секция Construction панели. Рисуют и тянут коробки — инструменты Live Builder в окне сцены.
    /// Плюс пункт меню GameObject → FeatherKit → Box Model — то же, что New у «Коробки»: объект рисуется мышью.
    /// </summary>
    [CustomEditor(typeof(BoxModel))]
    [CanEditMultipleObjects]
    public class BoxModelEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            if (targets.Length == 1)
                EditorGUILayout.LabelField("Boxes", BoxModelRebuilder.CollectParts((BoxModel)target).Count.ToString());
        }


        [MenuItem("GameObject/FeatherKit/Box Model", false, 10)]
        private static void CreateFromMenu(MenuCommand command)
        {
            BoxModelActions.BeginNewModelDrawing();
        }
    }
}
