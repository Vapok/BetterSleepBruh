using BetterSleepBruh.Components;
using BetterSleepBruh.Configuration;
using HarmonyLib;

namespace BetterSleepBruh.Patches;

internal static class FermenterPatches
{
    private static readonly int _lastBoostCheckKey = "BSB_FermenterBoostChecked".GetStableHashCode();

    private static void StampCurrentBoostTicks(Fermenter fermenter)
    {
        if (ConfigRegistry.ProtectMeadTimers == null || !ConfigRegistry.ProtectMeadTimers.Value)
            return;

        if (!SleepTracker.HasSyncedBoostTotal || fermenter == null)
            return;

        ZNetView nview = fermenter.m_nview;
        if (nview == null || !nview.IsValid() || !nview.IsOwner())
            return;

        ZDO zdo = nview.GetZDO();
        if (zdo == null)
            return;

        long startTime = zdo.GetLong(ZDOVars.s_startTime, 0L);
        if (startTime > 0L)
        {
            long currentTotalTicks = (long)(SleepTracker.TotalPartialSleepBoostSeconds * 10000000.0);
            zdo.Set(_lastBoostCheckKey, currentTotalTicks);
        }
    }

    [HarmonyPatch(typeof(Fermenter), "RPC_AddItem")]
    private static class RpcAddItemPatch
    {
        private static void Postfix(Fermenter __instance)
        {
            StampCurrentBoostTicks(__instance);
        }
    }

    [HarmonyPatch(typeof(Fermenter), "ResetFermentationTimer")]
    private static class ResetFermentationTimerPatch
    {
        private static void Postfix(Fermenter __instance)
        {
            StampCurrentBoostTicks(__instance);
        }
    }

    [HarmonyPatch(typeof(Fermenter), "GetFermentationTime")]
    private static class GetFermentationTimePatch
    {
        private static void Prefix(Fermenter __instance)
        {
            if (ConfigRegistry.ProtectMeadTimers == null || !ConfigRegistry.ProtectMeadTimers.Value)
                return;

            if (!SleepTracker.HasSyncedBoostTotal)
                return;

            ZNetView nview = __instance.m_nview;
            if (nview == null || !nview.IsValid() || !nview.IsOwner())
                return;

            ZDO zdo = nview.GetZDO();
            if (zdo == null)
                return;

            long startTime = zdo.GetLong(ZDOVars.s_startTime, 0L);
            if (startTime == 0L)
            {
                if (zdo.GetLong(_lastBoostCheckKey, -1L) >= 0L)
                    zdo.Set(_lastBoostCheckKey, -1L);
                return;
            }

            long nowTicks = ZNet.instance != null ? ZNet.instance.GetTime().Ticks : 0L;
            long currentTotalTicks = (long)(SleepTracker.TotalPartialSleepBoostSeconds * 10000000.0);
            long lastCheckedTicks = zdo.GetLong(_lastBoostCheckKey, -1L);
            if (lastCheckedTicks < 0L)
            {
                if (nowTicks > 0L && startTime > nowTicks)
                    zdo.Set(ZDOVars.s_startTime, nowTicks);
                zdo.Set(_lastBoostCheckKey, currentTotalTicks);
                return;
            }

            long deltaTicks = currentTotalTicks - lastCheckedTicks;
            if (deltaTicks > 0L)
            {
                long newStartTime = startTime + deltaTicks;
                if (nowTicks > 0L && newStartTime > nowTicks)
                    newStartTime = nowTicks;
                zdo.Set(ZDOVars.s_startTime, newStartTime);
                zdo.Set(_lastBoostCheckKey, currentTotalTicks);
            }
            else if (nowTicks > 0L && startTime > nowTicks)
            {
                zdo.Set(ZDOVars.s_startTime, nowTicks);
            }
        }
    }
}
