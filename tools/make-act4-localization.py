"""Author and merge the optional chapter's four-language native loc tables."""
import json
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
LOC=ROOT/'SatoriCharacter'/'localization'
# Approved C memory / B visitors and battle prose, in zhs/eng/jpn/kor order.
PROSE=json.loads((ROOT/'tools'/'act4-selected-prose.json').read_text(encoding='utf-8'))
data={lang:{} for lang in ('zhs','eng','jpn','kor')}
def add(table,key,zh,en,ja,ko):
    # CustomActModel / CustomEventModel implement BaseLib's ICustomModel, whose
    # actual ModelDb IDs are namespaced. Native relic/monster/power IDs are not.
    if table in ('acts','events') and key.startswith(('SATORI_GARDEN.','SATORI_GARDEN_ENTRANCE.','SATORI_GARDEN_VISITORS.','SATORI_HOMECOMING.')):
        key='SATORICHARACTER-'+key
    for lang,value in zip(data,(zh,en,ja,ko)):
        data[lang].setdefault(table,{})[key]=value
def ui(key,*text): add('events','SATORI_ACT4.'+key,*text)

ui('boss.bgm.credit','BGM · 514\n音乐：Maritumix','BGM · 514\nMusic: Maritumix','BGM · 514\n音楽：Maritumix','BGM · 514\n음악: Maritumix')
ui('boss.bgm.previous','上一首','Previous','前の曲','이전 곡')
ui('boss.bgm.next','下一首','Next','次の曲','다음 곡')
ui('boss.bgm.pause','暂停','Pause','一時停止','일시 정지')
ui('boss.bgm.resume','播放','Play','再生','재생')
ui('boss.bgm.drag','拖动曲名或背景移动播放器','Drag the title or background to move','曲名や背景をドラッグして移動','곡명이나 배경을 드래그하여 이동')
ui('boss.intent.visible',
   '当前显示真实意图。取念读取真实类型。重击前会出声预警。\n生命首次降至一半及以下时，下一次行动额外清除自身所有负面状态并获得5力量，仅触发一次。',
   'True intents are currently visible. Read Mind reads their true types. Heavy attacks are announced beforehand.\nAt half HP or below, her next action additionally removes all debuffs and grants 5 Strength. Triggers once.',
   '現在は本来の意図を表示。読心は本来の種類を読み取る。強打の前には声で予告する。\nHPが初めて半分以下になると、次の行動に加えて自身のデバフをすべて解除し、筋力5を得る。1回のみ。',
   '현재 실제 의도가 표시됩니다. 마음 읽기는 실제 유형을 읽으며 강타는 미리 말로 예고합니다.\n체력이 처음으로 절반 이하가 되면 다음 행동에서 추가로 모든 약화를 제거하고 힘을 5 얻습니다. 한 번만 발동합니다.')
ui('ending.empty','','','','')
ui('ending.result.title','欢迎回家','Welcome home','おかえりなさい','집에 돌아온 걸 환영해')
ui('ending.result.body',
   '最后一局结束了，姐妹踏上了回家的路。\n地灵殿的灯还亮着。\n本次冒险得分：[green]{Score}[/green]',
   'The last game is over, and the sisters are on their way home.\nThe lights of the Palace of the Earth Spirits are still on.\nRun score: [green]{Score}[/green]',
   '最後のひと遊びを終え、姉妹は帰り道についた。\n地霊殿には、まだ明かりが灯っている。\n今回のスコア：[green]{Score}[/green]',
   '마지막 놀이가 끝나고, 자매는 집으로 향했다.\n지령전의 불빛은 아직 켜져 있다.\n이번 모험 점수: [green]{Score}[/green]')
ui('ending.next','点击展开下一格','Click to reveal the next panel','クリックで次のコマへ','클릭하여 다음 칸 보기')
ui('ending.finish','点击完成冒险','Click to finish your adventure','クリックで冒険を終える','클릭하여 모험 마치기')
def event(model,page,*text):
    # Native rich-text paragraphs already have spacing. Blank dialogue lines
    # double the height and push four choices below the screen.
    add('events',model+'.pages.'+page+'.description',*(value.replace('\n\n','\n') for value in text))
def option(model,key,titles,descs):
    add('events',model+'.pages.INITIAL.options.'+key+'.title',*titles)
    add('events',model+'.pages.INITIAL.options.'+key+'.description',*descs)

add('acts','SATORI_GARDEN.title','无人注意的庭院','The Unnoticed Garden','誰にも気づかれない庭','아무도 눈치채지 못한 정원')
add('encounters','SATORI_KOISHI_ENCOUNTER.title','最后一局','One Last Game','最後のひと遊び','마지막 놀이')
add('encounters','SATORI_KOISHI_ENCOUNTER.loss','恋还想再玩一会儿……','Koishi still wants to play a little longer...','こいしは、まだもう少し遊びたいようだ……','코이시는 아직 조금 더 놀고 싶어 한다……')
add('monsters','SATORI_KOISHI.name','古明地恋','Komeiji Koishi','古明地こいし','코메이지 코이시')
for move,text in {
    'SUBCONSCIOUS':('无意识的捉迷藏','Unconscious Hide-and-Seek','無意識のかくれんぼ','무의식의 숨바꼭질'),
    'GARDEN':('无人注意的庭院','The Unnoticed Garden','誰にも気づかれない庭','아무도 눈치채지 못한 정원'),
    'HEART':('本能「本我的解放」','Instinct: Release of the Id','本能「イドの解放」','본능 「이드의 해방」'),
    'ROSE':('无意识的混乱','Unconscious Confusion','無意識の混乱','무의식의 혼란'),
    'LAST_GAME':('最后一局','One Last Game','最後のひと遊び','마지막 놀이'),
    'REKINDLED':('「被厌恶者的哲学」','Philosophy of a Hated Person','「嫌われ者のフィロソフィ」','「미움받는 자의 필로소피」'),
}.items(): add('monsters','SATORI_KOISHI.moves.'+move+'.title',*text)

for model,title,desc,flavor in [
('SATORI_LOST_HEART',('遗落的心结','A Lost Heartstring','落とされた心の結び目','잃어버린 마음 매듭'),
('击败第三幕全部首领后，前往第四章「无人注意的庭院」。\n首次进入时恢复全部已损生命；进阶2起改为恢复80%。\n只需获得一个心结；持有后，不再出现带火精英。','After defeating every Act 3 boss, enter Act 4: The Unnoticed Garden.\nRestore all missing HP once on entry (80% at Ascension 2+).\nOnly one heartstring is needed. Burning elites stop appearing while you hold it.','第3幕のボスをすべて倒すと、第4章「誰にも気づかれない庭」へ進む。\n初めて入ると失ったHPを全回復。アセンション2以降は80%回復。\n結び目は1つで十分。所持している間、強化エリートは出現しない。','3막의 모든 우두머리를 쓰러뜨리면 4장 「아무도 눈치채지 못한 정원」으로 진입합니다.\n처음 진입할 때 잃은 체력을 전부 회복합니다. 승천 2부터는 80% 회복합니다.\n매듭은 하나만 필요하며, 보유 중에는 불타는 엘리트가 나타나지 않습니다.'),
('她不经意留下的线，终于有了另一端。','A thread she left without thinking finally has someone at the other end.','何気なく残した糸に、ようやくもう一方の端ができた。','무심코 남긴 실의 반대편에 마침내 누군가가 닿았다.')),
('SATORI_PRECIOUS_MEMORY',('珍贵的回忆','A Precious Memory','大切な思い出','소중한 기억'),
('第四章来客事件中，可以邀请阿燐和阿空一起参加最终战。','In Act 4’s visitors event, you may ask both Orin and Okuu to join the final battle.','第4章の来客イベントで、お燐とお空の二人を最終戦に誘える。','4장의 방문자 이벤트에서 오린과 오쿠 두 사람을 최종전에 함께 데려갈 수 있습니다.'),
PROSE['memory.flavor']),
]:
    add('relics',model+'.title',*title); add('relics',model+'.description',*desc); add('relics',model+'.flavor',*flavor)

add('powers','SATORI_UNSEEN_PRESENCE_POWER.title','无人察觉 · 无实体','Unnoticed · Intangible','気づかれない存在・無形','인지되지 않는 존재 · 무형')
add('powers','SATORI_UNSEEN_PRESENCE_POWER.description',
    '受到的伤害和失去的生命至多为1。没有回合期限。\n全队共享破除进度：{Progress}/{Goal}。每位参战玩家需要4次；任何人打出《心结》增加1。队友倒下时免去其4次需求（每人限一次），保留已有进度。完成后永久破除，并消耗所有《心结》。',
    'Damage and HP loss are capped at 1. This does not expire.\nShared party progress: {Progress}/{Goal}. Each participant adds 4 to the goal; anyone playing a Heart Knot adds 1 progress. A fallen player waives 4 from the goal once, keeping earned progress. On completion, break Intangible permanently and Exhaust all Heart Knots.',
    '受けるダメージと失うHPは最大1。ターン経過では解除されない。\n全員共有の解除進行度：{Progress}/{Goal}。参加者1人につき目標が4増え、誰が「心の結び目」を使っても1進む。倒れた仲間の必要数4は1人1回免除され、進行度は保持する。達成すると無形を永続解除し、すべての結び目を廃棄する。',
    '받는 피해와 체력 손실은 최대 1이며 시간 제한이 없습니다.\n팀 공유 진행도: {Progress}/{Goal}. 참전자마다 목표가 4 늘고 누구든 마음의 매듭을 쓰면 1 진행됩니다. 쓰러진 아군은 한 번만 목표를 4 줄이며 기존 진행도는 유지됩니다. 완료 시 무형을 영구 해제하고 모든 마음의 매듭을 소멸시킵니다.')
for model,title,desc in [
 ('SATORI_HEART_KNOT',('心结','Heart Knot','心の結び目','마음의 매듭'),
  ('全队共享的无实体破除进度增加1。\n目标为每位参战玩家4次。完成后永久破除，并消耗所有《心结》。',
   'Add 1 to the party’s shared Intangible-breaking progress.\nThe goal is 4 per participant. On completion, break Intangible permanently and Exhaust all Heart Knots.',
   '全員共有の無形解除進行度を1増やす。\n目標は参加者1人につき4。達成すると無形を永続解除し、すべての「心の結び目」を廃棄する。',
   '팀 공유 무형 해제 진행도를 1 올립니다.\n목표는 참전자당 4입니다. 완료 시 무형을 영구 해제하고 모든 마음의 매듭을 소멸시킵니다.')),
 ('SATORI_NUCLEAR_AID',('核热支援','Nuclear Support','核熱支援','핵열 지원'),
  ('获得{Energy:energyIcons()}。','Gain {Energy:energyIcons()}.','{Energy:energyIcons()}を得る。','{Energy:energyIcons()}을 얻습니다.')),
 ('SATORI_SPIRIT_ESCORT',('怨灵护送','Spirit Escort','怨霊の護送','원령 호송'),
  ('获得{Block:diff()}点格挡。\n抽{Cards:diff()}张牌。','Gain {Block:diff()} Block.\nDraw {Cards:diff()} card.',
   'ブロックを{Block:diff()}得る。\nカードを{Cards:diff()}枚引く。','방어도를 {Block:diff()} 얻습니다.\n카드를 {Cards:diff()}장 뽑습니다.')),
]:
    add('cards',model+'.title',*title);add('cards',model+'.description',*desc)
add('powers','SATORI_UNCONSCIOUS_INTENT_POWER.title','无意识的行动','Unconscious Actions','無意識の行動','무의식의 행동')
add('powers','SATORI_UNCONSCIOUS_INTENT_POWER.description',
    '所有意图合并显示为一个？；行动执行时显示真实意图。取念仍读取真实类型。重击前会出声预警。\n生命首次降至一半及以下时，下一次行动额外清除自身所有负面状态并获得5力量，仅触发一次。',
    'All intents appear as a single ? until execution. Read Mind reads their true types. Heavy attacks are announced beforehand.\nAt half HP or below, her next action additionally removes all debuffs and grants 5 Strength. Triggers once.',
    'すべての意図を1つの？で表示し、実行時に本来の意図を示す。読心は本来の種類を読み取る。強打の前には声で予告する。\nHPが初めて半分以下になると、次の行動に加えて自身のデバフをすべて解除し、筋力5を得る。1回のみ。',
    '모든 의도를 하나의 ?로 표시하며 실행 시 실제 의도를 보여 줍니다. 마음 읽기는 실제 유형을 읽으며 강타는 미리 말로 예고합니다.\n체력이 처음으로 절반 이하가 되면 다음 행동에서 추가로 모든 약화를 제거하고 힘을 5 얻습니다. 한 번만 발동합니다.')

ui('burningElite.tip','带火精英：敌人获得{Strength}力量。击败后获得遗落的心结，开启可选第四章。','Burning Elite: enemies gain {Strength} Strength. Defeat them to obtain a Lost Heartstring and unlock optional Act 4.','強化エリート：敵が筋力を{Strength}得る。倒すと心の結び目を入手し、任意の第4章が解放される。','불타는 엘리트: 적들이 힘을 {Strength} 얻습니다. 처치하면 마음 매듭을 얻고 선택형 4장을 해금합니다.')
ui('rest.memoryStory',*PROSE['rest.memoryStory'])
ui('boss.presenceBroken','在那里。你的影子，我一直都记得。','There you are. I never forgot your shadow.','そこにいたのね。あなたの影は、ずっと覚えていた。','거기 있었구나. 네 그림자는 언제나 기억하고 있었어.')
ui('boss.phase2','这些烦人的念头，全部忘掉就好啦！','I’ll just forget all these bothersome thoughts!','こんな面倒な思いは、全部忘れちゃえばいいんだ！','이 성가신 생각들은 전부 잊어버리면 돼!')
ui('boss.phase3','好吧，这次真的是最后一局！','All right! This really is the last game!','わかった。今度こそ、本当に最後のひと遊び！','알았어. 이번에는 정말 마지막 놀이야!')
ui('boss.lastGame','接下来这一招，可要接住哦！','Be ready for my next move!','次の一撃、ちゃんと受け止めてね！','다음 공격은 제대로 받아 줘!')
ui('boss.heavyWarning.0',*PROSE['boss.heavyWarning.0'])
ui('boss.heavyWarning.1',*PROSE['boss.heavyWarning.1'])
ui('boss.heavyWarning.2',*PROSE['boss.heavyWarning.2'])
ui('boss.bubble.intro.koishi',*PROSE['boss.bubble.intro.koishi'])
ui('boss.bubble.intro.satori',*PROSE['boss.bubble.intro.satori'])
ui('boss.bubble.intro.1',*PROSE['boss.bubble.intro.1'])
ui('boss.bubble.intro.2',*PROSE['boss.bubble.intro.2'])
ui('boss.bubble.intro.3',*PROSE['boss.bubble.intro.3'])
ui('boss.bubble.intro.4',*PROSE['boss.bubble.intro.4'])
for choice,tail in {
1:('阿空：「我会控制好的，绝不烧花！」','Okuu: “I’ll be careful! No burning the flowers!”','お空：「ちゃんと加減するよ。花は燃やさない！」','오쿠: “잘 조절할게! 꽃은 안 태울 거야!”'),
2:('阿燐：「恋大人，我们来接您回去。别再躲啦。」','Orin: “Lady Koishi, we are here to bring you home. No more hiding.”','お燐：「こいし様、迎えに来ました。もう隠れないでくださいね。」','오린: “코이시 님, 데리러 왔어요. 이제 그만 숨으세요.”'),
3:('恋：「怎么大家都来了……好吧，只剩最后一局。」','Koishi: “Everyone came? ...Fine. Just one last game.”','こいし：「どうしてみんな来たの……わかった、最後のひと遊びだけ。」','코이시: “왜 다들 온 거야…… 알았어. 마지막 놀이만 하자.”'),
4:('恋：「只有姐姐一个人？那今天就只陪姐姐玩。」','Koishi: “Just you, sister? Then today I’ll play only with you.”','こいし：「お姉ちゃん一人？ じゃあ、今日はお姉ちゃんとだけ遊ぶね。」','코이시: “언니 혼자야? 그럼 오늘은 언니랑만 놀게.”')
}.items():
    ui('boss.intro.'+str(choice),
        '恋：「姐姐，不许偷偷猜我下一步哦。」\n觉：「最后一局。玩完以后，一起回去。」\n'+tail[0],
        'Koishi: “No secretly guessing my next move!”\nSatori: “One last game. Then we go home together.”\n'+tail[1],
        'こいし：「次の動きをこっそり当てちゃだめだよ。」\nさとり：「最後のひと遊び。終わったら、一緒に帰りましょう。」\n'+tail[2],
        '코이시: “내 다음 행동을 몰래 맞히면 안 돼!”\n사토리: “마지막 놀이야. 끝나면 함께 돌아가자.”\n'+tail[3])

entrance='SATORI_GARDEN_ENTRANCE'
add('events',entrance+'.title','无人注意的门','An Unnoticed Door','誰にも気づかれない扉','아무도 눈치채지 못한 문')
event(entrance,'INITIAL',
    '尖塔顶端的风终于停了。你正要转身，掌心的紫色心结忽然绷紧。\n\n墙角有一扇小门。刚才那里似乎什么也没有，而现在，门后传来熟悉的笑声。\n\n你认得那顶黑帽子留下的黄色缎带。恋就在门后。\n\n走进去，还是带着这条线索结束今天的冒险？',
    'The wind at the summit falls still. As you turn away, the purple heartstring tightens in your palm.\n\nA little door stands in the corner. A moment ago, there seemed to be nothing there. Now a familiar laugh drifts through it.\n\nYou recognize the yellow ribbon from that black hat. Koishi is just beyond the door.\n\nWill you follow her, or keep the clue and end today’s journey?',
    '頂の風が止んだ。振り返ろうとしたとき、手の中の紫色の結び目がぴんと張る。\n\n壁の隅に小さな扉がある。さっきは何もなかったはずなのに、今は向こうから聞き慣れた笑い声がする。\n\n黒い帽子の黄色いリボン。こいしは、この先にいる。\n\n追いかけるか、それとも手がかりを持ち帰り、今日の冒険を終えるか。',
    '탑 꼭대기의 바람이 멎었다. 돌아서려는 순간 손안의 보라색 매듭이 팽팽해진다.\n\n벽 모퉁이에 작은 문이 있다. 방금까지만 해도 아무것도 없던 자리인데, 이제 문 너머에서 익숙한 웃음소리가 들린다.\n\n검은 모자의 노란 리본. 코이시는 저 문 너머에 있다.\n\n뒤따라갈까, 아니면 단서를 간직한 채 오늘의 모험을 마칠까?')
option(entrance,'ENTER',('推开门','Open the door','扉を開ける','문을 연다'),('进入可选第四章：火堆 → 商店 → 来客 → 古明地恋。','Enter optional Act 4: Rest → Shop → Visitors → Komeiji Koishi.','任意の第4章へ：休憩所 → 商店 → 来客 → 古明地こいし。','선택형 4장 진입: 휴식 → 상점 → 방문자 → 코메이지 코이시.'))
option(entrance,'LEAVE',('暂且回去','Return for now','今日は引き返す','일단 돌아간다'),('保留本次正常胜利，前往原来的结局。','Keep your normal victory and continue to the original ending.','通常の勝利として、元の結末へ進む。','이번 승리를 유지하고 기존 결말로 진행합니다.'))
event(entrance,'ENTER','心结另一端的线飘进门里。你握紧它，踏入无人注意的庭院。','The other end of the heartstring slips through the doorway. You hold on and enter the unnoticed garden.','結び目の先の糸が扉の中へ揺れる。あなたはそれを握り、誰にも気づかれない庭へ足を踏み入れた。','매듭의 반대편 실이 문 안으로 흘러간다. 그것을 꼭 쥐고 아무도 눈치채지 못한 정원에 들어선다.')
event(entrance,'LEAVE','你把心结妥善收好。今天已经走得够远，这扇门不会让你忘记。','You put the heartstring away carefully. You have come far enough today. You will remember this door.','結び目を大切にしまう。今日は十分に歩いた。この扉のことは忘れない。','매듭을 소중히 간직한다. 오늘은 충분히 멀리 왔다. 이 문은 잊지 않을 것이다.')

visitors='SATORI_GARDEN_VISITORS'
add('events',visitors+'.title',*PROSE['visitors.title'])
event(visitors,'INITIAL',*PROSE['visitors.initial'])
option(visitors,'OKUU',PROSE['visitors.option.OKUU'],('最终战每2回合提供《核热支援》（偶数回合开始时）。','In the final battle, gain Nuclear Support every 2 turns, at the start of even turns.','最終戦では2ターンごと、偶数ターン開始時に「核熱支援」を届ける。','최종전에서 2턴마다, 짝수 턴 시작 시 핵열 지원을 제공합니다.'))
option(visitors,'ORIN',PROSE['visitors.option.ORIN'],('最终战每回合开始时，提供《怨灵护送》。','In the final battle, gain Spirit Escort at the start of every turn.','最終戦では毎ターン開始時に「怨霊の護送」を届ける。','최종전에서 매 턴 시작 시 원령 호송을 제공합니다.'))
both=PROSE['visitors.option.BOTH']
option(visitors,'BOTH',both,('回合开始时：阿空每2回合提供《核热支援》，阿燐每回合提供《怨灵护送》。','At turn start: Okuu gives Nuclear Support every 2 turns; Orin gives Spirit Escort every turn.','ターン開始時：お空は2ターンごとに「核熱支援」、お燐は毎ターン「怨霊の護送」を届ける。','턴 시작 시 오쿠는 2턴마다 핵열 지원을, 오린은 매 턴 원령 호송을 제공합니다.'))
option(visitors,'BOTH_LOCKED',both,('无法选择：需要在第四章火堆进行回忆，获得珍贵的回忆。','Locked: choose Remember at Act 4’s rest site to obtain a Precious Memory.','選択不可：第4章の休憩所で「思い出す」を選び、大切な思い出を得る必要がある。','선택 불가: 4장 휴식 장소에서 추억을 떠올려 소중한 기억을 얻어야 합니다.'))
option(visitors,'ALONE',PROSE['visitors.option.ALONE'],('独自前去，最终战获得《全知形态+》。','Go alone, gaining Omniscience+ for the final battle.','一人で向かい、最終戦で「全知の姿+」を得る。','혼자 가서 최종전에 전지의 형상+를 얻습니다.'))
for page in ('OKUU','ORIN','BOTH','ALONE'):
    event(visitors,page,*PROSE['visitors.page.'+page])

home='SATORI_HOMECOMING'
add('events',home+'.title','一直都在','Always There','ずっと、そこに','언제나 그 자리에')
for page in ('INITIAL','TOGETHER'):
    event(home,page,'','','','')
option(home,'HOME',('一起回家','Go home together','一緒に帰る','함께 집으로 돌아간다'),('完成第四章，记录本次胜利。','Complete Act 4 and record your victory.','第4章を終え、この冒険を勝利として記録する。','4장을 완료하고 이번 승리를 기록합니다.'))

for ident,title,desc in [
('SATORI_REMEMBER',('回忆','Remember','思い出す','추억을 떠올린다'),('获得珍贵的回忆，允许后面的事件邀请阿燐、阿空同时加入最终战。占用本次火堆行动。','Gain a Precious Memory, allowing both Orin and Okuu to join the final battle. Uses this rest-site action.','大切な思い出を得る。後のイベントでお燐とお空の二人を最終戦に誘える。今回の休憩所での行動を使う。','소중한 기억을 얻어 이후 이벤트에서 오린과 오쿠를 함께 최종전에 데려갈 수 있습니다. 이번 휴식 행동을 소모합니다.')),
('SATORI_GARDEN_PREVIEW',('前往第四章（预览）','Preview Act 4','第4章をプレビュー','4장 미리 보기'),('保留当前牌组，跳到第四章火堆。此为调试预览，会跳过剩余章节；请只在体验用存档使用。','Keep your deck and skip to Act 4’s rest site. This debug preview skips the remaining acts; use a separate preview run.','現在のデッキを保ち、第4章の休憩所へ移動。残りの幕を飛ばすデバッグ用プレビュー。体験用の冒険で使うこと。','현재 덱을 유지한 채 4장 휴식 장소로 건너뜁니다. 남은 막을 생략하는 디버그 미리 보기입니다. 체험용 모험에서 사용하세요.')),
]:
    add('rest_site_ui','OPTION_'+ident+'.name',*title); add('rest_site_ui','OPTION_'+ident+'.description',*desc)
for key,text in {
    'FOURTH_CHAPTER_SETTINGS.title':('第四章','Fourth Chapter','第4章','4장'),
    'ENABLE_FOURTH_CHAPTER.title':('启用可选第四章','Enable optional Act 4','任意の第4章を有効にする','선택형 4장 활성화'),
    'ENABLE_FOURTH_CHAPTER.hover.desc':('单人标准冒险由此开关控制。联机标准冒险中，队内任意觉领取遗落的心结即可在击败第三幕全部首领后全队进入庭院，不受各自本地开关影响。已进入第四章的存档不受开关影响。','Controls solo Standard runs. In multiplayer Standard runs, any Satori taking a Lost Heartstring opts the party into the garden after every Act 3 boss, independently of local toggles. Existing Act 4 saves remain playable.','ソロ通常プレイ用の設定。マルチ通常プレイでは、誰かのさとりが結び目を受け取ると、第3幕の全ボス撃破後に全員で庭へ進む。各自の設定には依存しない。第4章のセーブは継続可能。','1인 일반 모험용 설정입니다. 다인 일반 모험에서는 사토리 한 명이 마음 매듭을 받으면 각자의 설정과 관계없이 3막의 모든 우두머리 이후 전원이 정원에 진입합니다. 기존 4장 저장은 계속 진행할 수 있습니다.'),
    'ENABLE_CHAPTER_PREVIEW.title':('火堆显示第四章预览入口','Show Act 4 preview at rest sites','休憩所に第4章プレビューを表示','휴식 장소에 4장 미리 보기 표시'),
    'SHOW_FINAL_BATTLE_BGM_CREDIT.title':('显示最终战 BGM 介绍','Show final-battle BGM credit','最終戦のBGM紹介を表示','최종전 BGM 소개 표시'),
    'DialogueVoiceSettings':('对话语音','Dialogue voices','会話ボイス','대화 음성'),
    'ENABLE_DIALOGUE_VOICES.title':('启用对话语音','Enable dialogue voices','会話ボイスを有効にする','대화 음성 활성화'),
    'ENABLE_DIALOGUE_VOICES.hover.desc':('觉与恋的战斗气泡播放日语语音；关闭时立即停止。同伴气泡不配音。','Play Japanese speech for Satori and Koishi’s battle bubbles. Disabling stops it immediately. Companion bubbles are unvoiced.','さとりとこいしの戦闘中の吹き出しに日本語ボイスを再生する。無効にすると即座に停止する。同行者にはボイスを付けない。','사토리와 코이시의 전투 말풍선에 일본어 음성을 재생합니다. 끄면 즉시 중지됩니다. 동료 말풍선은 음성이 없습니다.'),
    'DIALOGUE_VOICE_VOLUME.title':('对话语音音量','Dialogue voice volume','会話ボイス音量','대화 음성 음량'),
    'DIALOGUE_VOICE_VOLUME.hover.desc':('同时遵守游戏总音量和音效音量。对话期间音乐轻微降低，避免与符卡喊名叠音。','Also follows the game’s master and SFX volume. Music is slightly lowered during speech; spell callouts do not overlap it.','ゲームのマスター・効果音の音量にも従う。会話中はBGMを少し下げ、スペルカードの掛け声と重ならないようにする。','게임 전체 및 효과음 음량도 따릅니다. 대화 중에는 음악을 조금 낮추며, 스펠 카드 음성과 겹치지 않습니다.'),
    'SHOW_FINAL_BATTLE_BGM_CREDIT.hover.desc':('播放器中显示音乐作者。关闭播放器时，在开场对白结束后短暂显示曲名与作者。','Show the artist in the music player. With the player hidden, briefly show title and artist after the opening dialogue.','プレーヤーに作者を表示。プレーヤー非表示時は冒頭の会話後に曲名と作者を短く表示する。','플레이어에 음악 제작자를 표시합니다. 플레이어를 숨긴 경우 도입 대화 후 곡명과 제작자를 잠시 표시합니다.'),
    'SHOW_FINAL_BATTLE_MUSIC_PLAYER.title':('显示决战音乐播放器','Show final battle music player','最終戦の音楽プレーヤーを表示','최종전 음악 플레이어 표시'),
    'HIDE_KOISHI_INTENTS.title':('隐藏恋的意图','Hide Koishi’s intents','こいしの意図を隠す','코이시의 의도 숨기기'),
    'HIDE_KOISHI_INTENTS.hover.desc':('默认开启。恋的所有意图只显示一个？；行动执行时揭示。关闭后显示全部真实意图，取念机制保持不变。','On by default. Show one ? for all Koishi’s intents until execution. Turn off to show all true intents. Read Mind is unchanged.','初期設定はオン。こいしの全意図を1つの？で隠し、実行時に公開。オフにすると本来の意図をすべて表示。読心の効果は変わらない。','기본값은 켜짐입니다. 코이시의 모든 의도를 하나의 ?로 숨기고 실행 시 공개합니다. 끄면 실제 의도를 모두 표시합니다. 마음 읽기는 그대로입니다.'),
    'SHOW_FINAL_BATTLE_MUSIC_PLAYER.hover.title':('决战音乐播放器','Final battle music player','最終戦の音楽プレーヤー','최종전 음악 플레이어'),
    'SHOW_FINAL_BATTLE_MUSIC_PLAYER.hover.desc':('仅恋决战显示上一首、暂停和下一首。开场对白后开始，歌单顺序循环，战斗结束停止。可拖动曲名或背景移动。','Previous, pause and next controls only in Koishi’s final battle. Music begins after the opening dialogue, cycles through the playlist in order and stops at combat end. Drag the title or background to move.','こいしとの最終戦のみ、前の曲・一時停止・次の曲を表示。冒頭の会話後に曲を順番に繰り返し、戦闘終了時に停止。曲名や背景をドラッグして移動。','코이시 최종전에서만 이전 곡, 일시 정지, 다음 곡을 표시합니다. 도입 대화 후 목록을 순서대로 반복하고 전투 종료 시 멈춥니다. 곡명이나 배경을 드래그하여 이동합니다.'),
    'ENABLE_CHAPTER_PREVIEW.hover.desc':('仅限单人，默认关闭。开启后前三幕火堆增加直达第四章的体验选项。会跳过余下章节，建议另开体验局；正常攻略无需开启。','Solo only, off by default. Adds an Act 4 shortcut at rest sites in Acts 1–3. Skips remaining acts; use a separate preview run.','ソロ専用、初期設定はオフ。第1～3幕の休憩所から第4章へ進む。残りの幕を飛ばすため、体験用の冒険で使うこと。','1인 전용이며 기본값은 꺼짐입니다. 1~3막 휴식 장소에 4장 바로 가기를 추가합니다. 남은 막을 생략하므로 체험용 모험에서 사용하세요.'),
}.items(): add('settings_ui','SATORICHARACTER-'+key,*text)

for lang,tables in data.items():
    for table,entries in tables.items():
        path=LOC/lang/(table+'.json'); path.parent.mkdir(parents=True,exist_ok=True)
        existing=json.loads(path.read_text(encoding='utf-8')) if path.exists() else {}
        existing.update(entries)
        path.write_text(json.dumps(existing,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
(ROOT/'docs'/'ACT4_LOCALIZATION.json').write_text(json.dumps(data,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print('Fourth chapter localization:',sum(len(entries) for entries in data['zhs'].values()),'keys × 4 languages')

