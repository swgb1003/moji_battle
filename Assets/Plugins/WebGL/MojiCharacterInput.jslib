mergeInto(LibraryManager.library, {
  // One real browser <input> per Unity InputField (keyed by its GameObject name) so the Japanese IME works.
  $MojiTextInput: {
    inputs: {},
    make: function (target) {
      var bridge = {
        element: null,
        target: target,
        composing: false,
        committed: '',
        bounds: null,
        send: function (method, value) {
          if (window.mojiSendMessage) window.mojiSendMessage(this.target, method, value);
        },
        commit: function () {
          if (!this.element || this.composing || this.element.value === this.committed) return;
          this.committed = this.element.value;
          this.send('ReceiveText', this.committed);
        },
        position: function () {
          if (!this.element || !this.bounds) return;
          var canvas = document.getElementById('unity-canvas');
          var rect = canvas.getBoundingClientRect();
          var b = this.bounds;
          var style = this.element.style;
          style.left = (rect.left + b[0] * rect.width + 2) + 'px';
          style.top = (rect.top + b[1] * rect.height + 2) + 'px';
          style.width = Math.max(1, b[2] * rect.width - 4) + 'px';
          style.height = Math.max(1, b[3] * rect.height - 4) + 'px';
          style.fontSize = Math.max(16, b[3] * rect.height * this.fontScale) + 'px';
          if (this.printedCode) {
            // Unity prints the six cells; the transparent browser field handles typing and selection.
            var pitch = b[2] * rect.width / 6;
            var glyphWidth = parseFloat(style.fontSize) * .6;
            style.letterSpacing = Math.max(0, pitch - glyphWidth) + 'px';
            style.paddingLeft = Math.max(0, (pitch - glyphWidth) / 2) + 'px';
          }
        },
        destroy: function () {
          var input = this.element;
          this.element = null;
          this.composing = false;
          this.bounds = null;
          if (input) input.remove();
        }
      };
      return bridge;
    }
  },
  MojiInputCreate__deps: ['$MojiTextInput'],
  MojiInputCreate: function (target, label, placeholder, maxLength, fontScale, printedCode) {
    var key = UTF8ToString(target);
    if (MojiTextInput.inputs[key]) MojiTextInput.inputs[key].destroy();
    var bridge = MojiTextInput.make(key);
    MojiTextInput.inputs[key] = bridge;
    bridge.fontScale = fontScale;
    bridge.printedCode = !!printedCode;
    var input = document.createElement('input');
    bridge.element = input;
    input.id = 'moji-input-' + key;
    input.type = 'text';
    input.lang = 'ja';
    input.autocomplete = 'off';
    input.spellcheck = false;
    input.setAttribute('autocorrect', 'off');
    input.setAttribute('inputmode', 'text');
    input.setAttribute('aria-label', UTF8ToString(label));
    input.placeholder = UTF8ToString(placeholder);
    // A soft cap only; the game validates and explains the real limit (IME text may be longer while composing).
    if (maxLength > 0) input.maxLength = maxLength;
    input.style.cssText = 'position:fixed;z-index:5;margin:0;padding:0;border:0;border-radius:0;outline:none;box-sizing:border-box;text-align:center;color:#20211f;background:#f2ebdb;font-family:"Yu Gothic UI",Meiryo,system-ui,sans-serif;';
    if (printedCode) {
      input.lang = 'en';
      input.setAttribute('autocapitalize', 'characters');
      input.style.background = 'transparent';
      input.style.color = 'transparent';
      input.style.caretColor = '#b93629';
      input.style.textAlign = 'left';
      input.style.fontFamily = 'monospace';
    }
    var justFocused = false;
    input.addEventListener('focus', function () {
      justFocused = true;
      if (printedCode) input.style.outline = '2px solid #b93629';
      bridge.send('ReceiveFocus', '1');
      input.select();
    });
    input.addEventListener('pointerup', function () {
      if (justFocused) { input.select(); justFocused = false; }
    });
    input.addEventListener('compositionstart', function () { bridge.composing = true; });
    input.addEventListener('compositionend', function () {
      bridge.composing = false;
      bridge.commit();
    });
    input.addEventListener('input', function (event) {
      if (!event.isComposing) bridge.commit();
    });
    input.addEventListener('blur', function () {
      if (bridge.element !== input) return;
      if (printedCode) input.style.outline = 'none';
      if (bridge.composing) {
        bridge.composing = false;
        input.value = bridge.committed;
      } else bridge.commit();
      bridge.send('ReceiveFocus', '0');
    });
    input.addEventListener('keydown', function (event) {
      // Enter while choosing an IME candidate must not submit or close the input.
      event.stopPropagation();
      if (bridge.composing || event.isComposing || event.keyCode === 229) return;
      if (event.key === 'Enter' || event.key === 'Escape') {
        event.preventDefault();
        if (event.key === 'Escape') input.value = bridge.committed;
        input.blur();
        document.getElementById('unity-canvas').focus();
        if (event.key === 'Enter') bridge.send('ReceiveSubmit', '');
      }
    });
    document.getElementById('player').appendChild(input);
  },
  MojiInputRect__deps: ['$MojiTextInput'],
  MojiInputRect: function (target, x, y, width, height) {
    var bridge = MojiTextInput.inputs[UTF8ToString(target)];
    if (!bridge) return;
    bridge.bounds = [x, y, width, height];
    bridge.position();
  },
  MojiInputValue__deps: ['$MojiTextInput'],
  MojiInputValue: function (target, value, force) {
    var bridge = MojiTextInput.inputs[UTF8ToString(target)];
    if (!bridge || !bridge.element || (bridge.composing && !force)) return;
    if (force && bridge.composing) bridge.element.blur();
    var preserveSelection = !force && document.activeElement === bridge.element;
    var selectionStart = bridge.element.selectionStart;
    var selectionEnd = bridge.element.selectionEnd;
    bridge.committed = UTF8ToString(value);
    bridge.element.value = bridge.committed;
    if (preserveSelection) bridge.element.setSelectionRange(
      Math.min(selectionStart, bridge.committed.length), Math.min(selectionEnd, bridge.committed.length));
    if (force) bridge.composing = false;
  },
  MojiInputShow__deps: ['$MojiTextInput'],
  MojiInputShow: function (target, visible) {
    var bridge = MojiTextInput.inputs[UTF8ToString(target)];
    var input = bridge && bridge.element;
    if (!input) return;
    if (!visible && document.activeElement === input) input.blur();
    input.hidden = !visible;
  },
  MojiInputDestroy__deps: ['$MojiTextInput'],
  MojiInputDestroy: function (target) {
    var key = UTF8ToString(target);
    var bridge = MojiTextInput.inputs[key];
    if (bridge) { bridge.destroy(); delete MojiTextInput.inputs[key]; }
  }
});
