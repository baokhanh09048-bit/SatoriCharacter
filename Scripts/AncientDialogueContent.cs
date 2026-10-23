using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Ancients;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Events;

namespace SatoriCharacter;

internal static class DialogueStrings
{
    internal static readonly ConcurrentDictionary<string,string> All=new(StringComparer.OrdinalIgnoreCase);
}

internal static class SatoriDialogues
{
    private sealed record Generated(IReadOnlyList<AncientDialogue> Dialogues);
    private static readonly ConditionalWeakTable<AncientDialogueSet,Generated> GeneratedSets=new();

    internal static bool IsGenerated(AncientDialogueSet set,IReadOnlyList<AncientDialogue> dialogues)
        =>GeneratedSets.TryGetValue(set,out var generated) && ReferenceEquals(generated.Dialogues,dialogues);

    internal static void Ensure(AncientDialogueSet set,string ancient,bool refresh=false)
    {
        // BaseLib CustomAncientModel reserves every character key, including
        // empty lists when the ancient has no authored dialogue for that hero.
        if(set.CharacterDialogues.TryGetValue("SATORI",out var existing) && existing.Count>0
            && !(refresh && IsGenerated(set,existing))) return;
        string textAncient=DialogueCatalog.All.ContainsKey(ancient)?ancient:"FALLBACK";
        var pages=DialogueCatalog.All[textAncient];
        var dialogues=new List<AncientDialogue>();
        for(int index=0;index<pages.Length;index++)
        {
            var page=pages[index];
            var dialogue=new AncientDialogue(Enumerable.Repeat("",page.Length).ToArray()) {
                VisitIndex=index<2?index:null,IsRepeating=index>=2,
                EndAttackers=ancient=="THE_ARCHITECT"?ArchitectAttackers.Both:ArchitectAttackers.None
            };
            for(int line=0;line<page.Length;line++)
            {
                var (character,text)=page[line];
                // Reuse exported translations for unknown mod ancients too.
                string prefix=$"{textAncient}.talk.SATORI.{index}-{line}"+(index>=2?"r":"");
                string key=prefix+(character?".char":".ancient");
                DialogueStrings.All["ancients:"+key]=text;
                dialogue.Lines[line].Speaker=character?AncientDialogueSpeaker.Character:AncientDialogueSpeaker.Ancient;
                dialogue.Lines[line].LineText=new LocString("ancients",key);
                if(line<page.Length-1)
                {
                    DialogueStrings.All["ancients:"+prefix+".next"]=character?"继续":"回应";
                    dialogue.Lines[line].NextButtonText=new LocString("ancients",prefix+".next");
                }
            }
            dialogues.Add(dialogue);
        }
        set.CharacterDialogues["SATORI"]=dialogues;
        GeneratedSets.Remove(set);
        GeneratedSets.Add(set,new Generated(dialogues));
    }
}

[HarmonyPatch(typeof(AncientDialogueSet),nameof(AncientDialogueSet.PopulateLocKeys))]
internal static class SatoriDialogueLocalizationRegistration
{
    // Run after native/BaseLib localization, so it cannot replace FALLBACK
    // keys with nonexistent keys belonging to the third-party ancient.
    [HarmonyPriority(Priority.Last)]
    private static void Postfix(AncientDialogueSet __instance,string ancientEntry)
        =>SatoriDialogues.Ensure(__instance,ancientEntry,refresh:true);
}

[HarmonyPatch(typeof(AncientEventModel),nameof(AncientEventModel.DialogueSet),MethodType.Getter)]
internal static class SatoriAncientDialogueRegistration
{
    private static void Postfix(AncientEventModel __instance,AncientDialogueSet __result)=>SatoriDialogues.Ensure(__result,__instance.Id.Entry);
}
[HarmonyPatch(typeof(TheArchitect),nameof(TheArchitect.DialogueSet),MethodType.Getter)]
internal static class SatoriArchitectDialogueRegistration
{
    private static void Postfix(TheArchitect __instance,AncientDialogueSet __result)=>SatoriDialogues.Ensure(__result,__instance.Id.Entry);
}

[HarmonyPatch(typeof(AncientDialogueSet),nameof(AncientDialogueSet.GetValidDialogues))]
internal static class SatoriDialogueVisits
{
    private static bool Prefix(AncientDialogueSet __instance,ModelId characterId,int charVisits,ref IEnumerable<AncientDialogue> __result)
    {
        if(characterId.Entry!="SATORI") return true;
        if(!__instance.CharacterDialogues.TryGetValue("SATORI",out var own) || own.Count==0)
        {
            SatoriDialogues.Ensure(__instance,"FALLBACK");
            own=__instance.CharacterDialogues["SATORI"];
        }
        // Keep third-party authored conversations on the native selector,
        // including their first-visit, agnostic and repeating dialogue rules.
        if(!SatoriDialogues.IsGenerated(__instance,own)) return true;
        var exact=own.Where(d=>d.VisitIndex==charVisits).ToArray();
        __result=exact.Length>0?exact:own.Where(d=>d.IsRepeating && (d.VisitIndex==null || charVisits>=d.VisitIndex)).ToArray();
        return false;
    }
}

[HarmonyPatch(typeof(LocString),nameof(LocString.Exists),new[]{typeof(string),typeof(string)})]
internal static class SatoriDialogueExists
{
    private static bool Prefix(string __0,string __1,ref bool __result)
    {
        if(!DialogueStrings.All.ContainsKey(__0+":"+__1)) return true;
        __result=true;return false;
    }
}
