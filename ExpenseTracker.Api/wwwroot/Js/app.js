/* The settings, helpers, toast and theme code live in common.js */

const listEl = $('expense-list');
const emptyEl = $('empty-state');
const searchEl = $('search');
const paginationEl = $('pagination');
const pageInfoEl = $('page-info');
const prevBtn = $('prev-btn');
const nextBtn = $('next-btn');

const PAGE_SIZE = 10;
let page = 1;
let summary = null;     // numbers for ALL expenses (cards and charts), from /api/expenses/summary
let listData = { items: [], page: 1, totalPages: 1, totalCount: 0, totalAmount: 0 };   // the current page

/* ---------- Helpers ---------- */

// Very large totals are shown in compact form, with the full value on hover
const compactMoney = new Intl.NumberFormat(LOCALE, {
    style: 'currency',
    currency: CURRENCY,
    notation: 'compact',
    maximumFractionDigits: 2
});

function setStat(id, value) {
    const el = $(id);
    el.textContent = value >= 10000000 ? compactMoney.format(value) : money.format(value);
    el.title = money.format(value);
}

function formatDate(value) {
    return new Date(value).toLocaleDateString(LOCALE, {
        day: 'numeric', month: 'short', year: 'numeric'
    });
}

/* ---------- Summary cards ---------- */

function renderStats() {
    setStat('stat-total', summary.totalSpent);
    setStat('stat-month', summary.thisMonth);
    $('stat-count').textContent = summary.count;
    $('stat-top').textContent = summary.byCategory.length ? summary.byCategory[0].name : '-';
}

/* ---------- Expense list ---------- */

function updateResultsLine() {
    const { totalCount, totalAmount } = listData;
    const isFiltered = summary && totalCount !== summary.count;

    $('results-line').textContent = isFiltered
        ? `Showing ${totalCount} of ${summary.count} expenses · ${money.format(totalAmount)}`
        : `${totalCount} expense${totalCount === 1 ? '' : 's'} · ${money.format(totalAmount)}`;
}

function renderList() {
    const { items, totalCount, totalPages } = listData;
    updateResultsLine();

    listEl.innerHTML = '';
    emptyEl.hidden = totalCount > 0;

    paginationEl.hidden = totalCount <= PAGE_SIZE;
    pageInfoEl.textContent = `Page ${page} of ${totalPages} · ${totalCount} expenses`;
    prevBtn.disabled = page <= 1;
    nextBtn.disabled = page >= totalPages;

    for (const e of items) {
        const li = document.createElement('li');
        li.className = 'expense-item';
        li.style.setProperty('--c', e.categoryColor);    // the avatar and badge read this color

        const avatar = document.createElement('div');
        avatar.className = 'avatar';
        avatar.textContent = e.category.charAt(0).toUpperCase();

        const info = document.createElement('div');
        info.className = 'expense-info';

        const title = document.createElement('div');
        title.className = 'expense-title';
        title.textContent = e.description || e.category;

        const meta = document.createElement('div');
        meta.className = 'expense-meta';

        const badge = document.createElement('span');
        badge.className = 'badge';
        badge.textContent = e.category;

        meta.append(badge, formatDate(e.date));
        info.append(title, meta);

        const amount = document.createElement('div');
        amount.className = 'expense-amount';
        amount.textContent = money.format(e.amount);

        const edit = document.createElement('a');
        edit.className = 'edit-btn';
        edit.textContent = '✏️';
        edit.title = 'Edit';
        edit.href = `add.html?id=${e.id}`;

        const del = document.createElement('button');
        del.className = 'delete-btn';
        del.textContent = '🗑';
        del.title = 'Delete';
        del.addEventListener('click', () => deleteExpense(e.id));

        const actions = document.createElement('div');
        actions.className = 'row-actions';
        actions.append(edit, del);

        li.append(avatar, info, amount, actions);
        listEl.appendChild(li);
    }
}

/* ---------- Charts ---------- */

let categoryChart;
let trendChart;

if (typeof Chart !== 'undefined') {
    Chart.defaults.font.family = "'Inter', system-ui, sans-serif";
}

// Read the current theme's colors from the CSS variables
function themeColors() {
    const css = getComputedStyle(document.documentElement);
    const get = (name) => css.getPropertyValue(name).trim();
    return {
        text: get('--muted'),
        grid: get('--border'),
        surface: get('--surface'),
        primary: get('--primary')
    };
}

function renderCategoryChart(colors) {
    const entries = summary.byCategory;                       // already sorted, biggest first
    const top = entries.slice(0, 6);
    const rest = entries.slice(6).reduce((sum, c) => sum + c.total, 0);
    if (rest > 0) top.push({ name: 'Everything else', color: '#94a3b8', total: rest });

    categoryChart?.destroy();
    categoryChart = undefined;

    $('category-empty').hidden = entries.length > 0;
    $('category-chart').hidden = entries.length === 0;
    if (entries.length === 0) return;

    categoryChart = new Chart($('category-chart'), {
        type: 'doughnut',
        data: {
            labels: top.map((c) => c.name),
            datasets: [{
                data: top.map((c) => c.total),
                backgroundColor: top.map((c) => c.color),
                borderColor: colors.surface,
                borderWidth: 3
            }]
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            cutout: '65%',
            plugins: {
                legend: {
                    position: 'bottom',
                    labels: { color: colors.text, usePointStyle: true, boxWidth: 8 }
                },
                tooltip: {
                    callbacks: { label: (ctx) => ` ${ctx.label}: ${money.format(ctx.parsed)}` }
                }
            }
        }
    });
}

function renderTrendChart(colors) {
    const months = summary.months.map((m) => {
        const [year, month] = m.month.split('-').map(Number);
        return {
            label: new Date(year, month - 1, 1).toLocaleDateString(LOCALE, { month: 'short', year: '2-digit' }),
            total: m.total
        };
    });

    trendChart?.destroy();
    trendChart = new Chart($('trend-chart'), {
        type: 'bar',
        data: {
            labels: months.map((m) => m.label),
            datasets: [{
                data: months.map((m) => m.total),
                // The current month is solid, earlier months are lighter
                backgroundColor: months.map((_, i) => (i === months.length - 1 ? colors.primary : colors.primary + '88')),
                borderRadius: 8,
                maxBarThickness: 36
            }]
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            plugins: {
                legend: { display: false },
                tooltip: {
                    callbacks: { label: (ctx) => ` ${money.format(ctx.parsed.y)}` }
                }
            },
            scales: {
                x: { ticks: { color: colors.text }, grid: { display: false }, border: { display: false } },
                y: {
                    beginAtZero: true,
                    ticks: { color: colors.text, callback: (value) => compactMoney.format(value) },
                    grid: { color: colors.grid },
                    border: { display: false }
                }
            }
        }
    });
}

function renderCharts() {
    // If the Chart.js library failed to load (for example, no internet), skip the charts
    if (typeof Chart === 'undefined' || !summary) return;

    const colors = themeColors();
    renderCategoryChart(colors);
    renderTrendChart(colors);
}

window.addEventListener('themechange', renderCharts);

/* ---------- Budgets ---------- */

function renderBudgets(items) {
    const list = $('budget-list');
    list.innerHTML = '';
    $('budget-empty').hidden = items.length > 0;
    $('budget-month').textContent = '· ' + new Date().toLocaleDateString(LOCALE, { month: 'long', year: 'numeric' });

    // The categories closest to (or over) their limit come first
    const rows = items
        .map((b) => ({ ...b, pct: (b.spent / b.budget) * 100 }))
        .sort((a, b) => b.pct - a.pct);

    for (const b of rows) {
        const state = b.pct >= 100 ? 'over' : b.pct >= 80 ? 'warn' : 'ok';

        const row = document.createElement('div');
        row.className = `budget-row ${state}`;
        row.style.setProperty('--c', b.color);

        const head = document.createElement('div');
        head.className = 'budget-head';

        const name = document.createElement('span');
        name.className = 'budget-name';
        const dot = document.createElement('span');
        dot.className = 'budget-dot';
        name.append(dot, b.name);

        const amounts = document.createElement('span');
        amounts.className = 'budget-amounts';
        amounts.textContent = `${money.format(b.spent)} of ${money.format(b.budget)}`;

        head.append(name, amounts);

        const track = document.createElement('div');
        track.className = 'budget-track';
        const bar = document.createElement('div');
        bar.className = 'budget-bar';
        bar.style.width = `${Math.min(b.pct, 100)}%`;
        track.appendChild(bar);

        const note = document.createElement('div');
        note.className = 'budget-note';
        const left = b.budget - b.spent;
        note.textContent = left >= 0
            ? `${money.format(left)} left · ${Math.round(b.pct)}% used`
            : `Over by ${money.format(-left)} · ${Math.round(b.pct)}% used`;

        row.append(head, track, note);
        list.appendChild(row);
    }
}

/* ---------- Loading data from the API ---------- */

async function loadSummary() {
    try {
        const res = await fetch(`${API}/summary`);
        if (!res.ok) throw new Error('Request failed');
        summary = await res.json();
    } catch {
        showToast('Could not reach the server', 'error');
        return;
    }

    renderStats();
    renderCharts();
    updateResultsLine();
}

async function loadBudgets() {
    try {
        const res = await fetch('/api/budgets/status');
        if (!res.ok) throw new Error('Request failed');
        renderBudgets(await res.json());
    } catch {
        /* the budget card is optional, so no error toast here */
    }
}

/* ---------- Filters ---------- */

const fCategory = $('f-category');
const fFrom = $('f-from');
const fTo = $('f-to');
const fMin = $('f-min');
const fMax = $('f-max');
const fSort = $('f-sort');

function buildParams() {
    const p = new URLSearchParams();
    const search = searchEl.value.trim();
    if (search) p.set('search', search);
    if (fCategory.value) p.set('categoryId', fCategory.value);
    if (fFrom.value) p.set('from', fFrom.value);
    if (fTo.value) p.set('to', fTo.value);
    if (fMin.value !== '') p.set('minAmount', fMin.value);
    if (fMax.value !== '') p.set('maxAmount', fMax.value);
    p.set('sort', fSort.value);
    p.set('page', page);
    p.set('pageSize', PAGE_SIZE);
    return p;
}

let listRequestId = 0;

// Asks the server for the current page, using the current filters, sort and page number
async function loadList() {
    const requestId = ++listRequestId;      // every call invalidates older calls still in flight

    try {
        const res = await fetch(`${API}?${buildParams()}`);
        if (!res.ok) {
            const err = await res.json().catch(() => null);
            throw new Error(err?.message || 'Could not load expenses');
        }
        const data = await res.json();
        if (requestId !== listRequestId) return;     // a newer request has started, so ignore this one

        listData = data;
        page = data.page;                            // the server fixes a page number that is out of range
    } catch (err) {
        if (requestId === listRequestId) showToast(err.message, 'error');
        return;
    }

    renderList();
}

function refreshAll() {
    return Promise.all([loadSummary(), loadBudgets(), loadList()]);
}

function updateFilterBadge() {
    const count = [fCategory.value, fFrom.value, fTo.value, fMin.value, fMax.value]
        .filter((v) => v !== '').length;
    $('filter-count').textContent = count;
    $('filter-count').hidden = count === 0;
}

function onFilterChange() {
    page = 1;
    updateFilterBadge();
    loadList();
}

function setMonthRange(offset) {
    const now = new Date();
    fFrom.value = toDateStr(new Date(now.getFullYear(), now.getMonth() + offset, 1));
    fTo.value = toDateStr(new Date(now.getFullYear(), now.getMonth() + offset + 1, 0));
    onFilterChange();
}

async function loadFilterCategories() {
    try {
        const res = await fetch('/api/categories');
        if (!res.ok) return;
        for (const c of await res.json()) fCategory.add(new Option(c.name, c.id));
    } catch {
        /* the category filter is optional */
    }
}

// Wait until the user stops typing before asking the server
let searchTimer;
searchEl.addEventListener('input', () => {
    clearTimeout(searchTimer);
    searchTimer = setTimeout(onFilterChange, 300);
});

[fCategory, fFrom, fTo, fMin, fMax, fSort].forEach((el) =>
    el.addEventListener('change', onFilterChange)
);

$('filter-toggle').addEventListener('click', () => {
    $('filters').hidden = !$('filters').hidden;
});

$('q-this').addEventListener('click', () => setMonthRange(0));
$('q-last').addEventListener('click', () => setMonthRange(-1));

$('filter-clear').addEventListener('click', () => {
    [fCategory, fFrom, fTo, fMin, fMax].forEach((el) => { el.value = ''; });
    fSort.value = 'date_desc';
    searchEl.value = '';
    onFilterChange();
});

/* ---------- Pagination ---------- */

function goToPage(newPage) {
    page = newPage;
    loadList();
    listEl.closest('.card').scrollIntoView({ behavior: 'smooth', block: 'start' });
}

prevBtn.addEventListener('click', () => goToPage(page - 1));
nextBtn.addEventListener('click', () => goToPage(page + 1));

/* ---------- Delete ---------- */

async function deleteExpense(id) {
    if (!confirm('Delete this expense?')) return;
    try {
        const res = await fetch(`${API}/${id}`, { method: 'DELETE' });
        if (!res.ok) throw new Error('Delete failed');
        showToast('Expense deleted');
        await refreshAll();
    } catch {
        showToast('Could not delete expense', 'error');
    }
}

/* ---------- Export to Excel ---------- */

const exportDialog = $('export-dialog');
const exportFrom = $('export-from');
const exportTo = $('export-to');
const exportHint = $('export-hint');
const exportBtn = $('export-download');
const chips = document.querySelectorAll('.chip');

// Subtract months safely (for example 31 Mar minus 1 month = 28/29 Feb)
function addMonths(date, n) {
    const d = new Date(date);
    const day = d.getDate();
    d.setDate(1);
    d.setMonth(d.getMonth() + n);
    const lastDay = new Date(d.getFullYear(), d.getMonth() + 1, 0).getDate();
    d.setDate(Math.min(day, lastDay));
    return d;
}

function presetRange(key) {
    const today = new Date();
    switch (key) {
        case 'today':
            return [today, today];
        case 'week': {
            const start = new Date(today);
            start.setDate(start.getDate() - 6);          // last 7 days including today
            return [start, today];
        }
        case 'month':
            return [addMonths(today, -1), today];
        case 'year':
            return [addMonths(today, -12), today];
        default:
            return [null, null];                          // all time
    }
}

function setActiveChip(key) {
    chips.forEach((c) => c.classList.toggle('active', c.dataset.range === key));
}

let hintRequestId = 0;

// Asks the server how many expenses fall in the chosen range (a page size of 1 keeps it tiny)
async function updateExportHint() {
    const from = exportFrom.value;
    const to = exportTo.value;

    if (from && to && from > to) {
        exportHint.textContent = '"From" date must be before "To" date.';
        exportHint.className = 'export-hint error';
        exportBtn.disabled = true;
        return;
    }

    const requestId = ++hintRequestId;
    exportHint.className = 'export-hint';
    exportHint.textContent = 'Checking...';
    exportBtn.disabled = true;

    const p = new URLSearchParams({ pageSize: 1 });
    if (from) p.set('from', from);
    if (to) p.set('to', to);

    try {
        const res = await fetch(`${API}?${p}`);
        if (!res.ok) throw new Error('Request failed');
        const data = await res.json();
        if (requestId !== hintRequestId) return;

        exportHint.textContent = data.totalCount
            ? `${data.totalCount} expense${data.totalCount === 1 ? '' : 's'} · ${money.format(data.totalAmount)} will be exported`
            : 'No expenses in this range.';
        exportBtn.disabled = data.totalCount === 0;
    } catch {
        if (requestId !== hintRequestId) return;
        exportHint.textContent = 'Could not check this range.';
        exportBtn.disabled = false;
    }
}

function applyPreset(key) {
    const [from, to] = presetRange(key);
    exportFrom.value = from ? toDateStr(from) : '';
    exportTo.value = to ? toDateStr(to) : '';
    setActiveChip(key);
    updateExportHint();
}

$('export-open').addEventListener('click', () => {
    applyPreset('month');
    exportDialog.showModal();
});

$('export-close').addEventListener('click', () => exportDialog.close());

// Clicking the dark backdrop closes the popup
exportDialog.addEventListener('click', (ev) => {
    if (ev.target === exportDialog) exportDialog.close();
});

chips.forEach((chip) =>
    chip.addEventListener('click', () => applyPreset(chip.dataset.range))
);

[exportFrom, exportTo].forEach((input) =>
    input.addEventListener('change', () => {
        setActiveChip(null);
        updateExportHint();
    })
);

exportBtn.addEventListener('click', async () => {
    const params = new URLSearchParams();
    if (exportFrom.value) params.set('from', exportFrom.value);
    if (exportTo.value) params.set('to', exportTo.value);

    exportBtn.disabled = true;
    exportBtn.textContent = 'Preparing...';

    try {
        const res = await fetch(`${API}/export?${params}`);
        if (!res.ok) {
            const err = await res.json().catch(() => null);
            throw new Error(err?.message || 'Export failed');
        }

        const blob = await res.blob();
        const url = URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = (exportFrom.value || exportTo.value)
            ? `expenses_${exportFrom.value || 'start'}_to_${exportTo.value || 'end'}.xlsx`
            : 'expenses_all.xlsx';
        document.body.appendChild(link);
        link.click();
        link.remove();
        URL.revokeObjectURL(url);

        exportDialog.close();
        showToast('Excel file downloaded');
    } catch (err) {
        showToast(err.message || 'Export failed', 'error');
    } finally {
        exportBtn.textContent = 'Download Excel';
        updateExportHint();
    }
});

/* ---------- Start ---------- */

showFlash();        // shows "Expense added" after coming back from the Add page
loadFilterCategories();
refreshAll();