// Already logged in? Go straight to the app
const existingToken = getToken();
if (existingToken && !tokenExpired(existingToken)) {
    window.location.replace('index.html');
}

const form = $('register-form');
const submitBtn = $('submit-btn');

form.addEventListener('submit', async (ev) => {
    ev.preventDefault();

    if ($('password').value !== $('confirm').value) {
        showToast('The two passwords do not match.', 'error');
        return;
    }

    submitBtn.disabled = true;
    submitBtn.textContent = 'Creating account...';

    try {
        const res = await fetch('/api/auth/register', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                name: $('name').value.trim(),
                email: $('email').value.trim(),
                password: $('password').value
            })
        });

        const data = await res.json().catch(() => null);
        if (!res.ok) throw new Error(data?.message || 'Could not create the account');

        saveSession(data);          // registering also logs you in
        window.location.replace('index.html');
    } catch (err) {
        showToast(err.message, 'error');
        submitBtn.disabled = false;
        submitBtn.textContent = 'Create account';
    }
});

$('name').focus();