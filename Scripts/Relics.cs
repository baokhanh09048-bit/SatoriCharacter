using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.ValueProps;

namespace SatoriCharacter;

public abstract class SatoriRelic : RelicModel
{
    private string IconRoot=>this is ThirdEye ? Assets.ArtRoot+"third-eye" : "res://SatoriCharacter/assets/relics/"+GetType().Name;
    public override string PackedIconPath=>IconRoot+".png";
    protected override string PackedIconOutlinePath=>IconRoot+"-outline.png";
    protected override string BigIconPath=>IconRoot+"-big.png";
}
public abstract class SatoriEyeRelic : SatoriRelic
{
    private int _lastTurn=-1;
    protected abstract bool AllEnemies {get;}
    protected override IEnumerable<IHoverTip> ExtraHoverTips=>new IHoverTip[]{new HoverTip(new LocString("cards","SATORI_MIRROR.title"),new LocString("cards","SATORI_MIRROR.description"))};
    public override async Task BeforeCombatStart()
    {
        _lastTurn=-1;
        await SatoriRules.Mind(new ThrowingPlayerChoiceContext(),Owner);
    }
    public override async Task AfterPlayerTurnStart(PlayerChoiceContext context,Player player)
    {
        if(player!=Owner || Owner.Creature.CombatState==null) return;
        int turn=Owner.PlayerCombatState!.TurnNumber;
        if(_lastTurn==turn || (!AllEnemies && turn>2)) return;
        _lastTurn=turn;
        var enemies=SatoriRules.Enemies(Owner).ToArray();
        if(enemies.Length==0) return;
        Flash();
        if(AllEnemies) foreach(var enemy in enemies) await SatoriRules.Mirror(context,Owner,enemy,null);
        else await SatoriRules.Mirror(context,Owner,Owner.RunState.Rng.CombatTargets.NextItem(enemies),null);
    }
}
public sealed class ThirdEye : SatoriEyeRelic
{
    public override RelicRarity Rarity=>RelicRarity.Starter;
    protected override bool AllEnemies=>false;
}
public sealed class SatoriOpenThirdEye : SatoriEyeRelic
{
    public override RelicRarity Rarity=>RelicRarity.Ancient;
    protected override bool AllEnemies=>true;
    public override bool IsAllowedInShops=>false;
}
[HarmonyPatch(typeof(TouchOfOrobas),nameof(TouchOfOrobas.GetUpgradedStarterRelic))]
internal static class SatoriOrobasUpgrade
{
    private static bool Prefix(RelicModel starterRelic,ref RelicModel __result)
    {
        if(starterRelic is not ThirdEye) return true;
        __result=ModelDb.Relic<SatoriOpenThirdEye>();
        return false;
    }
}
public sealed class SatoriBookmark : SatoriRelic
{
    public override RelicRarity Rarity=>RelicRarity.Common;
    private bool _used;
    public override Task BeforeCombatStart() {_used=false;return Task.CompletedTask;}
    public override async Task AfterCardPlayed(PlayerChoiceContext context,CardPlay play)
    {
        if(_used || play.Card.Owner!=Owner || play.Card is not SatoriPoolCard {IsMemory:true}) return;
        _used=true;Flash();
        await CardPileCmd.Draw(context,1,Owner);
    }
}
public sealed class SatoriRedSpool : SatoriRelic
{
    public override RelicRarity Rarity=>RelicRarity.Uncommon;
    public override async Task BeforeCombatStart()
    {
        var context=new ThrowingPlayerChoiceContext();
        Flash();
        foreach(var enemy in SatoriRules.Enemies(Owner)) await SatoriRules.ApplySuggestion(context,enemy,3,Owner.Creature,null);
    }
}
public sealed class SatoriHellEmber : SatoriRelic
{
    public override RelicRarity Rarity=>RelicRarity.Rare;
    private int _lastTurn=-1;
    public override Task BeforeCombatStart() {_lastTurn=-1;return Task.CompletedTask;}
    public override async Task AfterCardPlayed(PlayerChoiceContext context,CardPlay play)
    {
        if(play.Card.Owner!=Owner || play.Card is not SatoriAttackMemory || play.Target is not {IsAlive:true} target) return;
        int turn=Owner.PlayerCombatState!.TurnNumber;
        if(_lastTurn==turn) return;
        _lastTurn=turn;
        int damage=target.GetPowerAmount<SatoriMentalLoadPower>();
        if(damage<=0) return;
        Flash();
        await Sts2Compat.Damage(context,target,damage,ValueProp.Unpowered,Owner.Creature,null,null);
    }
}
public sealed class SatoriTeacup : SatoriRelic
{
    public override RelicRarity Rarity=>RelicRarity.Shop;
    public override async Task BeforeCombatStart()
    {
        Flash();
        await PowerCmd.Apply<SatoriEchoPower>(new ThrowingPlayerChoiceContext(),Owner.Creature,1,Owner.Creature,null);
    }
}

public sealed class SatoriKoishiNote : SatoriRelic
{
    public override RelicRarity Rarity=>RelicRarity.Common;
    protected override IEnumerable<DynamicVar> CanonicalVars=>new[]{new EnergyVar("Energy",1) {ColorPrefix="satori"}};
    private bool _used;
    public override Task BeforeCombatStart() {_used=false;return Task.CompletedTask;}
    public override async Task AfterCardExhausted(PlayerChoiceContext context,CardModel card,bool causedByEthereal)
    {
        if(_used || card.Owner!=Owner || card is not SatoriPoolCard {IsMemory:true}) return;
        _used=true;Flash();await PlayerCmd.GainEnergy(1,Owner);
    }
}
public sealed class SatoriHeartBell : SatoriRelic
{
    public override RelicRarity Rarity=>RelicRarity.Uncommon;
    private int _lastTurn=-1;
    public override Task BeforeCombatStart() {_lastTurn=-1;return Task.CompletedTask;}
    internal async Task OnMirror(PlayerChoiceContext context,MegaCrit.Sts2.Core.Entities.Creatures.Creature target,CardModel? source)
    {
        int turn=Owner.PlayerCombatState!.TurnNumber;
        if(_lastTurn==turn || !target.IsAlive) return;
        _lastTurn=turn;Flash();await SatoriRules.ApplyLoad(context,target,2,Owner.Creature,source);
    }
}
public sealed class SatoriUnconsciousHat : SatoriRelic
{
    public override RelicRarity Rarity=>RelicRarity.Rare;
    protected override IEnumerable<DynamicVar> CanonicalVars=>new[]{new EnergyVar("Energy",1) {ColorPrefix="satori"}};
    private int _lastTurn=-1;
    public override Task BeforeCombatStart() {_lastTurn=-1;return Task.CompletedTask;}
    internal async Task OnSuggestionExhausted(PlayerChoiceContext context)
    {
        int turn=Owner.PlayerCombatState!.TurnNumber;
        if(_lastTurn==turn || !Owner.Creature.IsAlive) return;
        _lastTurn=turn;Flash();await PlayerCmd.GainEnergy(1,Owner);
    }
}
public sealed class SatoriPalaceKey : SatoriRelic
{
    public override RelicRarity Rarity=>RelicRarity.Ancient;
    public override bool IsAllowedInShops=>false;
    protected override IEnumerable<IHoverTip> ExtraHoverTips=>new[]{HoverTipFactory.FromCard(ModelDb.Card<SatoriAncientDream>())};
    public override async Task AfterObtained()
    {
        Flash();await CardPileCmd.Add(Owner.RunState.CreateCard<SatoriAncientDream>(Owner),PileType.Deck);
    }
}

internal static class RelicStrings
{
    internal static readonly Dictionary<string,string> All=new(StringComparer.OrdinalIgnoreCase) {
        ["relics:SATORI_KOISHI_NOTE.title"]="恋的便签",
        ["relics:SATORI_KOISHI_NOTE.description"]="每场战斗首次消耗记忆牌时，获得{Energy:energyIcons()}。",
        ["relics:SATORI_KOISHI_NOTE.flavor"]="纸上只有一句：姐姐，我出去玩了。",
        ["relics:SATORI_HEART_BELL.title"]="心声铃",
        ["relics:SATORI_HEART_BELL.description"]="每回合首次取念时，对该敌人施加2层精神过载。",
        ["relics:SATORI_HEART_BELL.flavor"]="听不见的心声，也许会让它轻轻响一下。",
        ["relics:SATORI_UNCONSCIOUS_HAT.title"]="无意识的帽子",
        ["relics:SATORI_UNCONSCIOUS_HAT.description"]="每回合首次使一名敌人的暗示耗尽时，获得{Energy:energyIcons()}。",
        ["relics:SATORI_UNCONSCIOUS_HAT.flavor"]="帽檐下没有回答，只有一阵刚刚离去的风。",
        ["relics:SATORI_PALACE_KEY.title"]="地灵殿的钥匙",
        ["relics:SATORI_PALACE_KEY.description"]="获得王之蔑视。",
        ["relics:SATORI_PALACE_KEY.flavor"]="特兹卡塔拉为你保留了一扇回家的门。",
        ["relics:THIRD_EYE.title"]="第三只眼",
        ["relics:THIRD_EYE.description"]="前2个回合开始时，对随机敌人取念。",
        ["relics:THIRD_EYE.flavor"]="尚未说出口的念头，已经映入眼中。",
        ["relics:SATORI_OPEN_THIRD_EYE.title"]="心之瞳",
        ["relics:SATORI_OPEN_THIRD_EYE.description"]="每回合开始时，对所有敌人取念。",
        ["relics:SATORI_OPEN_THIRD_EYE.flavor"]="欧罗巴斯的触碰，让所有心声同时涌来。",
        ["relics:SATORI_BOOKMARK.title"]="读心书签",
        ["relics:SATORI_BOOKMARK.description"]="每场战斗首次打出记忆牌时，抽1张牌。",
        ["relics:SATORI_BOOKMARK.flavor"]="记下它，不必急着翻到下一页。",
        ["relics:SATORI_RED_SPOOL.title"]="红线线轴",
        ["relics:SATORI_RED_SPOOL.description"]="战斗开始时，对所有敌人施加3层暗示。",
        ["relics:SATORI_RED_SPOOL.flavor"]="还没有开口，红线已经搭上了心弦。",
        ["relics:SATORI_HELL_EMBER.title"]="旧地狱火种",
        ["relics:SATORI_HELL_EMBER.description"]="每回合首次打出攻念时，额外造成目标精神过载层数的伤害。",
        ["relics:SATORI_HELL_EMBER.flavor"]="火焰只是形状，燃料是积压的念头。",
        ["relics:SATORI_TEACUP.title"]="地灵殿茶杯",
        ["relics:SATORI_TEACUP.description"]="战斗开始时，获得1层心念。",
        ["relics:SATORI_TEACUP.flavor"]="一杯热茶，让杂乱的心声稍稍清晰。"
    };
}
