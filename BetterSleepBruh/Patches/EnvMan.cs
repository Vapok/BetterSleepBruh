using BetterSleepBruh.Configuration;
using HarmonyLib;

namespace BetterSleepBruh.Patches;

public class EnvManPatches
{
    [HarmonyPatch(typeof(EnvMan), nameof(EnvMan.CalculateCanSleep))]
    static class CalculateCanSleepPatch
    {
        static bool Prefix(EnvMan __instance, ref bool __result)
        {
            if (ConfigRegistry.UseVanilleSleep != null && ConfigRegistry.UseVanilleSleep.Value)
                return true;
            
            if (__instance.IsTimeSkipping())
            {
                __result = false;
                return false;
            }

            var dayFraction = __instance.GetDayFraction();
            var sleepStart = ConfigRegistry.SleepStart != null ? ConfigRegistry.SleepStart.Value : 0.5f;
            bool inSleepWindow;
            if (sleepStart < 0.25f)
                inSleepWindow = dayFraction < 0.25f && dayFraction >= sleepStart;
            else
                inSleepWindow = dayFraction >= sleepStart || dayFraction < 0.25f;

            if (!inSleepWindow)
            {
                __result = false;
                return false;
            }

            var localPlayer = Player.m_localPlayer;
            if (localPlayer != null && ZNet.instance != null)
            {
                if (ZNet.instance.GetTimeSeconds() <= localPlayer.m_wakeupTime + __instance.m_sleepCooldownSeconds)
                {
                    __result = false;
                    return false;
                }
            }

            __result = true;
            return false;
        }
    }
}
