import React from 'react';
import {AbsoluteFill, Composition} from 'remotion';
import {FPS, PRVideo, TOTAL} from './PRVideo';

/** The edit is laid out in 1920x1080 and drawn at 2560x1440 (text, shapes and 1440p footage stay sharp). */
const Sharp: React.FC = () => (
	<AbsoluteFill style={{backgroundColor: '#000'}}>
		<div style={{position: 'absolute', left: 0, top: 0, width: 1920, height: 1080, transform: `scale(${2560 / 1920})`, transformOrigin: '0 0', overflow: 'hidden'}}>
			<PRVideo />
		</div>
	</AbsoluteFill>
);

export const RemotionRoot: React.FC = () => (
	<Composition id="PRVideo" component={Sharp} durationInFrames={TOTAL} fps={FPS} width={2560} height={1440} />
);
