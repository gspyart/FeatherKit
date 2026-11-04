using System;
using UnityEngine;

namespace FeatherKit.LiveBuilder
{
    /// <summary>
    /// Скос одного ребра коробки (<see cref="BoxModelPart"/>): ребро — стык двух её сторон, срез идёт наискось
    /// от точки на первой стороне до точки на второй. Так из коробки получается пандус, склон под лестницу, скат крыши.
    /// Стороны — номерами: 0 +X, 1 −X, 2 +Y (верх), 3 −Y (низ), 4 +Z, 5 −Z; оси у сторон ребра разные.
    /// </summary>
    [Serializable]
    public struct BoxModelEdgeChamfer
    {
        [SerializeField] private int firstFace;
        [SerializeField] private int secondFace;
        [SerializeField] private float sizeOnFirstFace;
        [SerializeField] private float sizeOnSecondFace;


        public BoxModelEdgeChamfer(int firstFace, int secondFace, float sizeOnFirstFace, float sizeOnSecondFace)
        {
            this.firstFace = firstFace;
            this.secondFace = secondFace;
            this.sizeOnFirstFace = Mathf.Max(0f, sizeOnFirstFace);
            this.sizeOnSecondFace = Mathf.Max(0f, sizeOnSecondFace);
        }


        public int FirstFace => firstFace;
        public int SecondFace => secondFace;

        // Насколько срез заходит на первую сторону от ребра, м.
        public float SizeOnFirstFace => sizeOnFirstFace;

        // Насколько срез заходит на вторую сторону от ребра, м.
        public float SizeOnSecondFace => sizeOnSecondFace;

        // Срез есть, только когда он заходит на обе стороны.
        public bool HasCut => sizeOnFirstFace > 0f && sizeOnSecondFace > 0f;


        public bool IsOnEdge(int first, int second)
        {
            return firstFace == first && secondFace == second || firstFace == second && secondFace == first;
        }
    }
}
