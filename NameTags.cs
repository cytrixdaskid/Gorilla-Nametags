using BepInEx;
using GorillaGameModes;
using HarmonyLib;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Debug = UnityEngine.Debug;

[BepInPlugin("com.cytrixgt.nametags", "Cytrix Nametags", "1.0.0")]
public class NametagMod : BaseUnityPlugin
{
    private Harmony harmony;
    private Dictionary<VRRig, GameObject> nametags = new Dictionary<VRRig, GameObject>();
    private bool nametagsEnabled = true;
    private float toggleCooldown = 0f;
    private TMP_FontAsset gorillaFont;

    void Awake()
    {
        harmony = new Harmony("com.cytrixgt.nametags");
        harmony.PatchAll();
        Logger.LogInfo("Nametag mod loaded!");
    }

    void LoadFont()
    {
        if (gorillaFont != null) return;
        TMP_FontAsset[] fonts = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
        foreach (TMP_FontAsset font in fonts)
        {
            if (font.name.Contains("Utopium"))
            {
                gorillaFont = font;
                Logger.LogInfo("Found font: " + font.name);
                break;
            }
        }
    }

    void Update()
    {
        if (Keyboard.current != null &&
            Keyboard.current.shiftKey.wasPressedThisFrame &&
            Time.time > toggleCooldown)
        {
            toggleCooldown = Time.time + 0.5f;
            nametagsEnabled = !nametagsEnabled;
            Logger.LogInfo("Nametags " + (nametagsEnabled ? "enabled" : "disabled"));

            foreach (var kvp in nametags)
            {
                if (kvp.Value != null)
                    kvp.Value.SetActive(nametagsEnabled);
            }
        }

        if (!nametagsEnabled) return;
        if (!VRRigCache.isInitialized) return;

        LoadFont();

        var activeRigs = VRRigCache.ActiveRigs;

        List<VRRig> toRemove = new List<VRRig>();
        foreach (var kvp in nametags)
        {
            if (kvp.Key == null || !System.Linq.Enumerable.Contains(activeRigs, kvp.Key))
            {
                if (kvp.Value != null) Destroy(kvp.Value);
                toRemove.Add(kvp.Key);
            }
        }
        foreach (var rig in toRemove) nametags.Remove(rig);

        foreach (VRRig rig in activeRigs)
        {
            try
            {
                if (rig == null || rig.isOfflineVRRig) continue;

                if (!nametags.ContainsKey(rig) || nametags[rig] == null)
                    nametags[rig] = CreateNametag(rig);

                UpdateNametag(nametags[rig], rig);
            }
            catch (System.Exception e)
            {
                Debug.LogError("Nametag error: " + e.Message);
            }
        }
    }

    GameObject CreateNametag(VRRig rig)
    {
        GameObject nametagObj = new GameObject("Nametag_" + rig.playerNameVisible);

        Canvas canvas = nametagObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 10;
        nametagObj.transform.localScale = Vector3.one * 0.004f;

        // background
        GameObject bg = new GameObject("Background");
        bg.transform.SetParent(nametagObj.transform, false);
        Image bgImage = bg.AddComponent<Image>();
        bgImage.color = new Color(0.05f, 0.05f, 0.05f, 0.85f);
        bgImage.sprite = CreateRoundedSprite();
        bgImage.type = Image.Type.Sliced;
        RectTransform bgRect = bg.GetComponent<RectTransform>();
        bgRect.sizeDelta = new Vector2(220f, 80f);

        // name text (top)
        GameObject nameObj = new GameObject("NameText");
        nameObj.transform.SetParent(bg.transform, false);
        TextMeshProUGUI nameText = nameObj.AddComponent<TextMeshProUGUI>();
        nameText.fontSize = 22f;
        nameText.fontStyle = FontStyles.Bold | FontStyles.UpperCase;
        nameText.alignment = TextAlignmentOptions.Center;
        nameText.color = Color.white;
        nameText.characterSpacing = 3f;
        if (gorillaFont != null) nameText.font = gorillaFont;
        RectTransform nameRect = nameObj.GetComponent<RectTransform>();
        nameRect.sizeDelta = new Vector2(210f, 36f);
        nameRect.anchoredPosition = new Vector2(0f, 18f);

        // color text (bottom)
        GameObject colorObj = new GameObject("ColorText");
        colorObj.transform.SetParent(bg.transform, false);
        TextMeshProUGUI colorText = colorObj.AddComponent<TextMeshProUGUI>();
        colorText.fontSize = 30f;
        colorText.alignment = TextAlignmentOptions.Center;
        if (gorillaFont != null) colorText.font = gorillaFont;
        RectTransform colorRect = colorObj.GetComponent<RectTransform>();
        colorRect.sizeDelta = new Vector2(210f, 28f);
        colorRect.anchoredPosition = new Vector2(0f, -16f);

        return nametagObj;
    }

    Sprite CreateRoundedSprite()
    {
        int w = 220, h = 80, r = 40;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);

        for (int x = 0; x < w; x++)
        {
            for (int y = 0; y < h; y++)
            {
                float dx = Mathf.Max(0, Mathf.Max(r - x, x - (w - r)));
                float dy = Mathf.Max(0, Mathf.Max(r - y, y - (h - r)));
                tex.SetPixel(x, y, (dx * dx + dy * dy <= r * r) ? Color.white : Color.clear);
            }
        }

        tex.Apply();
        return Sprite.Create(tex,
            new Rect(0, 0, w, h),
            new Vector2(0.5f, 0.5f),
            100f, 0,
            SpriteMeshType.FullRect,
            new Vector4(r, r, r, r));
    }

    void UpdateNametag(GameObject nametagObj, VRRig rig)
    {
        if (nametagObj == null || rig == null) return;

        nametagObj.SetActive(true);

        // position above head
        Vector3 headPos = rig.nameTagAnchor != null
            ? rig.nameTagAnchor.transform.position
            : rig.transform.position + Vector3.up * 0.5f;
        nametagObj.transform.position = headPos + Vector3.up * 0.8f;

        // rotate to face local player
        GameObject localRig = GorillaTagger.Instance?.offlineVRRig?.gameObject;
        if (localRig != null)
        {
            Vector3 direction = nametagObj.transform.position - localRig.transform.position;
            direction.y = 0f;
            if (direction != Vector3.zero)
                nametagObj.transform.rotation = Quaternion.LookRotation(direction);
        }

        // update name
        TextMeshProUGUI nameText = nametagObj.transform
            .Find("Background/NameText")?.GetComponent<TextMeshProUGUI>();
        if (nameText != null)
            nameText.text = rig.playerNameVisible;

        // update color display
        TextMeshProUGUI colorText = nametagObj.transform
    .Find("Background/ColorText")?.GetComponent<TextMeshProUGUI>();

        if (colorText != null)
        {
            try
            {
                Color c = rig.playerColor;
                int r = Mathf.Clamp(Mathf.RoundToInt(c.r * 8f) + 0, 0, 9);
                int g = Mathf.Clamp(Mathf.RoundToInt(c.g * 8f) + 0, 0, 9);
                int b = Mathf.Clamp(Mathf.RoundToInt(c.b * 8f) + 0, 0, 9);

                colorText.text =
                    $"<color=#FF6666>{r}</color>" +
                    $"<color=#66FF66>{g}</color>" +
                    $"<color=#6699FF>{b}</color>";
            }
            catch (System.Exception e)
            {
                Debug.LogError("Color error: " + e.Message);
                colorText.color = Color.gray;
                colorText.text = "???";
            }
        }
    }
}