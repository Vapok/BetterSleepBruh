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
        public static ConfigEntry<float> SleepStart;
        public static ConfigEntry<float> BonusMultiplier;

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
                    new ConfigurationManagerAttributes { Order = 1 }),ref UseVanilleSleep);

            SyncedConfig("Server Settings", "Sleep Start", 0.5f,
                new ConfigDescription("Day Fraction to allow sleep to begin. Default is 0.5, or Noon. Only applies when Vanilla Sleep Start is disabled/false.",
                    new AcceptableValueRange<float>(0f, 0.99f), 
                    new ConfigurationManagerAttributes { Order = 2 }),ref SleepStart);

            SyncedConfig("Server Settings", "Bonus Multiplier", 0.6f,
                new ConfigDescription(
                    "Maximum extra night speed when everyone but one player is in bed: effective rate is 1 + (this × sleep fraction) game-seconds per real second (e.g. 0.6 and all-but-one sleeping ⇒ 1.6×).",
                    new AcceptableValueRange<float>(0f, 1f), 
                    new ConfigurationManagerAttributes { Order = 3 }),ref BonusMultiplier);

        }
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