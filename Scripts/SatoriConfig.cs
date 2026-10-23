using BaseLib.Config;

namespace SatoriCharacter;

public sealed class SatoriConfig : SimpleModConfig
{
    [ConfigSection("FourthChapterSettings")]
    [ConfigHoverTip]
    public static bool EnableFourthChapter { get; set; } = true;
    [ConfigHoverTip]
    public static bool EnableChapterPreview { get; set; } = false;
    [ConfigHoverTip]
    public static bool ShowFinalBattleBgmCredit { get; set; } = true;
    [ConfigHoverTip]
    public static bool ShowFinalBattleMusicPlayer { get; set; } = true;
    private static bool _hideKoishiIntents=true;
    [ConfigHoverTip]
    public static bool HideKoishiIntents
    {
        get => _hideKoishiIntents;
        set { _hideKoishiIntents=value; KoishiHiddenIntentPatch.RefreshSettings(); }
    }
    private static bool _enableSpellVoices = true;
    private static float _spellVoiceVolume = 300f;
    private static bool _enableSpellBanner = true;
    private static float _spellBannerDuration = 1.8f;
    private static float _spellBannerScale = 1f;
    private static bool _enableDialogueVoices = true;
    private static float _dialogueVoiceVolume = 300f;

    [ConfigSection("DialogueVoiceSettings")]
    [ConfigHoverTip]
    public static bool EnableDialogueVoices
    {
        get => _enableDialogueVoices;
        set { _enableDialogueVoices=value; BattleDialogueVoices.RefreshSettings(); }
    }

    [ConfigSlider(0, 400, 10, Format = "{0:0}%")]
    [ConfigHoverTip]
    public static float DialogueVoiceVolume
    {
        get => _dialogueVoiceVolume;
        set
        {
            _dialogueVoiceVolume=float.IsFinite(value)?Math.Clamp(value,0f,400f):300f;
            BattleDialogueVoices.RefreshSettings();
        }
    }

    [ConfigSection("SpellVoiceSettings")]
    [ConfigHoverTip]
    public static bool EnableSpellVoices
    {
        get => _enableSpellVoices;
        set { _enableSpellVoices = value; SpellCardVoices.RefreshSettings(); }
    }

    [ConfigSlider(0, 400, 10, Format = "{0:0}%")]
    [ConfigHoverTip]
    public static float SpellVoiceVolume
    {
        get => _spellVoiceVolume;
        set
        {
            _spellVoiceVolume = float.IsFinite(value) ? Math.Clamp(value, 0f, 400f) : 300f;
            SpellCardVoices.RefreshSettings();
        }
    }

    [ConfigSection("SpellBannerSettings")]
    [ConfigHoverTip]
    public static bool EnableSpellBanner
    {
        get => _enableSpellBanner;
        set { _enableSpellBanner = value; if (!value) SpellCardBanner.Hide(); }
    }

    [ConfigHoverTip]
    public static bool UseJapaneseSpellNames { get; set; } = false;

    [ConfigSlider(0.6, 4, 0.1, Format = "{0:0.0}s")]
    [ConfigHoverTip]
    public static float SpellBannerDuration
    {
        get => _spellBannerDuration;
        set => _spellBannerDuration = float.IsFinite(value) ? Math.Clamp(value, 0.6f, 4f) : 1.8f;
    }

    [ConfigSlider(0.7, 1.5, 0.1, Format = "{0:0.0}x")]
    [ConfigHoverTip]
    public static float SpellBannerScale
    {
        get => _spellBannerScale;
        set => _spellBannerScale = float.IsFinite(value) ? Math.Clamp(value, 0.7f, 1.5f) : 1f;
    }
}
