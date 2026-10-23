using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.Fonts;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using MegaCrit.Sts2.Core.Nodes.Vfx.Utilities;
using MegaCrit.Sts2.Core.Helpers;

namespace SatoriCharacter;

internal static class ChapterFourVisuals
{
    internal const string ActorPath="res://SatoriCharacter/scenes/garden_actor.tscn";
    internal static void PlayCreature(Creature creature,string action)
    {
        var node=NCombatRoom.Instance?.GetCreatureNode(creature)?.Visuals?.GetNodeOrNull<Node2D>("%Visuals");
        if (node?.HasMethod("garden_trigger")==true) node.Call("garden_trigger",action);
    }
    internal static void PlayCompanion(Player player,string who,string action)
    {
        var node=NCombatRoom.Instance?.GetCreatureNode(player.Creature)?.GetNodeOrNull<Node2D>("SatoriGarden_"+who);
        if (node?.HasMethod("garden_trigger")==true) node.Call("garden_trigger",action);
    }
    internal static void SetCharge(Player player,int charge)
    {
        var node=NCombatRoom.Instance?.GetCreatureNode(player.Creature)?.GetNodeOrNull<Node2D>("SatoriGarden_okuu");
        if (node?.HasMethod("garden_charge")==true) node.Call("garden_charge",charge);
    }
    internal static void AttachCompanions(NCreature node)
    {
        if(node.Entity.Player is not {} owner || !MegaCrit.Sts2.Core.Context.LocalContext.IsMe(owner)) return;
        var choice=node.Entity.Player?.GetRelic<SatoriLostHeart>()?.CompanionChoice ?? 0;
        // Idle Satori's visible feet lie about 6 units above the native origin.
        // Keep all three on that ground line; Okuu is the taller rear silhouette.
        if (choice is 1 or 3) Attach(node,"okuu",new Vector2(-280,-6),360);
        if (choice is 2 or 3) Attach(node,"orin",new Vector2(-135,-6),305);
    }
    private static void Attach(NCreature node,string who,Vector2 position,float height)
    {
        if (node.HasNode("SatoriGarden_"+who)) return;
        var actor=ResourceLoader.Load<PackedScene>(ActorPath).Instantiate<Node2D>();
        actor.Name="SatoriGarden_"+who; actor.Set("character_key",who); actor.Set("display_height",height);
        actor.Position=position; node.AddChild(actor);
        // The taller companion is behind Orin; both remain behind the player.
        node.MoveChild(actor,who=="okuu" ? 0 : node.HasNode("SatoriGarden_okuu") ? 1 : 0);
    }
    private static Tween? _introTween;
    private static NSpeechBubbleVfx? _battleBubble;
    internal static void ShowBattleIntroduction(Creature koishi)
    {
        var room=NCombatRoom.Instance;
        var player=ChapterFour.Player(koishi.CombatState?.RunState);
        if (room==null || player==null) return;
        var companionPlayer=MegaCrit.Sts2.Core.Context.LocalContext.GetMe(koishi.CombatState!.RunState) ?? player;
        int choice=companionPlayer.GetRelic<SatoriLostHeart>()?.CompanionChoice ?? 4;
        if (GodotObject.IsInstanceValid(_introTween)) _introTween!.Kill();
        // Owned by the room: leaving combat disposes the sequence. Nothing waits on it.
        _introTween=room.CreateTween();
        _introTween.TweenCallback(Callable.From(()=>ShowBubble(koishi,"boss.bubble.intro.koishi")));
        _introTween.TweenInterval(BattleDialogueVoices.DisplaySeconds("boss.bubble.intro.koishi")+.2);
        _introTween.TweenCallback(Callable.From(()=>ShowBubble(player.Creature,"boss.bubble.intro.satori")));
        _introTween.TweenInterval(BattleDialogueVoices.DisplaySeconds("boss.bubble.intro.satori")+.2);
        _introTween.TweenCallback(Callable.From(()=>
        {
            if (choice is 1 or 2) ShowCompanionBubble(companionPlayer,choice==1?"okuu":"orin","boss.bubble.intro."+choice);
            else ShowBubble(koishi,"boss.bubble.intro."+choice);
        }));
        _introTween.TweenInterval(choice is 1 or 2 ? 3.2 : BattleDialogueVoices.DisplaySeconds("boss.bubble.intro."+choice)+.2);
        _introTween.TweenCallback(Callable.From(()=>ChapterFourMusic.CompleteIntroduction(room)));
    }
    internal static void ShowLine(Creature speaker,string key)
    {
        // A phase change takes priority over any remaining introductory lines.
        if (GodotObject.IsInstanceValid(_introTween)) _introTween!.Kill();
        _introTween=null;
        ShowBubble(speaker,key);
        // Playing quickly can interrupt the opening with a phase/attack warning.
        // Release the music after that replacement line instead of leaving it silent.
        if (NCombatRoom.Instance is {} room && !ChapterFourMusic.IsIntroductionFinished(room))
        {
            _introTween=room.CreateTween();
            _introTween.TweenInterval(BattleDialogueVoices.DisplaySeconds(key)+.2);
            _introTween.TweenCallback(Callable.From(()=>ChapterFourMusic.CompleteIntroduction(room)));
        }
    }
    private static void ClearBattleBubble()
    {
        if (GodotObject.IsInstanceValid(_battleBubble)) _battleBubble!.QueueFree();
        _battleBubble=null;
    }
    private static void ShowBubble(Creature speaker,string key)
    {
        if (NCombatRoom.Instance?.GetCreatureNode(speaker)==null || !speaker.IsAlive
            || speaker.CombatState?.Encounter is not SatoriKoishiEncounter) return;
        ClearBattleBubble();
        _battleBubble=NSpeechBubbleVfx.Create(ChapterFour.Text(key).GetFormattedText(),speaker,
            BattleDialogueVoices.DisplaySeconds(key),speaker.Monster is SatoriKoishi ? VfxColor.Green : VfxColor.Cyan);
        if (_battleBubble!=null) speaker.GetVfxContainer()?.AddChild(_battleBubble);
        BattleDialogueVoices.Play(NCombatRoom.Instance!,key);
    }
    private static void ShowCompanionBubble(Player player,string who,string key)
    {
        var room=NCombatRoom.Instance;
        var actor=room?.GetCreatureNode(player.Creature)?.GetNodeOrNull<Node2D>("SatoriGarden_"+who);
        var container=player.Creature.GetVfxContainer();
        if (actor==null || container==null || !player.Creature.IsAlive) return;
        ClearBattleBubble();
        BattleDialogueVoices.Stop(room!);
        var position=actor.ToGlobal(new Vector2(45,who=="okuu"?-290:-245));
        _battleBubble=NSpeechBubbleVfx.Create(ChapterFour.Text(key).GetFormattedText(),DialogueSide.Left,
            position,3,who=="okuu"?VfxColor.Orange:VfxColor.Red);
        if (_battleBubble!=null) container.AddChild(_battleBubble);
    }
    internal static void ShowMemory()
    {
        if (NRestSiteRoom.Instance is {} room) ShowNotice(room,ChapterFour.Text("rest.memoryStory").GetFormattedText(),9);
    }
    private static void ShowNotice(Control parent,string text,double duration)
    {
        if (parent.GetNodeOrNull<Control>("SatoriGardenNotice") is {} old) { parent.RemoveChild(old); old.QueueFree(); }
        var panel=new PanelContainer { Name="SatoriGardenNotice", MouseFilter=Control.MouseFilterEnum.Ignore,
            Position=new Vector2(480,140),Size=new Vector2(960,170) };
        var style=new StyleBoxFlat { BgColor=new Color("20172de8"),BorderColor=new Color("c1a374"),
            BorderWidthTop=2,BorderWidthBottom=2,ContentMarginLeft=22,ContentMarginRight=22,ContentMarginTop=16,ContentMarginBottom=16 };
        panel.AddThemeStyleboxOverride("panel",style);
        var label=new Label { Text=text,AutowrapMode=TextServer.AutowrapMode.WordSmart,HorizontalAlignment=HorizontalAlignment.Center,
            MouseFilter=Control.MouseFilterEnum.Ignore };
        var font=FontManager.GetSubstituteFont(LocManager.Instance.Language,FontType.Bold);
        if (font!=null) label.AddThemeFontOverride("font",font);
        label.AddThemeFontSizeOverride("font_size",24); panel.AddChild(label); parent.AddChild(panel);
        var tween=panel.CreateTween(); tween.TweenInterval(duration); tween.TweenProperty(panel,"modulate:a",0f,.5);
        tween.TweenCallback(Callable.From(()=>panel.QueueFree()));
    }
    internal static void GardenBackdrop(NCombatRoom room)
    {
        if (ChapterFour.Run?.Act is not SatoriGarden) return;
        // BgContainer is a zero-sized marker at the scene's centre (+23 x),
        // not a full-screen Control. FullRect under it draws only a lower corner.
        var container=room.SceneContainer;
        if (container.HasNode("SatoriGardenBackdrop")) return;
        var backdrop=ResourceLoader.Load<PackedScene>("res://SatoriCharacter/scenes/garden_backdrop.tscn").Instantiate<Control>();
        backdrop.Name="SatoriGardenBackdrop"; container.AddChild(backdrop);
        backdrop.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        // Cover the native background, while remaining below creatures and VFX.
        container.MoveChild(backdrop,room.GetNode<Control>("%BgContainer").GetIndex()+1);
    }
    private static void AddBackdrop(Control container,string name,string file,int index)
    {
        if (container.HasNode(name)) return;
        var backdrop=new TextureRect { Name=name, Texture=ResourceLoader.Load<Texture2D>(ChapterFour.Art+file),
            ExpandMode=TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode=TextureRect.StretchModeEnum.KeepAspectCovered, MouseFilter=Control.MouseFilterEnum.Ignore };
        container.AddChild(backdrop);
        backdrop.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        container.MoveChild(backdrop,index);
    }
    internal static void GardenRestBackdrop(NRestSiteRoom room)
    {
        if (ChapterFour.Run?.Act is not SatoriGarden) return;
        // The native background owns RestSiteLighting, seating and fire logic.
        // Keep that hierarchy intact and replace only the environment behind it.
        AddBackdrop(room,"SatoriGardenRestBackdrop","garden-rest.png",0);
        foreach (Node root in room.GetNode<Control>("BgContainer").GetChildren())
        {
            if (root is not Control || root.GetNodeOrNull<TextureRect>("RestSiteBG") is not {} nativeBackground) continue;
            nativeBackground.Hide();
            foreach (var path in new[] { "RestSiteForeground", "RestSiteForeground2", "overlay_vfx",
                "RestSiteLighting/WallLight1", "RestSiteLighting/WallLight2",
                "RestSiteLighting/WallLight3", "RestSiteLighting/WallLight4" })
                root.GetNodeOrNull<CanvasItem>(path)?.Hide();
        }
    }
}
[HarmonyPatch(typeof(MonsterModel),nameof(MonsterModel.CreateVisuals))]
internal static class KoishiVisualPatch
{
    private static void Postfix(MonsterModel __instance,NCreatureVisuals __result)
    {
        if (__instance is SatoriKoishi) BattleVisualPatch.ReplaceBody(__result,ChapterFourVisuals.ActorPath,false);
    }
}
[HarmonyPatch(typeof(NCreature),nameof(NCreature._Ready))]
internal static class GardenPartyVisualPatch
{
    private static void Postfix(NCreature __instance)
    {
        if (__instance.Entity.Monster is SatoriKoishi)
        {
            // A restored combat may already have broken Intangible.
            var actor=__instance.Visuals?.GetNodeOrNull<Node2D>("%Visuals");
            if (actor?.HasMethod("garden_set_unseen")==true)
                actor.Call("garden_set_unseen",__instance.Entity.GetPower<SatoriUnseenPresencePower>()!=null);
        }
        if (__instance.Entity.Player!=null && __instance.Entity.CombatState?.Encounter is SatoriKoishiEncounter)
            ChapterFourVisuals.AttachCompanions(__instance);
    }
}
[HarmonyPatch(typeof(NCreature),nameof(NCreature.SetAnimationTrigger))]
internal static class KoishiAnimationPatch
{
    private static void Postfix(NCreature __instance,string trigger)
    {
        if (__instance.Entity.Monster is SatoriKoishi) ChapterFourVisuals.PlayCreature(__instance.Entity,trigger);
    }
}
[HarmonyPatch(typeof(NCreature),nameof(NCreature.StartDeathAnim))]
internal static class KoishiConcedePatch
{
    private static void Prefix(NCreature __instance,ref bool shouldRemove)
    {
        // Keep her drawing on the steps until the story room replaces combat.
        // Native combat still removes the defeated entity and records victory.
        if (__instance.Entity.Monster is SatoriKoishi) shouldRemove=false;
    }
    private static void Postfix(NCreature __instance)
    {
        if (__instance.Entity.Monster is SatoriKoishi) ChapterFourVisuals.PlayCreature(__instance.Entity,"Concede");
    }
}
[HarmonyPatch(typeof(NCombatRoom),nameof(NCombatRoom.SetUpBackground))]
internal static class GardenBackgroundPatch
{
    private static void Postfix(NCombatRoom __instance) => ChapterFourVisuals.GardenBackdrop(__instance);
}
[HarmonyPatch(typeof(NRestSiteRoom),nameof(NRestSiteRoom._Ready))]
internal static class GardenRestBackgroundPatch
{
    private static void Postfix(NRestSiteRoom __instance) => ChapterFourVisuals.GardenRestBackdrop(__instance);
}

