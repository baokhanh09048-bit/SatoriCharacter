using HarmonyLib;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace SatoriCharacter;

[HarmonyPatch(typeof(ActModel), nameof(ActModel.FromSave))]
internal static class GardenSaveCompatibility
{
    private static void Prefix(SerializableActModel save)
    {
        if (save.Id != ModelDb.GetId<SatoriGarden>() || save.SerializableRooms is not {} rooms)
            return;

        // Native JSON omits empty encounter lists, then FromSave enumerates them.
        // The garden deliberately has no normal/elite encounters. Normalize only
        // its omitted collections and let the native loader restore all state.
        rooms.EventIds ??= new();
        rooms.NormalEncounterIds ??= new();
        rooms.EliteEncounterIds ??= new();

        // v0.4.28 and earlier saved a virtual, unassigned entry below the
        // rest site. Promote the existing rest point, preserving all actual
        // room coordinates, connections and visited-room history. Mutate the
        // DTO in place: RunManager may already have retained its reference.
        if (save.SavedMap is { StartingPoint: { PointType: MapPointType.Unassigned } start } map
            && start.Coord == new MapCoord(3,-1)
            && start.ChildCoords is { Count: 1 } children
            && children[0] == new MapCoord(3,0)
            && map.Points.SingleOrDefault(p => p.Coord == children[0]) is { PointType: MapPointType.RestSite } rest)
        {
            map.StartingPoint = rest;
            map.Points.Remove(rest);
        }
    }
}

[HarmonyPatch(typeof(RunState), nameof(RunState.FromSerializable))]
internal static class GardenEntryCheckpointCompatibility
{
    private static void Prefix(SerializableRun save)
    {
        // v0.4.25-29 saved entry healing with a pre-finished MapRoom. Map is
        // not a resumable completed room in the native loader. Normalize only
        // the garden's untouched entry checkpoint, retaining HP and relic props.
        if (save.PreFinishedRoom is { RoomType: RoomType.Map }
            && save.VisitedMapCoords.Count == 0
            && save.CurrentActIndex >= 0 && save.CurrentActIndex < save.Acts.Count
            && save.Acts[save.CurrentActIndex].Id == ModelDb.GetId<SatoriGarden>())
            save.PreFinishedRoom = null;
    }
}
