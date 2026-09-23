using System;
using BetterSleepBruh.Components;
using BetterSleepBruh.Configuration;
using HarmonyLib;

namespace BetterSleepBruh.Patches;

internal static class GamePatches
{
    [HarmonyPatch(typeof(Game), nameof(Game.UpdateSleeping))]
    private static class UpdateSleepingPatch
    {
        private static bool Prefix(Game __instance)
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
                    if (ZRoutedRpc.instance != null)
                    {
                        ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, "SleepStop", Array.Empty<object>());
                    }
                }
                return false;
            }

            if (EnvMan.instance.IsTimeSkipping() || !EnvMan.CanSleep())
                return false;

            if (SleepTracker.AllPlayersSleeping)
            {
                __instance.m_sleeping = true;
                if (ZRoutedRpc.instance != null)
                {
                    ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, "SleepStart", Array.Empty<object>());
                }
                EnvMan.instance.SkipToMorning();
            }

            return false;
        }
    }
}
