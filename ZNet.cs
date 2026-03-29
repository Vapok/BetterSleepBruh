// Decompiled with JetBrains decompiler
// Type: ZNet
// Assembly: assembly_valheim, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: E4CFC702-61AB-46D1-9F39-CC2DEB9BC839
// Assembly location: G:\Steam\steamapps\common\Valheim\valheim_Data\Managed\assembly_valheim.dll

using GUIFramework;
using Splatform;
using Steamworks;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;
using UserManagement;

public class ZNet : MonoBehaviour
{
  public static Action WorldSaveStarted;
  public static Action WorldSaveFinished;
  private float m_banlistTimer;
  private static ZNet m_instance;
  public const int ServerPlayerLimit = 10;
  public int m_hostPort = 2456;
  public RectTransform m_passwordDialog;
  public RectTransform m_connectingDialog;
  public float m_badConnectionPing = 5f;
  public int m_zdoSectorsWidth = 512;
  private ZConnector2 m_serverConnector;
  private ISocket m_hostSocket;
  private readonly List<ZNetPeer> m_peers = new List<ZNetPeer>();
  private readonly List<ZNetPeer> m_peersCopy = new List<ZNetPeer>();
  private Thread m_saveThread;
  private bool m_saveExceededCloudQuota;
  private float m_saveStartTime;
  private float m_saveThreadStartTime;
  private float m_saveDoneTime;
  public static bool m_loadError = false;
  private float m_sendSaveMessage;
  private ZDOMan m_zdoMan;
  private ZRoutedRpc m_routedRpc;
  private ZNat m_nat;
  private double m_netTime = 2040.0;
  private ZDOID m_characterID = ZDOID.None;
  private Vector3 m_referencePosition = Vector3.zero;
  private bool m_publicReferencePosition;
  private float m_periodicSendTimer;
  public Dictionary<string, string> m_serverSyncedPlayerData = new Dictionary<string, string>();
  public static int m_backupCount = 2;
  public static int m_backupShort = 7200;
  public static int m_backupLong = 43200;
  private bool m_haveStoped;
  private static bool m_isServer = true;
  private static World m_world = (World) null;
  private static HttpClient m_httpClient;
  private int m_registerAttempts;
  public static OnlineBackendType m_onlineBackend = OnlineBackendType.Steamworks;
  private static string m_serverPlayFabPlayerId = (string) null;
  private static ulong m_serverSteamID = 0;
  private static string m_serverHost = "";
  private static int m_serverHostPort = 0;
  private static bool m_openServer = true;
  private static bool m_publicServer = true;
  private static string m_serverPassword = "";
  private static string m_serverPasswordSalt = "";
  private static string m_ServerName = "";
  private static ZNet.ConnectionStatus m_connectionStatus = ZNet.ConnectionStatus.None;
  private static ZNet.ConnectionStatus m_externalError = ZNet.ConnectionStatus.None;
  private SyncedList m_adminList;
  private SyncedList m_bannedList;
  private SyncedList m_permittedList;
  private List<ZNet.PlayerInfo> m_players = new List<ZNet.PlayerInfo>();
  private List<string> m_adminListForRpc = new List<string>();
  private ZRpc m_tempPasswordRPC;
  private List<ZNet.CrossNetworkUserInfo> m_playerHistory = new List<ZNet.CrossNetworkUserInfo>();
  private static readonly Dictionary<ZNetPeer, float> PeersToDisconnectAfterKick = new Dictionary<ZNetPeer, float>();
  private const string PlatformDisplayNameKey = "platformDisplayName";
  private readonly Platform m_steamPlatform = new Platform("Steam");

  public static ZNet instance => ZNet.m_instance;

  private void Awake()
  {
    ZNet.m_instance = this;
    ZNet.m_loadError = false;
    this.m_routedRpc = new ZRoutedRpc(ZNet.m_isServer);
    this.m_zdoMan = new ZDOMan(this.m_zdoSectorsWidth);
    this.m_passwordDialog.gameObject.SetActive(false);
    this.m_connectingDialog.gameObject.SetActive(false);
    WorldGenerator.Deitialize();
    if (!SteamManager.Initialize())
      return;
    ZLog.Log((object) ("Steam initialized, persona:" + SteamFriends.GetPersonaName()));
    ZSteamMatchmaking.Initialize();
    ZPlayFabMatchmaking.Initialize(ZNet.m_isServer);
    ZNet.m_backupCount = PlatformPrefs.GetInt("AutoBackups", ZNet.m_backupCount);
    ZNet.m_backupShort = PlatformPrefs.GetInt("AutoBackups_short", ZNet.m_backupShort);
    ZNet.m_backupLong = PlatformPrefs.GetInt("AutoBackups_long", ZNet.m_backupLong);
    if (ZNet.m_isServer)
    {
      FileHelpers.MigrateLocalSyncedListsToCloud();
      if (FileHelpers.LocalStorageSupport == LocalStorageSupport.Supported)
      {
        this.m_adminList = new SyncedList(new FileHelpers.FileLocation(FileHelpers.FileSource.Local, Utils.GetSaveDataPath(FileHelpers.FileSource.Local) + "/adminlist.txt"), "List admin players ID  ONE per line");
        this.m_bannedList = new SyncedList(new FileHelpers.FileLocation(FileHelpers.FileSource.Local, Utils.GetSaveDataPath(FileHelpers.FileSource.Local) + "/bannedlist.txt"), "List banned players ID  ONE per line");
        this.m_permittedList = new SyncedList(new FileHelpers.FileLocation(FileHelpers.FileSource.Local, Utils.GetSaveDataPath(FileHelpers.FileSource.Local) + "/permittedlist.txt"), "List permitted players ID ONE per line");
      }
      else if (FileHelpers.CloudStorageEnabled)
      {
        this.m_adminList = new SyncedList(new FileHelpers.FileLocation(FileHelpers.FileSource.Cloud, Utils.GetSaveDataPath(FileHelpers.FileSource.Cloud) + "/adminlist.txt"), "List admin players ID  ONE per line");
        this.m_bannedList = new SyncedList(new FileHelpers.FileLocation(FileHelpers.FileSource.Cloud, Utils.GetSaveDataPath(FileHelpers.FileSource.Cloud) + "/bannedlist.txt"), "List banned players ID  ONE per line");
        this.m_permittedList = new SyncedList(new FileHelpers.FileLocation(FileHelpers.FileSource.Cloud, Utils.GetSaveDataPath(FileHelpers.FileSource.Cloud) + "/permittedlist.txt"), "List permitted players ID ONE per line");
      }
      else
        ZLog.LogError((object) "Neither Local nor Cloud/Platform storage is enabled on this platform!");
      this.m_adminListForRpc = this.m_adminList.GetList();
      if (ZNet.m_world == null)
      {
        ZNet.m_publicServer = false;
        ZNet.m_world = World.GetDevWorld();
      }
      WorldGenerator.Initialize(ZNet.m_world);
      ZNet.m_connectionStatus = ZNet.ConnectionStatus.Connected;
      ZNet.m_externalError = ZNet.ConnectionStatus.None;
    }
    this.m_routedRpc.SetUID(ZDOMan.GetSessionID());
    if (this.IsServer())
      this.SendPlayerList();
    if (this.IsDedicated())
      return;
    this.m_serverSyncedPlayerData["platformDisplayName"] = PlatformManager.DistributionPlatform.LocalUser.DisplayName;
  }

  private void OnGenerationFinished()
  {
    if (!ZNet.m_openServer)
      return;
    this.OpenServer();
  }

  public void OpenServer()
  {
    if (!ZNet.m_isServer)
      return;
    ZNet.m_openServer = true;
    bool flag = ZNet.m_serverPassword != "";
    GameVersion currentVersion = Version.CurrentVersion;
    uint networkVersion = 36;
    string[] array = ZNet.m_world.m_startingGlobalKeys.ToArray();
    ZSteamMatchmaking.instance.RegisterServer(ZNet.m_ServerName, flag, currentVersion, array, networkVersion, ZNet.m_publicServer, ZNet.m_world.m_seedName, new ZSteamMatchmaking.ServerRegistered(this.OnSteamServerRegistered));
    if (ZNet.m_onlineBackend == OnlineBackendType.Steamworks)
    {
      ZSteamSocket zsteamSocket = new ZSteamSocket();
      zsteamSocket.StartHost();
      this.m_hostSocket = (ISocket) zsteamSocket;
      ZLog.Log((object) "Opened Steam server");
    }
    if (ZNet.m_onlineBackend != OnlineBackendType.PlayFab)
      return;
    ZPlayFabMatchmaking.instance.RegisterServer(ZNet.m_ServerName, flag, ZNet.m_publicServer, currentVersion, array, networkVersion, ZNet.m_world.m_seedName);
    ZPlayFabSocket zplayFabSocket = new ZPlayFabSocket();
    zplayFabSocket.StartHost();
    this.m_hostSocket = (ISocket) zplayFabSocket;
    ZLog.Log((object) "Opened PlayFab server");
  }

  private void Start()
  {
    ZRpc.SetLongTimeout(false);
    ZLog.Log((object) "ZNET START");
    MuteList.Load((Action) null);
    if (ZNet.m_isServer)
      this.ServerLoadWorld();
    else
      this.ClientConnect();
  }

  private void ServerLoadWorld()
  {
    this.LoadWorld();
    ZoneSystem.instance.GenerateLocationsIfNeeded();
    ZoneSystem.instance.GenerateLocationsCompleted += new Action(this.OnGenerationFinished);
    if (!ZNet.m_loadError)
      return;
    ZLog.LogError((object) "World db couldn't load correctly, saving has been disabled to prevent .old file from being overwritten.");
  }

  private void ClientConnect()
  {
    if (ZNet.m_onlineBackend == OnlineBackendType.PlayFab)
    {
      ZLog.Log((object) ("Connecting to server with PlayFab-backend " + ZNet.m_serverPlayFabPlayerId));
      this.Connect(ZNet.m_serverPlayFabPlayerId);
    }
    if (ZNet.m_onlineBackend == OnlineBackendType.Steamworks)
    {
      if (ZNet.m_serverSteamID != 0UL)
      {
        ZLog.Log((object) ("Connecting to server with Steam-backend " + ZNet.m_serverSteamID.ToString()));
        this.Connect(new CSteamID(ZNet.m_serverSteamID));
      }
      else
      {
        ZLog.Log((object) ("Connecting to server with Steam-backend " + ZNet.m_serverHost + ":" + ZNet.m_serverHostPort.ToString()));
        SteamNetworkingIPAddr host = new SteamNetworkingIPAddr();
        host.ParseString(ZNet.m_serverHost + ":" + ZNet.m_serverHostPort.ToString());
        this.Connect(host);
        return;
      }
    }
    if (ZNet.m_onlineBackend != OnlineBackendType.CustomSocket)
      return;
    ZLog.Log((object) ("Connecting to server with socket-backend " + ZNet.m_serverHost + "  " + ZNet.m_serverHostPort.ToString()));
    this.Connect(ZNet.m_serverHost, ZNet.m_serverHostPort);
  }

  private string GetServerIP() => ZNet.GetPublicIP();

  private string LocalIPAddress()
  {
    string str = IPAddress.Loopback.ToString();
    try
    {
      foreach (IPAddress address in Dns.GetHostEntry(Dns.GetHostName()).AddressList)
      {
        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
          str = address.ToString();
          break;
        }
      }
    }
    catch (Exception ex)
    {
      ZLog.Log((object) string.Format("Failed to get local address, using {0}: {1}", (object) str, (object) ex.Message));
    }
    return str;
  }

  public static bool ContainsValidIP(string containsIPAddress, out string ipAddress)
  {
    string ipAddress1;
    if (ZNet.ContainsValidIPv4(containsIPAddress, out ipAddress1))
    {
      ipAddress = ipAddress1;
      return true;
    }
    ipAddress = "";
    return false;
  }

  private static bool ContainsValidIPv6(string potentialIPv6Address, out string ipAddress)
  {
    IPAddress address;
    if (IPAddress.TryParse(potentialIPv6Address, out address))
    {
      ipAddress = address.ToString();
      ZLog.Log((object) ("Found IPv6 address! Using " + ipAddress + "."));
      return true;
    }
    ipAddress = "";
    return false;
  }

  private static bool ContainsValidIPv4(string containsIPAddress, out string ipAddress)
  {
    MatchCollection matchCollection = new Regex("\\d{1,3}\\.\\d{1,3}\\.\\d{1,3}\\.\\d{1,3}").Matches(containsIPAddress);
    if (matchCollection.Count > 0)
    {
      ipAddress = matchCollection[0].ToString();
      return true;
    }
    ipAddress = "";
    return false;
  }

  public static string GetPublicIP(int ipGetAttempts = 0)
  {
    if (ZNet.m_httpClient == null)
      ZNet.m_httpClient = new HttpClient();
    try
    {
      string[] strArray1 = new string[5]
      {
        "https://ipv4.icanhazip.com/",
        "https://api.ipify.org",
        "https://ipv4.myip.wtf/text",
        "https://checkip.amazonaws.com/",
        "https://ipinfo.io/ip/"
      };
      string[] strArray2 = new string[3]
      {
        "https://ipv6.icanhazip.com/",
        "https://api6.ipify.org",
        "https://ipv6.myip.wtf/text"
      };
      System.Random random = new System.Random();
      string ipAddress;
      if (ZNet.ContainsValidIP(DownloadString(ipGetAttempts < 5 ? strArray1[random.Next(strArray1.Length)] : strArray2[random.Next(strArray2.Length)], 5000), out ipAddress))
        return ipAddress;
      throw new Exception("Could not extract valid IP address from externalIP download string.");
    }
    catch (Exception ex)
    {
      ZLog.LogError((object) ex.Message);
      return "";
    }

    static string DownloadString(string downloadUrl, int timeoutMS = 5000) => Task.Run<string>((Func<Task<string>>) (async () => await DownloadStringAsync(downloadUrl, timeoutMS))).Result;

    static async Task<string> DownloadStringAsync(string downloadUrl, int timeoutMS = 5000)
    {
      try
      {
        ZNet.m_httpClient.Timeout = TimeSpan.FromMilliseconds((double) timeoutMS);
        ZNet.m_httpClient.GetAsync(downloadUrl);
        HttpResponseMessage async = await ZNet.m_httpClient.GetAsync(downloadUrl);
        async.EnsureSuccessStatusCode();
        return await async.Content.ReadAsStringAsync();
      }
      catch (Exception ex)
      {
        Debug.LogError((object) ("Exception while waiting for respons from " + downloadUrl + " -> " + ex.ToString()));
        return string.Empty;
      }
    }
  }

  private void OnSteamServerRegistered(bool success)
  {
    if (success)
      return;
    ++this.m_registerAttempts;
    RetryRegisterAfterDelay(Mathf.Min(1f * Mathf.Pow(2f, (float) (this.m_registerAttempts - 1)), 30f) * UnityEngine.Random.Range(0.875f, 1.125f));

    void RetryRegisterAfterDelay(float delay) => this.StartCoroutine(DelayThenRegisterCoroutine(delay));

    IEnumerator DelayThenRegisterCoroutine(float delay)
    {
      ZNet znet = this;
      ZLog.Log((object) string.Format("Steam register server failed! Retrying in {0}s, total attempts: {1}", (object) delay, (object) znet.m_registerAttempts));
      DateTime NextRetryUtc = DateTime.UtcNow + TimeSpan.FromSeconds((double) delay);
      while (DateTime.UtcNow < NextRetryUtc)
        yield return (object) null;
      bool password = ZNet.m_serverPassword != "";
      GameVersion currentVersion = Version.CurrentVersion;
      uint networkVersion = 36;
      string[] array = ZNet.m_world.m_startingGlobalKeys.ToArray();
      ZSteamMatchmaking.instance.RegisterServer(ZNet.m_ServerName, password, currentVersion, array, networkVersion, ZNet.m_publicServer, ZNet.m_world.m_seedName, new ZSteamMatchmaking.ServerRegistered(znet.OnSteamServerRegistered));
    }
  }

  public void Shutdown(bool save = true)
  {
    ZLog.Log((object) "ZNet Shutdown");
    if (save)
      this.Save(true);
    this.StopAll();
    this.enabled = false;
  }

  public void ShutdownWithoutSave(bool suspending)
  {
    ZLog.Log((object) "ZNet Shutdown without save");
    this.StopAll(suspending);
    this.enabled = false;
  }

  private void StopAll(bool suspending = false)
  {
    if (this.m_haveStoped)
      return;
    this.m_haveStoped = true;
    if (this.m_saveThread != null && this.m_saveThread.IsAlive)
    {
      this.m_saveThread.Join();
      this.m_saveThread = (Thread) null;
    }
    if (!suspending)
      this.m_zdoMan.ShutDown();
    this.SendDisconnect();
    ZSteamMatchmaking.instance.ReleaseSessionTicket();
    ZSteamMatchmaking.instance.UnregisterServer();
    ZPlayFabMatchmaking.instance?.UnregisterServer();
    if (this.m_hostSocket != null)
      this.m_hostSocket.Dispose();
    if (this.m_serverConnector != null)
      this.m_serverConnector.Dispose();
    foreach (ZNetPeer peer in this.m_peers)
      peer.Dispose();
    this.m_peers.Clear();
  }

  private void OnDestroy()
  {
    ZLog.Log((object) "ZNet OnDestroy");
    if (!((UnityEngine.Object) ZNet.m_instance == (UnityEngine.Object) this))
      return;
    ZNet.m_instance = (ZNet) null;
  }

  private ZNetPeer Connect(ISocket socket)
  {
    ZNetPeer peer = new ZNetPeer(socket, true);
    this.OnNewConnection(peer);
    ZNet.m_connectionStatus = ZNet.ConnectionStatus.Connecting;
    ZNet.m_externalError = ZNet.ConnectionStatus.None;
    this.m_connectingDialog.gameObject.SetActive(true);
    return peer;
  }

  public void Connect(string remotePlayerId)
  {
    ZPlayFabSocket socket = (ZPlayFabSocket) null;
    ZNetPeer peer = (ZNetPeer) null;
    socket = new ZPlayFabSocket(remotePlayerId, new Action<PlayFabMatchmakingServerData>(CheckServerData));
    peer = this.Connect((ISocket) socket);

    void CheckServerData(PlayFabMatchmakingServerData serverData)
    {
      if (socket == null)
        return;
      if (serverData == null)
      {
        ZLog.LogWarning((object) ("Failed to join server '" + serverData.serverName + "' because the found session has incompatible data!"));
        ZNet.m_connectionStatus = ZNet.ConnectionStatus.ErrorVersion;
        this.Disconnect(peer);
      }
      else
      {
        if (serverData.platformRestriction == PlatformManager.DistributionPlatform.Platform || PlatformManager.DistributionPlatform.PrivilegeProvider.CheckPrivilege(Privilege.CrossPlatformMultiplayer) == PrivilegeResult.Granted)
          return;
        ZLog.LogWarning((object) string.Format("Failed to join server '{0}' due to the local user's privilege settings. The server owner's platform restrictions are '{1}'", (object) serverData.serverName, (object) serverData.platformRestriction));
        ZNet.m_connectionStatus = ZNet.ConnectionStatus.ErrorCrossplayPrivilege;
        this.Disconnect(peer);
        if (PlatformManager.DistributionPlatform.UIProvider.ResolvePrivilege == null)
          return;
        PlatformManager.DistributionPlatform.UIProvider.ResolvePrivilege.Open(Privilege.CrossPlatformMultiplayer);
      }
    }
  }

  public void Connect(CSteamID hostID) => this.Connect((ISocket) new ZSteamSocket(hostID));

  public void Connect(SteamNetworkingIPAddr host) => this.Connect((ISocket) new ZSteamSocket(host));

  public void Connect(string host, int port)
  {
    this.m_serverConnector = new ZConnector2(host, port);
    ZNet.m_connectionStatus = ZNet.ConnectionStatus.Connecting;
    ZNet.m_externalError = ZNet.ConnectionStatus.None;
    this.m_connectingDialog.gameObject.SetActive(true);
  }

  private void UpdateClientConnector(float dt)
  {
    if (this.m_serverConnector == null || !this.m_serverConnector.UpdateStatus(dt, true))
      return;
    ZSocket2 zsocket2 = this.m_serverConnector.Complete();
    if (zsocket2 != null)
    {
      ZLog.Log((object) ("Connection established to " + this.m_serverConnector.GetEndPointString()));
      this.OnNewConnection(new ZNetPeer((ISocket) zsocket2, true));
    }
    else
    {
      ZNet.m_connectionStatus = ZNet.ConnectionStatus.ErrorConnectFailed;
      ZLog.Log((object) "Failed to connect to server");
    }
    this.m_serverConnector.Dispose();
    this.m_serverConnector = (ZConnector2) null;
  }

  private void OnNewConnection(ZNetPeer peer)
  {
    this.m_peers.Add(peer);
    peer.m_rpc.Register<ZPackage>("PeerInfo", new Action<ZRpc, ZPackage>(this.RPC_PeerInfo));
    peer.m_rpc.Register("Disconnect", new ZRpc.RpcMethod.Method(this.RPC_Disconnect));
    peer.m_rpc.Register("SavePlayerProfile", new ZRpc.RpcMethod.Method(this.RPC_SavePlayerProfile));
    if (ZNet.m_isServer)
    {
      peer.m_rpc.Register("ServerHandshake", new ZRpc.RpcMethod.Method(this.RPC_ServerHandshake));
    }
    else
    {
      peer.m_rpc.Register("Kicked", new ZRpc.RpcMethod.Method(this.RPC_Kicked));
      peer.m_rpc.Register<int>("Error", new Action<ZRpc, int>(this.RPC_Error));
      peer.m_rpc.Register<bool, string>("ClientHandshake", new Action<ZRpc, bool, string>(this.RPC_ClientHandshake));
      peer.m_rpc.Invoke("ServerHandshake");
    }
  }

  public void SaveOtherPlayerProfiles()
  {
    ZLog.Log((object) "Sending message to save player profiles");
    if (!this.IsServer())
    {
      ZLog.Log((object) "Only server can save the player profiles");
    }
    else
    {
      foreach (ZNetPeer peer in this.m_peers)
      {
        if (peer.m_rpc != null)
        {
          ZLog.Log((object) ("Sent to " + peer.m_socket.GetEndPointString()));
          peer.m_rpc.Invoke("SavePlayerProfile");
        }
      }
    }
  }

  private void RPC_SavePlayerProfile(ZRpc rpc) => Game.instance.SavePlayerProfile(true);

  private void RPC_ServerHandshake(ZRpc rpc)
  {
    ZNetPeer peer = this.GetPeer(rpc);
    if (peer == null)
      return;
    ZLog.Log((object) ("Got handshake from client " + peer.m_socket.GetEndPointString()));
    this.ClearPlayerData(peer);
    bool flag = !string.IsNullOrEmpty(ZNet.m_serverPassword);
    peer.m_rpc.Invoke("ClientHandshake", (object) flag, (object) ZNet.ServerPasswordSalt());
  }

  public bool InPasswordDialog() => this.m_passwordDialog.gameObject.activeSelf;

  public bool InConnectingScreen() => this.m_connectingDialog.gameObject.activeSelf;

  private void RPC_ClientHandshake(ZRpc rpc, bool needPassword, string serverPasswordSalt)
  {
    this.m_connectingDialog.gameObject.SetActive(false);
    ZNet.m_serverPasswordSalt = serverPasswordSalt;
    if (needPassword)
    {
      this.m_passwordDialog.gameObject.SetActive(true);
      GuiInputField componentInChildren = this.m_passwordDialog.GetComponentInChildren<GuiInputField>();
      componentInChildren.text = "";
      componentInChildren.ActivateInputField();
      componentInChildren.OnInputSubmit.AddListener(new UnityAction<string>(this.OnPasswordEntered));
      this.m_tempPasswordRPC = rpc;
      if (FejdStartup.ServerPassword == null)
        return;
      this.OnPasswordEntered(FejdStartup.ServerPassword);
    }
    else
      this.SendPeerInfo(rpc);
  }

  private void OnPasswordEntered(string pwd)
  {
    if (!this.m_tempPasswordRPC.IsConnected() || string.IsNullOrEmpty(pwd))
      return;
    this.m_passwordDialog.GetComponentInChildren<GuiInputField>().OnInputSubmit.RemoveListener(new UnityAction<string>(this.OnPasswordEntered));
    this.m_passwordDialog.gameObject.SetActive(false);
    this.SendPeerInfo(this.m_tempPasswordRPC, pwd);
    this.m_tempPasswordRPC = (ZRpc) null;
  }

  private void SendPeerInfo(ZRpc rpc, string password = "")
  {
    ZPackage zpackage = new ZPackage();
    zpackage.Write(ZNet.GetUID());
    zpackage.Write(Version.CurrentVersion.ToString());
    zpackage.Write(36U);
    zpackage.Write(this.m_referencePosition);
    zpackage.Write(Game.instance.GetPlayerProfile().GetName());
    if (this.IsServer())
    {
      zpackage.Write(ZNet.m_world.m_name);
      zpackage.Write(ZNet.m_world.m_seed);
      zpackage.Write(ZNet.m_world.m_seedName);
      zpackage.Write(ZNet.m_world.m_uid);
      zpackage.Write(ZNet.m_world.m_worldGenVersion);
      zpackage.Write(this.m_netTime);
    }
    else
    {
      string data = string.IsNullOrEmpty(password) ? "" : ZNet.HashPassword(password, ZNet.ServerPasswordSalt());
      zpackage.Write(data);
      rpc.GetSocket().GetHostName();
      SteamNetworkingIdentity serverIdentity = new SteamNetworkingIdentity();
      serverIdentity.SetSteamID(new CSteamID(ZNet.m_serverSteamID));
      byte[] array = ZSteamMatchmaking.instance.RequestSessionTicket(ref serverIdentity);
      if (array == null)
      {
        ZNet.m_connectionStatus = ZNet.ConnectionStatus.ErrorConnectFailed;
        return;
      }
      zpackage.Write(array);
    }
    rpc.Invoke("PeerInfo", (object) zpackage);
  }

  private void RPC_PeerInfo(ZRpc rpc, ZPackage pkg)
  {
    ZNetPeer peer = this.GetPeer(rpc);
    if (peer == null)
      return;
    long uid = pkg.ReadLong();
    string versionString = pkg.ReadString();
    uint num1 = 0;
    GameVersion gameVersion;
    ref GameVersion local = ref gameVersion;
    if (GameVersion.TryParseGameVersion(versionString, out local) && gameVersion >= Version.FirstVersionWithNetworkVersion)
      num1 = pkg.ReadUInt();
    string name = peer.m_socket.GetEndPointString();
    string hostName = peer.m_socket.GetHostName();
    string str1 = num1.ToString();
    uint num2 = 36;
    string str2 = num2.ToString();
    ZLog.Log((object) ("Network version check, their:" + str1 + ", mine:" + str2));
    if (num1 != 36U)
    {
      if (ZNet.m_isServer)
        rpc.Invoke("Error", (object) 3);
      else
        ZNet.m_connectionStatus = ZNet.ConnectionStatus.ErrorVersion;
      string[] strArray = new string[11];
      strArray[0] = "Peer ";
      strArray[1] = name;
      strArray[2] = " has incompatible version, mine:";
      strArray[3] = Version.CurrentVersion.ToString();
      strArray[4] = " (network version ";
      num2 = 36U;
      strArray[5] = num2.ToString();
      strArray[6] = ")   remote ";
      strArray[7] = gameVersion.ToString();
      strArray[8] = " (network version ";
      strArray[9] = num1 == uint.MaxValue ? "unknown" : num1.ToString();
      strArray[10] = ")";
      ZLog.Log((object) string.Concat(strArray));
    }
    else
    {
      Vector3 vector3 = pkg.ReadVector3();
      string playerName = pkg.ReadString();
      if (ZNet.m_isServer)
      {
        if (!this.IsAllowed(hostName, playerName))
        {
          rpc.Invoke("Error", (object) 8);
          ZLog.Log((object) ("Player " + playerName + " : " + hostName + " is blacklisted or not in whitelist."));
          return;
        }
        string str3 = pkg.ReadString();
        if (ZNet.m_onlineBackend == OnlineBackendType.Steamworks)
        {
          ZSteamSocket socket = peer.m_socket as ZSteamSocket;
          if (!ZSteamMatchmaking.instance.VerifySessionTicket(pkg.ReadByteArray(), socket.GetPeerID()))
          {
            ZLog.Log((object) ("Peer " + name + " has invalid session ticket"));
            rpc.Invoke("Error", (object) 8);
            return;
          }
        }
        if (ZNet.m_onlineBackend == OnlineBackendType.PlayFab)
        {
          PlatformUserID platformUserId = new PlatformUserID(peer.m_socket.GetHostName());
          if (!platformUserId.IsValid)
          {
            ZLog.LogError((object) "Failed to parse peer id! Using blank ID with unknown platform.");
            platformUserId = PlatformUserID.None;
          }
          if (PlatformManager.DistributionPlatform.PrivilegeProvider.CheckPrivilege(Privilege.CrossPlatformMultiplayer) != PrivilegeResult.Granted && PlatformManager.DistributionPlatform.Platform != platformUserId.m_platform)
          {
            rpc.Invoke("Error", (object) 10);
            Platform platform = PlatformManager.DistributionPlatform.Platform;
            string str4 = platform.ToString();
            platform = platformUserId.m_platform;
            string str5 = platform.ToString();
            ZLog.Log((object) ("Peer diconnected due to server platform privileges disallowing crossplay. Server platform: " + str4 + "   Peer platform: " + str5));
            return;
          }
          PlayFabManager.CheckIfUserAuthenticated((peer.m_socket as ZPlayFabSocket).m_remotePlayerId, platformUserId, (Action<bool>) (isAuthenticated =>
          {
            if (isAuthenticated)
              return;
            rpc.Invoke("Error", (object) 5);
            ZLog.Log((object) ("Peer " + name + " disconnected because they were not authenticated!"));
          }));
        }
        if (this.GetNrOfPlayers() >= 10)
        {
          rpc.Invoke("Error", (object) 9);
          ZLog.Log((object) ("Peer " + name + " disconnected due to server is full"));
          return;
        }
        if (ZNet.m_serverPassword != str3)
        {
          rpc.Invoke("Error", (object) 6);
          ZLog.Log((object) ("Peer " + name + " has wrong password"));
          return;
        }
        if (this.IsConnected(uid))
        {
          rpc.Invoke("Error", (object) 7);
          ZLog.Log((object) ("Already connected to peer with UID:" + uid.ToString() + "  " + name));
          return;
        }
      }
      else
      {
        ZNet.m_world = new World();
        ZNet.m_world.m_name = pkg.ReadString();
        ZNet.m_world.m_seed = pkg.ReadInt();
        ZNet.m_world.m_seedName = pkg.ReadString();
        ZNet.m_world.m_uid = pkg.ReadLong();
        ZNet.m_world.m_worldGenVersion = pkg.ReadInt();
        WorldGenerator.Initialize(ZNet.m_world);
        this.m_netTime = pkg.ReadDouble();
      }
      peer.m_refPos = vector3;
      peer.m_uid = uid;
      peer.m_playerName = playerName;
      rpc.Register<ZPackage>("ServerSyncedPlayerData", new Action<ZRpc, ZPackage>(this.RPC_ServerSyncedPlayerData));
      rpc.Register<ZPackage>("PlayerList", new Action<ZRpc, ZPackage>(this.RPC_PlayerList));
      rpc.Register<ZPackage>("AdminList", new Action<ZRpc, ZPackage>(this.RPC_AdminList));
      rpc.Register<string>("RemotePrint", new Action<ZRpc, string>(this.RPC_RemotePrint));
      if (ZNet.m_isServer)
      {
        rpc.Register<ZDOID>("CharacterID", new Action<ZRpc, ZDOID>(this.RPC_CharacterID));
        rpc.Register<string>("Kick", new Action<ZRpc, string>(this.RPC_Kick));
        rpc.Register<string>("Ban", new Action<ZRpc, string>(this.RPC_Ban));
        rpc.Register<string>("Unban", new Action<ZRpc, string>(this.RPC_Unban));
        rpc.Register<string>("RPC_RemoteCommand", new Action<ZRpc, string>(this.RPC_RemoteCommand));
        rpc.Register("Save", new ZRpc.RpcMethod.Method(this.RPC_Save));
        rpc.Register("PrintBanned", new ZRpc.RpcMethod.Method(this.RPC_PrintBanned));
      }
      else
        rpc.Register<double>("NetTime", new Action<ZRpc, double>(this.RPC_NetTime));
      if (ZNet.m_isServer)
      {
        this.SendPeerInfo(rpc);
        peer.m_socket.VersionMatch();
        this.SendPlayerList();
        this.SendAdminList();
      }
      else
      {
        peer.m_socket.VersionMatch();
        ZNet.m_connectionStatus = ZNet.ConnectionStatus.Connected;
      }
      this.m_zdoMan.AddPeer(peer);
      this.m_routedRpc.AddPeer(peer);
    }
  }

  private void SendDisconnect()
  {
    ZLog.Log((object) "Sending disconnect msg");
    foreach (ZNetPeer peer in this.m_peers)
      this.SendDisconnect(peer);
  }

  private void SendDisconnect(ZNetPeer peer)
  {
    if (peer.m_rpc == null)
      return;
    ZLog.Log((object) ("Sent to " + peer.m_socket.GetEndPointString()));
    peer.m_rpc.Invoke("Disconnect");
  }

  private void RPC_Disconnect(ZRpc rpc)
  {
    ZLog.Log((object) nameof (RPC_Disconnect));
    ZNetPeer peer = this.GetPeer(rpc);
    if (peer == null)
      return;
    if (peer.m_server)
      ZNet.m_connectionStatus = ZNet.ConnectionStatus.ErrorDisconnected;
    this.Disconnect(peer);
  }

  private void RPC_Error(ZRpc rpc, int error)
  {
    ZNet.ConnectionStatus connectionStatus = (ZNet.ConnectionStatus) error;
    ZNet.m_connectionStatus = connectionStatus;
    ZLog.Log((object) ("Got connectoin error msg " + connectionStatus.ToString()));
  }

  public bool IsConnected(long uid)
  {
    if (uid == ZNet.GetUID())
      return true;
    foreach (ZNetPeer peer in this.m_peers)
    {
      if (peer.m_uid == uid)
        return true;
    }
    return false;
  }

  private void ClearPlayerData(ZNetPeer peer)
  {
    this.m_routedRpc.RemovePeer(peer);
    this.m_zdoMan.RemovePeer(peer);
  }

  public void Disconnect(ZNetPeer peer)
  {
    this.ClearPlayerData(peer);
    this.m_peers.Remove(peer);
    peer.Dispose();
    if (!ZNet.m_isServer)
      return;
    this.SendPlayerList();
  }

  private void FixedUpdate() => this.UpdateNetTime(Time.fixedDeltaTime);

  private void Update()
  {
    float deltaTime = Time.deltaTime;
    ZSteamSocket.UpdateAllSockets(deltaTime);
    ZPlayFabSocket.UpdateAllSockets(deltaTime);
    if (this.IsServer())
      this.UpdateBanList(deltaTime);
    this.CheckForIncommingServerConnections();
    this.UpdatePeers(deltaTime);
    this.SendPeriodicData(deltaTime);
    this.m_zdoMan.Update(deltaTime);
    this.UpdateSave();
    if (ZNet.PeersToDisconnectAfterKick.Count < 1)
      return;
    foreach (ZNetPeer znetPeer in ZNet.PeersToDisconnectAfterKick.Keys.ToArray<ZNetPeer>())
    {
      if ((double) Time.time >= (double) ZNet.PeersToDisconnectAfterKick[znetPeer])
      {
        this.Disconnect(znetPeer);
        ZNet.PeersToDisconnectAfterKick.Remove(znetPeer);
      }
    }
  }

  private void LateUpdate() => ZPlayFabSocket.LateUpdateAllSocket();

  private void UpdateNetTime(float dt)
  {
    if (this.IsServer())
    {
      if (this.GetNrOfPlayers() <= 0)
        return;
      this.m_netTime += (double) dt;
    }
    else
      this.m_netTime += (double) dt;
  }

  private void UpdateBanList(float dt)
  {
    this.m_banlistTimer += dt;
    if ((double) this.m_banlistTimer <= 5.0)
      return;
    this.m_banlistTimer = 0.0f;
    this.CheckWhiteList();
    foreach (string user in this.m_bannedList.GetList())
      this.InternalKick(user);
  }

  private void CheckWhiteList()
  {
    if (this.m_permittedList.Count() == 0)
      return;
    foreach (ZNetPeer peer in this.m_peers)
    {
      if (peer.IsReady() && !ZNet.PeersToDisconnectAfterKick.ContainsKey(peer))
      {
        string hostName = peer.m_socket.GetHostName();
        if (!this.ListContainsId(this.m_permittedList, hostName))
        {
          ZLog.Log((object) ("Kicking player not in permitted list " + peer.m_playerName + " host: " + hostName));
          this.InternalKick(peer);
        }
      }
    }
  }

  public bool IsSaving() => this.m_saveThread != null;

  public void SaveWorldAndPlayerProfiles()
  {
    if (this.IsServer())
      this.RPC_Save((ZRpc) null);
    else
      this.GetServerRPC()?.Invoke("Save");
  }

  private void RPC_Save(ZRpc rpc)
  {
    if (!this.enabled)
      return;
    if (rpc != null && !this.ListContainsId(this.m_adminList, rpc.GetSocket().GetHostName()))
    {
      this.RemotePrint(rpc, "You are not admin");
    }
    else
    {
      if (!this.EnoughDiskSpaceAvailable(out bool _))
        return;
      this.RemotePrint(rpc, "Saving..");
      Game.instance.SavePlayerProfile(true);
      this.Save(false, true, !this.IsDedicated());
    }
  }

  private bool ListContainsId(SyncedList list, string idString)
  {
    PlatformUserID platform;
    if (!PlatformUserID.TryParse(idString, out platform))
      platform = new PlatformUserID(this.m_steamPlatform, idString);
    if (!(platform.m_platform == this.m_steamPlatform))
      return list.Contains(platform.ToString());
    return list.Contains(platform.ToString()) || list.Contains(platform.m_userID.ToString());
  }

  public void Save(bool sync, bool saveOtherPlayerProfiles = false, bool waitForNextFrame = false)
  {
    Game.instance.m_saveTimer = 0.0f;
    if (ZNet.m_loadError || ZoneSystem.instance.SkipSaving() || DungeonDB.instance.SkipSaving())
    {
      ZLog.LogWarning((object) "Skipping world save");
    }
    else
    {
      if (!ZNet.m_isServer || ZNet.m_world == null)
        return;
      if (saveOtherPlayerProfiles)
        this.SaveOtherPlayerProfiles();
      if (!waitForNextFrame)
        this.SaveWorld(sync);
      else
        this.StartCoroutine(this.DelayedSave(sync));
    }
  }

  private IEnumerator DelayedSave(bool sync)
  {
    yield return (object) null;
    this.SaveWorld(sync);
  }

  public bool EnoughDiskSpaceAvailable(
    out bool exitGamePopupShown,
    bool exitGamePrompt = false,
    Action<bool> onDecisionMade = null)
  {
    exitGamePopupShown = false;
    string worldSavePath = "";
    World worldIfIsHost = ZNet.GetWorldIfIsHost();
    FileHelpers.FileSource worldFileSource = FileHelpers.FileSource.Cloud;
    if (worldIfIsHost != null)
    {
      worldSavePath = worldIfIsHost.GetDBPath();
      worldFileSource = worldIfIsHost.m_fileSource;
    }
    PlayerProfile playerProfile = Game.instance.GetPlayerProfile();
    ulong availableFreeSpace;
    ulong byteLimitWarning;
    ulong byteLimitBlock;
    FileHelpers.CheckDiskSpace(worldSavePath, playerProfile.GetPath(), worldFileSource, playerProfile.m_fileSource, out availableFreeSpace, out byteLimitWarning, out byteLimitBlock);
    if ((availableFreeSpace <= byteLimitBlock ? 1 : (availableFreeSpace <= byteLimitWarning ? 1 : 0)) != 0)
      this.LowDiskLeftInformer(availableFreeSpace, byteLimitWarning, byteLimitBlock, exitGamePrompt, onDecisionMade);
    if (availableFreeSpace > byteLimitBlock)
      return true;
    if (exitGamePrompt)
      exitGamePopupShown = true;
    ZLog.LogWarning((object) "Not enough space left to save. ");
    return false;
  }

  private void LowDiskLeftInformer(
    ulong availableFreeSpace,
    ulong byteLimitWarning,
    ulong byteLimitBlock,
    bool exitGamePrompt,
    Action<bool> onDecisionMade)
  {
    if (availableFreeSpace <= byteLimitBlock)
    {
      if (this.IsDedicated())
        MessageHud.instance.MessageAll(MessageHud.MessageType.Center, "$msg_worldsaveblockedonserver");
      else if (exitGamePrompt)
        UnifiedPopup.Push((PopupBase) new YesNoPopup("$menu_lowdisk_block_exitanyway_header", "$menu_lowdisk_block_exitanyway_prompt", (PopupButtonCallback) (() =>
        {
          Action<bool> action = onDecisionMade;
          if (action != null)
            action(true);
          UnifiedPopup.Pop();
        }), (PopupButtonCallback) (() =>
        {
          Action<bool> action = onDecisionMade;
          if (action != null)
            action(false);
          UnifiedPopup.Pop();
        })));
      else
        this.SavingBlockedPopup();
    }
    else if (this.IsDedicated())
      MessageHud.instance.MessageAll(MessageHud.MessageType.Center, "$msg_worldsavewarningonserver");
    else
      this.SaveLowDiskWarningPopup();
    ZLog.LogWarning((object) string.Format("Running low on disk space... Available space: {0} bytes.", (object) availableFreeSpace));
  }

  private void SavingBlockedPopup() => UnifiedPopup.Push((PopupBase) new WarningPopup("$menu_lowdisk_header_block", "$menu_lowdisk_message_block", (PopupButtonCallback) (() => UnifiedPopup.Pop())));

  private void SaveLowDiskWarningPopup() => UnifiedPopup.Push((PopupBase) new WarningPopup("$menu_lowdisk_header_warn", "$menu_lowdisk_message_warn", (PopupButtonCallback) (() => UnifiedPopup.Pop())));

  public bool LocalPlayerIsAdminOrHost() => this.IsServer() || this.PlayerIsAdmin(UserInfo.GetLocalUser().UserId);

  public bool PlayerIsAdmin(PlatformUserID networkUserId)
  {
    List<string> adminList = this.GetAdminList();
    return networkUserId.IsValid && adminList != null && adminList.Contains(networkUserId.ToString());
  }

  public static World GetWorldIfIsHost() => ZNet.m_isServer ? ZNet.m_world : (World) null;

  private void SendPeriodicData(float dt)
  {
    this.m_periodicSendTimer += dt;
    if ((double) this.m_periodicSendTimer < 2.0)
      return;
    this.m_periodicSendTimer = 0.0f;
    if (this.IsServer())
    {
      this.SendNetTime();
      this.SendPlayerList();
    }
    else
    {
      foreach (ZNetPeer peer in this.m_peers)
      {
        if (peer.IsReady())
          this.SendServerSyncPlayerData(peer);
      }
    }
  }

  private void SendServerSyncPlayerData(ZNetPeer peer)
  {
    ZPackage zpackage = new ZPackage();
    zpackage.Write(this.m_referencePosition);
    zpackage.Write(this.m_publicReferencePosition);
    zpackage.Write(this.m_serverSyncedPlayerData.Count);
    foreach (KeyValuePair<string, string> keyValuePair in this.m_serverSyncedPlayerData)
    {
      zpackage.Write(keyValuePair.Key);
      zpackage.Write(keyValuePair.Value);
    }
    peer.m_rpc.Invoke("ServerSyncedPlayerData", (object) zpackage);
  }

  private void SendNetTime()
  {
    foreach (ZNetPeer peer in this.m_peers)
    {
      if (peer.IsReady())
        peer.m_rpc.Invoke("NetTime", (object) this.m_netTime);
    }
  }

  private void RPC_NetTime(ZRpc rpc, double time) => this.m_netTime = time;

  private void RPC_ServerSyncedPlayerData(ZRpc rpc, ZPackage data)
  {
    RandEventSystem.SetRandomEventsNeedsRefresh();
    ZNetPeer peer = this.GetPeer(rpc);
    if (peer == null)
      return;
    peer.m_refPos = data.ReadVector3();
    peer.m_publicRefPos = data.ReadBool();
    peer.m_serverSyncedPlayerData.Clear();
    int num = data.ReadInt();
    for (int index = 0; index < num; ++index)
      peer.m_serverSyncedPlayerData.Add(data.ReadString(), data.ReadString());
  }

  private void UpdatePeers(float dt)
  {
    foreach (ZNetPeer peer in this.m_peers)
    {
      if (!peer.m_rpc.IsConnected())
      {
        if (peer.m_server)
          ZNet.m_connectionStatus = ZNet.m_externalError == ZNet.ConnectionStatus.None ? (ZNet.m_connectionStatus != ZNet.ConnectionStatus.Connecting ? ZNet.ConnectionStatus.ErrorDisconnected : ZNet.ConnectionStatus.ErrorConnectFailed) : ZNet.m_externalError;
        this.Disconnect(peer);
        break;
      }
    }
    this.m_peersCopy.Clear();
    this.m_peersCopy.AddRange((IEnumerable<ZNetPeer>) this.m_peers);
    foreach (ZNetPeer znetPeer in this.m_peersCopy)
    {
      if (znetPeer.m_rpc.Update(dt) == ZRpc.ErrorCode.IncompatibleVersion)
        ZNet.m_connectionStatus = ZNet.ConnectionStatus.ErrorVersion;
    }
  }

  private void CheckForIncommingServerConnections()
  {
    if (this.m_hostSocket == null)
      return;
    ISocket socket = this.m_hostSocket.Accept();
    if (socket == null)
      return;
    if (!socket.IsConnected())
      socket.Dispose();
    else
      this.OnNewConnection(new ZNetPeer(socket, false));
  }

  public ZNetPeer GetPeerByPlayerName(string name)
  {
    foreach (ZNetPeer peer in this.m_peers)
    {
      if (peer.IsReady() && peer.m_playerName == name)
        return peer;
    }
    return (ZNetPeer) null;
  }

  public ZNetPeer GetPeerByHostName(string endpoint)
  {
    foreach (ZNetPeer peer in this.m_peers)
    {
      if (peer.IsReady() && peer.m_socket.GetHostName() == endpoint)
        return peer;
    }
    return (ZNetPeer) null;
  }

  public ZNetPeer GetPeer(long uid)
  {
    foreach (ZNetPeer peer in this.m_peers)
    {
      if (peer.m_uid == uid)
        return peer;
    }
    return (ZNetPeer) null;
  }

  private ZNetPeer GetPeer(ZRpc rpc)
  {
    foreach (ZNetPeer peer in this.m_peers)
    {
      if (peer.m_rpc == rpc)
        return peer;
    }
    return (ZNetPeer) null;
  }

  public List<ZNetPeer> GetConnectedPeers() => new List<ZNetPeer>((IEnumerable<ZNetPeer>) this.m_peers);

  private void SaveWorld(bool sync)
  {
    Action worldSaveStarted = ZNet.WorldSaveStarted;
    if (worldSaveStarted != null)
      worldSaveStarted();
    if (this.m_saveThread != null && this.m_saveThread.IsAlive)
    {
      this.m_saveThread.Join();
      this.m_saveThread = (Thread) null;
    }
    this.m_saveStartTime = Time.realtimeSinceStartup;
    this.m_zdoMan.PrepareSave();
    ZoneSystem.instance.PrepareSave();
    RandEventSystem.instance.PrepareSave();
    ZNet.m_backupCount = PlatformPrefs.GetInt("AutoBackups", ZNet.m_backupCount);
    this.m_saveThreadStartTime = Time.realtimeSinceStartup;
    this.m_saveThread = new Thread(new ThreadStart(this.SaveWorldThread));
    this.m_saveThread.Start();
    if (!sync)
      return;
    this.m_saveThread.Join();
    this.m_saveThread = (Thread) null;
    this.m_sendSaveMessage = 0.5f;
  }

  private void UpdateSave()
  {
    if ((double) this.m_sendSaveMessage > 0.0)
    {
      this.m_sendSaveMessage -= Time.fixedDeltaTime;
      if ((double) this.m_sendSaveMessage < 0.0)
      {
        this.PrintWorldSaveMessage();
        this.m_sendSaveMessage = 0.0f;
      }
    }
    if (this.m_saveThread == null || this.m_saveThread.IsAlive)
      return;
    this.m_saveThread = (Thread) null;
    this.m_sendSaveMessage = 0.5f;
  }

  private void PrintWorldSaveMessage()
  {
    float num1 = this.m_saveThreadStartTime - this.m_saveStartTime;
    float num2 = Time.realtimeSinceStartup - this.m_saveThreadStartTime;
    this.m_saveDoneTime = Time.realtimeSinceStartup;
    if (this.m_saveExceededCloudQuota)
    {
      this.m_saveExceededCloudQuota = false;
      MessageHud.instance.MessageAll(MessageHud.MessageType.TopLeft, "$msg_worldsavedcloudstoragefull ( " + num1.ToString("0.00") + "+" + num2.ToString("0.00") + "s )");
    }
    else
      MessageHud.instance.MessageAll(MessageHud.MessageType.TopLeft, "$msg_worldsaved ( " + num1.ToString("0.00") + "+" + num2.ToString("0.00") + "s )");
    Action worldSaveFinished = ZNet.WorldSaveFinished;
    if (worldSaveFinished == null)
      return;
    worldSaveFinished();
  }

  private void SaveWorldThread()
  {
    DateTime now1 = DateTime.Now;
    try
    {
      ulong opUsage = 52428800UL + FileHelpers.GetFileSize(ZNet.m_world.GetMetaPath(), ZNet.m_world.m_fileSource);
      if (FileHelpers.Exists(ZNet.m_world.GetDBPath(), ZNet.m_world.m_fileSource))
        opUsage += FileHelpers.GetFileSize(ZNet.m_world.GetDBPath(), ZNet.m_world.m_fileSource);
      bool flag1 = SaveSystem.CheckMove(ZNet.m_world.m_fileName, SaveDataType.World, ref ZNet.m_world.m_fileSource, now1, opUsage);
      bool flag2 = ZNet.m_world.m_createBackupBeforeSaving && !flag1;
      if (FileHelpers.CloudStorageEnabled && ZNet.m_world.m_fileSource == FileHelpers.FileSource.Cloud && FileHelpers.OperationExceedsCloudCapacity(opUsage * (flag2 ? 3UL : 2UL)))
      {
        if (!FileHelpers.LocalStorageSupported)
          throw new Exception("The world save operation may exceed the cloud save quota and was therefore not performed!");
        string metaPath1 = ZNet.m_world.GetMetaPath();
        string dbPath1 = ZNet.m_world.GetDBPath();
        ZNet.m_world.m_fileSource = FileHelpers.FileSource.Local;
        string metaPath2 = ZNet.m_world.GetMetaPath();
        string dbPath2 = ZNet.m_world.GetDBPath();
        string target = metaPath2;
        FileHelpers.FileCopyOutFromCloud(metaPath1, target, true);
        if (FileHelpers.FileExistsCloud(dbPath1))
          FileHelpers.FileCopyOutFromCloud(dbPath1, dbPath2, true);
        SaveSystem.InvalidateCache();
        ZLog.LogWarning((object) "The world save operation may exceed the cloud save quota and it has therefore been moved to local storage!");
        this.m_saveExceededCloudQuota = true;
      }
      if (flag2)
      {
        SaveWithBackups save;
        if (SaveSystem.TryGetSaveByName(ZNet.m_world.m_fileName, SaveDataType.World, out save) && !save.IsDeleted)
        {
          if (SaveSystem.CreateBackup(save.PrimaryFile, DateTime.Now, ZNet.m_world.m_fileSource))
            ZLog.Log((object) "Migrating world save from an old save format, created backup!");
          else
            ZLog.LogError((object) ("Failed to create backup of world save " + ZNet.m_world.m_fileName + "!"));
        }
        else
          ZLog.LogError((object) ("Failed to get world save " + ZNet.m_world.m_fileName + " from save system, so a backup couldn't be created!"));
      }
      ZNet.m_world.m_createBackupBeforeSaving = false;
      DateTime now2 = DateTime.Now;
      bool flag3 = ZNet.m_world.m_fileSource != FileHelpers.FileSource.Cloud;
      string dbPath = ZNet.m_world.GetDBPath();
      string str1 = flag3 ? dbPath + ".new" : dbPath;
      string oldFile = dbPath + ".old";
      ZLog.Log((object) "World save writing starting");
      FileWriter fileWriter = new FileWriter(str1, fileSource: ZNet.m_world.m_fileSource);
      ZLog.Log((object) "World save writing started");
      BinaryWriter binary = fileWriter.m_binary;
      binary.Write(37);
      binary.Write(this.m_netTime);
      this.m_zdoMan.SaveAsync(binary);
      ZoneSystem.instance.SaveASync(binary);
      RandEventSystem.instance.SaveAsync(binary);
      ZLog.Log((object) "World save writing finishing");
      fileWriter.Finish();
      SaveSystem.InvalidateCache();
      ZLog.Log((object) "World save writing finished");
      ZNet.m_world.m_needsDB = true;
      FileWriter metaWriter;
      ZNet.m_world.SaveWorldMetaData(now1, false, out bool _, out metaWriter);
      if (ZNet.m_world.m_fileSource == FileHelpers.FileSource.Cloud && (metaWriter.Status == FileWriter.WriterStatus.CloseFailed || fileWriter.Status == FileWriter.WriterStatus.CloseFailed))
      {
        string backupPath1 = GetBackupPath(ZNet.m_world.GetMetaPath(FileHelpers.FileSource.Local), now1);
        string backupPath2 = GetBackupPath(ZNet.m_world.GetDBPath(FileHelpers.FileSource.Local), now1);
        metaWriter.DumpCloudWriteToLocalFile(backupPath1);
        fileWriter.DumpCloudWriteToLocalFile(backupPath2);
        SaveSystem.InvalidateCache();
        string str2 = "";
        if (metaWriter.Status == FileWriter.WriterStatus.CloseFailed)
          str2 = str2 + "Cloud save to location \"" + ZNet.m_world.GetMetaPath() + "\" failed!\n";
        if (fileWriter.Status == FileWriter.WriterStatus.CloseFailed)
          str2 = str2 + "Cloud save to location \"" + dbPath + "\" failed!\n ";
        ZLog.LogError((object) (str2 + "Saved world as local backup \"" + backupPath1 + "\" and \"" + backupPath2 + "\". Use the \"Manage saves\" menu to restore this backup."));
      }
      else
      {
        if (flag3)
        {
          FileHelpers.ReplaceOldFile(dbPath, str1, oldFile, ZNet.m_world.m_fileSource);
          SaveSystem.InvalidateCache();
        }
        ZLog.Log((object) ("World saved ( " + (DateTime.Now - now2).TotalMilliseconds.ToString() + "ms )"));
        DateTime now3 = DateTime.Now;
        if (!ZNet.ConsiderAutoBackup(ZNet.m_world.m_fileName, SaveDataType.World, now1))
          return;
        ZLog.Log((object) ("World auto backup saved ( " + (DateTime.Now - now3).ToString() + "ms )"));
      }
    }
    catch (Exception ex)
    {
      ZLog.LogError((object) ("Error saving world! " + ex.Message));
      Terminal.m_threadSafeMessages.Enqueue("Error saving world! See log or console.");
      Terminal.m_threadSafeConsoleLog.Enqueue("Error saving world! " + ex.Message);
    }

    static string GetBackupPath(string filePath, DateTime now)
    {
      string directory;
      string fileName;
      string fileExtension;
      FileHelpers.SplitFilePath(filePath, out directory, out fileName, out fileExtension);
      return directory + fileName + "_backup_cloud-" + now.ToString("yyyyMMdd-HHmmss") + fileExtension;
    }
  }

  public static bool ConsiderAutoBackup(string saveName, SaveDataType dataType, DateTime now)
  {
    int num = 1200;
    int backupCount = ZNet.m_backupCount == 1 ? 0 : ZNet.m_backupCount;
    string s1;
    int result1;
    string s2;
    int result2;
    string s3;
    int result3;
    return backupCount > 0 && SaveSystem.ConsiderBackup(saveName, dataType, now, backupCount, !Terminal.m_testList.TryGetValue("autoshort", out s1) || !int.TryParse(s1, out result1) ? ZNet.m_backupShort : result1, !Terminal.m_testList.TryGetValue("autolong", out s2) || !int.TryParse(s2, out result2) ? ZNet.m_backupLong : result2, !Terminal.m_testList.TryGetValue("autowait", out s3) || !int.TryParse(s3, out result3) ? num : result3, (bool) (UnityEngine.Object) ZoneSystem.instance ? ZoneSystem.instance.TimeSinceStart() : 0.0f);
  }

  private void LoadWorld()
  {
    ZLog.Log((object) ("Load world: " + ZNet.m_world.m_name + " (" + ZNet.m_world.m_fileName + ")"));
    string dbPath = ZNet.m_world.GetDBPath();
    FileReader fileReader;
    try
    {
      fileReader = new FileReader(dbPath, ZNet.m_world.m_fileSource);
    }
    catch
    {
      ZLog.Log((object) ("  missing " + dbPath));
      this.WorldSetup();
      return;
    }
    BinaryReader binary = fileReader.m_binary;
    try
    {
      int version;
      if (!this.CheckDataVersion(binary, out version))
      {
        ZLog.Log((object) ("  incompatible data version " + version.ToString()));
        ZNet.m_loadError = true;
        binary.Close();
        fileReader.Dispose();
        this.WorldSetup();
        return;
      }
      if (version >= 4)
        this.m_netTime = binary.ReadDouble();
      this.m_zdoMan.Load(binary, version);
      if (version >= 12)
        ZoneSystem.instance.Load(binary, version);
      if (version >= 15)
        RandEventSystem.instance.Load(binary, version);
      fileReader.Dispose();
      this.WorldSetup();
    }
    catch (Exception ex)
    {
      ZLog.LogError((object) ("Exception while loading world " + dbPath + ":" + ex.ToString()));
      ZNet.m_loadError = true;
    }
    Game.instance.CollectResources();
  }

  private bool CheckDataVersion(BinaryReader reader, out int version)
  {
    version = reader.ReadInt32();
    return Version.IsWorldVersionCompatible(version);
  }

  private void WorldSetup()
  {
    ZoneSystem.instance.SetStartingGlobalKeys();
    ZNet.m_world.m_startingKeysChanged = false;
  }

  public int GetHostPort() => this.m_hostSocket != null ? this.m_hostSocket.GetHostPort() : 0;

  public static long GetUID() => ZDOMan.GetSessionID();

  public long GetWorldUID() => ZNet.m_world.m_uid;

  public string GetWorldName() => ZNet.m_world != null ? ZNet.m_world.m_name : (string) null;

  public void SetCharacterID(ZDOID id)
  {
    this.m_characterID = id;
    if (ZNet.m_isServer)
      return;
    this.m_peers[0].m_rpc.Invoke("CharacterID", (object) id);
  }

  private void RPC_CharacterID(ZRpc rpc, ZDOID characterID)
  {
    ZNetPeer peer = this.GetPeer(rpc);
    if (peer == null)
      return;
    peer.m_characterID = characterID;
    ZLog.Log((object) ("Got character ZDOID from " + peer.m_playerName + " : " + characterID.ToString()));
  }

  public void SetPublicReferencePosition(bool pub) => this.m_publicReferencePosition = pub;

  public bool IsReferencePositionPublic() => this.m_publicReferencePosition;

  public void SetReferencePosition(Vector3 pos) => this.m_referencePosition = pos;

  public Vector3 GetReferencePosition() => this.m_referencePosition;

  public List<ZDO> GetAllCharacterZDOS()
  {
    List<ZDO> allCharacterZdos = new List<ZDO>();
    ZDO zdo1 = this.m_zdoMan.GetZDO(this.m_characterID);
    if (zdo1 != null)
      allCharacterZdos.Add(zdo1);
    foreach (ZNetPeer peer in this.m_peers)
    {
      if (peer.IsReady() && !peer.m_characterID.IsNone())
      {
        ZDO zdo2 = this.m_zdoMan.GetZDO(peer.m_characterID);
        if (zdo2 != null)
          allCharacterZdos.Add(zdo2);
      }
    }
    return allCharacterZdos;
  }

  public int GetPeerConnections()
  {
    int peerConnections = 0;
    for (int index = 0; index < this.m_peers.Count; ++index)
    {
      if (this.m_peers[index].IsReady())
        ++peerConnections;
    }
    return peerConnections;
  }

  public ZNat GetZNat() => this.m_nat;

  public static void SetServer(
    bool server,
    bool openServer,
    bool publicServer,
    string serverName,
    string password,
    World world)
  {
    ZNet.m_isServer = server;
    ZNet.m_openServer = openServer;
    ZNet.m_publicServer = publicServer;
    ZNet.m_serverPassword = string.IsNullOrEmpty(password) ? "" : ZNet.HashPassword(password, ZNet.ServerPasswordSalt());
    ZNet.m_ServerName = serverName;
    ZNet.m_world = world;
  }

  private static string HashPassword(string password, string salt) => Encoding.ASCII.GetString(new MD5CryptoServiceProvider().ComputeHash(Encoding.ASCII.GetBytes(password + salt)));

  public static void ResetServerHost()
  {
    ZNet.m_serverPlayFabPlayerId = (string) null;
    ZNet.m_serverSteamID = 0UL;
    ZNet.m_serverHost = "";
    ZNet.m_serverHostPort = 0;
  }

  public static bool HasServerHost() => (ZNet.m_serverHost != "" ? 1 : (ZNet.m_serverPlayFabPlayerId != null ? 1 : 0)) != 0 || ZNet.m_serverSteamID > 0UL;

  public static void SetServerHost(string remotePlayerId)
  {
    ZNet.ResetServerHost();
    ZNet.m_serverPlayFabPlayerId = remotePlayerId;
    ZNet.m_onlineBackend = OnlineBackendType.PlayFab;
  }

  public static void SetServerHost(ulong serverID)
  {
    ZNet.ResetServerHost();
    ZNet.m_serverSteamID = serverID;
    ZNet.m_onlineBackend = OnlineBackendType.Steamworks;
  }

  public static void SetServerHost(string host, int port, OnlineBackendType backend)
  {
    ZNet.ResetServerHost();
    ZNet.m_serverHost = host;
    ZNet.m_serverHostPort = port;
    ZNet.m_onlineBackend = backend;
  }

  public static string GetServerString(bool includeBackend = true)
  {
    switch (ZNet.m_onlineBackend)
    {
      case OnlineBackendType.Steamworks:
        return (includeBackend ? "steam/" : "") + ZNet.m_serverSteamID.ToString() + "/" + ZNet.m_serverHost + ":" + ZNet.m_serverHostPort.ToString();
      case OnlineBackendType.PlayFab:
        return (includeBackend ? "playfab/" : "") + ZNet.m_serverPlayFabPlayerId;
      default:
        return (includeBackend ? "socket/" : "") + ZNet.m_serverHost + ":" + ZNet.m_serverHostPort.ToString();
    }
  }

  public bool IsServer() => ZNet.m_isServer;

  public static bool IsOpenServer() => ZNet.m_openServer;

  public bool IsDedicated() => false;

  public bool IsCurrentServerDedicated()
  {
    List<ZNetPeer> peers = this.GetPeers();
    bool flag = false;
    for (int index = 0; index < peers.Count; ++index)
    {
      if (peers[index].m_characterID.IsNone())
      {
        flag = true;
        break;
      }
    }
    return flag;
  }

  public static bool IsPasswordDialogShowing() => !((UnityEngine.Object) ZNet.m_instance == (UnityEngine.Object) null) && ZNet.m_instance.m_passwordDialog.gameObject.activeInHierarchy;

  public static bool IsSinglePlayer => ZNet.m_isServer && !ZNet.m_openServer;

  public static bool TryGetServerAssignedDisplayName(PlatformUserID userId, out string displayName)
  {
    if ((UnityEngine.Object) ZNet.instance == (UnityEngine.Object) null)
    {
      displayName = (string) null;
      return false;
    }
    for (int index = 0; index < ZNet.instance.m_players.Count; ++index)
    {
      if (ZNet.instance.m_players[index].m_userInfo.m_id == userId && ZNet.instance.m_players[index].m_serverAssignedDisplayName != null)
      {
        displayName = ZNet.instance.m_players[index].m_serverAssignedDisplayName;
        return true;
      }
    }
    displayName = (string) null;
    return false;
  }

  private string GetUniqueDisplayName(ZNet.CrossNetworkUserInfo userInfo)
  {
    bool flag = false;
    int num1 = 1;
    int num2 = 0;
    for (int index = 0; index < this.m_playerHistory.Count; ++index)
    {
      if (!(this.m_playerHistory[index].m_displayName != userInfo.m_displayName))
      {
        ++num2;
        if (!flag)
        {
          if (this.m_playerHistory[index].m_id == userInfo.m_id)
            flag = true;
          else
            ++num1;
        }
      }
    }
    if (!flag)
      ZLog.LogError((object) string.Format("Couldn't find matching ID to user {0} in player history!", (object) userInfo));
    return num2 > 1 ? string.Format("{0}#{1}", (object) userInfo.m_displayName, (object) num1) : userInfo.m_displayName;
  }

  private void UpdatePlayerList()
  {
    this.m_players.Clear();
    if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null)
    {
      ZNet.PlayerInfo playerInfo = new ZNet.PlayerInfo();
      playerInfo.m_name = Game.instance.GetPlayerProfile().GetName();
      playerInfo.m_userInfo.m_id = PlatformManager.DistributionPlatform.LocalUser.PlatformUserID;
      playerInfo.m_userInfo.m_displayName = PlatformManager.DistributionPlatform.LocalUser.DisplayName;
      playerInfo.m_characterID = this.m_characterID;
      playerInfo.m_publicPosition = this.m_publicReferencePosition;
      if (playerInfo.m_publicPosition)
        playerInfo.m_position = this.m_referencePosition;
      this.m_players.Add(playerInfo);
    }
    foreach (ZNetPeer peer in this.m_peers)
    {
      if (peer.IsReady())
      {
        ZNet.PlayerInfo playerInfo = new ZNet.PlayerInfo();
        playerInfo.m_name = peer.m_playerName;
        playerInfo.m_characterID = peer.m_characterID;
        playerInfo.m_userInfo.m_id = ZNet.m_onlineBackend != OnlineBackendType.Steamworks ? new PlatformUserID(peer.m_socket.GetHostName()) : new PlatformUserID(this.m_steamPlatform, peer.m_socket.GetHostName());
        playerInfo.m_userInfo.m_displayName = peer.m_serverSyncedPlayerData.ContainsKey("platformDisplayName") ? peer.m_serverSyncedPlayerData["platformDisplayName"] : "";
        playerInfo.m_publicPosition = peer.m_publicRefPos;
        if (playerInfo.m_publicPosition)
          playerInfo.m_position = peer.m_refPos;
        this.m_players.Add(playerInfo);
      }
    }
    this.UpdatePlayerHistory();
    for (int index = 0; index < this.m_players.Count; ++index)
    {
      ZNet.PlayerInfo player = this.m_players[index];
      player.m_serverAssignedDisplayName = this.GetUniqueDisplayName(player.m_userInfo);
      this.m_players[index] = player;
    }
  }

  private void SendPlayerList()
  {
    this.UpdatePlayerList();
    if (this.m_peers.Count <= 0)
      return;
    ZPackage zpackage = new ZPackage();
    zpackage.Write(this.m_players.Count);
    foreach (ZNet.PlayerInfo player in this.m_players)
    {
      zpackage.Write(player.m_name);
      zpackage.Write(player.m_characterID);
      zpackage.Write(player.m_userInfo.m_id.ToString());
      zpackage.Write(player.m_userInfo.m_displayName);
      zpackage.Write(player.m_serverAssignedDisplayName);
      zpackage.Write(player.m_publicPosition);
      if (player.m_publicPosition)
        zpackage.Write(player.m_position);
    }
    foreach (ZNetPeer peer in this.m_peers)
    {
      if (peer.IsReady())
        peer.m_rpc.Invoke("PlayerList", (object) zpackage);
    }
    this.UpdatePlayerHistory();
  }

  private void SendAdminList()
  {
    if (this.m_peers.Count <= 0)
      return;
    ZPackage zpackage = new ZPackage();
    zpackage.Write(this.m_adminList.Count());
    foreach (string data in this.m_adminList.GetList())
      zpackage.Write(data);
    foreach (ZNetPeer peer in this.m_peers)
    {
      if (peer.IsReady())
        peer.m_rpc.Invoke("AdminList", (object) zpackage);
    }
  }

  private void RPC_AdminList(ZRpc rpc, ZPackage pkg)
  {
    this.m_adminListForRpc.Clear();
    int num = pkg.ReadInt();
    for (int index = 0; index < num; ++index)
      this.m_adminListForRpc.Add(pkg.ReadString());
  }

  private void RPC_PlayerList(ZRpc rpc, ZPackage pkg)
  {
    this.m_players.Clear();
    int num = pkg.ReadInt();
    for (int index = 0; index < num; ++index)
    {
      ZNet.PlayerInfo playerInfo = new ZNet.PlayerInfo();
      playerInfo.m_name = pkg.ReadString();
      playerInfo.m_characterID = pkg.ReadZDOID();
      playerInfo.m_userInfo.m_id = new PlatformUserID(pkg.ReadString());
      playerInfo.m_userInfo.m_displayName = pkg.ReadString();
      playerInfo.m_serverAssignedDisplayName = pkg.ReadString();
      playerInfo.m_publicPosition = pkg.ReadBool();
      if (playerInfo.m_publicPosition)
        playerInfo.m_position = pkg.ReadVector3();
      this.m_players.Add(playerInfo);
    }
    this.UpdatePlayerHistory();
  }

  private void UpdatePlayerHistory()
  {
    List<PlatformUserID> platformUserIdList = new List<PlatformUserID>();
    PlatformUserID platformUserId = PlatformManager.DistributionPlatform.LocalUser.PlatformUserID;
    foreach (ZNet.PlayerInfo player in this.m_players)
    {
      int index = 0;
      while (index < this.m_playerHistory.Count && !(this.m_playerHistory[index].m_id == player.m_userInfo.m_id))
        ++index;
      if (index < this.m_playerHistory.Count)
      {
        this.m_playerHistory[index] = player.m_userInfo;
      }
      else
      {
        this.m_playerHistory.Add(player.m_userInfo);
        if (!(player.m_userInfo.m_id == platformUserId))
          platformUserIdList.Add(player.m_userInfo.m_id);
      }
    }
    IMatchmakingProvider matchmakingProvider = PlatformManager.DistributionPlatform.MatchmakingProvider;
    if (matchmakingProvider == null || platformUserIdList.Count <= 0)
      return;
    matchmakingProvider.AddRecentPlayers(platformUserIdList.ToArray());
  }

  public List<ZNet.PlayerInfo> GetPlayerList() => this.m_players;

  public static bool TryGetPlayerByPlatformUserID(
    PlatformUserID platformUserID,
    out ZNet.PlayerInfo playerInfo)
  {
    if ((UnityEngine.Object) ZNet.instance == (UnityEngine.Object) null)
    {
      playerInfo = new ZNet.PlayerInfo();
      return false;
    }
    for (int index = 0; index < ZNet.instance.m_players.Count; ++index)
    {
      if (ZNet.instance.m_players[index].m_userInfo.m_id == platformUserID)
      {
        playerInfo = ZNet.instance.m_players[index];
        return true;
      }
    }
    playerInfo = new ZNet.PlayerInfo();
    return false;
  }

  public List<string> GetAdminList() => this.m_adminListForRpc;

  public ZDOID LocalPlayerCharacterID => this.m_characterID;

  public void GetOtherPublicPlayers(List<ZNet.PlayerInfo> playerList)
  {
    foreach (ZNet.PlayerInfo player in this.m_players)
    {
      if (player.m_publicPosition && !player.m_characterID.IsNone() && !(player.m_characterID == this.m_characterID))
        playerList.Add(player);
    }
  }

  public int GetNrOfPlayers() => this.m_players.Count;

  public void GetNetStats(
    out float localQuality,
    out float remoteQuality,
    out int ping,
    out float outByteSec,
    out float inByteSec)
  {
    localQuality = 0.0f;
    remoteQuality = 0.0f;
    ping = 0;
    outByteSec = 0.0f;
    inByteSec = 0.0f;
    if (this.IsServer())
    {
      int num = 0;
      foreach (ZNetPeer peer in this.m_peers)
      {
        if (peer.IsReady())
        {
          ++num;
          float localQuality1;
          float remoteQuality1;
          int ping1;
          float outByteSec1;
          float inByteSec1;
          peer.m_socket.GetConnectionQuality(out localQuality1, out remoteQuality1, out ping1, out outByteSec1, out inByteSec1);
          localQuality += localQuality1;
          remoteQuality += remoteQuality1;
          ping += ping1;
          outByteSec += outByteSec1;
          inByteSec += inByteSec1;
        }
      }
      if (num <= 0)
        return;
      localQuality /= (float) num;
      remoteQuality /= (float) num;
      ping /= num;
    }
    else
    {
      if (ZNet.m_connectionStatus != ZNet.ConnectionStatus.Connected)
        return;
      foreach (ZNetPeer peer in this.m_peers)
      {
        if (peer.IsReady())
        {
          peer.m_socket.GetConnectionQuality(out localQuality, out remoteQuality, out ping, out outByteSec, out inByteSec);
          break;
        }
      }
    }
  }

  public void SetNetTime(double time) => this.m_netTime = time;

  public DateTime GetTime() => new DateTime((long) (this.m_netTime * 1000.0 * 10000.0));

  public float GetWrappedDayTimeSeconds() => (float) (this.m_netTime % 86400.0);

  public double GetTimeSeconds() => this.m_netTime;

  public static ZNet.ConnectionStatus GetConnectionStatus()
  {
    if ((UnityEngine.Object) ZNet.m_instance != (UnityEngine.Object) null && ZNet.m_instance.IsServer())
      return ZNet.ConnectionStatus.Connected;
    if (ZNet.m_externalError != ZNet.ConnectionStatus.None)
      ZNet.m_connectionStatus = ZNet.m_externalError;
    return ZNet.m_connectionStatus;
  }

  public bool HasBadConnection() => (double) this.GetServerPing() > (double) this.m_badConnectionPing;

  public float GetServerPing()
  {
    if (this.IsServer() || ZNet.m_connectionStatus == ZNet.ConnectionStatus.Connecting || ZNet.m_connectionStatus == ZNet.ConnectionStatus.None || ZNet.m_connectionStatus != ZNet.ConnectionStatus.Connected)
      return 0.0f;
    foreach (ZNetPeer peer in this.m_peers)
    {
      if (peer.IsReady())
        return peer.m_rpc.GetTimeSinceLastPing();
    }
    return 0.0f;
  }

  public ZNetPeer GetServerPeer()
  {
    if (this.IsServer())
      return (ZNetPeer) null;
    switch (ZNet.m_connectionStatus)
    {
      case ZNet.ConnectionStatus.None:
      case ZNet.ConnectionStatus.Connecting:
        return (ZNetPeer) null;
      case ZNet.ConnectionStatus.Connected:
        using (List<ZNetPeer>.Enumerator enumerator = this.m_peers.GetEnumerator())
        {
          while (enumerator.MoveNext())
          {
            ZNetPeer current = enumerator.Current;
            if (current.IsReady())
              return current;
          }
          break;
        }
    }
    return (ZNetPeer) null;
  }

  public ZRpc GetServerRPC() => this.GetServerPeer()?.m_rpc;

  public List<ZNetPeer> GetPeers() => this.m_peers;

  public void RemotePrint(ZRpc rpc, string text)
  {
    if (rpc == null)
    {
      if (!(bool) (UnityEngine.Object) Console.instance)
        return;
      Console.instance.Print(text);
    }
    else
      rpc.Invoke(nameof (RemotePrint), (object) text);
  }

  private void RPC_RemotePrint(ZRpc rpc, string text)
  {
    if (!(bool) (UnityEngine.Object) Console.instance)
      return;
    Console.instance.Print(text);
  }

  public void Kick(string user)
  {
    if (this.IsServer())
      this.InternalKick(user);
    else
      this.GetServerRPC()?.Invoke(nameof (Kick), (object) user);
  }

  private void RPC_Kick(ZRpc rpc, string user)
  {
    if (!this.ListContainsId(this.m_adminList, rpc.GetSocket().GetHostName()))
    {
      this.RemotePrint(rpc, "You are not admin");
    }
    else
    {
      this.RemotePrint(rpc, "Kicking user " + user);
      this.InternalKick(user);
    }
  }

  private void RPC_Kicked(ZRpc rpc)
  {
    ZNetPeer peer = this.GetPeer(rpc);
    if (peer == null || !peer.m_server)
      return;
    ZNet.m_connectionStatus = ZNet.ConnectionStatus.ErrorKicked;
    this.Disconnect(peer);
  }

  private void InternalKick(string user)
  {
    if (user == "")
      return;
    ZNetPeer peer = (ZNetPeer) null;
    PlatformUserID platform;
    if (!PlatformUserID.TryParse(user, out platform))
      platform = new PlatformUserID(this.m_steamPlatform, user);
    if (ZNet.m_onlineBackend == OnlineBackendType.Steamworks)
    {
      if (platform.m_platform == this.m_steamPlatform)
        peer = this.GetPeerByHostName(platform.m_userID);
    }
    else
      peer = this.GetPeerByHostName(platform.ToString());
    if (peer == null)
      peer = this.GetPeerByPlayerName(user);
    if (peer == null)
      return;
    this.InternalKick(peer);
  }

  private void InternalKick(ZNetPeer peer)
  {
    if (!this.IsServer() || peer == null || ZNet.PeersToDisconnectAfterKick.ContainsKey(peer))
      return;
    ZLog.Log((object) ("Kicking " + peer.m_playerName));
    peer.m_rpc.Invoke("Kicked");
    ZNet.PeersToDisconnectAfterKick[peer] = Time.time + 1f;
  }

  private bool IsAllowed(string hostName, string playerName) => !this.ListContainsId(this.m_bannedList, hostName) && !this.m_bannedList.Contains(playerName) && (this.m_permittedList.Count() <= 0 || this.ListContainsId(this.m_permittedList, hostName));

  public void Ban(string user)
  {
    if (this.IsServer())
      this.InternalBan((ZRpc) null, user);
    else
      this.GetServerRPC()?.Invoke(nameof (Ban), (object) user);
  }

  private void RPC_Ban(ZRpc rpc, string user)
  {
    if (!this.ListContainsId(this.m_adminList, rpc.GetSocket().GetHostName()))
      this.RemotePrint(rpc, "You are not admin");
    else
      this.InternalBan(rpc, user);
  }

  private void InternalBan(ZRpc rpc, string user)
  {
    if (!this.IsServer() || user == "")
      return;
    ZNetPeer peerByPlayerName = this.GetPeerByPlayerName(user);
    if (peerByPlayerName != null)
      user = peerByPlayerName.m_socket.GetHostName();
    this.RemotePrint(rpc, "Banning user " + user);
    this.m_bannedList.Add(user);
  }

  public void Unban(string user)
  {
    if (this.IsServer())
      this.InternalUnban((ZRpc) null, user);
    else
      this.GetServerRPC()?.Invoke(nameof (Unban), (object) user);
  }

  private void RPC_Unban(ZRpc rpc, string user)
  {
    if (!this.ListContainsId(this.m_adminList, rpc.GetSocket().GetHostName()))
      this.RemotePrint(rpc, "You are not admin");
    else
      this.InternalUnban(rpc, user);
  }

  private void InternalUnban(ZRpc rpc, string user)
  {
    if (!this.IsServer() || user == "")
      return;
    this.RemotePrint(rpc, "Unbanning user " + user);
    this.m_bannedList.Remove(user);
  }

  public bool IsAdmin(string hostName) => this.ListContainsId(this.m_adminList, hostName);

  public List<string> Banned => this.m_bannedList.GetList();

  public void PrintBanned()
  {
    if (this.IsServer())
      this.InternalPrintBanned((ZRpc) null);
    else
      this.GetServerRPC()?.Invoke(nameof (PrintBanned));
  }

  private void RPC_PrintBanned(ZRpc rpc)
  {
    if (!this.ListContainsId(this.m_adminList, rpc.GetSocket().GetHostName()))
      this.RemotePrint(rpc, "You are not admin");
    else
      this.InternalPrintBanned(rpc);
  }

  private void InternalPrintBanned(ZRpc rpc)
  {
    this.RemotePrint(rpc, "Banned users");
    List<string> list1 = this.m_bannedList.GetList();
    if (list1.Count == 0)
    {
      this.RemotePrint(rpc, "-");
    }
    else
    {
      for (int index = 0; index < list1.Count; ++index)
        this.RemotePrint(rpc, index.ToString() + ": " + list1[index]);
    }
    this.RemotePrint(rpc, "");
    this.RemotePrint(rpc, "Permitted users");
    List<string> list2 = this.m_permittedList.GetList();
    if (list2.Count == 0)
    {
      this.RemotePrint(rpc, "All");
    }
    else
    {
      for (int index = 0; index < list2.Count; ++index)
        this.RemotePrint(rpc, index.ToString() + ": " + list2[index]);
    }
  }

  public void RemoteCommand(string command)
  {
    if (this.IsServer())
      this.InternalCommand((ZRpc) null, command);
    else
      this.GetServerRPC()?.Invoke("RPC_RemoteCommand", (object) command);
  }

  private void RPC_RemoteCommand(ZRpc rpc, string command)
  {
    if (!this.ListContainsId(this.m_adminList, rpc.GetSocket().GetHostName()))
      this.RemotePrint(rpc, "You are not admin");
    else
      this.InternalCommand(rpc, command);
  }

  private void InternalCommand(ZRpc rpc, string command)
  {
    ZLog.Log((object) ("Remote admin '" + rpc.GetSocket().GetHostName() + "' executed command '" + command + "' remotely."));
    Console.instance.TryRunCommand(command);
  }

  private static string ServerPasswordSalt()
  {
    if (ZNet.m_serverPasswordSalt.Length == 0)
    {
      byte[] numArray = new byte[16];
      RandomNumberGenerator.Create().GetBytes(numArray);
      ZNet.m_serverPasswordSalt = Encoding.ASCII.GetString(numArray);
    }
    return ZNet.m_serverPasswordSalt;
  }

  public static void SetExternalError(ZNet.ConnectionStatus error) => ZNet.m_externalError = error;

  public float SaveStartTime => this.m_saveStartTime;

  public float SaveThreadStartTime => this.m_saveThreadStartTime;

  public float SaveDoneTime => this.m_saveDoneTime;

  public bool HaveStopped => this.m_haveStoped;

  public static World World => ZNet.m_world;

  public enum ConnectionStatus
  {
    None,
    Connecting,
    Connected,
    ErrorVersion,
    ErrorDisconnected,
    ErrorConnectFailed,
    ErrorPassword,
    ErrorAlreadyConnected,
    ErrorBanned,
    ErrorFull,
    ErrorPlatformExcluded,
    ErrorCrossplayPrivilege,
    ErrorKicked,
  }

  public struct CrossNetworkUserInfo : IEquatable<ZNet.CrossNetworkUserInfo>
  {
    public PlatformUserID m_id;
    public string m_displayName;

    public override bool Equals(object other) => other is ZNet.CrossNetworkUserInfo other1 && this.Equals(other1);

    public bool Equals(ZNet.CrossNetworkUserInfo other) => this.m_id == other.m_id && this.m_displayName == other.m_displayName;

    public override int GetHashCode() => HashCode.Combine<PlatformUserID, string>(this.m_id, this.m_displayName);

    public static bool operator ==(ZNet.CrossNetworkUserInfo lhs, ZNet.CrossNetworkUserInfo rhs) => lhs.Equals(rhs);

    public static bool operator !=(ZNet.CrossNetworkUserInfo lhs, ZNet.CrossNetworkUserInfo rhs) => !lhs.Equals(rhs);

    public override string ToString() => string.Format("{0} ({1})", (object) this.m_displayName, (object) this.m_id);
  }

  public struct PlayerInfo
  {
    public string m_name;
    public ZDOID m_characterID;
    public ZNet.CrossNetworkUserInfo m_userInfo;
    public string m_serverAssignedDisplayName;
    public bool m_publicPosition;
    public Vector3 m_position;

    public override string ToString()
    {
      string str1 = "([";
      string str2 = (!string.IsNullOrEmpty(this.m_name) ? str1 + this.m_name : str1 + "-") + string.Format(", {0}], [", (object) this.m_characterID);
      string str3 = (!string.IsNullOrEmpty(this.m_userInfo.m_displayName) ? str2 + this.m_userInfo.m_displayName : str2 + "-") + string.Format(", {0}], ", (object) this.m_characterID);
      return (!string.IsNullOrEmpty(this.m_serverAssignedDisplayName) ? str3 + this.m_serverAssignedDisplayName : str3 + "-") + ")";
    }
  }
}
