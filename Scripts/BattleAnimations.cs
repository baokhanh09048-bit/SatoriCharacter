using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace SatoriCharacter;

[HarmonyPatch(typeof(CharacterModel), nameof(CharacterModel.CreateVisuals))]
internal static class BattleVisualPatch
{
    private static void Postfix(CharacterModel __instance, NCreatureVisuals __result)
    {
        if (!Entry.IsTarget(__instance)) return;
        try
        {
            ReplaceBody(__result, Entry.ActorPath, true);
        }
        catch (Exception exception)
        {
            Log.Error($"SatoriCharacter: battle body replacement failed: {exception.Message}");
        }
    }

    internal static void ReplaceBody(NCreatureVisuals __result, string path, bool satori)
    {
            var scene = ResourceLoader.Load<PackedScene>(path);
            if (scene == null) return;
            var original = FindNode(__result, "Visuals") as Node2D;
            if (original == null)
            {
                Log.Warn("SatoriCharacter: original Visuals node missing; retaining original model.");
                return;
            }

            // SkinChanger may replay this visual hook on an already replaced body.
            if (original.HasMethod("satori_play_trigger") || original.HasMethod("garden_trigger")) return;

            // Keep the game's native visuals container, markers and form-effect anchors.
            // Replace only its drawing body before NCreatureVisuals._Ready caches it.
            var actor = scene.Instantiate<Node2D>();
            if (satori) actor.SetMeta("satori_character_id", Entry.TargetCharacterId);
            else { actor.Set("character_key","koishi"); actor.Set("display_height",340f); }
            var parent = original.GetParent();
            var childIndex = original.GetIndex();
            var owner = original.Owner;
            var unique = original.UniqueNameInOwner;
            var bodyName = original.Name;
            parent.RemoveChild(original);
            actor.Name = bodyName;
            parent.AddChild(actor);
            parent.MoveChild(actor, childIndex);
            actor.Owner = owner != null && owner.IsAncestorOf(actor) ? owner : __result;
            // Godot's unique-path bookkeeping needs nodes inside the scene tree.
            // TreeEntered runs before NCreatureVisuals._Ready resolves %Visuals.
            if (unique)
            {
                if (actor.IsInsideTree()) actor.UniqueNameInOwner = true;
                else actor.TreeEntered += () => actor.UniqueNameInOwner = true;
            }
            original.Free();

            if (FindNode(__result, "Bounds") is Control bounds)
            {
                bounds.Position = new Vector2(-112, -324);
                bounds.Size = new Vector2(224, 324);
            }
            if (FindNode(__result, "CenterPos") is Node2D center)
                center.Position = new Vector2(0, -175);
    }

    private static Node? FindNode(Node root, string name)
    {
        if (root.Name.ToString() == name) return root;
        foreach (var child in root.GetChildren())
            if (FindNode(child, name) is { } match) return match;
        return null;
    }
}

[HarmonyPatch(typeof(NCreature), nameof(NCreature.SetAnimationTrigger))]
internal static class AnimationTriggerPatch
{
    private static void Postfix(NCreature __instance, string trigger)
    {
        if (SatoriCardAnimations.SuppressesAction(__instance.Entity)
            && trigger is "Attack" or "Cast" or "PowerUp" or "Shiv" or "Defend") return;
        // An actor capability check prevents the patch from animating original/other skins.
        Entry.PlayActorTrigger(__instance, trigger);
    }
}

// The game's non-Spine lifecycle paths skip SetAnimationTrigger; forward visuals only.
[HarmonyPatch(typeof(NCreature), nameof(NCreature.StartDeathAnim))]
internal static class DeathVisualPatch
{
    private static void Postfix(NCreature __instance)
    {
        if (!__instance.HasSpineAnimation) Entry.PlayActorTrigger(__instance, "Dead");
    }
}

[HarmonyPatch(typeof(NCreature), nameof(NCreature.StartReviveAnim))]
internal static class ReviveVisualPatch
{
    private static void Postfix(NCreature __instance)
    {
        if (!__instance.HasSpineAnimation) Entry.PlayActorTrigger(__instance, "Revive");
    }
}

[HarmonyPatch(typeof(NCreature), "ImmediatelySetIdle")]
internal static class ImmediateIdleVisualPatch
{
    private static void Postfix(NCreature __instance)
    {
        if (!__instance.HasSpineAnimation && __instance.Entity.IsAlive)
            Entry.PlayActorTrigger(__instance, "Idle");
    }
}

[HarmonyPatch(typeof(NCreature), nameof(NCreature._Ready))]
internal static class LoadedCreatureVisualPatch
{
    private static void Postfix(NCreature __instance)
    {
        if (!__instance.HasSpineAnimation && __instance.Entity.IsDead)
            Entry.PlayActorTrigger(__instance, "Dead");
    }
}
