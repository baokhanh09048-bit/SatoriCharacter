using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace SatoriCharacter;

// DamageVar/BlockVar use native preview hooks. Weak and temporary Strength need
// the same live Heart Focus preview, without changing their underlying base value.
internal sealed class SatoriMemoryVar(string name,decimal value) : DynamicVar(name,value)
{
    public override void UpdateCardPreview(CardModel card,CardPreviewMode previewMode,Creature? target,bool runGlobalHooks)
        => PreviewValue=BaseValue+(runGlobalHooks?decimal.Floor(card.Owner.Creature.GetPowerAmount<SatoriEchoPower>()/2m):0);
}
