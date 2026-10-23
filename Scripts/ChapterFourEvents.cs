using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Entities.Cards;

namespace SatoriCharacter;

public abstract class SatoriGardenStory : CustomEventModel
{
    protected SatoriGardenStory() : base(false) { }
    public override string CustomInitialPortraitPath => ChapterFour.Art+"garden-event.png";
    protected EventOption Choice(string name, Func<Task>? action, params IHoverTip[] hoverTips) => new(this,action,
        new LocString("events",Id.Entry+".pages.INITIAL.options."+name+".title"),
        new LocString("events",Id.Entry+".pages.INITIAL.options."+name+".description"),
        Id.Entry+".pages.INITIAL.options."+name,hoverTips);
    protected async Task Finish(string page)
    {
        SetEventFinished(PageDescription(page));
        await SaveManager.Instance.SaveRun(ChapterFour.Run?.CurrentRoom);
    }
}

public sealed class SatoriGardenEntrance : SatoriGardenStory
{
    protected override IReadOnlyList<EventOption> GenerateInitialOptions() => new[] {
        Choice("ENTER",Enter), Choice("LEAVE",Leave) };
    private async Task Enter()
    {
        var key = Owner!.GetRelic<SatoriLostHeart>()!;
        key.EntranceAccepted = true;
        ChapterFour.AppendGarden(ChapterFour.Run!);
        await Finish("ENTER");
    }
    private async Task Leave()
    {
        Owner!.GetRelic<SatoriLostHeart>()!.EntranceDeclined = true;
        await Finish("LEAVE");
    }
    protected override void SetInitialEventState(bool isPreFinished)
    {
        if (Owner!.GetRelic<SatoriLostHeart>() is { EntranceAccepted: true }) SetEventFinished(PageDescription("ENTER"));
        else if (Owner.GetRelic<SatoriLostHeart>() is { EntranceDeclined: true }) SetEventFinished(PageDescription("LEAVE"));
        else base.SetInitialEventState(isPreFinished);
    }
}

public sealed class SatoriGardenVisitors : SatoriGardenStory
{
    protected override IReadOnlyList<EventOption> GenerateInitialOptions() => new[] {
        Choice("OKUU",()=>Join(1,"OKUU"),HoverTipFactory.FromCard<SatoriNuclearAid>()),
        Choice("ORIN",()=>Join(2,"ORIN"),HoverTipFactory.FromCard<SatoriSpiritEscort>()),
        Owner!.GetRelic<SatoriPreciousMemory>() != null
            ? Choice("BOTH",()=>Join(3,"BOTH"),HoverTipFactory.FromCard<SatoriNuclearAid>(),HoverTipFactory.FromCard<SatoriSpiritEscort>())
            : Choice("BOTH_LOCKED",null,HoverTipFactory.FromCard<SatoriNuclearAid>(),HoverTipFactory.FromCard<SatoriSpiritEscort>()),
        Choice("ALONE",()=>Join(4,"ALONE"),AloneCardPreview()) };
    private static IHoverTip AloneCardPreview()
    {
        // Show the actual temporary boon, including its upgraded cost and Innate.
        var card=ModelDb.Card<SatoriPerfectMemory>().ToMutable();
        card.UpgradeInternal(); card.FinalizeUpgradeInternal();
        card.EnergyCost.SetCustomBaseCost(0); card.AddKeyword(CardKeyword.Innate);
        return new CardHoverTip(card);
    }
    private async Task Join(int choice, string page)
    {
        var key = Owner!.GetRelic<SatoriLostHeart>();
        // Console/debug entry into this act also gets its persistent story state.
        key ??= await MegaCrit.Sts2.Core.Commands.RelicCmd.Obtain<SatoriLostHeart>(Owner);
        if (key.CompanionChoice != 0) return;
        key.CompanionChoice = choice;
        await Finish(page);
    }
    protected override void SetInitialEventState(bool isPreFinished)
    {
        int choice = Owner!.GetRelic<SatoriLostHeart>()?.CompanionChoice ?? 0;
        if (choice > 0) SetEventFinished(PageDescription(choice switch { 1=>"OKUU",2=>"ORIN",3=>"BOTH",_=>"ALONE" }));
        else base.SetInitialEventState(isPreFinished);
    }
}

public sealed class SatoriHomecoming : SatoriGardenStory
{
    public override LocString InitialDescription => ChapterFour.Text("ending.empty");
    public override string CustomInitialPortraitPath => ChapterFour.Art+"homecoming-comic.png";
    protected override IReadOnlyList<EventOption> GenerateInitialOptions() => new[] { Choice("HOME",GoHome) };
    private async Task GoHome()
    {
        if(MegaCrit.Sts2.Core.Context.LocalContext.IsMe(Owner)) await ChapterFour.Win();
        SetEventState(ChapterFour.Text("ending.empty"),Array.Empty<EventOption>());
    }
}

