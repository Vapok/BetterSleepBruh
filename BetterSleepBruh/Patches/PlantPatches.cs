using BetterSleepBruh.Components;
using BetterSleepBruh.Configuration;
using HarmonyLib;

namespace BetterSleepBruh.Patches;

internal static class PlantPatches
{
    private static readonly int _lastBoostCheckKey = "BSB_LastBoostChecked".GetStableHashCode();

    [HarmonyPatch(typeof(Plant), nameof(Plant.Awake))]
    private static class PlantAwakePatch
    {
        private static void Prefix(Plant __instance, ref bool __state)
        {
            __state = false;
            if (ConfigRegistry.ProtectCropTimers == null || !ConfigRegistry.ProtectCropTimers.Value)
                return;

            ZNetView nview = __instance.GetComponent<ZNetView>();
            if (nview == null || !nview.IsValid() || !nview.IsOwner())
                return;

            ZDO zdo = nview.GetZDO();
            if (zdo != null && zdo.GetLong(ZDOVars.s_plantTime, 0L) == 0L)
            {
                __state = true;
            }
        }

        private static void Postfix(Plant __instance, bool __state)
        {
            if (!__state || !SleepTracker.HasSyncedBoostTotal)
                return;

            ZNetView nview = __instance.m_nview;
            if (nview == null || !nview.IsValid() || !nview.IsOwner())
                return;

            ZDO zdo = nview.GetZDO();
            if (zdo != null)
            {
                long currentTotalTicks = (long)(SleepTracker.TotalPartialSleepBoostSeconds * 10000000.0);
                zdo.Set(_lastBoostCheckKey, currentTotalTicks);
            }
        }
    }

    [HarmonyPatch(typeof(Plant), "TimeSincePlanted")]
    private static class TimeSincePlantedPatch
    {
        private static void Prefix(Plant __instance)
        {
            if (ConfigRegistry.ProtectCropTimers == null || !ConfigRegistry.ProtectCropTimers.Value)
                return;

            if (!SleepTracker.HasSyncedBoostTotal)
                return;

            ZNetView nview = __instance.m_nview;
            if (nview == null || !nview.IsValid() || !nview.IsOwner())
                return;

            ZDO zdo = nview.GetZDO();
            if (zdo == null)
                return;

            long nowTicks = ZNet.instance != null ? ZNet.instance.GetTime().Ticks : 0L;
            long plantTime = zdo.GetLong(ZDOVars.s_plantTime, 0L);
            long currentTotalTicks = (long)(SleepTracker.TotalPartialSleepBoostSeconds * 10000000.0);
            long lastCheckedTicks = zdo.GetLong(_lastBoostCheckKey, -1L);
            if (lastCheckedTicks < 0L)
            {
                if (nowTicks > 0L && plantTime > nowTicks)
                    zdo.Set(ZDOVars.s_plantTime, nowTicks);
                zdo.Set(_lastBoostCheckKey, currentTotalTicks);
                return;
            }

            long deltaTicks = currentTotalTicks - lastCheckedTicks;
            if (deltaTicks > 0L)
            {
                if (plantTime > 0L)
                {
                    long newPlantTime = plantTime + deltaTicks;
                    if (nowTicks > 0L && newPlantTime > nowTicks)
                        newPlantTime = nowTicks;
                    zdo.Set(ZDOVars.s_plantTime, newPlantTime);
                }
                zdo.Set(_lastBoostCheckKey, currentTotalTicks);
            }
            else if (nowTicks > 0L && plantTime > nowTicks)
            {
                zdo.Set(ZDOVars.s_plantTime, nowTicks);
            }
        }
    }
}
