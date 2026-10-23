using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace SatoriCharacter;

// STS2's public-beta branch has moved several card/damage hooks forward while
// the main branch still uses their older signatures. Resolve those entry points
// at runtime so the same Workshop DLL can serve both branches.
internal static class Sts2Compat
{
    // The parameters stayed the same, but beta changed Task to
    // Task<CardPileAddResult?>. The return type is part of a CLR method reference,
    // so a direct call compiled for either branch can fail on the other branch.
    private static readonly Lazy<MethodInfo> ExhaustMethod = new(() =>
        typeof(CardCmd).GetMethod("Exhaust", BindingFlags.Public | BindingFlags.Static,
            binder: null,
            types: new[] { typeof(PlayerChoiceContext), typeof(CardModel), typeof(bool), typeof(bool) },
            modifiers: null)
        ?? throw new MissingMethodException(typeof(CardCmd).FullName, "Exhaust(PlayerChoiceContext, CardModel, bool, bool)"));

    internal static Task Exhaust(PlayerChoiceContext context, CardModel card,
        bool causedByEthereal = false, bool skipVisuals = false)
        => ExhaustMethod.Value.Invoke(null, new object?[] { context, card, causedByEthereal, skipVisuals }) as Task
           ?? throw new InvalidOperationException("CardCmd.Exhaust did not return an awaitable Task.");

    internal static AttackCommand FromCardCompat(this AttackCommand command, CardModel card, CardPlay? cardPlay)
    {
        var methods = command.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Where(method => method.Name == "FromCard")
            .Select(method => (Method: method, Parameters: method.GetParameters()))
            .Where(candidate => candidate.Parameters.Length is 1 or 2
                && candidate.Parameters[0].ParameterType.IsAssignableFrom(card.GetType()))
            .OrderByDescending(candidate => candidate.Parameters.Length == 2
                && candidate.Parameters[1].ParameterType == typeof(CardPlay))
            .ToArray();
        if (methods.Length == 0)
            throw new MissingMethodException(command.GetType().FullName, "FromCard(CardModel[, CardPlay])");

        var selected = methods[0];
        var arguments = selected.Parameters.Length == 1
            ? new object?[] { card }
            : new object?[] { card, cardPlay };
        return selected.Method.Invoke(command, arguments) as AttackCommand ?? command;
    }

    internal static Task Damage(
        PlayerChoiceContext context,
        Creature target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource,
        CardPlay? cardPlay)
    {
        var candidate = typeof(CreatureCmd).GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(method => method.Name == "Damage" && typeof(Task).IsAssignableFrom(method.ReturnType))
            .Select(method => (Method: method, Parameters: method.GetParameters()))
            .Where(candidate => candidate.Parameters.Length is >= 4 and <= 7
                && candidate.Parameters[0].ParameterType == typeof(PlayerChoiceContext)
                && candidate.Parameters[1].ParameterType == typeof(Creature)
                && candidate.Parameters[2].ParameterType == typeof(decimal)
                && candidate.Parameters[3].ParameterType == typeof(ValueProp)
                && candidate.Parameters.Skip(4).All(parameter =>
                    parameter.ParameterType == typeof(Creature)
                    || parameter.ParameterType == typeof(CardModel)
                    || parameter.ParameterType == typeof(CardPlay)))
            .Select(candidate => (candidate.Method, candidate.Parameters, Score: Score(candidate.Parameters, dealer, cardSource, cardPlay)))
            .OrderByDescending(candidate => candidate.Score)
            .ThenByDescending(candidate => candidate.Parameters.Length)
            .FirstOrDefault();

        if (candidate.Method is null)
            throw new MissingMethodException(typeof(CreatureCmd).FullName, "Damage(PlayerChoiceContext, Creature, decimal, ValueProp, ...)");

        var args = new object?[candidate.Parameters.Length];
        args[0] = context;
        args[1] = target;
        args[2] = amount;
        args[3] = props;
        for (var i = 4; i < args.Length; i++)
        {
            var type = candidate.Parameters[i].ParameterType;
            args[i] = type == typeof(Creature) ? dealer
                : type == typeof(CardModel) ? cardSource
                : type == typeof(CardPlay) ? cardPlay
                : null;
        }

        return (Task)candidate.Method.Invoke(null, args)!;
    }

    private static int Score(ParameterInfo[] parameters, Creature? dealer, CardModel? cardSource, CardPlay? cardPlay)
    {
        var score = 0;
        if (dealer is not null && parameters.Skip(4).Any(parameter => parameter.ParameterType == typeof(Creature))) score += 4;
        if (cardSource is not null && parameters.Skip(4).Any(parameter => parameter.ParameterType == typeof(CardModel))) score += 3;
        if (cardPlay is not null && parameters.Skip(4).Any(parameter => parameter.ParameterType == typeof(CardPlay))) score += 2;
        return score;
    }
}

[HarmonyPatch]
internal static class SatoriDamageModifiersCompat
{
    private static MethodBase TargetMethod() => AccessTools.GetDeclaredMethods(typeof(AbstractModel))
        .Single(method => method.Name == "ModifyDamageAdditive");

    private static bool Prefix(AbstractModel __instance, object[] __args, ref decimal __result)
    {
        if (__instance is not SatoriPower) return true;
        if (__args.Length < 5 || __args[2] is not ValueProp props) return true;

        var dealer = __args[3] as Creature;
        var cardSource = __args[4] as CardModel;
        if (__instance is SatoriDampenedIntentPower dampened)
        {
            __result = dealer == dampened.Owner && props.IsPoweredAttack() ? -dampened.Amount : 0m;
            return false;
        }
        if (__instance is SatoriEnginePower engine)
        {
            __result = dealer == engine.Owner && props.IsPoweredAttack()
                && engine is SatoriEchoPower && cardSource is SatoriPoolCard { IsMemory: true }
                    ? engine.Amount
                    : 0m;
            return false;
        }
        return true;
    }
}

// Like additive damage, this hook has five arguments on stable and six on beta.
// Patch the actual base hook so either branch invokes the same Discord rule.
[HarmonyPatch]
internal static class SatoriDiscordDamageCompat
{
    private static MethodBase TargetMethod() => AccessTools.GetDeclaredMethods(typeof(AbstractModel))
        .Single(method => method.Name == "ModifyDamageMultiplicative");

    private static bool Prefix(AbstractModel __instance,object[] __args,ref decimal __result)
    {
        if(__instance is not SatoriDiscordPower discord || __args.Length<4 || __args[2] is not ValueProp props) return true;
        __result=discord.AttackDamageMultiplier(__args[0] as Creature,props,__args[3] as Creature);
        return false;
    }
}
