using BetterSleepBruh.Components;
using BetterSleepBruh.Configuration;
using HarmonyLib;

namespace BetterSleepBruh.Patches;

internal static class SmelterPatches
{
    [HarmonyPatch(typeof(Smelter), "GetDeltaTime")]
    private static class GetDeltaTimePatch
    {
        private static void Postfix(ref double __result)
        {
            if (ConfigRegistry.ProtectSmelterTimers == null || !ConfigRegistry.ProtectSmelterTimers.Value)
                return;

            if (SleepTracker.CurrentExtraRate > 0.0)
            {
                __result /= (1.0 + SleepTracker.CurrentExtraRate);
            }
        }
    }
}
