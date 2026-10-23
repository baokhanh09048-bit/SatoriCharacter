using Godot;
using HarmonyLib;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.ValueProps;

namespace SatoriCharacter;

public sealed class SatoriKoishiEncounter : EncounterModel
{
    public override RoomType RoomType => RoomType.Boss;
    public override bool ShouldGiveRewards => false;
    public override string CustomBgm=>ChapterFourMusic.Path;
    public override IEnumerable<MonsterModel> AllPossibleMonsters => new[] { ModelDb.Monster<SatoriKoishi>() };
    public override string BossNodePath => ChapterFour.Art+"koishi-map";
    public override IEnumerable<string> ExtraAssetPaths => new[] {
        ChapterFour.Art+"koishi.png",ChapterFour.Art+"orin.png",ChapterFour.Art+"okuu.png",
        "res://SatoriCharacter/scenes/garden_actor.tscn", "res://SatoriCharacter/scenes/garden_backdrop.tscn",ChapterFourMusic.Path }
        .Concat(BattleDialogueVoices.AssetPaths);
    protected override IReadOnlyList<(MonsterModel,string?)> GenerateMonsters() => new[] { (ModelDb.Monster<SatoriKoishi>().ToMutable(),(string?)null) };
}

public sealed class SatoriKoishi : MonsterModel
{
    internal const int InitialHeartKnots=6;
    internal const int KnotsToBreak=4;
    private const int MultiAttackDamage=7;
    private const int HeavyAttackDamage=29;
    public override int MinInitialHp => AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies,514,360);
    public override int MaxInitialHp => MinInitialHp;
    protected override string VisualsPath => "res://scenes/creature_visuals/fallback.tscn";
    public override bool ShouldFadeAfterDeath => false;
    public override bool ShouldDisappearFromDoom => false;
    public override string DeathSfx => "";
    private bool _showActionIntent;
    internal bool HidesIntent => SatoriConfig.HideKoishiIntents && Creature.IsAlive && !_showActionIntent;
    public override IEnumerable<string> AssetPaths => base.AssetPaths.Append("res://SatoriCharacter/scenes/garden_actor.tscn")
        .Append(ChapterFour.Art+"koishi.png").Concat(new UnknownIntent().AssetPaths).Concat(NSpeechBubbleVfx.AssetPaths);

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var hide = PreviewedMove("SUBCONSCIOUS",Subconscious,new MultiAttackIntent(MultiAttackDamage,3));
        var garden = PreviewedMove("GARDEN",Garden,new DefendIntent(),new BuffIntent());
        var rose = PreviewedMove("ROSE",Confusion,new DebuffIntent());
        var finish = PreviewedMove("REKINDLED",Rekindled,new SingleAttackIntent(HeavyAttackDamage));
        var choose = new RandomBranchState("CHOOSE_ACTION");
        choose.AddBranch(hide,MoveRepeatType.CannotRepeat,4f);
        choose.AddBranch(garden,MoveRepeatType.CannotRepeat,()=>MoveStateMachine?.StateLog.LastOrDefault()?.Id=="LAST_GAME" ? 0f : 3f);
        choose.AddBranch(rose,MoveRepeatType.CannotRepeat,2f);
        choose.AddBranch(finish,MoveRepeatType.CannotRepeat,()=>MoveStateMachine?.StateLog.LastOrDefault()?.Id=="HEART" ? 0f : 2f);
        // Retain old saved move IDs as aliases, excluded from random selection.
        var heart = PreviewedMove("HEART",Rekindled,new SingleAttackIntent(HeavyAttackDamage));
        var pause = PreviewedMove("LAST_GAME",Garden,new DefendIntent(),new BuffIntent());
        foreach (var move in new[] {hide,garden,rose,finish,heart,pause}) move.FollowUpState=choose;
        return new MonsterMoveStateMachine(new List<MonsterState> { hide,garden,rose,finish,heart,pause,choose },hide);
    }
    private MoveState PreviewedMove(string id,Func<IReadOnlyList<Creature>,Task> action,params AbstractIntent[] intents)
        => new(id,async targets=>
        {
            if (NCombatRoomNode() is {} node)
            {
                // PerformMove already exposes the real intent. Show it immediately:
                // native fading takes up to 1.3s and would consume the reading pause.
                await node.RefreshIntents();
                (AccessTools.Field(typeof(NCreature),"_intentFadeTween")?.GetValue(node) as Tween)?.Kill();
                node.IntentContainer.Modulate=Colors.White;
                await Cmd.Wait(1f);
            }
            if (Creature.IsAlive && !CombatManager.Instance.IsEnding) await action(targets);
        },intents);
    public override async Task BeforeCombatStart()
    {
        _showActionIntent=false;
        var context=new ThrowingPlayerChoiceContext();
        await PowerCmd.Apply<SatoriUnseenPresencePower>(context,Creature,KnotsToBreak*Creature.CombatState!.Players.Count(player=>player.Creature.IsAlive),Creature,null);
        var actor=NCombatRoomNode()?.Visuals?.GetNodeOrNull<Node2D>("%Visuals");
        if (actor?.HasMethod("garden_set_unseen")==true) actor.Call("garden_set_unseen",true);
        await PowerCmd.Apply<SatoriUnconsciousIntentPower>(context,Creature,1,Creature,null);
        foreach (var player in Creature.CombatState!.Players.Where(player=>player.Creature.IsAlive))
        {
            // Combat copies, placed with the run's shuffle RNG. Never modify the permanent deck.
            for (int i=0;i<InitialHeartKnots;i++)
            {
                var knot=Creature.CombatState.CreateCard<SatoriHeartKnot>(player);
                await CardPileCmd.AddGeneratedCardToCombat(knot,PileType.Draw,player,CardPilePosition.Random);
            }
        }
        ChapterFourVisuals.ShowBattleIntroduction(Creature);
    }
    public override Task BeforeSideTurnStart(PlayerChoiceContext context, CombatSide side, IReadOnlyList<Creature> participants,ICombatState state)
    {
        if (side==CombatSide.Enemy) QueueHalfHealthCleanse();
        return Task.CompletedTask;
    }
    public override Task AfterSideTurnStartLate(CombatSide side,IReadOnlyList<Creature> participants,ICombatState state)
    {
        // Warn during the player's planning window, including appended moves.
        if (side==CombatSide.Player && Creature.IsAlive && IsHeavyMove())
            ChapterFourVisuals.ShowLine(Creature,"boss.heavyWarning."+(state.RoundNumber%3));
        return Task.CompletedTask;
    }
    private bool IsHeavyMove() => NextMove.Id.EndsWith("REKINDLED",StringComparison.Ordinal)
        || NextMove.Id.EndsWith("HEART",StringComparison.Ordinal);
    public override Task AfterCurrentHpChanged(Creature creature,decimal delta)
    {
        // Do not replace a currently executing move when Discord hurts Koishi herself.
        if (creature==Creature && !IsPerformingMove) QueueHalfHealthCleanse();
        return Task.CompletedTask;
    }
    private NCreature? NCombatRoomNode() => MegaCrit.Sts2.Core.Nodes.Rooms.NCombatRoom.Instance?.GetCreatureNode(Creature);
    internal void ShowActionIntent(bool show)
    {
        _showActionIntent=show;
        if (NCombatRoomNode() is {} node) TaskHelper.RunSafely(node.RefreshIntents());
    }
    private void QueueHalfHealthCleanse()
    {
        // The native power amount is the save-persistent phase marker: 1 before, 2 after.
        if (!Creature.IsAlive || Creature.CurrentHp*2>Creature.MaxHp || MoveStateMachine==null
            || Creature.GetPower<SatoriUnconsciousIntentPower>() is not {Amount:1}
            || NextMove.Intents.Any(intent=>intent is StunIntent)
            || NextMove.Id.Contains("SATORI_KOISHI_CLEANSE_",StringComparison.Ordinal)) return;
        SatoriIntents.AddKoishiCleanse(Creature,Cleanse);
    }
    private async Task Cleanse()
    {
        if (!Creature.IsAlive || Creature.GetPower<SatoriUnconsciousIntentPower>() is not {Amount:1} marker) return;
        // Mark first, before removal hooks, so healing or re-entrant effects cannot repeat it.
        await PowerCmd.ModifyAmount(new ThrowingPlayerChoiceContext(),marker,1,Creature,null);
        ChapterFourVisuals.PlayCreature(Creature,"Cast");
        ChapterFourVisuals.ShowLine(Creature,"boss.phase2");
        foreach (var power in Creature.Powers.Where(power=>power.TypeForCurrentAmount==PowerType.Debuff).ToArray())
            if (Creature.IsAlive && Creature.Powers.Contains(power)) await PowerCmd.Remove(power);
        if (Creature.IsAlive) await PowerCmd.Apply<StrengthPower>(new ThrowingPlayerChoiceContext(),Creature,5,Creature,null);
    }
    private async Task Attack(int damage,int hits=1)
    {
        ChapterFourVisuals.PlayCreature(Creature,"Attack");
        await DamageCmd.Attack(damage).FromMonster(this).WithHitCount(hits).WithNoAttackerAnim()
            .WithHitFx("vfx/vfx_attack_slash").Execute(null);
    }
    private Task Subconscious(IReadOnlyList<Creature> targets) => Attack(MultiAttackDamage,3);
    // The warning is shown only at the start of the player's planning turn.
    private Task Rekindled(IReadOnlyList<Creature> targets) => Attack(HeavyAttackDamage);
    private async Task Garden(IReadOnlyList<Creature> targets)
    {
        ChapterFourVisuals.PlayCreature(Creature,"Cast");
        await CreatureCmd.GainBlock(Creature,16,ValueProp.Unpowered,null);
        await PowerCmd.Apply<StrengthPower>(new ThrowingPlayerChoiceContext(),Creature,2,Creature,null);
    }
    private async Task Confusion(IReadOnlyList<Creature> targets)
    {
        ChapterFourVisuals.PlayCreature(Creature,"Cast");
        var context=new ThrowingPlayerChoiceContext();
        var living=targets.Where(target=>target.IsAlive).ToArray();
        await PowerCmd.Apply<WeakPower>(context,living,1,Creature,null);
        await PowerCmd.Apply<FrailPower>(context,living,1,Creature,null);
        await PowerCmd.Apply<VulnerablePower>(context,living,2,Creature,null);
        await CardPileCmd.AddToCombatAndPreview<Dazed>(living,PileType.Draw,2,null,CardPilePosition.Random);
    }
}

// Amount is the party's remaining knots; each participant starts with six cards.
public sealed class SatoriUnseenPresencePower : SatoriPower
{
    private int _goal=SatoriKoishi.KnotsToBreak;
    private HashSet<ulong> _participants=new();
    private HashSet<ulong> _waived=new();
    public override Task BeforeApplied(Creature target,decimal amount,Creature? applier,CardModel? cardSource)
    {
        _goal=Math.Max(SatoriKoishi.KnotsToBreak,(int)amount);
        _participants=target.CombatState!.Players.Where(player=>player.Creature.IsAlive).Select(player=>player.NetId).ToHashSet();
        _waived=new();
        return Task.CompletedTask;
    }
    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _participants=new(_participants);_waived=new(_waived);
    }
    public override LocString Description
    {
        get
        {
            var description=base.Description;
            description.Add("Goal",_goal);
            description.Add("Progress",IsMutable ? _goal-Math.Clamp(Amount,0,_goal) : 0);
            return description;
        }
    }
    public override decimal ModifyHpLostAfterOsty(Creature target,decimal amount,ValueProp props,Creature? dealer,CardModel? source)
        => CombatManager.Instance.IsInProgress && target==Owner && amount>1 ? 1 : amount;
    internal async Task ResolveKnot(PlayerChoiceContext context,CardPlay play)
    {
        if (play.Card is not SatoriHeartKnot || !Owner.IsAlive || Amount<=0) return;
        if (Amount>1) await PowerCmd.ModifyAmount(context,this,-1,play.Card.Owner.Creature,play.Card);
        else await Break(context);
    }
    public override async Task AfterDeath(PlayerChoiceContext context,Creature creature,bool wasRemovalPrevented,float deathAnimLength)
    {
        if(wasRemovalPrevented || creature.IsAlive || creature.Player is not {} player || !_participants.Contains(player.NetId)
            || !Owner.IsAlive || Amount<=0 || !Owner.Powers.Contains(this)
            || !Owner.CombatState!.Players.Any(other=>other.Creature.IsAlive) || !_waived.Add(player.NetId)) return;
        // Preserve earned progress. A fallen player's four-point quota is waived
        // once, so their inaccessible hand cannot strand the surviving party.
        int progress=_goal-Amount;
        _goal=Math.Max(SatoriKoishi.KnotsToBreak,_goal-SatoriKoishi.KnotsToBreak);
        int remaining=Math.Max(0,_goal-progress);
        if(remaining==0) await Break(context);
        else await PowerCmd.ModifyAmount(context,this,remaining-Amount,creature,null);
    }
    private async Task Break(PlayerChoiceContext context)
    {
            await PowerCmd.Remove(this);
            ChapterFourVisuals.PlayCreature(Owner,"PresenceBroken");
            if (ChapterFour.Player(Owner.CombatState?.RunState) is {} player)
                ChapterFourVisuals.ShowLine(player.Creature,"boss.presenceBroken");
            var knots=Owner.CombatState!.Players.SelectMany(player=>player.PlayerCombatState!.AllCards)
                .Where(card=>card is SatoriHeartKnot && card.Pile?.Type!=PileType.Exhaust).ToArray();
            foreach (var knot in knots)
                if (knot.Pile!=null && knot.Pile.Type!=PileType.Exhaust) await Sts2Compat.Exhaust(context,knot);
    }
}

[HarmonyPatch]
internal static class KoishiDamageCapCompat
{
    // Beta added CardPlay to this hook; resolve the base method on each branch.
    private static System.Reflection.MethodBase TargetMethod() => AccessTools.GetDeclaredMethods(typeof(AbstractModel)).Single(m=>m.Name=="ModifyDamageCap");
    private static bool Prefix(AbstractModel __instance,object[] __args,ref decimal __result)
    {
        if (__instance is not SatoriUnseenPresencePower power) return true;
        __result=__args[0] as Creature == power.Owner ? 1 : decimal.MaxValue;
        return false;
    }
}
public sealed class SatoriUnconsciousIntentPower : SatoriPower
{
    public override LocString Description=>SatoriConfig.HideKoishiIntents
        ? base.Description : ChapterFour.Text("boss.intent.visible");
}

// Amount continues to track the remaining proofs for mechanics and hover text.
// Only the native icon's counter shows infinity: it has no turn-based expiry.
[HarmonyPatch(typeof(NPower),"RefreshAmount")]
internal static class KoishiIntangibleAmountPatch
{
    private static void Postfix(NPower __instance)
    {
        if (__instance.Model is SatoriUnseenPresencePower
            && __instance.GetNodeOrNull<MegaLabel>("%AmountLabel") is {} label)
            label.SetTextAutoSize("∞");
    }
}

[HarmonyPatch(typeof(NCreature),nameof(NCreature.UpdateIntent))]
internal static class KoishiHiddenIntentPatch
{
    private static bool Prefix(NCreature __instance,IEnumerable<Creature> targets,ref Task __result)
    {
        if (__instance.Entity.Monster is not SatoriKoishi {HidesIntent:true}) return true;
        // Collapse display nodes only. NextMove.Intents remains authoritative for
        // Read Mind, AI, appended actions and execution-time native rendering.
        var container=__instance.IntentContainer;
        var question=container.GetChildren().OfType<NIntent>().FirstOrDefault();
        foreach (var child in container.GetChildren().Where(child=>child!=question))
        {
            container.RemoveChildSafely(child);
            child.QueueFreeSafely();
        }
        if (question==null)
        {
            question=NIntent.Create(__instance.GetHashCode()*.01f);
            container.AddChildSafely(question);
        }
        question.SetFrozen(false);
        question.UpdateIntent(new UnknownIntent(),targets,__instance.Entity);
        __result=Task.CompletedTask;
        return false;
    }
    internal static void RefreshSettings()
    {
        var room=MegaCrit.Sts2.Core.Nodes.Rooms.NCombatRoom.Instance;
        if (room==null || CombatManager.Instance.IsEnding) return;
        foreach (var node in room.CreatureNodes.Where(node=>node.Entity.Monster is SatoriKoishi))
            if (node.Entity.CombatState is {} state)
                TaskHelper.RunSafely(node.UpdateIntent(state.Players.Select(player=>player.Creature)));
    }
}

[HarmonyPatch(typeof(MonsterModel),nameof(MonsterModel.PerformMove))]
internal static class KoishiActionIntentPatch
{
    private static void Prefix(MonsterModel __instance)
    {
        if (__instance is SatoriKoishi koishi) koishi.ShowActionIntent(true);
    }
    private static void Postfix(MonsterModel __instance,ref Task __result)
    {
        if (__instance is SatoriKoishi koishi) __result=Finish(__result,koishi);
    }
    private static async Task Finish(Task task,SatoriKoishi koishi)
    {
        try { await task; }
        finally { koishi.ShowActionIntent(false); }
    }
}

internal static class GardenCompanionActions
{
    internal static async Task Begin(SatoriLostHeart key)
    {
        var player=key.Owner;
        var combat=player.Creature.CombatState;
        if (!player.Creature.IsAlive || combat?.Encounter is not SatoriKoishiEncounter) return;
        if (key.CompanionChoice==4)
        {
            var card=combat.CreateCard<SatoriPerfectMemory>(player);
            card.UpgradeInternal(); card.FinalizeUpgradeInternal();
            card.EnergyCost.SetCustomBaseCost(0); card.AddKeyword(CardKeyword.Innate);
            await CardPileCmd.AddGeneratedCardToCombat(card,PileType.Draw,player,CardPilePosition.Top);
        }
    }
    internal static async Task Turn(SatoriLostHeart key,PlayerChoiceContext context,Player player)
    {
        if (player!=key.Owner || !player.Creature.IsAlive || player.Creature.CombatState?.Encounter is not SatoriKoishiEncounter) return;
        var boss=player.Creature.CombatState.Enemies.FirstOrDefault(e=>e.IsAlive);
        if (boss==null) return;
        int turn=player.PlayerCombatState!.TurnNumber;
        if (key.CompanionChoice is 2 or 3)
        {
            var aid=player.Creature.CombatState.CreateCard<SatoriSpiritEscort>(player);
            await CardPileCmd.AddGeneratedCardToCombat(aid,PileType.Hand,player);
        }
        if (key.CompanionChoice is 1 or 3 && boss.IsAlive)
        {
            ChapterFourVisuals.SetCharge(player,turn%2);
            if (turn%2==0)
            {
                var aid=player.Creature.CombatState.CreateCard<SatoriNuclearAid>(player);
                await CardPileCmd.AddGeneratedCardToCombat(aid,PileType.Hand,player);
            }
        }
    }
}
