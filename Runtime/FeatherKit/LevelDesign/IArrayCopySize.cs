using UnityEngine;

namespace FeatherKit.LevelDesign
{
    /// <summary>
    /// Свой размер для <see cref="ArrayModifier"/> у объекта, чей размер не видно по мешам: проём, который режет чужую стену,
    /// триггер, точка. Массив ставит копии «от края» (Step From Edge) по этому размеру, а не по мешам объекта и его детей.
    /// </summary>
    public interface IArrayCopySize
    {
        // Размер одной копии по осям объекта, м; от опоры объекта до соседней — этот размер плюс зазор массива.
        Vector3 ArrayCopySize { get; }
    }
}
