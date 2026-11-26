import { LitElement, html, css } from '@umbraco-cms/backoffice/external/lit';
import { UmbElementMixin } from '@umbraco-cms/backoffice/element-api';
import { UMB_AUTH_CONTEXT } from '@umbraco-cms/backoffice/auth';

const API = '/umbraco/management/api/v1/mofam/form-submission';

// To add a form: add one item here (and its export in FormExportController).
const FORMS = [
  {
    key: 'contact',
    headline: 'Contact Submissions',
    url: `${API}/export-contact-submissions`,
    note: 'Downloads the contact form entries of the selected language, in UAE time.',
    fileName: 'contact-submissions.csv',
  },
  {
    key: 'newsletter',
    headline: 'Newsletter Subscribers',
    url: `${API}/export-newsletter-subscribers`,
    note: 'Downloads the newsletter subscribers of the selected language, in UAE time.',
    fileName: 'newsletter-subscribers.csv',
  },
];

// How long a message stays under a box.
const MESSAGE_SECONDS = 5;

// The fade of a message, in milliseconds. Keep it the same as the transition in the styles.
const FADE_MS = 300;

// The button shows its spinner for at least this long, so a fast answer does not make it blink.
const MIN_BUSY_MS = 600;

const wait = (ms) => new Promise((resolve) => setTimeout(resolve, ms));

const LANGUAGES = [
  { name: 'English', value: 'en' },
  { name: 'Arabic', value: 'ar' },
];

// The text to show for an error of the download.
function errorText(error) {
  // A TypeError is a network error ("Failed to fetch"): the server could not be reached.
  if (error instanceof TypeError) return 'The server could not be reached. Please try again.';
  if (error instanceof Error) return error.message;
  return 'The download failed.';
}

export default class MofamFormSubmissionDashboard extends UmbElementMixin(LitElement) {
  static properties = {
    _cultures: { state: true }, // the language of each box
    _busy: { state: true }, // the keys of the boxes that are downloading: { contact: true }
    _messages: { state: true }, // the message of each box: { text, kind, visible } where kind is success, info or failed
  };

  #auth;
  #timers = {}; // the hide timer of each box

  constructor() {
    super();
    this._cultures = Object.fromEntries(FORMS.map((f) => [f.key, 'en']));
    this._busy = {};
    this._messages = {};

    this.consumeContext(UMB_AUTH_CONTEXT, (context) => {
      this.#auth = context;
    });
  }

  disconnectedCallback() {
    super.disconnectedCallback();
    Object.values(this.#timers).forEach(clearTimeout);
  }

  #setMessage(key, message) {
    this._messages = { ...this._messages, [key]: message };
  }

  // Fades a message in under the box, then fades it out after a few seconds.
  // The message line always keeps its space, so the boxes do not move when it shows or hides.
  #showMessage(key, text, kind) {
    clearTimeout(this.#timers[key]);
    this.#setMessage(key, { text, kind, visible: true });

    this.#timers[key] = setTimeout(() => {
      // Fade out first, and remove the text when the fade is over.
      this.#setMessage(key, { text, kind, visible: false });
      this.#timers[key] = setTimeout(() => this.#setMessage(key, null), FADE_MS);
    }, MESSAGE_SECONDS * 1000);
  }

  async #download(form) {
    if (this._busy[form.key]) return;
    this._busy = { ...this._busy, [form.key]: true };
    const started = Date.now();

    // The old message fades out while the new one is loading.
    const old = this._messages[form.key];
    if (old?.visible) {
      clearTimeout(this.#timers[form.key]);
      this.#setMessage(form.key, { ...old, visible: false });
    }

    let result; // [text, kind]
    try {
      if (!this.#auth) throw new Error('You are not signed in. Please sign in again.');

      const config = this.#auth.getOpenApiConfiguration();
      const query = `?culture=${encodeURIComponent(this._cultures[form.key])}`;
      const response = await fetch(`${config.base}${form.url}${query}`, {
        credentials: config.credentials,
        headers: { Authorization: `Bearer ${await config.token()}` },
      });

      // 204: there is nothing to download for this language.
      if (response.status === 204) {
        result = ['No entries found for the selected language.', 'info'];
      } else {
        if (!response.ok) throw new Error(`The download failed (${response.status}).`);

        // The file name comes from the server, for example contact-submissions-en-20260225-1430.csv
        const disposition = response.headers.get('Content-Disposition') ?? '';
        const fileName = /filename="?([^";]+)"?/i.exec(disposition)?.[1] ?? form.fileName;

        // The link is not added to the page: the backoffice router catches the click of a link in the page.
        const url = URL.createObjectURL(await response.blob());
        const link = document.createElement('a');
        link.href = url;
        link.download = fileName;
        link.click();
        setTimeout(() => URL.revokeObjectURL(url), 5000);
        result = ['The file was downloaded successfully.', 'success'];
      }
    } catch (error) {
      result = [errorText(error), 'failed'];
    }

    // The spinner and the message change together, after the minimum busy time.
    await wait(Math.max(0, MIN_BUSY_MS - (Date.now() - started)));
    this._busy = { ...this._busy, [form.key]: false };
    this.#showMessage(form.key, ...result);
  }

  #renderForm(form) {
    const culture = this._cultures[form.key];
    const message = this._messages[form.key];

    return html`
      <uui-box>
        <div slot="headline" class="head">
          <span>${form.headline}</span>
          <span class="note">${form.note}</span>
        </div>

        <div class="row">
          <div class="field">
            <uui-label>Select language</uui-label>
            <uui-select
              label="Select language"
              .options=${LANGUAGES.map((l) => ({ ...l, selected: l.value === culture }))}
              @change=${(e) => (this._cultures = { ...this._cultures, [form.key]: e.target.value })}></uui-select>
          </div>

          <uui-button
            look="primary"
            color="positive"
            label="Export CSV"
            class=${this._busy[form.key] ? 'busy' : ''}
            aria-disabled=${this._busy[form.key] ? 'true' : 'false'}
            @click=${() => this.#download(form)}>
            ${this._busy[form.key]
              ? html`<uui-loader-circle></uui-loader-circle>`
              : html`<uui-icon name="icon-download-alt"></uui-icon>`}
            Export CSV
          </uui-button>
        </div>

        ${this.#renderMessage(message)}
      </uui-box>
    `;
  }

  // The line is always there (empty when there is no message), so it keeps its space.
  #renderMessage(message) {
    const classes = ['message', message?.kind, message?.visible ? 'visible' : ''].filter(Boolean).join(' ');
    return html`<p class=${classes} role="status">${message?.text ?? ''}</p>`;
  }

  render() {
    return html`${FORMS.map((form) => this.#renderForm(form))}`;
  }

  static styles = css`
    :host { display: block; padding: var(--uui-size-layout-1); }
    uui-box { max-width: 720px; }
    uui-box + uui-box { margin-top: var(--uui-size-layout-1); }
    .row { display: flex; gap: var(--uui-size-space-5); align-items: flex-end; flex-wrap: wrap; }
    .field { display: flex; flex-direction: column; gap: var(--uui-size-space-2); min-width: 220px; }
    uui-select { width: 100%; }
    /* The loader takes the place of the icon while the file is downloading, with the same size. */
    uui-button uui-icon, uui-button uui-loader-circle { margin-inline-end: var(--uui-size-space-3); }
    uui-button uui-loader-circle { font-size: 1em; vertical-align: middle; color: inherit; }
    /* While busy the button keeps its colour; it only stops taking clicks. */
    uui-button.busy { pointer-events: none; }
    .head { display: flex; flex-direction: column; gap: var(--uui-size-space-1); }
    .note { font-size: var(--uui-type-small-size); font-weight: normal; color: var(--uui-color-text-alt); }
    .message {
      margin: var(--uui-size-space-3) 0 0;
      font-size: var(--uui-type-small-size);
      line-height: 1.5;
      min-height: 1.5em;
      opacity: 0;
      transform: translateY(-2px);
      transition: opacity 300ms ease, transform 300ms ease;
    }
    .message.visible { opacity: 1; transform: none; }
    @media (prefers-reduced-motion: reduce) {
      .message { transition: none; }
    }
    .success { color: var(--uui-color-positive); }
    .info { color: var(--uui-color-text-alt); }
    .failed { color: var(--uui-color-danger); }
  `;
}

if (!customElements.get('mofam-form-submission-dashboard')) {
  customElements.define('mofam-form-submission-dashboard', MofamFormSubmissionDashboard);
}
