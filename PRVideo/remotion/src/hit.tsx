import React from 'react';
import {AbsoluteFill, Easing, interpolate, OffthreadVideo, random, Sequence, staticFile, useCurrentFrame} from 'remotion';
import {clamp, INK, InkBlot, RED} from './fx';

/** A highlight taken from a recorded match (frames are 30fps frames of each clip). */
export type Moment = {
	clip: string; // normal-speed clip
	slow: string; // the same match recorded at half speed
	hit: number; // impact frame in the normal clip
	hitSlow: number; // impact frame in the slow clip
	x: number; // impact point in the 1920x1080 layout
	y: number;
	style: string;
	label: string;
	damage: number;
	critical: boolean;
	ko?: boolean;
};

const FREEZE = 3;

/** Ink splash that bursts from the impact point and dries out. */
const Splash: React.FC<{x: number; y: number; at: number; seed: string; size?: number}> = ({x, y, at, seed, size = 150}) => {
	const frame = useCurrentFrame();
	const t = frame - at;
	if (t < 0 || t > 26) return null;
	const progress = interpolate(t, [0, 7], [0, 1], {...clamp, easing: Easing.out(Easing.cubic)});
	const opacity = interpolate(t, [0, 1, 16, 26], [0, 0.95, 0.85, 0], clamp);
	return (
		<div style={{position: 'absolute', left: 0, top: 0, width: 1920, height: 1080, opacity, mixBlendMode: 'multiply'}}>
			<InkBlot seed={seed} size={size} progress={progress} x={x} y={y} color={INK} />
			<InkBlot seed={seed + 'r'} size={size * 0.55} progress={progress} x={x + size * 0.25} y={y - size * 0.15} color={RED} />
		</div>
	);
};

/**
 * One hit: real-time approach -> freeze with impact frames -> half-speed follow-through -> real time again.
 * `lead` frames play before the impact; the whole thing lasts `dur` frames.
 */
export const HitShot: React.FC<{m: Moment; dur: number; lead: number; zoom?: number; slowFrames?: number}> = ({m, dur, lead, zoom = 1.36, slowFrames = 22}) => {
	const frame = useCurrentFrame();
	const post = Math.max(0, dur - lead - FREEZE - slowFrames);
	const tImpact = lead;
	// Punch in at the impact and ease back; focus drifts toward the impact point.
	const punch = interpolate(frame - tImpact, [-1, 0, 2, 20], [0, 0.16, 0.16, 0], {...clamp, easing: Easing.out(Easing.quad)});
	const z = Math.max(1.46, zoom) + punch + interpolate(frame, [0, dur], [0, 0.05]);
	// The fighters live in y 232..985 of the battle screen: stay inside it so the HUD bars never show.
	const cx = 960 + (m.x - 960) * 0.55;
	const cy = Math.min(985 - 540 / z, Math.max(232 + 540 / z, 540 + (m.y - 540) * 0.45));
	const room = 1 - 1 / z;
	const tx = Math.max(-960 * room, Math.min(960 * room, 960 - cx));
	const ty = Math.max(-540 * room, Math.min(540 * room, 540 - cy));
	let sx = 0, sy = 0;
	const st = frame - tImpact;
	if (st >= 0 && st < 12) {
		const k = (1 - st / 12) ** 2 * (m.critical || m.ko ? 30 : 18);
		sx = (random(`hx${m.hit}-${frame}`) - 0.5) * 2 * k;
		sy = (random(`hy${m.hit}-${frame}`) - 0.5) * 2 * k;
	}
	// Two impact frames: inverted ink silhouette, then high contrast.
	const impactFilter = st === 0 ? 'grayscale(1) contrast(6) invert(1)' : st === 1 ? 'grayscale(1) contrast(5)' : st === 2 ? 'contrast(1.5) saturate(1.4)' : undefined;
	const video = (clip: string, src: number, rate = 1) => (
		<OffthreadVideo src={staticFile(`footage/${clip}.mp4`)} trimBefore={Math.max(0, src)} playbackRate={rate} muted style={{width: '100%', height: '100%'}} />
	);
	return (
		<AbsoluteFill style={{overflow: 'hidden', backgroundColor: '#000'}}>
			<AbsoluteFill style={{transform: `translate(${sx}px, ${sy}px) scale(${z}) translate(${tx}px, ${ty}px)`, filter: impactFilter}}>
				<Sequence durationInFrames={lead}>{video(m.clip, m.hit - lead)}</Sequence>
				<Sequence from={lead} durationInFrames={FREEZE}>
					<OffthreadVideo src={staticFile(`footage/${m.slow}.mp4`)} trimBefore={m.hitSlow} playbackRate={0.001} muted style={{width: '100%', height: '100%'}} />
				</Sequence>
				<Sequence from={lead + FREEZE} durationInFrames={slowFrames}>{video(m.slow, m.hitSlow + 1)}</Sequence>
				{post > 0 && <Sequence from={lead + FREEZE + slowFrames} durationInFrames={post}>{video(m.clip, m.hit + Math.round(slowFrames / 2) + 1)}</Sequence>}
				<Splash x={m.x} y={m.y} at={tImpact} seed={`s${m.clip}${m.hit}`} size={m.critical || m.ko ? 190 : 140} />
			</AbsoluteFill>
		</AbsoluteFill>
	);
};
