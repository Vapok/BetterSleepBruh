using BetterSleepBruh.Configuration;
using HarmonyLib;

namespace BetterSleepBruh.Patches;

public class EnvManPatches
{
    [HarmonyPatch(typeof(EnvMan), nameof(EnvMan.CalculateCanSleep))]
    static class CalculateCanSleepPatch
    {
        static bool Prefix(EnvMan __instance, ref bool __result)
        {
            var dayFraction = __instance.GetDayFraction();
            var sleepStart = ConfigRegistry.SleepStart.Value;
            if (sleepStart < 0.25f)
                __result = dayFraction < 0.25f && dayFraction >= sleepStart;
            else
                __result = dayFraction >= sleepStart || dayFraction < 0.25f;

            return false;
        }
    }
}