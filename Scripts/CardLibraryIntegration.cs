using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Screens.CardLibrary;

namespace SatoriCharacter;

// The native library scene contains five hard-coded character filters. Its open
// handler looks up the current character in this map, even for mod characters.
// Register a real filter rather than substituting a vanilla character/pool.
internal static class SatoriLibraryFilter
{
    private static readonly System.Reflection.FieldInfo CharacterFilters=AccessTools.Field(typeof(NCardLibrary),"_cardPoolFilters");
    private static readonly System.Reflection.FieldInfo PoolFilters=AccessTools.Field(typeof(NCardLibrary),"_poolFilters");
    private static readonly System.Reflection.FieldInfo LastHovered=AccessTools.Field(typeof(NCardLibrary),"_lastHoveredControl");
    private static readonly System.Reflection.MethodInfo UpdateFilter=AccessTools.Method(typeof(NCardLibrary),"UpdateCardPoolFilter");
    internal static void Ensure(NCardLibrary library)
    {
        var character=ModelDb.Character<Satori>();
        var characters=(Dictionary<CharacterModel,NCardPoolFilter>)CharacterFilters.GetValue(library)!;
        if(characters.ContainsKey(character)) return;
        // Native Ready must have finished before we duplicate its ready-to-use UI.
        if(characters.Count==0) return;
        var template=library.GetNode<NCardPoolFilter>("%SilentPool");
        var filter=(NCardPoolFilter)template.Duplicate((int)(Node.DuplicateFlags.Scripts|Node.DuplicateFlags.Groups));
        filter.Name="SatoriPool";
        filter.UniqueNameInOwner=false;
        filter.Visible=true;
        filter.Loc=new LocString("characters","SATORI.title");
        // Duplicate() does not preserve the packed-scene owner used by the
        // native filter's %SelectionReticle lookup. Give this copy its own scope.
        var reticle=filter.FindChild("SelectionReticle",true,false)!;
        reticle.UniqueNameInOwner=false;
        reticle.Owner=filter;
        reticle.UniqueNameInOwner=true;
        var image=filter.GetNode<TextureRect>("Image");
        image.Texture=ResourceLoader.Load<Texture2D>(Assets.ArtRoot+"top-panel-icon.png");
        if(image.Material!=null) image.Material=(Material)image.Material.Duplicate();
        if(image.GetNodeOrNull<TextureRect>("Shadow") is {} shadow) shadow.Texture=image.Texture;
        var container=template.GetParent();
        container.AddChild(filter);
        container.MoveChild(filter,template.GetIndex()+1);
        var pools=(Dictionary<NCardPoolFilter,Func<CardModel,bool>>)PoolFilters.GetValue(library)!;
        pools.Add(filter,card=>card.Pool is SatoriCardPool && card.Rarity!=MegaCrit.Sts2.Core.Entities.Cards.CardRarity.Token);
        characters.Add(character,filter);
        filter.Connect(NCardPoolFilter.SignalName.Toggled,Callable.From<NCardPoolFilter>(selected=>UpdateFilter.Invoke(library,new object[]{selected})));
        filter.Connect(Control.SignalName.FocusEntered,Callable.From(()=>LastHovered.SetValue(library,filter)));
    }
}

[HarmonyPatch(typeof(NCardLibrary),nameof(NCardLibrary._Ready))]
internal static class CardLibraryReady
{
    private static void Postfix(NCardLibrary __instance)=>SatoriLibraryFilter.Ensure(__instance);
}
[HarmonyPatch(typeof(NCardLibrary),nameof(NCardLibrary.OnSubmenuOpened))]
internal static class CardLibraryOpen
{
    private static void Prefix(NCardLibrary __instance)=>SatoriLibraryFilter.Ensure(__instance);
}
