using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Achievements;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace SatoriCharacter;
internal static class Assets
{
    internal static CharacterModel Vanilla => ModelDb.Character<Silent>();
    internal const string ArtRoot = "res://SatoriCharacter/assets/batch02/";
    internal const string CardArtRoot = "res://SatoriCharacter/assets/card-portraits/";
    internal const string PowerArtRoot = "res://SatoriCharacter/assets/powers/";
    internal const string EnergyCounterArt = "res://SatoriCharacter/assets/ui/energy-counter.svg";
    internal static string[] UiPaths => new[] {EnergyCounterArt,"res://SatoriCharacter/assets/ui/energy-cost.svg","res://images/packed/sprite_fonts/satori_energy_icon.png",ArtRoot+"selection-icon.png", ArtRoot+"selection-locked.png", ArtRoot+"top-panel-icon.png", ArtRoot+"top-panel-icon-outline.png", ArtRoot+"map-marker.png", ArtRoot+"selection-keyart.png", ArtRoot+"third-eye.png", ArtRoot+"third-eye-big.png", ArtRoot+"third-eye-outline.png", "res://SatoriCharacter/scenes/satori_icon.tscn", "res://SatoriCharacter/scenes/satori_selection_bg.tscn"};
}
[HarmonyPatch(typeof(ModelDb), nameof(ModelDb.AllCharacters), MethodType.Getter)]
internal static class CharacterRegistration
{
    private static void Postfix(ref IEnumerable<CharacterModel> __result)
    {
        if (ModelDb.Contains(typeof(Satori)) && !__result.Any(c => c is Satori))
            __result = __result.Append(ModelDb.Character<Satori>()).ToArray();
    }
}
[HarmonyPatch]
internal static class NativeScenePaths
{
    private static IEnumerable<MethodBase> TargetMethods() => new[] {"CharacterSelectBg","CharacterSelectTransitionPath","EnergyCounterPath","MerchantAnimPath","RestSiteAnimPath","TrailPath","AttackSfx","CastSfx","PowerUpSfx","DeathSfx"}.Select(name => (MethodBase)AccessTools.PropertyGetter(typeof(CharacterModel),name));
    private static bool Prefix(CharacterModel __instance,MethodBase __originalMethod,ref string __result)
    {
        if (!Entry.IsTarget(__instance)) return true;
        __result = __originalMethod.Name switch {
            "get_CharacterSelectBg"=>"res://SatoriCharacter/scenes/satori_selection_bg.tscn",
            _=>(string)__originalMethod.Invoke(Assets.Vanilla,null)! };
        return false;
    }
}
[HarmonyPatch]
internal static class PreloadCharacterAssets
{
    private static IEnumerable<MethodBase> TargetMethods() => new[] {"AssetPaths","AssetPathsCharacterSelect"}.Select(name => (MethodBase)AccessTools.PropertyGetter(typeof(CharacterModel),name));
    private static bool Prefix(CharacterModel __instance,MethodBase __originalMethod,ref IEnumerable<string> __result)
    {
        if (!Entry.IsTarget(__instance)) return true;
        __result = ((IEnumerable<string>)__originalMethod.Invoke(Assets.Vanilla,null)!).Concat(Assets.UiPaths).Append("res://materials/cards/frames/satori_card_frame_mat.tres").Append(Entry.ActorPath).Distinct().ToArray();
        return false;
    }
}
[HarmonyPatch]
internal static class CharacterTextures
{
    private static IEnumerable<MethodBase> TargetMethods() => new[] {"IconTexture","IconOutlineTexture","ArmPointingTexture","ArmRockTexture","ArmPaperTexture","ArmScissorsTexture"}.Select(name => (MethodBase)AccessTools.PropertyGetter(typeof(CharacterModel),name));
    private static bool Prefix(CharacterModel __instance,MethodBase __originalMethod,ref Texture2D __result)
    {
        if (!Entry.IsTarget(__instance)) return true;
        __result = __originalMethod.Name switch {
            "get_IconTexture" => ResourceLoader.Load<Texture2D>(Assets.ArtRoot+"top-panel-icon.png"),
            "get_IconOutlineTexture" => ResourceLoader.Load<Texture2D>(Assets.ArtRoot+"top-panel-icon-outline.png"),
            _ => (Texture2D)__originalMethod.Invoke(Assets.Vanilla,null)! };
        return false;
    }
}
[HarmonyPatch(typeof(CharacterModel), nameof(CharacterModel.CreateVisuals))]
internal static class NativeVisualContainer
{
    private static bool Prefix(CharacterModel __instance,ref NCreatureVisuals __result)
    {
        if (!Entry.IsTarget(__instance)) return true;
        __result = Assets.Vanilla.CreateVisuals();
        return false;
    }
}
[HarmonyPatch(typeof(CharacterModel), nameof(CharacterModel.RunWonAchievement), MethodType.Getter)]
internal static class NativeAchievementFallback
{
    private static bool Prefix(CharacterModel __instance,ref Achievement __result)
    {
        if (!Entry.IsTarget(__instance)) return true;
        __result = Assets.Vanilla.RunWonAchievement;
        return false;
    }
}
