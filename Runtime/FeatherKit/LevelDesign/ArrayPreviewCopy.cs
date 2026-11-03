using UnityEngine;

namespace FeatherKit.LevelDesign
{
    /// <summary>
    /// Метка скрытой копии <see cref="ArrayModifier"/> в редакторе: чья она и какой номер. Такую копию не видно в иерархии,
    /// она не сохраняется в сцену и не попадает в сборку — редактор держит её по меткам, даже после перезагрузки скриптов.
    /// «Apply» снимает метку: копия становится обычным объектом.
    /// </summary>
    [AddComponentMenu("")]
    [DisallowMultipleComponent]
    public class ArrayPreviewCopy : MonoBehaviour
    {
        [SerializeField] private ArrayModifier owner;
        [SerializeField] private int number;


        public ArrayModifier Owner
        {
            get => owner;
            set => owner = value;
        }

        // Номер копии в массиве: у самого объекта — 1.
        public int Number
        {
            get => number;
            set => number = value;
        }
    }
}
