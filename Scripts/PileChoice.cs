using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace SatoriCharacter;

public abstract class SatoriKeepPileOption : CardModel
{
    protected SatoriKeepPileOption():base(0,CardType.Skill,CardRarity.Token,TargetType.Self,false) {}
    internal abstract PileType KeptPile {get;}
    internal abstract string ArtKey {get;}
    public override bool CanBeGeneratedInCombat=>false;
    public override CardPoolModel Pool=>ModelDb.CardPool<ColorlessCardPool>();
    public override CardPoolModel VisualCardPool=>ModelDb.CardPool<SatoriCardPool>();
    public override string PortraitPath=>Assets.CardArtRoot+ArtKey+".png";
    public override string BetaPortraitPath=>PortraitPath;
    public override IEnumerable<string> AllPortraitPaths=>new[]{PortraitPath};
    protected override IEnumerable<DynamicVar> CanonicalVars=>new[]{new DynamicVar("Count",0m)};
    protected override Task OnPlay(PlayerChoiceContext context,CardPlay play)=>Task.CompletedTask;
}
public sealed class SatoriKeepDraw : SatoriKeepPileOption
{
    internal override PileType KeptPile=>PileType.Draw;
    internal override string ArtKey=>"SatoriMemoryPalace";
}
public sealed class SatoriKeepHand : SatoriKeepPileOption
{
    internal override PileType KeptPile=>PileType.Hand;
    internal override string ArtKey=>"SatoriReflection";
}
public sealed class SatoriKeepDiscard : SatoriKeepPileOption
{
    internal override PileType KeptPile=>PileType.Discard;
    internal override string ArtKey=>"SatoriArrangeThoughts";
}

internal static class SatoriPileChoice
{
    internal static async Task Keep(PlayerChoiceContext context,Player owner,CardModel source)
    {
        var state=owner.Creature.CombatState!;
        var options=new CardModel[]{state.CreateCard<SatoriKeepDraw>(owner),state.CreateCard<SatoriKeepHand>(owner),state.CreateCard<SatoriKeepDiscard>(owner)};
        foreach(var option in options.Cast<SatoriKeepPileOption>())
            option.DynamicVars["Count"].BaseValue=option.KeptPile.GetPile(owner).Cards.Count;
        var chosen=await CardSelectCmd.FromChooseACardScreen(context,options,owner);
        if(chosen is not SatoriKeepPileOption keep) throw new InvalidOperationException("Satori pile selection requires one choice.");
        var removed=new[]{PileType.Draw,PileType.Hand,PileType.Discard}.Where(p=>p!=keep.KeptPile)
            .SelectMany(p=>p.GetPile(owner).Cards).Where(c=>c!=source).ToArray();
        foreach(var card in removed)
            if(card.Pile?.Type is PileType.Draw or PileType.Hand or PileType.Discard) await Sts2Compat.Exhaust(context,card);
    }
}
