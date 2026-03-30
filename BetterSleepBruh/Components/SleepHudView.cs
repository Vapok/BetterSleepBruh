using System;
using System.Reflection;
using BetterSleepBruh.Configuration;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BetterSleepBruh.Components;

/*
* Client UI only: displays server-driven sleep HUD data from routed RPCs. Does not modify world time.
*/
public sealed class SleepHudView : MonoBehaviour
{
    private RectTransform _segmentsRoot;
    private Image[] _segments = System.Array.Empty<Image>();
    private TextMeshProUGUI _boostTmp;
    private int _lastTotal = -1;

    private static readonly Color BgMidnight = new(0f, 0f, 0f, 0.39f);
    private static readonly Color PillowAwakeTint = new(0.32f, 0.32f, 0.34f, 1f);
    private static readonly Color32 MoonBeige = new(235, 225, 190, 255);
    private static readonly Color32 BoostYellow = new(255, 183, 91, 255);

    private const float StripHeightPx = 40f;

    private void Awake()
    {
        BetterSleepBruh.Log.Debug($"SleepHudView Is Awoken");
    }

    private void Start()
    {
        BetterSleepBruh.Log.Debug($"SleepHudView Is Starting, registering RPC's");
        ZRoutedRpc.instance.Register<int, int, double>("RPC_SleepingPlayerInfo", RPC_SleepingPlayerInfo);
        ZRoutedRpc.instance.Register("RPC_StartSleep", RPC_StartSleep);
        ZRoutedRpc.instance.Register("RPC_StopSleep", RPC_StopSleep);

    }

    private void RPC_SleepingPlayerInfo(long sender, int totalPlayers, int playersSleeping, double sleepBoost)
    {
        if (Player.m_localPlayer == null)
            return;

        BetterSleepBruh.Log.Debug($"[CLIENT] Total Players: {totalPlayers}");
        BetterSleepBruh.Log.Debug($"[CLIENT] Players Sleeping: {playersSleeping}");
        BetterSleepBruh.Log.Debug($"[CLIENT] Sleep Boost (extra rate × dt): {sleepBoost}");

        gameObject.SetActive(EnvMan.CanSleep());
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

        var player = Player.m_localPlayer;
        
        player.SetSleeping(false);
        
        if (player.InBed())
        {
            player.AttachStop();
        }
        
        gameObject.SetActive(false);
    }

    
    public static SleepHudView TryCreate(Transform mapGeometryTransform, float gapBelowMinimap = 1f)
    {
        if (mapGeometryTransform == null)
            return null;
        var map = mapGeometryTransform as RectTransform;
        if (map == null)
            return null;

        var existing = map.Find("BetterSleepBruh_SleepHud");
        if (existing != null)
            Destroy(existing.gameObject);

        var white = BaseWhiteSprite();
        var stripHeight = StripHeightPx;

        var rootGo = new GameObject("BetterSleepBruh_SleepHud", typeof(RectTransform), typeof(Image), typeof(SleepHudView));
        var rt = rootGo.GetComponent<RectTransform>();
        rt.SetParent(map, false);
        rt.SetAsLastSibling();
        rt.localScale = Vector3.one;

        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.sizeDelta = new Vector2(0f, stripHeight);
        rt.anchoredPosition = new Vector2(0f, -gapBelowMinimap);

        var bg = rootGo.GetComponent<Image>();
        bg.sprite = white;
        bg.type = Image.Type.Simple;
        bg.color = BgMidnight;
        bg.raycastTarget = false;
        rootGo.AddComponent<RectMask2D>();

        var font = ResolveTmpFont(map);
        if (font == null)
        {
            BetterSleepBruh.Log.Warning("[SleepHud] No TMP font; not creating sleep HUD.");
            Destroy(rootGo);
            return null;
        }

        var view = rootGo.GetComponent<SleepHudView>();
        view.BuildContent(rootGo.GetComponent<RectTransform>(), font);
        return view;
    }

    public void Refresh(int totalPlayers, int playersSleeping)
    {
        if (!isActiveAndEnabled)
            return;

        totalPlayers = Mathf.Max(0, totalPlayers);
        playersSleeping = Mathf.Clamp(playersSleeping, 0, totalPlayers);

        EnsureSegments(totalPlayers);
        for (var i = 0; i < _segments.Length; i++)
            _segments[i].color = i < playersSleeping ? Color.white : PillowAwakeTint;

        if (_boostTmp == null)
            return;

        var pct = GetBoostLabelPercent(totalPlayers, playersSleeping);
        if (pct <= 0.0001)
            _boostTmp.text = "+0%";
        else
            _boostTmp.text = $"+{pct:F0}%";
    }

    private static double GetBoostLabelPercent(int playerCount, int playersSleeping)
    {
        if (playerCount <= 1)
            return 0.0;
        if (playersSleeping <= 0 || playersSleeping >= playerCount)
            return 0.0;
        var sleepFraction = playersSleeping / (double)(playerCount - 1);
        return ConfigRegistry.BonusMultiplier.Value * sleepFraction * 100.0;
    }

    private void BuildContent(RectTransform rootRt, TMP_FontAsset font)
    {
        var rowGo = new GameObject("Row", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        var rowRt = (RectTransform)rowGo.transform;
        rowRt.SetParent(rootRt, false);
        StretchFull(rowRt);
        var hlg = rowGo.GetComponent<HorizontalLayoutGroup>();
        hlg.padding = new RectOffset(4, 4, 3, 3);
        hlg.spacing = 4;
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = true;

        CreateIconInRow(rowRt, "Moon", GetMoonIconSprite(), 28f, Color.white);
        CreateIconInRow(rowRt, "Bed", GetBedIconSprite(), 26f, Color.white);

        var barGo = new GameObject("BarHost", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(Image));
        var barRt = (RectTransform)barGo.transform;
        barRt.SetParent(rowRt, false);
        var barBg = barGo.GetComponent<Image>();
        barBg.sprite = BaseWhiteSprite();
        barBg.type = Image.Type.Simple;
        barBg.color = new Color(0f, 0f, 0f, 0.25f);
        barBg.raycastTarget = false;
        var barH = barGo.GetComponent<HorizontalLayoutGroup>();
        barH.spacing = 3;
        barH.padding = new RectOffset(4, 4, 4, 4);
        barH.childAlignment = TextAnchor.MiddleCenter;
        barH.childControlWidth = true;
        barH.childControlHeight = true;
        barH.childForceExpandWidth = true;
        barH.childForceExpandHeight = true;
        var barLe = barGo.AddComponent<LayoutElement>();
        barLe.flexibleWidth = 1f;
        barLe.minWidth = 48f;
        barLe.preferredHeight = 26f;
        _segmentsRoot = barRt;

        _boostTmp = CreateTmpInRow(rowRt, "Boost", font, 40f, 14f, BoostYellow, "0%", TextAlignmentOptions.MidlineRight);
    }

    private void EnsureSegments(int total)
    {
        if (total == _lastTotal && _segments.Length == total)
            return;

        _lastTotal = total;
        foreach (Transform c in _segmentsRoot)
            Destroy(c.gameObject);

        if (total <= 0)
        {
            _segments = System.Array.Empty<Image>();
            return;
        }

        var pillowSprite = GetPillowSegmentSprite();
        _segments = new Image[total];
        for (var i = 0; i < total; i++)
        {
            var segGo = new GameObject($"Seg_{i}", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            segGo.transform.SetParent(_segmentsRoot, false);
            var img = segGo.GetComponent<Image>();
            img.sprite = pillowSprite;
            img.type = Image.Type.Simple;
            img.preserveAspect = true;
            img.color = PillowAwakeTint;
            img.raycastTarget = false;
            var le = segGo.GetComponent<LayoutElement>();
            le.flexibleWidth = 1f;
            le.preferredHeight = 22f;
            le.minWidth = 8f;
            _segments[i] = img;
        }
    }

    private static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private static void CreateIconInRow(Transform rowParent, string name, Sprite sprite, float cellWidth, Color tint)
    {
        var wrap = new GameObject(name, typeof(RectTransform));
        var wrapRt = (RectTransform)wrap.transform;
        wrapRt.SetParent(rowParent, false);
        ConfigureRowItemStretch(wrapRt, cellWidth);
        var le = wrap.AddComponent<LayoutElement>();
        le.preferredWidth = cellWidth;
        le.minWidth = cellWidth;
        le.flexibleWidth = 0f;
        le.minHeight = -1f;
        le.preferredHeight = -1f;
        le.flexibleHeight = 1f;

        var imgGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        var imgRt = (RectTransform)imgGo.transform;
        imgRt.SetParent(wrapRt, false);
        StretchFull(imgRt);
        var img = imgGo.GetComponent<Image>();
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
        var wrap = new GameObject(name, typeof(RectTransform));
        var wrapRt = (RectTransform)wrap.transform;
        wrapRt.SetParent(rowParent, false);
        ConfigureRowItemStretch(wrapRt, cellWidth);
        var le = wrap.AddComponent<LayoutElement>();
        le.preferredWidth = cellWidth;
        le.minWidth = cellWidth;
        le.flexibleWidth = 0f;
        le.minHeight = -1f;
        le.preferredHeight = -1f;
        le.flexibleHeight = 1f;

        var labelGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        var labelRt = (RectTransform)labelGo.transform;
        labelRt.SetParent(wrapRt, false);
        StretchFull(labelRt);

        var tmp = labelGo.GetComponent<TextMeshProUGUI>();
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

        foreach (var path in HudFontResourcePaths)
        {
            var loaded = Resources.Load<TMP_FontAsset>(path);
            if (loaded != null)
                return loaded;
        }

        var root = nearUi;
        while (root.parent != null)
            root = root.parent;

        var sceneTmps = root.GetComponentsInChildren<TextMeshProUGUI>(true);
        for (var i = 0; i < sceneTmps.Length; i++)
        {
            var f = sceneTmps[i].font;
            if (f != null)
                return f;
        }

        var allFonts = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
        for (var i = 0; i < allFonts.Length; i++)
        {
            if (allFonts[i] != null && allFonts[i].material != null)
                return allFonts[i];
        }

        return null;
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
            var t = Type.GetType("UnityEngine.ImageConversion, UnityEngine.ImageConversionModule");
            if (t == null)
                return false;
            var m = t.GetMethod(
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
            var asm = Assembly.GetExecutingAssembly();
            string match = null;
            foreach (var name in asm.GetManifestResourceNames())
            {
                if (name.EndsWith(manifestEndsWith, StringComparison.OrdinalIgnoreCase))
                {
                    match = name;
                    break;
                }
            }

            if (match == null)
                return null;

            using (var stream = asm.GetManifestResourceStream(match))
            {
                if (stream == null)
                    return null;

                var len = (int)stream.Length;
                var bytes = new byte[len];
                if (stream.Read(bytes, 0, len) != len)
                    return null;

                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
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
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        var clear = new Color32(0, 0, 0, 0);
        var cx = (n - 1) / 2f;
        var cy = (n - 1) / 2f;
        var rOuter = n * 0.44f;
        var rInner = n * 0.36f;
        var cutCx = cx + n * 0.14f;
        for (var y = 0; y < n; y++)
        {
            for (var x = 0; x < n; x++)
            {
                var dx = x - cx;
                var dy = y - cy;
                var inDisk = dx * dx + dy * dy <= rOuter * rOuter;
                var dx2 = x - cutCx;
                var dy2 = y - cy;
                var inCut = dx2 * dx2 + dy2 * dy2 <= rInner * rInner;
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
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        for (var y = 0; y < h; y++)
        {
            for (var x = 0; x < w; x++)
                tex.SetPixel(x, y, new Color32(0, 0, 0, 0));
        }

        var shade = new Color32((byte)(MoonBeige.r * 0.72f), (byte)(MoonBeige.g * 0.72f), (byte)(MoonBeige.b * 0.72f), 255);
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
        for (var y = y0; y < y0 + rh && y < th; y++)
        {
            for (var x = x0; x < x0 + rw && x < tw; x++)
                tex.SetPixel(x, y, c);
        }
    }

    private static Sprite BaseWhiteSprite()
    {
        if (_whiteSprite != null)
            return _whiteSprite;
        var tex = Texture2D.whiteTexture;
        _whiteSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
        return _whiteSprite;
    }
}
