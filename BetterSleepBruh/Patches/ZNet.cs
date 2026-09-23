using System;
using BetterSleepBruh.Components;
using BetterSleepBruh.Configuration;
using HarmonyLib;

namespace BetterSleepBruh.Patches;

internal static class ZNetPatches
{
    private static float _timeSyncTimer;
    private const float TimeSyncInterval = 0.25f;

    [HarmonyPatch(typeof(ZNet), nameof(ZNet.UpdateNetTime))]
    private static class UpdateNetTime
    {
        private static void Postfix(ZNet __instance, float dt)
        {
            if (!__instance.IsServer()) return;
            if (__instance.GetNrOfPlayers() <= 0) return;
            if (SleepTracker.Instance == null) return;
            if (!SleepTracker.Instance.Enabled) return;

            double extraRate = SleepTracker.CurrentExtraRate;
            if (extraRate <= 0.0)
            {
                _timeSyncTimer = 0f;
                return;
            }

            double time = __instance.GetTimeSeconds();
            double morningStartSec = SleepTracker.GetNextMorningCapSeconds();

            if (double.IsPositiveInfinity(morningStartSec))
            {
                __instance.SetNetTime(time + dt * extraRate);
                SyncNetTimeToPeers(__instance, dt);
                return;
            }

            if (time >= morningStartSec)
            {
                __instance.SetNetTime(morningStartSec);
                _timeSyncTimer = 0f;
                return;
            }

            double remainingToMorning = morningStartSec - time;
            double effectiveExtraRate = extraRate;
            float fadeReal = ConfigRegistry.BoostFadeRealSecondsBeforeMorning != null
                ? ConfigRegistry.BoostFadeRealSecondsBeforeMorning.Value
                : 2f;

            if (fadeReal > 0.0f)
            {
                double totalRate = 1.0 + extraRate;
                double fadeWindowGame = fadeReal * totalRate;
                if (remainingToMorning < fadeWindowGame)
                    effectiveExtraRate = extraRate * (remainingToMorning / fadeWindowGame);
            }

            double boostDelta = dt * effectiveExtraRate;
            double appliedBoost = Math.Min(boostDelta, remainingToMorning);
            __instance.SetNetTime(time + appliedBoost);

            if (appliedBoost > 0.0)
            {
                SyncNetTimeToPeers(__instance, dt);
            }
        }

        private static void SyncNetTimeToPeers(ZNet znet, float dt)
        {
            if (znet.m_peers == null || znet.m_peers.Count == 0)
                return;

            _timeSyncTimer += dt;
            if (_timeSyncTimer < TimeSyncInterval)
                return;

            _timeSyncTimer = 0f;
            double netTime = znet.GetTimeSeconds();
            for (int i = 0; i < znet.m_peers.Count; i++)
            {
                ZNetPeer peer = znet.m_peers[i];
                if (peer != null && peer.IsReady() && peer.m_rpc != null)
                {
                    peer.m_rpc.Invoke("NetTime", (object)netTime);
                }
            }
        }
    }
}
