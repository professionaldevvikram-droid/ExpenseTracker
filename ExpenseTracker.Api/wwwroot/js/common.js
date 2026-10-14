/* ---------- Shared settings ---------- */

const API = '/api/expenses';

// Change these to your own currency and locale (for example 'USD' and 'en-US')
const CURRENCY = 'INR';
const LOCALE = 'en-IN';

const money = new Intl.NumberFormat(LOCALE, { style: 'currency', currency: CURRENCY });

const $ = (id) => document.getElementById(id);

/* ---------- Authentication ---------- */

const TOKEN_KEY = 'token';
const USER_KEY = 'user';
const PUBLIC_PAGES = ['login.html', 'register.html'];

let redirecting = false;

function getToken() {
    return localStorage.getItem(TOKEN_KEY);
}

function getUser() {
    try { return JSON.parse(localStorage.getItem(USER_KEY)); } catch { return null; }
}

function saveSession(auth) {
    localStorage.setItem(TOKEN_KEY, auth.token);
    localStorage.setItem(USER_KEY, JSON.stringify(auth.user));
}

function clearSession() {
    localStorage.removeItem(TOKEN_KEY);
    localStorage.removeItem(USER_KEY);
}

function isPublicPage() {
    return PUBLIC_PAGES.includes(window.location.pathname.split('/').pop());
}

// Reads the expiry time stored inside the token (no request to the server needed)
function tokenExpired(token) {
    try {
        const payload = JSON.parse(atob(token.split('.')[1].replace(/-/g, '+').replace(/_/g, '/')));
        return payload.exp * 1000 < Date.now();
    } catch {
        return true;
    }
}

function redirectToLogin(expired) {
    redirecting = true;
    clearSession();
    window.location.replace(expired ? 'login.html?expired=1' : 'login.html');
}

function logout() {
    clearSession();
    window.location.href = 'login.html';
}

// Protected pages need a valid token
if (!isPublicPage()) {
    const token = getToken();
    if (!token) redirectToLogin(false);
    else if (tokenExpired(token)) redirectToLogin(true);
}

// Every request to /api/... automatically carries the token
const nativeFetch = window.fetch.bind(window);

window.fetch = async (input, init = {}) => {
    const url = typeof input === 'string' ? input : input.url;
    const isApi = url.startsWith('/api/');
    const isLoginOrRegister = url.startsWith('/api/auth/login') || url.startsWith('/api/auth/register');

    if (!isApi || isLoginOrRegister) return nativeFetch(input, init);
    if (redirecting) return new Promise(() => { });       // the page is leaving, so send nothing

    const headers = new Headers(init.headers || {});
    const token = getToken();
    if (token) headers.set('Authorization', `Bearer ${token}`);

    const res = await nativeFetch(input, { ...init, headers });

    if (res.status === 401) {                            // the server rejected the token
        redirectToLogin(true);
        return new Promise(() => { });
    }
    return res;
};

/* ---------- Dates ---------- */

function toDateStr(d) {
    const month = String(d.getMonth() + 1).padStart(2, '0');
    const day = String(d.getDate()).padStart(2, '0');
    return `${d.getFullYear()}-${month}-${day}`;
}

function todayStr() {
    return toDateStr(new Date());
}

/* ---------- Toast messages ---------- */

const toastEl = $('toast');
let toastTimer;

function showToast(message, type = 'success') {
    toastEl.textContent = message;
    toastEl.className = `toast show ${type === 'error' ? 'error' : ''}`;
    clearTimeout(toastTimer);
    toastTimer = setTimeout(() => toastEl.classList.remove('show'), 2500);
}

// Lets one page leave a message for the next page (used after saving an expense)
function setFlash(message) {
    try { sessionStorage.setItem('flash', message); } catch { /* ignore */ }
}

function showFlash() {
    try {
        const message = sessionStorage.getItem('flash');
        if (message) {
            sessionStorage.removeItem('flash');
            showToast(message);
        }
    } catch { /* ignore */ }
}

/* ---------- Theme (light / dark) ---------- */

function applyTheme(theme) {
    document.documentElement.setAttribute('data-theme', theme);
    $('theme-toggle').textContent = theme === 'dark' ? '☀️' : '🌙';
    localStorage.setItem('theme', theme);
}

applyTheme(
    localStorage.getItem('theme') ||
    (window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light')
);

$('theme-toggle').addEventListener('click', () => {
    const current = document.documentElement.getAttribute('data-theme');
    applyTheme(current === 'dark' ? 'light' : 'dark');
});

function applyTheme(theme) {
    document.documentElement.setAttribute('data-theme', theme);
    $('theme-toggle').textContent = theme === 'dark' ? '☀️' : '🌙';
    localStorage.setItem('theme', theme);
    window.dispatchEvent(new Event('themechange'));
}

/* ---------- User menu (name and Log out button in the top bar) ---------- */

function renderUserMenu() {
    const user = getUser();
    const toggle = $('theme-toggle');
    if (!user || !toggle) return;

    // Some pages have the theme button directly in the header, so give it a wrapper first
    let actions = toggle.parentElement;
    if (!actions.classList.contains('topbar-actions')) {
        actions = document.createElement('div');
        actions.className = 'topbar-actions';
        toggle.replaceWith(actions);
        actions.appendChild(toggle);
    }

    const menu = document.createElement('div');
    menu.className = 'user-menu';

    const avatar = document.createElement('span');
    avatar.className = 'user-avatar';
    avatar.textContent = user.name.charAt(0).toUpperCase();

    const name = document.createElement('span');
    name.className = 'user-name';
    name.textContent = user.name;
    name.title = user.email;

    const logoutBtn = document.createElement('button');
    logoutBtn.type = 'button';
    logoutBtn.className = 'btn-secondary';
    logoutBtn.textContent = 'Log out';
    logoutBtn.addEventListener('click', logout);

    menu.append(avatar, name, logoutBtn);
    actions.insertBefore(menu, toggle);
}

if (!isPublicPage()) {
    renderUserMenu();

    // Confirms the account still exists (a 401 here sends you to the login page)
    fetch('/api/auth/me')
        .then((res) => (res.ok ? res.json() : null))
        .then((user) => { if (user) localStorage.setItem(USER_KEY, JSON.stringify(user)); })
        .catch(() => { /* a network problem is handled by the page itself */ });
}