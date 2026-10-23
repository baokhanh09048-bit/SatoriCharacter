using Godot;
using HarmonyLib;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Events;

namespace SatoriCharacter;

// Reuse native event buttons, votes, focus and callbacks; change only the garden
// story's drawing. A bounded description keeps all four choices on the screen.
[HarmonyPatch(typeof(NEventLayout),nameof(NEventLayout.SetEvent))]
internal static class GardenStoryLayoutPatch
{
    private static void Postfix(NEventLayout __instance,EventModel eventModel)
    {
        if (eventModel is not SatoriGardenStory) return;
        if (eventModel is SatoriHomecoming homecoming)
        {
            SatoriEndingComic.Show(__instance,homecoming);
            return;
        }
        if (__instance.GetNodeOrNull<TextureRect>("%Portrait") is {} portrait)
        {
            portrait.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            // Native event root is shifted up by 39; the landscape fills the room.
            portrait.OffsetTop=39; portrait.OffsetBottom=39;
            portrait.Scale=Vector2.One;
            portrait.PivotOffset=Vector2.Zero;
            portrait.ExpandMode=TextureRect.ExpandModeEnum.IgnoreSize;
            portrait.StretchMode=TextureRect.StretchModeEnum.KeepAspectCovered;
            portrait.MouseFilter=Control.MouseFilterEnum.Ignore;
            DrawStoryCharacters(__instance,portrait,(SatoriGardenStory)eventModel);
        }
        var description=__instance.GetNodeOrNull<MegaRichTextLabel>("%EventDescription");
        if (description?.GetParent() is VBoxContainer content)
        {
            content.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopLeft);
            content.AnchorLeft=.5f; content.AnchorRight=.5f;
            content.OffsetLeft=-20; content.OffsetRight=840;
            content.OffsetTop=155; content.OffsetBottom=1035;
            content.AddThemeConstantOverride("separation",8);
            if (content.GetNodeOrNull<Control>("Spacer") is {} spacer)
                spacer.CustomMinimumSize=new Vector2(0,8);
        }
        if (description!=null)
        {
            description.CustomMinimumSize=new Vector2(0,306);
            description.FitContent=false;
            description.ScrollActive=true;
            description.HorizontalAlignment=HorizontalAlignment.Left;
            description.VerticalAlignment=VerticalAlignment.Top;
            description.AddThemeConstantOverride("line_separation",-2);
            description.AddThemeConstantOverride("paragraph_separation",4);
            description.AutoSizeEnabled=true;
            description.MinFontSize=22; description.MaxFontSize=26;
            description.IsVerticallyBound=true; description.IsHorizontallyBound=true;
        }
        if (__instance.GetNodeOrNull<VBoxContainer>("%OptionsContainer") is {} options)
            options.AddThemeConstantOverride("separation",8);
    }

    private static void DrawStoryCharacters(NEventLayout layout,TextureRect background,SatoriGardenStory story)
    {
        if (layout.GetNodeOrNull<Control>("SatoriGardenCast") is {} previous)
        { layout.RemoveChild(previous); previous.QueueFree(); }
        var cast=new Control { Name="SatoriGardenCast",Position=new Vector2(40,230),
            Size=new Vector2(850,640),MouseFilter=Control.MouseFilterEnum.Ignore };
        layout.AddChild(cast); layout.MoveChild(cast,background.GetIndex()+1);
        if (story is SatoriGardenVisitors)
        {
            // Koishi is still beyond the gate; only the two arriving companions.
            AddCharacter(cast,"okuu",590,620,535);
            AddCharacter(cast,"orin",225,620,475);
            return;
        }
        int choice=story.Owner?.GetRelic<SatoriLostHeart>()?.CompanionChoice ?? 4;
        if (story is SatoriHomecoming && choice==3)
        {
            AddCharacter(cast,"okuu",180,620,415);
            AddCharacter(cast,"orin",330,620,365);
            AddCharacter(cast,"satori",505,620,370);
            AddCharacter(cast,"koishi",695,620,390);
        }
        else if (story is SatoriHomecoming && choice is 1 or 2)
        {
            AddCharacter(cast,choice==1?"okuu":"orin",210,620,choice==1?490:440);
            AddCharacter(cast,"satori",405,620,435);
            AddCharacter(cast,"koishi",685,620,450);
        }
        else
        {
            AddCharacter(cast,"satori",255,620,470);
            AddCharacter(cast,"koishi",605,620,490);
        }
    }

    private static void AddCharacter(Control parent,string key,float centre,float feet,float height)
    {
        string path=key=="satori" ? "res://SatoriCharacter/assets/animation-refresh-atlases/satori-story.png" : ChapterFour.Art+key+".png";
        var texture=ResourceLoader.Load<Texture2D>(path);
        if (texture==null) return;
        float width=height*texture.GetWidth()/texture.GetHeight();
        var actor=new TextureRect { Name="Cast_"+key,Texture=texture,
            Position=new Vector2(centre-width/2,feet-height),Size=new Vector2(width,height),
            ExpandMode=TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode=TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter=Control.MouseFilterEnum.Ignore };
        // Koishi's existing battle frame faces left, toward the other characters.
        parent.AddChild(actor);
    }
}
