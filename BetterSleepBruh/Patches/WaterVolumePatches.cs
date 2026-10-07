using BetterSleepBruh.Components;
using BetterSleepBruh.Configuration;
using HarmonyLib;
using UnityEngine;

namespace BetterSleepBruh.Patches;

internal static class WaterVolumePatches
{
    private static float _currentCalmFactor = 1.0f;
    private static int _lastCalmFrame = -1;

    [HarmonyPatch(typeof(WaterVolume), "UpdateWaterTime")]
    private static class UpdateWaterTimePatch
    {
        private static bool Prefix(float dt)
        {
            if (ConfigRegistry.DecoupleWaterWaveSpeed == null || !ConfigRegistry.DecoupleWaterWaveSpeed.Value)
                return true;

            if (SleepTracker.CurrentExtraRate <= 0.0)
                return true;

            WaterVolume.s_wrappedDayTimeSeconds = (WaterVolume.s_wrappedDayTimeSeconds + dt) % 86400f;
            WaterVolume.s_waterTime += dt;
            return false;
        }
    }

    [HarmonyPatch(typeof(WaterVolume), nameof(WaterVolume.GetWaterSurface))]
    private static class GetWaterSurfacePatch
    {
        private static void Prefix(ref float waveFactor)
        {
            if (ConfigRegistry.CalmOceanDuringSleep == null || !ConfigRegistry.CalmOceanDuringSleep.Value)
                return;

            float target = SleepTracker.CurrentExtraRate > 0.0
                ? (ConfigRegistry.OceanCalmMultiplier != null ? ConfigRegistry.OceanCalmMultiplier.Value : 0.5f)
                : 1.0f;

            int currentFrame = Time.frameCount;
            if (_lastCalmFrame != currentFrame)
            {
                _lastCalmFrame = currentFrame;
                _currentCalmFactor = Mathf.MoveTowards(_currentCalmFactor, target, Time.deltaTime * 0.5f);
            }

            waveFactor *= _currentCalmFactor;
        }
    }
}
