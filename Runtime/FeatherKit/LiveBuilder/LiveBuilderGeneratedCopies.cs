using System.Collections.Generic;
using FeatherKit.Attributes;
using UnityEngine;

namespace FeatherKit.LiveBuilder
{
    /// <summary>
    /// Контейнер настоящих копий одного оригинала из повтора (<see cref="LiveBuilderArray"/>): экземпляры префабов и прочие объекты.
    /// - чей он, какие копии и из какого содержимого оригинала они собраны
    /// - выключил ли повтор сам оригинал: на своём месте он не показан (пропуск или другой вариант узора)
    /// Лежит соседом оригинала, спрятан в иерархии, сохраняется в сцену; ведёт его редактор Live Builder.
    /// </summary>
    [FDescription("Настоящие копии одного объекта из повтора Live Builder. Создаёт и убирает сам повтор — руками не править.")]
    [AddComponentMenu("")]
    [DisallowMultipleComponent]
    public class LiveBuilderGeneratedCopies : MonoBehaviour
    {
        [SerializeField] private GameObject original;
        [SerializeField] private List<GameObject> copies = new();
        [SerializeField] private bool hidesOriginal;
        [SerializeField] private string builtContent = "";


        public GameObject Original
        {
            get => original;
            set => original = value;
        }

        public List<GameObject> Copies => copies ??= new List<GameObject>();

        // Повтор выключил оригинал: показать его снова — тоже дело повтора, а выключенный руками он не трогает.
        public bool HidesOriginal
        {
            get => hidesOriginal;
            set => hidesOriginal = value;
        }

        // Подпись содержимого оригинала, из которого собраны копии: поменялась — копии собираются заново.
        public string BuiltContent
        {
            get => builtContent;
            set => builtContent = value ?? "";
        }
    }
}
