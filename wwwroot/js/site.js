// Cookie consent: first-visit banner, Accept/Reject stored for 1 year and
// logged server-side. Optional scripts load ONLY after an accepted choice.
// No optional providers are configured, and analytics stay OFF by default,
// so this hook currently loads nothing.
(function () {
    var COOKIE = 'acxiom_consent';
    var banner = document.getElementById('cookie-banner');
    if (!banner) return;

    function readChoice() {
        var m = document.cookie.match(new RegExp('(?:^|; )' + COOKIE + '=([^;]*)'));
        return m ? decodeURIComponent(m[1]).split('|')[0] : null;
    }

    function loadOptionalScripts(choice) {
        // Hook for future optional providers (analytics, chat, media).
        // Gate every optional script here behind: choice === 'accepted'.
        // Intentionally empty: no optional providers configured.
    }

    function record(choice) {
        var value = encodeURIComponent(choice + '|' + new Date().toISOString());
        document.cookie = COOKIE + '=' + value + '; path=/; max-age=31536000; samesite=lax';
        try {
            var body = new FormData();
            body.append('choice', choice);
            fetch('/Legal/Consent', { method: 'POST', body: new URLSearchParams(body) });
        } catch (e) { /* consent still stored locally */ }
        banner.hidden = true;
        loadOptionalScripts(choice);
    }

    window.acxiomCookiePreferences = function () {
        banner.hidden = false;
        var first = banner.querySelector('button');
        if (first) first.focus();
    };

    var existing = readChoice();
    if (existing) {
        loadOptionalScripts(existing);
        return;
    }
    banner.hidden = false;
    document.getElementById('cookie-accept').addEventListener('click', function () { record('accepted'); });
    document.getElementById('cookie-reject').addEventListener('click', function () { record('rejected'); });
})();

// Accessible mobile nav: aria-expanded sync, Esc to close, close on link click.
(function () {
    var toggle = document.getElementById('navToggle');
    var nav = document.getElementById('primaryNav');
    if (!toggle || !nav) return;

    function setExpanded(open) {
        toggle.setAttribute('aria-expanded', open ? 'true' : 'false');
    }

    nav.addEventListener('shown.bs.collapse', function () { setExpanded(true); });
    nav.addEventListener('hidden.bs.collapse', function () { setExpanded(false); });

    document.addEventListener('keydown', function (e) {
        if (e.key === 'Escape' && nav.classList.contains('show')) {
            var collapse = bootstrap.Collapse.getInstance(nav);
            if (collapse) collapse.hide();
            toggle.focus();
        }
    });

    nav.querySelectorAll('a').forEach(function (link) {
        link.addEventListener('click', function () {
            if (window.innerWidth < 768 && nav.classList.contains('show')) {
                var collapse = bootstrap.Collapse.getInstance(nav);
                if (collapse) collapse.hide();
            }
        });
    });
})();
