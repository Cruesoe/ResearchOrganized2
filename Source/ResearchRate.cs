using HarmonyLib;
using RimWorld;
using Verse;

namespace ResearchOrganized2
{
    /// <summary>The colony's recent research speed, from the points colonists add to the current project,
    /// kept in hourly buckets over the last day. Lifetime: one game; cleared when another game is loaded.</summary>
    internal static class ResearchRate
    {
        private const int BucketTicks = GenDate.TicksPerHour;
        private const int Buckets = 24;

        private static readonly float[] points = new float[Buckets];
        private static readonly int[] bucketHour = new int[Buckets];
        private static int firstTick = -1;
        private static Game game;

        private struct Sample
        {
            public ResearchProjectDef project;
            public float before;
        }

        public static void Patch(Harmony harmony)
        {
            var method = AccessTools.Method(typeof(ResearchManager), nameof(ResearchManager.ResearchPerformed));
            if (method == null) return;
            harmony.Patch(method, prefix: new HarmonyMethod(typeof(ResearchRate), nameof(Before)),
                postfix: new HarmonyMethod(typeof(ResearchRate), nameof(After)));
        }

        private static void Before(ResearchManager __instance, out Sample __state)
        {
            var project = __instance.GetProject();
            __state = new Sample { project = project, before = project != null ? __instance.GetProgress(project) : 0f };
        }

        private static void After(ResearchManager __instance, Sample __state)
        {
            if (__state.project == null) return;
            float added = __instance.GetProgress(__state.project) - __state.before;
            if (added > 0f) Record(added);
        }

        private static void EnsureGame()
        {
            if (Current.Game == game) return;
            game = Current.Game;
            firstTick = -1;
            for (int i = 0; i < Buckets; i++)
            {
                points[i] = 0f;
                bucketHour[i] = -1;
            }
        }

        private static void Record(float added)
        {
            EnsureGame();
            int tick = Find.TickManager.TicksGame;
            if (firstTick < 0) firstTick = tick;
            int hour = tick / BucketTicks;
            int index = hour % Buckets;
            if (bucketHour[index] != hour)
            {
                bucketHour[index] = hour;
                points[index] = 0f;
            }
            points[index] += added;
        }

        /// <summary>The current game hour, so callers can refresh cached estimates once an hour.</summary>
        public static int Hour => Find.TickManager != null ? Find.TickManager.TicksGame / BucketTicks : 0;

        /// <summary>Progress per tick over the last day, or 0 until there is at least an hour of data.</summary>
        private static float PerTick()
        {
            EnsureGame();
            if (firstTick < 0) return 0f;
            int tick = Find.TickManager.TicksGame;
            int span = System.Math.Min(tick - firstTick, Buckets * BucketTicks);
            if (span < BucketTicks) return 0f;
            int hour = tick / BucketTicks;
            float sum = 0f;
            for (int i = 0; i < Buckets; i++)
                if (bucketHour[i] > hour - Buckets) sum += points[i];
            return sum / span;
        }

        /// <summary>Ticks left on a standard research project at the recent pace; -1 when there is no estimate.</summary>
        public static int TicksLeft(ResearchProjectDef project)
        {
            if (project == null || project.knowledgeCategory != null || project.IsFinished) return -1;
            float rate = PerTick();
            if (rate <= 0f) return -1;
            float left = project.Cost - project.ProgressReal;
            return left <= 0f ? -1 : (int)System.Math.Min(left / rate, int.MaxValue);
        }
    }
}
