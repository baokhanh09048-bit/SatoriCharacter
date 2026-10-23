using HarmonyLib;
using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.ValueProps;

namespace SatoriCharacter;

internal static class SatoriIntents
{
    internal static bool Provoke(Creature? target,Player player)
    {
        if(SatoriRules.HasAttack(target) || !SatoriRules.CanRewrite(target)) return false;
        var monster=target!.Monster!;
        var attacks=monster.MoveStateMachine!.States.Values.OfType<MoveState>()
            .Where(m=>!m.Id.StartsWith("SATORI_",StringComparison.Ordinal) && m.Intents.Any(i=>i is AttackIntent))
            .Distinct().ToArray();
        if(attacks.Length==0) return false;
        monster.SetMoveImmediate(player.RunState.Rng.CombatTargets.NextItem(attacks)!);
        return true;
    }
    // An appended intent runs after the original move. Its follow-up delegates to
    // the original state, so neither AI rotation nor mandatory boss moves change.
    private sealed class AppendedMove : MoveState
    {
        private readonly MoveState _original;
        internal AppendedMove(MoveState original,AbstractIntent intent,Func<Task> extra,string prefix="SATORI_APPENDED_")
            :base(prefix+original.Id,async targets=> {
                await original.PerformMove(targets);
                await extra();
            },original.Intents.Append(intent).ToArray())
        {
            _original=original;
            MustPerformOnceBeforeTransitioning=original.MustPerformOnceBeforeTransitioning;
        }
        public override string GetNextState(Creature owner,Rng rng)=>_original.GetNextState(owner,rng);
        public override void OnExitState() {base.OnExitState();_original.OnExitState();}
    }
    private static bool Append(Creature? target,AbstractIntent intent,Func<Task> extra,string prefix="SATORI_APPENDED_")
    {
        if(target is not {IsAlive:true,Monster:not null} || target.Monster.MoveStateMachine==null) return false;
        var monster=target.Monster;
        var move=new AppendedMove(monster.NextMove,intent,extra,prefix);
        monster.MoveStateMachine.States[move.Id]=move;
        // Appending preserves and executes the mandatory move in full.
        monster.SetMoveImmediate(move,true);
        return true;
    }
    internal static bool AddDefend(Creature? target,int block)
        =>Append(target,new DefendIntent(),async ()=> {
            if(target!.IsAlive) await CreatureCmd.GainBlock(target,block,ValueProp.Unpowered,null);
        });
    internal static bool AddKoishiCleanse(Creature target,Func<Task> extra)
        =>Append(target,new BuffIntent(),extra,"SATORI_KOISHI_CLEANSE_");
    internal static bool AddStrength(Creature? target,int strength,CardModel source)
        =>Append(target,new BuffIntent(),async ()=> {
            if(target!.IsAlive) await PowerCmd.Apply<StrengthPower>(new BlockingPlayerChoiceContext(),target,strength,target,source);
        });
    internal static void RewriteVulnerable(Creature? target,Creature player,int amount,CardModel source)
    {
        if(!SatoriRules.HasAttack(target) || !SatoriRules.CanRewrite(target)) return;
        var monster=target!.Monster!;var original=monster.NextMove;
        var move=new MoveState("SATORI_REWRITTEN_VULNERABLE",async _=> {
            if(target.IsAlive && player.IsAlive)
                await PowerCmd.Apply<VulnerablePower>(new BlockingPlayerChoiceContext(),player,amount,target,source);
        },new DebuffIntent()) {FollowUpStateId=original.Id,MustPerformOnceBeforeTransitioning=true};
        monster.MoveStateMachine!.States[move.Id]=move;
        monster.SetMoveImmediate(move);
    }
}

internal static class SatoriDiscordTargets
{
    private sealed class Redirect {internal Creature? Victim;}
    private static readonly ConditionalWeakTable<MonsterModel,Redirect> Moves=new();
    private static readonly ConditionalWeakTable<AttackCommand,Redirect> Commands=new();
    internal static void Begin(MonsterModel monster) {Moves.Remove(monster);Moves.Add(monster,new());}
    internal static void End(MonsterModel monster)=>Moves.Remove(monster);
    private static Creature Pick(Creature attacker)
        =>attacker.CombatState!.HittableEnemies.FirstOrDefault(e=>e!=attacker && e.IsAlive)??attacker;
    internal static bool TryRedirect(AttackCommand command,Creature attacker)
    {
        if(Commands.TryGetValue(command,out _)) return false;
        if(attacker.Monster!=null && Moves.TryGetValue(attacker.Monster,out var move))
        {
            if(move.Victim!=null) return false;
            move.Victim=Pick(attacker);
            return true;
        }
        Commands.Add(command,new Redirect {Victim=Pick(attacker)});
        return true;
    }
    internal static Creature? Target(AttackCommand command)
    {
        var attacker=command.Attacker;
        if(attacker==null || !command.DamageProps.IsPoweredAttack()) return null;
        Redirect? redirect=null;
        if(!Commands.TryGetValue(command,out redirect) && attacker.Monster!=null) Moves.TryGetValue(attacker.Monster,out redirect);
        if(redirect?.Victim==null) return null;
        // A dead target is not replaced between hits: native attacks stop when
        // their chosen target dies, rather than spilling damage onto the player.
        return redirect.Victim;
    }
}

[HarmonyPatch(typeof(AttackCommand),"GetPossibleTargets")]
internal static class DiscordAttackTargets
{
    private static bool Prefix(AttackCommand __instance,ref IReadOnlyList<Creature> __result)
    {
        if(SatoriDiscordTargets.Target(__instance) is not {} target) return true;
        __result=new[]{target};return false;
    }
}

[HarmonyPatch(typeof(MonsterModel),nameof(MonsterModel.PerformMove))]
internal static class DiscordMoveScope
{
    private static void Prefix(MonsterModel __instance)=>SatoriDiscordTargets.Begin(__instance);
    private static void Postfix(MonsterModel __instance,ref Task __result)=>__result=Finish(__result,__instance);
    private static async Task Finish(Task task,MonsterModel monster)
    {
        try {await task;} finally {SatoriDiscordTargets.End(monster);}
    }
}
