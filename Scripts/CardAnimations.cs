using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace SatoriCharacter;

// Scope follows nested/autoplay effects across awaits without leaking to later turns.
internal static class SatoriCardAnimations
{
    private static readonly AsyncLocal<CardModel?> Current = new();
    internal static bool OwnsAnimation(CardModel card) => Current.Value == card;
    internal static bool SuppressesAction(MegaCrit.Sts2.Core.Entities.Creatures.Creature creature)
        => Current.Value?.Owner.Creature == creature;
    internal static IDisposable Begin(CardModel card, string? action = null, bool animate = true)
    {
        var previous = Current.Value;
        Current.Value = card;
        if (animate && card.Owner.Character is Satori
            && NCombatRoom.Instance?.GetCreatureNode(card.Owner.Creature) is {} node)
            Entry.PlayActorTrigger(node, action ?? Select(card));
        return new Scope(previous);
    }
    private static string Select(CardModel card)
    {
        if (card.Type == CardType.Power) return "PowerUp";
        if (card.Type == CardType.Attack) return "Attack";
        if (card is SatoriPoolCard pool && pool.Spec.Effects.Any(effect => effect.Op is
            "Suggestion" or "SuggestionAll" or "SuggestionNonAttackAll" or "DoubleSuggestionAll"
            or "Load" or "LoadAll" or "LoadAttackAll" or "SkillsToLoad" or "SuggestionToLoadAll"
            or "Detonate" or "DoubleLoad" or "SettleLoad" or "Discord" or "DiscordAll" or "DiscordAbsentAll"
            or "Mirror" or "MirrorAll" or "PoisonFromLoad" or "Generate" or "GenerateUpgraded" or "RecallMemory" or "CopyMemory"
            or "Weak" or "WeakAll" or "Vulnerable" or "VulnerableAll" or "Dampen" or "TempStrengthLoss"
            or "RewriteVulnerable" or "StopAttacks" or "SuggestionStop" or "CommandGuard")) return "Cast";
        return card.GainsBlock ? "Defend" : "Cast";
    }
    private sealed class Scope(CardModel? previous) : IDisposable
    {
        public void Dispose() => Current.Value = previous;
    }
}
