using System;
using BepInEx.Configuration;
using Vapok.Common.Abstractions;
using Vapok.Common.Managers.Configuration;

namespace BetterSleepBruh.Configuration
{
    public class ConfigRegistry : ConfigSyncBase
    {
        //Configuration Entry Privates
        
        public static Waiting Waiter;
        public static ConfigEntry<bool> UseVanilleSleep;
        public static ConfigEntry<bool> TestingMode;
        public static ConfigEntry<int> TestingMaxPlayers;
        public static ConfigEntry<int> TestingSleepingPlayers;
        public static ConfigEntry<float> SleepStart;
        public static ConfigEntry<float> BonusMultiplier;
        public static ConfigEntry<float> BonusIncrementScale;
        public static ConfigEntry<float> BoostFadeRealSecondsBeforeMorning;


        public ConfigRegistry(IPluginInfo mod, bool enableLockedConfigs = false): base(mod, enableLockedConfigs)
        {
            //Waiting For Startup
            Waiter = new Waiting();

            InitializeConfigurationSettings();
        }

        public sealed override void InitializeConfigurationSettings()
        {
            if (_config == null)
                return;
            
            //User Configs
            SyncedConfig("Server Settings", "Use Vanilla Sleep Start", false,
                new ConfigDescription("Default is false/disabled; Set to True/Enabled to resume Vanilla Sleep Start",
                    null, 
                    new ConfigurationManagerAttributes { Order = 1, IsAdminOnly = true }),ref UseVanilleSleep);

            SyncedConfig("Server Settings", "Sleep Start", 0.5f,
                new ConfigDescription("Day Fraction to allow sleep to begin. Default is 0.5, or Noon. Only applies when Vanilla Sleep Start is disabled/false.",
                    new AcceptableValueRange<float>(0f, 0.99f), 
                    new ConfigurationManagerAttributes { Order = 2, IsAdminOnly = true }),ref SleepStart);

            SyncedConfig("Server Settings", "Bonus Multiplier", 0.6f,
                new ConfigDescription(
                    "Scales the bonus increment (added on top of normal time): extra rate = this × sleep fraction × 10. At 1.0 and all-but-one in bed, extra rate is 10 (time advances 11× vs vanilla dt alone).",
                    new AcceptableValueRange<float>(0f, 1f), 
                    new ConfigurationManagerAttributes { Order = 3, IsAdminOnly = true }),ref BonusMultiplier);

            SyncedConfig("Server Settings", "Bonus Increment Scale", 20f,
                new ConfigDescription(
                    "Scales the bonus increment (added on top of normal time): extra rate = this × sleep fraction × 10. At 1.0 and all-but-one in bed, extra rate is 10 (time advances 11× vs vanilla dt alone).",
                    new AcceptableValueRange<float>(0f, 30f), 
                    new ConfigurationManagerAttributes { Order = 4, IsAdminOnly = true }),ref BonusIncrementScale);

            SyncedConfig("Server Settings", "Boost Fade (Real Seconds)", 3f,
                new ConfigDescription(
                    "Partial boost linearly ramps to zero over this many real-time seconds before the next morning. Uses net rate (1 + extra) so higher boost = longer game-time taper. 0 = no taper (hard cut only at morning).",
                    new AcceptableValueRange<float>(0f, 30f),
                    new ConfigurationManagerAttributes { Order = 5, IsAdminOnly = true }),
                ref BoostFadeRealSecondsBeforeMorning);

            SyncedConfig("Testing Mode", "Enable Testing Mode", false,
                new ConfigDescription(
                    "When enabled, Fake Total Players and Simulate Players In Bed override real counts for boost math and HUD (server + RPC).",
                    null,
                    new ConfigurationManagerAttributes { Order = 4, IsAdminOnly = true }),
                ref TestingMode);

            SyncedConfig("Testing Mode", "Fake Total Players", 10,
                new ConfigDescription(
                    "Spoofed total player count while Testing Mode is on.",
                    new AcceptableValueRange<int>(2, 80), 
                    new ConfigurationManagerAttributes { Order = 5, IsAdminOnly = true }),ref TestingMaxPlayers);

            SyncedConfig("Testing Mode", "Simulate Players In Bed", 1,
                new ConfigDescription(
                    "Spoofed count of players in bed while Testing Mode is on (clamped to Fake Total Players).",
                    new AcceptableValueRange<int>(0, 80), 
                    new ConfigurationManagerAttributes { Order = 6, IsAdminOnly = true}),ref TestingSleepingPlayers);

        }

        /*
         * When TestingMode is enabled, returns TestingMaxPlayers; otherwise return realTotalPlayers/>.
         * Use for all sleep occupancy totals (boost math, HUD broadcast).
        */
        
        public static int GetEffectiveTotalPlayersForMod(int realTotalPlayers)
        {
            if (TestingMode != null && TestingMode.Value && TestingMaxPlayers != null)
                return TestingMaxPlayers.Value;
            return realTotalPlayers;
        }

        /*
         * When TestingMode is enabled, returns TestingSleepingPlayers(clamped to
         * effectivePlayerCount"); otherwise returns realSleepingCount clamped to that total.
        */
        public static int GetEffectiveSleepingPlayersForMod(int realSleepingCount, int effectivePlayerCount)
        {
            var maxSleep = effectivePlayerCount < 0 ? 0 : effectivePlayerCount;
            if (TestingMode != null && TestingMode.Value && TestingSleepingPlayers != null)
                return ClampInt(TestingSleepingPlayers.Value, 0, maxSleep);
            return ClampInt(realSleepingCount, 0, maxSleep);
        }

        private static int ClampInt(int value, int min, int max)
        {
            if (value < min) return min;
            return value > max ? max : value;
        }

        public static bool IsPlayerCountTestingActive => TestingMode != null && TestingMode.Value;
    }
    
    public class Waiting
    {
        public void ConfigurationComplete(bool configDone)
        {
            if (configDone)
                StatusChanged?.Invoke(this, EventArgs.Empty);
        }
        public event EventHandler StatusChanged;            
    }

}