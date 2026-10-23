using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace SatoriCharacter;

// Use native text tips and CardHoverTip so inspection works without a run/owner.
internal static class MemoryHoverTips
{
    internal static IHoverTip Memory => new HoverTip(new LocString("cards","SATORI_MEMORY.title"),new LocString("cards","SATORI_MEMORY.description"));
    internal static IHoverTip ReadMind => new HoverTip(new LocString("cards","SATORI_MIRROR.title"),new LocString("cards","SATORI_MIRROR.description"));

    internal static Type[] RelatedPowers(PowerModel power) => power switch {
        SatoriWhisperPower or SatoriStaticPower or SatoriFeedbackPower or SatoriNightmarePower or SatoriDeepSeaPower => new[]{typeof(SatoriMentalLoadPower)},
        SatoriRedStringsPower or SatoriPuppetTheaterPower or SatoriDreadAuraPower or SatoriCalmPower => new[]{typeof(SatoriSuggestionPower)},
        SatoriKingDisdainPower => new[]{typeof(SatoriSuggestionPower),typeof(SatoriMentalLoadPower)},
        SatoriTemporaryFocusPower or SatoriFocusPower or SatoriPerfectMemoryPower => new[]{typeof(SatoriEchoPower)},
        SatoriEchoPower => new[]{typeof(StrengthPower),typeof(WeakPower),typeof(VulnerablePower)},
        _ => Array.Empty<Type>()
    };
    internal static bool MentionsMemory(PowerModel power) => power is SatoriEchoPower or SatoriTemporaryFocusPower or SatoriFocusPower
        or SatoriMirrorHallPower or SatoriTeaMemoryPower or SatoriMindSovereignPower or SatoriDreamStudyPower or SatoriResonancePower;
    internal static bool ReadsMind(PowerModel power) => power is SatoriMirrorHallPower or SatoriTeaMemoryPower or SatoriMindSovereignPower;
    internal static IEnumerable<IHoverTip> ForPower(PowerModel power)
    {
        foreach(var type in RelatedPowers(power))
            yield return HoverTipFactory.FromPower(ModelDb.GetById<PowerModel>(ModelDb.GetId(type)));
        if(power is SatoriResonancePower or SatoriFeedbackPower)
            yield return HoverTipFactory.Static(StaticHoverTip.Block);
        if(MentionsMemory(power)) yield return Memory;
        if(ReadsMind(power))
        {
            yield return ReadMind;
            foreach(var preview in Previews()) yield return preview;
        }
    }
    internal static IEnumerable<IHoverTip> Previews(string category="EachFree",bool upgraded=false)
    {
        if(category is "EachFree" or "Attack") yield return HoverTipFactory.FromCard<SatoriAttackMemory>(upgraded);
        if(category is "EachFree" or "Defense") yield return HoverTipFactory.FromCard<SatoriDefenseMemory>(upgraded);
        if(category is "EachFree" or "Debuff") yield return HoverTipFactory.FromCard<SatoriDebuffMemory>(upgraded);
        if(category is "EachFree" or "Buff") yield return HoverTipFactory.FromCard<SatoriBuffMemory>(upgraded);
    }
}
