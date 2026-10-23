using HarmonyLib;
using Godot;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Vfx.Utilities;
using MegaCrit.Sts2.Core.Helpers;

namespace SatoriCharacter;

[HarmonyPatch(typeof(EnergyIconHelper),nameof(EnergyIconHelper.GetPath),new[]{typeof(string)})]
internal static class SatoriEnergyPath
{
    private static bool Prefix(string prefix,ref string __result)
    {
        if(prefix!="satori") return true;
        __result="res://SatoriCharacter/assets/ui/energy-cost.svg";return false;
    }
}

// Reuse the engine's counter behavior and hover/energy events while replacing
// all character-specific layers before the node enters the tree.
[HarmonyPatch(typeof(NEnergyCounter),nameof(NEnergyCounter.Create))]
internal static class SatoriEnergyCounter
{
    private static void Postfix(Player player,NEnergyCounter? __result)
    {
        if(player.Character is not Satori || __result==null) return;
        __result.Name="SatoriEnergyCounter";
        var layers=__result.GetNode<Control>("Layers");
        var eye=layers.GetNode<TextureRect>("Layer1");
        eye.Texture=ResourceLoader.Load<Texture2D>(Assets.EnergyCounterArt);
        var rotation=layers.GetNode<Control>("RotationLayers");
        foreach(var child in rotation.GetChildren()) child.Free();
        foreach(var child in layers.GetChildren())
            if(child!=eye && child!=rotation) child.Free();
        foreach(var name in new[]{"EnergyVfxBack","EnergyVfxFront"})
        {
            var vfx=__result.GetNode<NParticlesContainer>(name);
            foreach(var child in vfx.GetChildren()) child.Free();
            AccessTools.Field(typeof(NParticlesContainer),"_particles").SetValue(vfx,new Godot.Collections.Array<GpuParticles2D>());
        }
        var label=__result.GetNode<Label>("Label");
        label.AddThemeConstantOverride("outline_size",6);
        label.AddThemeConstantOverride("shadow_outline_size",6);
    }
}
