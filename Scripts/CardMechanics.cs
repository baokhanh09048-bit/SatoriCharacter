using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.ValueProps;

namespace SatoriCharacter;

internal sealed record Effect(string Op,decimal Value,decimal Upgrade,string Arg="",string When="",string Scale="",int Cap=0);
internal sealed record CardSpec(string Key,string Title,int Cost,int UpgradeCost,CardType Type,CardRarity Rarity,TargetType Target,string Route,Effect[] Effects,CardKeyword[] Keywords,string Description,CardKeyword[] UpgradeKeywords,CardKeyword[] RemoveKeywords);

// Concrete generated subclasses give every card a stable native model ID. Rules live here.
public abstract class SatoriPoolCard : CardModel
{
    internal CardSpec Spec { get; }
    internal bool IsMemory => Spec.Route == "记忆";
    protected SatoriPoolCard(string key) : this(CardCatalog.ByName[key]) {}
    private SatoriPoolCard(CardSpec spec) : base(spec.Cost,spec.Type,spec.Rarity,spec.Target) { Spec=spec; }
    public override CardPoolModel Pool => ModelDb.CardPool<SatoriCardPool>();
    public override CardPoolModel VisualCardPool => Pool;
    public override string PortraitPath => Assets.CardArtRoot + Spec.Key + ".png";
    public override string BetaPortraitPath => PortraitPath;
    public override IEnumerable<string> AllPortraitPaths => new[] {PortraitPath};
    public override IEnumerable<CardKeyword> CanonicalKeywords => Spec.Keywords;
    protected override bool IsPlayable=>!Spec.Effects.Any(e=>e.Op=="FifthTurn") || IsMutable && Owner?.PlayerCombatState?.TurnNumber==5;
    protected override bool ShouldGlowGoldInternal=>Spec.Effects.Any(e=>e.Op=="FifthTurn") && IsPlayable;
    public override bool GainsBlock => Spec.Effects.Any(e=>e.Op is "Block" or "BlockBoth" or "CommandGuard" or "SuggestionGuard");
    protected override IEnumerable<DynamicVar> CanonicalVars
    {
        get
        {
            foreach(var (effect,i) in Spec.Effects.Select((effect,i)=>(effect,i))) yield return effect.Op switch {
                "Damage" or "LoadStrike" or "IntentStrike" or "MemoryBarrage" or "MemoryVolley" => (DynamicVar)new DamageVar("V"+i,effect.Value,Type==CardType.Attack?ValueProp.Move:ValueProp.Unpowered),
                "Block" or "BlockBoth" or "CommandGuard" => new BlockVar("V"+i,effect.Value,ValueProp.Move),
                "Weak" or "Vulnerable" or "TempStrength" or "Strength" when IsMemory => new SatoriMemoryVar("V"+i,effect.Value),
                "Energy" or "ExhaustStatusForEnergy" => new EnergyVar("V"+i,(int)effect.Value) {ColorPrefix="colorless"},
                "Power" when effect.Arg=="EnergyNextTurnPower" => new EnergyVar("V"+i,(int)effect.Value) {ColorPrefix="colorless"},
                _ => new DynamicVar("V"+i,effect.Value)
            };
            if(Spec.Effects.Any(e=>e.Op=="MemoryBarrage"))
            {
                yield return new CalculationBaseVar(0m);
                yield return new CalculationExtraVar(1m);
                yield return new CalculatedVar("MemoryHits").WithMultiplier(static (card,_)=>card.Owner?.Creature.GetPower<SatoriMindPower>()?.CombatMemoriesPlayed??0);
            }
            if(Spec.Effects.Any(e=>e.Op=="ExhaustMemoryStrike"))
            {
                yield return new CalculationBaseVar(8m);
                yield return new ExtraDamageVar(2m);
                yield return new CalculatedDamageVar(ValueProp.Move).WithMultiplier(static (card,_)=>card.Owner?.PlayerCombatState?.ExhaustPile.Cards.Count(c=>c is SatoriPoolCard {IsMemory:true})??0);
            }
            if(Spec.Effects.FirstOrDefault(e=>e.Op=="SuggestionStrike") is {} suggestionStrike)
            {
                yield return new CalculationBaseVar(decimal.Parse(suggestionStrike.Arg,System.Globalization.CultureInfo.InvariantCulture));
                yield return new ExtraDamageVar(suggestionStrike.Value);
                yield return new CalculatedDamageVar(ValueProp.Move).WithMultiplier(static (card,target)=>target==null?0:SatoriRules.OwnSuggestion(target,card.Owner.Creature)?.Amount??0);
            }
        }
    }
    protected override IEnumerable<IHoverTip> ExtraHoverTips
    {
        get
        {
            bool memory=IsMemory || Spec.Description.Contains("记忆牌",StringComparison.Ordinal)
                || Spec.Effects.Any(e=>e.Op is "Generate" or "GenerateUpgraded" or "RandomUpgradedMemory" or "CopyMemory" or "RecallMemory" or "UpgradeMemories");
            bool readsMind=Spec.Effects.Any(e=>e.Op is "Mirror" or "MirrorAll");
            if(IsMemory) yield return HoverTipFactory.FromPower(ModelDb.Power<SatoriEchoPower>());
            foreach(var power in Spec.Effects.Select(e=>e.Op switch {
                "Suggestion" or "SuggestionAll" or "CommandGuard" or "SuggestionGuard" or "SuggestionStop" or "SuggestionStrike" or "SuggestionNonAttackAll" or "DoubleSuggestionAll" or "SuggestionToLoadAll" => typeof(SatoriSuggestionPower),
                "Load" or "LoadAll" or "Detonate" or "DoubleLoad" or "LoadAttackAll" or "SuggestionToLoadAll" or "LoadStrike" or "SettleLoad" or "SkillsToLoad" or "PoisonFromLoad" => typeof(SatoriMentalLoadPower),
                "TempFocus" => typeof(SatoriEchoPower),
                "Dampen" => typeof(SatoriDampenedIntentPower),
                "Discord" or "DiscordAll" or "DiscordAbsentAll" => typeof(SatoriDiscordPower),
                "Weak" or "WeakAll" or "SelfWeak" => typeof(WeakPower),
                "Vulnerable" or "VulnerableAll" or "RewriteVulnerable" => typeof(VulnerablePower),
                "Strength" or "TempStrength" or "AddStrengthIntent" or "TempStrengthLoss" => typeof(StrengthPower),
                "Power" => SatoriRules.PowerType(e.Arg),
                _ => null
            }).OfType<Type>().Distinct())
            {
                var model=ModelDb.GetById<PowerModel>(ModelDb.GetId(power));
                var related=MemoryHoverTips.RelatedPowers(model);
                // Engine cards already describe their own trigger. Explain the
                // states they produce instead of repeating that trigger at 0.
                if(model is not (SatoriEnginePower or SatoriTemporaryFocusPower or SatoriTeaMemoryPower) || model is SatoriEchoPower)
                    yield return HoverTipFactory.FromPower(model);
                foreach(var type in related)
                    yield return HoverTipFactory.FromPower(ModelDb.GetById<PowerModel>(ModelDb.GetId(type)));
                if(model is SatoriResonancePower or SatoriFeedbackPower)
                    yield return HoverTipFactory.Static(StaticHoverTip.Block);
                memory |= MemoryHoverTips.MentionsMemory(model);
                readsMind |= MemoryHoverTips.ReadsMind(model);
            }
            if(Spec.Effects.Any(e=>e.Op=="PoisonFromLoad")) yield return HoverTipFactory.FromPower(ModelDb.Power<PoisonPower>());
            if(Spec.Effects.Any(e=>e.Op is "Energy" or "ExhaustStatusForEnergy" || e.Op=="Power" && e.Arg=="EnergyNextTurnPower"))
                yield return HoverTipFactory.ForEnergy(this);
            if(Spec.Effects.Any(e=>e.Op is "StopAttacks" or "SuggestionStop"))
            {
                // v0.107's static tip requests a loose .tres absent from its PCK.
                // Resident atlas sprites also survive room-cache disposal.
                if(!AtlasManager.IsAtlasLoaded("intent_atlas")) AtlasManager.LoadAtlas("intent_atlas");
                yield return new HoverTip(new LocString("intents","STUN.title"),new LocString("intents","STUN.description"),AtlasManager.GetSprite("intent_atlas","intent_stun"));
            }
            if(memory || readsMind) yield return MemoryHoverTips.Memory;
            if(readsMind) yield return MemoryHoverTips.ReadMind;
            if(readsMind)
                foreach(var preview in MemoryHoverTips.Previews()) yield return preview;
            foreach(var effect in Spec.Effects.Where(e=>e.Op is "Generate" or "GenerateUpgraded" or "RandomUpgradedMemory"))
                foreach(var preview in MemoryHoverTips.Previews(effect.Op=="RandomUpgradedMemory"?"EachFree":effect.Arg,effect.Op!="Generate"))
                    yield return preview;
        }
    }
    protected override void OnUpgrade()
    {
        for(int i=0;i<Spec.Effects.Length;i++)
        {
            decimal change=Spec.Effects[i].Upgrade-Spec.Effects[i].Value;
            if(change!=0) DynamicVars["V"+i].UpgradeValueBy(change);
        }
        if(Spec.UpgradeCost!=Spec.Cost) EnergyCost.UpgradeBy(Spec.UpgradeCost-Spec.Cost);
        foreach(var keyword in Spec.UpgradeKeywords) AddKeyword(keyword);
        foreach(var keyword in Spec.RemoveKeywords) RemoveKeyword(keyword);
        if(Spec.Effects.Any(e=>e.Op=="ExhaustMemoryStrike"))
        {
            var bonus=Spec.Effects.Single(e=>e.Op=="Bonus");
            DynamicVars.ExtraDamage.UpgradeValueBy(bonus.Upgrade-bonus.Value);
            ((CalculatedVar)DynamicVars["CalculatedDamage"]).RecalculateForUpgradeOrEnchant();
        }
        if(Spec.Effects.FirstOrDefault(e=>e.Op=="SuggestionStrike") is {} suggestionStrike)
        {
            DynamicVars.ExtraDamage.UpgradeValueBy(suggestionStrike.Upgrade-suggestionStrike.Value);
            ((CalculatedVar)DynamicVars["CalculatedDamage"]).RecalculateForUpgradeOrEnchant();
        }
    }
    protected override async Task OnPlay(PlayerChoiceContext choiceContext,CardPlay cardPlay)
    {
        if(Spec.Effects.Any(e=>e.Op=="FifthTurn") && !IsPlayable) return;
        using var animation = SatoriCardAnimations.Begin(this);
        SpellCardPresentation.Play(this);
        var mind=await SatoriRules.Mind(choiceContext,Owner);
        for(int i=0;i<Spec.Effects.Length;i++)
        {
            var effect=Spec.Effects[i];
            if(!SatoriRules.Condition(effect.When,cardPlay.Target,Owner,mind)) continue;
            decimal value=DynamicVars["V"+i].BaseValue;
            switch(effect.Op)
            {
                case "Bonus": break; // An additive scaler for the associated damage effect.
                case "FifthTurn": break;
                case "TempFocus": await PowerCmd.Apply<SatoriTemporaryFocusPower>(choiceContext,Owner.Creature,value,Owner.Creature,this);break;
                case "SkillsToLoad":
                    int skillCount=Owner.PlayerCombatState!.Hand.Cards.Count(c=>c.Type==CardType.Skill && c!=this);
                    for(int n=0;n<skillCount;n++)
                    {
                        var enemies=SatoriRules.Enemies(Owner).ToArray();if(enemies.Length==0) break;
                        await SatoriRules.ApplyLoad(choiceContext,Owner.RunState.Rng.CombatTargets.NextItem(enemies),(int)value,Owner.Creature,this);
                    }
                    break;
                case "ExhaustMemoryStrike":
                    if(cardPlay.Target is {IsAlive:true} leechTarget)
                        await DamageCmd.Attack(((CalculatedVar)DynamicVars["CalculatedDamage"]).Calculate(leechTarget)).FromCardCompat(this,cardPlay).Targeting(leechTarget).Execute(choiceContext);
                    break;
                case "DiscardToTop":
                    var discard=Owner.PlayerCombatState!.DiscardPile.Cards.ToArray();
                    if(discard.Length>0)
                    {
                        var returned=await CardSelectCmd.FromSimpleGrid(choiceContext,discard,Owner,new CardSelectorPrefs(new LocString("cards",Id.Entry+".selectionScreenPrompt"),1));
                        foreach(var card in returned) await CardPileCmd.Add(card,PileType.Draw,CardPilePosition.Top);
                    }
                    break;
                case "ExhaustThenDraw":
                    if(Owner.PlayerCombatState!.Hand.Cards.Any(c=>c!=this))
                    {
                        var consumed=(await CardSelectCmd.FromHand(choiceContext,Owner,new CardSelectorPrefs(CardSelectorPrefs.ExhaustSelectionPrompt,1),c=>c!=this,this)).FirstOrDefault();
                        if(consumed!=null) {await Sts2Compat.Exhaust(choiceContext,consumed);await CardPileCmd.Draw(choiceContext,1,Owner);}
                    }
                    break;
                case "KeepPile":
                    await SatoriPileChoice.Keep(choiceContext,Owner,this);
                    if(value>0) await PowerCmd.Apply<SatoriExtraTurnPower>(choiceContext,Owner.Creature,value,Owner.Creature,this);
                    break;
                case "MemoryVolley":
                    if(cardPlay.Target is {IsAlive:true} volleyTarget)
                        await DamageCmd.Attack(value).FromCardCompat(this,cardPlay).WithHitCount(mind.MemoriesPlayed>0?5:3).OnlyPlayAnimOnce().Targeting(volleyTarget).Execute(choiceContext);
                    break;
                case "MemoryBarrage":
                    int hits=(int)((CalculatedVar)DynamicVars["MemoryHits"]).Calculate(null);
                    if(hits>0) await DamageCmd.Attack(value).FromCardCompat(this,cardPlay).WithHitCount(hits).OnlyPlayAnimOnce().TargetingAllOpponents(Owner.Creature.CombatState!).Execute(choiceContext);
                    break;
                case "LoadStrike":
                    if(cardPlay.Target is {IsAlive:true} loadedTarget)
                    {
                        int bonusIndex=Array.FindIndex(Spec.Effects,e=>e.Op=="Bonus");
                        value+=(loadedTarget.GetPowerAmount<SatoriMentalLoadPower>())*DynamicVars["V"+bonusIndex].BaseValue;
                        await DamageCmd.Attack(value).FromCardCompat(this,cardPlay).Targeting(loadedTarget).Execute(choiceContext);
                    }
                    break;
                case "IntentStrike":
                    if(cardPlay.Target is {IsAlive:true,Monster:not null} intentTarget)
                    {
                        value+=intentTarget.Monster.NextMove.Intents.OfType<AttackIntent>().Sum(intent=>intent.GetTotalDamage(new[]{Owner.Creature},intentTarget));
                        await DamageCmd.Attack(value).FromCardCompat(this,cardPlay).Targeting(intentTarget).Execute(choiceContext);
                    }
                    break;
                case "Damage":
                    if(effect.Scale=="Memory")
                    {
                        int bonusIndex=Array.FindIndex(Spec.Effects,e=>e.Op=="Bonus");
                        value+=(effect.Cap>0?Math.Min(mind.MemoriesPlayed,effect.Cap):mind.MemoriesPlayed)*DynamicVars["V"+bonusIndex].BaseValue;
                    }
                    if(Type==CardType.Attack)
                    {
                        var attack=DamageCmd.Attack(value).FromCardCompat(this,cardPlay).WithHitCount(string.IsNullOrEmpty(effect.Arg)?1:int.Parse(effect.Arg)).OnlyPlayAnimOnce();
                        if(TargetType==TargetType.AllEnemies) attack.TargetingAllOpponents(Owner.Creature.CombatState!);
                        else if(cardPlay.Target is {IsAlive:true} target) attack.Targeting(target);
                        else break;
                        await attack.Execute(choiceContext);
                    }
                    else if(cardPlay.Target is {IsAlive:true} psychicTarget)
                        await Sts2Compat.Damage(choiceContext,psychicTarget,value,ValueProp.Unpowered,Owner.Creature,this,cardPlay);
                    break;
                case "Block": await CreatureCmd.GainBlock(Owner.Creature,value,ValueProp.Move,cardPlay); break;
                case "BlockBoth":
                    await CreatureCmd.GainBlock(Owner.Creature,value,ValueProp.Move,cardPlay);
                    foreach(var enemy in SatoriRules.Enemies(Owner)) await CreatureCmd.GainBlock(enemy,value,ValueProp.Unpowered,null);
                    break;
                case "Draw": await CardPileCmd.Draw(choiceContext,value,Owner); break;
                case "Energy": if(Owner.Creature.IsAlive) await PlayerCmd.GainEnergy(value,Owner); break;
                case "ExhaustStatusForEnergy":
                    var fuel=(await CardSelectCmd.FromHand(choiceContext,Owner,
                        new CardSelectorPrefs(CardSelectorPrefs.ExhaustSelectionPrompt,1),c=>c!=this,this)).FirstOrDefault();
                    if(fuel!=null)
                    {
                        bool status=fuel.Type==CardType.Status;
                        await Sts2Compat.Exhaust(choiceContext,fuel);
                        if(status && Owner.Creature.IsAlive) await PlayerCmd.GainEnergy(value,Owner);
                    }
                    break;
                case "LoseHp": await Sts2Compat.Damage(choiceContext,Owner.Creature,value,ValueProp.Unblockable|ValueProp.Unpowered|ValueProp.Move,null,this,cardPlay); break;
                case "LoseMaxHp": await CreatureCmd.LoseMaxHp(choiceContext,Owner.Creature,value,true); break;
                case "TempStrength": await PowerCmd.Apply<SatoriTemporaryStrengthPower>(choiceContext,Owner.Creature,value,Owner.Creature,this); break;
                case "TempStrengthLoss": if(cardPlay.Target is {IsAlive:true} mistTarget) await PowerCmd.Apply<SatoriMistPower>(choiceContext,mistTarget,value,Owner.Creature,this); break;
                case "Strength": await PowerCmd.Apply<StrengthPower>(choiceContext,Owner.Creature,value,Owner.Creature,this); break;
                case "Weak": await SatoriRules.Apply(choiceContext,typeof(WeakPower),cardPlay.Target,value,Owner.Creature,this); break;
                case "SelfWeak": await PowerCmd.Apply<WeakPower>(choiceContext,Owner.Creature,value,Owner.Creature,this); break;
                case "WeakAll": foreach(var target in SatoriRules.Enemies(Owner)) await PowerCmd.Apply<WeakPower>(choiceContext,target,value,Owner.Creature,this); break;
                case "Vulnerable": await SatoriRules.Apply(choiceContext,typeof(VulnerablePower),cardPlay.Target,value,Owner.Creature,this); break;
                case "VulnerableAll": foreach(var target in SatoriRules.Enemies(Owner)) await PowerCmd.Apply<VulnerablePower>(choiceContext,target,value,Owner.Creature,this); break;
                case "Suggestion": await SatoriRules.ApplySuggestion(choiceContext,cardPlay.Target,value,Owner.Creature,this); break;
                case "SuggestionAll": foreach(var target in SatoriRules.Enemies(Owner)) await SatoriRules.ApplySuggestion(choiceContext,target,value,Owner.Creature,this); break;
                case "SuggestionNonAttackAll": foreach(var target in SatoriRules.Enemies(Owner).Where(t=>!SatoriRules.HasAttack(t))) await SatoriRules.ApplySuggestion(choiceContext,target,value,Owner.Creature,this); break;
                case "Load": await SatoriRules.ApplyLoad(choiceContext,cardPlay.Target,value,Owner.Creature,this); break;
                case "PoisonFromLoad":
                    if(cardPlay.Target is {IsAlive:true} poisonTarget)
                    {
                        int poison=poisonTarget.GetPowerAmount<SatoriMentalLoadPower>()/2;
                        if(poison>0) await PowerCmd.Apply<PoisonPower>(choiceContext,poisonTarget,poison,Owner.Creature,this);
                    }
                    break;
                case "LoadAll": foreach(var target in SatoriRules.Enemies(Owner)) await SatoriRules.ApplyLoad(choiceContext,target,value,Owner.Creature,this); break;
                case "LoadAttackAll": foreach(var target in SatoriRules.Enemies(Owner).Where(SatoriRules.HasAttack)) await SatoriRules.ApplyLoad(choiceContext,target,value,Owner.Creature,this); break;
                case "Dampen": await SatoriRules.Apply(choiceContext,typeof(SatoriDampenedIntentPower),cardPlay.Target,value,Owner.Creature,this); break;
                case "RewriteDefend": SatoriRules.RewriteDefend(cardPlay.Target,(int)value); break;
                case "RewriteVulnerable": SatoriIntents.RewriteVulnerable(cardPlay.Target,Owner.Creature,(int)value,this); break;
                case "Provoke": SatoriIntents.Provoke(cardPlay.Target,Owner);break;
                case "CopyHandMemories":
                    // Snapshot originals: copies generated during the loop are not copied again.
                    var originals=Owner.PlayerCombatState!.Hand.Cards.Where(c=>c is SatoriPoolCard {IsMemory:true}).ToArray();
                    foreach(var original in originals)
                    {
                        if(!mind.TryGenerate()) break;
                        var copy=CombatState!.CloneCard(original);
                        await CardPileCmd.AddGeneratedCardToCombat(copy,PileType.Hand,Owner);
                    }
                    break;
                case "DiscordAbsentAll":
                    foreach(var target in SatoriRules.Enemies(Owner).Where(t=>!t.Powers.OfType<SatoriDiscordPower>().Any(power=>power.Applier==Owner.Creature)).ToArray())
                        await PowerCmd.Apply<SatoriDiscordPower>(choiceContext,target,value,Owner.Creature,this);
                    break;
                case "AddDefendIntent":
                    if(!SatoriIntents.AddDefend(cardPlay.Target,(int)value)) return;
                    break;
                case "AddStrengthIntent":
                    if(!SatoriIntents.AddStrength(cardPlay.Target,(int)value,this)) return;
                    break;
                case "StopAttacks":
                    foreach(var target in SatoriRules.Enemies(Owner).Where(SatoriRules.HasAttack)) await SatoriRules.Stop(target);
                    break;
                case "SuggestionStop":
                    var orderSuggestion=SatoriRules.OwnSuggestion(cardPlay.Target,Owner.Creature);
                    if(orderSuggestion!=null && orderSuggestion.Amount>=value && SatoriRules.CanRewrite(cardPlay.Target))
                    {
                        await orderSuggestion.SpendLayers(choiceContext,(int)value,this);
                        await SatoriRules.Stop(cardPlay.Target!);
                    }
                    break;
                case "SuggestionStrike":
                    if(cardPlay.Target is {IsAlive:true} suggestionTarget)
                    {
                        await DamageCmd.Attack(((CalculatedVar)DynamicVars["CalculatedDamage"]).Calculate(suggestionTarget)).FromCardCompat(this,cardPlay).Targeting(suggestionTarget).Execute(choiceContext);
                    }
                    break;
                case "DoubleLoad":
                    if(cardPlay.Target is {IsAlive:true} doubleTarget && doubleTarget.GetPower<SatoriMentalLoadPower>() is {} doubleLoad)
                        // Modify the existing layers directly: growth bonuses must not turn a doubling into extra stacks.
                        await PowerCmd.ModifyAmount(choiceContext,doubleLoad,doubleLoad.Amount,null,null);
                    break;
                case "DoubleSuggestionAll":
                    foreach(var enemy in SatoriRules.Enemies(Owner))
                        if(SatoriRules.OwnSuggestion(enemy,Owner.Creature) is {} suggestion)
                            await PowerCmd.ModifyAmount(choiceContext,suggestion,suggestion.Amount,null,null);
                    break;
                case "SuggestionToLoadAll":
                    foreach(var target in SatoriRules.Enemies(Owner))
                    {
                        if(SatoriRules.OwnSuggestion(target,Owner.Creature) is not {} conversion) continue;
                        int layers=conversion.Amount;
                        await conversion.SpendLayers(choiceContext,layers,this);
                        await SatoriRules.ApplyLoad(choiceContext,target,layers,Owner.Creature,this);
                    }
                    break;
                case "Discord": await SatoriRules.Apply(choiceContext,typeof(SatoriDiscordPower),cardPlay.Target,value,Owner.Creature,this); break;
                case "DiscordAll": foreach(var target in SatoriRules.Enemies(Owner)) await SatoriRules.Apply(choiceContext,typeof(SatoriDiscordPower),target,value,Owner.Creature,this); break;
                case "SettleLoad":
                    if(cardPlay.Target is {IsAlive:true} settleTarget && settleTarget.GetPower<SatoriMentalLoadPower>() is {} settleLoad)
                        await settleLoad.SettleNow(choiceContext);
                    break;
                case "Power": await SatoriRules.Apply(choiceContext,SatoriRules.PowerType(effect.Arg)!,Owner.Creature,value,Owner.Creature,this); break;
                case "CommandGuard":
                    var commanded=cardPlay.Target?.GetPowerInstances<SatoriSuggestionPower>().FirstOrDefault(p=>p.Applier==Owner.Creature);
                    int suggested=commanded?.Amount??0;
                    await CreatureCmd.GainBlock(Owner.Creature,value+suggested*int.Parse(effect.Arg),ValueProp.Move,cardPlay);
                    if(commanded!=null) await commanded.CashOut(choiceContext,false);
                    break;
                case "SuggestionGuard":
                    int ownedSuggestion=cardPlay.Target?.GetPowerInstances<SatoriSuggestionPower>().FirstOrDefault(p=>p.Applier==Owner.Creature)?.Amount??0;
                    await CreatureCmd.GainBlock(Owner.Creature,value*ownedSuggestion,ValueProp.Move,cardPlay);
                    break;
                case "Detonate":
                    if(cardPlay.Target is {IsAlive:true} loadTarget && loadTarget.GetPower<SatoriMentalLoadPower>() is {} load)
                    {
                        int consumed=Math.Min((int)value,load.Amount);
                        await PowerCmd.ModifyAmount(choiceContext,load,-consumed,Owner.Creature,this);
                        if(consumed>0) await Sts2Compat.Damage(choiceContext,loadTarget,consumed*int.Parse(effect.Arg),ValueProp.Unpowered,Owner.Creature,this,cardPlay);
                    }
                    break;
                case "Mirror": for(int n=0;n<(int)value;n++) await SatoriRules.Mirror(choiceContext,Owner,cardPlay.Target,this); break;
                case "MirrorAll":
                    foreach(var enemy in SatoriRules.Enemies(Owner).ToArray())
                        for(int n=0;n<(int)value;n++) await SatoriRules.Mirror(choiceContext,Owner,enemy,this);
                    break;
                case "Generate":
                    if(effect.Arg=="EachFree") foreach(var category in new[]{"Attack","Defense","Debuff","Buff"}) await SatoriRules.GenerateMemory(choiceContext,Owner,category,this);
                    else for(int n=0;n<(int)value;n++) await SatoriRules.GenerateMemory(choiceContext,Owner,effect.Arg,this);
                    break;
                case "GenerateUpgraded":
                    for(int n=0;n<(int)value;n++) await SatoriRules.GenerateMemory(choiceContext,Owner,effect.Arg,this,true);
                    break;
                case "RandomUpgradedMemory":
                    var categories=new[]{"Attack","Defense","Debuff","Buff"};
                    for(int n=0;n<(int)value;n++)
                        await SatoriRules.GenerateMemory(choiceContext,Owner,Owner.RunState.Rng.CombatCardGeneration.NextItem(categories)!,this,true);
                    break;
                case "Discard":
                case "ExhaustChoice":
                case "ExhaustExact":
                case "UpgradeMemories":
                case "CopyMemory":
                    var selected=await CardSelectCmd.FromHand(choiceContext,Owner,
                        new CardSelectorPrefs(new LocString("cards",Id.Entry+".selectionScreenPrompt"),effect.Op=="ExhaustExact"?Math.Min((int)value,Owner.PlayerCombatState!.Hand.Cards.Count(c=>c!=this)):0,effect.Op=="CopyMemory"?1:(int)value),
                        card=>card!=this && (effect.Op is "Discard" or "ExhaustChoice" or "ExhaustExact" || card is SatoriPoolCard {IsMemory:true}),this);
                    foreach(var card in selected.ToArray())
                    {
                        if(effect.Op=="Discard") await CardCmd.Discard(choiceContext,card);
                        if(effect.Op is "ExhaustChoice" or "ExhaustExact") await Sts2Compat.Exhaust(choiceContext,card);
                        if(effect.Op=="UpgradeMemories" && card.IsUpgradable) CardCmd.Upgrade(card);
                        if(effect.Op=="CopyMemory") for(int n=0;n<(int)value;n++)
                            await SatoriRules.GenerateMemory(choiceContext,Owner,SatoriRules.MemoryCategory(card),this,card.IsUpgraded);
                    }
                    break;
                case "RecallMemory":
                    var memories=Owner.PlayerCombatState!.ExhaustPile.Cards.Where(c=>c is SatoriPoolCard {IsMemory:true}).ToArray();
                    if(memories.Length==0) break;
                    var recalled=await CardSelectCmd.FromSimpleGrid(choiceContext,memories,Owner,new CardSelectorPrefs(new LocString("cards","SATORI_RECALL.prompt"),0,Math.Min((int)value,memories.Length)));
                    foreach(var card in recalled.ToArray())
                    {
                        if(!mind.TryGenerate()) break;
                        await CardPileCmd.Add(card,PileType.Hand);
                    }
                    break;
                default: throw new InvalidOperationException("Unknown Satori card rule: "+effect.Op);
            }
        }
    }
}

internal static class SatoriRules
{
    internal static SatoriSuggestionPower? OwnSuggestion(Creature? target,Creature applier)
        =>target?.GetPowerInstances<SatoriSuggestionPower>().FirstOrDefault(p=>p.Applier==applier);
    internal static bool CanRewrite(Creature? target)=>target is {IsAlive:true,Monster:not null} && target.Monster.NextMove.CanTransitionAway;
    internal static async Task Stop(Creature target)
    {
        if(CanRewrite(target)) await CreatureCmd.Stun(target,target.Monster!.NextMove.Id);
    }
    internal static void RewriteDefend(Creature? target,int block)
    {
        if(!HasAttack(target) || !CanRewrite(target)) return;
        var monster=target!.Monster!;
        var original=monster.NextMove;
        var move=new MoveState("SATORI_REWRITTEN_DEFEND",async _=> {
            if(target.IsAlive) await CreatureCmd.GainBlock(target,block,ValueProp.Unpowered,null);
        },new DefendIntent()) {FollowUpStateId=original.Id,MustPerformOnceBeforeTransitioning=true};
        monster.MoveStateMachine!.States[move.Id]=move;
        monster.SetMoveImmediate(move);
    }
    internal static IEnumerable<Creature> Enemies(Player player) => player.Creature.CombatState?.HittableEnemies.Where(c=>c.IsAlive).ToArray()??Array.Empty<Creature>();
    internal static bool HasAttack(Creature? target) => target?.Monster?.NextMove?.Intents.Any(i=>i.IntentType is IntentType.Attack or IntentType.DeathBlow)==true;
    internal static bool Condition(string when,Creature? target,Player owner,SatoriMindPower mind) => when switch {
        "" => true,"NonAttack"=>!HasAttack(target),"Attack"=>HasAttack(target),"AnyAttack"=>Enemies(owner).Any(HasAttack),
        "MemoryPlayed"=>mind.MemoriesPlayed>0,"PreviousMemory"=>mind.PreviousWasMemory,"Suggested"=>(OwnSuggestion(target,owner.Creature)?.Amount??0)>0,
        _=>throw new InvalidOperationException("Unknown Satori condition "+when)
    };
    internal static Type? PowerType(string key) => typeof(Entry).Assembly.GetType("SatoriCharacter."+key) ?? typeof(PowerModel).Assembly.GetType("MegaCrit.Sts2.Core.Models.Powers."+key);
    internal static async Task Apply(PlayerChoiceContext context,Type type,Creature? target,decimal amount,Creature applier,CardModel? source)
    {
        if(target is not {IsAlive:true} || amount<=0) return;
        var power=ModelDb.GetById<PowerModel>(ModelDb.GetId(type)).ToMutable();
        await PowerCmd.Apply(context,power,target,amount,applier,source);
    }
    internal static async Task ApplySuggestion(PlayerChoiceContext context,Creature? target,decimal amount,Creature applier,CardModel? source)
    {
        if(target is not {IsAlive:true}) return;
        await Apply(context,typeof(SatoriSuggestionPower),target,amount,applier,source);
    }
    internal static async Task ApplyLoad(PlayerChoiceContext context,Creature? target,decimal amount,Creature applier,CardModel? source)
    {
        if(target is not {IsAlive:true}) return;
        // DeepSea's native amount hook adds its bonus; load has no custom stack cap.
        await Apply(context,typeof(SatoriMentalLoadPower),target,amount,applier,source);
    }
    internal static async Task<SatoriMindPower> Mind(PlayerChoiceContext context,Player player)
    {
        if(player.Creature.GetPower<SatoriMindPower>() is {} existing) return existing;
        return (await PowerCmd.Apply<SatoriMindPower>(context,player.Creature,1,player.Creature,null))!;
    }
    internal static string MemoryCategory(CardModel card) => card switch {
        SatoriAttackMemory=>"Attack",SatoriDefenseMemory=>"Defense",SatoriDebuffMemory=>"Debuff",_=>"Buff"
    };
    internal static IEnumerable<string> IntentCategories(Creature? target)
    {
        var intents=target?.Monster?.NextMove?.Intents.Select(i=>i.IntentType).ToArray()??Array.Empty<IntentType>();
        if(intents.Length==0) {yield return "Buff";yield break;}
        foreach(var intent in intents) yield return intent switch {
            IntentType.Attack or IntentType.DeathBlow=>"Attack",
            IntentType.Debuff or IntentType.DebuffStrong or IntentType.StatusCard or IntentType.CardDebuff=>"Debuff",
            IntentType.Defend=>"Defense",_=>"Buff"
        };
    }
    internal static async Task Mirror(PlayerChoiceContext context,Player player,Creature? target,CardModel? source)
    {
        if(target is not {IsAlive:true}) return;
        var mind=await Mind(context,player);
        if(!mind.TryMirror()) return;
        foreach(var category in IntentCategories(target).ToArray())
        {
            if(!await GenerateMemory(context,player,category,source)) return;
            if(mind.Mirrors==1)
                for(int i=0;i<player.Creature.GetPowerAmount<SatoriMindSovereignPower>();i++) await GenerateMemory(context,player,category,source);
        }
        if(player.GetRelic<SatoriHeartBell>() is {} bell) await bell.OnMirror(context,target,source);
    }
    internal static async Task<bool> GenerateMemory(PlayerChoiceContext context,Player player,string category,CardModel? source,bool upgraded=false)
    {
        var mind=await Mind(context,player);
        if(!mind.TryGenerate()) return false;
        CardModel canonical=category switch {
            "Attack"=>ModelDb.Card<SatoriAttackMemory>(),"Defense"=>ModelDb.Card<SatoriDefenseMemory>(),"Debuff"=>ModelDb.Card<SatoriDebuffMemory>(),
            "Buff"=>ModelDb.Card<SatoriBuffMemory>(),_=>throw new InvalidOperationException("Unknown memory "+category)
        };
        var card=player.Creature.CombatState!.CreateCard(canonical,player);
        if(upgraded) {card.UpgradeInternal();card.FinalizeUpgradeInternal();}
        await CardPileCmd.AddGeneratedCardToCombat(card,PileType.Hand,player);
        return true;
    }
}
