using System;

namespace PolicePursuit.Core
{
    public static class WantedLevel
    {
        // One extra star every 30 s, 1..5.
        public static int ForDuration(double seconds) => Math.Clamp(1 + (int)(seconds / 30), 1, 5);
    }
}
