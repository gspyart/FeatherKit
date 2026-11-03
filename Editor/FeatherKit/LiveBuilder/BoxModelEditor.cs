using FeatherKit.LiveBuilder;
using UnityEditor;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Инспектор объекта из коробок: поля и число коробок. Всё, что делает инструмент, — в окне Live Builder (FeatherKit → Live Builder).
    /// Плюс пункт меню GameObject → FeatherKit → Box Model — то же, что «Новый объект» в окне: объект рисуется мышью.
    /// </summary>
    [CustomEditor(typeof(BoxModel))]
    public class BoxModelEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.LabelField("Коробок", BoxModelRebuilder.CollectParts((BoxModel)target).Count.ToString());
            EditorGUILayout.HelpBox("Рисовать и тянуть коробки — окно FeatherKit → Live Builder, в окне сцены — клавиша B.", MessageType.None);
        }


        [MenuItem("GameObject/FeatherKit/Box Model", false, 10)]
        private static void CreateFromMenu(MenuCommand command)
        {
            BoxModelActions.BeginNewModelDrawing();
        }
    }
}
