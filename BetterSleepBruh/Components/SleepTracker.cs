using BetterSleepBruh.Configuration;
using UnityEngine;

namespace BetterSleepBruh.Components;

/*
* Server-only: tracks sleep window state and broadcasts occupancy / boost to clients.
* World time is advanced only on the server (Harmony postfix on ZNet.UpdateNetTime). Never add this to client peers.
*/

public class SleepTracker : MonoBehaviour
{
    public static SleepTracker Instance { get; private set; }
    public bool CanSleep { get; private set; }
    public bool Enabled = true;

    public static double LastPartialSleepExtraRate { get; private set; }

    private bool _lastCanSleep;

    
    /******************
     * STATIC METHODS
     ******************/
    private static bool IsCharacterInBedForBoost(ZDO zdo)
    {
        return zdo.GetBool(ZDOVars.s_inBed);
    }

    private static void GetSleepOccupancyCounts(out int playerCount, out int playersSleeping)
    {
        playerCount = 0;
        playersSleeping = 0;
        var znet = ZNet.instance;
        if (znet == null)
            return;

        var zdos = znet.GetAllCharacterZDOS();
        var sessionPlayers = znet.GetNrOfPlayers();
        playerCount = System.Math.Max(zdos.Count, sessionPlayers);
        foreach (var z in zdos)
        {
            if (IsCharacterInBedForBoost(z))
                playersSleeping++;
        }
    }

    private static double ComputeExtraRateForPartialBoost(int playerCount, int playersSleeping)
    {
        if (playerCount <= 1)
            return 0.0;
        if (playersSleeping <= 0 || playersSleeping >= playerCount)
            return 0.0;
        var sleepFraction = playersSleeping / (double)(playerCount - 1);
        return ConfigRegistry.BonusMultiplier.Value * sleepFraction;
    }

    public static double GetNextMorningCapSeconds()
    {
        if (ZNet.instance == null || !ZNet.instance.IsServer() || EnvMan.instance == null)
            return double.PositiveInfinity;

        var env = EnvMan.instance;
        var timeSeconds = ZNet.instance.GetTimeSeconds();
        var dayLen = env.m_dayLengthSec;
        var day = env.GetDay(timeSeconds - dayLen * 0.150000005960464);
        return env.GetMorningStartSec(day + 1);
    }

    /*
    * Extra game-time per real second (additive after vanilla m_netTime += dt).
    * sleepFraction = playersSleeping / (playerCount - 1) with
    * playerCount = max(characterZdos, GetNrOfPlayers()).
    */
    public static double ComputePartialSleepBoost()
    {
        if (ZNet.instance == null || !ZNet.instance.IsServer())
        {
            LastPartialSleepExtraRate = 0.0;
            return 0.0;
        }

        if (EnvMan.instance == null || !EnvMan.CanSleep() || EnvMan.instance.IsTimeSkipping())
        {
            LastPartialSleepExtraRate = 0.0;
            return 0.0;
        }

        GetSleepOccupancyCounts(out var playerCount, out var playersSleeping);
        LastPartialSleepExtraRate = ComputeExtraRateForPartialBoost(playerCount, playersSleeping);
        return LastPartialSleepExtraRate;
    }
    
    /******************
     * PRIVATE METHODS
     ******************/

    private void Awake()
    {
        if (ZNet.instance == null || !ZNet.instance.IsServer())
        {
            Destroy(this);
            return;
        }

        Instance = this;
        BetterSleepBruh.Log.Debug($"[SERVER] SleepTracker Awakes.");

    }

    private void Start()
    {
        BetterSleepBruh.Log.Debug($"[SERVER] SleepTracker Start.");
        if (!ZNet.instance.IsServer())
            return;

        _lastCanSleep = EnvMan.CanSleep();
        ZRoutedRpc.instance.Register(nameof(NotifyBedOccupancyChanged), NotifyBedOccupancyChanged);
        InvokeRepeating(nameof(UpdateSleeping), 1f, 1f);

    }
    
    private void UpdateSleeping()
    {
        if (!ZNet.instance.IsServer())
            return;

        CanSleep = EnvMan.CanSleep();

        BetterSleepBruh.Log.Info($"[SERVER] CanSleep: {CanSleep}");
        BetterSleepBruh.Log.Info($"[SERVER] lastCanSleep: {_lastCanSleep}");
        BetterSleepBruh.Log.Info($"[SERVER] EnvMan IsTimeSkipping: {EnvMan.instance != null && EnvMan.instance.IsTimeSkipping()}");

        if (CanSleep)
            BroadcastSleepingInfoNow();

        if (CanSleep & !_lastCanSleep)
            ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody,"RPC_StartSleep");

        if (!CanSleep & _lastCanSleep)
            ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody,"RPC_StopSleep");

        _lastCanSleep = CanSleep;
    }

    // Server-only hook: rebroadcasts HUD payload immediately when occupancy changes.
    private void NotifyBedOccupancyChanged(long sender)
    {
        if (ZNet.instance == null || !ZNet.instance.IsServer())
            return;
        
        if (Instance == null || !Instance.Enabled)
            return;
        
        BetterSleepBruh.Log.Debug($"[SERVER] NotifyBedOccupancyChanged Heard from {sender}");
        
        if (EnvMan.instance != null && EnvMan.CanSleep())
            BroadcastSleepingInfoNow();
    }

    private void BroadcastSleepingInfoNow()
    {
        if (!ZNet.instance.IsServer())
            return;

        GetSleepOccupancyCounts(out var playersOnServer, out var playersSleeping);
        var boost = ComputePartialSleepBoost();

        BetterSleepBruh.Log.Debug($"[SERVER] Player Sleeping Info: Players on Server: {playersOnServer} Players Sleeping: {playersSleeping} Extra rate: {boost}");

        ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody,
            "RPC_SleepingPlayerInfo",
            playersOnServer,
            playersSleeping,
            boost);
    }
}
