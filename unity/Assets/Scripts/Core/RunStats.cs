namespace PolicePursuit.Core
{
    public class RunStats
    {
        public double DistanceM;
        public int NearMisses;
        public double DurationS;
        public int MaxWanted = 1;
        public int Coins;

        public long Score => ScoreFormula.Compute(DistanceM, NearMisses, DurationS, MaxWanted);

        public void Reset()
        {
            DistanceM = 0;
            NearMisses = 0;
            DurationS = 0;
            MaxWanted = 1;
            Coins = 0;
        }
    }
}
