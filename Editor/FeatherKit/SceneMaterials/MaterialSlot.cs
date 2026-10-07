using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools
{
    /// <summary>
    /// Один слот материала на объекте: чей он, какой по счёту и что в нём лежит.
    /// Через него работают все операции окна материалов — рендерер и картинка интерфейса
    /// для них выглядят одинаково.
    /// </summary>
    public class MaterialSlot
    {
        public Component Owner;
        public int Index;
        public Material Material;

        public bool IsSceneMaterial => Material != null && !EditorUtility.IsPersistent(Material);

        /// <summary>Подпись для подсказки: объект, компонент и номер слота, если слотов несколько.</summary>
        public string Title
        {
            get
            {
                var slots = Owner is Renderer renderer ? renderer.sharedMaterials.Length : 1;
                var component = Owner.GetType().Name;

                return slots > 1
                    ? $"{Owner.gameObject.name} · {component}, слот {Index + 1}"
                    : $"{Owner.gameObject.name} · {component}";
            }
        }
    }
}
