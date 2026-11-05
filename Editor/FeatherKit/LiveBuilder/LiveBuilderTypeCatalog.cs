using System.Collections.Generic;
using UnityEditor;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Находит всех наследников базового класса Live Builder и создаёт по одному: инструменты, вкладки окна, карточки «Выделено».
    /// Так новый инструмент, вкладка или карточка — один новый класс, без правки списков. Порядок задаёт тот, кто просит.
    /// </summary>
    public static class LiveBuilderTypeCatalog
    {
        // По одному экземпляру каждого не абстрактного наследника с конструктором без параметров.
        public static List<TBase> CreateAll<TBase>() where TBase : class
        {
            var instances = new List<TBase>();

            foreach (var type in TypeCache.GetTypesDerivedFrom<TBase>())
                if (!type.IsAbstract && !type.ContainsGenericParameters && type.GetConstructor(System.Type.EmptyTypes) != null)
                    instances.Add((TBase)System.Activator.CreateInstance(type));

            return instances;
        }
    }
}
