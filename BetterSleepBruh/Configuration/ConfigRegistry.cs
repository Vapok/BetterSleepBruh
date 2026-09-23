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
        internal static ConfigEntry<bool> ShowSplashOnStartup;
        internal static ConfigEntry<bool> EnableTelemetry;


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
                    "When enabled, Fake Total Players and Simulate Players In Bed are added on top of real player counts for boost math and HUD (server + RPC).",
                    null,
                    new ConfigurationManagerAttributes { Order = 4, IsAdminOnly = true }),
                ref TestingMode);

            SyncedConfig("Testing Mode", "Fake Total Players", 10,
                new ConfigDescription(
                    "Count of fake connected players to add to the server while Testing Mode is on.",
                    new AcceptableValueRange<int>(0, 80), 
                    new ConfigurationManagerAttributes { Order = 5, IsAdminOnly = true }),ref TestingMaxPlayers);

            SyncedConfig("Testing Mode", "Simulate Players In Bed", 1,
                new ConfigDescription(
                    "Count of fake players in bed to add while Testing Mode is on (clamped to Fake Total Players).",
                    new AcceptableValueRange<int>(0, 80), 
                    new ConfigurationManagerAttributes { Order = 6, IsAdminOnly = true}),ref TestingSleepingPlayers);

            //Local Configs
            UnsyncedConfig("Local Config", "Show Splash on Startup", true,
                new ConfigDescription("If enabled, displays the mod overview and links splash screen on game startup.",
                    null, new ConfigurationManagerAttributes { Order = 4 }), ref ShowSplashOnStartup);

            UnsyncedConfig("Local Config", "Enable Anonymous Telemetry", true,
                new ConfigDescription("If enabled, sends anonymous mod launch and heartbeat telemetry to help improve mod stability and track active versions.",
                    null, new ConfigurationManagerAttributes { Order = 5 }), ref EnableTelemetry);
        }

        public static int GetEffectiveTotalPlayersForMod(int realTotalPlayers)
        {
            int total = Math.Max(0, realTotalPlayers);
            if (TestingMode != null && TestingMode.Value && TestingMaxPlayers != null)
                total += Math.Max(0, TestingMaxPlayers.Value);
            return total;
        }

        public static int GetEffectiveSleepingPlayersForMod(int realSleepingCount, int effectivePlayerCount)
        {
            int sleeping = Math.Max(0, realSleepingCount);
            if (TestingMode != null && TestingMode.Value && TestingSleepingPlayers != null)
            {
                int maxFakeSleep = TestingMaxPlayers != null ? Math.Max(0, TestingMaxPlayers.Value) : 0;
                int fakeSleeping = ClampInt(TestingSleepingPlayers.Value, 0, maxFakeSleep);
                sleeping += fakeSleeping;
            }
            return ClampInt(sleeping, 0, effectivePlayerCount);
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