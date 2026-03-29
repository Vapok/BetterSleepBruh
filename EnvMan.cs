// Decompiled with JetBrains decompiler
// Type: EnvMan
// Assembly: assembly_valheim, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: E4CFC702-61AB-46D1-9F39-CC2DEB9BC839
// Assembly location: G:\Steam\steamapps\common\Valheim\valheim_Data\Managed\assembly_valheim.dll

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityStandardAssets.ImageEffects;

public class EnvMan : MonoBehaviour
{
  private static int s_lastFrame = int.MaxValue;
  private static EnvMan s_instance;
  private static bool s_isDay;
  private static bool s_isDaylight;
  private static bool s_isAfternoon;
  private static bool s_isCold;
  private static bool s_isFreezing;
  private static bool s_isNight;
  private static bool s_isWet;
  private static bool s_canSleep;
  public Light m_dirLight;
  public bool m_debugTimeOfDay;
  [Range(0.0f, 1f)]
  public float m_debugTime = 0.5f;
  public string m_debugEnv = "";
  public bool m_debugWind;
  [Range(0.0f, 360f)]
  public float m_debugWindAngle;
  [Range(0.0f, 1f)]
  public float m_debugWindIntensity = 1f;
  public float m_sunHorizonTransitionH = 0.08f;
  public float m_sunHorizonTransitionL = 0.02f;
  public long m_dayLengthSec = 1200;
  public float m_transitionDuration = 2f;
  public long m_environmentDuration = 20;
  public long m_windPeriodDuration = 10;
  public float m_windTransitionDuration = 5f;
  public List<EnvSetup> m_environments = new List<EnvSetup>();
  public List<string> m_interiorBuildingOverrideEnvironments = new List<string>();
  public List<BiomeEnvSetup> m_biomes = new List<BiomeEnvSetup>();
  public string m_introEnvironment = "ThunderStorm";
  public float m_edgeOfWorldWidth = 500f;
  [Header("Music")]
  public float m_randomMusicIntervalMin = 60f;
  public float m_randomMusicIntervalMax = 200f;
  [Header("Other")]
  public MeshRenderer m_clouds;
  public MeshRenderer m_rainClouds;
  public MeshRenderer m_rainCloudsDownside;
  public float m_wetTransitionDuration = 15f;
  public double m_sleepCooldownSeconds = 30.0;
  public float m_oceanLevelEnvCheckAshlandsDeepnorth = 20f;
  private bool m_skipTime;
  private double m_skipToTime;
  private double m_timeSkipSpeed = 1.0;
  private const double c_TimeSkipDuration = 12.0;
  private double m_totalSeconds;
  private float m_smoothDayFraction;
  private Color m_sunFogColor = Color.white;
  private GameObject[] m_currentPSystems;
  private GameObject m_currentEnvObject;
  private const float c_MorningL = 0.15f;
  private Vector4 m_windDir1 = new Vector4(0.0f, 0.0f, -1f, 0.0f);
  private Vector4 m_windDir2 = new Vector4(0.0f, 0.0f, -1f, 0.0f);
  private Vector4 m_wind = new Vector4(0.0f, 0.0f, -1f, 0.0f);
  private float m_windTransitionTimer = -1f;
  private Vector3 m_cloudOffset = Vector3.zero;
  private string m_forceEnv = "";
  private EnvSetup m_currentEnv;
  private EnvSetup m_prevEnv;
  private EnvSetup m_nextEnv;
  private string m_ambientMusic;
  private float m_ambientMusicTimer;
  private Heightmap m_cachedHeightmap;
  private Heightmap.Biome m_currentBiome;
  private bool m_inAshlandsOrDeepnorth;
  private long m_environmentPeriod;
  private float m_transitionTimer;
  private bool m_firstEnv = true;
  private static readonly int s_netRefPos = Shader.PropertyToID("_NetRefPos");
  private static readonly int s_skyboxSunDir = Shader.PropertyToID("_SkyboxSunDir");
  private static readonly int s_sunDir = Shader.PropertyToID("_SunDir");
  private static readonly int s_sunFogColor = Shader.PropertyToID("_SunFogColor");
  private static readonly int s_wet = Shader.PropertyToID("_Wet");
  private static readonly int s_sunColor = Shader.PropertyToID("_SunColor");
  private static readonly int s_ambientColor = Shader.PropertyToID("_AmbientColor");
  private static readonly int s_globalWind1 = Shader.PropertyToID("_GlobalWind1");
  private static readonly int s_globalWind2 = Shader.PropertyToID("_GlobalWind2");
  private static readonly int s_globalWindAlpha = Shader.PropertyToID("_GlobalWindAlpha");
  private static readonly int s_cloudOffset = Shader.PropertyToID("_CloudOffset");
  private static readonly int s_globalWindForce = Shader.PropertyToID("_GlobalWindForce");
  private static readonly int s_rain = Shader.PropertyToID("_Rain");

  public static EnvMan instance => EnvMan.s_instance;

  private void Awake()
  {
    EnvMan.s_instance = this;
    foreach (EnvSetup environment in this.m_environments)
      this.InitializeEnvironment(environment);
    foreach (BiomeEnvSetup biome in this.m_biomes)
      this.InitializeBiomeEnvSetup(biome);
    this.m_currentEnv = this.GetDefaultEnv();
  }

  private void OnDestroy() => EnvMan.s_instance = (EnvMan) null;

  private void InitializeEnvironment(EnvSetup env)
  {
    this.SetParticleArrayEnabled(env.m_psystems, false);
    if (!(bool) (UnityEngine.Object) env.m_envObject)
      return;
    env.m_envObject.SetActive(false);
  }

  private void InitializeBiomeEnvSetup(BiomeEnvSetup biome)
  {
    foreach (EnvEntry environment in biome.m_environments)
      environment.m_env = this.GetEnv(environment.m_environment);
  }

  private void SetParticleArrayEnabled(GameObject[] psystems, bool enabled)
  {
    foreach (GameObject psystem in psystems)
    {
      foreach (ParticleSystem componentsInChild in psystem.GetComponentsInChildren<ParticleSystem>())
        componentsInChild.emission.enabled = enabled;
      MistEmitter componentInChildren = psystem.GetComponentInChildren<MistEmitter>();
      if ((bool) (UnityEngine.Object) componentInChildren)
        componentInChildren.enabled = enabled;
    }
  }

  private float RescaleDayFraction(float fraction)
  {
    fraction = (double) fraction < 0.150000005960464 || (double) fraction > 0.850000023841858 ? ((double) fraction >= 0.5 ? (float) (0.75 + ((double) fraction - 0.850000023841858) / 0.150000005960464 * 0.25) : (float) ((double) fraction / 0.150000005960464 * 0.25)) : (float) (0.25 + ((double) fraction - 0.150000005960464) / 0.699999988079071 * 0.5);
    return fraction;
  }

  private void Update()
  {
    this.m_cloudOffset += EnvMan.instance.GetWindForce() * Time.deltaTime * 0.01f;
    Shader.SetGlobalVector(EnvMan.s_cloudOffset, (Vector4) this.m_cloudOffset);
    Shader.SetGlobalVector(EnvMan.s_netRefPos, (Vector4) ZNet.instance.GetReferencePosition());
  }

  private void FixedUpdate()
  {
    if (Time.frameCount == EnvMan.s_lastFrame)
      return;
    EnvMan.s_lastFrame = Time.frameCount;
    this.UpdateTimeSkip(Time.fixedDeltaTime);
    this.m_totalSeconds = ZNet.instance.GetTimeSeconds();
    long totalSeconds = (long) this.m_totalSeconds;
    float num1 = this.RescaleDayFraction(Mathf.Clamp01((float) (this.m_totalSeconds * 1000.0 % (double) (this.m_dayLengthSec * 1000L) / 1000.0) / (float) this.m_dayLengthSec));
    float smoothDayFraction = this.m_smoothDayFraction;
    this.m_smoothDayFraction = Mathf.Repeat(Mathf.LerpAngle(this.m_smoothDayFraction * 360f, num1 * 360f, 0.01f), 360f) / 360f;
    if (this.m_debugTimeOfDay)
      this.m_smoothDayFraction = this.m_debugTime;
    float num2 = Mathf.Pow(Mathf.Max(1f - Mathf.Clamp01(this.m_smoothDayFraction / 0.25f), Mathf.Clamp01((float) (((double) this.m_smoothDayFraction - 0.75) / 0.25))), 0.5f);
    float num3 = Mathf.Pow(Mathf.Clamp01((float) (1.0 - (double) Mathf.Abs(this.m_smoothDayFraction - 0.5f) / 0.25)), 0.5f);
    float num4 = Mathf.Min(Mathf.Clamp01((float) (1.0 - ((double) this.m_smoothDayFraction - 0.259999990463257) / -(double) this.m_sunHorizonTransitionL)), Mathf.Clamp01((float) (1.0 - ((double) this.m_smoothDayFraction - 0.259999990463257) / (double) this.m_sunHorizonTransitionH)));
    float num5 = Mathf.Min(Mathf.Clamp01((float) (1.0 - ((double) this.m_smoothDayFraction - 0.740000009536743) / -(double) this.m_sunHorizonTransitionH)), Mathf.Clamp01((float) (1.0 - ((double) this.m_smoothDayFraction - 0.740000009536743) / (double) this.m_sunHorizonTransitionL)));
    float num6 = (float) (1.0 / ((double) num2 + (double) num3 + (double) num4 + (double) num5));
    float nightInt = num2 * num6;
    float dayInt = num3 * num6;
    float morningInt = num4 * num6;
    float eveningInt = num5 * num6;
    Heightmap.Biome biome = this.GetBiome();
    this.UpdateTriggers(smoothDayFraction, this.m_smoothDayFraction, biome, Time.fixedDeltaTime);
    this.UpdateEnvironment(totalSeconds, biome);
    this.InterpolateEnvironment(Time.fixedDeltaTime);
    this.UpdateWind(totalSeconds, Time.fixedDeltaTime);
    if (!string.IsNullOrEmpty(this.m_forceEnv))
    {
      EnvSetup env = this.GetEnv(this.m_forceEnv);
      if (env != null)
        this.SetEnv(env, dayInt, nightInt, morningInt, eveningInt, Time.fixedDeltaTime);
    }
    else
      this.SetEnv(this.m_currentEnv, dayInt, nightInt, morningInt, eveningInt, Time.fixedDeltaTime);
    EnvMan.s_isDay = this.CalculateDay();
    EnvMan.s_isDaylight = this.CalculateDaylight();
    EnvMan.s_isAfternoon = this.CalculateAfternoon();
    EnvMan.s_isCold = this.CalculateCold();
    EnvMan.s_isFreezing = this.CalculateFreezing();
    EnvMan.s_isNight = this.CalculateNight();
    EnvMan.s_isWet = this.CalculateWet();
    EnvMan.s_canSleep = this.CalculateCanSleep();
  }

  private int GetCurrentDay() => (int) (this.m_totalSeconds / (double) this.m_dayLengthSec);

  private void UpdateTriggers(
    float oldDayFraction,
    float newDayFraction,
    Heightmap.Biome biome,
    float dt)
  {
    if ((UnityEngine.Object) Player.m_localPlayer == (UnityEngine.Object) null || biome == Heightmap.Biome.None)
      return;
    EnvSetup currentEnvironment = this.GetCurrentEnvironment();
    if (currentEnvironment == null)
      return;
    this.UpdateAmbientMusic(biome, currentEnvironment, dt);
    if ((double) oldDayFraction > 0.200000002980232 && (double) oldDayFraction < 0.25 && (double) newDayFraction > 0.25 && (double) newDayFraction < 0.300000011920929)
      this.OnMorning(biome, currentEnvironment);
    if ((double) oldDayFraction <= 0.699999988079071 || (double) oldDayFraction >= 0.75 || (double) newDayFraction <= 0.75 || (double) newDayFraction >= 0.800000011920929)
      return;
    this.OnEvening(biome, currentEnvironment);
  }

  private void UpdateAmbientMusic(Heightmap.Biome biome, EnvSetup currentEnv, float dt)
  {
    this.m_ambientMusicTimer += dt;
    if ((double) this.m_ambientMusicTimer <= 2.0)
      return;
    this.m_ambientMusicTimer = 0.0f;
    this.m_ambientMusic = (string) null;
    BiomeEnvSetup biomeEnvSetup = this.GetBiomeEnvSetup(biome);
    if (EnvMan.IsDay())
    {
      if (currentEnv.m_musicDay.Length > 0)
      {
        this.m_ambientMusic = currentEnv.m_musicDay;
      }
      else
      {
        if (biomeEnvSetup.m_musicDay.Length <= 0)
          return;
        this.m_ambientMusic = biomeEnvSetup.m_musicDay;
      }
    }
    else if (currentEnv.m_musicNight.Length > 0)
    {
      this.m_ambientMusic = currentEnv.m_musicNight;
    }
    else
    {
      if (biomeEnvSetup.m_musicNight.Length <= 0)
        return;
      this.m_ambientMusic = biomeEnvSetup.m_musicNight;
    }
  }

  public string GetAmbientMusic() => this.m_ambientMusic;

  private void OnMorning(Heightmap.Biome biome, EnvSetup currentEnv)
  {
    string name = "morning";
    if (currentEnv.m_musicMorning.Length > 0)
    {
      name = !(currentEnv.m_musicMorning == currentEnv.m_musicDay) ? currentEnv.m_musicMorning : "-";
    }
    else
    {
      BiomeEnvSetup biomeEnvSetup = this.GetBiomeEnvSetup(biome);
      if (biomeEnvSetup.m_musicMorning.Length > 0)
        name = !(biomeEnvSetup.m_musicMorning == biomeEnvSetup.m_musicDay) ? biomeEnvSetup.m_musicMorning : "-";
    }
    if (name != "-")
      MusicMan.instance.TriggerMusic(name);
    Player.m_localPlayer.Message(MessageHud.MessageType.Center, Localization.instance.Localize("$msg_newday", this.GetCurrentDay().ToString()), 0, (Sprite) null);
  }

  private void OnEvening(Heightmap.Biome biome, EnvSetup currentEnv)
  {
    string name = "evening";
    if (currentEnv.m_musicEvening.Length > 0)
    {
      name = !(currentEnv.m_musicEvening == currentEnv.m_musicNight) ? currentEnv.m_musicEvening : "-";
    }
    else
    {
      BiomeEnvSetup biomeEnvSetup = this.GetBiomeEnvSetup(biome);
      if (biomeEnvSetup.m_musicEvening.Length > 0)
        name = !(biomeEnvSetup.m_musicEvening == biomeEnvSetup.m_musicNight) ? biomeEnvSetup.m_musicEvening : "-";
    }
    if (name != "-")
      MusicMan.instance.TriggerMusic(name);
    MusicMan.instance.TriggerMusic(name);
  }

  public void SetForceEnvironment(string env)
  {
    if (this.m_forceEnv == env)
      return;
    ZLog.Log((object) ("Setting forced environment " + env));
    this.m_forceEnv = env;
    this.FixedUpdate();
    if (!(bool) (UnityEngine.Object) ReflectionUpdate.instance)
      return;
    ReflectionUpdate.instance.UpdateReflection();
  }

  private EnvSetup SelectWeightedEnvironment(List<EnvEntry> environments)
  {
    float maxInclusive = 0.0f;
    foreach (EnvEntry environment in environments)
    {
      if (!environment.m_ashlandsOverride && !environment.m_deepnorthOverride)
        maxInclusive += environment.m_weight;
    }
    float num1 = UnityEngine.Random.Range(0.0f, maxInclusive);
    float num2 = 0.0f;
    foreach (EnvEntry environment in environments)
    {
      if (!environment.m_ashlandsOverride && !environment.m_deepnorthOverride)
      {
        num2 += environment.m_weight;
        if ((double) num2 >= (double) num1)
          return environment.m_env;
      }
    }
    EnvEntry environment1 = environments[environments.Count - 1];
    return environment1.m_ashlandsOverride || environment1.m_deepnorthOverride ? (EnvSetup) null : environment1.m_env;
  }

  private string GetEnvironmentOverride()
  {
    if (!string.IsNullOrEmpty(this.m_debugEnv))
      return this.m_debugEnv;
    if ((UnityEngine.Object) Player.m_localPlayer != (UnityEngine.Object) null && Player.m_localPlayer.InIntro())
      return this.m_introEnvironment;
    string envOverride = RandEventSystem.instance.GetEnvOverride();
    if (!string.IsNullOrEmpty(envOverride))
      return envOverride;
    string environment = EnvZone.GetEnvironment();
    return !string.IsNullOrEmpty(environment) ? environment : (string) null;
  }

  private void UpdateEnvironment(long sec, Heightmap.Biome biome)
  {
    string environmentOverride = this.GetEnvironmentOverride();
    if (!string.IsNullOrEmpty(environmentOverride))
    {
      this.m_environmentPeriod = -1L;
      this.m_currentBiome = this.GetBiome();
      this.QueueEnvironment(environmentOverride);
    }
    else
    {
      long seed = sec / this.m_environmentDuration;
      Vector3 position = Utils.GetMainCamera().transform.position;
      bool flag1 = WorldGenerator.IsAshlands(position.x, position.z);
      bool flag2 = WorldGenerator.IsDeepnorth(position.x, position.y);
      bool flag3 = flag1 | flag2;
      this.m_dirLight.renderMode = !(bool) (UnityEngine.Object) Player.m_localPlayer || !Player.m_localPlayer.InInterior() ? LightRenderMode.ForcePixel : LightRenderMode.ForceVertex;
      if (this.m_environmentPeriod == seed && this.m_currentBiome == biome && flag3 == this.m_inAshlandsOrDeepnorth)
        return;
      this.m_environmentPeriod = seed;
      this.m_currentBiome = biome;
      this.m_inAshlandsOrDeepnorth = flag3;
      UnityEngine.Random.State state = UnityEngine.Random.state;
      UnityEngine.Random.InitState((int) seed);
      List<EnvEntry> availableEnvironments = this.GetAvailableEnvironments(biome);
      if (availableEnvironments != null && availableEnvironments.Count > 0)
      {
        EnvSetup env = this.SelectWeightedEnvironment(availableEnvironments);
        foreach (EnvEntry envEntry in availableEnvironments)
        {
          if (envEntry.m_ashlandsOverride & flag1)
            env = envEntry.m_env;
          if (envEntry.m_deepnorthOverride & flag2)
            env = envEntry.m_env;
        }
        if (env != null)
          this.QueueEnvironment(env);
      }
      UnityEngine.Random.state = state;
    }
  }

  private BiomeEnvSetup GetBiomeEnvSetup(Heightmap.Biome biome)
  {
    foreach (BiomeEnvSetup biome1 in this.m_biomes)
    {
      if (biome1.m_biome == biome)
        return biome1;
    }
    return (BiomeEnvSetup) null;
  }

  private List<EnvEntry> GetAvailableEnvironments(Heightmap.Biome biome) => this.GetBiomeEnvSetup(biome)?.m_environments;

  private Heightmap.Biome GetBiome()
  {
    Camera mainCamera = Utils.GetMainCamera();
    if ((UnityEngine.Object) mainCamera == (UnityEngine.Object) null)
      return Heightmap.Biome.None;
    Vector3 position = mainCamera.transform.position;
    if ((UnityEngine.Object) this.m_cachedHeightmap == (UnityEngine.Object) null || !this.m_cachedHeightmap.IsPointInside(position))
      this.m_cachedHeightmap = Heightmap.FindHeightmap(position);
    if (!(bool) (UnityEngine.Object) this.m_cachedHeightmap)
      return Heightmap.Biome.None;
    bool flag1 = WorldGenerator.IsAshlands(position.x, position.z);
    bool flag2 = WorldGenerator.IsDeepnorth(position.x, position.y);
    return this.m_cachedHeightmap.GetBiome(position, this.m_oceanLevelEnvCheckAshlandsDeepnorth, flag1 | flag2);
  }

  private void InterpolateEnvironment(float dt)
  {
    if (this.m_nextEnv == null)
      return;
    this.m_transitionTimer += dt;
    float i = Mathf.Clamp01(this.m_transitionTimer / this.m_transitionDuration);
    this.m_currentEnv = this.InterpolateEnvironment(this.m_prevEnv, this.m_nextEnv, i);
    if ((double) i < 1.0)
      return;
    this.m_currentEnv = this.m_nextEnv;
    this.m_prevEnv = (EnvSetup) null;
    this.m_nextEnv = (EnvSetup) null;
  }

  private void QueueEnvironment(string name)
  {
    if (this.m_currentEnv.m_name == name || this.m_nextEnv != null && this.m_nextEnv.m_name == name)
      return;
    EnvSetup env = this.GetEnv(name);
    if (env == null)
      return;
    this.QueueEnvironment(env);
  }

  private void QueueEnvironment(EnvSetup env)
  {
    if (Terminal.m_showTests)
    {
      Terminal.Log((object) string.Format("Queuing environment: {0} (biome: {1})", (object) env.m_name, (object) this.m_currentBiome));
      Terminal.m_testList["Env"] = string.Format("{0} (biome: {1})", (object) env.m_name, (object) this.m_currentBiome);
    }
    if (this.m_firstEnv)
    {
      this.m_firstEnv = false;
      this.m_currentEnv = env;
    }
    else
    {
      this.m_prevEnv = this.m_currentEnv.Clone();
      this.m_nextEnv = env;
      this.m_transitionTimer = 0.0f;
    }
  }

  private EnvSetup InterpolateEnvironment(EnvSetup a, EnvSetup b, float i)
  {
    EnvSetup envSetup = a.Clone();
    envSetup.m_name = b.m_name;
    if ((double) i >= 0.5)
    {
      envSetup.m_isFreezingAtNight = b.m_isFreezingAtNight;
      envSetup.m_isFreezing = b.m_isFreezing;
      envSetup.m_isCold = b.m_isCold;
      envSetup.m_isColdAtNight = b.m_isColdAtNight;
      envSetup.m_isColdAtNight = b.m_isColdAtNight;
    }
    envSetup.m_ambColorDay = Color.Lerp(a.m_ambColorDay, b.m_ambColorDay, i);
    envSetup.m_ambColorNight = Color.Lerp(a.m_ambColorNight, b.m_ambColorNight, i);
    envSetup.m_fogColorDay = Color.Lerp(a.m_fogColorDay, b.m_fogColorDay, i);
    envSetup.m_fogColorEvening = Color.Lerp(a.m_fogColorEvening, b.m_fogColorEvening, i);
    envSetup.m_fogColorMorning = Color.Lerp(a.m_fogColorMorning, b.m_fogColorMorning, i);
    envSetup.m_fogColorNight = Color.Lerp(a.m_fogColorNight, b.m_fogColorNight, i);
    envSetup.m_fogColorSunDay = Color.Lerp(a.m_fogColorSunDay, b.m_fogColorSunDay, i);
    envSetup.m_fogColorSunEvening = Color.Lerp(a.m_fogColorSunEvening, b.m_fogColorSunEvening, i);
    envSetup.m_fogColorSunMorning = Color.Lerp(a.m_fogColorSunMorning, b.m_fogColorSunMorning, i);
    envSetup.m_fogColorSunNight = Color.Lerp(a.m_fogColorSunNight, b.m_fogColorSunNight, i);
    envSetup.m_fogDensityDay = Mathf.Lerp(a.m_fogDensityDay, b.m_fogDensityDay, i);
    envSetup.m_fogDensityEvening = Mathf.Lerp(a.m_fogDensityEvening, b.m_fogDensityEvening, i);
    envSetup.m_fogDensityMorning = Mathf.Lerp(a.m_fogDensityMorning, b.m_fogDensityMorning, i);
    envSetup.m_fogDensityNight = Mathf.Lerp(a.m_fogDensityNight, b.m_fogDensityNight, i);
    envSetup.m_sunColorDay = Color.Lerp(a.m_sunColorDay, b.m_sunColorDay, i);
    envSetup.m_sunColorEvening = Color.Lerp(a.m_sunColorEvening, b.m_sunColorEvening, i);
    envSetup.m_sunColorMorning = Color.Lerp(a.m_sunColorMorning, b.m_sunColorMorning, i);
    envSetup.m_sunColorNight = Color.Lerp(a.m_sunColorNight, b.m_sunColorNight, i);
    envSetup.m_lightIntensityDay = Mathf.Lerp(a.m_lightIntensityDay, b.m_lightIntensityDay, i);
    envSetup.m_lightIntensityNight = Mathf.Lerp(a.m_lightIntensityNight, b.m_lightIntensityNight, i);
    envSetup.m_sunAngle = Mathf.Lerp(a.m_sunAngle, b.m_sunAngle, i);
    envSetup.m_windMin = Mathf.Lerp(a.m_windMin, b.m_windMin, i);
    envSetup.m_windMax = Mathf.Lerp(a.m_windMax, b.m_windMax, i);
    envSetup.m_rainCloudAlpha = Mathf.Lerp(a.m_rainCloudAlpha, b.m_rainCloudAlpha, i);
    envSetup.m_ambientLoop = (double) i > 0.75 ? b.m_ambientLoop : a.m_ambientLoop;
    envSetup.m_ambientVol = (double) i > 0.75 ? b.m_ambientVol : a.m_ambientVol;
    envSetup.m_musicEvening = b.m_musicEvening;
    envSetup.m_musicMorning = b.m_musicMorning;
    envSetup.m_musicDay = b.m_musicDay;
    envSetup.m_musicNight = b.m_musicNight;
    return envSetup;
  }

  private void SetEnv(
    EnvSetup env,
    float dayInt,
    float nightInt,
    float morningInt,
    float eveningInt,
    float dt)
  {
    Camera mainCamera = Utils.GetMainCamera();
    if ((UnityEngine.Object) mainCamera == (UnityEngine.Object) null)
      return;
    this.m_dirLight.transform.rotation = Quaternion.Euler(env.m_sunAngle - 90f, 0.0f, 0.0f) * Quaternion.Euler(0.0f, -90f, 0.0f) * Quaternion.Euler((float) (360.0 * (double) this.m_smoothDayFraction - 90.0), 0.0f, 0.0f);
    Vector3 vector3 = -this.m_dirLight.transform.forward;
    this.m_dirLight.intensity = env.m_lightIntensityDay * dayInt;
    this.m_dirLight.intensity += env.m_lightIntensityNight * nightInt;
    if ((double) nightInt > 0.0)
      this.m_dirLight.transform.rotation = this.m_dirLight.transform.rotation * Quaternion.Euler(180f, 0.0f, 0.0f);
    this.m_dirLight.transform.position = mainCamera.transform.position - this.m_dirLight.transform.forward * 3000f;
    this.m_dirLight.color = new Color(0.0f, 0.0f, 0.0f, 0.0f);
    this.m_dirLight.color += env.m_sunColorNight * nightInt;
    if ((double) dayInt > 0.0)
    {
      this.m_dirLight.color += env.m_sunColorDay * dayInt;
      this.m_dirLight.color += env.m_sunColorMorning * morningInt;
      this.m_dirLight.color += env.m_sunColorEvening * eveningInt;
    }
    RenderSettings.fogColor = new Color(0.0f, 0.0f, 0.0f, 0.0f);
    RenderSettings.fogColor += env.m_fogColorNight * nightInt;
    RenderSettings.fogColor += env.m_fogColorDay * dayInt;
    RenderSettings.fogColor += env.m_fogColorMorning * morningInt;
    RenderSettings.fogColor += env.m_fogColorEvening * eveningInt;
    this.m_sunFogColor = new Color(0.0f, 0.0f, 0.0f, 0.0f);
    this.m_sunFogColor += env.m_fogColorSunNight * nightInt;
    if ((double) dayInt > 0.0)
    {
      this.m_sunFogColor += env.m_fogColorSunDay * dayInt;
      this.m_sunFogColor += env.m_fogColorSunMorning * morningInt;
      this.m_sunFogColor += env.m_fogColorSunEvening * eveningInt;
    }
    this.m_sunFogColor = Color.Lerp(RenderSettings.fogColor, this.m_sunFogColor, Mathf.Clamp01(Mathf.Max(nightInt, dayInt) * 3f));
    RenderSettings.fogDensity = 0.0f;
    RenderSettings.fogDensity += env.m_fogDensityNight * nightInt;
    RenderSettings.fogDensity += env.m_fogDensityDay * dayInt;
    RenderSettings.fogDensity += env.m_fogDensityMorning * morningInt;
    RenderSettings.fogDensity += env.m_fogDensityEvening * eveningInt;
    RenderSettings.ambientMode = AmbientMode.Flat;
    RenderSettings.ambientLight = Color.Lerp(env.m_ambColorNight, env.m_ambColorDay, dayInt);
    SunShafts component = mainCamera.GetComponent<SunShafts>();
    if ((bool) (UnityEngine.Object) component)
      component.sunColor = this.m_dirLight.color;
    if ((UnityEngine.Object) env.m_envObject != (UnityEngine.Object) this.m_currentEnvObject)
    {
      if ((bool) (UnityEngine.Object) this.m_currentEnvObject)
      {
        this.m_currentEnvObject.SetActive(false);
        this.m_currentEnvObject = (GameObject) null;
      }
      if ((bool) (UnityEngine.Object) env.m_envObject)
      {
        this.m_currentEnvObject = env.m_envObject;
        this.m_currentEnvObject.SetActive(true);
      }
    }
    if (env.m_psystems != this.m_currentPSystems)
    {
      if (this.m_currentPSystems != null)
      {
        this.SetParticleArrayEnabled(this.m_currentPSystems, false);
        this.m_currentPSystems = (GameObject[]) null;
      }
      if (env.m_psystems != null && (!env.m_psystemsOutsideOnly || (bool) (UnityEngine.Object) Player.m_localPlayer && !Player.m_localPlayer.InShelter()))
      {
        this.SetParticleArrayEnabled(env.m_psystems, true);
        this.m_currentPSystems = env.m_psystems;
      }
    }
    this.m_clouds.material.SetFloat(EnvMan.s_rain, env.m_rainCloudAlpha);
    if ((bool) (UnityEngine.Object) env.m_ambientLoop)
      AudioMan.instance.QueueAmbientLoop(env.m_ambientLoop, env.m_ambientVol);
    else
      AudioMan.instance.StopAmbientLoop();
    Shader.SetGlobalVector(EnvMan.s_skyboxSunDir, (Vector4) vector3);
    Shader.SetGlobalVector(EnvMan.s_skyboxSunDir, (Vector4) vector3);
    Shader.SetGlobalVector(EnvMan.s_sunDir, (Vector4) -this.m_dirLight.transform.forward);
    Shader.SetGlobalColor(EnvMan.s_sunFogColor, this.m_sunFogColor);
    Shader.SetGlobalColor(EnvMan.s_sunColor, this.m_dirLight.color * this.m_dirLight.intensity);
    Shader.SetGlobalColor(EnvMan.s_ambientColor, RenderSettings.ambientLight);
    float num = Mathf.MoveTowards(Shader.GetGlobalFloat(EnvMan.s_wet), env.m_isWet ? 1f : 0.0f, dt / this.m_wetTransitionDuration);
    Shader.SetGlobalFloat(EnvMan.s_wet, num);
  }

  public float GetDayFraction() => this.m_smoothDayFraction;

  public int GetDay() => this.GetDay(ZNet.instance.GetTimeSeconds());

  public int GetDay(double time) => (int) (time / (double) this.m_dayLengthSec);

  public double GetMorningStartSec(int day) => (double) ((long) day * this.m_dayLengthSec) + (double) this.m_dayLengthSec * 0.150000005960464;

  private void UpdateTimeSkip(float dt)
  {
    if (!ZNet.instance.IsServer() || !this.m_skipTime)
      return;
    double time = ZNet.instance.GetTimeSeconds() + (double) dt * this.m_timeSkipSpeed;
    if (time >= this.m_skipToTime)
    {
      time = this.m_skipToTime;
      this.m_skipTime = false;
    }
    ZNet.instance.SetNetTime(time);
  }

  public bool IsTimeSkipping() => this.m_skipTime;

  public void SkipToMorning()
  {
    double timeSeconds = ZNet.instance.GetTimeSeconds();
    int day = this.GetDay(timeSeconds - (double) this.m_dayLengthSec * 0.150000005960464);
    double morningStartSec = this.GetMorningStartSec(day + 1);
    this.m_skipTime = true;
    this.m_skipToTime = morningStartSec;
    this.m_timeSkipSpeed = (morningStartSec - timeSeconds) / 12.0;
    ZLog.Log((object) ("Time " + timeSeconds.ToString() + ", day:" + day.ToString() + "    nextm:" + morningStartSec.ToString() + "  skipspeed:" + this.m_timeSkipSpeed.ToString()));
  }

  public static bool IsFreezing() => EnvMan.s_isFreezing;

  public static bool IsCold() => EnvMan.s_isCold;

  public static bool IsWet() => EnvMan.s_isWet;

  public static bool CanSleep() => EnvMan.s_canSleep;

  public static bool IsDay() => EnvMan.s_isDay;

  public static bool IsAfternoon() => EnvMan.s_isAfternoon;

  public static bool IsNight() => EnvMan.s_isNight;

  public static bool IsDaylight() => EnvMan.s_isDaylight;

  private bool CalculateFreezing()
  {
    EnvSetup currentEnvironment = this.GetCurrentEnvironment();
    return currentEnvironment != null && (currentEnvironment.m_isFreezing || currentEnvironment.m_isFreezingAtNight && !EnvMan.IsDay());
  }

  private bool CalculateCold()
  {
    EnvSetup currentEnvironment = this.GetCurrentEnvironment();
    return currentEnvironment != null && (currentEnvironment.m_isCold || currentEnvironment.m_isColdAtNight && !EnvMan.IsDay());
  }

  private bool CalculateWet()
  {
    EnvSetup currentEnvironment = this.GetCurrentEnvironment();
    return currentEnvironment != null && currentEnvironment.m_isWet;
  }

  private bool CalculateCanSleep()
  {
    if (!EnvMan.IsAfternoon() && !EnvMan.IsNight())
      return false;
    return (UnityEngine.Object) Player.m_localPlayer == (UnityEngine.Object) null || ZNet.instance.GetTimeSeconds() > Player.m_localPlayer.m_wakeupTime + this.m_sleepCooldownSeconds;
  }

  private bool CalculateDay()
  {
    float dayFraction = this.GetDayFraction();
    return (double) dayFraction >= 0.25 && (double) dayFraction <= 0.75;
  }

  private bool CalculateAfternoon()
  {
    float dayFraction = this.GetDayFraction();
    return (double) dayFraction >= 0.5 && (double) dayFraction <= 0.75;
  }

  private bool CalculateNight()
  {
    float dayFraction = this.GetDayFraction();
    return (double) dayFraction <= 0.25 || (double) dayFraction >= 0.75;
  }

  private bool CalculateDaylight()
  {
    EnvSetup currentEnvironment = this.GetCurrentEnvironment();
    return (currentEnvironment == null || !currentEnvironment.m_alwaysDark) && EnvMan.IsDay();
  }

  public Heightmap.Biome GetCurrentBiome() => this.m_currentBiome;

  public bool IsEnvironment(string name) => this.GetCurrentEnvironment().m_name == name;

  public bool IsEnvironment(List<string> names)
  {
    EnvSetup currentEnvironment = this.GetCurrentEnvironment();
    return names.Contains(currentEnvironment.m_name);
  }

  public EnvSetup GetCurrentEnvironment()
  {
    if (!string.IsNullOrEmpty(this.m_forceEnv))
    {
      EnvSetup env = this.GetEnv(this.m_forceEnv);
      if (env != null)
        return env;
    }
    return this.m_currentEnv;
  }

  public Color GetSunFogColor() => this.m_sunFogColor;

  public Vector3 GetSunDirection() => this.m_dirLight.transform.forward;

  private EnvSetup GetEnv(string name)
  {
    foreach (EnvSetup environment in this.m_environments)
    {
      if (environment.m_name == name)
        return environment;
    }
    return (EnvSetup) null;
  }

  private EnvSetup GetDefaultEnv()
  {
    foreach (EnvSetup environment in this.m_environments)
    {
      if (environment.m_default)
        return environment;
    }
    return (EnvSetup) null;
  }

  public void SetDebugWind(float angle, float intensity)
  {
    this.m_debugWind = true;
    this.m_debugWindAngle = angle;
    this.m_debugWindIntensity = Mathf.Clamp01(intensity);
  }

  public void ResetDebugWind() => this.m_debugWind = false;

  public Vector3 GetWindForce() => this.GetWindDir() * this.m_wind.w;

  public Vector3 GetWindDir() => new Vector3(this.m_wind.x, this.m_wind.y, this.m_wind.z);

  public float GetWindIntensity() => this.m_wind.w;

  private void UpdateWind(long timeSec, float dt)
  {
    if (this.m_debugWind)
    {
      float f = (float) Math.PI / 180f * this.m_debugWindAngle;
      this.SetTargetWind(new Vector3(Mathf.Sin(f), 0.0f, Mathf.Cos(f)), this.m_debugWindIntensity);
    }
    else
    {
      EnvSetup currentEnvironment = this.GetCurrentEnvironment();
      if (currentEnvironment != null)
      {
        UnityEngine.Random.State state = UnityEngine.Random.state;
        float angle = 0.0f;
        float intensity = 0.5f;
        this.AddWindOctave(timeSec, 1, ref angle, ref intensity);
        this.AddWindOctave(timeSec, 2, ref angle, ref intensity);
        this.AddWindOctave(timeSec, 4, ref angle, ref intensity);
        this.AddWindOctave(timeSec, 8, ref angle, ref intensity);
        UnityEngine.Random.state = state;
        Vector3 dir = new Vector3(Mathf.Sin(angle), 0.0f, Mathf.Cos(angle));
        float num = Mathf.Lerp(currentEnvironment.m_windMin, currentEnvironment.m_windMax, intensity);
        if ((bool) (UnityEngine.Object) Player.m_localPlayer && !Player.m_localPlayer.InInterior())
        {
          float v = Utils.LengthXZ(Player.m_localPlayer.transform.position);
          if ((double) v > 10500.0 - (double) this.m_edgeOfWorldWidth)
          {
            float t = 1f - Mathf.Pow(1f - Utils.LerpStep(10500f - this.m_edgeOfWorldWidth, 10500f, v), 2f);
            dir = Player.m_localPlayer.transform.position.normalized;
            num = Mathf.Lerp(num, 1f, t);
          }
          else
          {
            Ship localShip = Ship.GetLocalShip();
            if ((bool) (UnityEngine.Object) localShip && localShip.IsWindControllActive())
              dir = localShip.transform.forward;
          }
        }
        this.SetTargetWind(dir, num);
      }
    }
    this.UpdateWindTransition(dt);
  }

  private void AddWindOctave(long timeSec, int octave, ref float angle, ref float intensity)
  {
    UnityEngine.Random.InitState((int) (timeSec / (this.m_windPeriodDuration / (long) octave)));
    angle += UnityEngine.Random.value * (6.283185f / (float) octave);
    intensity += (float) (-(0.5 / (double) octave) + (double) UnityEngine.Random.value / (double) octave);
  }

  private void SetTargetWind(Vector3 dir, float intensity)
  {
    if ((double) this.m_windTransitionTimer >= 0.0)
      return;
    intensity = Mathf.Clamp(intensity, 0.05f, 1f);
    if (Mathf.Approximately(dir.x, this.m_windDir1.x) && Mathf.Approximately(dir.y, this.m_windDir1.y) && Mathf.Approximately(dir.z, this.m_windDir1.z) && Mathf.Approximately(intensity, this.m_windDir1.w))
      return;
    this.m_windTransitionTimer = 0.0f;
    this.m_windDir2 = new Vector4(dir.x, dir.y, dir.z, intensity);
  }

  private void UpdateWindTransition(float dt)
  {
    if ((double) this.m_windTransitionTimer >= 0.0)
    {
      this.m_windTransitionTimer += dt;
      float t = Mathf.Clamp01(this.m_windTransitionTimer / this.m_windTransitionDuration);
      Shader.SetGlobalVector(EnvMan.s_globalWind1, this.m_windDir1);
      Shader.SetGlobalVector(EnvMan.s_globalWind2, this.m_windDir2);
      Shader.SetGlobalFloat(EnvMan.s_globalWindAlpha, t);
      this.m_wind = Vector4.Lerp(this.m_windDir1, this.m_windDir2, t);
      if ((double) t >= 1.0)
      {
        this.m_windDir1 = this.m_windDir2;
        this.m_windTransitionTimer = -1f;
      }
    }
    else
    {
      Shader.SetGlobalVector(EnvMan.s_globalWind1, this.m_windDir1);
      Shader.SetGlobalFloat(EnvMan.s_globalWindAlpha, 0.0f);
      this.m_wind = this.m_windDir1;
    }
    Shader.SetGlobalVector(EnvMan.s_globalWindForce, (Vector4) this.GetWindForce());
  }

  public void GetWindData(out Vector4 wind1, out Vector4 wind2, out float alpha)
  {
    wind1 = this.m_windDir1;
    wind2 = this.m_windDir2;
    if ((double) this.m_windTransitionTimer >= 0.0)
      alpha = Mathf.Clamp01(this.m_windTransitionTimer / this.m_windTransitionDuration);
    else
      alpha = 0.0f;
  }

  public void AppendEnvironment(EnvSetup env)
  {
    EnvSetup env1 = this.GetEnv(env.m_name);
    if (env1 != null)
    {
      ZLog.LogError((object) ("Environment with name " + env.m_name + " is defined multiple times and will be overwritten! Check locationlists & gamemain."));
      this.m_environments.Remove(env1);
    }
    this.m_environments.Add(env);
    this.InitializeEnvironment(env);
  }

  public void AppendBiomeSetup(BiomeEnvSetup biomeEnv)
  {
    BiomeEnvSetup biomeEnvSetup = this.GetBiomeEnvSetup(biomeEnv.m_biome);
    if (biomeEnvSetup != null)
    {
      biomeEnvSetup.m_environments.AddRange((IEnumerable<EnvEntry>) biomeEnv.m_environments);
      if (!string.IsNullOrEmpty(biomeEnv.m_musicDay) || !string.IsNullOrEmpty(biomeEnv.m_musicEvening) || !string.IsNullOrEmpty(biomeEnv.m_musicMorning) || !string.IsNullOrEmpty(biomeEnv.m_musicNight))
        ZLog.LogError((object) ("EnvSetup " + biomeEnv.m_name + " sets music, but is already defined previously in " + biomeEnvSetup.m_name + ", only settings from first loaded envsetup per biome will be used!"));
    }
    this.m_biomes.Add(biomeEnv);
    this.InitializeBiomeEnvSetup(biomeEnv);
  }

  public bool CheckInteriorBuildingOverride()
  {
    string lower = this.GetCurrentEnvironment().m_name.ToLower();
    foreach (string overrideEnvironment in this.m_interiorBuildingOverrideEnvironments)
    {
      if (overrideEnvironment.ToLower() == lower)
        return true;
    }
    return false;
  }
}
