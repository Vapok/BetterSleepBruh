using BetterSleepBruh.Components;
using BetterSleepBruh.Configuration;
using HarmonyLib;

namespace BetterSleepBruh.Patches;

internal static class CookingStationPatches
{
    [HarmonyPatch(typeof(CookingStation), "GetDeltaTime")]
    private static class GetDeltaTimePatch
    {
        private static void Postfix(ref float __result)
        {
            if (ConfigRegistry.ProtectCookingTimers == null || !ConfigRegistry.ProtectCookingTimers.Value)
                return;

            if (SleepTracker.CurrentExtraRate > 0.0)
            {
                __result /= (float)(1.0 + SleepTracker.CurrentExtraRate);
            }
        }
    }
}
