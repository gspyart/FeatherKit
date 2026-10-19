using System;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LevelPalette
{
    /// <summary>Поле для имени вкладки или пресета прямо под ним: Enter — сохранить, Esc — закрыть.</summary>
    public class PaletteNameEditPopup : PopupWindowContent
    {
        private const string NameFieldControl = "LevelPaletteNameField";

        private readonly string originalName;
        private readonly Action<string> applyName;
        private string name;
        private bool isFocusSet;


        public PaletteNameEditPopup(string currentName, Action<string> applyName)
        {
            originalName = currentName;
            this.applyName = applyName;
            name = currentName;
        }


        public override Vector2 GetWindowSize()
        {
            return new Vector2(200f, EditorGUIUtility.singleLineHeight + 8f);
        }


        public override void OnGUI(Rect rect)
        {
            var current = Event.current;

            if (current.type == EventType.KeyDown && (current.keyCode == KeyCode.Return || current.keyCode == KeyCode.KeypadEnter))
            {
                ApplyName();
                editorWindow.Close();
                return;
            }

            GUI.SetNextControlName(NameFieldControl);
            name = EditorGUI.TextField(new Rect(4f, 4f, rect.width - 8f, EditorGUIUtility.singleLineHeight), name);

            if (!isFocusSet)
            {
                EditorGUI.FocusTextInControl(NameFieldControl);
                isFocusSet = true;
            }
        }

        private void ApplyName()
        {
            if (!string.IsNullOrWhiteSpace(name) && name.Trim() != originalName)
                applyName(name.Trim());
        }
    }
}
