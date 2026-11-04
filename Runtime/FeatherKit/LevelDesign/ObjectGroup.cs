using FeatherKit.Attributes;
using UnityEngine;

namespace FeatherKit.LevelDesign
{
    /// <summary>
    /// Метка «группа объектов», как коллекция в Blender: пустой объект-папка, куда кладут части одного целого —
    /// здание, комнату, участок уровня. Помеченные объекты видны в окне групп (клавиша M в редакторе):
    /// выделенное переезжает в группу одним кликом, там же переключатели группы.
    /// Клик в окне сцены по части группы сначала выделяет саму группу, второй клик — деталь (<see cref="SelectionBaseAttribute"/>).
    /// Хранит одну настройку — «всё внутри статичное»: флаги ставит редактор, в том числе тому, что положат потом.
    /// В игре метка ничего не делает.
    /// </summary>
    [FDescription("Object group, like a Blender collection. Press M to open the groups window: move the selection here, " +
                  "toggle the group on and off, hide it in the Scene view, make everything inside static.")]
    [AddComponentMenu("FeatherKit/Level Design/Object Group")]
    [DisallowMultipleComponent]
    [SelectionBase]
    public class ObjectGroup : MonoBehaviour
    {
        public const string KeepsContentsStaticField = nameof(keepsContentsStatic);

        [Tooltip("On — the group and everything inside get all Static flags, including objects added later. " +
                 "Off here — the flags stay as they are; switching Static off in the groups window (M) also removes them")]
        [SerializeField] private bool keepsContentsStatic;


        public bool KeepsContentsStatic => keepsContentsStatic;
    }
}
