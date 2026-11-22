using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Новые колонны (<see cref="LiveBuilderColumn"/>): объект с колонной по нарисованной коробке, одним шагом Undo.
    /// В объекте из коробок — его ребёнок с его материалом и фаской; без объекта — отдельно, туда же, куда новый объект из коробок.
    /// </summary>
    public static class LiveBuilderColumnFactory
    {
        public const string ColumnName = "Column";


        // Колонна в коробке min–max: сечение — по X и Z, высота — по Y. Оси — объекта parentModel, а без него — рамки рисования frameToWorld.
        public static LiveBuilderColumn CreateInBox(BoxModel parentModel, Matrix4x4 frameToWorld, Vector3 min, Vector3 max)
        {
            var columnObject = new GameObject(ColumnName);
            var localMin = Vector3.Min(min, max);

            if (parentModel != null)
            {
                columnObject.transform.SetParent(parentModel.transform, false);
                columnObject.transform.localPosition = localMin;
            }
            else
            {
                ObjectFactory.PlaceGameObject(columnObject, null);

                if (columnObject.transform.parent == null)
                    BoxModelFactory.PutIntoLevelGeometry(columnObject);

                columnObject.transform.SetPositionAndRotation(frameToWorld.MultiplyPoint3x4(localMin), frameToWorld.rotation);
            }

            GameObjectUtility.SetStaticEditorFlags(columnObject, (StaticEditorFlags)~0);
            Undo.RegisterCreatedObjectUndo(columnObject, "Create Column");

            var column = columnObject.AddComponent<LiveBuilderColumn>();
            column.Sides = LiveBuilderBoxModeSettings.ColumnSides;
            column.Size = Vector3.Max(min, max) - localMin;
            column.TextureAlignment = LiveBuilderTextureAlignment.World;
            column.Material = parentModel != null && parentModel.DefaultMaterial != null ? parentModel.DefaultMaterial : BoxModelFactory.LoadDefaultMaterial();

            if (parentModel != null)
            {
                column.BevelSize = parentModel.BevelSize;
                column.BevelSegments = parentModel.BevelSegments;
            }

            return column;
        }
    }
}
