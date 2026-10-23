using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Saves.Managers;

namespace SatoriCharacter;

// These leaf checks grant only vanilla character epochs and reject custom
// characters. Keep the surrounding native victory statistics/rewards flow intact.
[HarmonyPatch]
internal static class SatoriVanillaEpochChecks
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        foreach(string name in new[]{"CheckFifteenElitesDefeatedEpoch","CheckFifteenBossesDefeatedEpoch","ObtainCharUnlockEpoch"})
            yield return AccessTools.Method(typeof(ProgressSaveManager),name);
    }
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(Player localPlayer)=>localPlayer.Character is not Satori;
}
