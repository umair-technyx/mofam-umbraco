// "HTML with Preview" property editor.
// Two tabs: HTML (a code box) and Preview (the HTML shown as a page).
// The saved value is plain text (the HTML), like any other text field.
//
// No build step and no libraries, so it needs no maintenance. Umbraco only needs
// a "value" property, the standard "change" event and the element name in umbraco-package.json.
// The preview has scripts turned off. English and Arabic both work (the LTR / RTL button).

const CODE_ICON =
  '<svg viewBox="0 0 24 24" width="16" height="16" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><polyline points="16 18 22 12 16 6"/><polyline points="8 6 2 12 8 18"/></svg>';

const EYE_ICON =
  '<svg viewBox="0 0 24 24" width="16" height="16" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z"/><circle cx="12" cy="12" r="3"/></svg>';

// Fonts that show both English and Arabic.
const TEXT_FONTS = 'system-ui, "Segoe UI", Tahoma, Arial, "Noto Sans Arabic", sans-serif';
const CODE_FONTS = 'ui-monospace, SFMono-Regular, Menlo, Consolas, "Segoe UI", Tahoma, monospace';

const STYLE = `
  :host { display: block; }
  .box { border: 1px solid var(--uui-color-border, #d8d7d9); border-radius: 3px; background: var(--uui-color-surface, #fff); overflow: hidden; }
  .tabs { display: flex; align-items: stretch; gap: 2px; border-bottom: 1px solid var(--uui-color-border, #d8d7d9); background: var(--uui-color-surface-alt, #f3f3f5); }
  .tab { display: inline-flex; align-items: center; gap: 6px; padding: 8px 14px; border: 0; background: transparent; color: var(--uui-color-text-alt, #68676b); font: inherit; cursor: pointer; border-bottom: 2px solid transparent; }
  .tab:hover { color: var(--uui-color-text, #060606); }
  .tab[aria-selected="true"] { color: var(--uui-color-text, #060606); border-bottom-color: var(--uui-color-interactive, #3544b1); background: var(--uui-color-surface, #fff); }
  .dir { margin-inline-start: auto; align-self: center; margin-inline-end: 8px; padding: 3px 10px; border: 1px solid var(--uui-color-border, #d8d7d9); border-radius: 3px; background: var(--uui-color-surface, #fff); color: var(--uui-color-text, #060606); font: inherit; font-size: 12px; cursor: pointer; }
  textarea { display: block; width: 100%; min-height: 320px; box-sizing: border-box; padding: 12px; border: 0; outline: none; resize: vertical; background: var(--uui-color-surface, #fff); color: var(--uui-color-text, #060606); font: 13px/1.6 ${CODE_FONTS}; tab-size: 2; white-space: pre-wrap; overflow-wrap: anywhere; overflow: auto; direction: ltr; text-align: left; }
  iframe { display: block; width: 100%; height: 320px; border: 0; background: #fff; resize: vertical; overflow: auto; }
  [hidden] { display: none !important; }
`;

class MofamHtmlPreviewEditor extends HTMLElement {
  #value = '';
  #tab = 'html';
  #dir = 'ltr';
  #box;
  #textarea;
  #frame;
  #htmlTab;
  #previewTab;
  #dirButton;

  constructor() {
    super();
    const root = this.attachShadow({ mode: 'open' });
    root.innerHTML = `
      <style>${STYLE}</style>
      <div class="box">
        <div class="tabs" role="tablist">
          <button type="button" class="tab" id="htmlTab" role="tab">${CODE_ICON}<span>HTML</span></button>
          <button type="button" class="tab" id="previewTab" role="tab">${EYE_ICON}<span>Preview</span></button>
          <button type="button" class="dir" id="dirButton" title="Show the preview left to right or right to left" hidden>LTR</button>
        </div>
        <textarea spellcheck="false" aria-label="HTML"></textarea>
      </div>`;

    this.#box = root.querySelector('.box');
    this.#textarea = root.querySelector('textarea');
    this.#htmlTab = root.getElementById('htmlTab');
    this.#previewTab = root.getElementById('previewTab');
    this.#dirButton = root.getElementById('dirButton');

    this.#htmlTab.addEventListener('click', () => this.#show('html'));
    this.#previewTab.addEventListener('click', () => this.#show('preview'));

    this.#dirButton.addEventListener('click', () => {
      this.#dir = this.#dir === 'ltr' ? 'rtl' : 'ltr';
      this.#dirButton.textContent = this.#dir.toUpperCase();
      this.#renderPreview();
    });

    this.#textarea.addEventListener('input', () => {
      this.#value = this.#textarea.value;
      // Umbraco listens to this event and saves the new value.
      this.dispatchEvent(new Event('change', { bubbles: true, composed: false }));
    });

    this.#show('html');
  }

  // Umbraco sets "value" when the property loads and reads it when the page is saved.
  get value() {
    return this.#value;
  }

  set value(newValue) {
    this.#value = typeof newValue === 'string' ? newValue : '';
    this.#textarea.value = this.#value;
    if (this.#tab === 'preview') this.#renderPreview();
  }

  // A readonly property must not be editable.
  set readonly(isReadonly) {
    this.#textarea.readOnly = !!isReadonly;
  }

  #show(tab) {
    this.#tab = tab;
    const isHtml = tab === 'html';

    this.#textarea.hidden = !isHtml;
    this.#dirButton.hidden = isHtml;
    this.#htmlTab.setAttribute('aria-selected', String(isHtml));
    this.#previewTab.setAttribute('aria-selected', String(!isHtml));

    if (isHtml) {
      this.#frame?.remove();
      this.#frame = null;
    } else {
      this.#renderPreview();
    }
  }

  // A new iframe is made each time. Reusing a hidden iframe leaves the preview blank.
  #renderPreview() {
    this.#frame?.remove();

    const frame = document.createElement('iframe');
    frame.title = 'Preview';
    // Empty sandbox: no scripts, no forms, no access to the backoffice.
    frame.setAttribute('sandbox', '');
    frame.srcdoc = this.#page(this.#value);

    this.#box.appendChild(frame);
    this.#frame = frame;
  }

  // The HTML is wrapped in a page for the preview only. The saved value is not changed.
  // A full page (with <html>) is shown as it is.
  #page(html) {
    if (/<html[\s>]/i.test(html)) return html;

    return `<!doctype html>
<html dir="${this.#dir}">
<head>
<meta charset="utf-8">
<style>body{margin:16px;font-family:${TEXT_FONTS};line-height:1.5}</style>
</head>
<body>${html}</body>
</html>`;
  }
}

if (!customElements.get('mofam-html-preview-editor')) {
  customElements.define('mofam-html-preview-editor', MofamHtmlPreviewEditor);
}

export default MofamHtmlPreviewEditor;
