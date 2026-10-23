using Godot;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Saves;

namespace SatoriCharacter;

internal static class SpellCardVoices
{
    private const string PlayerName = "SatoriSpellVoice";
    private static readonly Dictionary<string, AudioStream> Streams = new();
    private static readonly HashSet<string> ReportedFailures = new();

    internal static void Play(NCombatRoom room, string key, string path)
    {
        if (!SatoriConfig.EnableSpellVoices || SatoriConfig.SpellVoiceVolume <= 0
            || BattleDialogueVoices.IsPlaying(room)) return;
        try
        {
            if (!Streams.TryGetValue(key, out var stream))
            {
                stream = ResourceLoader.Load<AudioStream>(path);
                if (stream == null) throw new InvalidOperationException("Voice resource missing: " + path);
                Streams[key] = stream;
            }
            var player = room.GetNodeOrNull<AudioStreamPlayer>(PlayerName);
            if (player == null)
            {
                player = new AudioStreamPlayer { Name = PlayerName, MaxPolyphony = 1 };
                room.AddChild(player);
            }
            ApplySettings(player);
            // Rapid card plays replace the previous callout instead of stacking
            // voices or delaying card mechanics. Room disposal stops playback.
            player.Stop();
            player.Stream = stream;
            player.Play();
        }
        catch (Exception error)
        {
            if (ReportedFailures.Add(key))
                Log.Warn($"SatoriCharacter: spell voice unavailable ({key}): {error.Message}");
        }
    }

    internal static void Stop(NCombatRoom room) => room.GetNodeOrNull<AudioStreamPlayer>(PlayerName)?.Stop();

    internal static void RefreshSettings()
    {
        var room = NCombatRoom.Instance;
        if (room == null || !GodotObject.IsInstanceValid(room)) return;
        var player = room.GetNodeOrNull<AudioStreamPlayer>(PlayerName);
        if (player == null) return;
        if (!SatoriConfig.EnableSpellVoices || SatoriConfig.SpellVoiceVolume <= 0) player.Stop();
        ApplySettings(player);
    }

    private static void ApplySettings(AudioStreamPlayer player)
    {
        // Native SFX/Master buses handle the game's volume and background mute.
        bool hasSfxBus = AudioServer.GetBusIndex("SFX") >= 0;
        player.Bus = hasSfxBus ? "SFX" : "Master";
        float gain = SatoriConfig.SpellVoiceVolume / 100f;
        player.VolumeLinear = gain * (hasSfxBus ? 1f : Mathf.Pow(SaveManager.Instance.SettingsSave.VolumeSfx, 2f));
    }
}
