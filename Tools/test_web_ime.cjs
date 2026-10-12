// DOM-level IME regression tests. No browser profile, authentication or OS automation.
const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const { JSDOM } = require(path.join(process.argv[2], 'jsdom'));
const source = fs.readFileSync(path.join(__dirname, '../Assets/Plugins/WebGL/MojiCharacterInput.jslib'), 'utf8');

function setup() {
  const dom = new JSDOM('<div id="player"><canvas id="unity-canvas" tabindex="-1"></canvas></div>');
  const messages = [];
  dom.window.mojiSendMessage = (...args) => messages.push(args);
  dom.window.document.getElementById('unity-canvas').getBoundingClientRect = () => ({ left: 12, top: 30, width: 960, height: 540 });
  const context = vm.createContext({
    window: dom.window, document: dom.window.document,
    LibraryManager: { library: {} }, mergeInto: Object.assign, UTF8ToString: value => value
  });
  vm.runInContext(source, context);
  const api = context.LibraryManager.library;
  context.MojiTextInput = api.$MojiTextInput;
  api.MojiInputCreate('CharInput', '武器の文字', '文字', 0, .8);
  api.MojiInputValue('CharInput', '一', true);
  const input = dom.window.document.getElementById('moji-input-CharInput');
  input.focus();
  const event = (type, properties = {}) => {
    const event = new dom.window.Event(type, { bubbles: true, cancelable: true });
    Object.assign(event, properties);
    input.dispatchEvent(event);
    return event;
  };
  const texts = () => messages.filter(message => message[1] === 'ReceiveText').map(message => message[2]);
  return { dom, api, input, event, messages, texts, context };
}

test('romaji and kana remain untouched until the kanji candidate is confirmed', () => {
  const s = setup();
  assert.equal(s.input.hasAttribute('maxlength'), false);
  s.event('compositionstart');
  for (const value of ['u', 'ut', 'utsu', 'うつ']) {
    s.input.value = value;
    s.event('input', { isComposing: true });
    s.api.MojiInputValue('CharInput', '一', false);
    assert.equal(s.input.value, value);
    assert.deepEqual(s.texts(), []);
  }
  s.event('keydown', { key: 'Enter', isComposing: true, keyCode: 229 });
  assert.equal(s.dom.window.document.activeElement, s.input);
  s.input.value = '鬱';
  s.event('compositionend');
  s.event('input', { isComposing: false });
  assert.deepEqual(s.texts(), ['鬱']);
});

test('hiragana, katakana and kanji reach Unity without losing Unicode', () => {
  const s = setup();
  for (const value of ['あ', 'カ', '龍']) {
    s.event('compositionstart');
    s.input.value = value;
    s.event('input', { isComposing: true });
    s.event('compositionend');
  }
  assert.deepEqual(s.texts(), ['あ', 'カ', '龍']);
});

test('paste and multiple characters are sent intact for game validation', () => {
  const s = setup();
  for (const value of ['→', 'A', '漢字', '', '𠮷']) {
    s.input.value = value;
    s.event('input', { isComposing: false });
  }
  assert.deepEqual(s.texts(), ['→', 'A', '漢字', '', '𠮷']);
});

test('IME Enter after compositionend is not mistaken for submission', () => {
  const s = setup();
  s.event('compositionstart');
  s.input.value = '火';
  s.event('compositionend');
  s.event('keydown', { key: 'Enter', isComposing: false, keyCode: 229 });
  assert.equal(s.dom.window.document.activeElement, s.input);
  s.event('keydown', { key: 'Enter', isComposing: false, keyCode: 13 });
  assert.equal(s.dom.window.document.activeElement.id, 'unity-canvas');
  assert.deepEqual(s.texts(), ['火']);
  assert.deepEqual(s.messages.filter(m => m[1] === 'ReceiveFocus').map(m => m[2]), ['1', '0']);
});

test('leaving the field during conversion does not commit unfinished kana', () => {
  const s = setup();
  s.event('compositionstart');
  s.input.value = 'にほ';
  s.input.blur();
  assert.equal(s.input.value, '一');
  assert.deepEqual(s.texts(), []);
});

test('switching players cancels composition and uses the new player character', () => {
  const s = setup();
  s.event('compositionstart');
  s.input.value = 'やま';
  s.api.MojiInputValue('CharInput', '→', true);
  assert.equal(s.input.value, '→');
  assert.deepEqual(s.texts(), []);
  s.input.focus();
  s.input.value = '山';
  s.event('input', { isComposing: false });
  assert.deepEqual(s.texts(), ['山']);
});

test('confirmation hides the overlay and leaving customization removes it', () => {
  const s = setup();
  s.api.MojiInputShow('CharInput', false);
  assert.equal(s.input.hidden, true);
  assert.equal(s.dom.window.document.activeElement.id, '');
  s.api.MojiInputShow('CharInput', true);
  assert.equal(s.input.hidden, false);
  s.api.MojiInputDestroy('CharInput');
  assert.equal(s.dom.window.document.querySelector('#moji-input-CharInput'), null);
});

test('input follows scaled canvas bounds and stays inside the fullscreen container', () => {
  const s = setup();
  s.api.MojiInputRect('CharInput', .5, .25, .1, .1);
  assert.equal(s.input.style.left, '494px');
  assert.equal(s.input.style.top, '167px');
  assert.equal(s.input.style.width, '92px');
  assert.equal(s.input.style.height, '50px');
  assert.equal(s.input.parentElement.id, 'player');
  s.dom.window.document.getElementById('unity-canvas').getBoundingClientRect = () => ({ left: 0, top: 0, width: 1920, height: 1080 });
  s.api.MojiInputRect('CharInput', .5, .25, .1, .1);
  assert.equal(s.input.style.left, '962px');
  assert.equal(s.input.style.width, '188px');
});

test('the special-move name field is a second, independent input', () => {
  const s = setup();
  s.api.MojiInputCreate('SpecialNameInput', '必殺技の名前', '例：炎龍一閃', 20, .5);
  const name = s.dom.window.document.getElementById('moji-input-SpecialNameInput');
  assert.equal(name.maxLength, 20);
  assert.equal(name.placeholder, '例：炎龍一閃');
  name.focus();
  name.value = '火炎昇龍波';
  name.dispatchEvent(new s.dom.window.Event('input', { bubbles: true }));
  assert.deepEqual(s.messages.filter(m => m[1] === 'ReceiveText').map(m => [m[0], m[2]]), [['SpecialNameInput', '火炎昇龍波']]);
  s.api.MojiInputShow('SpecialNameInput', false);
  assert.equal(name.hidden, true);
  assert.equal(s.input.hidden, false, 'hiding one field leaves the other');
  s.api.MojiInputDestroy('SpecialNameInput');
  assert.ok(s.dom.window.document.getElementById('moji-input-CharInput'));
});

test('room code typing, deletion and normalization retain the printed cells and editing position', () => {
  const s = setup();
  s.api.MojiInputCreate('RoomCodeInput', 'ルームコード', '', 0, .64, true);
  const code = s.dom.window.document.getElementById('moji-input-RoomCodeInput');
  assert.equal(code.style.background, 'transparent');
  assert.equal(code.style.color, 'transparent');
  assert.equal(code.getAttribute('autocapitalize'), 'characters');
  assert.equal(code.hasAttribute('maxlength'), false, 'Separators in pasted codes reach the normalizer intact.');
  s.api.MojiInputRect('RoomCodeInput', .5, .25, .4, .1);
  code.focus();
  for (const value of ['m', 'M7', 'M7k', 'M7K2', 'M7K2q', 'M7K2Q9', 'M7K2Q']) {
    code.value = value;
    code.dispatchEvent(new s.dom.window.Event('input', { bubbles: true }));
    s.api.MojiInputValue('RoomCodeInput', value.toUpperCase(), false);
  }
  assert.equal(code.value, 'M7K2Q');
  code.setSelectionRange(2, 2);
  s.api.MojiInputValue('RoomCodeInput', 'M7K2Q', false);
  assert.equal(code.selectionStart, 2, 'Normalizing an edit must not move the caret to the end.');
  assert.deepEqual(s.messages.filter(m => m[0] === 'RoomCodeInput' && m[1] === 'ReceiveText').map(m => m[2]),
    ['m', 'M7', 'M7k', 'M7K2', 'M7K2q', 'M7K2Q9', 'M7K2Q']);
  assert.notEqual(code.style.outline, 'none');
  s.api.MojiInputShow('RoomCodeInput', false);
  assert.equal(code.hidden, true);
  assert.equal(code.style.outline, 'none');
});

test('room code Enter commits before submitting and IME confirmation does not submit', () => {
  const s = setup();
  s.api.MojiInputCreate('RoomCodeInput', 'ルームコード', '', 0, .64, true);
  const code = s.dom.window.document.getElementById('moji-input-RoomCodeInput');
  code.focus();
  code.value = 'M7K2Q9';
  code.dispatchEvent(new s.dom.window.KeyboardEvent('keydown', { key: 'Enter', isComposing: true, bubbles: true }));
  assert.equal(s.messages.filter(m => m[1] === 'ReceiveSubmit').length, 0);
  code.dispatchEvent(new s.dom.window.KeyboardEvent('keydown', { key: 'Enter', bubbles: true }));
  assert.deepEqual(s.messages.filter(m => m[0] === 'RoomCodeInput').slice(-3),
    [['RoomCodeInput', 'ReceiveText', 'M7K2Q9'], ['RoomCodeInput', 'ReceiveFocus', '0'], ['RoomCodeInput', 'ReceiveSubmit', '']]);
});
