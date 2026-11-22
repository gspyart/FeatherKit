using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Одно место, где Live Builder хранит материал: сторона коробки, пол комнаты, стена по линии, наружные стены здания, стенки выреза.
    /// Меш постройки собирается из этих данных заново при каждой правке — красить надо их, а не меш: покраска меша слетела бы.
    /// Problem — почему сюда материал не положить (у стены тип — материал задаёт он); тогда TryAssign ничего не меняет.
    /// У места может быть и подгонка текстуры (<see cref="LiveBuilderTextureFitAccess"/>): её берёт и кладёт пипетка (<see cref="LiveBuilderTexturePipette"/>).
    /// Найти такое место — <see cref="LiveBuilderMaterialSlots"/>.
    /// </summary>
    public readonly struct LiveBuilderMaterialSlot
    {
        private const string UndoName = "Assign Material";

        private readonly System.Action<Material> assign;
        private readonly System.Func<Material> read;


        public LiveBuilderMaterialSlot(Object owner, string description, System.Action<Material> assign, string problem = null,
            System.Func<Material> read = null, LiveBuilderTextureFitAccess fit = default)
        {
            Owner = owner;
            Description = description;
            Problem = problem;
            Fit = fit;
            this.assign = assign;
            this.read = read;
        }


        // Чьи данные: коробка, комната, лестница, стена, здание, вырез или объект из коробок.
        public Object Owner { get; }

        // Коротко, что покрасится: «верх коробки», «пол здания».
        public string Description { get; }

        public string Problem { get; }

        // Подгонка текстуры этого места; нет — у места её не бывает (материал объекта).
        public LiveBuilderTextureFitAccess Fit { get; }

        public bool IsValid => Owner != null;


        // Положить материал одним шагом отмены; нельзя — false.
        public bool TryAssign(Material material)
        {
            if (Owner == null || Problem != null || assign == null)
                return false;

            Undo.RecordObject(Owner, UndoName);
            assign(material);
            EditorUtility.SetDirty(Owner);
            return true;
        }


        // Материал и подгонку — одним шагом отмены. Материал нельзя (Problem) — подгонка всё равно ложится, ответ — false.
        public bool TryAssign(Material material, LiveBuilderTextureFit fit)
        {
            if (Owner == null)
                return false;

            var canAssignMaterial = Problem == null && assign != null;

            if (!canAssignMaterial && !Fit.CanSet)
                return false;

            Undo.RecordObject(Owner, UndoName);

            if (canAssignMaterial)
                assign(material);

            if (Fit.CanSet)
                Fit.Set(fit);

            EditorUtility.SetDirty(Owner);
            return canAssignMaterial;
        }


        // Что видно на месте сейчас: материал (пусто — то, что за ним по запасному порядку) и подгонка («как есть» — по умолчанию).
        public bool ReadPaint(out Material material, out LiveBuilderTextureFit fit)
        {
            material = read?.Invoke();
            fit = Fit.CanGet ? Fit.Get() ?? new LiveBuilderTextureFit() : new LiveBuilderTextureFit();
            return Owner != null && read != null;
        }
    }


    /// <summary>Как прочитать и записать подгонку текстуры одного места материала; пусто — у места подгонки нет.</summary>
    public readonly struct LiveBuilderTextureFitAccess
    {
        private readonly System.Func<LiveBuilderTextureFit> get;
        private readonly System.Action<LiveBuilderTextureFit> set;


        public LiveBuilderTextureFitAccess(System.Func<LiveBuilderTextureFit> get, System.Action<LiveBuilderTextureFit> set)
        {
            this.get = get;
            this.set = set;
        }


        public bool CanGet => get != null;
        public bool CanSet => set != null;


        // Своя или по запасному порядку; null — «как есть».
        public LiveBuilderTextureFit Get()
        {
            return get?.Invoke();
        }


        public void Set(LiveBuilderTextureFit fit)
        {
            set?.Invoke(fit);
        }


        // Место в списке подгонок постройки.
        public static LiveBuilderTextureFitAccess Of<TPlace>(LiveBuilderTextureFitList<TPlace> fits, TPlace place) where TPlace : struct, System.Enum
        {
            return new LiveBuilderTextureFitAccess(() => fits.Get(place), fit => fits.Set(place, fit));
        }
    }
}
