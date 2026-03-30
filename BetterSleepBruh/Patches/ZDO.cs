using System.Collections.Generic;
using System.Runtime.CompilerServices;
using BetterSleepBruh.Components;
using HarmonyLib;

namespace BetterSleepBruh.Patches;

public class ZDOPatches
{
    [HarmonyPatch(typeof(ZDO), nameof(ZDO.Set),new []{typeof(int), typeof(bool)})]
    static class ZDOSetPatch
    {
        private static readonly Dictionary<ZDOID,bool> LastValue = new();
        
        static void Prefix(ZDO __instance, int hash, bool value)
        {
            if (hash != ZDOVars.s_inBed) return;
            BetterSleepBruh.Log.Debug($"{__instance.m_uid}: Previous inBed: {__instance.GetBool(hash)}");
            LastValue.Add(__instance.m_uid,__instance.GetBool(hash));
        }
        static void Postfix(ZDO __instance, int hash, bool value)
        {
            if (hash != ZDOVars.s_inBed) return;
            
            if (LastValue[__instance.m_uid] != value)
                ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody,"NotifyBedOccupancyChanged");
            
            BetterSleepBruh.Log.Debug($"{__instance.m_uid}: LastValue: {LastValue[__instance.m_uid]} New inBed: {value}");
            LastValue.Remove(__instance.m_uid);
        }
    }
}