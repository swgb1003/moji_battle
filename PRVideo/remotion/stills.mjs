// Usage: node stills.mjs <seconds...>  -> ../out/stills/t_<s>.png (half size)
import {bundle} from '@remotion/bundler';
import {renderStill, selectComposition} from '@remotion/renderer';
import path from 'node:path';
const serveUrl = await bundle({entryPoint: path.resolve('src/index.ts')});
const composition = await selectComposition({serveUrl, id: 'PRVideo'});
for (const s of process.argv.slice(2)) {
	const frame = Math.round(Number(s) * 30);
	await renderStill({composition, serveUrl, frame, output: `../out/stills/t_${s}.png`, scale: 0.375});
	console.log('still', s);
}
