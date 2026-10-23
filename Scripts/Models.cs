using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Characters;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.PotionPools;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using MegaCrit.Sts2.Core.ValueProps;

namespace SatoriCharacter;
public sealed class Satori : CharacterModel
{
    protected override CharacterModel? UnlocksAfterRunAs => null;
    public override Color NameColor => new("D18FAB");
    public override CharacterGender Gender => CharacterGender.Feminine;
    public override int StartingHp => 70;
    public override int StartingGold => 99;
    public override CardPoolModel CardPool => ModelDb.CardPool<SatoriCardPool>();
    public override RelicPoolModel RelicPool => ModelDb.RelicPool<SatoriRelicPool>();
    public override PotionPoolModel PotionPool => ModelDb.PotionPool<SharedPotionPool>();
    public override IEnumerable<CardModel> StartingDeck => Enumerable.Repeat<CardModel>(ModelDb.Card<SatoriStrike>(),4)
        .Concat(Enumerable.Repeat<CardModel>(ModelDb.Card<SatoriDefend>(),4))
        .Concat(new CardModel[] {ModelDb.Card<SatoriReadHeart>(),ModelDb.Card<SatoriRecallThought>()});
    public override IReadOnlyList<RelicModel> StartingRelics => new[] {ModelDb.Relic<ThirdEye>()};
    public override float AttackAnimDelay => .12f;
    public override float CastAnimDelay => .10f;
    public override Color EnergyLabelOutlineColor => new("593948");
    public override Color DialogueColor => new("593948");
    public override VfxColor SpeechBubbleColor => VfxColor.Cyan;
    public override Color MapDrawingColor => new("BE719C");
    public override Color RemoteTargetingLineColor => new("E5A1BC");
    public override Color RemoteTargetingLineOutline => new("593948");
    public override string CharacterSelectSfx => ModelDb.Character<Silent>().CharacterSelectSfx;
    public override string CharacterTransitionSfx => ModelDb.Character<Silent>().CharacterTransitionSfx;
    protected override string CharacterSelectIconPath => Assets.ArtRoot+"selection-icon.png";
    protected override string CharacterSelectLockedIconPath => Assets.ArtRoot+"selection-locked.png";
    protected override string MapMarkerPath => Assets.ArtRoot+"map-marker.png";
    protected override string IconPath => "res://SatoriCharacter/scenes/satori_icon.tscn";
    public override List<string> GetArchitectAttackVfx() => ModelDb.Character<Silent>().GetArchitectAttackVfx();
}
public sealed class SatoriCardPool : CardPoolModel
{
    public override string Title => "satori";
    public override string EnergyColorName => "satori";
    public override string CardFrameMaterialPath => "satori_card_frame";
    public override Color DeckEntryCardColor => new("D18FAB");
    public override Color EnergyOutlineColor => new("593948");
    public override bool IsColorless => false;
    protected override CardModel[] GenerateAllCards() => CardCatalog.All.Where(c=>c.Route!="记忆")
        .Select(c=>ModelDb.GetById<CardModel>(ModelDb.GetId(typeof(Entry).Assembly.GetType("SatoriCharacter."+c.Key)!))).ToArray();
}
public abstract class SatoriBasicCard : CardModel
{
    protected SatoriBasicCard(CardType type,TargetType target) : base(1,type,CardRarity.Basic,target) {}
    public override CardPoolModel Pool => ModelDb.CardPool<SatoriCardPool>();
    public override CardPoolModel VisualCardPool => Pool;
    public override string BetaPortraitPath => PortraitPath;
    public override IEnumerable<string> AllPortraitPaths => new[] {PortraitPath};
}
public sealed class SatoriStrike : SatoriBasicCard
{
    public SatoriStrike() : base(CardType.Attack,TargetType.AnyEnemy) {}
    public override string PortraitPath => Assets.CardArtRoot+"SatoriStrike.png";
    protected override HashSet<CardTag> CanonicalTags => new() {CardTag.Strike};
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] {new DamageVar(6m,ValueProp.Move)};
    protected override async Task OnPlay(PlayerChoiceContext choiceContext,CardPlay cardPlay)
    {
        using var animation = SatoriCardAnimations.Begin(this);
        if (cardPlay.Target != null)
            await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCardCompat(this,cardPlay).Targeting(cardPlay.Target).Execute(choiceContext);
    }
    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(3m);
}
public sealed class SatoriDefend : SatoriBasicCard
{
    public SatoriDefend() : base(CardType.Skill,TargetType.Self) {}
    public override string PortraitPath => Assets.CardArtRoot+"SatoriDefend.png";
    public override bool GainsBlock => true;
    protected override HashSet<CardTag> CanonicalTags => new() {CardTag.Defend};
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] {new BlockVar(5m,ValueProp.Move)};
    protected override async Task OnPlay(PlayerChoiceContext choiceContext,CardPlay cardPlay)
    {
        using var animation = SatoriCardAnimations.Begin(this);
        await CreatureCmd.GainBlock(Owner.Creature,DynamicVars.Block,cardPlay);
    }
    protected override void OnUpgrade() => DynamicVars.Block.UpgradeValueBy(3m);
}
public sealed class SatoriRelicPool : RelicPoolModel
{
    public override string EnergyColorName => "satori";
    public override Color LabOutlineColor => new("D18FAB");
    protected override IEnumerable<RelicModel> GenerateAllRelics() => new RelicModel[] {
        ModelDb.Relic<ThirdEye>(),ModelDb.Relic<SatoriOpenThirdEye>(),ModelDb.Relic<SatoriBookmark>(),
        ModelDb.Relic<SatoriRedSpool>(),ModelDb.Relic<SatoriHellEmber>(),ModelDb.Relic<SatoriTeacup>(),
        ModelDb.Relic<SatoriKoishiNote>(),ModelDb.Relic<SatoriHeartBell>(),ModelDb.Relic<SatoriUnconsciousHat>(),ModelDb.Relic<SatoriPalaceKey>()};
}
