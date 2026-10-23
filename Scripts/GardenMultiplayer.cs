using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Singleton;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;

namespace SatoriCharacter;

// Native scaling only accepts indices 0..2. Use the third-act boss factor
// specifically for our fourth-act encounter, including HP and enemy block.
[HarmonyPatch(typeof(MultiplayerScalingModel),nameof(MultiplayerScalingModel.GetMultiplayerScaling))]
internal static class GardenMultiplayerScalingPatch
{
    private static bool Prefix(EncounterModel? encounter,int actIndex,ref decimal __result)
    {
        if(actIndex!=3 || encounter is not SatoriKoishiEncounter) return true;
        __result=1.3m;return false;
    }
}

[HarmonyPatch(typeof(Hook),nameof(Hook.AfterActEntered))]
internal static class GardenPartyEntryPatch
{
    private static void Postfix(IRunState runState,ref Task __result)
    {
        if(runState.Act is SatoriGarden) __result=Finish(__result,runState);
    }
    private static async Task Finish(Task native,IRunState run)
    {
        await native;
        // Grant personal story state after the native listener enumeration.
        // Every class gets its own choices, memory and one-time entrance heal.
        bool changed=false;
        foreach(var player in run.Players)
        {
            var key=player.GetRelic<SatoriLostHeart>();
            if(key==null) {key=await RelicCmd.Obtain<SatoriLostHeart>(player);changed=true;}
            changed|=!key.EntranceAccepted;
            key.EntranceAccepted=true;
            await key.AfterActEntered();
        }
        // A dead player does not heal, but their new story state still belongs
        // in the entrance checkpoint even when they are last in roster order.
        if(changed)await SaveManager.Instance.SaveRun(null,saveProgress:false);
    }
}
