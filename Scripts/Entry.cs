using Godot;
using BaseLib.Config;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace SatoriCharacter;
[ModInitializer(nameof(Init))]
public static class Entry
{
    public const string TargetCharacterId = "satori";
    public const string ActorPath = "res://SatoriCharacter/scenes/satori_actor.tscn";
    private static readonly HashSet<string> ObservedTriggers = new();
    public static void Init()
    {
        if (!ResourceLoader.Exists(ActorPath))
            throw new InvalidOperationException("SatoriCharacter.pck is incomplete: bundled actor scene is missing.");
        ModConfigRegistry.Register("SatoriCharacter", new SatoriConfig());
        new Harmony("llxs.SatoriCharacter").PatchAll(typeof(Entry).Assembly);
        // The native ModelDb.Init discovers public concrete models in mod assemblies.
        // Constructing canonical models before that pass would create duplicates.
        Log.Info("SatoriCharacter 0.4.34: multiplayer garden, shared party Heart Knot progress, personal companion resources, selected card buffs and Discord damage reduction.");
    }
    public static void CustomizeSelectionBackground(Control background)
    {
        if (background.GetNodeOrNull<TextureRect>("TextureRect") is {} picture)
        {
            picture.Texture = ResourceLoader.Load<Texture2D>(Assets.ArtRoot+"selection-keyart.png");
            picture.Material = null;
        }
        foreach (var child in background.GetChildren())
            if (child is CpuParticles2D particles) particles.Hide();
    }
    public static bool IsTarget(CharacterModel? character) => character is Satori;
    public static void PlayActorTrigger(NCreature creature, string trigger)
    {
        if (!IsTarget(creature.Entity.Player?.Character)) return;
        var body = creature.Visuals?.GetNodeOrNull<Node2D>("%Visuals");
        if (body != null && GodotObject.IsInstanceValid(body) && body.IsNodeReady() && body.HasMethod("satori_play_trigger"))
        {
            body.Call("satori_play_trigger",trigger);
            if (ObservedTriggers.Add(trigger)) Log.Info($"SatoriCharacter: battle trigger delivered: {trigger}");
        }
    }
}

