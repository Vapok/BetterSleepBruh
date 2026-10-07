using BetterSleepBruh.Components;
using BetterSleepBruh.Configuration;
using HarmonyLib;

namespace BetterSleepBruh.Patches;

internal static class ShipPatches
{
    [HarmonyPatch(typeof(Ship), "UpdateWaterForce")]
    private static class UpdateWaterForcePatch
    {
        private static bool Prefix(Ship __instance, float depth, float time)
        {
            if (SleepTracker.CurrentExtraRate <= 0.0)
                return true;

            bool preventDamage = ConfigRegistry.PreventBoatSleepImpactDamage != null && ConfigRegistry.PreventBoatSleepImpactDamage.Value;
            bool suppressShake = ConfigRegistry.SuppressBoatImpactScreenShake != null && ConfigRegistry.SuppressBoatImpactScreenShake.Value;

            if (preventDamage || suppressShake)
            {
                __instance.m_lastDepth = depth;
                __instance.m_lastUpdateWaterForceTime = time;
                return false;
            }

            return true;
        }
    }
}
