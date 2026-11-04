using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools
{
    /// <summary>
    /// Низ окна групп: кнопки «New Group» (рядом с выделенным) и «New at Root» (в корне сцены).
    /// Нажал — на их месте поле имени. Enter или «Create» — создать, Esc или «×» — отказаться.
    /// Саму группу заводит окно: эта полоска только спрашивает, где и как назвать.
    /// </summary>
    public class ObjectGroupCreationBar
    {
        public const float Height = 30f;

        private const string NameFieldControl = "ObjectGroupNewName";
        private const string DefaultGroupName = "Group";
        private const float ButtonWidth = 96f;
        private const float SmallButtonWidth = 22f;
        private const float Padding = 6f;

        private ObjectGroupPlacement placement;
        private string groupName = DefaultGroupName;
        private bool isFocusRequested;


        public bool IsNaming { get; private set; }


        // ── Имя ──────────────────────────────────────────

        public void StartNaming(ObjectGroupPlacement newPlacement)
        {
            placement = newPlacement;
            groupName = DefaultGroupName;
            IsNaming = true;
            isFocusRequested = true;
        }


        public void CancelNaming() => IsNaming = false;


        // Имя не пустое — отдаёт его и место, полоска возвращается к кнопкам.
        public bool TryConfirm(out ObjectGroupPlacement confirmedPlacement, out string confirmedName)
        {
            confirmedPlacement = placement;
            confirmedName = groupName.Trim();

            if (confirmedName.Length == 0)
                return false;

            IsNaming = false;
            return true;
        }


        // ── Рисование ────────────────────────────────────

        // true — нажали «Create»: окно забирает имя через TryConfirm.
        public bool Draw(Rect rect, bool hasObjectsToMove)
        {
            var inner = new Rect(rect.x + Padding, rect.y + 5f, rect.width - Padding * 2f, rect.height - 10f);
            return IsNaming ? DrawNameField(inner) : DrawButtons(inner, hasObjectsToMove);
        }

        private bool DrawButtons(Rect rect, bool hasObjectsToMove)
        {
            var nextToObjectsRect = new Rect(rect.x, rect.y, ButtonWidth, rect.height);
            var sceneRootRect = new Rect(nextToObjectsRect.xMax + 4f, rect.y, ButtonWidth, rect.height);

            using (new EditorGUI.DisabledScope(!hasObjectsToMove))
            {
                var nextToObjectsContent = new GUIContent("New Group", "A new group where the selection lies, with the selection inside");

                if (GUI.Button(nextToObjectsRect, nextToObjectsContent, EditorStyles.miniButton))
                    StartNaming(ObjectGroupPlacement.NextToObjects);
            }

            var sceneRootContent = new GUIContent("New at Root", "A new group at the scene root; the selection goes inside, if any");

            if (GUI.Button(sceneRootRect, sceneRootContent, EditorStyles.miniButton))
                StartNaming(ObjectGroupPlacement.SceneRoot);

            return false;
        }

        private bool DrawNameField(Rect rect)
        {
            var cancelRect = new Rect(rect.xMax - SmallButtonWidth, rect.y, SmallButtonWidth, rect.height);
            var createRect = new Rect(cancelRect.x - 4f - ButtonWidth * 0.7f, rect.y, ButtonWidth * 0.7f, rect.height);
            var fieldRect = new Rect(rect.x, rect.y, createRect.x - 4f - rect.x, rect.height);

            GUI.SetNextControlName(NameFieldControl);
            groupName = EditorGUI.TextField(fieldRect, groupName);

            if (isFocusRequested)
            {
                EditorGUI.FocusTextInControl(NameFieldControl);
                isFocusRequested = false;
            }

            var createLabel = placement == ObjectGroupPlacement.SceneRoot ? "At Root" : "Create";
            var isCreatePressed = GUI.Button(createRect, createLabel, EditorStyles.miniButton);

            if (GUI.Button(cancelRect, "×", EditorStyles.miniButton))
                CancelNaming();

            return isCreatePressed;
        }
    }
}
