// Progressive enhancement: both examples and every chapter work without JavaScript.
(() => {
  const key = 'migrator-code-style';
  let preferred = 'classic';
  try { if (localStorage.getItem(key) === 'fluent') preferred = 'fluent'; } catch { /* Storage may be disabled. */ }
  const examples = [...document.querySelectorAll('.code-example')];
  function choose(style) {
    preferred = style;
    examples.forEach(example => {
      example.querySelectorAll('[data-style]').forEach(button => {
        const active = button.dataset.style === style;
        button.setAttribute('aria-selected', String(active));
        button.tabIndex = active ? 0 : -1;
      });
      example.querySelectorAll('[data-code-style]').forEach(panel => { panel.hidden = panel.dataset.codeStyle !== style; });
    });
    try { localStorage.setItem(key, style); } catch { /* Preference is optional. */ }
  }
  examples.forEach(example => {
    const tabs = example.querySelector('.code-tabs');
    tabs.hidden = false;
    tabs.setAttribute('role', 'tablist');
    tabs.querySelectorAll('button').forEach(button => {
      button.setAttribute('role', 'tab');
      const panel = document.getElementById(button.getAttribute('aria-controls'));
      panel.setAttribute('role', 'tabpanel');
      panel.setAttribute('aria-labelledby', button.id);
      button.addEventListener('click', () => choose(button.dataset.style));
      button.addEventListener('keydown', event => {
        if (!['ArrowLeft', 'ArrowRight', 'Home', 'End'].includes(event.key)) return;
        event.preventDefault();
        const style = event.key === 'Home' ? 'classic' : event.key === 'End' ? 'fluent' : preferred === 'classic' ? 'fluent' : 'classic';
        choose(style);
        tabs.querySelector(`[data-style="${style}"]`).focus();
      });
    });
  });
  choose(preferred);
  const status = document.getElementById('copy-status');
  if (navigator.clipboard && window.isSecureContext) {
    document.querySelectorAll('[data-copy]').forEach(button => {
      button.hidden = false;
      button.addEventListener('click', async () => {
        try {
          await navigator.clipboard.writeText(document.getElementById(button.dataset.copy).textContent);
          button.textContent = 'Copied';
          status.textContent = 'Code copied to clipboard.';
          setTimeout(() => { button.textContent = 'Copy'; }, 2000);
        } catch { status.textContent = 'Unable to copy. Select the code and copy it manually.'; }
      });
    });
  }
  const chapters = document.querySelector('.chapter-menu');
  if (chapters && matchMedia('(max-width: 680px)').matches) chapters.open = false;
  function revealSource() {
    const id = location.hash.slice(1);
    const sources = document.getElementById('sources');
    if (sources && (id === 'sources' || id.startsWith('source-'))) {
      sources.open = true;
      document.getElementById(id)?.scrollIntoView();
    }
  }
  window.addEventListener('hashchange', revealSource);
  revealSource();
  const search = document.querySelector('.search-wrap');
  if (!search) return;
  search.hidden = false;
  const input = search.querySelector('input');
  const popover = search.querySelector('.search-popover');
  const results = search.querySelector('ul');
  const searchStatus = search.querySelector('[role=status]');
  const indexUrl = new URL(search.dataset.searchIndex, document.baseURI);
  let indexPromise;
  let request = 0;
  async function runSearch() {
    const current = ++request;
    const query = input.value.trim().toLowerCase();
    results.replaceChildren();
    if (!query) { popover.hidden = true; return; }
    popover.hidden = false;
    searchStatus.textContent = 'Searching…';
    try {
      indexPromise ??= fetch(indexUrl).then(response => {
        if (!response.ok) throw new Error('Search index unavailable');
        return response.json();
      });
      const index = await indexPromise;
      if (current !== request) return;
      const words = query.split(/\s+/);
      const ranked = index.map(entry => {
        const heading = `${entry.title} ${entry.group}`.toLowerCase();
        const text = `${heading} ${entry.summary} ${entry.text}`.toLowerCase();
        return { entry, score: words.every(word => text.includes(word)) ? words.reduce((sum, word) => sum + (heading.includes(word) ? 10 : 1), 0) : 0 };
      }).filter(hit => hit.score > 0).sort((a, b) => b.score - a.score).slice(0, 8);
      searchStatus.textContent = ranked.length ? `${ranked.length} matching chapters` : 'No chapters found. Try a database name or an operation.';
      ranked.forEach(({ entry }) => {
        const li = document.createElement('li');
        const link = document.createElement('a');
        link.href = new URL('../' + entry.url, indexUrl).href;
        const title = document.createElement('strong'); title.textContent = entry.title;
        const description = document.createElement('span'); description.textContent = `${entry.group} · ${entry.summary}`;
        link.append(title, description); li.append(link); results.append(li);
      });
    } catch {
      if (current !== request) return;
      indexPromise = undefined;
      searchStatus.textContent = 'Search is unavailable. Use the documentation chapter navigation.';
    }
  }
  input.addEventListener('input', runSearch);
  input.addEventListener('focus', () => { if (input.value.trim()) runSearch(); });
  search.querySelector('form').addEventListener('submit', event => { event.preventDefault(); runSearch(); });
  input.addEventListener('keydown', event => {
    if (event.key === 'ArrowDown') { const first = results.querySelector('a'); if (first) { event.preventDefault(); first.focus(); } }
  });
  search.addEventListener('keydown', event => {
    if (event.key === 'Escape') { ++request; popover.hidden = true; input.focus(); popover.hidden = true; }
  });
  document.addEventListener('click', event => { if (!search.contains(event.target)) { ++request; popover.hidden = true; } });
})();
