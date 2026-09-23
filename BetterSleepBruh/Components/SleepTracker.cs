using System;
using System.Collections.Generic;
using BetterSleepBruh.Configuration;
using UnityEngine;

namespace BetterSleepBruh.Components;

public class SleepTracker : MonoBehaviour
{
    public static SleepTracker Instance { get; private set; }
    public bool CanSleep { get; private set; }
    public bool Enabled = true;

    public static double CurrentExtraRate { get; private set; }
    public static int CurrentPlayerCount { get; private set; }
    public static int CurrentSleepingCount { get; private set; }
    public static bool AllPlayersSleeping => CurrentPlayerCount > 0 && CurrentSleepingCount >= CurrentPlayerCount;

    public static double LastPartialSleepExtraRate => CurrentExtraRate;

    private bool _lastCanSleep;
    private int _lastBroadcastTotal = -1;
    private int _lastBroadcastSleeping = -1;
    private double _lastBroadcastExtraRate = -1.0;

    private static bool IsCharacterInBedForBoost(ZDO zdo)
    {
        return zdo != null && zdo.IsValid() && zdo.GetBool(ZDOVars.s_inBed);
    }

    public static void GetSleepOccupancyCounts(out int playerCount, out int playersSleeping)
    {
        playerCount = 0;
        playersSleeping = 0;
        ZNet znet = ZNet.instance;
        if (znet == null)
            return;

        List<ZDO> zdos = znet.GetAllCharacterZDOS();
        int sessionPlayers = znet.GetNrOfPlayers();
        int zdosCount = zdos != null ? zdos.Count : 0;
        int realTotal = Math.Max(zdosCount, sessionPlayers);
        playerCount = ConfigRegistry.GetEffectiveTotalPlayersForMod(realTotal);

        int realSleeping = 0;
        if (zdos != null)
        {
            for (int i = 0; i < zdos.Count; i++)
            {
                ZDO z = zdos[i];
                if (z != null && IsCharacterInBedForBoost(z))
                    realSleeping++;
            }
        }

        playersSleeping = ConfigRegistry.GetEffectiveSleepingPlayersForMod(realSleeping, playerCount);
    }

    private static double ComputeExtraRateForPartialBoost(int playerCount, int playersSleeping)
    {
        if (playerCount <= 1)
            return 0.0;
        if (playersSleeping <= 0 || playersSleeping >= playerCount)
            return 0.0;
        double sleepFraction = playersSleeping / (double)(playerCount - 1);
        return ConfigRegistry.BonusMultiplier.Value * sleepFraction * ConfigRegistry.BonusIncrementScale.Value;
    }

    public static double GetNextMorningCapSeconds()
    {
        if (ZNet.instance == null || !ZNet.instance.IsServer() || EnvMan.instance == null)
            return double.PositiveInfinity;

        EnvMan env = EnvMan.instance;
        double timeSeconds = ZNet.instance.GetTimeSeconds();
        double dayLen = env.m_dayLengthSec;
        int day = env.GetDay(timeSeconds - dayLen * 0.150000005960464);
        return env.GetMorningStartSec(day + 1);
    }

    public static double ComputePartialSleepBoost()
    {
        return CurrentExtraRate;
    }

    private void Awake()
    {
        if (ZNet.instance == null || !ZNet.instance.IsServer())
        {
            Destroy(this);
            return;
        }

        Instance = this;
        BetterSleepBruh.Log.Debug("[SERVER] SleepTracker Awakes.");
    }

    private float _lastRpcRecalcTime;

    private void Start()
    {
        BetterSleepBruh.Log.Debug("[SERVER] SleepTracker Start.");
        if (!ZNet.instance.IsServer())
            return;

        _lastCanSleep = EnvMan.CanSleep();
        if (ZRoutedRpc.instance != null)
        {
            ZRoutedRpc.instance.Register(nameof(NotifyBedOccupancyChanged), NotifyBedOccupancyChanged);
            ZRoutedRpc.instance.Register(nameof(RPC_RequestSleepingPlayerInfo), RPC_RequestSleepingPlayerInfo);
        }
        InvokeRepeating(nameof(UpdateSleeping), 1f, 1f);
    }

    public void OnBedOccupancyChanged()
    {
        if (ZNet.instance == null || !ZNet.instance.IsServer())
            return;

        if (!Enabled)
            return;

        RecalculateOccupancy();
    }

    private void NotifyBedOccupancyChanged(long sender)
    {
        ZNet znet = ZNet.instance;
        if (znet == null || !znet.IsServer())
            return;
        
        if (!Enabled)
            return;

        if (sender != ZNet.GetUID() && znet.GetPeer(sender) == null)
            return;

        float now = Time.time;
        if (now - _lastRpcRecalcTime < 0.25f)
            return;

        _lastRpcRecalcTime = now;
        BetterSleepBruh.Log.Debug($"[SERVER] NotifyBedOccupancyChanged Heard from {sender}");
        RecalculateOccupancy();
    }

    private void RPC_RequestSleepingPlayerInfo(long sender)
    {
        ZNet znet = ZNet.instance;
        if (znet == null || !znet.IsServer())
            return;

        if (sender != ZNet.GetUID() && znet.GetPeer(sender) == null)
            return;

        if (ZRoutedRpc.instance != null)
        {
            ZRoutedRpc.instance.InvokeRoutedRPC(sender,
                "RPC_SleepingPlayerInfo",
                CurrentPlayerCount,
                CurrentSleepingCount,
                CurrentExtraRate);
        }
    }

    public void RecalculateOccupancy(bool forceBroadcast = false)
    {
        if (ZNet.instance == null || !ZNet.instance.IsServer())
        {
            CurrentPlayerCount = 0;
            CurrentSleepingCount = 0;
            CurrentExtraRate = 0.0;
            return;
        }

        GetSleepOccupancyCounts(out int playerCount, out int playersSleeping);
        CurrentPlayerCount = playerCount;
        CurrentSleepingCount = playersSleeping;

        bool canSleepNow = EnvMan.instance != null && EnvMan.CanSleep() && !EnvMan.instance.IsTimeSkipping();
        CurrentExtraRate = canSleepNow ? ComputeExtraRateForPartialBoost(playerCount, playersSleeping) : 0.0;

        bool stateChanged = forceBroadcast || 
                            playerCount != _lastBroadcastTotal || 
                            playersSleeping != _lastBroadcastSleeping || 
                            Math.Abs(CurrentExtraRate - _lastBroadcastExtraRate) > 0.001;

        if (stateChanged)
        {
            BroadcastSleepingInfoNow();
        }
    }

    private int _heartbeatCounter;

    private void UpdateSleeping()
    {
        if (ZNet.instance == null || !ZNet.instance.IsServer())
            return;

        CanSleep = EnvMan.CanSleep();

        if (CanSleep)
        {
            _heartbeatCounter++;
            bool forceHeartbeat = _heartbeatCounter >= 3;
            if (forceHeartbeat)
                _heartbeatCounter = 0;

            RecalculateOccupancy(forceBroadcast: forceHeartbeat);
        }
        else
        {
            RecalculateOccupancy();
        }

        if (CanSleep && !_lastCanSleep)
        {
            if (ZRoutedRpc.instance != null)
            {
                ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, "RPC_StartSleep");
            }
            RecalculateOccupancy(forceBroadcast: true);
        }

        if (!CanSleep && _lastCanSleep)
        {
            if (EnvMan.instance == null || !EnvMan.instance.IsTimeSkipping())
            {
                if (ZRoutedRpc.instance != null)
                {
                    ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, "RPC_StopSleep");
                }
            }
            _lastBroadcastTotal = -1;
            _lastBroadcastSleeping = -1;
            _lastBroadcastExtraRate = -1.0;
        }

        _lastCanSleep = CanSleep;
    }

    private void BroadcastSleepingInfoNow()
    {
        if (ZNet.instance == null || !ZNet.instance.IsServer())
            return;

        _lastBroadcastTotal = CurrentPlayerCount;
        _lastBroadcastSleeping = CurrentSleepingCount;
        _lastBroadcastExtraRate = CurrentExtraRate;

        if (ConfigRegistry.IsPlayerCountTestingActive)
            BetterSleepBruh.Log.Debug($"[BetterSleepBruh TESTING] broadcast total={CurrentPlayerCount} sleeping={CurrentSleepingCount} extraRate={CurrentExtraRate}");
        else
            BetterSleepBruh.Log.Debug($"[SERVER] Player Sleeping Info: Players on Server: {CurrentPlayerCount} Players Sleeping: {CurrentSleepingCount} Extra rate: {CurrentExtraRate}");

        if (ZRoutedRpc.instance != null)
        {
            ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody,
                "RPC_SleepingPlayerInfo",
                CurrentPlayerCount,
                CurrentSleepingCount,
                CurrentExtraRate);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }
}
