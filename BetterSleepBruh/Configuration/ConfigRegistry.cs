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
            SyncedConfig("Server Settings", "Sleep Start", 0.5f,
                new ConfigDescription("Day Fraction to allow sleep to begin. Default is 0.5, or Noon",
                    new AcceptableValueRange<float>(0f, 0.99f), 
                    new ConfigurationManagerAttributes { Order = 1 }),ref SleepStart);

            SyncedConfig("Server Settings", "Bonus Multiplier", 0.6f,
                new ConfigDescription("Maximum benefit of Time Boost if everyone but 1 person is in a bed. Default 60%",
                    new AcceptableValueRange<float>(0f, 1f), 
                    new ConfigurationManagerAttributes { Order = 1 }),ref BonusMultiplier);

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