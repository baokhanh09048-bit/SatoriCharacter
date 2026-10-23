using System.Reflection;
using System.Reflection.Emit;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.RestSite;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.RestSite;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;
using MegaCrit.Sts2.Core.Nodes.Screens.Shops;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Runs;

namespace SatoriCharacter;


internal static class SceneActors
{
    internal const string Name = "SatoriSceneActor";
    internal static Node2D? Attach(Node parent, string context, Vector2 position, float scale)
    {
        try
        {
            if (parent.GetNodeOrNull<Node2D>(Name) is {} existing) return existing;
            var scene = ResourceLoader.Load<PackedScene>(Entry.ActorPath);
            if (scene == null) return null;
            var actor = scene.Instantiate<Node2D>();
            actor.SetMeta("satori_character_id", Entry.TargetCharacterId);
            actor.Name = Name;
            actor.Set("scene_context", context);
            actor.Set("display_scale", scale);
            actor.Position = position;
            // Keep Spine nodes alive for the game's own callbacks, only hide their drawing.
            foreach (var child in parent.GetChildren())
                if (child.GetClass() == "SpineSprite" && child is CanvasItem drawing) drawing.Hide();
            parent.AddChild(actor);
            return actor;
        }
        catch (Exception e) { Log.Warn($"SatoriCharacter: {context} drawing unavailable: {e.Message}"); return null; }
    }
    internal static void Play(Node? parent, string trigger)
    {
        var actor = parent?.GetNodeOrNull<Node2D>(Name);
        if (actor != null && GodotObject.IsInstanceValid(actor) && actor.IsNodeReady())
            actor.Call("satori_play_trigger", trigger);
    }
    internal static void PlayRest(RestSiteOption option, string trigger)
    {
        var player = AccessTools.Property(typeof(RestSiteOption), "Owner").GetValue(option) as Player;
        if (player != null && Entry.IsTarget(player.Character))
            Play(NRestSiteRoom.Instance?.GetCharacterForPlayer(player), trigger);
    }
}

[HarmonyPatch(typeof(NRestSiteCharacter), nameof(NRestSiteCharacter._Ready))]
internal static class RestDrawingPatch
{
    // Native _Ready switches on indices 0..2 and throws before connecting the
    // hitbox in a fourth act. Reuse its Glory pose only for this garden.
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var replacement=AccessTools.Method(typeof(RestDrawingPatch),nameof(RestVisualActIndex));
        foreach (var instruction in instructions)
        {
            if (instruction.operand is MethodInfo method && method.Name=="get_CurrentActIndex" && method.ReturnType==typeof(int))
            {
                var replaced=new CodeInstruction(instruction) { opcode=OpCodes.Call,operand=replacement };
                yield return replaced;
            }
            else yield return instruction;
        }
    }
    private static int RestVisualActIndex(IRunState run) => run.Act is SatoriGarden ? 2 : run.CurrentActIndex;
    private static void Postfix(NRestSiteCharacter __instance)
    {
        if (Entry.IsTarget(__instance.Player.Character))
            SceneActors.Attach(__instance, "rest", new Vector2(-25, 255), .72f);
    }
}
[HarmonyPatch(typeof(NRestSiteCharacter), nameof(NRestSiteCharacter.FlipX))]
internal static class RestFlipPatch
{
    private static void Postfix(NRestSiteCharacter __instance)
    {
        if (!Entry.IsTarget(__instance.Player.Character)) return;
        if (__instance.GetNodeOrNull<Node2D>(SceneActors.Name) is not {} actor) return;
        actor.Scale = new Vector2(-actor.Scale.X, actor.Scale.Y);
        actor.Position = new Vector2(-actor.Position.X, actor.Position.Y);
    }
}
[HarmonyPatch(typeof(HealRestSiteOption), nameof(HealRestSiteOption.DoLocalPostSelectVfx))]
internal static class RestSleepPatch
{
    private static void Prefix(HealRestSiteOption __instance) => SceneActors.PlayRest(__instance, "RestSleep");
}
[HarmonyPatch(typeof(SmithRestSiteOption), nameof(SmithRestSiteOption.DoLocalPostSelectVfx))]
internal static class RestUpgradePatch
{
    private static void Prefix(SmithRestSiteOption __instance) => SceneActors.PlayRest(__instance, "Upgrade");
}
[HarmonyPatch(typeof(NRestSiteRoom), "OnProceedButtonReleased")]
internal static class RestExitPatch
{
    private static void Postfix(NRestSiteRoom __instance)
    {
        foreach (var character in __instance.Characters)
            if (Entry.IsTarget(character.Player.Character)) SceneActors.Play(character, "RestExit");
    }
}
[HarmonyPatch(typeof(NMerchantRoom), "AfterRoomIsLoaded")]
internal static class MerchantDrawingPatch
{
    private static void Postfix(List<Player> ____players, List<NMerchantCharacter> ____playerVisuals)
    {
        for (var i = 0; i < Math.Min(____players.Count, ____playerVisuals.Count); i++)
            if (Entry.IsTarget(____players[i].Character))
                SceneActors.Attach(____playerVisuals[i], "merchant", Vector2.Zero, .43f);
    }
}
// Opening the purchase overlay keeps the existing actor and animation position.
// The merchant entrance clip is played only when its room actor is attached.
[HarmonyPatch]
internal static class VictoryDrawingPatch
{
    // The public-beta build added a second overload that takes CombatTurnState.
    // Resolve the original zero-argument lifecycle method explicitly. It is
    // public in stable and nonpublic in beta, so include both visibility flags.
    private static MethodBase TargetMethod() => typeof(CombatManager).GetMethod(
        "EndCombatInternal",
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
        binder: null,
        types: Type.EmptyTypes,
        modifiers: null)
        ?? throw new MissingMethodException(typeof(CombatManager).FullName, "EndCombatInternal()");

    private static void Prefix()
    {
        var room = NCombatRoom.Instance;
        if (room == null || !GodotObject.IsInstanceValid(room)) return;
        foreach (var actor in room.GetTree().GetNodesInGroup("satori_battle"))
            if (GodotObject.IsInstanceValid(actor) && actor.IsNodeReady()
                && actor.GetMeta("satori_character_id", "").AsString() == Entry.TargetCharacterId)
                actor.Call("satori_play_trigger", "Victory");
    }
}
[HarmonyPatch]
internal static class BlockDrawingPatch
{
    private static MethodBase TargetMethod() => AccessTools.GetDeclaredMethods(typeof(CreatureCmd))
        .Single(m => m.Name == "GainBlock" && m.GetParameters().Length == 5 && m.GetParameters()[1].ParameterType == typeof(decimal));
    private static void Prefix(Creature creature, decimal amount)
    {
        if (SatoriCardAnimations.SuppressesAction(creature)) return;
        if (amount > 0 && creature.IsAlive && Entry.IsTarget(creature.Player?.Character)
            && NCombatRoom.Instance?.GetCreatureNode(creature) is {} node)
            Entry.PlayActorTrigger(node, "Defend");
    }
}
