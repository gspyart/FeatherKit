using System.Collections.Generic;
using UnityEngine;

namespace FeatherKit.Feedback
{
    /// <summary>
    /// Личные копии материалов объекта под одно шейдерное свойство: собрать их, писать
    /// в них значение и прибрать за собой.
    ///
    /// Копиями, а не через MaterialPropertyBlock: MPB выкидывает рендерер из SRP Batcher,
    /// а копии — нет, батчер группирует по варианту шейдера. И не общим ассетом: тогда
    /// от одного попадания засветилась бы вся волна врагов.
    ///
    /// Заводит их тот, кто гонит эффект — вспышка урона, растворение. Кто завёл, тот
    /// и обязан позвать <see cref="Dispose"/>: копии материалов сборщиком мусора
    /// не собираются и текут до конца сессии.
    /// </summary>
    public class MaterialCopies
    {
        private readonly Material[] instances;


        /// <summary>
        /// Собрать копии со всех рендереров под объектом, у чьих материалов есть
        /// нужное свойство.
        /// </summary>
        public MaterialCopies(GameObject root, int propertyId)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            var collected = new List<Material>();

            for (var i = 0; i < renderers.Length; i++)
            {
                // Сначала смотрим в sharedMaterials, и это не придирка. Обращение
                // к renderer.materials САМО создаёт копии и подменяет ими общий ассет.
                // Спросишь у ненужного рендерера — копии уже созданы, а в список мы их
                // не возьмём, значит и не уничтожим. Такие копии текут до конца сессии.
                if (!HasProperty(renderers[i].sharedMaterials, propertyId))
                    continue;

                var copies = renderers[i].materials;

                // Берём все копии этого рендерера, даже без свойства: раз уж они созданы,
                // за ними надо прибрать. Запись в материал без свойства ничего не делает.
                for (var j = 0; j < copies.Length; j++)
                {
                    if (copies[j] != null)
                        collected.Add(copies[j]);
                }
            }

            instances = collected.ToArray();
        }


        /// <summary>Сколько копий завели. Ноль — свойства нет ни на одном материале.</summary>
        public int Count => instances.Length;


        public void SetFloat(int propertyId, float value)
        {
            for (var i = 0; i < instances.Length; i++)
            {
                if (instances[i] != null)
                    instances[i].SetFloat(propertyId, value);
            }
        }

        public void SetColor(int propertyId, Color value)
        {
            for (var i = 0; i < instances.Length; i++)
            {
                if (instances[i] != null)
                    instances[i].SetColor(propertyId, value);
            }
        }


        /// <summary>Стоит ли ключевое слово хотя бы на одной копии.</summary>
        public bool HasKeyword(string keyword)
        {
            for (var i = 0; i < instances.Length; i++)
            {
                if (instances[i] != null && instances[i].IsKeywordEnabled(keyword))
                    return true;
            }

            return false;
        }


        public void Dispose()
        {
            for (var i = 0; i < instances.Length; i++)
            {
                if (instances[i] != null)
                    Object.Destroy(instances[i]);
            }
        }


        private static bool HasProperty(Material[] materials, int propertyId)
        {
            for (var i = 0; i < materials.Length; i++)
            {
                if (materials[i] != null && materials[i].HasProperty(propertyId))
                    return true;
            }

            return false;
        }
    }
}
