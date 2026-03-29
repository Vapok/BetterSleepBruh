/* BetterSleepBruh by Vapok */

using System;
using System.Reflection;
using BepInEx;
using BetterSleepBruh.Components;
using BetterSleepBruh.Configuration;
using HarmonyLib;
using JetBrains.Annotations;
using Jotunn.Utils;
using UnityEngine;
using Vapok.Common.Abstractions;
using Vapok.Common.Managers;
using Vapok.Common.Managers.Configuration;
using Vapok.Common.Managers.LocalizationManager;

namespace BetterSleepBruh
{
    [BepInDependency(Jotunn.Main.ModGuid)]
    [BepInDependency("com.ValheimModding.YamlDotNetDetector")]
    [BepInPlugin(_pluginId, _displayName, _version)]
    [SynchronizationMode(AdminOnlyStrictness.IfOnServer)]
    public class BetterSleepBruh : BaseUnityPlugin, IPluginInfo
    {
        //Module Constants
        private const string _pluginId = "vapok.mods.BetterSleepBruh";
        private const string _displayName = "Better Sleep Bruh!";
        private const string _version = "1.0.1";
        
        private SleepHudView _sleepHud;
        private int _sleepHudBuildAttempts;

        
        //Interface Properties
        public string PluginId => _pluginId;
        public string DisplayName => _displayName;
        public string Version => _version;
        public BaseUnityPlugin Instance => _instance;
        
        //Class Properties
        public static ILogIt Log => _log;
        public static bool ValheimAwake;
        public static Waiting Waiter;
        
        //Class Privates
        private static BetterSleepBruh _instance;
        private static ConfigSyncBase _config;
        private static ILogIt _log;
        private Harmony _harmony;
        
        [UsedImplicitly]
        // This the main function of the mod. BepInEx will call this.
        private void Awake()
        {
            //I'm awake!
            _instance = this;
            
            //Waiting For Startup
            Waiter = new Waiting();
            
            //Jotunn Localization
            var localization = Jotunn.Managers.LocalizationManager.Instance.GetLocalization();

            //Register Logger
            LogManager.Init(PluginId,out _log);
            
            //Initialize Managers
            Localizer.Init(localization);

            //Register Configuration Settings
            _config = new ConfigRegistry(_instance);

            Localizer.Waiter.StatusChanged += InitializeModule;
            
            //Patch Harmony
            _harmony = new Harmony(Info.Metadata.GUID);
            _harmony.PatchAll(Assembly.GetExecutingAssembly());

            //???

            //Profit
        }

        private void Start()
        {
            InvokeRepeating(nameof(WaitForGame), 1f, 1f);
            InvokeRepeating(nameof(WaitForZNet), 1f, 1f);
            InvokeRepeating(nameof(TryBuildSleepHud), 0f, 0.25f);
        }
        private void WaitForGame()
        {
            if (Game.instance == null)
                return;

            CancelInvoke(nameof(WaitForGame));
        }

        private void WaitForZNet()
        {
            if (ZNet.instance == null)
                return;

            CancelInvoke(nameof(WaitForZNet));
            
            if (!ZNet.instance.IsServer())
                return;
            
            Game.instance.gameObject.AddComponent<SleepTracker>();
        }

        private void TryBuildSleepHud()
        {
            if (_sleepHud != null)
            {
                CancelInvoke(nameof(TryBuildSleepHud));
                return;
            }

            if (_sleepHudBuildAttempts++ > 120)
            {
                CancelInvoke(nameof(TryBuildSleepHud));
                BetterSleepBruh.Log.Warning("[CLIENT] Gave up building sleep HUD: minimap hierarchy not found in time.");
                return;
            }

            BuildSleepGui();
            if (_sleepHud != null)
                CancelInvoke(nameof(TryBuildSleepHud));
        }

        private void BuildSleepGui()
        {
            var goGameMain = GameObject.Find("_GameMain");
            if (goGameMain == null)
            {
                BetterSleepBruh.Log.Debug($"[CLIENT] Can't Find _GameMain");
                return;
            }
            
            var goHud = goGameMain.transform.Find("LoadingGUI/PixelFix/IngameGui/HUD/hudroot");
            if (goHud == null)
            {
                BetterSleepBruh.Log.Debug($"[CLIENT] Can't Find HUD");
                return;
            }

            var goMiniMap = goHud.Find("MiniMap/small");
            if (goMiniMap == null)
            {
                BetterSleepBruh.Log.Debug($"[CLIENT] Can't Find Minimap");
                return;
            }

            _sleepHud = SleepHudView.TryCreate(goMiniMap);

            if (_sleepHud != null)
            {
                _sleepHud.gameObject.SetActive(false);
                return;
            }

            BetterSleepBruh.Log.Warning($"[CLIENT] Can't Build SleepHud");
        }
        
        private void Update()
        {
            if (!Player.m_localPlayer || !ZNetScene.instance)
                return;
        }

        public void InitializeModule(object send, EventArgs args)
        {
            if (ValheimAwake)
                return;
            
            ConfigRegistry.Waiter.ConfigurationComplete(true);

            ValheimAwake = true;
        }
        
        private void OnDestroy()
        {
            _instance = null;
        }

        public class Waiting
        {
            public void ValheimIsAwake(bool awakeFlag)
            {
                if (awakeFlag)
                    StatusChanged?.Invoke(this, EventArgs.Empty);
            }
            public event EventHandler StatusChanged;            
        }
    }
}