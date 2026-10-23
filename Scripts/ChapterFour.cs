using BaseLib.Abstracts;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Entities.RestSite;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Achievements;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Acts;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace SatoriCharacter;

internal static class ChapterFour
{
    internal static RunState? Run => AccessTools.Property(typeof(RunManager),"State").GetValue(RunManager.Instance) as RunState;
    internal const string Art = "res://SatoriCharacter/assets/act4/";
    internal static Player? Player(IRunState? run) => run?.Players.FirstOrDefault(player=>player.Character is Satori);
    internal static SatoriLostHeart? EntranceKey(IRunState run) => run.Players.Where(player=>player.Character is Satori)
        .Select(player=>player.GetRelic<SatoriLostHeart>()).FirstOrDefault(key=>key is {EntranceDeclined:false});
    // Multiplayer route rules must not depend on each client's local settings.
    // Taking the optional key is the party's opt-in; preview remains single-player.
    internal static bool Eligible(IRunState? run) => run != null && (run.Players.Count>1 || SatoriConfig.EnableFourthChapter)
        && run.GameMode == GameMode.Standard && Player(run) != null && (run.Acts.Count==3 || run.Act is SatoriGarden);
    internal static LocString Text(string key) => new("events", "SATORI_ACT4."+key);
    internal static Task FadeOut() => NGame.Instance?.Transition.RoomFadeOut() ?? Task.CompletedTask;
    internal static Task FadeIn() => (Task)AccessTools.Method(typeof(RunManager),"FadeIn").Invoke(RunManager.Instance,new object[] { true })!;
    internal static async Task Win()
    {
        var manager=RunManager.Instance;
        // Beta's public WinRun supports any victory room. Stable's private method
        // casts the local event to TheArchitect for its animation; use its native
        // result/statistics and game-over commands directly for our own ending.
        var publicWin=typeof(RunManager).GetMethod("WinRun",System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Instance);
        if (publicWin!=null) { await (Task)publicWin.Invoke(manager,null)!; return; }
        AccessTools.Method(typeof(RunManager),"OnEnded").Invoke(manager,new object[] { true });
        await (Task)AccessTools.Method(typeof(RunManager),"GuaranteeKillAllPlayers").Invoke(manager,null)!;
    }

    // Select from the completed topology, without consuming shared RNG. This also
    // reconstructs the same marker from native saved maps (which don't save quests).
    internal static MapPoint? BurningElite(IRunState? run)
    {
        if (!Eligible(run) || run!.CurrentActIndex > 2 || run.Players.Any(player=>player.Character is Satori && player.GetRelic<SatoriLostHeart>()!=null)) return null;
        var reachable = new HashSet<MapPoint>();
        var queue = new Queue<MapPoint>(run.Map.startMapPoints);
        queue.Enqueue(run.Map.StartingMapPoint);
        while (queue.TryDequeue(out var point))
            if (reachable.Add(point)) foreach (var child in point.Children) queue.Enqueue(child);
        return reachable.Where(p => p.PointType == MapPointType.Elite)
            .OrderBy(p => BurningOrder(run, p)).FirstOrDefault();
    }
    private static ulong BurningOrder(IRunState run, MapPoint p)
    {
        // Seed widened from UInt32 in stable to UInt64 in beta.
        ulong seed=Convert.ToUInt64(AccessTools.Property(typeof(RunRngSet),"Seed").GetValue(run.Rng));
        ulong x = seed ^ (ulong)(run.CurrentActIndex + 1) * 0x9e3779b97f4a7c15UL
            ^ (ulong)(p.coord.row * 17 + p.coord.col + 1);
        x ^= x >> 30; x *= 0xbf58476d1ce4e5b9UL; x ^= x >> 27;
        return x * 0x94d049bb133111ebUL;
    }
    internal static bool AtBurningElite(IRunState run) => BurningElite(run) is {} point && run.CurrentMapPoint?.coord == point.coord;

    internal static void AppendGarden(RunState run)
    {
        if (run.Acts.Any(a => a is SatoriGarden)) return;
        // Don't replace or reorder another mod's added acts.
        if (run.Acts.Count != 3) throw new InvalidOperationException("Satori garden requires the normal three-act route.");
        var garden = (SatoriGarden)ModelDb.Act<SatoriGarden>().ToMutable();
        garden.PrepareRooms();
        AccessTools.PropertySetter(typeof(RunState), nameof(RunState.Acts)).Invoke(run, new object[] { run.Acts.Append(garden).ToList() });
    }
}

// These story items never enter the existing ten-relic character pool.
public abstract class SatoriStoryRelic : RelicModel
{
    protected abstract string ArtName { get; }
    public override RelicRarity Rarity => RelicRarity.Event;
    public override bool IsAllowedInShops => false;
    public override string PackedIconPath => ChapterFour.Art + ArtName + ".png";
    protected override string PackedIconOutlinePath => ChapterFour.Art + ArtName + "-outline.png";
    protected override string BigIconPath => ChapterFour.Art + ArtName + "-big.png";
}

public sealed class SatoriLostHeart : SatoriStoryRelic
{
    protected override string ArtName => "lost-heart";
    [SavedProperty] public bool EntranceAccepted { get; set; }
    [SavedProperty] public bool EntranceDeclined { get; set; }
    [SavedProperty] public int CompanionChoice { get; set; }
    [SavedProperty] public bool PreviewPending { get; set; }
    [SavedProperty] public bool GardenEntryHealed { get; set; }
    public override async Task AfterActEntered()
    {
        if (GardenEntryHealed || Owner.RunState.Act is not SatoriGarden
            || !Owner.Creature.IsAlive) return;
        // The garden has no ancient room. Reuse its heal command and ascension
        // rule once at entry, before the first rest-site choice.
        decimal amount=Owner.Creature.MaxHp-Owner.Creature.CurrentHp;
        if (Owner.RunState.AscensionLevel>=(int)AscensionLevel.WearyTraveler) amount*=.8m;
        GardenEntryHealed=true;
        await CreatureCmd.Heal(Owner.Creature,amount,playAnim:false);
        // EnterAct saves the map before AfterActEntered. Persist HP and marker
        // together so quitting on the entry map cannot lose or repeat the heal.
        // Like native act-entry map saves, no completed room is restored here.
        // Passing MapRoom as preFinishedRoom writes a checkpoint the native
        // AbstractRoom.FromSerializable loader cannot deserialize.
        await SaveManager.Instance.SaveRun(null,saveProgress:false);
    }
    public override Task BeforeCombatStart() => GardenCompanionActions.Begin(this);
    public override Task AfterPlayerTurnStart(PlayerChoiceContext context,Player player) => GardenCompanionActions.Turn(this,context,player);
}
public sealed class SatoriPreciousMemory : SatoriStoryRelic
{
    protected override string ArtName => "precious-memory";
}

// A separate, event-only pool gives native descriptions/collection a valid
// energy-icon owner without adding story items to Satori's ten random relics.
public sealed class SatoriStoryRelicPool : RelicPoolModel
{
    public override string EnergyColorName => "satori";
    public override Color LabOutlineColor => new("D18FAB");
    protected override IEnumerable<RelicModel> GenerateAllRelics() => new RelicModel[] {
        ModelDb.Relic<SatoriLostHeart>(), ModelDb.Relic<SatoriPreciousMemory>() };
}
[HarmonyPatch(typeof(ModelDb),nameof(ModelDb.AllRelicPools),MethodType.Getter)]
internal static class GardenStoryRelicPoolPatch
{
    private static void Postfix(ref IEnumerable<RelicPoolModel> __result)
    {
        __result=__result.Append(ModelDb.RelicPool<SatoriStoryRelicPool>()).Distinct();
    }
}

public sealed class SatoriGarden : CustomActModel
{
    public SatoriGarden() : base(4, false) { }
    public override Color MapTraveledColor => new("e9c47d");
    public override Color MapUntraveledColor => new("b9a5cf");
    public override Color MapBgColor => new("302c42");
    protected override int BaseNumberOfRooms => 3;
    public override IEnumerable<AncientEventModel> AllAncients => Array.Empty<AncientEventModel>();
    public override IEnumerable<EventModel> AllEvents => new[] { ModelDb.Event<SatoriGardenVisitors>() };
    public override IEnumerable<EncounterModel> GenerateAllEncounters() => new[] { ModelDb.Encounter<SatoriKoishiEncounter>() };
    protected override string CustomMapTopBgPath => ChapterFour.Art+"garden-map-top.png";
    protected override string CustomMapMidBgPath => ChapterFour.Art+"garden-map-middle.png";
    protected override string CustomMapBotBgPath => ChapterFour.Art+"garden-map-bottom.png";
    protected override string CustomRestSiteBackgroundPath => ModelDb.Act<Glory>().RestSiteBackgroundPath;
    protected override ActMap? CustomCreateMap(RunState state, bool replaceTreasureWithElites) => new SatoriGardenMap();
    internal void PrepareRooms()
    {
        AssertMutable();
        _rooms = new RoomSet { Boss = ModelDb.Encounter<SatoriKoishiEncounter>() };
        _rooms.events.Add(ModelDb.Event<SatoriGardenVisitors>());
    }
}

internal sealed class SatoriGardenMap : ActMap
{
    private readonly MapPoint?[,] _grid = new MapPoint?[7,3];
    protected override MapPoint?[,] Grid => _grid;
    public override MapPoint StartingMapPoint { get; } = new(3,0) { PointType = MapPointType.RestSite, CanBeModified = false };
    public override MapPoint BossMapPoint { get; } = new(3,3) { PointType = MapPointType.Boss, CanBeModified = false };
    internal SatoriGardenMap()
    {
        MapPoint previous = StartingMapPoint;
        // NMapScreen enables StartingMapPoint on an unvisited map and creates
        // its node separately from Grid. The actual rest site must be that
        // starting point, without a duplicate node in the grid.
        startMapPoints.Add(StartingMapPoint);
        var types = new[] { MapPointType.Shop, MapPointType.Unknown };
        for (int row = 1; row <= types.Length; row++)
        {
            var point = new MapPoint(3,row) { PointType = types[row-1], CanBeModified = false };
            _grid[3,row] = point; previous.AddChildPoint(point); previous = point;
        }
        previous.AddChildPoint(BossMapPoint);
    }
}

[HarmonyPatch(typeof(ActModel), nameof(ActModel.GenerateRooms))]
internal static class GardenRoomsPatch
{
    private static bool Prefix(ActModel __instance)
    {
        if (__instance is not SatoriGarden garden) return true;
        garden.PrepareRooms(); return false;
    }
}
[HarmonyPatch(typeof(RunManager), "RollRoomTypeFor")]
internal static class GardenQuestionRoomPatch
{
    private static bool Prefix(MapPointType pointType, ref RoomType __result)
    {
        if (ChapterFour.Run?.Act is not SatoriGarden || pointType != MapPointType.Unknown) return true;
        __result = RoomType.Event; return false;
    }
}

// Append only after the final Act 3 boss, including A10's second boss. Native
// EnterNextAct then opens the garden map. The four choices belong to its '?' room.
[HarmonyPatch(typeof(RunManager),nameof(RunManager.EnterNextAct))]
internal static class GardenAppendAfterBossPatch
{
    private static void Prefix()
    {
        var run=ChapterFour.Run;
        if (!ChapterFour.Eligible(run) || run!.CurrentActIndex!=2 || run.Acts.Count!=3) return;
        if (run.CurrentRoom is not CombatRoom { RoomType:RoomType.Boss }) return;
        if (ChapterFour.EntranceKey(run) is not {} key) return;
        key.EntranceAccepted=true;
        ChapterFour.AppendGarden(run);
    }
}
[HarmonyPatch(typeof(RunManager), nameof(RunManager.EnterRoom))]
internal static class GardenEntrancePatch
{
    private static void Prefix(RunManager __instance, ref AbstractRoom room)
    {
        var run = ChapterFour.Run;
        if (room is not EventRoom { CanonicalEvent: TheArchitect }) return;
        if (run?.Act is SatoriGarden) { room = new EventRoom(ModelDb.Event<SatoriHomecoming>()); return; }
    }
}

[HarmonyPatch(typeof(NEventRoom), nameof(NEventRoom.Proceed))]
internal static class GardenEntranceProceedPatch
{
    private static bool Prefix(ref Task __result)
    {
        if (ChapterFour.Run?.CurrentRoom is not EventRoom { CanonicalEvent: SatoriGardenEntrance }) return true;
        __result = Continue(); return false;
    }
    private static async Task Continue()
    {
        var manager = RunManager.Instance;
        var run = ChapterFour.Run!;
        if (ChapterFour.Player(run)?.GetRelic<SatoriLostHeart>() is { EntranceAccepted: true })
        {
            ChapterFour.AppendGarden(run);
            await manager.EnterAct(3);
        }
        else
        {
            await ChapterFour.FadeOut();
            await manager.EnterRoom(new EventRoom(ModelDb.Event<TheArchitect>()));
            await ChapterFour.FadeIn();
        }
    }
}

[HarmonyPatch(typeof(Hook), nameof(Hook.BeforeCombatStart))]
internal static class BurningEliteCombatPatch
{
    private static void Postfix(IRunState runState, ICombatState? combatState, ref Task __result)
    {
        if (combatState != null && ChapterFour.AtBurningElite(runState)) __result = Apply(__result,runState,combatState);
    }
    private static async Task Apply(Task native, IRunState run, ICombatState combat)
    {
        await native;
        if (run.CurrentRoom is not CombatRoom { RoomType: RoomType.Elite }) return;
        var context = new ThrowingPlayerChoiceContext();
        foreach (var enemy in combat.Enemies.Where(e => e.IsAlive).ToArray())
            await PowerCmd.Apply<StrengthPower>(context,enemy,run.CurrentActIndex + 2,enemy,null);
    }
}
[HarmonyPatch(typeof(Hook), nameof(Hook.AfterCombatEnd))]
internal static class BurningEliteRewardPatch
{
    private static void Postfix(IRunState runState, ICombatState? combatState, CombatRoom room, ref Task __result)
    {
        if (room.RoomType == RoomType.Elite && ChapterFour.AtBurningElite(runState)) __result = Award(__result,runState,combatState);
    }
    private static async Task Award(Task native, IRunState run, ICombatState? combat)
    {
        await native;
        if (combat == null || combat.Enemies.Any(e => e.IsAlive)) return;
        foreach (var player in run.Players.Where(player=>player.Character is Satori && player.Creature.IsAlive))
            if (player.GetRelic<SatoriLostHeart>() == null && !roomHasKey(run,player))
                ((CombatRoom)run.CurrentRoom!).AddExtraReward(player,new RelicReward(ModelDb.Relic<SatoriLostHeart>().ToMutable(),player));
    }
    private static bool roomHasKey(IRunState run,Player player) => run.CurrentRoom is CombatRoom room
        && room.ExtraRewards.TryGetValue(player,out var rewards) && rewards.OfType<RelicReward>().Any(r=>r.Relic is SatoriLostHeart);
}
[HarmonyPatch(typeof(NNormalMapPoint), nameof(NNormalMapPoint._Ready))]
internal static class BurningEliteDrawingPatch
{
    private static void Postfix(NNormalMapPoint __instance)
    {
        var run = ChapterFour.Run;
        if (__instance.Point.coord != ChapterFour.BurningElite(run)?.coord) return;
        var container=__instance.GetNode<Control>("%IconContainer");
        var flame=ResourceLoader.Load<PackedScene>("res://SatoriCharacter/scenes/burning_elite.tscn").Instantiate<Control>();
        container.AddChild(flame); container.MoveChild(flame,0);
        var tip=ChapterFour.Text("burningElite.tip");
        tip.Add("Strength",run!.CurrentActIndex+2);
        __instance.TooltipText=tip.GetFormattedText();
    }
}

[HarmonyPatch(typeof(RestSiteOption), nameof(RestSiteOption.Generate))]
internal static class GardenRestOptionsPatch
{
    private static void Postfix(Player player, List<RestSiteOption> __result)
    {
        if (player.RunState.Act is SatoriGarden && player.GetRelic<SatoriPreciousMemory>() == null)
            __result.Add(new SatoriRememberRestOption(player));
        else if (player.Character is Satori && player.RunState.Players.Count==1 && SatoriConfig.EnableChapterPreview && ChapterFour.Eligible(player.RunState) && player.RunState.Acts.Count == 3)
            __result.Add(new SatoriGardenPreviewOption(player));
    }
}
public sealed class SatoriRememberRestOption : CustomRestSiteOption
{
    public SatoriRememberRestOption(Player owner) : base(owner) { }
    public override string OptionId => "SATORI_REMEMBER";
    public override string CustomIconPath => ChapterFour.Art+"precious-memory-big.png";
    public override async Task<bool> OnSelect()
    {
        if (Owner.GetRelic<SatoriPreciousMemory>() == null) await RelicCmd.Obtain<SatoriPreciousMemory>(Owner);
        SceneActors.PlayRest(this,"RestSleep");
        ChapterFourVisuals.ShowMemory();
        return true;
    }
}
public sealed class SatoriGardenPreviewOption : CustomRestSiteOption
{
    public SatoriGardenPreviewOption(Player owner) : base(owner) { }
    public override string OptionId => "SATORI_GARDEN_PREVIEW";
    public override string CustomIconPath => ChapterFour.Art+"lost-heart-big.png";
    public override async Task<bool> OnSelect()
    {
        var key = Owner.GetRelic<SatoriLostHeart>() ?? await RelicCmd.Obtain<SatoriLostHeart>(Owner);
        key.EntranceAccepted = true; key.PreviewPending = true;
        return true;
    }
}
[HarmonyPatch(typeof(NRestSiteRoom), "OnProceedButtonReleased")]
internal static class GardenPreviewProceedPatch
{
    private static bool Prefix()
    {
        var run = ChapterFour.Run;
        if (run?.Players.Count!=1) return true;
        if (ChapterFour.Player(run)?.GetRelic<SatoriLostHeart>() is not { PreviewPending: true } key) return true;
        key.PreviewPending = false;
        ChapterFour.AppendGarden(run!);
        MegaCrit.Sts2.Core.Helpers.TaskHelper.RunSafely(RunManager.Instance.EnterAct(3));
        return false;
    }
}

[HarmonyPatch(typeof(RunManager),nameof(RunManager.ProceedFromTerminalRewardsScreen))]
internal static class GardenFinalCombatProceedPatch
{
    private static bool Prefix(ref Task __result)
    {
        if (ChapterFour.Run?.CurrentRoom is not CombatRoom { Encounter:SatoriKoishiEncounter, IsPreFinished:true }) return true;
        __result=RunManager.Instance.EnterNextAct(); return false;
    }
}
[HarmonyPatch(typeof(AbstractRoom),nameof(AbstractRoom.IsVictoryRoom),MethodType.Getter)]
internal static class GardenVictoryRoomPatch
{
    private static void Postfix(AbstractRoom __instance,ref bool __result)
    {
        if (__instance is EventRoom { CanonicalEvent:SatoriHomecoming }) __result=true;
    }
}
[HarmonyPatch(typeof(AchievementsHelper),nameof(AchievementsHelper.CheckForDefeatedAllEnemiesAchievement))]
internal static class GardenNativeAchievementPatch
{
    // Native optional acts have no official achievement enum entry.
    private static bool Prefix(ActModel act) => act is not SatoriGarden;
}
[HarmonyPatch]
internal static class KoishiRoomIconPatch
{
    private static IEnumerable<System.Reflection.MethodBase> TargetMethods() =>
        new[] { "GetRoomIconPath", "GetRoomIconOutlinePath" }.Select(name=>AccessTools.Method(typeof(ImageHelper),name));
    private static bool Prefix(ModelId? modelId,System.Reflection.MethodBase __originalMethod,ref string __result)
    {
        if (modelId?.Entry != "SATORI_KOISHI_ENCOUNTER") return true;
        __result=ChapterFour.Art+(__originalMethod.Name=="GetRoomIconPath" ? "koishi-map.png" : "koishi-map_outline.png");
        return false;
    }
}

