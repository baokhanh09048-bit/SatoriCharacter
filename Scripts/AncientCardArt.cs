using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;

namespace SatoriCharacter;

// The current DLL still names an older atlas layout. Resolve Satori's Ancient
// backgrounds through the paths actually present in the installed game pack.
[HarmonyPatch(typeof(CardModel),nameof(CardModel.AncientBorder),MethodType.Getter)]
internal static class SatoriAncientBorder
{
    private static bool Prefix(CardModel __instance,ref Texture2D __result)
    {
        if(__instance is not SatoriPoolCard || __instance.Rarity!=CardRarity.Ancient) return true;
        __result=ResourceLoader.Load<Texture2D>("res://images/atlases/compressed.sprites/card_template/ancient_card_border.tres");return false;
    }
}
[HarmonyPatch(typeof(CardModel),nameof(CardModel.AncientTextBg),MethodType.Getter)]
internal static class SatoriAncientTextBackground
{
    private static bool Prefix(CardModel __instance,ref Texture2D __result)
    {
        if(__instance is not SatoriPoolCard || __instance.Rarity!=CardRarity.Ancient) return true;
        string type=__instance.Type.ToString().ToLowerInvariant();
        __result=ResourceLoader.Load<Texture2D>($"res://images/atlases/compressed.sprites/card_template/ancient_card_text_bg_{type}.tres");return false;
    }
}
