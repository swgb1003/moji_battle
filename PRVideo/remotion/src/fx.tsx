import React from 'react';
import {
	AbsoluteFill,
	Easing,
	interpolate,
	random,
	spring,
	staticFile,
	useCurrentFrame,
	useVideoConfig,
	Img,
} from 'remotion';

export const INK = '#15110e';
export const PAPER = '#eee4cf';
export const RED = '#c0281c';
export const BLUE = '#1f3c78';
export const GOLD = '#d4a838';
export const BRUSH = 'YujiBoku';
export const GOTHIC = 'NotoSansJPBold';

export const FontFaces: React.FC = () => (
	<style>{`
@font-face { font-family: '${BRUSH}'; src: url('${staticFile('fonts/YujiBoku-Regular.ttf')}'); }
@font-face { font-family: '${GOTHIC}'; src: url('${staticFile('fonts/NotoSansJP-Bold.ttf')}'); }
`}</style>
);

export const clamp = {extrapolateLeft: 'clamp', extrapolateRight: 'clamp'} as const;

/** Washi paper ground. */
export const Paper: React.FC<{dark?: number}> = ({dark = 0}) => (
	<AbsoluteFill style={{backgroundColor: PAPER}}>
		<Img src={staticFile('img/design_paper.png')} style={{width: '100%', height: '100%', objectFit: 'cover'}} />
		{dark > 0 && <AbsoluteFill style={{backgroundColor: INK, opacity: dark}} />}
	</AbsoluteFill>
);

/** Film grain and vignette over everything. */
export const Grain: React.FC<{opacity?: number}> = ({opacity = 0.16}) => {
	const frame = useCurrentFrame();
	const seed = Math.floor(frame / 2) % 8;
	return (
		<AbsoluteFill style={{pointerEvents: 'none'}}>
			<svg width="100%" height="100%" style={{position: 'absolute', opacity, mixBlendMode: 'overlay'}}>
				<filter id={`grain${seed}`}>
					<feTurbulence type="fractalNoise" baseFrequency="0.9" numOctaves="2" seed={seed} />
					<feColorMatrix type="saturate" values="0" />
				</filter>
				<rect width="100%" height="100%" filter={`url(#grain${seed})`} />
			</svg>
			<AbsoluteFill style={{background: 'radial-gradient(ellipse at center, rgba(0,0,0,0) 55%, rgba(0,0,0,0.55) 100%)'}} />
		</AbsoluteFill>
	);
};

/** Camera shake: offsets decay after each impact frame (relative to this sequence). */
export const useShake = (impacts: number[], strength = 22, length = 10) => {
	const frame = useCurrentFrame();
	let x = 0, y = 0, r = 0;
	for (const at of impacts) {
		const t = frame - at;
		if (t < 0 || t > length) continue;
		const k = (1 - t / length) ** 2 * strength;
		x += (random(`x${at}-${frame}`) - 0.5) * 2 * k;
		y += (random(`y${at}-${frame}`) - 0.5) * 2 * k;
		r += (random(`r${at}-${frame}`) - 0.5) * 0.06 * k;
	}
	return `translate(${x}px, ${y}px) rotate(${r}deg)`;
};

/** White (or red) flash that fades out. */
export const Flash: React.FC<{at?: number; length?: number; color?: string; peak?: number}> = ({at = 0, length = 8, color = '#fff', peak = 0.9}) => {
	const frame = useCurrentFrame();
	const o = interpolate(frame - at, [0, 1, length], [0, peak, 0], clamp);
	return <AbsoluteFill style={{backgroundColor: color, opacity: o, pointerEvents: 'none'}} />;
};

/** Ink blot made of deterministic splatter circles; grows in with `progress` (0..1). */
export const InkBlot: React.FC<{seed: string; size: number; progress: number; color?: string; x: number; y: number}> = ({seed, size, progress, color = INK, x, y}) => {
	const drops = Array.from({length: 26}, (_, i) => {
		const a = random(`${seed}a${i}`) * Math.PI * 2;
		const d = (i < 6 ? 0.15 : 0.35 + random(`${seed}d${i}`) * 0.75) * size;
		const r = (i < 6 ? 0.42 : 0.03 + random(`${seed}r${i}`) * 0.09) * size;
		const delay = i < 6 ? 0 : random(`${seed}t${i}`) * 0.4;
		const p = Math.max(0, Math.min(1, (progress - delay) / (1 - delay)));
		return {cx: Math.cos(a) * d * (0.6 + 0.4 * p), cy: Math.sin(a) * d * (0.6 + 0.4 * p), r: r * Easing.out(Easing.back(2))(p)};
	});
	return (
		<svg style={{position: 'absolute', left: x - size * 1.5, top: y - size * 1.5, overflow: 'visible'}} width={size * 3} height={size * 3}>
			<filter id={`rough${seed}`}>
				<feTurbulence type="fractalNoise" baseFrequency="0.035" numOctaves="3" seed={seed.length} />
				<feDisplacementMap in="SourceGraphic" scale={size * 0.12} />
			</filter>
			<g transform={`translate(${size * 1.5}, ${size * 1.5})`} filter={`url(#rough${seed})`} fill={color}>
				{drops.map((d, i) => (d.r > 0 ? <circle key={i} cx={d.cx} cy={d.cy} r={d.r} /> : null))}
			</g>
		</svg>
	);
};

/**
 * Ink wipe transition: a rough-edged ink band sweeps across, covering the cut at `mid` frames.
 * Place it at the cut point - mid.
 */
export const InkWipe: React.FC<{length?: number; color?: string; dir?: 1 | -1}> = ({length = 16, color = INK, dir = 1}) => {
	const frame = useCurrentFrame();
	const {width, height} = useVideoConfig();
	const p = interpolate(frame, [0, length], [0, 1], {...clamp, easing: Easing.inOut(Easing.cubic)});
	const lead = interpolate(p, [0, 0.5], [-0.25, 1.25], clamp);
	const tail = interpolate(p, [0.5, 1], [-0.25, 1.25], clamp);
	const x0 = Math.min(lead, tail) * width * 1.0;
	const x1 = Math.max(lead, tail) * width * 1.0;
	if (frame < 0 || frame > length) return null;
	const left = dir === 1 ? tail * width : width - lead * width;
	const w = (lead - tail) * width;
	return (
		<AbsoluteFill style={{pointerEvents: 'none'}}>
			<svg width={width} height={height} style={{position: 'absolute'}}>
				<filter id="wipeEdge" x="-20%" y="-20%" width="140%" height="140%">
					<feTurbulence type="fractalNoise" baseFrequency="0.012 0.06" numOctaves="4" seed={7} />
					<feDisplacementMap in="SourceGraphic" scale="160" xChannelSelector="R" yChannelSelector="G" />
				</filter>
				<rect x={left - 60} y={-120} width={Math.max(0, w) + 120} height={height + 240} fill={color} filter="url(#wipeEdge)" transform={`skewX(${-12 * dir})`} />
			</svg>
			<span style={{display: 'none'}}>{x0 + x1}</span>
		</AbsoluteFill>
	);
};

/** Approximate run length of a line of text in em. */
const textEm = (text: string) => [...text].reduce((n, c) => n + (c.charCodeAt(0) < 0x2000 ? 0.55 : c === '、' || c === '。' ? 0.75 : 1), 0);

/** Dry-brush ink stroke (horizontal or vertical) that paints itself in. */
export const BrushStroke: React.FC<{seed: string; x: number; y: number; w: number; h: number; progress: number; color?: string; vertical?: boolean}> = ({seed, x, y, w, h, progress, color = RED, vertical = false}) => {
	const W = vertical ? h : w, H = vertical ? w : h;
	const streaks = Array.from({length: 9}, (_, i) => ({
		y: (i + 0.5) / 9,
		th: 0.06 + random(`${seed}s${i}`) * 0.07,
		end: 0.86 + random(`${seed}e${i}`) * 0.14,
	}));
	const id = `brush${seed}`;
	return (
		<svg
			style={{position: 'absolute', left: x - w / 2, top: y - h / 2, overflow: 'visible'}}
			width={w}
			height={h}
		>
			<filter id={id} x="-10%" y="-30%" width="120%" height="160%">
				<feTurbulence type="fractalNoise" baseFrequency="0.02 0.09" numOctaves="3" seed={seed.length * 7} />
				<feDisplacementMap in="SourceGraphic" scale={Math.min(W, H) * 0.35} />
			</filter>
			<g filter={`url(#${id})`} transform={vertical ? `translate(${w}, 0) rotate(90)` : undefined}>
				<rect x={W * 0.02} y={H * 0.1} width={W * 0.96 * progress} height={H * 0.8} fill={color} rx={H * 0.2} />
				{streaks.map((s, i) => (
					<rect key={i} x={0} y={H * (s.y - s.th / 2)} width={W * s.end * progress} height={H * s.th} fill={color} />
				))}
			</g>
		</svg>
	);
};

/**
 * Big brush text that slams in from large scale.
 * `band` paints a brush stroke of that color behind the text; `outline` draws a contrasting edge.
 */
export const Slam: React.FC<{
	text: string;
	at?: number;
	size?: number;
	color?: string;
	x?: number;
	y?: number;
	font?: string;
	vertical?: boolean;
	band?: string;
	outline?: string;
	seed?: string;
}> = ({text, at = 0, size = 160, color = INK, x = 960, y = 540, font = BRUSH, vertical = false, band, outline, seed}) => {
	const frame = useCurrentFrame();
	const {fps} = useVideoConfig();
	const t = frame - at;
	if (t < 0) return null;
	const textAt = band ? 4 : 0;
	const s = spring({frame: t - textAt, fps, config: {damping: 13, stiffness: 260, mass: 0.6}});
	const scale = interpolate(s, [0, 1], [2.4, 1]);
	const opacity = interpolate(t - textAt, [0, 3], [0, 1], clamp);
	const len = textEm(text) * size;
	const o = Math.max(2, size * 0.028);
	const edge = outline
		? [[1, 0], [-1, 0], [0, 1], [0, -1], [0.7, 0.7], [-0.7, 0.7], [0.7, -0.7], [-0.7, -0.7]].map(([dx, dy]) => `${dx * o * 1.6}px ${dy * o * 1.6}px 0 ${outline}`).join(', ') + ', '
		: '';
	return (
		<>
			{band && (
				<BrushStroke
					seed={seed ?? text}
					x={x}
					y={y}
					w={vertical ? size * 1.7 : len + size * 1.3}
					h={vertical ? len + size * 1.1 : size * 1.55}
					vertical={vertical}
					progress={interpolate(t, [0, 7], [0, 1], {...clamp, easing: Easing.out(Easing.cubic)})}
					color={band}
				/>
			)}
			<div
				style={{
					position: 'absolute',
					left: x,
					top: y,
					transform: `translate(-50%, -50%) scale(${t < textAt ? 0 : scale})`,
					opacity: t < textAt ? 0 : opacity,
					fontFamily: font,
					fontSize: size,
					color,
					whiteSpace: 'nowrap',
					writingMode: vertical ? 'vertical-rl' : undefined,
					lineHeight: 1.05,
					WebkitTextStroke: `${o}px ${color}`,
					paintOrder: 'stroke fill',
					textShadow: `${edge}0 ${size * 0.05}px ${size * 0.15}px rgba(0,0,0,0.45)`,
				}}
			>
				{text}
			</div>
		</>
	);
};

/** Red hanko stamp with a brush character inside. */
export const Stamp: React.FC<{text: string; at?: number; x: number; y: number; size?: number; rotate?: number}> = ({text, at = 0, x, y, size = 150, rotate = -8}) => {
	const frame = useCurrentFrame();
	const {fps} = useVideoConfig();
	const t = frame - at;
	if (t < 0) return null;
	const s = spring({frame: t, fps, config: {damping: 10, stiffness: 320, mass: 0.5}});
	const scale = interpolate(s, [0, 1], [2.2, 1]);
	const vertical = text.length > 1;
	return (
		<div
			style={{
				position: 'absolute',
				left: x,
				top: y,
				width: size,
				height: size * (vertical ? Math.max(1, text.length * 0.78) : 1),
				transform: `translate(-50%, -50%) rotate(${rotate}deg) scale(${scale})`,
				opacity: interpolate(t, [0, 2], [0, 0.95], clamp),
				border: `${size * 0.07}px solid ${RED}`,
				borderRadius: size * 0.08,
				color: RED,
				fontFamily: BRUSH,
				fontSize: size * 0.62,
				display: 'flex',
				alignItems: 'center',
				justifyContent: 'center',
				writingMode: vertical ? 'vertical-rl' : undefined,
				lineHeight: 1,
				mixBlendMode: 'multiply',
				background: 'rgba(192,40,28,0.06)',
			}}
		>
			{text}
		</div>
	);
};

/** Cinematic bars. */
export const Letterbox: React.FC<{size?: number}> = ({size = 90}) => (
	<AbsoluteFill style={{pointerEvents: 'none'}}>
		<div style={{position: 'absolute', left: 0, right: 0, top: 0, height: size, background: '#000'}} />
		<div style={{position: 'absolute', left: 0, right: 0, bottom: 0, height: size, background: '#000'}} />
	</AbsoluteFill>
);

/** Radial speed lines for impact moments. */
export const SpeedLines: React.FC<{at?: number; length?: number; cx?: number; cy?: number; color?: string}> = ({at = 0, length = 10, cx = 960, cy = 540, color = '#fff'}) => {
	const frame = useCurrentFrame();
	const t = frame - at;
	if (t < 0 || t > length) return null;
	const o = interpolate(t, [0, 2, length], [0, 0.85, 0], clamp);
	const lines = Array.from({length: 70}, (_, i) => {
		const a = (i / 70) * Math.PI * 2 + random(`sl${i}`) * 0.08;
		const r0 = 380 + random(`sl0${i}-${Math.floor(frame / 2)}`) * 260;
		const w = 3 + random(`slw${i}`) * 10;
		return {a, r0, w};
	});
	return (
		<svg width={1920} height={1080} style={{position: 'absolute', opacity: o}}>
			{lines.map((l, i) => {
				const x1 = cx + Math.cos(l.a) * l.r0, y1 = cy + Math.sin(l.a) * l.r0;
				const x2 = cx + Math.cos(l.a) * 1500, y2 = cy + Math.sin(l.a) * 1500;
				const n = {x: -Math.sin(l.a) * l.w, y: Math.cos(l.a) * l.w};
				return <polygon key={i} points={`${x1},${y1} ${x2 + n.x},${y2 + n.y} ${x2 - n.x},${y2 - n.y}`} fill={color} />;
			})}
		</svg>
	);
};

/** Sub caption in gothic on an ink brush band. */
export const Band: React.FC<{text: string; at?: number; x?: number; y?: number; size?: number; color?: string; bg?: string}> = ({text, at = 0, x = 960, y = 940, size = 46, color = PAPER, bg = INK}) => {
	const frame = useCurrentFrame();
	const t = frame - at;
	if (t < 0) return null;
	const p = interpolate(t, [0, 8], [0, 1], {...clamp, easing: Easing.out(Easing.cubic)});
	return (
		<div
			style={{
				position: 'absolute',
				left: x,
				top: y,
				transform: 'translate(-50%, -50%)',
				padding: `${size * 0.32}px ${size * 0.9}px`,
				background: bg,
				color,
				fontFamily: GOTHIC,
				fontSize: size,
				letterSpacing: size * 0.06,
				whiteSpace: 'nowrap',
				clipPath: `inset(0 ${100 - p * 100}% 0 0)`,
				boxShadow: '0 8px 30px rgba(0,0,0,0.4)',
			}}
		>
			{text}
		</div>
	);
};
