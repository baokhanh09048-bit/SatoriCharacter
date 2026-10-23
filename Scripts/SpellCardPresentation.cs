using System.Text.Json;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace SatoriCharacter;

internal sealed record SpellVoiceClip(string Path, string Japanese);

internal static class SpellCardPresentation
{
    private static Dictionary<string, SpellVoiceClip>? _clips;
    private static readonly HashSet<string> ReportedFailures = new();

    internal static void Play(SatoriPoolCard card)
    {
        if (NonInteractiveMode.IsActive || CombatManager.Instance.IsEnding) return;
        var room = NCombatRoom.Instance;
        if (room == null || !GodotObject.IsInstanceValid(room) || !room.IsInsideTree()) return;
        SpellVoiceClip clip;
        try
        {
            if (_clips == null)
            {
                using var manifest = JsonDocument.Parse(Godot.FileAccess.GetFileAsString(
                    "res://SatoriCharacter/spell_voice_manifest.json"));
                _clips = manifest.RootElement.GetProperty("clips").EnumerateArray().ToDictionary(
                    c => c.GetProperty("key").GetString()!,
                    c => new SpellVoiceClip(c.GetProperty("path").GetString()!, c.GetProperty("japanese").GetString()!));
            }
            if (!_clips.TryGetValue(card.Spec.Key, out clip!)) return;
        }
        catch (Exception error)
        {
            ReportFailure("manifest", error);
            return;
        }

        // Visual and audio settings are independent, and neither delays card effects.
        if (SatoriConfig.EnableSpellBanner)
        {
            try
            {
                string title = SatoriConfig.UseJapaneseSpellNames
                    ? clip.Japanese
                    : new MegaCrit.Sts2.Core.Localization.LocString("cards", card.Id.Entry + ".title").GetFormattedText();
                SpellCardBanner.Show(room, title);
            }
            catch (Exception error) { ReportFailure("banner", error); }
        }
        SpellCardVoices.Play(room, card.Spec.Key, clip.Path);
    }

    private static void ReportFailure(string component, Exception error)
    {
        if (ReportedFailures.Add(component)) Log.Warn($"SatoriCharacter: spell {component} unavailable: {error.Message}");
    }
}
