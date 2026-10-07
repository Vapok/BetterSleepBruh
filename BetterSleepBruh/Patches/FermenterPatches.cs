using BetterSleepBruh.Components;
using BetterSleepBruh.Configuration;
using HarmonyLib;

namespace BetterSleepBruh.Patches;

internal static class FermenterPatches
{
    private static readonly int _lastBoostCheckKey = "BSB_FermenterBoostChecked".GetStableHashCode();

    [HarmonyPatch(typeof(Fermenter), "GetFermentationTime")]
    private static class GetFermentationTimePatch
    {
        private static void Prefix(Fermenter __instance)
        {
            if (ConfigRegistry.ProtectMeadTimers == null || !ConfigRegistry.ProtectMeadTimers.Value)
                return;

            ZNetView nview = __instance.m_nview;
            if (nview == null || !nview.IsValid() || !nview.IsOwner())
                return;

            ZDO zdo = nview.GetZDO();
            if (zdo == null)
                return;

            long startTime = zdo.GetLong(ZDOVars.s_startTime, 0L);
            if (startTime == 0L)
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
                    long newStartTime = startTime + deltaTicks;
                    zdo.Set(ZDOVars.s_startTime, newStartTime);
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
