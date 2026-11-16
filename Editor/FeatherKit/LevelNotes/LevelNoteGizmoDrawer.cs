using FeatherKit.Debugging;
using FeatherKit.LevelNotes;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools
{
    /// <summary>
    /// Рисует заметку в окне сцены: флажок цвета её типа и над ним первую строку текста; у выделенной — весь текст и автор.
    /// Флажок кликабельный — клик выделяет заметку. Отдельно от <see cref="LevelNote"/>, чтобы код рисования не попал в игру.
    /// Ещё рисует призрак флажка там, где заметка встанет, пока вводится её текст.
    /// </summary>
    public static class LevelNoteGizmoDrawer
    {
        // Высота текста — в долях от ползунка размера гизмо, как у FGizmos: вдали мелкий текст прячется, флажок остаётся.
        private const float TextHeight = FGizmos.DefaultTextHeight;

        // Строка текста занимает чуть больше своей высоты.
        private const float LineSpacing = 1.25f;
        private const float TextGapAboveFlag = 0.15f;

        private const string DoneMark = "✓ ";
        private const string EmptyTitle = "(пусто)";
        private const float PreviewPoleThickness = 3f;

        private static readonly Color SelectionOutlineColor = new(1f, 1f, 1f, 0.9f);


        // ── Заметка ──────────────────────────────────────

        [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected | GizmoType.Pickable)]
        private static void DrawNote(LevelNote note, GizmoType gizmoType)
        {
            var shape = new LevelNoteFlagShape(note.transform.position, Camera.current);
            var color = LevelNoteKindStyle.ColorOf(note);
            var isSelected = (gizmoType & GizmoType.Selected) != 0;

            var previousMatrix = Gizmos.matrix;
            var previousColor = Gizmos.color;

            Gizmos.color = color;
            DrawBox(shape.PoleCenter, shape.Rotation, shape.PoleSize, isWire: false);
            DrawBox(shape.ClothCenter, shape.Rotation, shape.ClothSize, isWire: false);

            if (isSelected)
            {
                Gizmos.color = SelectionOutlineColor;
                DrawBox(shape.ClothCenter, shape.Rotation, shape.ClothSize, isWire: true);
            }

            Gizmos.matrix = previousMatrix;
            Gizmos.color = previousColor;

            DrawTexts(note, shape, color, isSelected);
        }

        private static void DrawBox(Vector3 center, Quaternion rotation, Vector3 size, bool isWire)
        {
            Gizmos.matrix = Matrix4x4.TRS(center, rotation, Vector3.one);

            if (isWire)
                Gizmos.DrawWireCube(Vector3.zero, size);
            else
                Gizmos.DrawCube(Vector3.zero, size);
        }

        // Подпись центрируется по точке — поднимаем её на половину высоты, чтобы низ не наезжал на флажок.
        private static void DrawTexts(LevelNote note, LevelNoteFlagShape shape, Color color, bool isSelected)
        {
            var firstLine = note.FirstLine;
            var label = firstLine.Length == 0 ? EmptyTitle : firstLine;

            if (note.IsDone)
                label = DoneMark + label;

            var lineCount = 1;

            if (isSelected)
            {
                var details = DetailsOf(note);

                if (details.Length > 0)
                {
                    label += "\n" + details;
                    lineCount += 1 + CountLineBreaks(details);
                }
            }

            var labelWorldHeight = lineCount * TextHeight * Gizmos.probeSize * LineSpacing;
            var labelPosition = shape.PoleTop + Vector3.up * (TextGapAboveFlag + labelWorldHeight * 0.5f);
            FGizmos.Label(labelPosition, label, color, TextHeight);
        }

        private static int CountLineBreaks(string text)
        {
            var count = 0;

            foreach (var character in text)
            {
                if (character == '\n')
                    count++;
            }

            return count;
        }

        // Всё после первой строки и подпись автора.
        private static string DetailsOf(LevelNote note)
        {
            var text = note.Text.Trim();
            var lineEnd = text.IndexOf('\n');
            var rest = lineEnd < 0 ? "" : text.Substring(lineEnd + 1).Trim();
            var authorLine = string.IsNullOrWhiteSpace(note.Author) ? "" : $"— {note.Author}";

            if (rest.Length == 0)
                return authorLine;

            return authorLine.Length == 0 ? rest : rest + "\n" + authorLine;
        }


        // ── Призрак ──────────────────────────────────────

        // Через Handles, а не Gizmos: зовётся из окна сцены, а не из отрисовки гизмо.
        public static void DrawPreview(Vector3 point, LevelNoteKind kind, Camera camera)
        {
            var shape = new LevelNoteFlagShape(point, camera);
            var color = LevelNoteKindStyle.ColorOf(kind);

            using (new Handles.DrawingScope(color))
            {
                Handles.DrawLine(shape.PoleBottom, shape.PoleTop, PreviewPoleThickness);
                Handles.DrawAAConvexPolygon(shape.ClothCorners());
            }
        }
    }


    /// <summary>
    /// Форма флажка заметки: древко из точки вверх и полотнище от его верха вбок, лицом к камере.
    /// Вблизи — в метрах мира, вдали не мельче нескольких пикселей экрана.
    /// </summary>
    public readonly struct LevelNoteFlagShape
    {
        private const float PoleHeight = 1.6f;
        private const float PoleThickness = 0.05f;
        private const float ClothWidth = 0.7f;
        private const float ClothHeight = 0.45f;
        private const float ClothThickness = 0.02f;

        // Сколько метров мира в 80 пикселях экрана (HandleUtility.GetHandleSize) даёт масштаб 1: дальше флажок не мельчает.
        private const float HandleSizeAtFullScale = 2.5f;


        public LevelNoteFlagShape(Vector3 basePoint, Camera camera)
        {
            var scale = Mathf.Max(1f, HandleUtility.GetHandleSize(basePoint) / HandleSizeAtFullScale);
            var facing = FacingDirection(camera);

            Rotation = Quaternion.LookRotation(facing, Vector3.up);
            Side = Rotation * Vector3.right;
            PoleBottom = basePoint;
            PoleTop = basePoint + Vector3.up * (PoleHeight * scale);
            PoleSize = new Vector3(PoleThickness, PoleHeight, PoleThickness) * scale;
            ClothSize = new Vector3(ClothWidth, ClothHeight, ClothThickness) * scale;
        }


        public Quaternion Rotation { get; }
        public Vector3 Side { get; }
        public Vector3 PoleBottom { get; }
        public Vector3 PoleTop { get; }
        public Vector3 PoleSize { get; }
        public Vector3 ClothSize { get; }

        public Vector3 PoleCenter => (PoleBottom + PoleTop) * 0.5f;

        public Vector3 ClothCenter => PoleTop + Side * (ClothSize.x * 0.5f) - Vector3.up * (ClothSize.y * 0.5f);


        public Vector3[] ClothCorners()
        {
            var clothBottom = PoleTop - Vector3.up * ClothSize.y;
            var width = Side * ClothSize.x;
            return new[] { PoleTop, PoleTop + width, clothBottom + width, clothBottom };
        }


        // Взгляд камеры по горизонтали; камера смотрит отвесно вниз — по её «верху».
        private static Vector3 FacingDirection(Camera camera)
        {
            if (camera == null)
                return Vector3.forward;

            var flatForward = Vector3.ProjectOnPlane(camera.transform.forward, Vector3.up);

            if (flatForward.sqrMagnitude < 0.0001f)
                flatForward = Vector3.ProjectOnPlane(camera.transform.up, Vector3.up);

            return flatForward.sqrMagnitude < 0.0001f ? Vector3.forward : flatForward.normalized;
        }
    }
}
