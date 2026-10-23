using HarmonyLib;
using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace SatoriCharacter;

public abstract class SatoriPower : PowerModel
{
    protected override IEnumerable<MegaCrit.Sts2.Core.HoverTips.IHoverTip> ExtraHoverTips => MemoryHoverTips.ForPower(this);
    public override PowerStackType StackType => PowerStackType.Counter;
    public override PowerType Type => PowerType.Buff;
    public override bool ShouldScaleInMultiplayer => false;
}
public sealed class SatoriTemporaryStrengthPower : TemporaryStrengthPower
{
    public override AbstractModel OriginModel => ModelDb.Card<SatoriBuffMemory>();
    public override bool ShouldScaleInMultiplayer => false;
    private static readonly AsyncLocal<Creature?> Expiring=new();
    internal static bool IsExpiringFor(Creature creature)=>Expiring.Value==creature;
    public override async Task AfterSideTurnEnd(PlayerChoiceContext context,CombatSide side,IEnumerable<Creature> participants)
    {
        var previous=Expiring.Value;Expiring.Value=Owner;
        try {await base.AfterSideTurnEnd(context,side,participants);} finally {Expiring.Value=previous;}
    }
}
public sealed class SatoriMistPower : TemporaryStrengthPower
{
    public override AbstractModel OriginModel=>ModelDb.Card<SatoriCounterEcho>();
    protected override bool IsPositive=>false;
    public override bool ShouldScaleInMultiplayer=>false;
}

// Native combat power lifecycle scopes counters to a combat and the owning player.
// Round reset happens before any start-of-turn engines or card draws.
public sealed class SatoriMindPower : SatoriPower
{
    protected override bool IsVisibleInternal => false;
    public override bool ShouldPlayVfx => false;
    public int MemoriesPlayed {get;private set;}
    public int CombatMemoriesPlayed {get;private set;}
    internal HashSet<CardModel> GeneratedCards {get;private set;}=new();
    public int AttacksPlayed {get;private set;}
    public int SkillsPlayed {get;private set;}
    public int Mirrors {get;private set;}
    public int Generated {get;private set;}
    public bool PreviousWasMemory {get;private set;}
    private int _round=-1;
    private void Refresh()
    {
        int round=Owner.Player?.PlayerCombatState?.TurnNumber ?? Owner.CombatState!.RoundNumber;
        if(_round==round) return;
        _round=round;MemoriesPlayed=AttacksPlayed=SkillsPlayed=Mirrors=Generated=0;
        PreviousWasMemory=false;
    }
    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();_round=-1;
        CombatMemoriesPlayed=MemoriesPlayed=AttacksPlayed=SkillsPlayed=Mirrors=Generated=0;
        PreviousWasMemory=false;GeneratedCards=new();
    }
    public bool TryMirror() {Refresh();Mirrors++;return true;}
    public bool TryGenerate() {Refresh();Generated++;return true;}
    public override Task AfterCardGeneratedForCombat(CardModel card,Player? creator)
    {
        if(card.Owner?.Creature==Owner) GeneratedCards.Add(card);
        return Task.CompletedTask;
    }
    public override Task BeforeSideTurnStart(PlayerChoiceContext context,CombatSide side,IReadOnlyList<Creature> participants,ICombatState state) {Refresh();return Task.CompletedTask;}
    // Late keeps all ordinary AfterCardPlayed engines looking at the previous card/first card.
    public override Task AfterCardPlayedLate(PlayerChoiceContext context,CardPlay play)
    {
        if(play.Card.Owner.Creature!=Owner) return Task.CompletedTask;
        Refresh();
        PreviousWasMemory=play.Card is SatoriPoolCard {IsMemory:true};
        if(PreviousWasMemory)
        {
            MemoriesPlayed++;CombatMemoriesPlayed++;
            Owner.Player?.PlayerCombatState?.RecalculateCardValues();
        }
        if(play.Card.Type==CardType.Attack) AttacksPlayed++;
        if(play.Card.Type==CardType.Skill) SkillsPlayed++;
        return Task.CompletedTask;
    }
}

public sealed class SatoriSuggestionPower : SatoriPower
{
    public override PowerType Type => PowerType.Debuff;
    public override PowerInstanceType InstanceType => PowerInstanceType.InstancedPerApplier;
    internal async Task SpendLayers(PlayerChoiceContext context,int requested,CardModel? source)
    {
        var recipient=Applier;
        int cost=recipient?.HasPower<SatoriCalmPower>()==true?(requested+1)/2:requested;
        cost=Math.Min(Amount,cost);
        if(cost<=0) return;
        if(cost<Amount) {await PowerCmd.ModifyAmount(context,this,-cost,recipient,source);return;}
        await PowerCmd.Remove(this);
        if(recipient?.Player?.GetRelic<SatoriUnconsciousHat>() is {} hat) await hat.OnSuggestionExhausted(context);
        if(recipient is {IsAlive:true} && recipient.GetPower<SatoriRedStringsPower>() is {} strings)
            await SatoriRules.ApplySuggestion(context,Owner,strings.Amount,recipient,null);
    }
    internal async Task CashOut(PlayerChoiceContext context,bool automatic)
    {
        var recipient=Applier;
        int stacks=Amount;
        await SpendLayers(context,stacks,null);
        if(recipient is not {IsAlive:true,Player:not null}) return;
        if(automatic) await CreatureCmd.GainBlock(recipient,stacks,ValueProp.Unpowered,null);
    }
    public override async Task BeforeDamageReceived(PlayerChoiceContext context,Creature target,decimal amount,ValueProp props,Creature? dealer,CardModel? source)
    {
        // Only spend the layers needed for blockable HP damage to this applier.
        // Existing block, zero damage, HP costs and attacks on other targets
        // leave the suggestion intact. Native damage truncates fractional HP loss.
        if(target!=Applier || dealer!=Owner || !target.IsAlive || props.HasFlag(ValueProp.Unblockable)) return;
        int available=target.HasPower<SatoriCalmPower>()?Amount*2:Amount;
        int covered=(int)Math.Min(available,Math.Max(0m,decimal.Floor(amount-target.Block)));
        if(covered<=0) return;
        await SpendLayers(context,covered,null);
        await CreatureCmd.GainBlock(target,covered,ValueProp.Unpowered,null);
    }
}

public sealed class SatoriDiscordPower : SatoriPower
{
    public override PowerType Type => PowerType.Debuff;
    public override PowerInstanceType InstanceType => PowerInstanceType.InstancedPerApplier;
    internal decimal AttackDamageMultiplier(Creature? target,ValueProp props,Creature? dealer)
    {
        // Below the redirect threshold, each layer protects its own applier.
        // Redirected attacks keep their damage, even when layers remain after
        // spending five. At five or more, the preview represents that redirect.
        if(dealer!=Owner || target!=Applier || Applier?.Player==null || !props.IsPoweredAttack() || Amount<=0 || Amount>=5) return 1m;
        return 1m-Amount*0.1m;
    }
    public override async Task BeforeAttack(AttackCommand command)
    {
        if(command.Attacker!=Owner || !command.DamageProps.IsPoweredAttack() || Amount<5 || !Owner.IsAlive || Owner.Monster==null) return;
        if(!SatoriDiscordTargets.TryRedirect(command,Owner)) return;
        // One five-layer spend covers the whole move, including multiple attack
        // commands and all hits. No damage is added by this power itself.
        await PowerCmd.ModifyAmount(new BlockingPlayerChoiceContext(),this,-5,Applier,null);
    }
}

public sealed class SatoriMentalLoadPower : SatoriPower
{
    public override PowerType Type => PowerType.Debuff;
    // Intent is captured before the enemy acts; each enemy round settles once.
    private sealed class LoadClock {internal ICombatState? State;internal int Round=-1;internal bool Settled;internal bool? AttackIntent;}
    private static readonly ConditionalWeakTable<Creature,LoadClock> Clocks=new();
    private bool _settling;
    private LoadClock Clock()
    {
        var clock=Clocks.GetOrCreateValue(Owner);
        var state=Owner.CombatState!;
        if(clock.State!=state || clock.Round!=state.RoundNumber) {clock.State=state;clock.Round=state.RoundNumber;clock.Settled=false;clock.AttackIntent=null;}
        return clock;
    }
    internal Task SettleNow(PlayerChoiceContext context)=>Settle(context,true);
    private async Task Settle(PlayerChoiceContext context,bool manual=false)
    {
        if(_settling || Amount<=0 || !Owner.IsAlive || Owner.CombatState==null) return;
        var clock=Clock();
        if(!manual && clock.Settled) return;
        _settling=true;if(!manual) clock.Settled=true;
        try
        {
            var enemy=Owner;var applier=Applier;
            bool attack=manual?SatoriRules.HasAttack(Owner):clock.AttackIntent??SatoriRules.HasAttack(Owner);
            decimal damage=decimal.Floor(Amount*(attack?1.5m:1m));
            await Sts2Compat.Damage(context,enemy,damage,ValueProp.Unpowered,applier,null,null);
        }
        finally {_settling=false;}
    }
    public override async Task AfterSideTurnEnd(PlayerChoiceContext context,CombatSide side,IEnumerable<Creature> participants)
    {
        if(side==CombatSide.Enemy && participants.Contains(Owner)) await Settle(context);
    }
    public override async Task BeforeSideTurnStart(PlayerChoiceContext context,CombatSide side,IReadOnlyList<Creature> participants,ICombatState state)
    {
        if(side!=CombatSide.Enemy || !participants.Contains(Owner)) return;
        Clock().AttackIntent=SatoriRules.HasAttack(Owner);
        if(state.Players.Any(player=>player.Creature.IsAlive && player.Creature.HasPower<SatoriNightmarePower>())) await Settle(context);
    }
}

public sealed class SatoriDampenedIntentPower : SatoriPower
{
    public override PowerType Type=>PowerType.Debuff;
    public override async Task AfterSideTurnEnd(PlayerChoiceContext context,CombatSide side,IEnumerable<Creature> participants)
    {
        if(side==CombatSide.Enemy && participants.Contains(Owner)) await PowerCmd.Remove(this);
    }
}

// Shared trigger implementation; each concrete power retains a native icon/amount/ID.
public abstract class SatoriEnginePower : SatoriPower
{
    public override async Task AfterPlayerTurnStart(PlayerChoiceContext context,Player player)
    {
        if(player.Creature!=Owner) return;
        if(this is SatoriMirrorHallPower)
            for(int n=0;n<Amount;n++) await SatoriRules.Mirror(context,player,SatoriRules.Enemies(player).FirstOrDefault(),null);
        if(this is SatoriMemoryPalacePower) await CardPileCmd.Draw(context,Amount,player);
        if(this is SatoriDreadAuraPower) foreach(var enemy in SatoriRules.Enemies(player)) await SatoriRules.ApplySuggestion(context,enemy,Amount,Owner,null);
        if(this is SatoriPerfectMemoryPower) await PowerCmd.Apply<SatoriEchoPower>(context,Owner,Amount,Owner,null);
    }
    public override async Task AfterCardPlayed(PlayerChoiceContext context,CardPlay play)
    {
        if(play.Card.Owner.Creature!=Owner || Owner.Player==null) return;
        var mind=await SatoriRules.Mind(context,Owner.Player);
        bool memory=play.Card is SatoriPoolCard {IsMemory:true};
        if(this is SatoriWhisperPower && play.Card.Type==CardType.Skill && play.Card is not SatoriWhisperDominion)
            foreach(var enemy in SatoriRules.Enemies(Owner.Player)) await SatoriRules.ApplyLoad(context,enemy,Amount,Owner,play.Card);
        if(this is SatoriResonancePower && memory) await CreatureCmd.GainBlock(Owner,Amount,ValueProp.Unpowered,null);
        if(this is SatoriDreamStudyPower && memory) await CardPileCmd.Draw(context,Amount,Owner.Player);
        if(this is SatoriPuppetTheaterPower && play.Card.Type==CardType.Attack)
        {
            IEnumerable<Creature> targets=play.Card.TargetType==TargetType.AllEnemies?SatoriRules.Enemies(Owner.Player):play.Target is {IsAlive:true}?new[]{play.Target}:Array.Empty<Creature>();
            foreach(var enemy in targets) await SatoriRules.ApplySuggestion(context,enemy,Amount,Owner,play.Card);
        }
    }
    public override async Task BeforeSideTurnEnd(PlayerChoiceContext context,CombatSide side,IEnumerable<Creature> participants)
    {
        if(side!=CombatSide.Player || Owner.Player==null) return;
        if(this is SatoriStaticPower) foreach(var enemy in SatoriRules.Enemies(Owner.Player)) await SatoriRules.ApplyLoad(context,enemy,Amount,Owner,null);
    }
    public override decimal ModifyBlockAdditive(Creature target,decimal block,ValueProp props,CardModel? cardSource,CardPlay? cardPlay)
        => this is SatoriEchoPower && target==Owner && cardSource is SatoriPoolCard {IsMemory:true} ? Amount : 0;
    public override decimal ModifyPowerAmountGivenAdditive(PowerModel power,Creature giver,decimal amount,Creature? target,CardModel? source)
    {
        if(giver!=Owner || amount<=0) return 0;
        if(this is SatoriDeepSeaPower && power is SatoriMentalLoadPower) return Amount;
        // TemporaryStrengthPower forwards its already-scaled amount into StrengthPower.
        // Scale the temporary wrapper once; its internal strength must not scale again.
        if(power is StrengthPower && source is SatoriPoolCard {IsMemory:true} memory && memory.Spec.Effects.Any(e=>e.Op=="TempStrength")) return 0;
        if(this is SatoriEchoPower && source is SatoriPoolCard {IsMemory:true}
            && power is WeakPower or VulnerablePower or StrengthPower or SatoriTemporaryStrengthPower) return decimal.Floor(Amount/2m);
        return 0;
    }
}
public sealed class SatoriWhisperPower : SatoriEnginePower
{
    public override async Task AfterSideTurnEnd(PlayerChoiceContext context,CombatSide side,IEnumerable<Creature> participants)
    {
        if(side==CombatSide.Player && participants.Contains(Owner)) await PowerCmd.Remove(this);
    }
}
public sealed class SatoriMirrorHallPower : SatoriEnginePower {}
public sealed class SatoriEchoPower : SatoriEnginePower {}
public sealed class SatoriResonancePower : SatoriEnginePower {}
public sealed class SatoriStaticPower : SatoriEnginePower {}
public sealed class SatoriFeedbackPower : SatoriEnginePower
{
    public override async Task BeforeAttack(AttackCommand command)
    {
        if(Owner.IsAlive && command.Attacker?.Side==CombatSide.Enemy && command.DamageProps.IsPoweredAttack()
            && command.Attacker.GetPowerAmount<SatoriMentalLoadPower>()>0)
            await CreatureCmd.GainBlock(Owner,Amount,ValueProp.Unpowered,null);
    }
}
public sealed class SatoriDreamStudyPower : SatoriEnginePower {}
public sealed class SatoriRedStringsPower : SatoriEnginePower {}
public sealed class SatoriPuppetTheaterPower : SatoriEnginePower {}
public sealed class SatoriDreadAuraPower : SatoriEnginePower {}
public abstract class SatoriSwitchPower : SatoriEnginePower
{
    public override PowerStackType StackType=>PowerStackType.Single;
    public override Task AfterApplied(Creature? applier,CardModel? source) {if(Amount>1)SetAmount(1);return Task.CompletedTask;}
    public override Task AfterPowerAmountChanged(PlayerChoiceContext context,PowerModel power,decimal amount,Creature? applier,CardModel? source)
    {if(power==this && Amount>1)SetAmount(1);return Task.CompletedTask;}
}
public sealed class SatoriNightmarePower : SatoriSwitchPower {}
public sealed class SatoriDeepSeaPower : SatoriEnginePower {}
// Stable old IDs are retained while the two cards acquire new engines.
public sealed class SatoriRupturePower : SatoriSwitchPower
{
    public override Task AfterApplied(Creature? applier,CardModel? source)
    {
        base.AfterApplied(applier,source);
        if(Owner.GetPower<SatoriMindPower>() is {} mind)
            foreach(var card in mind.GeneratedCards.ToArray()) if(card.IsUpgradable && card.CombatState==Owner.CombatState) CardCmd.Upgrade(card);
        return Task.CompletedTask;
    }
    public override Task AfterCardGeneratedForCombat(CardModel card,Player? creator)
    {
        if(card.Owner==Owner.Player && card.IsUpgradable) CardCmd.Upgrade(card);
        return Task.CompletedTask;
    }
}
public sealed class SatoriFocusPower : SatoriEnginePower
{
    private const int MemoriesPerEcho=5;
    private int _memories;
    public int Progress=>_memories;
    public override int DisplayAmount=>_memories;
    public override async Task AfterCardPlayed(PlayerChoiceContext context,CardPlay play)
    {
        if(play.Card.Owner.Creature!=Owner || play.Card is not SatoriPoolCard {IsMemory:true}) return;
        _memories++;
        if(_memories>=MemoriesPerEcho)
        {
            _memories-=MemoriesPerEcho;
            await PowerCmd.Apply<SatoriEchoPower>(context,Owner,Amount,Owner,null);
        }
        InvokeDisplayAmountChanged();
    }
}
public sealed class SatoriMindWardPower : SatoriSwitchPower {}
public sealed class SatoriPerfectMemoryPower : SatoriEnginePower {}
public sealed class SatoriCalmPower : SatoriSwitchPower {}
public sealed class SatoriMemoryPalacePower : SatoriEnginePower {}
// The shipping build exposes the extra-turn hooks but has no public Ambergris
// power. Use those native hooks; the turn manager handles draw/reset/skip-enemies.
public sealed class SatoriExtraTurnPower : SatoriPower
{
    protected override bool IsVisibleInternal=>false;
    public override bool ShouldPlayVfx=>false;
    public override bool ShouldTakeExtraTurn(Player player)=>Amount>0 && player==Owner.Player;
    public override async Task AfterTakingExtraTurn(Player player)
    {
        if(player==Owner.Player) await PowerCmd.Decrement(this);
    }
}
public sealed class SatoriMindSovereignPower : SatoriEnginePower {}

// Native atlas paths need a hook; each original symbol is shared by small/big UI.
[HarmonyPatch(typeof(PowerModel),nameof(PowerModel.PackedIconPath),MethodType.Getter)]
internal static class PowerPlaceholderIcon
{
    private static bool Prefix(PowerModel __instance,ref string __result)
    {
        if (__instance is SatoriUnseenPresencePower)
        { __result=ModelDb.Power<IntangiblePower>().PackedIconPath; return false; }
        if (__instance is SatoriUnconsciousIntentPower)
        { __result=ChapterFour.Art+"unconscious-intent.png"; return false; }
        if(__instance is not SatoriPower && __instance is not SatoriTemporaryStrengthPower && __instance is not SatoriMistPower) return true;
        __result=Assets.PowerArtRoot+(__instance is SatoriTeaMemoryPower?"SatoriMirrorHallPower":__instance is SatoriExtraTurnPower?"SatoriMemoryPalacePower":__instance.GetType().Name)+".svg";return false;
    }
}
[HarmonyPatch(typeof(PowerModel),nameof(PowerModel.ResolvedBigIconPath),MethodType.Getter)]
internal static class PowerPlaceholderBigIcon
{
    private static bool Prefix(PowerModel __instance,ref string __result)
    {
        if (__instance is SatoriUnseenPresencePower)
        { __result=ModelDb.Power<IntangiblePower>().ResolvedBigIconPath; return false; }
        if (__instance is SatoriUnconsciousIntentPower)
        { __result=ChapterFour.Art+"unconscious-intent-big.png"; return false; }
        if(__instance is not SatoriPower && __instance is not SatoriTemporaryStrengthPower && __instance is not SatoriMistPower) return true;
        __result=Assets.PowerArtRoot+(__instance is SatoriTeaMemoryPower?"SatoriMirrorHallPower":__instance is SatoriExtraTurnPower?"SatoriMemoryPalacePower":__instance.GetType().Name)+".svg";return false;
    }
}
