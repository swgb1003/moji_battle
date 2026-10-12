# お題バトル：画面デザイン案

作成日：2026-10-10。組み込み imagegen ツールで生成した画像モックアップです。ゲームへの実装・デプロイは行っていません。

遷移：メニュー → お題一覧 → お題の詳細 → 対戦準備 → バトル → 結果。

結果からは、構成を変えて再挑戦／同じ構成で再戦／次のお題の詳細／お題一覧へ進みます。

「一文字の可能性」を例に、文字「一」を固定し、字体・サイズ・握る位置・持ち方・戦い方・反転・必殺技を調整する案です。相手は「刀」・M・両手・カウンター・剣舞陣。称号「一の使い手」は報酬の提案です。タイトル画面への入口追加はありません。

画像中のステータス数値・難易度は説明用です。実装時は実際のゲームデータを表示し、既存の必殺技名「一閃」を使用します。

## 1. メニュー

画像：[01_menu.png](01_menu.png)

参考画像：Reports/UI/02_mode_select.png

生成プロンプト：

```text
Use case: ui-mockup. Create ONE polished full-screen 16:9 Japanese Unity game UI concept, 1920x1080 composition, not a contact sheet, no devices or perspective. Match the supplied reference game's visual system exactly: lightly textured ivory washi paper, near-black ink typography, fine double-line rectangular borders with pixel-like square corner ornaments, vermilion red primary actions, muted indigo opponent accents, no rounded cards, no gradients, no photo backgrounds. Bold clean readable Japanese typography, generous consistent margins, complete unclipped controls. Game brand MOJI Fighter X top right. This is a proposed new challenge mode in the existing game. Stick figures hold oversized colored Japanese glyphs as physical weapons. No swords replacing the glyphs, no fantasy people. Clear, deliberate hierarchy. Screen 1 of 6: mode selection menu. Header '01 / MODE SELECT', black upper-left '← 戻る', small upper-right '⚙ 設定'. Large title '遊び方を選ぶ'. Rebalance existing two large cards into THREE equal width main cards in a single row, each containing simple ink art and generous action button: '01 バトル' red-vs-indigo stickman glyph duel, caption '自由に組んで、対戦する', button 'バトルへ →'; '02 トーナメント' trophy and bracket, caption '8人の大会で優勝を目指す', button '大会へ →'; '03 お題バトル' large vermilion '一' and indigo '刀' glyphs, small red 'NEW' tab, caption '条件を攻略して、腕を試す', vermilion button 'お題を選ぶ →'. Two shallow secondary cards in bottom row: '友人と対戦' connection icon and button '対戦へ →'; '称号' badge and '獲得 22 / 50', button '称号を見る →'. Highlight new challenge card subtly with red outline. This is a menu, NOT the title screen. Match reference shell and paper closely.
```

## 2. お題一覧

画像：[02_list.png](02_list.png)

参考画像：Reports/UI/02_mode_select.png

生成プロンプト：

```text
Use case: ui-mockup. Create ONE polished full-screen 16:9 Japanese Unity game UI concept, 1920x1080 composition, not a contact sheet, no devices or perspective. Match the supplied reference game's visual system exactly: lightly textured ivory washi paper, near-black ink typography, fine double-line rectangular borders with pixel-like square corner ornaments, vermilion red primary actions, muted indigo opponent accents, no rounded cards, no gradients, no photo backgrounds. Bold clean readable Japanese typography, generous consistent margins, complete unclipped controls. Game brand MOJI Fighter X top right. This is a proposed new challenge mode in the existing game. Stick figures hold oversized colored Japanese glyphs as physical weapons. No swords replacing the glyphs, no fantasy people. Clear, deliberate hierarchy. Screen 2 of 6: challenge list. Header 'CHALLENGE / SELECT', black upper-left '← メニュー'. Large title 'お題バトル', subtitle '文字と構成で、勝ち筋を見つけよう'. Upper-right small progress 'クリア 2 / 6' with vermilion progress bar. Filter tabs 'すべて' selected black, '未クリア', 'クリア済み'. Main area 3 columns by 2 rows, six large bordered cards. Each card has clear ordinal, big glyph-based emblem, task name, difficulty dots, and short constraint summary. Card1 '#01 一文字の可能性', red '一' versus blue '刀', '難易度 ●○○', '文字「一」で勝利する', red focus border and small '挑戦する →'. Card2 '#02 小よく大を制す', small '小' versus huge '大', '難易度 ●○○', 'SサイズでXLに挑む', small red 'CLEAR' stamp. Card3 '#03 鉄壁突破', blue '盾', '難易度 ●●○', '守りの堅い相手を倒す', red 'CLEAR' stamp. Card4 '#04 一撃の美学', red brush impact emblem, '難易度 ●●○', '必殺技でとどめを刺す'. Card5 '#05 声援の力', cheering ink emblem, '難易度 ●●○', '声援を使って勝利する'. Card6 '#06 挑戦者の証', crest emblem, '難易度 ●●●', '強敵「龍」を倒す'. No padlocks; every task available. Bottom hint 'お題を選ぶと、相手と条件を確認できます'. Match existing achievement cards aesthetic, prioritize Japanese legibility.
```

## 3. お題の詳細

画像：[03_detail.png](03_detail.png)

参考画像：Reports/UI/02_mode_select.png

生成プロンプト：

```text
Use case: ui-mockup. Create ONE polished full-screen 16:9 Japanese Unity game UI concept, 1920x1080 composition, not a contact sheet, no devices or perspective. Match the supplied reference game's visual system exactly: lightly textured ivory washi paper, near-black ink typography, fine double-line rectangular borders with pixel-like square corner ornaments, vermilion red primary actions, muted indigo opponent accents, no rounded cards, no gradients, no photo backgrounds. Bold clean readable Japanese typography, generous consistent margins, complete unclipped controls. Game brand MOJI Fighter X top right. This is a proposed new challenge mode in the existing game. Stick figures hold oversized colored Japanese glyphs as physical weapons. No swords replacing the glyphs, no fantasy people. Clear, deliberate hierarchy. Screen 3 of 6: challenge detail/briefing, same task #01 as list. Header 'CHALLENGE / BRIEFING', black upper-left '← お題一覧'. Main headline '一文字の可能性', small subheading '#01 / 難易度 ●○○'. Left half large thin-bordered versus preview with red stick figure physically wielding oversized single horizontal glyph '一' and indigo stick figure wielding '刀', black 'VS' centered. Under left player 'あなた / 一', under opponent '相手 / 刀'. Lower section of left panel clear opponent loadout intel '相手の構成' and structured row 'Mサイズ / 両手 / カウンター', next row '必殺技：剣舞陣'. Right half top card 'クリア条件' with high emphasis '文字「一」で相手に勝利する', next simple constraint box '使用文字　一 固定' and '字体・サイズ・持ち方・戦い方・必殺技は変更できます'. Middle card '攻略のヒント' body 'シンプルな形を活かして、握る位置や持ち方を試そう。' Bottom right initial-clear reward framed mini badge '初回クリア報酬', ink circular achievement-style crest, title '称号「一の使い手」'. Bottom footer secondary ink-outlined button 'お題一覧へ' on left; large vermilion primary button '構成をつくる →' on right. No battle running and no editable loadout controls yet. Authentic existing retro game screen.
```

## 4. 対戦準備

画像：[04_prepare.png](04_prepare.png)

参考画像：Reports/UI/03_customize_p1.png

生成プロンプト：

```text
Use case: ui-mockup. Create ONE polished full-screen 16:9 Japanese Unity game UI concept, 1920x1080 composition, not a contact sheet, no devices or perspective. Match the supplied reference game's visual system exactly: lightly textured ivory washi paper, near-black ink typography, fine double-line rectangular borders with pixel-like square corner ornaments, vermilion red primary actions, muted indigo opponent accents, no rounded cards, no gradients, no photo backgrounds. Bold clean readable Japanese typography, generous consistent margins, complete unclipped controls. Game brand MOJI Fighter X top right. This is a proposed new challenge mode in the existing game. Stick figures hold oversized colored Japanese glyphs as physical weapons. No swords replacing the glyphs, no fantasy people. Clear, deliberate hierarchy. Screen 4 of 6: challenge preparation / customizing one player, preserve the existing customize reference's left preview / right table composition. Header 'CHALLENGE / PREPARE', black upper-left '← お題の詳細'. Main title '挑戦の構成をつくる'. A shallow clear full-width condition strip under title with small red '#01 一文字の可能性', bold '条件：文字「一」で相手に勝利する', and right '相手：刀 / M / 両手 / カウンター'. Left panel heading 'あなた / PREVIEW', small button 'ガイド表示'. Preview black stickman standing on baseline, wielding large vermilion horizontal '一' glyph, grip marker small amber ring on glyph. Instruction '文字の上でドラッグ → 握る位置を決定'. Underneath modest stat meters labels '射程', '扱いやすさ', '攻撃', '防御'. Right panel heading 'あなたの構成'; no P2 editable tab (opponent locked). First table row '文字' and large '一' next to padlock and text 'お題指定'. Further rows segmented buttons: '字体' choices 'ゴシック' selected red, '明朝', '丸ゴシック', '筆文字'; 'サイズ' 'S', 'M' selected red, 'L', 'XL'; '持ち方' '片手', '両手' selected red, '逆手', '横持ち'; '戦い方' '猛攻', 'H&A', 'カウンター' selected red, '鉄壁'; '反転' 'なし' selected red, '左右', '上下', '180°'. Bottom row outlined button '必殺技：一閃 →'. Footer right big red action 'この構成で挑戦 →'; left smaller '条件を確認'. Make controls legible and comfortably spaced, using existing UI rather than inventing new editing features.
```

## 5. バトル

画像：[05_battle.png](05_battle.png)

参考画像：Reports/UI/battle_01_live.png

生成プロンプト：

```text
Use case: ui-mockup. Create ONE polished full-screen 16:9 Japanese Unity game UI concept, 1920x1080 composition, not a contact sheet, no devices or perspective. Match the supplied reference game's visual system exactly: lightly textured ivory washi paper, near-black ink typography, fine double-line rectangular borders with pixel-like square corner ornaments, vermilion red primary actions, muted indigo opponent accents, no rounded cards, no gradients, no photo backgrounds. Bold clean readable Japanese typography, generous consistent margins, complete unclipped controls. Game brand MOJI Fighter X top right. This is a proposed new challenge mode in the existing game. Stick figures hold oversized colored Japanese glyphs as physical weapons. No swords replacing the glyphs, no fantasy people. Clear, deliberate hierarchy. Screen 5 of 6: active challenge battle, preserve actual reference game's arena and UI: top health bars, smaller special and cheer gauges, giant kanji physical weapons held by black stickmen, faint huge circular ink brush in paper background, ground line near bottom. Header 'CHALLENGE / BATTLE', upper-left '← 戻る', upper-right '⚙ 設定'. Add compact horizontally centered mission banner below header '01 一文字の可能性 ｜ 条件：文字「一」で勝利'. Left HUD red glyph tile '一', player label 'あなた / 一 / カウンター', HP '68 / 100' red bar. Right HUD indigo glyph tile '刀', label 'COM / 刀 / カウンター', HP '35 / 100' indigo bar. Center 'TIME 42', small 'AUTO'. Below left health: labels '必殺' 100% red gauge and active primary small '必殺「一閃」 K', second '声援' partly filled gauge '回復中'. Right similarly '必殺' 72% indigo gauge and '声援' partially filled gauge '回復中'. Main arena two black stickmen actively clashing: left player's physical glyph '一' is exactly one long thick vermilion horizontal stroke, facing right; opponent glyph '刀' large clearly recognizable indigo facing left. Subtle ink collision lines, small floating damage '12'. Keep paper open and avoid decorative extra labels in arena. Bottom toolbar '▶ 対戦中', outlined '一時停止', speeds '0.5×', '1×' selected black, '2×', '4×', and '詳細'. No win or result text yet. Existing gameplay remains automatic with special and cheer intervention. No just-guard UI.
```

## 6. 結果

画像：[06_result.png](06_result.png)

参考画像：Reports/UI/02_mode_select.png

生成プロンプト：

```text
Use case: ui-mockup. Create ONE polished full-screen 16:9 Japanese Unity game UI concept, 1920x1080 composition, not a contact sheet, no devices or perspective. Match the supplied reference game's visual system exactly: lightly textured ivory washi paper, near-black ink typography, fine double-line rectangular borders with pixel-like square corner ornaments, vermilion red primary actions, muted indigo opponent accents, no rounded cards, no gradients, no photo backgrounds. Bold clean readable Japanese typography, generous consistent margins, complete unclipped controls. Game brand MOJI Fighter X top right. This is a proposed new challenge mode in the existing game. Stick figures hold oversized colored Japanese glyphs as physical weapons. No swords replacing the glyphs, no fantasy people. Clear, deliberate hierarchy. Screen 6 of 6: challenge success result, exact same #01 一文字の可能性, victorious player red glyph '一', defeated opponent indigo glyph '刀'. Header 'CHALLENGE / RESULT', upper-left '← お題一覧'. Top centered small '#01 一文字の可能性', huge vermilion brush-style 'お題クリア！' with tasteful distressed rectangular red 'CLEAR' stamp. Main content two balanced thin double-border panels. Left panel heading '対戦結果', prominent red stick figure raising single horizontal red glyph '一' and smaller subdued indigo defeated glyph '刀', clearly readable 'あなたの勝利', compact stats '決着時間 18秒' and '残りHP 68 / 100'. Beneath stats a checkmark row '✓ 文字「一」を使用' and '✓ 相手に勝利'. Right panel heading '初回クリア報酬', large circular ink badge with laurel/ribbon and single black horizontal stroke inside, small vermilion 'NEW', text '称号「一の使い手」' and '獲得しました'. Under reward progress 'お題クリア 3 / 6' vermilion half-filled bar. Bottom two-row action area: first row large ink-outline button '構成を変えて再挑戦' on left and equally large primary vermilion '次のお題へ →' on right; second row smaller outlined 'もう一度戦う' on left and 'お題一覧へ' on right. Hierarchy celebrates success but makes adapting loadout and advancing clear. No currency, no gacha, no intrusive unlock dialogs, no confetti. Spacious and readable Japanese game UI.
```

## バトル画面の最終調整プロンプト

```text
Edit this Japanese challenge battle UI mockup precisely, preserving its whole composition, paper texture, typography, corner borders, mission banner, fighting stickmen and giant glyph weapons. Correct the opponent HP gauge at upper right: numeric value is 35 / 100, so the indigo filled region must span exactly 35% of the gauge from its RIGHT edge (bar empties toward right), leaving 65% empty paper on the LEFT. Currently its fill is too long. Add the missing small disabled rectangular intervention button to the right of the red '必殺「一閃」 K' button within the left intervention panel, label '声援 L', pale paper-gray background and gray type because its gauge is recharging. Shorten the existing special button to fit both controls within the same existing panel width, keeping all labels readable. Special move name must be exactly '一閃' (閃 is gate with person inside). Keep every other element unchanged. Output the same single 16:9 full-screen image.
```

参照：生成済みのバトル画面。最終版は 05_battle.png。

