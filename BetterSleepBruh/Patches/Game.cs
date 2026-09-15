using System;
using BetterSleepBruh.Components;
using BetterSleepBruh.Configuration;
using HarmonyLib;

namespace BetterSleepBruh.Patches;

public class GamePatches
{
    [HarmonyPatch(typeof(Game), nameof(Game.UpdateSleeping))]
    public static class UpdateSleepingPatch
    {
        public static bool Prefix(Game __instance)
        {
            if (ConfigRegistry.UseVanilleSleep != null && ConfigRegistry.UseVanilleSleep.Value)
                return true;

            if (ZNet.instance == null || !ZNet.instance.IsServer() || EnvMan.instance == null)
                return false;

            if (__instance.m_sleeping)
            {
                if (!EnvMan.instance.IsTimeSkipping())
                {
                    __instance.m_sleeping = false;
                    ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, "SleepStop", Array.Empty<object>());
                }
                return false;
            }

            if (EnvMan.instance.IsTimeSkipping())
                return false;

            SleepTracker.GetSleepOccupancyCounts(out var playerCount, out var playersSleeping);

            if (playerCount > 0 && playersSleeping >= playerCount)
            {
                __instance.m_sleeping = true;
                ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, "SleepStart", Array.Empty<object>());
                EnvMan.instance.SkipToMorning();
            }

            return false;
        }
    }
}
