using BetterSleepBruh.Components;
using BetterSleepBruh.Configuration;
using HarmonyLib;

namespace BetterSleepBruh.Patches;

internal static class PlantPatches
{
    private static readonly int _lastBoostCheckKey = "BSB_LastBoostChecked".GetStableHashCode();

    [HarmonyPatch(typeof(Plant), "TimeSincePlanted")]
    private static class TimeSincePlantedPatch
    {
        private static void Prefix(Plant __instance)
        {
            if (ConfigRegistry.ProtectCropTimers == null || !ConfigRegistry.ProtectCropTimers.Value)
                return;

            ZNetView nview = __instance.m_nview;
            if (nview == null || !nview.IsValid() || !nview.IsOwner())
                return;

            ZDO zdo = nview.GetZDO();
            if (zdo == null)
                return;

            long currentTotalTicks = (long)(SleepTracker.TotalPartialSleepBoostSeconds * 10000000.0);
            long lastCheckedTicks = zdo.GetLong(_lastBoostCheckKey, -1L);
            if (lastCheckedTicks < 0L)
            {
                if (nview.IsOwner())
                    zdo.Set(_lastBoostCheckKey, currentTotalTicks);
                return;
            }

            long deltaTicks = currentTotalTicks - lastCheckedTicks;
            if (deltaTicks > 0L)
            {
                if (nview.IsOwner())
                {
                    long plantTime = zdo.GetLong(ZDOVars.s_plantTime, 0L);
                    if (plantTime > 0L)
                    {
                        long newPlantTime = plantTime + deltaTicks;
                        zdo.Set(ZDOVars.s_plantTime, newPlantTime);
                    }
                    zdo.Set(_lastBoostCheckKey, currentTotalTicks);
                }
            }
            else if (lastCheckedTicks > currentTotalTicks && nview.IsOwner())
            {
                zdo.Set(_lastBoostCheckKey, currentTotalTicks);
            }
        }
    }
}
