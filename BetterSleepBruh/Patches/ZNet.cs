using System;
using BetterSleepBruh.Components;
using HarmonyLib;

namespace BetterSleepBruh.Patches;

public class ZNetPatches
{
    [HarmonyPatch(typeof(ZNet), nameof(ZNet.UpdateNetTime))]
    static class UpdateNetTime
    {
        static void Postfix(ZNet __instance, float dt)
        {
            if (!__instance.IsServer()) return;
            if (__instance.GetNrOfPlayers() <= 0) return;
            if (!SleepTracker.Instance.Enabled) return;

            var boost = SleepTracker.ComputePartialSleepBoost();
            if (boost <= 0.0) return;

            var time = __instance.GetTimeSeconds();
            var morningCap = SleepTracker.GetNextMorningCapSeconds();

            if (time >= morningCap)
            {
                time = morningCap;
            }
            else
            {
                time += dt * boost;
                time = Math.Min(time, morningCap);
            }
            
            __instance.SetNetTime(time);
        }
    }
}
