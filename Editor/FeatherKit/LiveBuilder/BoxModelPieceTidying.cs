using FeatherKit.LiveBuilder;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Порядок в кусках объекта из коробок — коробках, комнатах, лестницах, группах повтора. Наводится перед каждой сверкой меша:
    /// куски не поворачиваются и не масштабируются, все, кроме групп, помечены EditorOnly и в сборку не попадают,
    /// имя коробки по умолчанию следует за её режимом. Здесь же имена и метка, с которыми куски создаются.
    /// </summary>
    public static class BoxModelPieceTidying
    {
        public const string AddPartName = "Add Box";
        public const string CutPartName = "Cut Box";

        // Метка Unity: объект с ней в сборку игры не попадает.
        public const string EditorOnlyTag = "EditorOnly";


        public static void Tidy(BoxModel model)
        {
            foreach (var part in BoxModelRebuilder.CollectParts(model))
            {
                TidyTransform(part.transform);
                MarkEditorOnly(part.gameObject);
                RenameByMode(part);
            }

            foreach (var stairs in model.GetComponentsInChildren<BoxModelStairs>(false))
            {
                TidyTransform(stairs.transform);
                MarkEditorOnly(stairs.gameObject);
            }

            foreach (var room in model.GetComponentsInChildren<BoxModelRoom>(false))
            {
                TidyTransform(room.transform);
                MarkEditorOnly(room.gameObject);
            }

            foreach (var repeat in model.GetComponentsInChildren<BoxModelRepeat>(false))
                TidyTransform(repeat.transform);
        }

        private static void TidyTransform(Transform pieceTransform)
        {
            if (pieceTransform.localRotation != Quaternion.identity)
                pieceTransform.localRotation = Quaternion.identity;

            if (pieceTransform.localScale != Vector3.one)
                pieceTransform.localScale = Vector3.one;
        }

        private static void MarkEditorOnly(GameObject piece)
        {
            if (!piece.CompareTag(EditorOnlyTag))
                piece.tag = EditorOnlyTag;
        }

        // Имя по умолчанию следует за режимом: «Add Box (1)», переключённая на вырезание, становится «Cut Box (1)». Свои имена не трогаем.
        private static void RenameByMode(BoxModelPart part)
        {
            var wrongPrefix = part.Mode == BoxModelPartMode.Add ? CutPartName : AddPartName;
            var rightPrefix = part.Mode == BoxModelPartMode.Add ? AddPartName : CutPartName;

            if (part.name.StartsWith(wrongPrefix))
                part.name = rightPrefix + part.name.Substring(wrongPrefix.Length);
        }
    }
}
