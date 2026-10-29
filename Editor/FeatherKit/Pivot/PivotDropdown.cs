using UnityEditor;
using UnityEditor.Toolbars;
using UnityEngine;

namespace FeatherKit.EditorTools
{
    /// <summary>
    /// Кнопка-меню панели «Расстановка»: перенести пивот выделенных в центр их детей,
    /// не двигая детей, или открыть окно «Пивот» с точкой по числам.
    /// </summary>
    [EditorToolbarElement(ElementId, typeof(SceneView))]
    public class PivotDropdown : EditorToolbarDropdown
    {
        public const string ElementId = "FeatherKit/ObjectPlacement/Pivot";

        public PivotDropdown()
        {
            icon = (Texture2D)EditorGUIUtility.IconContent("ToolHandleCenter").image;
            tooltip = "Пивот: перенести точку опоры, не двигая детей";

            clicked += ShowMenu;
            this.EnableOnlyWithSelection();
        }


        private void ShowMenu()
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("В центр детей"), false, () => PivotMover.MoveToChildrenCenter(Selection.transforms, ChildrenCenterPoint.Center));
            menu.AddItem(new GUIContent("В центр низа детей"), false, () => PivotMover.MoveToChildrenCenter(Selection.transforms, ChildrenCenterPoint.BottomCenter));
            menu.AddSeparator("");
            menu.AddItem(new GUIContent("Окно «Пивот»…"), false, PivotWindow.Open);
            menu.DropDown(worldBound);
        }
    }
}
