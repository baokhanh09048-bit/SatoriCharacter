using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Entities.Cards;

namespace SatoriCharacter;

// Validate disposable clones after IDs are assigned, without touching a run/save.
[HarmonyPatch(typeof(ModelDb), nameof(ModelDb.InitIds))]
internal static class ContentValidation
{
    private static void Postfix()
    {
        var character = ModelDb.Character<Satori>();
        var deck = character.StartingDeck.ToArray();
        var strike = ModelDb.Card<SatoriStrike>().ToMutable();
        var defend = ModelDb.Card<SatoriDefend>().ToMutable();
        Require(ModelDb.AllCharacters.Count(c => c is Satori) == 1, "character registration");
        Require(deck.Length == 10 && deck.Count(c => c is SatoriStrike) == 4 && deck.Count(c => c is SatoriDefend) == 4 && deck.Count(c=>c is SatoriReadHeart)==1 && deck.Count(c=>c is SatoriRecallThought)==1, "4 Strike + 4 Defend + ReadHeart + RecallThought");
        Require(character.StartingHp == 70 && character.MaxEnergy == 3, "starting stats");
        Require(strike.EnergyCost.Canonical == 1 && defend.EnergyCost.Canonical == 1, "basic costs");
        Require(strike.DynamicVars.Damage.BaseValue == 6 && defend.DynamicVars.Block.BaseValue == 5, "basic values");
        strike.UpgradeInternal();
        strike.FinalizeUpgradeInternal();
        defend.UpgradeInternal();
        defend.FinalizeUpgradeInternal();
        Require(strike.DynamicVars.Damage.BaseValue == 9 && defend.DynamicVars.Block.BaseValue == 8, "upgrade values");
        Require(character.StartingRelics.Single() is ThirdEye, "starting relic");
        Require(ModelDb.RelicPool<SatoriRelicPool>().AllRelics.Count()==10,"ten own relics");
        Require(ModelDb.Relic<MegaCrit.Sts2.Core.Models.Relics.TouchOfOrobas>().GetUpgradedStarterRelic(ModelDb.Relic<ThirdEye>()) is SatoriOpenThirdEye,"native Orobas starter upgrade");
        Require(ModelDb.Card<SatoriAttackMemory>().DynamicVars["V0"].BaseValue==5 && ModelDb.Card<SatoriBuffMemory>().DynamicVars["V0"].BaseValue==2,"memory base values");
        var recall=ModelDb.Card<SatoriRecallThought>();
        Require(recall.DynamicVars["V0"].BaseValue==4 && recall.DynamicVars["V1"].BaseValue==1,"Recall four block + one memory");
        var recallUpgrade=recall.ToMutable();recallUpgrade.UpgradeInternal();recallUpgrade.FinalizeUpgradeInternal();
        Require(recallUpgrade.DynamicVars["V0"].BaseValue==4 && recallUpgrade.DynamicVars["V1"].BaseValue==2,"Recall upgrade four block + two memories");
        var pool=character.CardPool.AllCards.ToArray();
        Require(character.CardPool is SatoriCardPool && pool.Length==92 && pool.Select(c=>c.Id).Distinct().Count()==92,"own pool of 92 unique cards");
        foreach(var expected in new[]{(CardRarity.Basic,4),(CardRarity.Common,22),(CardRarity.Uncommon,38),(CardRarity.Rare,26),(CardRarity.Ancient,2)})
            Require(pool.Count(c=>c.Rarity==expected.Item1)==expected.Item2,"rarity "+expected.Item1);
        // Card types are authored in the catalog. Balance edits may legitimately
        // turn attacks into skills without changing the 92-card pool.
        var specs=CardCatalog.All.Where(spec=>spec.Route!="记忆").ToArray();
        foreach(var group in specs.GroupBy(spec=>spec.Type))
            Require(pool.Count(card=>card.Type==group.Key)==group.Count(),"catalog type count "+group.Key);
        foreach(var canonical in pool)
            Require(canonical.Type==CardCatalog.ByName[canonical.GetType().Name].Type,"catalog type "+canonical.Id);
        foreach(var canonical in pool)
        {
            var clone=canonical.ToMutable();clone.UpgradeInternal();clone.FinalizeUpgradeInternal();
            Require(clone.IsUpgraded,"upgrade "+canonical.Id);
        }
        foreach(var memory in new CardModel[]{ModelDb.Card<SatoriAttackMemory>(),ModelDb.Card<SatoriDefenseMemory>(),ModelDb.Card<SatoriDebuffMemory>(),ModelDb.Card<SatoriBuffMemory>()})
        {
            Require(memory.EnergyCost.Canonical==0,"memory zero base cost "+memory.Id);
            var clone=memory.ToMutable();clone.UpgradeInternal();clone.FinalizeUpgradeInternal();
            Require(clone.EnergyCost.Canonical==0,"upgraded memory zero base cost "+memory.Id);
            Require(memory.DynamicVars.Values.All(v=>clone.DynamicVars[v.Name].BaseValue==v.BaseValue),"memory upgrade keeps values "+memory.Id);
        }
        Log.Info($"SatoriCharacter CONTENT_VERIFY_OK: {character.Id}; roster once; deck 4+4+ReadHeart+RecallThought; HP70 / energy3; own pool92 (4 Basic / 22 Common / 38 Uncommon / 26 Rare / 2 Ancient); {pool.Count(card=>card.Type==CardType.Attack)} Attack / {pool.Count(card=>card.Type==CardType.Skill)} Skill / {pool.Count(card=>card.Type==CardType.Power)} Power; upgrades92; memory tokens only upgrade Retain; temporary2 Strength; unlimited mirrors/suggestion; combat memory count; ThirdEye first-two-turn random thought; Orobas all-enemies upgrade; ten relics; memory Focus half-rounded-down statuses.");
    }
    private static void Require(bool condition, string detail)
    {
        if (!condition) throw new InvalidOperationException("SatoriCharacter content validation failed: " + detail);
    }
}
