using System.Linq;
using BetterSleepBruh.Configuration;
using UnityEngine;

namespace BetterSleepBruh.Components;

public class SleepTracker : MonoBehaviour
{
    public static SleepTracker Instance { get; private set; }
    public bool CanSleep { get; private set; }
    public bool Enabled = true;
    
    private bool _lastCanSleep;
    private const double VanillaSkipDurationRealSec = 12.0;

    
    /******************
     * STATIC METHODS
     ******************/
    private static bool IsCharacterInBedForBoost(ZDO zdo)
    {
        foreach (var player in Player.GetAllPlayers())
        {
            if (player == null)
                continue;
            var nview = player.GetComponent<ZNetView>();
            if (nview == null)
                continue;
            var pzdo = nview.GetZDO();
            if (pzdo == null)
                continue;
            if (ReferenceEquals(zdo, pzdo) || zdo.m_uid == pzdo.m_uid)
                return player.InBed();
        }

        return zdo.GetBool(ZDOVars.s_inBed);
    }

    private static double ComputeVanillaMorningSkipRate()
    {
        if (ZNet.instance == null || !ZNet.instance.IsServer())
            return 0.0;
        if (EnvMan.instance == null)
            return 0.0;

        var env = EnvMan.instance;
        var timeSeconds = ZNet.instance.GetTimeSeconds();
        var dayLen = env.m_dayLengthSec;
        var day = env.GetDay(timeSeconds - dayLen * 0.150000005960464);
        var morningStartSec = env.GetMorningStartSec(day + 1);
        var sleepRate = (morningStartSec - timeSeconds) / VanillaSkipDurationRealSec;
        return sleepRate > 0.0 ? sleepRate : 0.0;
    }

    public static double GetNextMorningCapSeconds()
    {
        if (ZNet.instance == null || EnvMan.instance == null)
            return double.PositiveInfinity;

        var env = EnvMan.instance;
        var timeSeconds = ZNet.instance.GetTimeSeconds();
        var dayLen = env.m_dayLengthSec;
        var day = env.GetDay(timeSeconds - dayLen * 0.150000005960464);
        return env.GetMorningStartSec(day + 1);
    }

    public static double ComputePartialSleepBoost()
    {
        if (ZNet.instance == null || !ZNet.instance.IsServer())
            return 0.0;
        if (EnvMan.instance == null || !EnvMan.CanSleep())
            return 0.0;
        if (EnvMan.instance.IsTimeSkipping())
            return 0.0;

        var players = ZNet.instance.GetAllCharacterZDOS();
        var playerCount = players.Count;
        if (playerCount <= 1)
            return 0.0;

        var playersSleeping = players.Count(IsCharacterInBedForBoost);
        if (playersSleeping <= 0 || playersSleeping >= playerCount)
            return 0.0;

        var liveVanillaSkipRate = ComputeVanillaMorningSkipRate();
        if (liveVanillaSkipRate <= 0.0)
            return 0.0;

        var sleepFraction = playersSleeping / (double)(playerCount - 1);
        return ConfigRegistry.BonusMultiplier.Value * liveVanillaSkipRate * sleepFraction;
    }

    public static double GetHudBoostDisplayPercent(int playerCount, int playersSleeping)
    {
        if (playerCount <= 1)
            return 0.0;
        if (playersSleeping <= 0 || playersSleeping >= playerCount)
            return 0.0;
        return ConfigRegistry.BonusMultiplier.Value * 100.0;
    }

    
    /******************
     * PRIVATE METHODS
     ******************/

    private void Awake()
    {
        Instance = this;
        BetterSleepBruh.Log.Debug($"[SERVER] SleepTracker Awakes.");

    }

    private void Start()
    {
        BetterSleepBruh.Log.Debug($"[SERVER] SleepTracker Start.");
        if (!ZNet.instance.IsServer())
            return;

        _lastCanSleep = EnvMan.CanSleep();
        InvokeRepeating(nameof(UpdateSleeping), 1f, 1f);

    }
    
    private void UpdateSleeping()
    {
        if (!ZNet.instance.IsServer())
            return;

        CanSleep = EnvMan.CanSleep();

        BetterSleepBruh.Log.Info($"[SERVER] CanSleep: {CanSleep}");
        BetterSleepBruh.Log.Info($"[SERVER] EnvMan IsTimeSkipping: {EnvMan.instance != null && EnvMan.instance.IsTimeSkipping()}");

        if (CanSleep)
            UpdatePlayerSleepingInfo();

        if (CanSleep & !_lastCanSleep)
            ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody,"RPC_StartSleep");

        if (!CanSleep & _lastCanSleep)
            ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody,"RPC_StopSleep");

        _lastCanSleep = CanSleep;
    }


    private void UpdatePlayerSleepingInfo()
    {
        if (!ZNet.instance.IsServer())
            return;

        var allCharacterZdos = ZNet.instance.GetAllCharacterZDOS();
        var playersOnServer = allCharacterZdos.Count;
        var playersSleeping = allCharacterZdos.Count(x => x.GetBool(ZDOVars.s_inBed));

        ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody,
            "RPC_SleepingPlayerInfo",
            playersOnServer,
            playersSleeping,
            ComputePartialSleepBoost());
    }
}
