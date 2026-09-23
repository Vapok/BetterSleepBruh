using System;
using System.Reflection;
using BetterSleepBruh.Configuration;
using BetterSleepBruh.Patches;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BetterSleepBruh.Components;

/*
* Client UI only: displays server-driven sleep HUD data from routed RPCs. Does not modify world time.
*/
public sealed class SleepHudView : MonoBehaviour
{
    // Beyond this many players, show one pillow + numeric X/Y instead of one icon per player.
    private const int MaxPillowSegments = 5;

    private RectTransform _segmentsRoot;
    private Image[] _segments = System.Array.Empty<Image>();
    private TextMeshProUGUI _boostTmp;
    private TextMeshProUGUI _ratioTmp;
    private TMP_FontAsset _hudFont;
    private int _lastTotal = -1;
    private int _lastRefreshedTotal = -1;
    private int _lastRefreshedSleeping = -1;
    private bool _compactLayout;

    private GameObject _bedIconGo;
    private GameObject _arrowChaserGo;
    private Image[] _arrowImages;
    private bool _isBoosting;
    private float _boostFraction;
    private float _arrowTimer;

    private const float ArrowMinStepDuration = 0.08f;
    private const float ArrowMaxStepDuration = 0.35f;

    private static readonly Color BgMidnight = new(0f, 0f, 0f, 0.39f);
    private static readonly Color PillowAwakeTint = new(0.32f, 0.32f, 0.34f, 1f);
    private static readonly Color ArrowDimTint = new(0.35f, 0.28f, 0.20f, 0.45f);
    private static readonly Color32 MoonBeige = new(235, 225, 190, 255);
    private static readonly Color32 BoostYellow = new(255, 183, 91, 255);

    private const float StripHeightPx = 40f;

    private bool _rpcsRegistered;

    private void Awake()
    {
        BetterSleepBruh.Log.Debug($"SleepHudView Is Awoken");
        RegisterRpcs();
    }

    private void Start()
    {
        BetterSleepBruh.Log.Debug("[CLIENT] SleepHudView Is Starting");
        RegisterRpcs();
        RequestOrSyncInitialState();
    }

    private void RegisterRpcs()
    {
        if (_rpcsRegistered || ZRoutedRpc.instance == null)
            return;

        BetterSleepBruh.Log.Debug("[CLIENT] Registering SleepHudView RPCs");
        ZRoutedRpc.instance.Register<int, int, double>("RPC_SleepingPlayerInfo", RPC_SleepingPlayerInfo);
        ZRoutedRpc.instance.Register("RPC_StartSleep", RPC_StartSleep);
        ZRoutedRpc.instance.Register("RPC_StopSleep", RPC_StopSleep);
        _rpcsRegistered = true;
    }

    private void OnEnable()
    {
        RequestOrSyncInitialState();
    }

    private void OnDisable()
    {
        _isBoosting = false;
        _arrowTimer = 0f;
    }

    private void Update()
    {
        if (!_isBoosting || _arrowImages == null || _arrowImages.Length < 3)
            return;

        float stepDuration = Mathf.Lerp(ArrowMaxStepDuration, ArrowMinStepDuration, _boostFraction);
        _arrowTimer += Time.deltaTime;
        int step = (int)(_arrowTimer / stepDuration) % 4;

        _arrowImages[0].color = (step >= 0 && step <= 2) ? (Color)BoostYellow : ArrowDimTint;
        _arrowImages[1].color = (step >= 1 && step <= 2) ? (Color)BoostYellow : ArrowDimTint;
        _arrowImages[2].color = (step == 2) ? (Color)BoostYellow : ArrowDimTint;
    }

    private void RequestOrSyncInitialState()
    {
        if (SleepTracker.Instance != null)
        {
            if (SleepTracker.Instance.CanSleep)
            {
                gameObject.SetActive(true);
                Refresh(SleepTracker.CurrentPlayerCount, SleepTracker.CurrentSleepingCount);
            }
            else
            {
                gameObject.SetActive(false);
            }
        }
        else if (ZRoutedRpc.instance != null)
        {
            ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.instance.GetServerPeerID(), "RPC_RequestSleepingPlayerInfo");
        }
    }

    private void RPC_SleepingPlayerInfo(long sender, int totalPlayers, int playersSleeping, double sleepBoost)
    {
        if (Player.m_localPlayer == null)
            return;

        if (ConfigRegistry.IsPlayerCountTestingActive)
            BetterSleepBruh.Log.Debug($"[BetterSleepBruh TESTING] HUD total={totalPlayers} sleeping={playersSleeping} extraRate={sleepBoost}");
        else
        {
            BetterSleepBruh.Log.Debug($"[CLIENT] Total Players: {totalPlayers}");
            BetterSleepBruh.Log.Debug($"[CLIENT] Players Sleeping: {playersSleeping}");
            BetterSleepBruh.Log.Debug($"[CLIENT] Sleep Boost (extra rate × dt): {sleepBoost}");
        }

        if (!gameObject.activeSelf && (EnvMan.instance == null || EnvManPatches.IsInSleepWindow(EnvMan.instance)))
        {
            gameObject.SetActive(true);
        }

        Refresh(totalPlayers, playersSleeping);
    }

    private void RPC_StartSleep(long sender)
    {
        if (Player.m_localPlayer == null)
            return;

        BetterSleepBruh.Log.Debug($"[CLIENT] Start Sleep");

        gameObject.SetActive(true);
    }

    private void RPC_StopSleep(long sender)
    {
        if (Player.m_localPlayer == null)
            return;

        BetterSleepBruh.Log.Debug($"[CLIENT] Stop Sleep");

        _isBoosting = false;
        if (_arrowChaserGo != null && _arrowChaserGo.activeSelf)
            _arrowChaserGo.SetActive(false);
        if (_bedIconGo != null && !_bedIconGo.activeSelf)
            _bedIconGo.SetActive(true);

        if (EnvMan.instance == null || !EnvMan.instance.IsTimeSkipping())
        {
            Player player = Player.m_localPlayer;
            if (player != null)
            {
                player.SetSleeping(false);
                if (player.InBed())
                {
                    player.AttachStop();
                }
                player.m_wakeupTime = ZNet.instance != null ? ZNet.instance.GetTimeSeconds() : 0.0;
            }
        }

        gameObject.SetActive(false);
    }

    
    public static SleepHudView TryCreate(Transform mapGeometryTransform, float gapBelowMinimap = 1f)
    {
        if (mapGeometryTransform == null)
            return null;
        RectTransform map = mapGeometryTransform as RectTransform;
        if (map == null)
            return null;

        Transform existing = map.Find("BetterSleepBruh_SleepHud");
        if (existing != null)
            Destroy(existing.gameObject);

        Sprite white = BaseWhiteSprite();
        float stripHeight = StripHeightPx;

        GameObject rootGo = new GameObject("BetterSleepBruh_SleepHud", typeof(RectTransform), typeof(Image), typeof(SleepHudView));
        RectTransform rt = rootGo.GetComponent<RectTransform>();
        rt.SetParent(map, false);
        rt.SetAsLastSibling();
        rt.localScale = Vector3.one;

        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.sizeDelta = new Vector2(0f, stripHeight);
        rt.anchoredPosition = new Vector2(0f, -gapBelowMinimap);

        Image bg = rootGo.GetComponent<Image>();
        bg.sprite = white;
        bg.type = Image.Type.Simple;
        bg.color = BgMidnight;
        bg.raycastTarget = false;
        rootGo.AddComponent<RectMask2D>();

        TMP_FontAsset font = ResolveTmpFont(map);
        if (font == null)
        {
            BetterSleepBruh.Log.Warning("[SleepHud] No TMP font; not creating sleep HUD.");
            Destroy(rootGo);
            return null;
        }

        SleepHudView view = rootGo.GetComponent<SleepHudView>();
        view.BuildContent(rootGo.GetComponent<RectTransform>(), font);
        return view;
    }

    public void Refresh(int totalPlayers, int playersSleeping)
    {
        if (_segmentsRoot == null)
            return;

        totalPlayers = Mathf.Max(0, totalPlayers);
        playersSleeping = Mathf.Clamp(playersSleeping, 0, totalPlayers);

        EnsureSegments(totalPlayers);
        if (_compactLayout)
        {
            if (_segments.Length == 1)
                _segments[0].color = playersSleeping > 0 ? Color.white : PillowAwakeTint;
            if (_ratioTmp != null)
            {
                string ratioText = $"{playersSleeping}/{totalPlayers}";
                if (_ratioTmp.text != ratioText)
                    _ratioTmp.text = ratioText;
            }
        }
        else
        {
            if (totalPlayers != _lastRefreshedTotal || playersSleeping != _lastRefreshedSleeping)
            {
                for (int i = 0; i < _segments.Length; i++)
                    _segments[i].color = i < playersSleeping ? Color.white : PillowAwakeTint;
            }
        }

        _lastRefreshedTotal = totalPlayers;
        _lastRefreshedSleeping = playersSleeping;

        double pct = GetBonusLabelPercent(totalPlayers, playersSleeping);
        double maxPct = ConfigRegistry.BonusMultiplier != null ? ConfigRegistry.BonusMultiplier.Value * 100.0 : 60.0;
        bool isBoosting = totalPlayers > 1 && playersSleeping > 0 && playersSleeping < totalPlayers && pct > 0.0001;
        _isBoosting = isBoosting;
        _boostFraction = isBoosting && maxPct > 0.0 ? Mathf.Clamp01((float)(pct / maxPct)) : 0f;

        if (_bedIconGo != null && _arrowChaserGo != null)
        {
            if (isBoosting)
            {
                if (!_arrowChaserGo.activeSelf)
                    _arrowChaserGo.SetActive(true);
                if (_bedIconGo.activeSelf)
                    _bedIconGo.SetActive(false);
            }
            else
            {
                if (_arrowChaserGo.activeSelf)
                    _arrowChaserGo.SetActive(false);
                if (!_bedIconGo.activeSelf)
                    _bedIconGo.SetActive(true);
            }
        }

        if (_boostTmp == null)
            return;

        string newBoostText = pct <= 0.0001 ? "+0%" : $"+{pct:F0}%";
        if (_boostTmp.text != newBoostText)
            _boostTmp.text = newBoostText;
    }

    private static double GetBonusLabelPercent(int playerCount, int playersSleeping)
    {
        if (playerCount <= 1 || playersSleeping <= 0)
            return 0.0;

        double maxPct = ConfigRegistry.BonusMultiplier.Value * 100.0;
        if (playersSleeping >= playerCount - 1)
            return maxPct;

        return playersSleeping / (double)playerCount * maxPct;
    }

    private void BuildContent(RectTransform rootRt, TMP_FontAsset font)
    {
        _hudFont = font;

        GameObject rowGo = new GameObject("Row", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        RectTransform rowRt = (RectTransform)rowGo.transform;
        rowRt.SetParent(rootRt, false);
        StretchFull(rowRt);
        HorizontalLayoutGroup hlg = rowGo.GetComponent<HorizontalLayoutGroup>();
        hlg.padding = new RectOffset(4, 4, 3, 3);
        hlg.spacing = 4;
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = true;

        CreateIconInRow(rowRt, "Moon", GetMoonIconSprite(), 28f, Color.white);
        CreateBedSlot(rowRt);

        GameObject barGo = new GameObject("BarHost", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(Image));
        RectTransform barRt = (RectTransform)barGo.transform;
        barRt.SetParent(rowRt, false);
        Image barBg = barGo.GetComponent<Image>();
        barBg.sprite = BaseWhiteSprite();
        barBg.type = Image.Type.Simple;
        barBg.color = new Color(0f, 0f, 0f, 0.25f);
        barBg.raycastTarget = false;
        HorizontalLayoutGroup barH = barGo.GetComponent<HorizontalLayoutGroup>();
        barH.spacing = 3;
        barH.padding = new RectOffset(4, 4, 4, 4);
        barH.childAlignment = TextAnchor.MiddleCenter;
        barH.childControlWidth = true;
        barH.childControlHeight = true;
        barH.childForceExpandWidth = true;
        barH.childForceExpandHeight = true;
        LayoutElement barLe = barGo.AddComponent<LayoutElement>();
        barLe.flexibleWidth = 1f;
        barLe.minWidth = 48f;
        barLe.preferredHeight = 26f;
        _segmentsRoot = barRt;

        _boostTmp = CreateTmpInRow(rowRt, "Boost", font, 40f, 14f, BoostYellow, "0%", TextAlignmentOptions.MidlineRight);

        SleepTracker.GetSleepOccupancyCounts(out int initialTotal, out int initialSleeping);
        if (initialTotal <= 0)
            initialTotal = 1;

        Refresh(initialTotal, initialSleeping);
    }

    private void EnsureSegments(int total)
    {
        if (_segmentsRoot == null)
            return;
        bool compact = total > MaxPillowSegments;
        int expectedSegCount = compact ? 1 : total;
        if (total == _lastTotal && _compactLayout == compact && _segments.Length == expectedSegCount)
            return;

        _lastTotal = total;
        _compactLayout = compact;
        _ratioTmp = null;

        for (int c = _segmentsRoot.childCount - 1; c >= 0; c--)
        {
            Transform child = _segmentsRoot.GetChild(c);
            if (child != null)
                Destroy(child.gameObject);
        }

        if (total <= 0)
        {
            _segments = System.Array.Empty<Image>();
            return;
        }

        Sprite pillowSprite = GetPillowSegmentSprite();

        if (compact)
        {
            GameObject pillowWrap = new GameObject("PillowCompact", typeof(RectTransform), typeof(LayoutElement));
            pillowWrap.transform.SetParent(_segmentsRoot, false);
            LayoutElement pillowLe = pillowWrap.GetComponent<LayoutElement>();
            pillowLe.flexibleWidth = 0f;
            pillowLe.minWidth = 28f;
            pillowLe.preferredWidth = 40f;
            pillowLe.preferredHeight = 22f;
            pillowLe.flexibleHeight = 1f;

            GameObject segGo = new GameObject("Seg_0", typeof(RectTransform), typeof(Image));
            RectTransform segRt = (RectTransform)segGo.transform;
            segRt.SetParent(pillowWrap.transform, false);
            StretchFull(segRt);
            Image img = segGo.GetComponent<Image>();
            img.sprite = pillowSprite;
            img.type = Image.Type.Simple;
            img.preserveAspect = true;
            img.color = PillowAwakeTint;
            img.raycastTarget = false;

            _segments = new[] { img };

            GameObject ratioWrap = new GameObject("Ratio", typeof(RectTransform), typeof(LayoutElement));
            ratioWrap.transform.SetParent(_segmentsRoot, false);
            LayoutElement ratioLe = ratioWrap.GetComponent<LayoutElement>();
            ratioLe.flexibleWidth = 1f;
            ratioLe.minWidth = 52f;
            ratioLe.preferredHeight = 22f;
            ratioLe.flexibleHeight = 1f;

            if (_hudFont != null)
            {
                GameObject labelGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
                RectTransform labelRt = (RectTransform)labelGo.transform;
                labelRt.SetParent(ratioWrap.transform, false);
                StretchFull(labelRt);
                _ratioTmp = labelGo.GetComponent<TextMeshProUGUI>();
                ApplyFont(_ratioTmp, _hudFont);
                _ratioTmp.fontSize = 14f;
                _ratioTmp.color = BoostYellow;
                _ratioTmp.text = "0/0";
                _ratioTmp.alignment = TextAlignmentOptions.MidlineLeft;
                _ratioTmp.textWrappingMode = TextWrappingModes.NoWrap;
                _ratioTmp.overflowMode = TextOverflowModes.Overflow;
                _ratioTmp.raycastTarget = false;
                _ratioTmp.margin = Vector4.zero;
            }
        }
        else
        {
            _segments = new Image[total];
            for (int i = 0; i < total; i++)
            {
                GameObject segGo = new GameObject($"Seg_{i}", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
                segGo.transform.SetParent(_segmentsRoot, false);
                Image img = segGo.GetComponent<Image>();
                img.sprite = pillowSprite;
                img.type = Image.Type.Simple;
                img.preserveAspect = true;
                img.color = PillowAwakeTint;
                img.raycastTarget = false;
                LayoutElement le = segGo.GetComponent<LayoutElement>();
                le.flexibleWidth = 1f;
                le.preferredHeight = 22f;
                le.minWidth = 8f;
                _segments[i] = img;
            }
        }
    }

    private static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private void CreateBedSlot(Transform rowParent)
    {
        float cellWidth = 28f;
        GameObject slotWrap = new GameObject("BedSlot", typeof(RectTransform));
        RectTransform slotRt = (RectTransform)slotWrap.transform;
        slotRt.SetParent(rowParent, false);
        ConfigureRowItemStretch(slotRt, cellWidth);
        LayoutElement le = slotWrap.AddComponent<LayoutElement>();
        le.preferredWidth = cellWidth;
        le.minWidth = cellWidth;
        le.flexibleWidth = 0f;
        le.minHeight = -1f;
        le.preferredHeight = -1f;
        le.flexibleHeight = 1f;

        _bedIconGo = new GameObject("BedIcon", typeof(RectTransform), typeof(Image));
        RectTransform bedRt = (RectTransform)_bedIconGo.transform;
        bedRt.SetParent(slotRt, false);
        StretchFull(bedRt);
        Image bedImg = _bedIconGo.GetComponent<Image>();
        bedImg.sprite = GetBedIconSprite();
        bedImg.type = Image.Type.Simple;
        bedImg.color = Color.white;
        bedImg.preserveAspect = true;
        bedImg.raycastTarget = false;

        _arrowChaserGo = new GameObject("ArrowChaser", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        RectTransform arrowRt = (RectTransform)_arrowChaserGo.transform;
        arrowRt.SetParent(slotRt, false);
        StretchFull(arrowRt);
        HorizontalLayoutGroup hlg = _arrowChaserGo.GetComponent<HorizontalLayoutGroup>();
        hlg.padding = new RectOffset(0, 0, 0, 0);
        hlg.spacing = 1f;
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childControlWidth = false;
        hlg.childControlHeight = false;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;

        Sprite chevron = GetChevronSprite();
        _arrowImages = new Image[3];
        for (int i = 0; i < 3; i++)
        {
            GameObject arrowGo = new GameObject($"Arrow_{i}", typeof(RectTransform), typeof(Image));
            RectTransform aRt = (RectTransform)arrowGo.transform;
            aRt.SetParent(arrowRt, false);
            aRt.sizeDelta = new Vector2(8f, 14f);
            Image img = arrowGo.GetComponent<Image>();
            img.sprite = chevron;
            img.type = Image.Type.Simple;
            img.color = ArrowDimTint;
            img.preserveAspect = true;
            img.raycastTarget = false;
            _arrowImages[i] = img;
        }

        _arrowChaserGo.SetActive(false);
        _bedIconGo.SetActive(true);
    }

    private static void CreateIconInRow(Transform rowParent, string name, Sprite sprite, float cellWidth, Color tint)
    {
        GameObject wrap = new GameObject(name, typeof(RectTransform));
        RectTransform wrapRt = (RectTransform)wrap.transform;
        wrapRt.SetParent(rowParent, false);
        ConfigureRowItemStretch(wrapRt, cellWidth);
        LayoutElement le = wrap.AddComponent<LayoutElement>();
        le.preferredWidth = cellWidth;
        le.minWidth = cellWidth;
        le.flexibleWidth = 0f;
        le.minHeight = -1f;
        le.preferredHeight = -1f;
        le.flexibleHeight = 1f;

        GameObject imgGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        RectTransform imgRt = (RectTransform)imgGo.transform;
        imgRt.SetParent(wrapRt, false);
        StretchFull(imgRt);
        Image img = imgGo.GetComponent<Image>();
        img.sprite = sprite;
        img.type = Image.Type.Simple;
        img.color = tint;
        img.preserveAspect = true;
        img.raycastTarget = false;
    }

    private static TextMeshProUGUI CreateTmpInRow(
        Transform rowParent,
        string name,
        TMP_FontAsset font,
        float cellWidth,
        float fontSize,
        Color color,
        string text,
        TextAlignmentOptions alignment)
    {
        GameObject wrap = new GameObject(name, typeof(RectTransform));
        RectTransform wrapRt = (RectTransform)wrap.transform;
        wrapRt.SetParent(rowParent, false);
        ConfigureRowItemStretch(wrapRt, cellWidth);
        LayoutElement le = wrap.AddComponent<LayoutElement>();
        le.preferredWidth = cellWidth;
        le.minWidth = cellWidth;
        le.flexibleWidth = 0f;
        le.minHeight = -1f;
        le.preferredHeight = -1f;
        le.flexibleHeight = 1f;

        GameObject labelGo = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        RectTransform labelRt = (RectTransform)labelGo.transform;
        labelRt.SetParent(wrapRt, false);
        StretchFull(labelRt);

        TextMeshProUGUI tmp = labelGo.GetComponent<TextMeshProUGUI>();
        ApplyFont(tmp, font);
        tmp.fontSize = fontSize;
        tmp.color = color;
        tmp.text = text;
        tmp.alignment = alignment;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.overflowMode = TextOverflowModes.Overflow;
        tmp.raycastTarget = false;
        tmp.margin = Vector4.zero;
        return tmp;
    }

    private static void ConfigureRowItemStretch(RectTransform rt, float width)
    {
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 0.5f);
        rt.sizeDelta = new Vector2(width, 0f);
        rt.anchoredPosition = Vector2.zero;
    }

    private static void ApplyFont(TextMeshProUGUI tmp, TMP_FontAsset font)
    {
        tmp.font = font;
        if (font != null && font.material != null)
            tmp.fontSharedMaterial = font.material;
    }

    private static TMP_FontAsset ResolveTmpFont(Transform nearUi)
    {
        if (TMP_Settings.instance != null && TMP_Settings.defaultFontAsset != null)
            return TMP_Settings.defaultFontAsset;

        TMP_FontAsset resolvedFont = null;
        for (int p = 0; p < HudFontResourcePaths.Length; p++)
        {
            TMP_FontAsset loaded = Resources.Load<TMP_FontAsset>(HudFontResourcePaths[p]);
            if (loaded != null)
            {
                resolvedFont = loaded;
                break;
            }
        }

        if (resolvedFont == null)
        {
            Transform root = nearUi;
            while (root.parent != null)
                root = root.parent;

            TextMeshProUGUI[] sceneTmps = root.GetComponentsInChildren<TextMeshProUGUI>(true);
            for (int i = 0; i < sceneTmps.Length; i++)
            {
                TMP_FontAsset f = sceneTmps[i].font;
                if (f != null)
                {
                    resolvedFont = f;
                    break;
                }
            }
        }

        if (resolvedFont == null)
        {
            TMP_FontAsset[] allFonts = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
            for (int i = 0; i < allFonts.Length; i++)
            {
                if (allFonts[i] != null && allFonts[i].material != null)
                {
                    resolvedFont = allFonts[i];
                    break;
                }
            }
        }

        if (resolvedFont != null && TMP_Settings.defaultFontAsset == null)
        {
            try
            {
                TMP_Settings.defaultFontAsset = resolvedFont;
            }
            catch
            {
            }
        }

        return resolvedFont;
    }

    private static readonly string[] HudFontResourcePaths =
    {
        "Fonts & Materials/LiberationSans SDF",
        "Fonts & Materials/LiberationSans SDF TMP",
    };

    private static Sprite _whiteSprite;
    private static Sprite _moonCrescentSprite;
    private static Sprite _bedFilledSprite;
    private static Sprite _resolvedMoonIcon;
    private static Sprite _resolvedBedIcon;
    private static Sprite _resolvedPillowSprite;

    private const string EmbeddedMoonName = "sleephud_moon.png";
    private const string EmbeddedBedName = "sleephud_bed.png";
    private const string EmbeddedPillowName = "sleephud_pillow.png";

    private static Sprite GetMoonIconSprite()
    {
        if (_resolvedMoonIcon != null)
            return _resolvedMoonIcon;
        _resolvedMoonIcon = TryLoadEmbeddedPngSprite(EmbeddedMoonName) ?? GetMoonCrescentSprite();
        return _resolvedMoonIcon;
    }

    private static Sprite GetBedIconSprite()
    {
        if (_resolvedBedIcon != null)
            return _resolvedBedIcon;
        _resolvedBedIcon = TryLoadEmbeddedPngSprite(EmbeddedBedName) ?? GetBedFilledSprite();
        return _resolvedBedIcon;
    }

    private static Sprite GetPillowSegmentSprite()
    {
        if (_resolvedPillowSprite != null)
            return _resolvedPillowSprite;
        _resolvedPillowSprite = TryLoadEmbeddedPngSprite(EmbeddedPillowName) ?? BaseWhiteSprite();
        return _resolvedPillowSprite;
    }

    private static bool TryLoadPngIntoTexture(Texture2D tex, byte[] bytes)
    {
        try
        {
            Type t = Type.GetType("UnityEngine.ImageConversion, UnityEngine.ImageConversionModule");
            if (t == null)
                return false;
            MethodInfo m = t.GetMethod(
                "LoadImage",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(Texture2D), typeof(byte[]) },
                null);
            if (m == null)
                return false;
            return (bool)m.Invoke(null, new object[] { tex, bytes });
        }
        catch
        {
            return false;
        }
    }

    private static Sprite TryLoadEmbeddedPngSprite(string manifestEndsWith)
    {
        try
        {
            Assembly asm = Assembly.GetExecutingAssembly();
            string match = null;
            string[] resourceNames = asm.GetManifestResourceNames();
            for (int i = 0; i < resourceNames.Length; i++)
            {
                string name = resourceNames[i];
                if (name.EndsWith(manifestEndsWith, StringComparison.OrdinalIgnoreCase))
                {
                    match = name;
                    break;
                }
            }

            if (match == null)
                return null;

            using (System.IO.Stream stream = asm.GetManifestResourceStream(match))
            {
                if (stream == null)
                    return null;

                int len = (int)stream.Length;
                byte[] bytes = new byte[len];
                if (stream.Read(bytes, 0, len) != len)
                    return null;

                Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!TryLoadPngIntoTexture(tex, bytes))
                    return null;

                tex.filterMode = FilterMode.Bilinear;
                tex.wrapMode = TextureWrapMode.Clamp;
                BetterSleepBruh.Log.Debug($"[SleepHud] Using embedded HUD icon: {match}");
                return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
            }
        }
        catch (Exception ex)
        {
            BetterSleepBruh.Log.Warning($"[SleepHud] Could not load embedded {manifestEndsWith}: {ex.Message}");
            return null;
        }
    }

    private static Sprite GetMoonCrescentSprite()
    {
        if (_moonCrescentSprite != null)
            return _moonCrescentSprite;

        const int n = 32;
        Texture2D tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        Color32 clear = new Color32(0, 0, 0, 0);
        float cx = (n - 1) / 2f;
        float cy = (n - 1) / 2f;
        float rOuter = n * 0.44f;
        float rInner = n * 0.36f;
        float cutCx = cx + n * 0.14f;
        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                float dx = x - cx;
                float dy = y - cy;
                bool inDisk = dx * dx + dy * dy <= rOuter * rOuter;
                float dx2 = x - cutCx;
                float dy2 = y - cy;
                bool inCut = dx2 * dx2 + dy2 * dy2 <= rInner * rInner;
                tex.SetPixel(x, y, inDisk && !inCut ? MoonBeige : clear);
            }
        }

        tex.Apply();
        tex.filterMode = FilterMode.Point;
        _moonCrescentSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
        return _moonCrescentSprite;
    }

    private static Sprite GetBedFilledSprite()
    {
        if (_bedFilledSprite != null)
            return _bedFilledSprite;

        const int w = 36;
        const int h = 24;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
                tex.SetPixel(x, y, new Color32(0, 0, 0, 0));
        }

        Color32 shade = new Color32((byte)(MoonBeige.r * 0.72f), (byte)(MoonBeige.g * 0.72f), (byte)(MoonBeige.b * 0.72f), 255);
        FillRect(tex, w, h, 3, 3, 22, 5, shade);
        FillRect(tex, w, h, 2, 7, 24, 11, MoonBeige);
        FillRect(tex, w, h, 24, 5, 10, 15, MoonBeige);
        FillRect(tex, w, h, 5, 9, 12, 5, new Color32(255, 255, 255, 90));

        tex.Apply();
        tex.filterMode = FilterMode.Point;
        _bedFilledSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
        return _bedFilledSprite;
    }

    private static void FillRect(Texture2D tex, int tw, int th, int x0, int y0, int rw, int rh, Color32 c)
    {
        for (int y = y0; y < y0 + rh && y < th; y++)
        {
            for (int x = x0; x < x0 + rw && x < tw; x++)
                tex.SetPixel(x, y, c);
        }
    }

    private static Sprite _chevronSprite;

    private static Sprite GetChevronSprite()
    {
        if (_chevronSprite != null)
            return _chevronSprite;

        const int w = 8;
        const int h = 14;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        Color32 clear = new Color32(0, 0, 0, 0);
        Color32 white = new Color32(255, 255, 255, 255);

        for (int y = 0; y < h; y++)
        {
            int distFromMid = y > 6 ? (y - 7) : (6 - y);
            int xCenter = 7 - distFromMid;
            for (int x = 0; x < w; x++)
            {
                bool isChevron = x == xCenter || x == xCenter - 1;
                tex.SetPixel(x, y, isChevron ? white : clear);
            }
        }

        tex.Apply();
        tex.filterMode = FilterMode.Point;
        _chevronSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
        return _chevronSprite;
    }

    private static Sprite BaseWhiteSprite()
    {
        if (_whiteSprite != null)
            return _whiteSprite;
        Texture2D tex = Texture2D.whiteTexture;
        _whiteSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
        return _whiteSprite;
    }
}
