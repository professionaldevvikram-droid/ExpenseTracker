// Already logged in? Go straight to the app
const existingToken = getToken();
if (existingToken && !tokenExpired(existingToken)) {
    window.location.replace('index.html');
}

if (new URLSearchParams(window.location.search).get('expired')) {
    showToast('Your session has ended. Please log in again.', 'error');
}

const form = $('login-form');
const submitBtn = $('submit-btn');

form.addEventListener('submit', async (ev) => {
    ev.preventDefault();

    submitBtn.disabled = true;
    submitBtn.textContent = 'Logging in...';

    try {
        const res = await fetch('/api/auth/login', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                email: $('email').value.trim(),
                password: $('password').value
            })
        });

        const data = await res.json().catch(() => null);
        if (!res.ok) throw new Error(data?.message || 'Could not log in');

        saveSession(data);
        window.location.replace('index.html');
    } catch (err) {
        showToast(err.message, 'error');
        submitBtn.disabled = false;
        submitBtn.textContent = 'Log in';
    }
});

$('email').focus();