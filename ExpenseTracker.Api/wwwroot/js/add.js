const form = $('expense-form');
const submitBtn = $('submit-btn');
const categorySelect = $('category');

// add.html          -> add a new expense
// add.html?id=5     -> edit expense 5
const editId = new URLSearchParams(window.location.search).get('id');
const isEdit = editId !== null;

if (isEdit) {
    document.title = 'Edit expense';
    $('page-title').textContent = 'Edit expense';
    $('page-subtitle').textContent = 'Update the details below';
    submitBtn.textContent = 'Save changes';
    submitBtn.disabled = true;               // enabled again once the expense has loaded
} else {
    $('date').value = todayStr();
}

async function loadCategories() {
    const res = await fetch('/api/categories');
    if (!res.ok) throw new Error('Could not load categories');
    const categories = await res.json();

    categorySelect.innerHTML = '';
    const placeholder = new Option('Select a category', '', true, true);
    placeholder.disabled = true;
    categorySelect.add(placeholder);

    for (const c of categories) {
        categorySelect.add(new Option(c.name, c.id));
    }
}

// Fill the form with the expense being edited
async function loadExpense() {
    try {
        const res = await fetch(`${API}/${editId}`);
        if (!res.ok) throw new Error('Not found');
        const e = await res.json();

        $('amount').value = e.amount;
        categorySelect.value = e.categoryId;
        $('description').value = e.description ?? '';
        $('date').value = e.date.slice(0, 10);

        submitBtn.disabled = false;
    } catch {
        showToast('Expense not found', 'error');
        setTimeout(() => { window.location.href = 'index.html'; }, 1200);
    }
}

form.addEventListener('submit', async (ev) => {
    ev.preventDefault();

    const expense = {
        amount: parseFloat($('amount').value),
        categoryId: parseInt(categorySelect.value, 10),
        description: $('description').value.trim() || null,
        date: $('date').value
    };

    submitBtn.disabled = true;
    submitBtn.textContent = 'Saving...';

    try {
        const res = await fetch(isEdit ? `${API}/${editId}` : API, {
            method: isEdit ? 'PUT' : 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(expense)
        });
        if (!res.ok) throw new Error('Save failed');

        setFlash(isEdit ? 'Expense updated' : 'Expense added');
        window.location.href = 'index.html';
    } catch {
        showToast('Could not save expense', 'error');
        submitBtn.disabled = false;
        submitBtn.textContent = isEdit ? 'Save changes' : 'Save expense';
    }
});

async function init() {
    try {
        await loadCategories();
    } catch {
        showToast('Could not load categories', 'error');
        return;
    }

    if (isEdit) {
        await loadExpense();
    } else {
        $('amount').focus();
    }
}

init();