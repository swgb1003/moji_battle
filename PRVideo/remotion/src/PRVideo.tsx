import React, {useEffect, useState} from 'react';
import {
	AbsoluteFill,
	Audio,
	continueRender,
	delayRender,
	Easing,
	Img,
	interpolate,
	OffthreadVideo,
	Sequence,
	spring,
	staticFile,
	useCurrentFrame,
	useVideoConfig,
} from 'remotion';
import {BEATS} from './beats';
import {HitShot, type Moment} from './hit';
import {MARKS, MOMENTS} from './moments';
import {Band, BLUE, BRUSH, clamp, Flash, GOLD, GOTHIC, Grain, INK, InkBlot, InkWipe, Letterbox, PAPER, Paper, RED, Slam, SpeedLines, Stamp, useShake} from './fx';

export const FPS = 30;
export const TOTAL = 90 * FPS;
const fr = (s: number) => Math.round(s * FPS);
/** Beat time (s) by index in the edited music. */
const bt = (i: number) => BEATS[i];
/** Downbeat (bar start) k: the drop at 2.16s is bar 0. */
const db = (k: number) => BEATS[5 + 4 * k];
const beatIndex = (k: number) => 5 + 4 * k;
/** The bass returns on bar 40 after the break. */
const CLIMAX = db(40);

/** Frame of the n-th event `label` (first column after the frame) in a scripted clip. */
const mark = (clip: string, label: string, n = 0): number => {
	const rows = (MARKS[clip] ?? []).filter((r) => r[1] === label);
	if (rows.length <= n) throw new Error(`no mark ${label} in ${clip}`);
	return rows[n][0] as number;
};
/** Frames of all rows whose label starts with one of the prefixes. */
const marksOf = (clip: string, ...prefixes: string[]) =>
	(MARKS[clip] ?? []).filter((r) => prefixes.some((p) => String(r[1]).startsWith(p))).map((r) => [r[0] as number, String(r[1])] as [number, string]);

// Highlights of the recorded 一 vs 木 matches: the KO, the three biggest hits, then a varied montage.
const byScore = [...MOMENTS].sort((a, b) => b.score - a.score);
const KO: Moment = byScore.find((m) => m.ko)!;
const CLIMAX_HITS: Moment[] = [];
for (const m of byScore) if (!m.ko && CLIMAX_HITS.length < 3 && !CLIMAX_HITS.some((c) => c.clip === m.clip)) CLIMAX_HITS.push(m);
const MONTAGE: Moment[] = [];
for (const m of byScore) {
	if (MONTAGE.length >= 8 || m.ko || CLIMAX_HITS.includes(m)) continue;
	if (MONTAGE.some((x) => x.label === m.label) || MONTAGE.filter((x) => x.clip === m.clip).length >= 2) continue;
	MONTAGE.push(m);
}
for (const m of byScore) if (MONTAGE.length < 8 && !m.ko && !CLIMAX_HITS.includes(m) && !MONTAGE.includes(m)) MONTAGE.push(m);
const SE_FOR: Record<string, string> = {Thrust: 'counter_hit', DashSlash: 'dash_hit', Leap: 'hit_heavy_01', StrokeShot: 'dash_hit', Smash: 'hit_heavy_02', Throw: 'hit_heavy_02'};
const seFor = (m: Moment) => (m.critical ? 'critical' : SE_FOR[m.style] ?? 'hit_heavy_01');

const useFonts = () => {
	const [handle] = useState(() => delayRender('fonts'));
	useEffect(() => {
		const faces = [new FontFace(BRUSH, `url(${staticFile('fonts/YujiBoku-Regular.ttf')})`), new FontFace(GOTHIC, `url(${staticFile('fonts/NotoSansJP-Bold.ttf')})`)];
		Promise.all(faces.map((f) => f.load())).then((loaded) => {
			loaded.forEach((f) => document.fonts.add(f));
			continueRender(handle);
		});
	}, [handle]);
};

/** Place children from time a to b (seconds). */
const At: React.FC<{a: number; b: number; children: React.ReactNode; name?: string}> = ({a, b, children, name}) => (
	<Sequence from={fr(a)} durationInFrames={Math.max(1, fr(b) - fr(a))} name={name}>
		{children}
	</Sequence>
);

/** Full-screen footage with zoom about a focus point and camera shake. */
const Shot: React.FC<{
	clip: string;
	src: number;
	dur: number;
	rate?: number;
	zoom?: [number, number];
	cx?: number;
	cy?: number;
	impacts?: number[];
	filter?: string;
}> = ({clip, src, dur, rate = 1, zoom = [1, 1], cx = 960, cy = 540, impacts = [], filter}) => {
	const frame = useCurrentFrame();
	// Slight overscan when shaking so the frame edges never show.
	const z = interpolate(frame, [0, dur], zoom, clamp) * (impacts.length ? 1.04 : 1);
	const shake = useShake(impacts);
	// Keep the focus shift within what the zoom leaves room for, so no black edge appears.
	const room = Math.max(0, 1 - 1 / z);
	// Battle footage: keep the spectator bar at the bottom of the screen out of frame.
	if (/^(tech_|battle_|pick_)/.test(clip) && z > 1.3) cy = Math.min(985 - 540 / z, Math.max(232 + 540 / z, cy));
	const tx = Math.max(-960 * room, Math.min(960 * room, 960 - cx));
	const ty = Math.max(-540 * room, Math.min(540 * room, 540 - cy));
	return (
		<AbsoluteFill style={{overflow: 'hidden', backgroundColor: '#000'}}>
			<AbsoluteFill style={{transform: `${shake} scale(${z}) translate(${tx}px, ${ty}px)`, filter}}>
				<OffthreadVideo src={staticFile(`footage/${clip}.mp4`)} trimBefore={src} playbackRate={rate} muted style={{width: '100%', height: '100%'}} />
			</AbsoluteFill>
		</AbsoluteFill>
	);
};

/** UI footage framed like a mounted print, sliding in. */
const Card: React.FC<{clip: string; src: number; rate?: number; x: number; y: number; scale: number; rotate?: number; enter?: boolean}> = ({clip, src, rate = 1, x, y, scale, rotate = -1.5, enter = true}) => {
	const frame = useCurrentFrame();
	const {fps} = useVideoConfig();
	const s = enter ? spring({frame, fps, config: {damping: 16, stiffness: 170}}) : 1;
	const dx = interpolate(s, [0, 1], [700, 0]);
	const r = interpolate(s, [0, 1], [8, rotate]);
	return (
		<div
			style={{
				position: 'absolute',
				left: x - 960,
				top: y - 540,
				width: 1920,
				height: 1080,
				transform: `translateX(${dx}px) rotate(${r}deg) scale(${scale})`,
				boxShadow: '0 40px 90px rgba(0,0,0,0.55)',
				outline: `${12 / scale}px solid ${INK}`,
				backgroundColor: PAPER,
			}}
		>
			<OffthreadVideo src={staticFile(`footage/${clip}.mp4`)} trimBefore={src} playbackRate={rate} muted style={{width: '100%', height: '100%'}} />
		</div>
	);
};

const Se: React.FC<{at: number; file: string; volume?: number}> = ({at, file, volume = 0.55}) => (
	<Sequence from={fr(at)} durationInFrames={fr(3)}>
		<Audio src={staticFile(`se/${file}.wav`)} volume={volume} />
	</Sequence>
);

/** Ink-dark ground with drifting texture. */
const InkGround: React.FC = () => {
	const frame = useCurrentFrame();
	return (
		<AbsoluteFill style={{backgroundColor: '#0d0b09'}}>
			<Img src={staticFile('img/design_ink.png')} style={{position: 'absolute', width: 2600, height: 1080, left: -200 - frame * 1.2, opacity: 0.18, mixBlendMode: 'screen'}} />
		</AbsoluteFill>
	);
};

// ---------------------------------------------------------------- 0: opening
const Opening: React.FC = () => {
	const frame = useCurrentFrame();
	return (
		<AbsoluteFill>
			<InkGround />
			<AbsoluteFill style={{transform: useShake([4, 26], 10, 6)}}>
				<Slam text="最強の文字は" at={4} size={130} color={PAPER} x={960} y={410} />
				<Slam text="どいつだ...??" at={26} size={170} color={PAPER} x={960} y={640} band={RED} />
			</AbsoluteFill>
			<AbsoluteFill style={{backgroundColor: '#fff', opacity: interpolate(frame, [fr(db(0)) - 5, fr(db(0))], [0, 0.7], clamp)}} />
		</AbsoluteFill>
	);
};

// ---------------------------------------------------------------- 1: title
const Title: React.FC<{dur: number}> = ({dur}) => (
	<AbsoluteFill>
		<Shot clip="title" src={0} dur={dur} zoom={[1.0, 1.07]} cx={960} cy={470} impacts={[2]} />
		<SpeedLines at={0} length={12} color={INK} cy={420} />
		<Flash at={0} length={10} />
	</AbsoluteFill>
);

// ---------------------------------------------------------------- 2: customize
const GROUP: Record<string, string> = {char: '文字', size: 'サイズ', font: '字体', grip: '持ち方'};
const VALUE: Record<string, string> = {Mincho: '明朝', RoundedGothic: '丸ゴシック', Brush: '筆文字', OneHanded: '片手', Reverse: '逆手', Horizontal: '横持ち', TwoHanded: '両手'};
const CUSTOM_CHANGES: [number, string, string][] = marksOf('customize', 'char ', 'size ', 'font ', 'grip ').map(([f, label]) => {
	const [kind, value] = label.split(' ');
	return [f, GROUP[kind], VALUE[value] ?? value];
});
const Customize: React.FC = () => {
	const start = db(2);
	return (
		<AbsoluteFill>
			<Paper />
			{CUSTOM_CHANGES.map(([src, group, value], i) => {
				const a = bt(beatIndex(2) + i), b = bt(beatIndex(2) + i + 1);
				return (
					<At key={i} a={a - start} b={b - start}>
						<Card clip="customize" src={src + 2} x={1185} y={560} scale={0.7} enter={i === 0} />
						<Stamp text={value.length <= 2 ? value : value.slice(0, 1)} x={1745} y={250} size={value.length <= 2 ? 150 : 130} at={0} />
						<Band text={`${group}  ${value}`} x={1185} y={985} size={40} at={0} />
					</At>
				);
			})}
			<Slam text="字の形が、" vertical at={4} size={116} x={430} y={430} />
			<Slam text="そのまま性能に。" vertical at={20} size={100} x={235} y={590} color={PAPER} band={RED} />
		</AbsoluteFill>
	);
};

// ---------------------------------------------------------------- 3: fusion (bars 6-12)
const BIRTH_SRC = mark('fusion', 'birth'), BIRTH_IMPACT = BIRTH_SRC + 37, BIRTH_END = mark('fusion', 'finish');
const FUSION_LAYOUT = mark('fusion', 'click ToLayout'), FUSION_RIGHT = mark('fusion', 'right 忍');
const birthStart = () => db(9) - (BIRTH_IMPACT - BIRTH_SRC) / FPS;
const Fusion: React.FC = () => {
	const t0 = db(6);
	const bs = birthStart(), be = bs + (BIRTH_END - BIRTH_SRC) / FPS;
	const rel = (s: number) => s - t0;
	return (
		<AbsoluteFill>
			<At a={0} b={rel(db(7))}>
				<Paper />
				<Card clip="fusion" src={30} rate={(FUSION_RIGHT + 10 - 30) / ((db(7) - db(6)) * FPS)} x={1130} y={560} scale={0.76} />
				<Slam text="部品を選んで" vertical at={4} size={116} x={300} y={520} />
			</At>
			<At a={rel(db(7))} b={rel(bs)}>
				<Paper />
				<Card clip="fusion" src={FUSION_LAYOUT} rate={(BIRTH_SRC - FUSION_LAYOUT) / ((bs - db(7)) * FPS)} x={1130} y={560} scale={0.76} enter={false} />
				<Slam text="組み合わせる" vertical at={0} size={110} x={300} y={520} color={PAPER} band={RED} />
			</At>
			<At a={rel(bs)} b={rel(be)}>
				<Shot clip="fusion" src={BIRTH_SRC} dur={fr(be - bs)} zoom={[1.0, 1.12]} cx={960} cy={480} impacts={[BIRTH_IMPACT - BIRTH_SRC]} />
				<Flash at={BIRTH_IMPACT - BIRTH_SRC} length={9} color={GOLD} peak={0.6} />
			</At>
			<At a={rel(be)} b={rel(db(12))}>
				<Paper />
				<Card clip="fusion" src={BIRTH_END + 2} x={960} y={720} scale={0.6} rotate={1} />
				<Slam text="文字を組み合わせて" at={2} size={104} x={960} y={140} />
				<Slam text="君だけの漢字を作ろう！" at={fr(bt(beatIndex(11)) - be)} size={112} x={960} y={290} color={PAPER} band={RED} />
			</At>
		</AbsoluteFill>
	);
};

// ---------------------------------------------------------------- 4: modes (bars 12-20)
const MODES: {clip: string; num: string; name: string; line: string}[] = [
	{clip: 'mode_battle', num: '壱', name: 'バトル', line: '文字を自由にカスタマイズして対決'},
	{clip: 'mode_tournament', num: '弐', name: 'トーナメント', line: 'ワールド文字カップ、48文字の頂点へ'},
	{clip: 'mode_challenge', num: '参', name: 'お題バトル', line: '条件を攻略して腕を試す'},
	{clip: 'mode_draft', num: '肆', name: '文字ドラフト', line: '候補から選んだ文字で戦う'},
	{clip: 'mode_run', num: '伍', name: '勝ち抜き・育成', line: '一文字を育て、五連戦を突破'},
	{clip: 'mode_practice', num: '陸', name: '練習', line: '自分で操作して技を試す'},
	{clip: 'mode_friend', num: '漆', name: '友人と対戦', line: 'ルームコードでオンライン対戦'},
	{clip: 'mode_achievements', num: '捌', name: '称号', line: '77種類を集める'},
];
const MODE_BAR = 12;
const Modes: React.FC = () => {
	const t0 = db(MODE_BAR);
	return (
		<AbsoluteFill>
			<Paper />
			{MODES.map((m, i) => (
				<At key={m.clip} a={db(MODE_BAR + i) - t0} b={db(MODE_BAR + 1 + i) - t0}>
					<Card clip={m.clip} src={4} x={1335} y={545} scale={0.56} rotate={i % 2 ? 1.5 : -1.5} />
					<Stamp text={m.num} x={190} y={250} size={140} at={0} rotate={-10} />
					<Slam text={m.name} at={2} size={Math.min(130, 620 / m.name.length)} x={430} y={470} color={PAPER} band={INK} seed={`m${i}`} />
					<Band text={m.line} at={8} x={430} y={650} size={m.line.length > 15 ? 30 : 34} />
				</At>
			))}
			<div style={{position: 'absolute', left: 60, top: 860, fontFamily: GOTHIC, fontSize: 34, color: INK, letterSpacing: 8, opacity: 0.75}}>遊び方は 8 つ</div>
		</AbsoluteFill>
	);
};

// ---------------------------------------------------------------- 5: stages (bars 20-22)
const Stages: React.FC = () => {
	const dur = db(22) - db(20);
	return (
		<AbsoluteFill>
			<Paper />
			<Card clip="stage_select" src={12} rate={(mark('stage_select', 'click StageCard0') - 12) / (dur * FPS)} x={1180} y={560} scale={0.74} />
			<Slam text="多彩なステージ" vertical at={3} size={112} x={290} y={540} color={PAPER} band={RED} />
		</AbsoluteFill>
	);
};

// ---------------------------------------------------------------- 6: AI special moves (bars 22-27)
const AI_TYPE = [mark('ai_special', 'click SpecialMoves') - 6, mark('ai_special', 'generate')], AI_GEN = [mark('ai_special', 'generate'), mark('ai_special', 'generated')], AI_DONE = mark('ai_special', 'generated') + 2;
const SPECIAL_AT = mark('battle_special', 'special');
const SPECIAL_HIT = marksOf('battle_special', 'hit').map(([f]) => f).find((f) => f > SPECIAL_AT + 20) ?? SPECIAL_AT + 50;
const AiSpecial: React.FC = () => {
	const t0 = db(22);
	const typeEnd = 2.1, genEnd = typeEnd + 1.2;
	const panel = {zoom: [1.3, 1.38] as [number, number], cx: 1150, cy: 520};
	return (
		<AbsoluteFill>
			<At a={0} b={typeEnd}>
				<Shot clip="ai_special" src={AI_TYPE[0]} rate={(AI_TYPE[1] - AI_TYPE[0]) / (typeEnd * FPS)} dur={fr(typeEnd)} {...panel} />
				<Slam text="技名を付ければ、" at={6} size={96} x={960} y={950} color={PAPER} band={INK} />
			</At>
			<At a={typeEnd} b={genEnd}>
				<Shot clip="ai_special" src={AI_GEN[0]} rate={(AI_GEN[1] - AI_GEN[0]) / ((genEnd - typeEnd) * FPS)} dur={fr(genEnd - typeEnd)} {...panel} />
				<Slam text="AI が技を作る。" at={2} size={104} x={960} y={950} color={PAPER} band={RED} />
			</At>
			<At a={genEnd} b={db(25) - t0}>
				<Shot clip="ai_special" src={AI_DONE} dur={fr(db(25) - t0 - genEnd)} zoom={[1.42, 1.5]} cx={760} cy={430} impacts={[1]} />
				<Flash at={0} length={8} peak={0.5} />
				<Slam text="AI が技を作る。" at={0} size={104} x={960} y={950} color={PAPER} band={RED} />
			</At>
			<At a={db(25) - t0} b={db(26) - t0}>
				<Shot clip="battle_special" src={SPECIAL_AT - 6} dur={fr(db(26) - db(25))} zoom={[1.0, 1.08]} />
			</At>
			<At a={db(26) - t0} b={db(27) - t0}>
				<Shot clip="battle_special" src={SPECIAL_HIT - fr(bt(beatIndex(26) + 1) - db(26))} dur={fr(db(27) - db(26))} zoom={[1.56, 1.46]} cx={960} cy={610} impacts={[fr(bt(beatIndex(26) + 1) - db(26))]} />
				<SpeedLines at={fr(bt(beatIndex(26) + 1) - db(26))} length={8} cy={640} />
				<Letterbox size={70} />
			</At>
		</AbsoluteFill>
	);
};

// ---------------------------------------------------------------- 7: watch or control (bars 27-29)
const WatchOrPlay: React.FC = () => {
	const t0 = db(27);
	return (
		<AbsoluteFill>
			<At a={0} b={db(28) - t0}>
				<Shot clip={MONTAGE[0].clip} src={140} dur={fr(db(28) - t0)} zoom={[1.46, 1.55]} cx={960} cy={610} filter="saturate(0.9)" />
				<AbsoluteFill style={{backgroundColor: 'rgba(0,0,0,0.2)'}} />
				<Slam text="観るも、" at={2} size={170} color={PAPER} x={960} y={200} band={INK} />
			</At>
			<At a={db(28) - t0} b={db(29) - t0}>
				<Shot clip="worldcup_final_vs" src={60} dur={fr(db(29) - db(28))} zoom={[2.0, 2.25]} cx={960} cy={276} />
				<AbsoluteFill style={{backgroundColor: 'rgba(0,0,0,0.15)'}} />
				<Slam text="操るも。" at={2} size={180} color={PAPER} x={960} y={790} band={RED} />
			</At>
		</AbsoluteFill>
	);
};

// ---------------------------------------------------------------- 8: technique montage (bars 29-37): 一 performs, 木 receives
const TECH_BAR = 29;
const Techniques: React.FC = () => {
	const t0 = db(TECH_BAR);
	return (
		<AbsoluteFill>
			{MONTAGE.map((m, i) => {
				const a = db(TECH_BAR + i), b = db(TECH_BAR + 1 + i);
				const lead = fr(bt(beatIndex(TECH_BAR + i) + 1) - a);
				return (
					<At key={i} a={a - t0} b={b - t0}>
						<HitShot m={m} dur={fr(b - a)} lead={lead} />
						<SpeedLines at={lead} length={8} cx={960} cy={560} />
						<Flash at={lead} length={5} peak={0.45} />
						<Letterbox size={70} />
						<Slam text={m.label} at={lead - 3} size={118} color={PAPER} x={i % 2 ? 1400 : 520} y={850} band={INK} seed={`t${i}`} />
					</At>
				);
			})}
			<At a={0} b={db(TECH_BAR + 2) - t0}>
				<Slam text="多彩な技を極めよう！" at={2} size={112} color={PAPER} x={960} y={190} band={RED} />
			</At>
		</AbsoluteFill>
	);
};

// ---------------------------------------------------------------- 9: resolve (bar 37 to the climax)
const Resolve: React.FC = () => {
	const frame = useCurrentFrame();
	const dur = fr(CLIMAX - db(37));
	const dark = interpolate(frame, [0, fr(0.6), dur - 18, dur], [0.15, 0.45, 0.45, 1], clamp);
	return (
		<AbsoluteFill>
			<Shot clip="worldcup_final_vs" src={0} dur={dur} rate={150 / dur} zoom={[1.0, 1.18]} cy={470} />
			<AbsoluteFill style={{backgroundColor: '#000', opacity: dark}} />
			<Slam text="覚悟はできたか？" at={fr(bt(beatIndex(37) + 1) - db(37))} size={150} color={PAPER} x={960} y={540} band={RED} />
		</AbsoluteFill>
	);
};

// ---------------------------------------------------------------- 10: climax (bar 40-49)
const Climax: React.FC = () => {
	const t0 = CLIMAX;
	const lead = (k: number) => fr(bt(beatIndex(k) + 1) - db(k));
	const koLead = fr(db(45) - db(44));
	const [c1, c2, c3] = CLIMAX_HITS;
	return (
		<AbsoluteFill>
			<At a={0} b={db(41) - t0}>
				<HitShot m={c1} dur={fr(db(41) - t0)} lead={2} zoom={1.4} slowFrames={26} />
				<SpeedLines at={2} length={12} cy={560} />
				<Flash at={0} length={10} />
			</At>
			<At a={db(41) - t0} b={db(42) - t0}>
				<HitShot m={c2} dur={fr(db(42) - db(41))} lead={lead(41)} />
				<Flash at={lead(41)} length={6} peak={0.5} />
			</At>
			<At a={db(42) - t0} b={db(44) - t0}>
				<HitShot m={c3} dur={fr(db(44) - db(42))} lead={lead(42)} slowFrames={40} />
				<SpeedLines at={lead(42)} length={8} cy={560} />
			</At>
			<At a={db(44) - t0} b={db(46) - t0}>
				<HitShot m={KO} dur={fr(db(46) - db(44))} lead={koLead} zoom={1.45} slowFrames={44} />
				<Flash at={koLead} length={12} color={RED} peak={0.45} />
				<SpeedLines at={koLead} length={14} cy={560} color={INK} />
			</At>
			<At a={db(46) - t0} b={db(47) - t0}>
				<Shot clip={KO.clip} src={KO.hit + 19} dur={fr(db(47) - db(46))} zoom={[1.46, 1.6]} cx={960 + (KO.x - 960) * 0.5} cy={610} filter="contrast(1.1) saturate(1.1)" />
				<Letterbox size={110} />
			</At>
			<At a={db(47) - t0} b={db(49) - t0}>
				<Shot clip="worldcup_ceremony" src={200} dur={fr(db(49) - db(47))} zoom={[1.0, 1.1]} cy={470} />
				<AbsoluteFill style={{background: 'linear-gradient(rgba(0,0,0,0) 55%, rgba(0,0,0,0.75))'}} />
				<Slam text="頂点に立てるのはただ一文字" at={4} size={98} color={GOLD} x={960} y={905} band={INK} />
			</At>
		</AbsoluteFill>
	);
};

// ---------------------------------------------------------------- 11: end card
const EndCard: React.FC = () => {
	const frame = useCurrentFrame();
	const {fps} = useVideoConfig();
	const s = spring({frame, fps, config: {damping: 12, stiffness: 240, mass: 0.6}});
	const urlAt = fr(db(50) - db(49));
	return (
		<AbsoluteFill>
			<Paper />
			<AbsoluteFill style={{transform: useShake([2], 18, 10)}}>
				<Img
					src={staticFile('img/logo_alpha.png')}
					style={{position: 'absolute', left: 960 - 1075 * 0.62, top: 380 - 440 * 0.62, width: 1075 * 1.24, height: 440 * 1.24, transform: `scale(${interpolate(s, [0, 1], [2.4, 1])})`, opacity: interpolate(frame, [0, 3], [0, 1], clamp)}}
				/>
			</AbsoluteFill>
			<Band text="ブラウザで今すぐ遊べる" at={fr(0.5)} x={960} y={760} size={56} />
			<Sequence from={urlAt}>
				<div
					style={{
						position: 'absolute',
						left: 0,
						right: 0,
						top: 850,
						textAlign: 'center',
						fontFamily: GOTHIC,
						fontSize: 72,
						color: RED,
						letterSpacing: 2,
						opacity: interpolate(frame - urlAt, [0, 6], [0, 1], clamp),
						transform: `translateY(${interpolate(frame - urlAt, [0, 8], [24, 0], {...clamp, easing: Easing.out(Easing.cubic)})}px)`,
					}}
				>
					moji-fighter-x.vercel.app
				</div>
			</Sequence>
			<Flash at={0} length={8} />
		</AbsoluteFill>
	);
};

export const PRVideo: React.FC = () => {
	useFonts();
	const wipe = (s: number, dir: 1 | -1 = 1) => (
		<Sequence from={fr(s) - 8} durationInFrames={16}>
			<InkWipe length={16} dir={dir} />
		</Sequence>
	);
	const bs = birthStart();
	return (
		<AbsoluteFill style={{backgroundColor: '#000'}}>
			<At a={0} b={db(0)} name="0 opening"><Opening /></At>
			<At a={db(0)} b={db(2)} name="1 title"><Title dur={fr(db(2) - db(0))} /></At>
			<At a={db(2)} b={db(6)} name="2 customize"><Customize /></At>
			<At a={db(6)} b={db(12)} name="3 fusion"><Fusion /></At>
			<At a={db(12)} b={db(20)} name="4 modes"><Modes /></At>
			<At a={db(20)} b={db(22)} name="5 stages"><Stages /></At>
			<At a={db(22)} b={db(27)} name="6 AI specials"><AiSpecial /></At>
			<At a={db(27)} b={db(29)} name="7 watch/play"><WatchOrPlay /></At>
			<At a={db(29)} b={db(37)} name="8 techniques"><Techniques /></At>
			<At a={db(37)} b={CLIMAX} name="9 resolve"><Resolve /></At>
			<At a={CLIMAX} b={db(49)} name="10 climax"><Climax /></At>
			<At a={db(49)} b={90} name="11 end"><EndCard /></At>

			{wipe(db(2))}
			{wipe(db(6), -1)}
			{wipe(db(12))}
			{wipe(db(20), -1)}
			{wipe(db(22))}
			{wipe(db(29), -1)}

			<Grain />

			<Audio src={staticFile('music.wav')} />
			<Se at={db(0)} file="hit_heavy_01" volume={0.6} />
			<Se at={bs} file="charge" volume={0.35} />
			<Se at={db(9)} file="critical" volume={0.45} />
			<Se at={db(22) + 3.3} file="critical" volume={0.4} />
			<Se at={bt(beatIndex(26) + 1)} file="dash_hit" volume={0.5} />
			{MONTAGE.map((m, i) => <Se key={i} at={bt(beatIndex(TECH_BAR + i) + 1)} file={seFor(m)} volume={0.55} />)}
			<Se at={CLIMAX + 0.1} file="hit_heavy_02" volume={0.65} />
			<Se at={CLIMAX + 0.07} file={seFor(CLIMAX_HITS[0])} volume={0.55} />
			<Se at={bt(beatIndex(41) + 1)} file={seFor(CLIMAX_HITS[1])} volume={0.55} />
			<Se at={bt(beatIndex(42) + 1)} file={seFor(CLIMAX_HITS[2])} volume={0.55} />
			<Se at={db(45)} file="ko" volume={0.7} />
			<Se at={db(49)} file="hit_heavy_02" volume={0.6} />
		</AbsoluteFill>
	);
};
