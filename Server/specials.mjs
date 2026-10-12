import Anthropic from '@anthropic-ai/sdk';

// The game's motion parts. A special move is a chain of 1-4 of these; the game owns the physics of each.
export const STYLES = {
  Overhead: '振り下ろし', Rising: '斬り上げ', Thrust: '突き', Sweep: '横薙ぎ（奥行き方向に払う）', LowSweep: '足払い',
  Bash: '盾当て（字形ごと体当たり）', Launch: '打ち上げ（当たれば空中の相手を叩き落とす連携が自動で続く）',
  Spin: '回転斬り', Leap: '跳び斬り（跳び上がって真上から）', DashSlash: '踏み込み斬り（一気に間合いを詰める）',
  Whirl: '全身ぶん回し', GroundWave: '衝撃波（跳んで地面を叩き、地を這う波を飛ばす）', StrokeShot: '一画飛ばし（字の一画を飛ばす飛び道具）',
  FrontKick: '前蹴り',
};
export const ELEMENTS = {
  none: '墨（属性なし）', fire: '炎', thunder: '雷', ice: '氷', wind: '風', water: '水', earth: '土・岩',
  light: '光', dark: '闇', flower: '花・桜', poison: '毒',
};
export const EFFECTS = {
  burst: '弾ける飛沫', ring: '広がる輪', pillar: '立ち昇る柱', slash: '斬撃の線', spiral: '渦巻き', rain: '降り注ぐ粒',
};
const ELEMENT_COLORS = {
  none: ['#20211F', '#8A8578'], fire: ['#E2462B', '#F6B23C'], thunder: ['#F2D43A', '#6A5CD8'], ice: ['#7FD3F0', '#E8F7FF'],
  wind: ['#5DBE8A', '#D8F2E4'], water: ['#2F7DD8', '#9ED0F5'], earth: ['#8A5A2B', '#C9A26B'], light: ['#F7E58C', '#FFFFFF'],
  dark: ['#4B2A6B', '#B05CE0'], flower: ['#F08FB0', '#FFE3EC'], poison: ['#7FBF3F', '#5B2A7A'],
};
const MAX_NAME = 7, MAX_STEPS = 4, MAX_STAT = 5, STAT_BUDGET = 9;
const segmenter = new Intl.Segmenter('ja', { granularity: 'grapheme' });
export const graphemes = s => [...segmenter.segment(s)].map(x => x.segment);
const clip = (s, n) => graphemes(String(s ?? '').replace(/[\u0000-\u001f\u007f<>]/g, '').trim()).slice(0, n).join('');
const fail = (status, message) => { throw Object.assign(new Error(message), { status }); };

/** 技名の条件: 選んだ一文字を必ず含み、7 文字以内。 */
export function checkName(name, character) {
  if (typeof character !== 'string' || graphemes(character.trim()).length !== 1) fail(400, '文字を一つ選んでください。');
  if (typeof name !== 'string' || /[\u0000-\u001f\u007f<>]/.test(name)) fail(400, '技名を入力してください。');
  const n = name.trim(), length = graphemes(n).length;
  if (length < 1) fail(400, '技名を入力してください。');
  if (length > MAX_NAME) fail(400, `技名は${MAX_NAME}文字以内にしてください。`);
  if (!graphemes(n).includes(character.trim())) fail(400, `技名に「${character.trim()}」を入れてください。`);
  return n;
}

/**
 * AI の出力も、クライアントから届いた装備も、同じ規則で丸める。
 * 能力の合計は STAT_BUDGET まで（名前で強さが決まらないように）。不明な値は無難な既定へ。
 */
export function sanitizeSpecial(raw, character) {
  if (!raw || typeof raw !== 'object' || typeof raw.name !== 'string' || !raw.name.trim()) return null;
  if (typeof raw.character === 'string' && raw.character && raw.character !== character) return null; // made for another character
  let name;
  try { name = checkName(raw.name, character); } catch { return null; }
  const steps = (Array.isArray(raw.steps) ? raw.steps : []).filter(s => Object.hasOwn(STYLES, s)).slice(0, MAX_STEPS);
  if (steps.length === 0) steps.push('DashSlash');
  const stat = v => Math.min(MAX_STAT, Math.max(1, Math.round(Number.isFinite(v) ? v : 3)));
  const stats = [stat(raw.power), stat(raw.speed), stat(raw.reach)];
  while (stats.reduce((a, b) => a + b) > STAT_BUDGET) stats[stats.indexOf(Math.max(...stats))]--;
  const element = Object.hasOwn(ELEMENTS, raw.element) ? raw.element : 'none';
  const effect = Object.hasOwn(EFFECTS, raw.effect) ? raw.effect : 'burst';
  const hex = (v, fallback) => typeof v === 'string' && /^#[0-9a-fA-F]{6}$/.test(v) ? v.toUpperCase() : fallback;
  const [p, s] = ELEMENT_COLORS[element];
  return { name, character: character.trim(), reading: clip(raw.reading, 16), steps, power: stats[0], speed: stats[1], reach: stats[2],
    element, effect, primary: hex(raw.primary, p), secondary: hex(raw.secondary, s),
    callout: clip(raw.callout, 14), description: clip(raw.description, 48) };
}

const schema = {
  type: 'object', additionalProperties: false,
  required: ['ok', 'reason', 'reading', 'steps', 'power', 'speed', 'reach', 'element', 'effect', 'primary', 'secondary', 'callout', 'description'],
  properties: {
    ok: { type: 'boolean' },
    reason: { type: 'string' },
    reading: { type: 'string' },
    steps: { type: 'array', items: { type: 'string', enum: Object.keys(STYLES) } },
    power: { type: 'integer' }, speed: { type: 'integer' }, reach: { type: 'integer' },
    element: { type: 'string', enum: Object.keys(ELEMENTS) },
    effect: { type: 'string', enum: Object.keys(EFFECTS) },
    primary: { type: 'string' }, secondary: { type: 'string' },
    callout: { type: 'string' }, description: { type: 'string' },
  },
};
const list = o => Object.entries(o).map(([k, v]) => `- ${k}: ${v}`).join('\n');
const system = `あなたは対戦格闘ゲーム「MOJI Fighter X」の必殺技デザイナーです。
このゲームでは、プレイヤーが選んだ一文字（漢字・かな・英数字など）の字形がそのまま武器になり、棒人間が物理演算で戦います。
プレイヤーが付けた必殺技名を読み、名前の意味・漢字の印象・語感から、技の動きと見た目を決めてください。

## 動き（steps）
次の部品から1〜4個を選び、出す順に並べます。名前の印象に合う連携にしてください（例: 「昇」「龍」→ Launch、「連」「乱」→ 複数の斬撃、「波」「砲」→ 飛び道具、「嵐」「旋」→ Whirl や Spin）。
${list(STYLES)}

## 能力（power / speed / reach）
各1〜5の整数で、合計は必ず9以下。威力重視・速さ重視・間合い重視のどれかに寄せて個性を出してください。
- power: 一撃の重さ
- speed: 溜めの短さ
- reach: 踏み込みの伸び

## 見た目
- element: 属性
${list(ELEMENTS)}
- effect: 当たった時と技の間に出る演出の形
${list(EFFECTS)}
- primary / secondary: 演出の主色と副色（#RRGGBB）。属性と名前に合う色。
- callout: 技を出す時の掛け声（14文字以内、例:「燃え尽きろ！」）
- description: 技の説明（48文字以内）
- reading: 技名の読み仮名（ひらがな）

## 断る場合
技名が実在の人物・集団への中傷、差別、性的な内容などで、ゲーム内で他の人に見せるのにふさわしくない時だけ ok を false にし、reason に短い理由を書いてください。
格闘ゲームなので「斬」「殺」「死」「地獄」などの物騒な言葉は問題ありません。それ以外は ok を true、reason を空にします。`;

/** Claude で技を設計する。ANTHROPIC_API_KEY が無ければ null（生成は使えない）。 */
export function createClaudeGenerator({ apiKey = process.env.ANTHROPIC_API_KEY, model = process.env.SPECIAL_MODEL || 'claude-opus-5-5', fetch } = {}) {
  if (!apiKey) return null;
  const client = new Anthropic({ apiKey, timeout: 45000, maxRetries: 1, ...(fetch ? { fetch } : {}) });
  return async ({ character, name }) => {
    let response;
    try {
      response = await client.beta.messages.create({
        model, max_tokens: 8000,
        betas: ['server-side-fallback-2026-07-01'], fallbacks: 'default',
        output_config: { effort: 'low', format: { type: 'json_schema', schema } },
        system,
        messages: [{ role: 'user', content: `武器の文字: ${character}\n必殺技名: ${name}` }],
      });
    } catch (e) {
      if (e instanceof Anthropic.RateLimitError) fail(503, 'AI が混み合っています。少し待ってからお試しください。');
      if (e instanceof Anthropic.APIError) { console.error('special generation failed', e.status, e.message); fail(502, 'AI で技を作れませんでした。'); }
      throw e;
    }
    if (response.stop_reason === 'refusal') return { ok: false, reason: 'この技名では作れませんでした。' };
    if (response.stop_reason === 'max_tokens') fail(502, 'AI で技を作れませんでした。');
    const text = response.content.find(b => b.type === 'text')?.text;
    try { return JSON.parse(text); } catch { fail(502, 'AI で技を作れませんでした。'); }
  };
}

/**
 * Local development only (SPECIAL_FAKE=1): a keyword-based stand-in for Claude, so the game can be tested without an API key or cost.
 * Never enabled on the public server.
 */
export function createFakeGenerator() {
  const pick = (name, table, fallback) => { for (const [chars, value] of table) if ([...chars].some(c => name.includes(c))) return value; return fallback; };
  return async ({ name }) => {
    const steps = [];
    for (const [chars, motion] of [['昇龍天', ['Launch']], ['連乱舞', ['Overhead', 'Rising', 'Thrust']], ['旋嵐渦', ['Whirl']],
      ['突貫閃', ['DashSlash']], ['波砲飛弾', ['StrokeShot']], ['震轟', ['GroundWave']], ['蹴脚', ['FrontKick']]])
      if ([...chars].some(c => name.includes(c))) steps.push(...motion);
    const element = pick(name, [['炎火焔', 'fire'], ['雷電', 'thunder'], ['氷雪凍', 'ice'], ['風嵐', 'wind'], ['水流海', 'water'],
      ['岩土地山', 'earth'], ['光聖輝', 'light'], ['闇黒影', 'dark'], ['桜花', 'flower'], ['毒', 'poison']], 'none');
    return { ok: true, reason: '', reading: '', steps: steps.length ? steps.slice(0, 4) : ['DashSlash'], power: 3, speed: 3, reach: 3,
      element, effect: pick(name, [['柱昇', 'pillar'], ['輪', 'ring'], ['斬閃', 'slash'], ['渦旋', 'spiral'], ['雨', 'rain']], 'burst'),
      primary: '', secondary: '', callout: `${name}！`, description: 'ローカル確認用の技（AI 未使用）。' };
  };
}
