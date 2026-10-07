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
        public static ConfigEntry<bool> ProtectCookingTimers;
        public static ConfigEntry<bool> ProtectSmelterTimers;
        public static ConfigEntry<bool> ProtectCropTimers;
        public static ConfigEntry<bool> ProtectMeadTimers;
        public static ConfigEntry<bool> ProtectPickableTimers;
        public static ConfigEntry<bool> DecoupleWaterWaveSpeed;
        public static ConfigEntry<bool> CalmOceanDuringSleep;
        public static ConfigEntry<float> OceanCalmMultiplier;
        public static ConfigEntry<bool> PreventBoatSleepImpactDamage;
        public static ConfigEntry<bool> SuppressBoatImpactScreenShake;


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

            SyncedConfig("Station Timers", "Protect Cooking Timers", true,
                new ConfigDescription("If enabled, food on cooking stations and in ovens will not cook or burn faster during partial sleep.",
                    null,
                    new ConfigurationManagerAttributes { Order = 1, IsAdminOnly = true }),
                ref ProtectCookingTimers);

            SyncedConfig("Station Timers", "Protect Smelter Timers", true,
                new ConfigDescription("If enabled, smelters, blast furnaces, and kilns process at normal speed during partial sleep.",
                    null,
                    new ConfigurationManagerAttributes { Order = 2, IsAdminOnly = true }),
                ref ProtectSmelterTimers);

            SyncedConfig("Station Timers", "Protect Crop Timers", true,
                new ConfigDescription("If enabled, crops and planted saplings grow at normal speed during partial sleep.",
                    null,
                    new ConfigurationManagerAttributes { Order = 3, IsAdminOnly = true }),
                ref ProtectCropTimers);

            SyncedConfig("Station Timers", "Protect Mead Timers", true,
                new ConfigDescription("If enabled, fermenters and mead barrels ferment at normal speed during partial sleep.",
                    null,
                    new ConfigurationManagerAttributes { Order = 4, IsAdminOnly = true }),
                ref ProtectMeadTimers);

            SyncedConfig("Station Timers", "Protect Pickable Timers", true,
                new ConfigDescription("If enabled, berry bushes and wild pickables respawn at normal speed during partial sleep.",
                    null,
                    new ConfigurationManagerAttributes { Order = 5, IsAdminOnly = true }),
                ref ProtectPickableTimers);

            SyncedConfig("Ocean & Boat Physics", "Decouple Water Wave Speed", true,
                new ConfigDescription("If enabled, ocean wave simulation moves at normal real-time speed while time is accelerated, preventing violent wave physics.",
                    null,
                    new ConfigurationManagerAttributes { Order = 1, IsAdminOnly = true }),
                ref DecoupleWaterWaveSpeed);

            SyncedConfig("Ocean & Boat Physics", "Calm Ocean During Sleep", true,
                new ConfigDescription("If enabled, ocean waves are calmed and smoothed while night is accelerated.",
                    null,
                    new ConfigurationManagerAttributes { Order = 2, IsAdminOnly = true }),
                ref CalmOceanDuringSleep);

            SyncedConfig("Ocean & Boat Physics", "Ocean Calm Multiplier", 0.5f,
                new ConfigDescription("Wave height multiplier applied when ocean calming is active (0.0 = completely flat, 1.0 = normal storm waves).",
                    new AcceptableValueRange<float>(0f, 1f),
                    new ConfigurationManagerAttributes { Order = 3, IsAdminOnly = true }),
                ref OceanCalmMultiplier);

            SyncedConfig("Ocean & Boat Physics", "Prevent Boat Sleep Impact Damage", true,
                new ConfigDescription("Prevents boats from taking spurious water impact damage from waves during accelerated sleep.",
                    null,
                    new ConfigurationManagerAttributes { Order = 4, IsAdminOnly = true }),
                ref PreventBoatSleepImpactDamage);

            SyncedConfig("Ocean & Boat Physics", "Suppress Boat Impact Screen Shake", true,
                new ConfigDescription("Prevents camera screen shake caused by high-velocity wave wakes during accelerated sleep.",
                    null,
                    new ConfigurationManagerAttributes { Order = 5, IsAdminOnly = true }),
                ref SuppressBoatImpactScreenShake);

            //Local Configs
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