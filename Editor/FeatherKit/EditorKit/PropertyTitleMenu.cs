using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools
{
    /// <summary>
    /// Меню поля по правому клику на заголовке, который FeatherKit рисует сам: раскрытое поле,
    /// блок, свёртка. Unity своё меню («Copy», «Paste», «Revert») показывает только у полей,
    /// нарисованных ею самой, — здесь его приходится открывать вручную.
    ///
    /// Открывает родное меню Unity. Его функция закрытая и после обновления Unity может пропасть —
    /// тогда своё запасное меню с копированием через <see cref="FieldValueClipboard"/>.
    /// </summary>
    public static class PropertyTitleMenu
    {
        private const string CopyItemName = "Copy";
        private const string PasteItemName = "Paste";

        // Закрытая функция Unity, которая собирает меню поля. Ищем один раз: нашлась или нет — до перезапуска не изменится.
        private static readonly MethodInfo UnityPropertyMenuBuilder = typeof(EditorGUI).GetMethod(
            "FillPropertyContextMenu",
            BindingFlags.Static | BindingFlags.NonPublic,
            null,
            new[] { typeof(SerializedProperty), typeof(SerializedProperty), typeof(GenericMenu) },
            null);


        /// <summary>Открыть меню поля, если по заголовку кликнули правой кнопкой.</summary>
        public static void HandleRightClick(Rect title, SerializedProperty property)
        {
            var current = Event.current;

            if (current.type != EventType.ContextClick || !title.Contains(current.mousePosition))
                return;

            // Копия — потому что поле часто приходит из перебора, который к моменту выбора пункта уйдёт дальше.
            var menu = BuildUnityMenu(property.Copy()) ?? BuildOwnMenu(property.Copy());

            menu.ShowAsContext();
            current.Use();
        }


        private static GenericMenu BuildUnityMenu(SerializedProperty property)
        {
            if (UnityPropertyMenuBuilder == null)
                return null;

            try
            {
                var menu = (GenericMenu)UnityPropertyMenuBuilder.Invoke(null, new object[] { property, null, null });
                return menu != null && menu.GetItemCount() > 0 ? menu : null;
            }
            catch (TargetInvocationException)
            {
                // Функция есть, но её устройство поменялось — тогда обходимся своим меню.
                return null;
            }
        }

        private static GenericMenu BuildOwnMenu(SerializedProperty property)
        {
            var menu = new GenericMenu();

            menu.AddItem(new GUIContent(CopyItemName), false, () => FieldValueClipboard.Copy(property));

            if (FieldValueClipboard.CanPaste(property))
                menu.AddItem(new GUIContent(PasteItemName), false, () => FieldValueClipboard.Paste(property));
            else
                menu.AddDisabledItem(new GUIContent(PasteItemName));

            return menu;
        }
    }
}
