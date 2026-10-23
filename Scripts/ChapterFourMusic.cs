using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Nodes.Audio;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.Fonts;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace SatoriCharacter;

internal static class ChapterFourMusic
{
    // Keep the native encounter's custom-music identifier stable.
    internal const string Path="res://SatoriCharacter/assets/music/koishi-514.ogg";
    private const string PlayerName="SatoriKoishiMusic";
    private const string CreditName="SatoriKoishiMusicCredit";
    private const string ControlsName="SatoriKoishiMusicControls";
    private const string IntroductionFinished="SatoriKoishiIntroductionFinished";
    private const string MusicStopped="SatoriKoishiMusicStopped";
    private const float TrackVolume=.75f;
    private sealed record Track(string Id,string Title,string Artist,string Path);
    private static readonly Lazy<Track[]> Playlist=new(()=>
        JsonSerializer.Deserialize<Track[]>(Godot.FileAccess.GetFileAsString(
            "res://SatoriCharacter/final_battle_music_manifest.json"),
            new JsonSerializerOptions {PropertyNameCaseInsensitive=true})
        is {Length:>0} tracks ? tracks : throw new InvalidDataException("Final battle playlist is empty."));
    private sealed class Session(AudioStreamPlayer player)
    {
        internal readonly AudioStreamPlayer Player=player;
        internal int Index;
        internal bool Started,Paused;
        internal PanelContainer? Controls;
        internal Label? Title,Artist;
        internal Button? Pause;
    }
    private static readonly ConditionalWeakTable<NCombatRoom,Session> Sessions=new();
    internal static void Start(NRunMusicController controller)
    {
        var room=NCombatRoom.Instance;
        if (room==null || CombatManager.Instance.IsEnding || room.HasMeta(MusicStopped) || room.HasNode(PlayerName)) return;
        var player=new AudioStreamPlayer {Name=PlayerName,ProcessMode=Node.ProcessModeEnum.Always};
        var session=new Session(player);
        if (!LoadTrack(session,0)) {player.Free();return;}
        Sessions.Add(room,session);
        // Native FMOD has no bank event for these files; room ownership handles cleanup.
        if (AccessTools.Field(typeof(NRunMusicController),"_proxy").GetValue(controller) is Node proxy)
            proxy.Call("stop_music");
        room.AddChild(player);
        player.Finished+=()=>ChangeTrack(room,1);
        ApplyVolume(player);
        TryStartPlayback(room);
        var timer=new Godot.Timer {WaitTime=.2,ProcessMode=Node.ProcessModeEnum.Always};
        player.AddChild(timer);
        timer.Timeout+=()=>
        {
            if (!GodotObject.IsInstanceValid(player)) return;
            if (CombatManager.Instance.IsEnding) {Stop();return;}
            ApplyVolume(player);
            RefreshControls(room,session);
        };
        timer.Start();
        player.TreeExiting+=()=>
        {
            Sessions.Remove(room);
            if (GodotObject.IsInstanceValid(session.Controls)) session.Controls!.QueueFree();
            if (GodotObject.IsInstanceValid(controller) && controller.IsInsideTree()) controller.UpdateMusic();
        };
    }
    private static bool LoadTrack(Session session,int index)
    {
        var stream=ResourceLoader.Load<AudioStreamOggVorbis>(Playlist.Value[index].Path);
        if (stream==null) return false;
        stream.Loop=false;
        session.Player.Stop();
        session.Player.Stream=stream;
        session.Index=index;
        session.Player.SetMeta("track_index",index);
        return true;
    }
    internal static bool IsIntroductionFinished(NCombatRoom room)=>room.HasMeta(IntroductionFinished);
    internal static void CompleteIntroduction(NCombatRoom room)
    {
        if (!GodotObject.IsInstanceValid(room) || !room.IsInsideTree() || CombatManager.Instance.IsEnding) return;
        room.SetMeta(IntroductionFinished,true);
        TryStartPlayback(room);
    }
    private static void TryStartPlayback(NCombatRoom room)
    {
        if (!IsIntroductionFinished(room) || CombatManager.Instance.IsEnding
            || !Sessions.TryGetValue(room,out var session) || session.Started
            || !GodotObject.IsInstanceValid(session.Player) || session.Player.IsQueuedForDeletion()) return;
        session.Started=true;
        ApplyVolume(session.Player);
        session.Player.Play();
        Callable.From(()=>
        {
            if (!GodotObject.IsInstanceValid(room) || !room.IsInsideTree() || CombatManager.Instance.IsEnding) return;
            RefreshControls(room,session);
            ShowCredit(room);
        }).CallDeferred();
    }
    internal static void ChangeTrack(NCombatRoom room,int direction)
    {
        if (!GodotObject.IsInstanceValid(room) || !room.IsInsideTree() || CombatManager.Instance.IsEnding
            || !Sessions.TryGetValue(room,out var session) || !session.Started
            || session.Player.IsQueuedForDeletion()) return;
        int next=(session.Index+direction%Playlist.Value.Length+Playlist.Value.Length)%Playlist.Value.Length;
        if (!LoadTrack(session,next)) return;
        session.Player.StreamPaused=false;
        session.Player.Play();
        session.Player.StreamPaused=session.Paused;
        RefreshControls(room,session);
    }
    internal static void TogglePause(NCombatRoom room)
    {
        if (CombatManager.Instance.IsEnding || !Sessions.TryGetValue(room,out var session) || !session.Started) return;
        session.Paused=!session.Paused;
        session.Player.StreamPaused=session.Paused;
        RefreshControls(room,session);
    }
    private static void ApplyVolume(AudioStreamPlayer player)
    {
        // Master is already synchronized by the game; only apply its BGM slider.
        player.Bus="Master";
        float duck=BattleDialogueVoices.IsPlaying(NCombatRoom.Instance) ? .8f : 1f;
        float target=TrackVolume*Mathf.Pow(SaveManager.Instance.SettingsSave.VolumeBgm,2f)*duck;
        player.VolumeLinear=player.Playing?Mathf.Lerp(player.VolumeLinear,target,.65f):target;
    }
    internal static void RefreshVolume(NCombatRoom room)
    {
        if (GodotObject.IsInstanceValid(room) && room.GetNodeOrNull<AudioStreamPlayer>(PlayerName) is {} player)
            ApplyVolume(player);
    }
    private static string Text(string key)=>ChapterFour.Text("boss.bgm."+key).GetFormattedText();
    private static void RefreshControls(NCombatRoom room,Session session)
    {
        if (!session.Started || !GodotObject.IsInstanceValid(session.Player) || session.Player.IsQueuedForDeletion()
            || !room.IsInsideTree() || CombatManager.Instance.IsEnding) return;
        if (SatoriConfig.ShowFinalBattleMusicPlayer && !GodotObject.IsInstanceValid(session.Controls)) CreateControls(room,session);
        if (!GodotObject.IsInstanceValid(session.Controls)) return;
        session.Controls!.Visible=SatoriConfig.ShowFinalBattleMusicPlayer;
        var track=Playlist.Value[session.Index];
        session.Title!.Text=$"{session.Index+1}/{Playlist.Value.Length} · {track.Title}";
        session.Title.TooltipText=track.Title;
        session.Artist!.Text=track.Artist;
        session.Artist.Visible=SatoriConfig.ShowFinalBattleBgmCredit;
        session.Pause!.Text=Text(session.Paused?"resume":"pause");
    }
    private static void CreateControls(NCombatRoom room,Session session)
    {
        var parent=room.Ui;
        if (parent==null) return;
        var view=parent.Size;
        if (view.X<=0 || view.Y<=0) view=room.GetViewportRect().Size;
        float scale=Math.Clamp(view.X/1920f,.5f,1.5f);
        var panel=new PanelContainer {Name=ControlsName,Position=new Vector2(64*scale,view.Y*.16f),
            CustomMinimumSize=new Vector2(320*scale,0),ZIndex=30,
            TooltipText=Text("drag"),
            MouseFilter=Control.MouseFilterEnum.Stop,FocusMode=Control.FocusModeEnum.None};
        panel.AddThemeStyleboxOverride("panel",new StyleBoxFlat {
            BgColor=new Color("20172ddd"),BorderColor=new Color("c1a374b0"),BorderWidthLeft=2,
            ContentMarginLeft=12*scale,ContentMarginRight=12*scale,
            ContentMarginTop=8*scale,ContentMarginBottom=8*scale});
        var font=FontManager.GetSubstituteFont(LocManager.Instance.Language,FontType.Bold);
        if (font!=null) panel.AddThemeFontOverride("font",font);
        panel.AddThemeFontSizeOverride("font_size",Math.Max(12,(int)(18*scale)));
        var column=new VBoxContainer {MouseFilter=Control.MouseFilterEnum.Ignore};
        session.Title=new Label {MouseFilter=Control.MouseFilterEnum.Ignore,ClipText=true,
            TextOverrunBehavior=TextServer.OverrunBehavior.TrimEllipsis};
        session.Artist=new Label {MouseFilter=Control.MouseFilterEnum.Ignore,ClipText=true,
            TextOverrunBehavior=TextServer.OverrunBehavior.TrimEllipsis};
        session.Artist.AddThemeFontSizeOverride("font_size",Math.Max(11,(int)(15*scale)));
        session.Title.AddThemeColorOverride("font_color",new Color("fff0d8"));
        session.Artist.AddThemeColorOverride("font_color",new Color("c1a374"));
        column.AddChild(session.Title);column.AddChild(session.Artist);
        var buttons=new HBoxContainer {MouseFilter=Control.MouseFilterEnum.Ignore};
        Button AddButton(string name,string text,Action action)
        {
            var button=new Button {Name=name,Text=text,FocusMode=Control.FocusModeEnum.None,
                CustomMinimumSize=new Vector2(90*scale,34*scale),SizeFlagsHorizontal=Control.SizeFlags.ExpandFill};
            button.Pressed+=action;
            buttons.AddChild(button);return button;
        }
        AddButton("Previous",Text("previous"),()=>ChangeTrack(room,-1));
        session.Pause=AddButton("Pause",Text("pause"),()=>TogglePause(room));
        AddButton("Next",Text("next"),()=>ChangeTrack(room,1));
        column.AddChild(buttons);panel.AddChild(column);parent.AddChild(panel);
        session.Controls=panel;
        bool dragging=false;
        Vector2 dragOffset=Vector2.Zero;
        void ClampPosition(Vector2 position)
        {
            var bounds=parent.Size;
            if (bounds.X<=0 || bounds.Y<=0) bounds=room.GetViewportRect().Size;
            panel.Position=new Vector2(Math.Clamp(position.X,0,Math.Max(0,bounds.X-panel.Size.X)),
                Math.Clamp(position.Y,0,Math.Max(0,bounds.Y-panel.Size.Y)));
        }
        // Labels pass input to the panel. Buttons consume their own clicks, so
        // dragging the title/background never changes tracks or steals focus.
        panel.GuiInput+=input=>
        {
            if (input is InputEventMouseButton {ButtonIndex:MouseButton.Left} button)
            {
                dragging=button.Pressed;
                if (dragging) dragOffset=parent.GetLocalMousePosition()-panel.Position;
                panel.AcceptEvent();
            }
            else if (dragging && input is InputEventMouseMotion motion)
            {
                if ((motion.ButtonMask&MouseButtonMask.Left)==0) {dragging=false;return;}
                ClampPosition(parent.GetLocalMousePosition()-dragOffset);
                panel.AcceptEvent();
            }
        };
        panel.VisibilityChanged+=()=>{if (!panel.Visible) dragging=false;};
        parent.Resized+=()=>{if (GodotObject.IsInstanceValid(panel)) ClampPosition(panel.Position);};
    }
    private static void ShowCredit(NCombatRoom room)
    {
        if (SatoriConfig.ShowFinalBattleMusicPlayer || !SatoriConfig.ShowFinalBattleBgmCredit || !GodotObject.IsInstanceValid(room)
            || !room.IsInsideTree() || !Sessions.TryGetValue(room,out var session) || session.Player.IsQueuedForDeletion()) return;
        var parent=room.Ui;
        if (parent==null || parent.HasNode(CreditName)) return;
        var view=parent.Size;
        if (view.X<=0 || view.Y<=0) view=room.GetViewportRect().Size;
        float scale=Math.Clamp(view.X/1920f,.5f,1.5f);
        var resting=new Vector2(64f*scale,view.Y*.16f);
        var panel=new PanelContainer { Name=CreditName,Size=new Vector2(400f*scale,84f*scale),
            Position=resting-new Vector2(60f*scale,0),Modulate=new Color(1,1,1,0),ZIndex=30,
            MouseFilter=Control.MouseFilterEnum.Ignore,FocusMode=Control.FocusModeEnum.None };
        panel.AddThemeStyleboxOverride("panel",new StyleBoxFlat {
            BgColor=new Color("20172dcc"),BorderColor=new Color("c1a374b0"),BorderWidthLeft=2,
            ContentMarginLeft=18f*scale,ContentMarginRight=18f*scale,
            ContentMarginTop=10f*scale,ContentMarginBottom=10f*scale });
        var track=Playlist.Value[session.Index];
        var label=new Label { Text=$"BGM · {track.Title}\n{track.Artist}",
            MouseFilter=Control.MouseFilterEnum.Ignore,FocusMode=Control.FocusModeEnum.None,
            VerticalAlignment=VerticalAlignment.Center };
        var font=FontManager.GetSubstituteFont(LocManager.Instance.Language,FontType.Bold);
        if (font!=null) label.AddThemeFontOverride("font",font);
        label.AddThemeFontSizeOverride("font_size",Math.Max(14,(int)(23f*scale)));
        label.AddThemeColorOverride("font_color",new Color("fff0d8"));
        panel.AddChild(label); parent.AddChild(panel);
        var tween=panel.CreateTween();
        tween.TweenProperty(panel,"modulate:a",1f,.4);
        tween.Parallel().TweenProperty(panel,"position:x",resting.X,.4)
            .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(panel,"position:x",resting.X+20f*scale,4);
        tween.TweenProperty(panel,"modulate:a",0f,.6);
        tween.Parallel().TweenProperty(panel,"position:x",resting.X+100f*scale,.6);
        tween.TweenCallback(Callable.From(()=>panel.QueueFree()));
    }
    internal static void Stop()
    {
        var room=NCombatRoom.Instance;
        if (room==null) return;
        room.SetMeta(MusicStopped,true);
        if (Sessions.TryGetValue(room,out var session)) session.Started=false;
        Sessions.Remove(room);
        var player=room.GetNodeOrNull<AudioStreamPlayer>(PlayerName);
        if (player!=null) {player.Stop();player.QueueFree();}
        foreach (var name in new[]{ControlsName,CreditName})
            if (room.Ui?.GetNodeOrNull<Control>(name) is {} control) {control.Hide();control.QueueFree();}
    }
}

[HarmonyPatch(typeof(NRunMusicController),nameof(NRunMusicController.PlayCustomMusic))]
internal static class KoishiMusicStartPatch
{
    private static bool Prefix(NRunMusicController __instance,string customMusic)
    {
        if (customMusic!=ChapterFourMusic.Path) return true;
        ChapterFourMusic.Start(__instance);
        return false;
    }
}

[HarmonyPatch]
internal static class KoishiMusicEndPatch
{
    private static IEnumerable<System.Reflection.MethodBase> TargetMethods()=>AccessTools.GetDeclaredMethods(typeof(CombatManager))
        .Where(method=>method.Name is "EndCombatInternal" or "ProcessPendingLoss");
    private static void Prefix(CombatManager __instance,object[] __args)
    {
        // A stale turn-state's cleanup must not close a later combat's player.
        if (__args.Length>0 && !ReferenceEquals(__args[0],AccessTools.Field(typeof(CombatManager),"_turnState")?.GetValue(__instance))) return;
        if (NCombatRoom.Instance is {} room) BattleDialogueVoices.Stop(room);
        ChapterFourMusic.Stop();
    }
}
