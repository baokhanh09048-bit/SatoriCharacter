using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace SatoriCharacter;

// Each cup has its own two-turn countdown, including when played more than once.
public sealed class SatoriTeaMemoryPower : SatoriPower
{
    public override PowerInstanceType InstanceType=>PowerInstanceType.Instanced;
    public override async Task AfterPlayerTurnStart(PlayerChoiceContext context,MegaCrit.Sts2.Core.Entities.Players.Player player)
    {
        if(player.Creature!=Owner || !Owner.IsAlive) return;
        var enemies=SatoriRules.Enemies(player).ToArray();
        if(enemies.Length>0)
            await SatoriRules.Mirror(context,player,player.RunState.Rng.CombatTargets.NextItem(enemies),null);
        await PowerCmd.Decrement(this);
    }
}

public sealed class SatoriMentalShieldPower : SatoriSwitchPower
{
    public override PowerStackType StackType=>PowerStackType.Single;
    public override bool TryModifyPowerAmountReceived(PowerModel canonicalPower,Creature target,decimal amount,Creature? applier,out decimal modifiedAmount)
    {
        bool expiry=canonicalPower is MegaCrit.Sts2.Core.Models.Powers.StrengthPower && SatoriTemporaryStrengthPower.IsExpiringFor(Owner);
        bool blocked=target==Owner && !expiry && canonicalPower.GetTypeForAmount(amount)==PowerType.Debuff;
        modifiedAmount=blocked?0:amount;return blocked;
    }
    public override async Task BeforeSideTurnStart(PlayerChoiceContext context,CombatSide side,IReadOnlyList<Creature> participants,ICombatState state)
    {
        if(side==CombatSide.Player && participants.Contains(Owner)) await PowerCmd.Remove(this);
    }
}

public sealed class SatoriTemporaryFocusPower : SatoriPower
{
    public override async Task BeforeApplied(Creature target,decimal amount,Creature? applier,CardModel? source)
        =>await PowerCmd.Apply<SatoriEchoPower>(new ThrowingPlayerChoiceContext(),target,amount,applier,source);
    public override async Task AfterPowerAmountChanged(PlayerChoiceContext context,PowerModel power,decimal amount,Creature? applier,CardModel? source)
    {
        if(power==this && amount!=Amount) await PowerCmd.Apply<SatoriEchoPower>(context,Owner,amount,applier,source);
    }
    public override async Task AfterSideTurnEnd(PlayerChoiceContext context,CombatSide side,IEnumerable<Creature> participants)
    {
        if(side!=CombatSide.Player || !participants.Contains(Owner)) return;
        int amount=Amount;await PowerCmd.Remove(this);
        if(Owner.GetPower<SatoriEchoPower>() is {} focus) await PowerCmd.ModifyAmount(context,focus,-Math.Min(amount,focus.Amount),Owner,null);
    }
}

public sealed class SatoriKingDisdainPower : SatoriSwitchPower
{
    private static readonly AsyncLocal<Creature?> Reacting=new();
    public override async Task AfterPowerAmountChanged(PlayerChoiceContext context,PowerModel power,decimal amount,Creature? applier,CardModel? source)
    {
        await base.AfterPowerAmountChanged(context,power,amount,applier,source);
        if(amount<=0 || applier!=Owner || Reacting.Value==Owner || !power.Owner.IsAlive) return;
        if(power is not SatoriSuggestionPower && power is not SatoriMentalLoadPower) return;
        var previous=Reacting.Value;Reacting.Value=Owner;
        try
        {
            if(power is SatoriSuggestionPower) await SatoriRules.ApplyLoad(context,power.Owner,3,Owner,source);
            else await SatoriRules.ApplySuggestion(context,power.Owner,3,Owner,source);
        }
        finally {Reacting.Value=previous;}
    }
}
