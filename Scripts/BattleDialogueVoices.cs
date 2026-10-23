using System.Text.Json;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Saves;

namespace SatoriCharacter;

internal sealed record DialogueVoiceClip(string Path,double Seconds);

internal static class BattleDialogueVoices
{
    internal const string ManifestPath="res://SatoriCharacter/dialogue_voice_manifest.json";
    private const string PlayerName="SatoriBattleDialogueVoice";
    private static Dictionary<string,DialogueVoiceClip>? _clips;
    private static readonly Dictionary<string,AudioStream> Streams=new();
    private static readonly HashSet<string> Failures=new();

    private static Dictionary<string,DialogueVoiceClip> Clips
    {
        get
        {
            if (_clips!=null) return _clips;
            try
            {
                using var manifest=JsonDocument.Parse(Godot.FileAccess.GetFileAsString(ManifestPath));
                _clips=manifest.RootElement.GetProperty("clips").EnumerateArray().ToDictionary(
                    c=>c.GetProperty("key").GetString()!,
                    c=>new DialogueVoiceClip(c.GetProperty("path").GetString()!,c.GetProperty("seconds").GetDouble()));
            }
            catch (Exception error)
            {
                Report("manifest",error);
                _clips=new(); // Text bubbles remain usable without the optional audio.
            }
            return _clips;
        }
    }

    internal static IEnumerable<string> AssetPaths=>Clips.Values.Select(c=>c.Path).Append(ManifestPath);
    internal static bool IsPlaying(NCombatRoom? room)=>room!=null && GodotObject.IsInstanceValid(room)
        && room.GetNodeOrNull<AudioStreamPlayer>(PlayerName) is {Playing:true};

    internal static double DisplaySeconds(string key)
    {
        // Keep localized subtitles readable as well as allowing the whole Japanese
        // recording to finish. Fast mode must not shorten the spoken sentence.
        double seconds=Math.Clamp(ChapterFour.Text(key).GetFormattedText().Length*.075,3.5,10);
        if (SatoriConfig.EnableDialogueVoices && SatoriConfig.DialogueVoiceVolume>0
            && Clips.TryGetValue(key,out var clip)) seconds=Math.Max(seconds,clip.Seconds+.35);
        return seconds;
    }

    internal static void Play(NCombatRoom room,string key)
    {
        if (!SatoriConfig.EnableDialogueVoices || SatoriConfig.DialogueVoiceVolume<=0
            || NonInteractiveMode.IsActive || CombatManager.Instance.IsEnding || !room.IsInsideTree()) return;
        if (!Clips.TryGetValue(key,out var clip)) return; // Companions have no recordings.
        try
        {
            if (!Streams.TryGetValue(key,out var stream))
            {
                stream=ResourceLoader.Load<AudioStream>(clip.Path);
                if (stream==null) throw new InvalidOperationException("Missing audio: "+clip.Path);
                Streams[key]=stream;
            }
            var player=room.GetNodeOrNull<AudioStreamPlayer>(PlayerName);
            if (player==null)
            {
                player=new AudioStreamPlayer {Name=PlayerName,MaxPolyphony=1};
                room.AddChild(player);
                player.Finished+=()=>ChapterFourMusic.RefreshVolume(room);
            }
            SpellCardVoices.Stop(room);
            player.Stop();
            player.Stream=stream;
            ApplySettings(player);
            player.Play();
            ChapterFourMusic.RefreshVolume(room);
        }
        catch (Exception error) { Report(key,error); }
    }

    internal static void Stop(NCombatRoom room)
    {
        room.GetNodeOrNull<AudioStreamPlayer>(PlayerName)?.Stop();
        ChapterFourMusic.RefreshVolume(room);
    }
    internal static void RefreshSettings()
    {
        var room=NCombatRoom.Instance;
        if (room==null || !GodotObject.IsInstanceValid(room)) return;
        var player=room.GetNodeOrNull<AudioStreamPlayer>(PlayerName);
        if (player==null) return;
        if (!SatoriConfig.EnableDialogueVoices || SatoriConfig.DialogueVoiceVolume<=0) Stop(room);
        ApplySettings(player);
    }
    private static void ApplySettings(AudioStreamPlayer player)
    {
        bool sfx=AudioServer.GetBusIndex("SFX")>=0;
        player.Bus=sfx?"SFX":"Master";
        player.VolumeLinear=SatoriConfig.DialogueVoiceVolume/100f
            *(sfx?1f:Mathf.Pow(SaveManager.Instance.SettingsSave.VolumeSfx,2f));
    }
    private static void Report(string key,Exception error)
    {
        if (Failures.Add(key)) Log.Warn($"SatoriCharacter: dialogue voice unavailable ({key}): {error.Message}");
    }
}
