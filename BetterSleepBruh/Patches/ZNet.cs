using System;
using BetterSleepBruh.Components;
using BetterSleepBruh.Configuration;
using HarmonyLib;

namespace BetterSleepBruh.Patches;

public class ZNetPatches
{
    // Server-only: extra ZNet.SetNetTime after vanilla advance — clients never run this branch.
    [HarmonyPatch(typeof(ZNet), nameof(ZNet.UpdateNetTime))]
    static class UpdateNetTime
    {
        static void Postfix(ZNet __instance, float dt)
        {
            if (!__instance.IsServer()) return;
            if (__instance.GetNrOfPlayers() <= 0) return;
            if (SleepTracker.Instance == null) return;
            if (!SleepTracker.Instance.Enabled) return;

            var extraRate = SleepTracker.ComputePartialSleepBoost();
            if (extraRate <= 0.0) return;

            var time = __instance.GetTimeSeconds();
            // Same morning target as EnvMan.SkipToMorning: GetMorningStartSec(day + 1) from GetDay(time - 0.15 * dayLen).
            var morningStartSec = SleepTracker.GetNextMorningCapSeconds();

            if (double.IsPositiveInfinity(morningStartSec))
            {
                __instance.SetNetTime(time + dt * extraRate);
                return;
            }

            if (time >= morningStartSec)
            {
                __instance.SetNetTime(morningStartSec);
                return;
            }

            var remainingToMorning = morningStartSec - time;

            var effectiveExtraRate = extraRate;
            var fadeReal = ConfigRegistry.BoostFadeRealSecondsBeforeMorning != null
                ? ConfigRegistry.BoostFadeRealSecondsBeforeMorning.Value
                : 2f;
            if (fadeReal > 0.0f)
            {
                // Net game-time rate from vanilla dt + our boost is (1 + extraRate) game-sec per real-sec.
                // Last `fadeReal` real-time seconds of the approach span `fadeReal * (1 + extraRate)` game-sec before morning.
                var totalRate = 1.0 + extraRate;
                var fadeWindowGame = fadeReal * totalRate;
                if (remainingToMorning < fadeWindowGame)
                    effectiveExtraRate = extraRate * (remainingToMorning / fadeWindowGame);
            }

            var boostDelta = dt * effectiveExtraRate;
            // Do not overshoot morningStartSec (same target as EnvMan.SkipToMorning).
            var appliedBoost = Math.Min(boostDelta, remainingToMorning);
            __instance.SetNetTime(time + appliedBoost);
        }
    }
}
