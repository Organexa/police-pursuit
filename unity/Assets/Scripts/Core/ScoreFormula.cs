using System;

namespace PolicePursuit.Core
{
    public static class ScoreFormula
    {
        // Must match the server (supabase init.sql), including multiplication order.
        // Checked against shared/score_cases.json.
        public static long Compute(double distanceM, int nearMisses, double durationS, int maxWanted) =>
            (long)Math.Floor(distanceM) + nearMisses * 250L + (long)Math.Floor(durationS * maxWanted * 10);
    }
}
