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
        private bool _zNetHasStopped;

        
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
            ConfigRegistry.Waiter.ConfigurationComplete(true);
            InvokeRepeating(nameof(WaitForGame), 1f, 1f);
            InvokeRepeating(nameof(WaitForZNet), 1f, 1f);
        }

        private void WaitForGame()
        {
            if (Game.instance == null)
                return;

            CancelInvoke(nameof(WaitForGame));
            
            InvokeRepeating(nameof(TryBuildSleepHud), 0f, 0.25f);
        }

        private void WaitForZNet()
        {
            if (ZNet.instance == null || _zNetHasStopped)
                return;

            CancelInvoke(nameof(WaitForZNet));
            
            if (!ZNet.instance.IsServer())
                return;
            Game.instance.gameObject.AddComponent<SleepTracker>();
        }

        private void TryBuildSleepHud()
        {
            Log.Debug($"Waiting for GuiBuild");
            if (_sleepHud != null)
            {
                CancelInvoke(nameof(TryBuildSleepHud));
                return;
            }

            Log.Debug($"[GuiBuild] Checking for Server...");
            // No need to build the UI if this if this is a dedicated server
            if (ZNet.instance != null && ZNet.instance.IsDedicated())
            {
                CancelInvoke(nameof(TryBuildSleepHud));
                return;
            }

            Log.Debug($"[GuiBuild] Checking for previous ZNetShutdown {_zNetHasStopped}...");
            if (_zNetHasStopped) return;
            
            Log.Debug($"[GuiBuild] Checking for Player...");
            if (Player.m_localPlayer == null) return;
            
            Log.Debug($"[GuiBuild] Checking Attempts...");
            if (_sleepHudBuildAttempts++ > 120)
            {
                CancelInvoke(nameof(TryBuildSleepHud));
                Log.Warning("[GuiBuild] Gave up building sleep HUD: minimap hierarchy not found in time.");
                return;
            }

            Log.Debug($"[GuiBuild] Building GUI");
            BuildSleepGui();
            if (_sleepHud != null)
                CancelInvoke(nameof(TryBuildSleepHud));
            Log.Debug($"[GuiBuild] SleepHud is built: {_sleepHud != null}");
        }

        private void BuildSleepGui()
        {
            var goGameMain = GameObject.Find("_GameMain");
            if (goGameMain == null)
            {
                Log.Debug($"[GuiBuild] Can't Find _GameMain");
                return;
            }
            
            var goHud = goGameMain.transform.Find("LoadingGUI/PixelFix/IngameGui/HUD/hudroot");
            if (goHud == null)
            {
                Log.Debug($"[GuiBuild] Can't Find HUD");
                return;
            }

            var goMiniMap = goHud.Find("MiniMap/small");
            if (goMiniMap == null)
            {
                Log.Debug($"[GuiBuild] Can't Find Minimap");
                return;
            }

            _sleepHud = SleepHudView.TryCreate(goMiniMap);

            if (_sleepHud != null)
            {
                _sleepHud.gameObject.SetActive(EnvMan.CanSleep());
                return;
            }

            Log.Warning($"[GuiBuild] Can't Build SleepHud");
        }
        
        private void Update()
        {
            if (!Player.m_localPlayer || !ZNetScene.instance || Game.instance == null)
                return;
            
            _zNetHasStopped = ZNet.instance.HaveStopped;

            if (!_zNetHasStopped) return;
            if (_sleepHud != null)
            {
                InvokeRepeating(nameof(WaitForZNet), 1f, 1f);
                InvokeRepeating(nameof(TryBuildSleepHud), 0f, 0.25f);
                _sleepHud = null;
            }
        }

        public void InitializeModule(object send, EventArgs args)
        {
            if (ValheimAwake)
                return;

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