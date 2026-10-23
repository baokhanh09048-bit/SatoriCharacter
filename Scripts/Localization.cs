using HarmonyLib;
using MegaCrit.Sts2.Core.Localization;
namespace SatoriCharacter;
[HarmonyPatch(typeof(LocString), nameof(LocString.GetRawText))]
internal static class Localization
{
    private static readonly Dictionary<string,string> Text = new(StringComparer.OrdinalIgnoreCase)
    {
        ["characters:SATORI.title"]="古明地觉",
        ["characters:SATORI.titleObject"]="古明地觉",
        ["characters:SATORI.description"]="地灵殿的主人，能够读取心声。\n以低语牵动敌意，将心象化作弹幕，让杂念压垮对手。\n操纵、复写与精神过载，共同组成她的专属卡池。",
        ["characters:SATORI.pronounObject"]="她",
        ["characters:SATORI.possessiveAdjective"]="她的",
        ["characters:SATORI.pronounPossessive"]="她的",
        ["characters:SATORI.pronounSubject"]="她",
        ["characters:SATORI.cardsModifierTitle"]="古明地觉牌",
        ["characters:SATORI.cardsModifierDescription"]="92张专属牌。暗示、记忆与精神过载的心灵弹幕。",
        ["characters:SATORI.eventDeathPrevention"]="我已经听见了那一瞬间的心声。",
        ["characters:SATORI.unlockText"]="默认解锁。",
        ["cards:SATORI_STRIKE.title"]="打击",
        ["cards:SATORI_STRIKE.description"]="造成{Damage:diff()}点伤害。",
        ["cards:SATORI_DEFEND.title"]="防御",
        ["cards:SATORI_DEFEND.description"]="获得{Block:diff()}点格挡。",
        ["relics:THIRD_EYE.title"]="第三只眼",
        ["relics:THIRD_EYE.description"]="前2个回合开始时，对随机敌人取念。",
        ["relics:THIRD_EYE.flavor"]="尚未说出口的念头，已经映入眼中。"
    };
    private static bool Prefix(LocString __instance,ref string __result)
    {
        if (LocManager.Instance.Language is not ("zhs" or "zht")) return true;
        string key=$"{__instance.LocTable}:{__instance.LocEntryKey}";
        if (!DialogueStrings.All.TryGetValue(key,out var value) && !RelicStrings.All.TryGetValue(key,out value) && !CardStrings.All.TryGetValue(key,out value) && !Text.TryGetValue(key,out value)) return true;
        __result=value;
        return false;
    }
}
