using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FeatherKit.EditorTools
{
    /// <summary>
    /// Ссылки на объекты сцены при копировании графа через текст: EditorJsonUtility их не сохраняет, пишет пустыми.
    /// Собираем их отдельно — путь поля и id объекта — и после копии кладём на те же пути.
    /// </summary>
    internal static class ScenarioSceneReferences
    {
        /// <summary>Объекты сцены под root; путь в ответе — с targetPath вместо пути root: так он ляжет в копию.</summary>
        public static List<ScenarioSceneReference> Collect(SerializedProperty root, string targetPath)
        {
            var references = new List<ScenarioSceneReference>();
            var property = root.Copy();
            var end = root.GetEndProperty();

            while (property.Next(true) && !SerializedProperty.EqualContents(property, end))
            {
                if (property.propertyType != SerializedPropertyType.ObjectReference || property.objectReferenceValue == null
                                                                                    || EditorUtility.IsPersistent(property.objectReferenceValue))
                    continue;

                var path = targetPath + property.propertyPath.Substring(root.propertyPath.Length);
                references.Add(new ScenarioSceneReference(path, property.objectReferenceValue));
            }

            return references;
        }

        /// <summary>Положить ссылки в копию и применить. Отдаёт, сколько объектов не нашлось: сцену закрыли или объект удалили.</summary>
        public static int Restore(SerializedObject target, IEnumerable<ScenarioSceneReference> references)
        {
            var missingCount = 0;
            target.Update();

            foreach (var reference in references)
            {
                var property = target.FindProperty(reference.Path);
                var sceneObject = reference.Resolve();

                if (property == null || sceneObject == null)
                    missingCount++;
                else
                    property.objectReferenceValue = sceneObject;
            }

            target.ApplyModifiedPropertiesWithoutUndo();

            return missingCount;
        }
    }


    /// <summary>Одна ссылка на объект сцены: где лежала и как найти объект — в этой сессии редактора и после перезапуска.</summary>
    [Serializable]
    internal class ScenarioSceneReference
    {
        [SerializeField] private string path;
        [SerializeField] private string globalObjectId;
        [SerializeField] private ulong entityId;
        [SerializeField] private long editorSessionId;


        public ScenarioSceneReference(string path, Object sceneObject)
        {
            this.path = path;
            globalObjectId = GlobalObjectId.GetGlobalObjectIdSlow(sceneObject).ToString();
            entityId = EntityId.ToULong(sceneObject.GetEntityId());
            editorSessionId = EditorAnalyticsSessionInfo.id;
        }


        public string Path => path;


        // В той же сессии — по id сессии: он есть и у объектов без сохранения и в открытом префабе.
        public Object Resolve()
        {
            if (editorSessionId == EditorAnalyticsSessionInfo.id)
            {
                var sameSessionObject = EditorUtility.EntityIdToObject(EntityId.FromULong(entityId));

                if (sameSessionObject != null)
                    return sameSessionObject;
            }

            return GlobalObjectId.TryParse(globalObjectId, out var id) ? GlobalObjectId.GlobalObjectIdentifierToObjectSlow(id) : null;
        }
    }
}
