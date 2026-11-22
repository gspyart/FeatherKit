using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Одна копия одного повтора (<see cref="LiveBuilderArrayCopyPoses.PlacedCopiesOf"/>): перенос в мире от оригинала и вариант узора.
    /// - по осям и на прямом участке стены — только сдвиг (<see cref="Shift"/>), до бита как раньше
    /// - на изгибе стены копия ещё и повёрнута (<see cref="IsTurned"/>): такую строят только настоящие копии (<see cref="LiveBuilderArraySync"/>)
    /// </summary>
    public readonly struct LiveBuilderArrayCopy
    {
        private LiveBuilderArrayCopy(Vector3 shift, Matrix4x4 move, bool isTurned, int variant)
        {
            Shift = shift;
            Move = move;
            IsTurned = isTurned;
            Variant = variant;
        }


        // Сдвиг в мире; у повёрнутой — сдвиг точки мира в нуле, сам по себе он ничего не значит.
        public Vector3 Shift { get; }

        // Перенос в мире: копия = Move × оригинал.
        public Matrix4x4 Move { get; }

        public bool IsTurned { get; }

        // Вариант узора (<see cref="LiveBuilderArrayPattern"/>): AllVariants — без узора, копия показывает всё.
        public int Variant { get; }

        public bool IsAtOriginalPlace => !IsTurned && Shift == Vector3.zero;


        // ── Откуда ───────────────────────────────────────────────────────

        public static LiveBuilderArrayCopy Shifted(Vector3 shift, int variant)
        {
            return new LiveBuilderArrayCopy(shift, Matrix4x4.Translate(shift), false, variant);
        }


        public static LiveBuilderArrayCopy Turned(Matrix4x4 move, int variant)
        {
            return new LiveBuilderArrayCopy(move.GetColumn(3), move, true, variant);
        }


        // ── Узор ─────────────────────────────────────────────────────────

        // Показывает ли копия узел варианта variant: узел не под вариантом и копия без узора показывают всё.
        public bool Shows(int variant)
        {
            return variant == LiveBuilderArrayPattern.AllVariants || Variant == LiveBuilderArrayPattern.AllVariants || Variant == variant;
        }
    }
}
