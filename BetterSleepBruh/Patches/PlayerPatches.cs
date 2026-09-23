using BetterSleepBruh.Components;
using HarmonyLib;

namespace BetterSleepBruh.Patches;

internal static class PlayerPatches
{
    [HarmonyPatch(typeof(Player), nameof(Player.AttachStart))]
    private static class AttachStartPatch
    {
        private static void Postfix(Player __instance, bool isBed)
        {
            if (!isBed)
                return;

            if (ZNet.instance == null)
                return;

            if (SleepTracker.Instance != null && ZNet.instance.IsServer())
            {
                SleepTracker.Instance.OnBedOccupancyChanged();
            }
            else if (__instance == Player.m_localPlayer && ZRoutedRpc.instance != null)
            {
                ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.instance.GetServerPeerID(), "NotifyBedOccupancyChanged");
            }
        }
    }

    [HarmonyPatch(typeof(Player), nameof(Player.AttachStop))]
    private static class AttachStopPatch
    {
        private static void Prefix(Player __instance, out bool __state)
        {
            __state = __instance.InBed();
        }

        private static void Postfix(Player __instance, bool __state)
        {
            if (!__state)
                return;

            if (ZNet.instance == null)
                return;

            if (SleepTracker.Instance != null && ZNet.instance.IsServer())
            {
                SleepTracker.Instance.OnBedOccupancyChanged();
            }
            else if (__instance == Player.m_localPlayer && ZRoutedRpc.instance != null)
            {
                ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.instance.GetServerPeerID(), "NotifyBedOccupancyChanged");
            }
        }
    }
}
