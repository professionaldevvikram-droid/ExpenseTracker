const CAT_API = '/api/categories';

const form = $('category-form');
const nameEl = $('cat-name');
const colorEl = $('cat-color');
const budgetEl = $('cat-budget');
const submitBtn = $('cat-submit');
const cancelBtn = $('cat-cancel');
const listEl = $('category-list');

let categories = [];
let editingId = null;      // null means we are adding a new category

async function readError(res, fallback) {
    const err = await res.json().catch(() => null);
    return err?.message || fallback;
}

async function load() {
    try {
        const res = await fetch(CAT_API);
        if (!res.ok) throw new Error('Request failed');
        categories = await res.json();
        render();
    } catch {
        showToast('Could not load categories', 'error');
    }
}

function render() {
    listEl.innerHTML = '';

    for (const c of categories) {
        const count = c.expenseCount;

        const li = document.createElement('li');
        li.className = 'expense-item';
        li.style.setProperty('--c', c.color);

        const avatar = document.createElement('div');
        avatar.className = 'avatar';
        avatar.textContent = c.name.charAt(0).toUpperCase();

        const info = document.createElement('div');
        info.className = 'expense-info';

        const title = document.createElement('div');
        title.className = 'expense-title';
        title.textContent = c.name;

        const meta = document.createElement('div');
        meta.className = 'expense-meta';
        meta.textContent = `${count} expense${count === 1 ? '' : 's'}`
            + (c.monthlyBudget ? ` · Budget ${money.format(c.monthlyBudget)} / month` : '');

        info.append(title, meta);

        const edit = document.createElement('button');
        edit.type = 'button';
        edit.className = 'edit-btn';
        edit.textContent = '✏️';
        edit.title = 'Edit';
        edit.addEventListener('click', () => startEdit(c));

        const del = document.createElement('button');
        del.type = 'button';
        del.className = 'delete-btn';
        del.textContent = '🗑';
        del.disabled = count > 0;                       // a category in use can't be deleted
        del.title = count > 0 ? `Used by ${count} expense${count === 1 ? '' : 's'}` : 'Delete';
        del.addEventListener('click', () => deleteCategory(c));

        const actions = document.createElement('div');
        actions.className = 'row-actions';
        actions.append(edit, del);

        li.append(avatar, info, actions);
        listEl.appendChild(li);
    }
}

function startEdit(c) {
    editingId = c.id;
    nameEl.value = c.name;
    colorEl.value = c.color;
    budgetEl.value = c.monthlyBudget ?? '';
    $('form-title').textContent = 'Edit category';
    submitBtn.textContent = 'Save changes';
    cancelBtn.hidden = false;
    window.scrollTo({ top: 0, behavior: 'smooth' });
    nameEl.focus();
}

function resetForm() {
    editingId = null;
    form.reset();                                     // color and budget go back to the defaults
    $('form-title').textContent = 'Add category';
    submitBtn.textContent = 'Add category';
    cancelBtn.hidden = true;
}

cancelBtn.addEventListener('click', resetForm);

form.addEventListener('submit', async (ev) => {
    ev.preventDefault();

    const isEdit = editingId !== null;
    const body = {
        name: nameEl.value.trim(),
        color: colorEl.value,
        monthlyBudget: budgetEl.value === '' ? null : parseFloat(budgetEl.value)
    };

    submitBtn.disabled = true;
    try {
        const res = await fetch(isEdit ? `${CAT_API}/${editingId}` : CAT_API, {
            method: isEdit ? 'PUT' : 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(body)
        });
        if (!res.ok) throw new Error(await readError(res, 'Could not save category'));

        showToast(isEdit ? 'Category updated' : 'Category added');
        resetForm();
        await load();
    } catch (err) {
        showToast(err.message, 'error');
    } finally {
        submitBtn.disabled = false;
    }
});

async function deleteCategory(c) {
    if (!confirm(`Delete the category "${c.name}"?`)) return;

    try {
        const res = await fetch(`${CAT_API}/${c.id}`, { method: 'DELETE' });
        if (!res.ok) throw new Error(await readError(res, 'Could not delete category'));

        showToast('Category deleted');
        if (editingId === c.id) resetForm();
        await load();
    } catch (err) {
        showToast(err.message, 'error');
    }
}

load();