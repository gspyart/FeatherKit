using UnityEngine;

namespace FeatherKit.LiveBuilder
{
    /// <summary>
    /// Докуда стена в здании (<see cref="SplineWall"/> с привязкой к этажам), как верх стены в Revit:
    /// до потолка своего этажа, на весь этаж сквозь перекрытие (шахта лифта — без щелей между этажами) или своя высота.
    /// </summary>
    public enum SplineWallHeightInBuilding
    {
        [InspectorName("До потолка этажа")] ToCeiling,
        [InspectorName("Весь этаж, сквозь перекрытие")] FullStorey,
        [InspectorName("Своя высота")] Custom
    }
}
