using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Models.Relics;

namespace SatoriCharacter;

[HarmonyPatch(typeof(ArchaicTooth),"GetTranscendenceStarterCard")]
internal static class SatoriTranscendenceStarter
{
    private static bool Prefix(Player player,ref CardModel? __result)
    {
        if(player.Character is not Satori || player.Deck.Cards.FirstOrDefault(c=>c is SatoriReadHeart) is not {} read) return true;
        __result=read;return false;
    }
}
[HarmonyPatch(typeof(ArchaicTooth),"GetTranscendenceTransformedCard")]
internal static class SatoriTranscendenceResult
{
    private static bool Prefix(CardModel starterCard,ref CardModel __result)
    {
        if(starterCard is not SatoriReadHeart) return true;
        var card=starterCard.Owner.RunState.CreateCard<SatoriAncientHeart>(starterCard.Owner);
        if(starterCard.IsUpgraded) CardCmd.Upgrade(card);
        if(starterCard.Enchantment is {} enchantment)
        {
            var copied=(EnchantmentModel)enchantment.MutableClone();CardCmd.Enchant(copied,card,copied.Amount);
        }
        __result=card;return false;
    }
}
[HarmonyPatch(typeof(ArchaicTooth),nameof(ArchaicTooth.TranscendenceCards),MethodType.Getter)]
internal static class SatoriTranscendenceCatalog
{
    private static void Postfix(ref List<CardModel> __result)
    {
        if(!__result.Any(c=>c is SatoriAncientHeart)) __result=__result.Append(ModelDb.Card<SatoriAncientHeart>()).ToList();
    }
}
[HarmonyPatch(typeof(Tezcatara),"OptionPool1",MethodType.Getter)]
internal static class SatoriAncientKeyChoice
{
    private static readonly System.Reflection.MethodInfo CreateOption=AccessTools.Method(typeof(AncientEventModel),"RelicOption",new[]{typeof(RelicModel),typeof(string),typeof(string)});
    private static void Postfix(Tezcatara __instance,List<EventOption> __result)
    {
        if(__instance.Owner!=null && (__instance.Owner.Character is not Satori || __instance.Owner.Deck.Cards.Any(c=>c is SatoriAncientDream))) return;
        var option=(EventOption)CreateOption.Invoke(__instance,new object?[]{ModelDb.Relic<SatoriPalaceKey>().ToMutable(),"INITIAL",null})!;
        __result.Add(option);
    }
}
