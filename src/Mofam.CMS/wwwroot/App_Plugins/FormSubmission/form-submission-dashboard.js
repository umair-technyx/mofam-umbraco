import { LitElement, html, css, nothing } from '@umbraco-cms/backoffice/external/lit';
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
    _busy: { state: true }, // the key of the box that is downloading
    _messages: { state: true }, // the message of each box: { text, failed }
  };

  #auth;

  constructor() {
    super();
    this._cultures = Object.fromEntries(FORMS.map((f) => [f.key, 'en']));
    this._busy = null;
    this._messages = {};

    this.consumeContext(UMB_AUTH_CONTEXT, (context) => {
      this.#auth = context;
    });
  }

  async #download(form) {
    this._busy = form.key;
    this._messages = { ...this._messages, [form.key]: null };

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
        this._messages = { ...this._messages, [form.key]: { text: 'No entries found for the selected language.', failed: false } };
        return;
      }

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
    } catch (error) {
      this._messages = { ...this._messages, [form.key]: { text: errorText(error), failed: true } };
    } finally {
      this._busy = null;
    }
  }

  #renderForm(form) {
    const culture = this._cultures[form.key];
    const message = this._messages[form.key];

    return html`
      <uui-box headline=${form.headline}>
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
            .state=${this._busy === form.key ? 'waiting' : undefined}
            ?disabled=${this._busy !== null}
            @click=${() => this.#download(form)}>
            <uui-icon name="icon-download-alt"></uui-icon>
            Export CSV
          </uui-button>
        </div>

        <div class="note">
          <uui-icon name="icon-info"></uui-icon>
          <span>${form.note}</span>
        </div>

        ${this.#renderMessage(message)}
      </uui-box>
    `;
  }

  #renderMessage(message) {
    if (!message) return nothing;

    const kind = message.failed ? 'failed' : 'info';
    return html`<p class=${kind}>${message.text}</p>`;
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
    uui-button uui-icon { margin-inline-end: var(--uui-size-space-3); }
    .note {
      display: flex; gap: var(--uui-size-space-3); align-items: flex-start;
      margin-top: var(--uui-size-space-5); padding-top: var(--uui-size-space-4);
      border-top: 1px solid var(--uui-color-divider-standard);
      font-size: var(--uui-type-small-size); color: var(--uui-color-text-alt);
    }
    .note uui-icon { flex: none; margin-top: 2px; }
    .failed, .info { margin: var(--uui-size-space-4) 0 0; }
    .failed { color: var(--uui-color-danger); }
    .info { color: var(--uui-color-text-alt); }
  `;
}

if (!customElements.get('mofam-form-submission-dashboard')) {
  customElements.define('mofam-form-submission-dashboard', MofamFormSubmissionDashboard);
}
