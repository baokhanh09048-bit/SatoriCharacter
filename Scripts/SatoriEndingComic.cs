using Godot;
using HarmonyLib;
using System.Text.Json;
using MegaCrit.Sts2.Core.Nodes.Events;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens.GameOverScreen;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.addons.mega_text;

namespace SatoriCharacter;

// Newly illustrated panels, displayed in order. The native victory event remains
// the saved room and the final click goes through its ordinary option action.
internal static class SatoriEndingComic
{
    internal const string RootName="SatoriEndingComic";
    internal static void Show(NEventLayout layout,SatoriHomecoming story)
    {
        if (layout.HasNode(RootName)) return;
        var texture=ResourceLoader.Load<Texture2D>(ChapterFour.Art+"homecoming-comic.png");
        if (texture==null) return; // Keep the native finish option usable if art is missing.
        layout.GetNodeOrNull<Control>("%Portrait")?.Hide();
        layout.GetNodeOrNull<Control>("%Title")?.Hide();
        layout.GetNodeOrNull<Control>("%SharedEventLabel")?.Hide();
        if (layout.GetNodeOrNull<Control>("%EventDescription")?.GetParent() is Control content) content.Hide();
        layout.GetNodeOrNull<Control>("%OptionsContainer")?.Hide();

        // Room-owned, but viewport-aligned: the native event layout's offsets and
        // room HUD must not introduce margins around this ending page.
        var root=new CanvasLayer { Name=RootName,Layer=80 };
        layout.AddChild(root);
        var stage=new Control { Name="Stage",ClipContents=true,MouseFilter=Control.MouseFilterEnum.Ignore };
        root.AddChild(stage);
        var page=new Control { Name="Page",MouseFilter=Control.MouseFilterEnum.Ignore };
        stage.AddChild(page);
        var pageShade=new ColorRect { Color=new Color("08060d"),MouseFilter=Control.MouseFilterEnum.Ignore };
        page.AddChild(pageShade); pageShade.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var illustration=new TextureRect { Name="Illustration",Texture=texture,
            ExpandMode=TextureRect.ExpandModeEnum.IgnoreSize,StretchMode=TextureRect.StretchModeEnum.Scale,
            MouseFilter=Control.MouseFilterEnum.Ignore };
        page.AddChild(illustration); illustration.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        // Masks follow the actual illustrated gutters. They reveal the freshly
        // generated page without cropping a diagonal panel into a rectangle.
        var geometryPath=ChapterFour.Art+"homecoming-comic-layout.json";
        var geometry=JsonSerializer.Deserialize<float[][][]>(Godot.FileAccess.GetFileAsString(geometryPath))!;
        var regions=geometry.Select(region=>region.Select(point=>new Vector2(point[0],point[1])).ToArray()).ToArray();
        var masks=new Polygon2D[regions.Length];
        for (int i=0;i<masks.Length;i++)
        {
            masks[i]=new Polygon2D {Name="PanelMask"+i,Color=new Color("08060d"),Visible=i>0};
            page.AddChild(masks[i]);
        }
        void Arrange()
        {
            var view=stage.GetViewportRect().Size;
            stage.Position=Vector2.Zero;
            stage.Size=view;
            var imageSize=texture.GetSize();
            float scale=Math.Max(view.X/imageSize.X,view.Y/imageSize.Y);
            page.Size=imageSize*scale;
            page.Position=(view-page.Size)/2f;
            for (int i=0;i<masks.Length;i++) masks[i].Polygon=regions[i].Select(point=>point*page.Size).ToArray();
        }
        var viewport=stage.GetViewport();
        viewport.SizeChanged+=Arrange;
        root.TreeExiting+=()=>viewport.SizeChanged-=Arrange;
        Arrange();
        int revealed=1;
        var next=new Button { Name="NextPanel",Flat=true,FocusMode=Control.FocusModeEnum.All };
        foreach (var state in new[] {"normal","hover","pressed","focus"})
            next.AddThemeStyleboxOverride(state,new StyleBoxEmpty());
        stage.AddChild(next); next.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        next.Pressed+=()=>
        {
            if (revealed<masks.Length)
            {
                var mask=masks[revealed++];
                var tween=mask.CreateTween(); tween.TweenProperty(mask,"modulate:a",0f,.25);
                tween.TweenCallback(Callable.From(mask.Hide));
            }
            else if (story.CurrentOptions.Count>0 && NEventRoom.Instance is {} room)
            {
                next.Disabled=true;
                next.ReleaseFocus();
                // Close before the native choice starts the saved victory flow.
                // Otherwise this high canvas layer also covers the game-over UI.
                root.Visible=false;
                room.OptionButtonClicked(story.CurrentOptions[0],0);
            }
        };
        Callable.From(next.GrabFocus).CallDeferred();
    }
}

[HarmonyPatch(typeof(NEventLayout),nameof(NEventLayout.DefaultFocusedControl),MethodType.Getter)]
internal static class EndingComicFocusPatch
{
    private static void Postfix(NEventLayout __instance,ref Control? __result)
    {
        if (__instance.GetNodeOrNull<Button>(SatoriEndingComic.RootName+"/Stage/NextPanel") is {} button
            && !button.Disabled && button.IsVisibleInTree())
            __result=button;
    }
}

internal static class SatoriHomecomingResult
{
    internal static bool IsHomecoming(NGameOverScreen screen)
        => AccessTools.Field(typeof(NGameOverScreen),"_runState").GetValue(screen)
            is RunState {CurrentRoom:EventRoom {CanonicalEvent:SatoriHomecoming}};

    internal static bool IsHiddenContinue(NClickableControl control)
    {
        if (control is not NGameOverContinueButton || control.Name!="ContinueButton") return false;
        for (Node? parent=control.GetParent();parent!=null;parent=parent.GetParent())
            if (parent is NGameOverScreen screen) return IsHomecoming(screen);
        return false;
    }

    internal static void ApplyButtons(NGameOverScreen screen)
    {
        if (screen.GetNodeOrNull<NButton>("%ContinueButton") is {} next)
        {
            next.Disable();
            next.Hide();
            next.MouseFilter=Control.MouseFilterEnum.Ignore;
            next.FocusMode=Control.FocusModeEnum.None;
        }
        if (screen.GetNodeOrNull<NButton>("%MainMenuButton") is {} menu)
        {
            menu.Show();
            menu.Enable();
        }
    }

    internal static void ApplyText(NGameOverScreen screen)
    {
        screen.GetNodeOrNull<NCommonBanner>("%Banner")?.label.SetTextAutoSize(
            ChapterFour.Text("ending.result.title").GetFormattedText());
        if (screen.GetNodeOrNull<MegaRichTextLabel>("%VictoryDamageLabel") is not {} label) return;
        var body=ChapterFour.Text("ending.result.body");
        body.Add("Score",(int)AccessTools.Field(typeof(NGameOverScreen),"_score").GetValue(screen)!);
        string text=body.GetFormattedText();
        var run=(RunState)AccessTools.Field(typeof(NGameOverScreen),"_runState").GetValue(screen)!;
        var player=(Player)AccessTools.Field(typeof(NGameOverScreen),"_localPlayer").GetValue(screen)!;
        // Retain the native ascension-unlock notice on the dedicated ending.
        if (run.AscensionLevel is >0 and <10 && run.AscensionLevel>=player.MaxAscensionWhenRunStarted
            && run.GameMode==GameMode.Standard)
        {
            var notice=new LocString("game_over_screen","VICTORY_UNLOCKED_ASCENSION");
            notice.Add("AscensionLevel",run.AscensionLevel+1);
            text+="\n\n"+notice.GetFormattedText();
        }
        label.Text=text;
        // The native Architect paragraph crosses the character's body. Put this
        // shorter homecoming caption below her and above the single menu button.
        label.AnchorLeft=.12f; label.AnchorRight=.88f;
        label.AnchorTop=.69f; label.AnchorBottom=.87f;
        label.OffsetLeft=label.OffsetRight=label.OffsetTop=label.OffsetBottom=0;
    }
}

// Keep native victory/history/unlock processing and the ordinary menu action.
[HarmonyPatch(typeof(NGameOverScreen),nameof(NGameOverScreen._Ready))]
internal static class EndingComicMainMenuPatch
{
    private static void Postfix(NGameOverScreen __instance)
    {
        if (!SatoriHomecomingResult.IsHomecoming(__instance)) return;
        SatoriHomecomingResult.ApplyButtons(__instance);
    }
}

[HarmonyPatch(typeof(NGameOverScreen),"InitializeBannerAndQuote")]
internal static class EndingComicResultTextPatch
{
    private static void Postfix(NGameOverScreen __instance)
    {
        if (SatoriHomecomingResult.IsHomecoming(__instance)) SatoriHomecomingResult.ApplyText(__instance);
    }
}

// AnimateIn enables Continue after its awaited fades; hiding only in _Ready is
// insufficient. Suppress that one button's enable, including its hotkey binding.
[HarmonyPatch(typeof(NClickableControl),nameof(NClickableControl.Enable))]
internal static class EndingComicContinueEnablePatch
{
    private static bool Prefix(NClickableControl __instance)=>!SatoriHomecomingResult.IsHiddenContinue(__instance);
}

[HarmonyPatch(typeof(NGameOverScreen),nameof(NGameOverScreen.DefaultFocusedControl),MethodType.Getter)]
internal static class EndingComicMainMenuFocusPatch
{
    private static void Postfix(NGameOverScreen __instance,ref Control? __result)
    {
        if (SatoriHomecomingResult.IsHomecoming(__instance)
            && __instance.GetNodeOrNull<NButton>("%MainMenuButton") is {} button
            && button.IsEnabled && button.IsVisibleInTree()) __result=button;
    }
}
