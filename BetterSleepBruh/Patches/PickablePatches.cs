using BetterSleepBruh.Components;
using BetterSleepBruh.Configuration;
using HarmonyLib;

namespace BetterSleepBruh.Patches;

internal static class PickablePatches
{
    private static readonly int _lastBoostCheckKey = "BSB_PickableBoostChecked".GetStableHashCode();
    private static readonly AccessTools.FieldRef<Pickable, ZNetView> _nviewRef = AccessTools.FieldRefAccess<Pickable, ZNetView>("m_nview");

    private static void ApplyBoostShift(Pickable pickable)
    {
        if (ConfigRegistry.ProtectPickableTimers == null || !ConfigRegistry.ProtectPickableTimers.Value)
            return;

        if (pickable == null || pickable.m_respawnTimeMinutes <= 0f || !pickable.GetPicked())
            return;

        ZNetView nview = _nviewRef(pickable);
        if (nview == null)
            nview = pickable.GetComponent<ZNetView>();

        if (nview == null || !nview.IsValid() || !nview.IsOwner())
            return;

        ZDO zdo = nview.GetZDO();
        if (zdo == null)
            return;

        long currentTotalTicks = (long)(SleepTracker.TotalPartialSleepBoostSeconds * 10000000.0);
        long lastCheckedTicks = zdo.GetLong(_lastBoostCheckKey, -1L);
        if (lastCheckedTicks < 0L)
        {
            zdo.Set(_lastBoostCheckKey, currentTotalTicks);
            return;
        }

        long deltaTicks = currentTotalTicks - lastCheckedTicks;
        if (deltaTicks > 0L)
        {
            long pickedTime = zdo.GetLong(ZDOVars.s_pickedTime, 0L);
            if (pickedTime > 0L)
            {
                long newPickedTime = pickedTime + deltaTicks;
                zdo.Set(ZDOVars.s_pickedTime, newPickedTime);
            }
            zdo.Set(_lastBoostCheckKey, currentTotalTicks);
        }
        else if (lastCheckedTicks > currentTotalTicks)
        {
            zdo.Set(_lastBoostCheckKey, currentTotalTicks);
        }
    }

    [HarmonyPatch(typeof(Pickable), "ShouldRespawn")]
    private static class ShouldRespawnPatch
    {
        private static void Prefix(Pickable __instance)
        {
            ApplyBoostShift(__instance);
        }
    }

    [HarmonyPatch(typeof(Pickable), nameof(Pickable.SetPicked))]
    private static class SetPickedPatch
    {
        private static void Postfix(Pickable __instance, bool picked)
        {
            if (!picked || ConfigRegistry.ProtectPickableTimers == null || !ConfigRegistry.ProtectPickableTimers.Value)
                return;

            if (__instance == null || __instance.m_respawnTimeMinutes <= 0f)
                return;

            ZNetView nview = _nviewRef(__instance);
            if (nview == null)
                nview = __instance.GetComponent<ZNetView>();

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

    [HarmonyPatch(typeof(Pickable), nameof(Pickable.GetHoverText))]
    private static class GetHoverTextPatch
    {
        private static void Prefix(Pickable __instance)
        {
            ApplyBoostShift(__instance);
        }
    }
}
