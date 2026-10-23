"""Authoring source for the v0.3.14 pool. Generates models, specs and a reviewable card list.
No image generation or external calls. Change this file, then rerun to update the pool.
"""
import json, re
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
cards=[]
def e(op, value=0, up=None, arg='', when='', scale='', cap=0):
    return dict(op=op, value=value, up=value if up is None else up, arg=arg, when=when, scale=scale, cap=cap)
def add(key,title,cost,typ,rarity,target,route,*effects,keywords='',upgrade_cost=None,upgrade_keywords='',remove_keywords=''):
    cards.append(dict(key=key,title=title,cost=cost,type=typ,rarity=rarity,target=target,route=route,effects=list(effects),keywords=keywords.split() if keywords else [],upgrade_cost=cost if upgrade_cost is None else upgrade_cost,upgrade_keywords=upgrade_keywords.split(),remove_keywords=remove_keywords.split()))
def atk(key,title,cost,rarity,route,*effects,**kw): add(key,title,cost,'Attack',rarity,'AnyEnemy',route,*effects,**kw)
def aoe(key,title,cost,rarity,route,*effects,**kw): add(key,title,cost,'Attack',rarity,'AllEnemies',route,*effects,**kw)
def skill(key,title,cost,rarity,route,*effects,target='Self',**kw): add(key,title,cost,'Skill',rarity,target,route,*effects,**kw)
def power(key,title,cost,rarity,route,name,value,up=None,**kw): add(key,title,cost,'Power',rarity,'Self',route,e('Power',value,up,name),**kw)

# Existing basic IDs stay stable; two new basics introduce the character's play loop.
add('SatoriStrike','打击',1,'Attack','Basic','AnyEnemy','基础',e('Damage',6,9))
add('SatoriDefend','防御',1,'Skill','Basic','Self','基础',e('Block',5,8))
skill('SatoriReadHeart','读心',0,'Basic','操纵',e('Suggestion',3,5),target='AnyEnemy')
skill('SatoriRecallThought','想起',1,'Basic','复写',e('Block',4,4),e('Mirror',1,2),target='AnyEnemy')

# 22 commons: nine attacks, thirteen skills. Every enabler has immediate value.
atk('SatoriWhisperBolt','耳语弹',1,'Common','操纵',e('Damage',8,11),e('Suggestion',3,4))
atk('SatoriMindNeedle','心针',0,'Common','通用',e('Damage',4,7))
atk('SatoriProbingStrike','窥隙',1,'Common','意图',e('Damage',8,11),e('Damage',4,5,when='NonAttack'))
atk('SatoriUnrest','不安',1,'Common','负荷',e('Damage',6,9),e('Load',2,3))
atk('SatoriEchoBullet','回声弹',1,'Common','复写',e('Damage',6,9,scale='Memory',cap=3),e('Bonus',2,2))
aoe('SatoriThoughtWave','念波',1,'Common','通用',e('Damage',4,6,arg='2'))
atk('SatoriSoftRebuke','轻声斥责',1,'Common','操纵',e('Damage',8,11),e('Weak',1,2))
atk('SatoriGatheredThoughts','聚念',1,'Common','负荷',e('LoadStrike',6,9),e('Bonus',1,1))
atk('SatoriInsightCut','识破',1,'Common','意图',e('Damage',7,10),e('Damage',5,7,when='Attack'))
skill('SatoriWhisper','耳语',1,'Common','复写',e('Mirror',1,1),e('DiscardToTop',1,1),target='AnyEnemy',upgrade_cost=0)
skill('SatoriMindScreen','礼尚往来',1,'Common','意图',e('AddDefendIntent',6,4),e('Block',12,16),target='AnyEnemy')
skill('SatoriQuietBreath','打起精神',0,'Common','意图',e('AddStrengthIntent',2,2),e('Energy',2,3),target='AnyEnemy')
skill('SatoriReadAndWrite','读心复写',0,'Common','复写',e('Block',4,6),e('Mirror',1,1),target='AnyEnemy',keywords='Exhaust')
skill('SatoriReflection','映照',1,'Common','复写',e('Draw',2,3),e('Mirror',1,1),target='AnyEnemy')
skill('SatoriStaticNoise','杂音',1,'Common','负荷',e('Load',3,4),e('Vulnerable',1,2),target='AnyEnemy')
skill('SatoriForewarning','预备心防',1,'Common','意图',e('Block',7,10),e('Block',5,6,when='AnyAttack'))
skill('SatoriResonate','共鸣',1,'Common','复写',e('Block',7,10),e('Draw',1,1,when='MemoryPlayed'))
skill('SatoriSteadyHands','定心',1,'Common','通用',e('Block',5,8),e('Power',2,2,'SatoriTeaMemoryPower'))
skill('SatoriArrangeThoughts','档案整理',1,'Common','通用',e('Draw',2,3),e('ExhaustExact',2,2))
skill('SatoriHesitation','迟疑',1,'Common','操纵',e('Block',7,10),e('Suggestion',4,5),target='AnyEnemy')
skill('SatoriNoiseBarrier','杂念屏障',1,'Common','负荷',e('Block',6,9),e('Load',2,3),target='AnyEnemy')
skill('SatoriMemoryPolish','心象加工',1,'Uncommon','复写',e('Draw',2,2),e('Power',1,1,'SatoriEchoPower'),keywords='Exhaust',upgrade_cost=0)

# 38 uncommons: eleven attacks, twenty-one skills, six powers.
atk('SatoriMindExtortion','思维勒索',1,'Uncommon','操纵',e('SuggestionStrike',3,4,arg='4'))
atk('SatoriAfterimage','残像',1,'Uncommon','复写',e('Damage',8,11),e('Damage',8,11,when='PreviousMemory'))
skill('SatoriAgitation','激化',1,'Uncommon','负荷',e('PoisonFromLoad'),target='AnyEnemy',keywords='Exhaust',remove_keywords='Exhaust')
aoe('SatoriNoiseBurst','嘈杂',2,'Uncommon','通用',e('Damage',12,15),e('WeakAll',2,3))
atk('SatoriMemoryRain','记忆弹雨',1,'Uncommon','复写',e('MemoryVolley',2,3))
atk('SatoriThoughtLeech','念头汲取',1,'Uncommon','复写',e('ExhaustMemoryStrike',8,8),e('Bonus',2,3))
atk('SatoriHeartRend','心裂',2,'Uncommon','意图',e('IntentStrike',8,11))
atk('SatoriCounterEcho','逆响',1,'Common','意图',e('Damage',8,11),e('TempStrengthLoss',3,4))
skill('SatoriRedThreadSweep','红线横扫',1,'Uncommon','操纵',e('MirrorAll',1,1),e('SuggestionAll',3,5),target='AllEnemies')
atk('SatoriIntrusiveThought','侵入思维',1,'Uncommon','负荷',e('Damage',10,14),e('Load',3,4,when='NonAttack'))
skill('SatoriRetainedImage','滞留心象',1,'Uncommon','复写',e('TempFocus',3,5),keywords='Retain')
skill('SatoriIncite','挑拨',1,'Uncommon','操纵',e('Discord',1,1),target='AnyEnemy',upgrade_cost=0)
skill('SatoriCommandStop','止语',1,'Uncommon','操纵',e('SuggestionGuard',3,4),target='AnyEnemy')
skill('SatoriQuietCourt','静庭',2,'Uncommon','操纵',e('Block',9,13),e('DiscordAbsentAll',2,3))
skill('SatoriRewriteHostility','挑衅',1,'Uncommon','意图',e('Provoke',1,1),target='AnyEnemy',keywords='Exhaust',upgrade_keywords='Retain')
skill('SatoriBorrowedCourage','心灵屏蔽',1,'Uncommon','操纵',e('Power',1,1,arg='SatoriMentalShieldPower'),keywords='Exhaust',remove_keywords='Exhaust')
skill('SatoriMindEchoes','心声回廊',1,'Uncommon','复写',e('Mirror',2,2,arg='Free'),target='AnyEnemy',keywords='Exhaust',upgrade_cost=0)
skill('SatoriRecollection','追忆',1,'Uncommon','复写',e('Block',4,7),e('RecallMemory',1,2))
skill('SatoriMemorySketch','心象速写',0,'Uncommon','复写',e('GenerateUpgraded',1,2,arg='Defense'),keywords='Exhaust')
skill('SatoriEditMemory','书写记忆',1,'Uncommon','复写',e('RandomUpgradedMemory',2,3))
skill('SatoriThoughtTwin','双生念头',1,'Uncommon','复写',e('CopyHandMemories',1,1),keywords='Exhaust',remove_keywords='Exhaust')
skill('SatoriMentalOverload','超载',2,'Uncommon','负荷',e('Load',8,11),target='AnyEnemy')
skill('SatoriHeadache','换个念头',1,'Uncommon','意图',e('RewriteVulnerable',2,1),target='AnyEnemy',keywords='Exhaust')
skill('SatoriOminousSilence','压抑的静默',1,'Uncommon','意图',e('LoadAttackAll',5,7))
skill('SatoriLongNight','长夜',2,'Uncommon','通用',e('Block',17,23))
skill('SatoriMindSeal','心封',1,'Uncommon','通用',e('Block',8,11),e('Power',1,1,arg='BlurPower'))
skill('SatoriInnerOrder','内在秩序',0,'Uncommon','通用',e('ExhaustThenDraw',1,1),keywords='Exhaust',remove_keywords='Exhaust')
skill('SatoriSuddenFocus','熬夜',0,'Uncommon','通用',e('Power',5,5,arg='DoomPower'),e('Energy',1,1),e('Draw',2,3),keywords='Exhaust')
skill('SatoriExhale','阿空的燃料清单',0,'Uncommon','通用',e('ExhaustStatusForEnergy',1,2))
skill('SatoriGuardMemory','守念',1,'Uncommon','复写',e('Block',6,9),e('Generate',1,1,arg='Defense'))
skill('SatoriBindingRecall','束念',1,'Uncommon','负荷',e('SkillsToLoad',2,3))
skill('SatoriLingeringEcho','观察弱点',1,'Uncommon','意图',e('Strength',3,4,when='Attack'),target='AnyEnemy')
skill('SatoriWhisperDominion','低语支配',1,'Uncommon','负荷',e('Power',2,3,'SatoriWhisperPower'))
power('SatoriMemoryHall','记忆回廊',2,'Uncommon','复写','SatoriMirrorHallPower',1,1,upgrade_cost=1)
power('SatoriEchoAmplifier','心念凝聚',1,'Uncommon','复写','SatoriEchoPower',2,3)
power('SatoriThoughtShield','思绪护盾',1,'Uncommon','复写','SatoriResonancePower',2,3)
power('SatoriUnceasingNoise','不断的杂音',1,'Uncommon','负荷','SatoriStaticPower',1,2)
power('SatoriVerbalEcho','言语回声',1,'Uncommon','负荷','SatoriFeedbackPower',4,6)

# 26 rares: six attacks, seven skills, thirteen powers.
atk('SatoriMindCollapse','意识崩塌',2,'Rare','负荷',e('Damage',16,21),e('SettleLoad',1,1))
aoe('SatoriHeartStorm','心灵风暴',2,'Rare','混合',e('Damage',8,11),e('SuggestionToLoadAll',1,1))
skill('SatoriPerfectEcho','完美回响',1,'Rare','操纵',e('DiscordAll',2,3),target='AllEnemies')
atk('SatoriPuppetStrike','傀儡一击',2,'Rare','操纵',e('Damage',10,10),e('Discord',3,4))
skill('SatoriSubliminalFang','⑤',1,'Rare','混合',e('FifthTurn',5,5),e('WeakAll',5,5),e('VulnerableAll',5,5),e('LoadAll',5,5),e('SuggestionAll',5,5),target='AllEnemies',keywords='Exhaust',upgrade_keywords='Retain')
aoe('SatoriRecallBarrage','追忆齐射',2,'Rare','复写',e('MemoryBarrage',3,5))
skill('SatoriMindLiberation','心灵解放',0,'Rare','通用',e('LoseMaxHp',1,1),e('Draw',3,5),e('Energy',2,2),keywords='Exhaust')
skill('SatoriTotalSilence','万籁俱寂',2,'Rare','意图',e('StopAttacks',1,1),keywords='Exhaust',upgrade_cost=1)
skill('SatoriDeepImprint','深层烙印',2,'Rare','负荷',e('DoubleLoad',1,1),target='AnyEnemy',keywords='Exhaust',upgrade_cost=1)
skill('SatoriFourfoldMemory','四重心象',2,'Rare','复写',e('Generate',1,1,arg='EachFree'),keywords='Exhaust',upgrade_cost=1)
skill('SatoriEmptyMind','无念',2,'Rare','通用',e('BlockBoth',20,20),e('Power',2,2,arg='EnergyNextTurnPower'),e('Power',2,3,arg='DrawCardsNextTurnPower'),target='AllEnemies',keywords='Exhaust')
skill('SatoriFinalOrder','最后的命令',1,'Rare','操纵',e('SuggestionStop',8,6),target='AnyEnemy',keywords='Exhaust')
skill('SatoriDreamReprise','梦境重奏',1,'Rare','复写',e('Block',5,8),e('RecallMemory',3,4),keywords='Exhaust')
power('SatoriDreamStudy','梦境研习',2,'Rare','复写','SatoriDreamStudyPower',1,1,upgrade_cost=1)
power('SatoriRedStrings','红线网络',1,'Rare','操纵','SatoriRedStringsPower',6,9)
power('SatoriPuppetTheater','傀儡剧场',1,'Rare','操纵','SatoriPuppetTheaterPower',2,3)
power('SatoriDreadAura','威压',1,'Rare','操纵','SatoriDreadAuraPower',3,4)
power('SatoriNightmareField','噩梦领域',0,'Rare','负荷','SatoriNightmarePower',1,1,upgrade_keywords='Innate')
power('SatoriDeepSea','深海意识',2,'Rare','负荷','SatoriDeepSeaPower',2,2,upgrade_cost=1)
power('SatoriSharedNightmare','传染的噩梦',2,'Rare','复写','SatoriRupturePower',1,1,upgrade_cost=1,keywords='Innate')
power('SatoriClearSight','澄澈之眼',1,'Rare','复写','SatoriFocusPower',1,1,upgrade_keywords='Innate')
skill('SatoriMindWard','心灵结界',1,'Rare','操纵',e('DoubleSuggestionAll',1,1),keywords='Exhaust',remove_keywords='Exhaust')
add('SatoriPerfectMemory','过目不忘',3,'Power','Rare','Self','复写',e('Power',1,2,'SatoriEchoPower'),e('Power',1,2,'SatoriPerfectMemoryPower'),keywords='Ethereal')
power('SatoriCalmHeart','平静的心',2,'Rare','操纵','SatoriCalmPower',1,1,upgrade_cost=1)
skill('SatoriMemoryPalace','心无旁骛',3,'Rare','通用',e('KeepPile',1,1),keywords='Exhaust',upgrade_keywords='Retain')
power('SatoriMindSovereign','心之统御',3,'Rare','混合','SatoriMindSovereignPower',1,1,upgrade_cost=2)

# Two Ancient cards stay outside ordinary card rewards/shops, like native pools.
skill('SatoriAncientHeart','入侵',0,'Ancient','混合',e('SuggestionAll',6,9),e('LoadAll',6,9),target='AllEnemies')
power('SatoriAncientDream','王之蔑视',1,'Ancient','混合','SatoriKingDisdainPower',1,1,upgrade_keywords='Innate')

# Generated tokens are always zero cost and never enter permanent rewards.
tokens=[
 dict(key='SatoriAttackMemory',title='攻念',cost=0,type='Attack',rarity='None',target='AnyEnemy',route='记忆',effects=[e('Damage',5,5)],keywords=['Exhaust'],upgrade_cost=0),
 dict(key='SatoriDefenseMemory',title='守护心象',cost=0,type='Skill',rarity='None',target='Self',route='记忆',effects=[e('Block',4,4)],keywords=['Exhaust'],upgrade_cost=0),
 dict(key='SatoriDebuffMemory',title='咒念',cost=0,type='Skill',rarity='None',target='AnyEnemy',route='记忆',effects=[e('Weak',1,1),e('Vulnerable',1,1)],keywords=['Exhaust'],upgrade_cost=0),
 dict(key='SatoriBuffMemory',title='蓄念',cost=0,type='Skill',rarity='None',target='Self',route='记忆',effects=[e('TempStrength',2,2)],keywords=['Exhaust'],upgrade_cost=0)
]
for token in tokens:
    token['rarity']='Token'
    token['upgrade_keywords']=['Retain']
    token['remove_keywords']=[]

# Spell-card names refer to the SA/DS in-game spell lists; the rest are original
# flavor and light fan jokes, not presented as official Touhou spell names.
NAMES={
 'SatoriMindNeedle':'你在想什么？','SatoriProbingStrike':'心口不一','SatoriInsightCut':'缄默',
 'SatoriReadAndWrite':'想起「恐怖的回忆」','SatoriMemorySketch':'想起「飞行虫之巢」',
 'SatoriThoughtTwin':'想起「二重黑死蝶」','SatoriMemoryRain':'想起「波与粒的境界」',
 'SatoriRecallBarrage':'想起「百万鬼夜行」','SatoriPerfectEcho':'想起「春之京人偶」',
 'SatoriMindCollapse':'想起「恐怖催眠术」','SatoriDeepImprint':'脑符「脑指纹测谎法」',
 'SatoriMindSovereign':'心花「羞于留影的蔷薇」','SatoriMindSeal':'心灵的墙',
 'SatoriQuietCourt':'离间操纵','SatoriLongNight':'地底的长夜','SatoriSteadyHands':'喝杯茶吧',
 'SatoriArrangeThoughts':'档案整理','SatoriInnerOrder':'断念','SatoriEmptyMind':'闲暇时刻',
 'SatoriSuddenFocus':'熬夜','SatoriMindLiberation':'脑力透支','SatoriDreamStudy':'地灵殿图书室',
 'SatoriPerfectMemory':'全知形态','SatoriCalmHeart':'今日闭门谢客','SatoriMemoryPalace':'心无旁骛',
 'SatoriFourfoldMemory':'第三只眼的相册','SatoriDreamReprise':'想起「旧地狱的梦」',
 'SatoriRedStrings':'第三只眼的红线',
 'SatoriPuppetTheater':'地底木偶剧','SatoriDreadAura':'怨灵畏惧的少女',
 'SatoriNightmareField':'少女的噩梦','SatoriClearSight':'少女觉',
 'SatoriSharedNightmare':'旧地狱的来信','SatoriAncientHeart':'入侵',
 'SatoriAncientDream':'王之蔑视','SatoriNoiseBarrier':'猫猫结界','SatoriThoughtShield':'陪伴',
}
NAMES.update({'SatoriPerfectEcho': '想起「春之京都人偶」', 'SatoriProbingStrike': '想起「户隐山之投」', 'SatoriCounterEcho': '想起「朦朦迷雾」', 'SatoriMindSeal': '想起「风神木叶隐身术」', 'SatoriHeartStorm': '想起「天狗巨暴流」', 'SatoriRedThreadSweep': '想起「鸟居旋风」', 'SatoriPuppetTheater': '想起「稻草人偶神风」', 'SatoriMentalOverload': '想起「回归虚无」', 'SatoriAgitation': '想起「水银之毒」', 'SatoriLongNight': '想起「水精公主」', 'SatoriFourfoldMemory': '想起「贤者之石」', 'SatoriWhisperBolt': '想起「延展手臂」', 'SatoriThoughtWave': '想起「河童之河口浪潮」', 'SatoriRecollection': '想起「粼粼水底之心伤」'})
for c in cards:
    c['title']=NAMES.get(c['key'],c['title'])
    for effect in c['effects']:
        if effect['scale']=='Memory': effect['cap']=0

POWER_TEXT={
 'SatoriTeaMemoryPower':'下{Amount}个回合开始时，对随机敌人取念1次。',
'SatoriKingDisdainPower':'施加精神过载时，额外施加3层暗示。\n施加暗示时，额外施加3层精神过载。',
'SatoriMistPower':'本回合力量-{Amount}。',
'SatoriMentalShieldPower':'直到下回合开始，无法获得减益。',
'SatoriTemporaryFocusPower':'本回合心念+{Amount}。',
'SatoriWhisperPower':'本回合每打出1张技能牌，对所有敌人施加{Amount}层精神过载。',
'SatoriMirrorHallPower':'回合开始，对最左侧敌人取念{Amount}次。',
'SatoriEchoPower':'记忆牌伤害、格挡+{Amount}；力量、虚弱和易伤加成减半，向下取整。',
'SatoriResonancePower':'打出记忆牌时，获得{Amount}点格挡。',
'SatoriStaticPower':'回合结束，对所有敌人施加{Amount}层精神过载。',
'SatoriFeedbackPower':'有精神过载的敌人攻击前，获得{Amount}点格挡。',
'SatoriDreamStudyPower':'每打出1张记忆牌，抽{Amount}张牌。',
'SatoriRedStringsPower':'一名敌人的暗示耗尽时，立即对其施加{Amount}层暗示。',
'SatoriPuppetTheaterPower':'你每打出一张攻击牌后，对其命中目标施加{Amount}层暗示。',
'SatoriDreadAuraPower':'每回合开始，对所有敌人施加{Amount}层暗示。',
'SatoriNightmarePower':'精神过载提前到敌方回合开始结算。',
'SatoriDeepSeaPower':'每当你施加精神过载时，额外施加{Amount}层。',
'SatoriRupturePower':'本场战斗获得的临时牌均升级。不叠加。',
'SatoriFocusPower':'每打出5张记忆牌，获得{Amount}层心念。',
'SatoriMindWardPower':'旧版心灵结界，已停用。',
'SatoriPerfectMemoryPower':'回合开始，获得{Amount}层心念。',
'SatoriCalmPower':'消耗暗示时，只扣除一半层数，向上取整。不叠加。',
'SatoriMemoryPalacePower':'回合开始，抽{Amount}张牌。',
'SatoriExtraTurnPower':'回合结束后获得{Amount}个额外回合。',
'SatoriMindSovereignPower':'每回合首次取念，每种获得的记忆牌额外生成{Amount}张副本。',
'SatoriSuggestionPower':'将受到该敌人的伤害时，消耗所需暗示获得等量格挡。',
'SatoriDiscordPower':'不足5层时，每层使对施加者的攻击伤害降低10%。达到5层时，消耗5层，将下一次攻击行动转向另一名敌人；无其他敌人则攻击自己。转移的攻击不受此减伤影响。',
'SatoriMentalLoadPower':'敌方回合结束时受到层数的伤害；有攻击意图时伤害提高50%。',
'SatoriDampenedIntentPower':'本回合每次攻击伤害减少{Amount}。',
}
POWER_NAMES={
 'SatoriTeaMemoryPower':'茶余心声',
'SatoriKingDisdainPower':'王之蔑视','SatoriMistPower':'朦朦迷雾','SatoriMentalShieldPower':'心灵屏蔽','SatoriTemporaryFocusPower':'临时心念',
'SatoriWhisperPower':'低语支配','SatoriMirrorHallPower':'记忆回廊','SatoriEchoPower':'心念','SatoriResonancePower':'陪伴','SatoriStaticPower':'不断的杂音','SatoriFeedbackPower':'言语回声','SatoriDreamStudyPower':'地灵殿图书室','SatoriRedStringsPower':'红线网络','SatoriPuppetTheaterPower':'傀儡剧场','SatoriDreadAuraPower':'威压','SatoriNightmarePower':'少女的噩梦','SatoriDeepSeaPower':'深海意识','SatoriRupturePower':'旧地狱的来信','SatoriFocusPower':'少女觉','SatoriMindWardPower':'心灵结界','SatoriPerfectMemoryPower':'全知形态','SatoriCalmPower':'平静的心','SatoriMemoryPalacePower':'心象宫殿','SatoriMindSovereignPower':'心之统御','SatoriExtraTurnPower':'额外回合','SatoriSuggestionPower':'暗示','SatoriDiscordPower':'内讧','SatoriMentalLoadPower':'精神过载','SatoriMindPower':'心象计数','SatoriDampenedIntentPower':'迟钝'}
WHEN={'NonAttack':'若目标没有攻击意图，','Attack':'若目标有攻击意图，','AnyAttack':'若有敌人准备攻击，','MemoryPlayed':'若本回合已打出记忆牌，','PreviousMemory':'若上一张牌是记忆牌，','Suggested':'若目标有暗示，','Never':'（无效果）'}
def id_for(key): return re.sub(r'(?<!^)(?=[A-Z])','_',key).upper()
def describe(c, numeric=False, upgraded=False):
    out=[]
    for i,x in enumerate(c['effects']):
        if x['when']=='Never' or x['op']=='Bonus': continue
        n=str(x['up'] if upgraded else x['value']) if numeric else '{V'+str(i)+':diff()}'
        op,arg=x['op'],x['arg']
        energy=f'{n}点能量' if numeric else '{V'+str(i)+':energyIcons()}'
        if op=='MemoryVolley':
            t=f'造成{n}点伤害3次。若本回合已打出记忆牌，额外造成2次。'
        elif op=='MemoryBarrage':
            hits=str(0) if numeric else '{MemoryHits}'
            t=f'本场战斗每打出过1张记忆牌，对所有敌人造成{n}点伤害1次。（当前{hits}次）'
        elif op=='ExhaustMemoryStrike':
            total='8' if numeric else '{CalculatedDamage:diff()}'
            bonus=c['effects'][i+1]
            bn=str(bonus['up'] if upgraded else bonus['value']) if numeric else '{V'+str(i+1)+':diff()}'
            t=f'造成{total}点伤害。消耗堆每有1张记忆牌，伤害+{bn}。'
        elif op in ('Damage','LoadStrike','IntentStrike'):
            t=f'造成{n}点伤害'+(f'，共{arg}次' if arg else '')+'。'
            if x['scale']:
                b=next(y for y in c['effects'] if y['op']=='Bonus')
                bn=str(b['up'] if upgraded else b['value']) if numeric else '{V'+str(c['effects'].index(b))+':diff()}'
                t+=f'本回合每打出1张记忆牌，额外造成{bn}点伤害。'
            if op=='LoadStrike': t+='额外造成目标精神过载层数的伤害。'
            if op=='IntentStrike': t+='额外造成目标预计攻击伤害。'
        else:
            t={'Block':f'获得{n}点格挡。','BlockBoth':f'你和所有敌人各获得{n}点格挡。','Draw':f'抽{n}张牌。','Discard':f'弃掉至多{n}张手牌。','Energy':f'获得{energy}。','LoseHp':f'失去{n}点生命。','LoseMaxHp':f'失去{n}点最大生命。','TempStrength':f'本回合获得{n}点力量。','Strength':f'获得{n}点力量。','Suggestion':f'施加{n}层暗示。','SuggestionAll':f'对所有敌人施加{n}层暗示。','Load':f'施加{n}层精神过载。','LoadAll':f'对所有敌人施加{n}层精神过载。','Weak':f'施加{n}层虚弱。','WeakAll':f'对所有敌人施加{n}层虚弱。','Vulnerable':f'施加{n}层易伤。','Discord':f'施加内讧，其伤害上限为{n}。','CommandGuard':f'获得{n}点格挡。清空目标的暗示，每层再获得{arg}点格挡。','Detonate':f'消耗目标至多{n}层精神过载，每层造成{arg}点精神伤害。','Mirror':f'取念{n}次。','UpgradeMemories':f'选择至多{n}张手中的记忆牌，升级它们。','CopyMemory':f'选择1张手中的记忆牌，生成它的{n}张副本，保留升级。','RecallMemory':f'从消耗堆选择至多{n}张记忆牌加入手牌。','ExhaustChoice':f'消耗至多{n}张手牌。'}.get(op,'')
            if op=='Generate':
                t='生成攻念、守护心象、咒念、蓄念各1张。' if arg=='EachFree' else f'生成{n}张'+{'Attack':'攻念','Defense':'守护心象','Debuff':'咒念','Buff':'蓄念'}[arg]+'。'
            if op=='GenerateUpgraded': t=f'生成{n}张升级过的守护心象。'
            if op=='TempFocus': t=f'本回合获得{n}层心念。'
            if op=='SkillsToLoad': t=f'手牌每有1张技能牌，对随机敌人施加{n}层精神过载。'
            if op=='DiscardToTop': t='将弃牌堆1张牌置于抽牌堆顶。'
            if op=='ExhaustThenDraw': t='消耗1张手牌，抽1张牌。'
            if op=='ExhaustStatusForEnergy': t=f'选择并消耗另一张手牌。若为状态牌，获得{energy}。'
            if op=='Dampen': t=f'本回合目标每次攻击伤害减少{n}。'
            if op=='TempStrengthLoss': t=f'目标本回合失去{n}点力量。'
            if op=='Provoke': t='若目标没有攻击意图，随机改为它自身的一种攻击意图。'
            if op=='SuggestionGuard': t=f'目标每有1层你的暗示，获得{n}点格挡。'
            if op=='DiscordAbsentAll': t=f'对所有没有你的内讧的敌人施加{n}层内讧。'
            if op=='CopyHandMemories': t='复制手中所有记忆牌。'
            if op=='RewriteDefend': t=f'若目标准备攻击，将其本回合意图改为获得{n}点格挡。'
            if op=='StopAttacks': t='击晕所有具有攻击意图的敌人。'
            if op=='SuggestionStop': t=f'若目标至少有{n}层你的暗示，消耗这些层数，击晕目标。'
            if op=='SuggestionStrike':
                total=arg if numeric else '{CalculatedDamage:diff()}'
                t=f'造成{total}点伤害。目标每有1层你的暗示，伤害+{n}。'
            if op=='DoubleLoad': t='使目标的精神过载层数翻倍。'
            if op=='DoubleSuggestionAll': t='使所有敌人身上你的暗示层数翻倍。'
            if op=='SuggestionToLoadAll': t='消耗所有敌人身上你的暗示，施加等量精神过载。'
            if op=='SuggestionNonAttackAll': t=f'对没有攻击意图的敌人施加{n}层暗示。'
            if op=='LoadAttackAll': t=f'对准备攻击的敌人施加{n}层精神过载。'
            if op=='Discord': t=f'施加{n}层内讧。'
            if op=='DiscordAll': t=f'对所有敌人施加{n}层内讧。'
            if op=='SettleLoad': t='立即结算目标的精神过载一次。'
            if op=='PoisonFromLoad': t='施加目标精神过载层数一半的中毒（向下取整）。'
            if op=='MirrorAll': t=f'对所有敌人取念{n}次。'
            if op=='ExhaustExact': t=f'消耗{n}张手牌。'
            if op=='SelfWeak': t=f'获得{n}层虚弱。'
            if op=='AddDefendIntent': t=f'给目标追加获得{n}点格挡的意图。'
            if op=='AddStrengthIntent': t=f'给目标追加获得{n}点力量的意图。'
            if op=='RewriteVulnerable': t=f'若目标有攻击意图，将其本回合意图改为对你施加{n}层易伤。'
            if op=='VulnerableAll': t=f'对所有敌人施加{n}层易伤。'
            if op=='RandomUpgradedMemory': t=f'生成{n}张随机记忆牌并升级。'
            if op=='FifthTurn': t='只能在你的第5回合打出。'
            if op=='KeepPile':
                t='选择保留抽牌堆、手牌或弃牌堆，消耗其余两个牌堆的所有牌。'
                t+='获得一个额外回合。' if x['value']>0 and x['up']>0 else (('获得一个额外回合。' if x['up' if upgraded else 'value']>0 else '') if numeric else '{IfUpgraded:show:获得一个额外回合。|}')
            if op=='Power':
                if arg=='SatoriEchoPower': t=f'获得{n}层心念。'
                elif arg=='DoomPower': t=f'获得{n}点灾厄。'
                elif arg=='BlurPower': t=f'格挡在下{n}个回合开始时不会被移除。'
                elif arg=='EnergyNextTurnPower': t=f'下回合获得{energy}。'
                elif arg=='DrawCardsNextTurnPower': t=f'下回合多抽{n}张牌。'
                else: t=POWER_TEXT[arg].replace('{Amount}',n)
        if c['target']=='AllEnemies' and op=='Damage': t='对所有敌人'+t
        out.append(WHEN.get(x['when'],'')+t)
    return '\n'.join(out)

assert len(cards)==92,len(cards)
assert all(c['cost']==c['upgrade_cost']==0 for c in tokens)
assert all(e['op']!='FreeMemory' for c in cards for e in c['effects'])
assert len({c['key'] for c in cards+tokens})==96
expected={'Basic':4,'Common':22,'Uncommon':38,'Rare':26,'Ancient':2}
assert {r:sum(c['rarity']==r for c in cards) for r in expected}==expected
q=lambda s: json.dumps(s,ensure_ascii=False)
lines=['// Generated by tools/make_card_catalog.py; edit the authoring source.','namespace SatoriCharacter;','internal static class CardCatalog {','internal static readonly CardSpec[] All = new CardSpec[] {']
for c in cards+tokens:
    eff=', '.join('new("'+x['op']+'", '+str(x['value'])+'m, '+str(x['up'])+'m, '+q(x['arg'])+', '+q(x['when'])+', '+q(x['scale'])+', '+str(x['cap'])+')' for x in c['effects'])
    kws=', '.join('CardKeyword.'+k for k in c['keywords'])
    adds=', '.join('CardKeyword.'+k for k in c['upgrade_keywords'])
    removes=', '.join('CardKeyword.'+k for k in c['remove_keywords'])
    lines.append(f'new({q(c["key"])}, {q(c["title"])}, {c["cost"]}, {c["upgrade_cost"]}, CardType.{c["type"]}, CardRarity.{c["rarity"]}, TargetType.{c["target"]}, {q(c["route"])}, new Effect[] {{ {eff} }}, new CardKeyword[] {{ {kws} }}, {q(describe(c))}, new CardKeyword[] {{ {adds} }}, new CardKeyword[] {{ {removes} }}),')
lines+=['};','internal static readonly IReadOnlyDictionary<string,CardSpec> ByName = All.ToDictionary(c=>c.Key);','}']
lines.insert(1,'using MegaCrit.Sts2.Core.Entities.Cards;')
(ROOT/'Scripts/CardCatalog.g.cs').write_text('\n'.join(lines)+'\n',encoding='utf-8')
cls=['// Generated models have distinct native IDs for saves, compendium and rewards.','namespace SatoriCharacter;']
for c in cards[2:]+tokens:
    cls.append(f'public sealed class {c["key"]} : SatoriPoolCard {{ public {c["key"]}() : base("{c["key"]}") {{}} }}')
(ROOT/'Scripts/Cards.g.cs').write_text('\n'.join(cls)+'\n',encoding='utf-8')
loc={}
for c in cards+tokens:
    if c['key'] in ('SatoriStrike','SatoriDefend'): continue # Existing native Damage/Block variables.
    loc['cards:'+id_for(c['key'])+'.title']=c['title']
    loc['cards:'+id_for(c['key'])+'.description']=describe(c)
    # Selection prompts must use the native card-description variables.
    loc['cards:'+id_for(c['key'])+'.selectionScreenPrompt']='选择卡牌。'
for k,v in POWER_TEXT.items():
    loc['powers:'+id_for(k)+'.title']=POWER_NAMES[k]
    loc['powers:'+id_for(k)+'.description']=v
loc['powers:SATORI_ECHO_POWER.smartDescription']=POWER_TEXT['SatoriEchoPower']
loc['powers:SATORI_ECHO_POWER.description']='每层心念使记忆牌伤害、格挡+1；每2层使力量、虚弱和易伤+1，向下取整。'
loc['cards:SATORI_MIRROR.title']='取念与记忆牌'
loc['cards:SATORI_MEMORY.title']='记忆牌'
loc['cards:SATORI_MEMORY.description']='攻念、守护心象、咒念和蓄念统称记忆牌。均为0费、消耗，升级后增加保留，数值不变。心念可强化它们。'
loc['cards:SATORI_MIRROR.description']='按敌人每个当前意图各生成1张记忆牌：攻击→攻念，减益→咒念，格挡→守护心象，其他→蓄念。均消耗，升级后保留。心念提高伤害和格挡，力量、虚弱、易伤加成减半，向下取整。'
loc['cards:SATORI_RECALL.prompt']='选择要召回的记忆牌。'
for key,title,other in [('SatoriKeepDraw','保留抽牌堆','手牌与弃牌堆'),('SatoriKeepHand','保留手牌','抽牌堆与弃牌堆'),('SatoriKeepDiscard','保留弃牌堆','抽牌堆与手牌')]:
    loc['cards:'+id_for(key)+'.title']=title
    loc['cards:'+id_for(key)+'.description']='保留其中{Count}张牌。\n消耗'+other+'的所有牌。'

loc['powers:SATORI_MIND_POWER.title']='心象计数'
loc['powers:SATORI_MIND_POWER.description']='战斗内的心象与牌序记录。'
(ROOT/'Scripts/CardStrings.g.cs').write_text('namespace SatoriCharacter;\ninternal static class CardStrings {\ninternal static readonly Dictionary<string,string> All=new(StringComparer.OrdinalIgnoreCase) {\n'+''.join(f'[{q(k)}]={q(v)},\n' for k,v in loc.items())+'};\n}\n',encoding='utf-8')
(ROOT/'docs/cards-v0314.json').write_text(json.dumps(cards+tokens,ensure_ascii=False,indent=2),encoding='utf-8')
doc=['# 古明地觉 v0.3.14：先古对话、入侵与王之蔑视','',
'92张永久牌与4种记忆牌，原有模型ID和卡图保留。初始遗物改为前2回合随机取念，欧罗巴斯升级仍每回合对所有敌人取念。','',
'## 规则','',
'- 暗示无自定义层数上限；只消耗攻击者的暗示抵挡伤害。每回合取念、生成、复制、召回不设数量上限，手牌溢出依原版规则进入弃牌堆。',
'- 四类记忆牌基础只有消耗，升级后增加保留；攻念5伤害、守护心象4格挡、蓄念本回合2力量、咒念1虚弱与1易伤，升级只增加保留，不加数值。心念对伤害和格挡加完整层数，对力量、虚弱、易伤加一半，向下取整。',
'- 旧地狱的来信2费，固有，升级仅降至1费：本场战斗生成的临时牌均升级；已有临时牌也升级，不改永久牌组。',
'- 地灵殿图书室2费，升级1费：每打出1张记忆牌抽1张；少女觉每打出5张记忆牌获得1心念，计数跨回合，升级只加固有。',
'- 心灵结界1费：翻倍所有敌人身上自己的暗示，升级移除消耗；今日闭门谢客仍使消耗暗示减半，向上取整。',
'- 万籁俱寂、最后的命令使用原生击晕意图，保留原版不可打断行动限制；最后的命令移除格挡。',
'- 脑力透支失去1最大生命，抽3/5，获得2能量；闲暇时刻改为2费，和所有敌人各获得20格挡，下回合获得2能量、多抽2/3；仍保留原来的消耗。',
'- 百万鬼夜行按照本场战斗累计打出的记忆牌数，对所有敌人造成3/5伤害若干次；牌面实时显示次数，不消耗累计计数。',
'- 全知形态3费、虚无，原有即时与回合开始获得心念的效果不变；少女的噩梦0费，升级加固有；想起「稻草人偶神风」、怨灵畏惧的少女为1费。',
'- 深海意识每次施加精神过载额外2层，升级仅减1费。王之蔑视替换地底蔷薇园，为1费先古能力牌，升级固有：每次施加精神过载追加3暗示，施加暗示追加3精神过载；追加效果不再触发。',
'- 读心0费，只施加3/5暗示；欧罗巴斯的先古之牙优先将读心升格为入侵：0费，全体6/9暗示与6/9精神过载，保留原牌升级和附魔。',
'- 心象加工蓝卡1费、抽2、获得1层心念，升级只降至0费，保留原消耗；朦朦迷雾白卡8/11伤害及本回合降低目标3/4力量。二重黑死蝶复制手中全部记忆牌，升级移除消耗。',
'- 止语每层自己的暗示提供3/4格挡，不消耗暗示；挑衅只从敌人本身已有的真实攻击行动里随机选择，保留原版不可打断限制，消耗，升级保留。',
'- 低语支配为技能，本回合随后每张技能向全体施加2/3精神过载，激活牌本身不触发；压抑的静默5/7、回归虚无8/11。离间操纵9/13格挡，只有没有你的内讧的敌人才获得2/3内讧。',
'- 卡牌名称可用简称指定。继续优先改造旧牌，保持92张永久牌规模。卡框和压缩60fps动画沿用，心灵屏蔽与临时心念增加独立状态图标。','',
'- 每个意图各生成1张记忆牌，多意图可同时取念。暗示按未被格挡覆盖的伤害消耗，保留剩余层数；红线在暗示耗尽时向该敌人补6/9层。',
'- 内讧不足5层时，每层使对施加者的攻击伤害降低10%，与虚弱乘算；达到5层，消耗5层转移下一次攻击行动，其他层数保留。没有其他敌人则攻击自己；多段与同一行动内的多次攻击都转向，转移攻击不受内讧减伤影响。',
'- 礼尚往来（原心幕）：追加敌人6/4格挡意图，自己获得12/16格挡。打起精神（原静息）：追加敌人2力量意图，自己获得2/3能量，不消耗。',
'- 换个念头：若目标有攻击意图，将其本回合意图改为对你施加2/1易伤，本牌消耗。追加力量、格挡意图仍在原行动后执行。',
'- 杂音1费：施加3/4精神过载、1/2易伤；映照抽2/3并取念1次；书写记忆生成2/3张随机记忆牌并升级。',
'- 心无旁骛3费、消耗：使用原生三选一界面保留抽牌堆、手牌或弃牌堆，消耗另外两堆，基础即获得1个额外回合，升级只增加保留。',
'- ⑤1费、消耗：只能在玩家第5回合打出，对全体敌人施加5虚弱、5易伤、5精神过载、5暗示；升级增加保留。',
'- 所有获得能量的牌面使用原生能量图标，文本卡表保留数值便于阅读。','',
'- 精神过载每个敌方回合结束结算，攻击意图伤害为1.5倍，向下取整；少女的噩梦提前到敌方回合开始，不重复触发。',
'- 心灵屏蔽覆盖到下个玩家回合开始，只阻止新的减益，不移除已存在的减益。滞留心象的临时心念在玩家回合结束移除，其他心念保留。',
'- 耳语取念1次、弃牌回顶，升级0费；断念先消耗1张其他手牌再抽1张，升级移除自身消耗。',
'## 全部卡牌','',
'下表基础数值未计入战斗加成。百万鬼夜行的次数在战斗中实时显示。','',
'| # | 卡牌 | 稀有度/类型 | 费用 | 方向 | 未升级 → 升级 |','|---|---|---|---|---|---|']
rar={'Basic':'基础','Common':'普通','Uncommon':'罕见','Rare':'稀有','Ancient':'远古','Token':'生成'}
typ={'Attack':'攻击','Skill':'技能','Power':'能力'}
for i,c in enumerate(cards+tokens,1):
    a=describe(c,True).replace('\n',' ');b=describe(c,True,True).replace('\n',' ')
    labels={'Exhaust':'消耗','Retain':'保留','Ethereal':'虚无','Innate':'固有'}
    before_kw=list(c['keywords'])
    after_kw=[k for k in before_kw if k not in c['remove_keywords']]+c['upgrade_keywords']
    if before_kw: a+='；'+'、'.join(labels[k] for k in before_kw)
    if after_kw: b+='；'+'、'.join(labels[k] for k in after_kw)
    cost=str(c['cost'])+(' → '+str(c['upgrade_cost']) if c['cost']!=c['upgrade_cost'] else '')
    doc.append(f'| {i} | {c["title"]} | {rar[c["rarity"]]}/{typ[c["type"]]} | {cost} | {c["route"]} | {a} → {b}'+' |')
(ROOT/'docs/CARD_POOL_V0314.md').write_text('\n'.join(doc)+'\n',encoding='utf-8')
print('Generated',len(cards),'permanent cards +',len(tokens),'memory tokens.')
