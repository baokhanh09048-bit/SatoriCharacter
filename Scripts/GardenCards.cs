using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.ValueProps;

namespace SatoriCharacter;

// Combat-only cards stay outside the 92 permanent cards and their reward pool.
public sealed class SatoriHeartKnot : CardModel
{
    public SatoriHeartKnot() : base(1,CardType.Status,CardRarity.Status,TargetType.None) { }
    public override int MaxUpgradeLevel => 0;
    public override CardPoolModel Pool => ModelDb.CardPool<StatusCardPool>();
    public override string PortraitPath => ChapterFour.Art+"card-heart-knot.png";
    public override string BetaPortraitPath => PortraitPath;
    public override IEnumerable<string> AllPortraitPaths => new[] { PortraitPath };
    protected override IEnumerable<IHoverTip> ExtraHoverTips => new[] {
        HoverTipFactory.FromPower(ModelDb.Power<SatoriUnseenPresencePower>()) };
    protected override async Task OnPlay(PlayerChoiceContext context,CardPlay play)
    {
        using var animation=SatoriCardAnimations.Begin(this,"Cast");
        var presence=Owner.Creature.CombatState?.Enemies
            .Where(enemy=>enemy.IsAlive && enemy.Monster is SatoriKoishi)
            .Select(enemy=>enemy.GetPower<SatoriUnseenPresencePower>()).FirstOrDefault(power=>power!=null);
        if (presence!=null) await presence.ResolveKnot(context,play);
    }
}

public abstract class SatoriGardenAidCard : CardModel
{
    protected SatoriGardenAidCard(int cost,CardType type,TargetType target) : base(cost,type,CardRarity.Token,target) { }
    public override int MaxUpgradeLevel => 0;
    public override CardPoolModel Pool => ModelDb.CardPool<SatoriCardPool>();
    public override IEnumerable<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Ethereal,CardKeyword.Exhaust };
    public override string BetaPortraitPath => PortraitPath;
    public override IEnumerable<string> AllPortraitPaths => new[] { PortraitPath };
}

public sealed class SatoriNuclearAid : SatoriGardenAidCard
{
    public SatoriNuclearAid() : base(0,CardType.Skill,TargetType.Self) { }
    public override IEnumerable<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Retain,CardKeyword.Exhaust };
    public override string PortraitPath => ChapterFour.Art+"card-nuclear-aid.png";
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new EnergyVar(2) { ColorPrefix="satori" } };
    protected override async Task OnPlay(PlayerChoiceContext context,CardPlay play)
    {
        using var animation=SatoriCardAnimations.Begin(this,animate:false);
        ChapterFourVisuals.PlayCompanion(Owner,"okuu","Cast");
        if (Owner.Creature.IsAlive) await PlayerCmd.GainEnergy(DynamicVars.Energy.IntValue,Owner);
    }
}

public sealed class SatoriSpiritEscort : SatoriGardenAidCard
{
    public SatoriSpiritEscort() : base(0,CardType.Skill,TargetType.Self) { }
    public override bool GainsBlock => true;
    public override string PortraitPath => ChapterFour.Art+"card-spirit-escort.png";
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] {
        new BlockVar(6m,ValueProp.Move),new CardsVar(1) };
    protected override async Task OnPlay(PlayerChoiceContext context,CardPlay play)
    {
        using var animation=SatoriCardAnimations.Begin(this,animate:false);
        // Orin deliberately keeps her one accepted loop, even while supplying aid.
        await CreatureCmd.GainBlock(Owner.Creature,DynamicVars.Block,play);
        await CardPileCmd.Draw(context,DynamicVars.Cards.BaseValue,Owner);
    }
}
