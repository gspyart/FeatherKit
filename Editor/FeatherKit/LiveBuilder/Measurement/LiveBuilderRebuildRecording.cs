namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Итог одной записи <see cref="LiveBuilderRebuildProfiler"/>: сколько она шла, вызовы и время каждой стадии, счётчики.
    /// Своё время стадий не пересекается: их сумма плюс время вне стадий — вся запись.
    /// </summary>
    public sealed class LiveBuilderRebuildRecording
    {
        private readonly int[] callsByStage;
        private readonly double[] selfMillisecondsByStage;
        private readonly double[] totalMillisecondsByStage;


        public LiveBuilderRebuildRecording(double elapsedMilliseconds, int[] callsByStage, double[] selfMillisecondsByStage, double[] totalMillisecondsByStage,
            int passCount, int rebuiltConstructionCount, int rebuiltBuildingPartCount, int savedFileCount)
        {
            ElapsedMilliseconds = elapsedMilliseconds;
            this.callsByStage = callsByStage;
            this.selfMillisecondsByStage = selfMillisecondsByStage;
            this.totalMillisecondsByStage = totalMillisecondsByStage;
            PassCount = passCount;
            RebuiltConstructionCount = rebuiltConstructionCount;
            RebuiltBuildingPartCount = rebuiltBuildingPartCount;
            SavedFileCount = savedFileCount;
        }


        public double ElapsedMilliseconds { get; }
        public int PassCount { get; }
        public int RebuiltConstructionCount { get; }
        public int RebuiltBuildingPartCount { get; }
        public int SavedFileCount { get; }

        // Сумма своего времени всех стадий; остальное — вне стадий.
        public double StagesMilliseconds
        {
            get
            {
                var sum = 0.0;

                foreach (var milliseconds in selfMillisecondsByStage)
                    sum += milliseconds;

                return sum;
            }
        }


        public int CallsOf(LiveBuilderRebuildStage stage)
        {
            return callsByStage[(int)stage];
        }


        public double SelfMillisecondsOf(LiveBuilderRebuildStage stage)
        {
            return selfMillisecondsByStage[(int)stage];
        }


        public double TotalMillisecondsOf(LiveBuilderRebuildStage stage)
        {
            return totalMillisecondsByStage[(int)stage];
        }
    }
}
