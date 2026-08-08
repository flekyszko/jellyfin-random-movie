(function () {
    'use strict';

    var PLUGIN_ID = '1F9B27A8-2C4E-4D6A-9C45-3A0A6B6F9C1B';

var I18N = {
    hu: {
        title: 'Véletlen film ajánló',
        language: 'Nyelv',
        genre: 'Műfaj',
        year: 'Év',
        rating: 'Minimális értékelés (0-10)',
        genreAny: 'Bármelyik',
        yearAny: 'Bármelyik',
        ratingAny: 'Bármilyen',
        getMovie: 'Görgetés',
        loading: 'Keresés…',
        noKey: 'A TMDB API kulcs nincs beállítva.\nÁllítsd be: Dashboard → Plugins → Random Movie.',
        noResult: 'Nem találtam ilyen filmet. Próbálj lazítani a szűrőkön.\n(Lehet, hogy a TMDB kulcs hibás.)',
        onImdb: 'Megnézem az IMDb-n',
        overview: 'Leírás'
    },
    en: {
        title: '🎲 Random Movie Picker',
        language: 'Language',
        genre: 'Genre',
        year: 'Year',
        rating: 'Minimum rating (0-10)',
        any: 'Any',
        getMovie: 'Get Movie',
        loading: 'Searching…',
        noKey: 'TMDB API key is not set.\nConfigure it in Dashboard → Plugins → Random Movie.',
        noResult: 'No movie matched. Try loosening the filters or check the TMDB key.',
        onImdb: 'View on IMDb',
        overview: 'Overview'
    }
};

    var GENRES = [
        { id: 28, hu: 'Akció', en: 'Action' },
        { id: 12, hu: 'Kaland', en: 'Adventure' },
        { id: 16, hu: 'Animáció', en: 'Animation' },
        { id: 35, hu: 'Vígjáték', en: 'Comedy' },
        { id: 80, hu: 'Bűnügyi', en: 'Crime' },
        { id: 18, hu: 'Dráma', en: 'Drama' },
        { id: 14, hu: 'Fantasy', en: 'Fantasy' },
        { id: 36, hu: 'Történelmi', en: 'History' },
        { id: 27, hu: 'Horror', en: 'Horror' },
        { id: 53, hu: 'Thriller', en: 'Thriller' },
        { id: 878, hu: 'Sci-fi', en: 'Sci-Fi' },
        { id: 10749, hu: 'Romantikus', en: 'Romance' },
        { id: 10751, hu: 'Családi', en: 'Family' },
        { id: 10402, hu: 'Zene', en: 'Music' },
        { id: 99, hu: 'Dokumentumfilm', en: 'Documentary' },
        { id: 37, hu: 'Western', en: 'Western' },
        { id: 10770, hu: 'TV-film', en: 'TV Movie' }
    ];

    var YEARS = [];
    var nowY = new Date().getFullYear();
    for (var y = nowY; y >= 1950; y--) YEARS.push(y);

    var RATINGS = [
        { v: '', l: '★ 0+' },
        { v: 5, l: '★ 5.0+' },
        { v: 6, l: '★ 6.0+' },
        { v: 6.5, l: '★ 6.5+' },
        { v: 7, l: '★ 7.0+' },
        { v: 7.5, l: '★ 7.5+' },
        { v: 8, l: '★ 8.0+' },
        { v: 8.5, l: '★ 8.5+' },
        { v: 9, l: '★ 9.0+' }
    ];

    var lang = 'hu';
    var overlay = null;

    function t(key) {
        var b = I18N[lang] || I18N.hu;
        return b[key] !== undefined ? b[key] : key;
    }

    function escapeHtml(s) {
        if (!s) return '';
        return String(s).replace(/[&<>"']/g, function (c) {
            return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c];
        });
    }

    function getToken() {
        return (window.ApiClient && ApiClient.accessToken) ? ApiClient.accessToken() : (localStorage.getItem('jellyfin_credentials') && (JSON.parse(localStorage.getItem('jellyfin_credentials')).token || '')) || '';
    }

    function apiBase() {
        if (window.ApiClient && ApiClient.getServerAddress) {
            return ApiClient.getServerAddress() + '/';
        }
        return window.location.origin + '/';
    }

    function fetchMovie(url) {
        var headers = { Accept: 'application/json' };
        var token = getToken();
        if (token) {
            headers.Authorization = 'MediaBrowser Token="' + token + '"';
        }
        return fetch(url, { headers: headers }).then(function (r) {
            if (!r.ok) throw new Error('HTTP ' + r.status);
            return r.json();
        });
    }

    function toast(msg) {
        if (window.Toast && Toast.show) { Toast.show(msg); }
        else { alert(msg); }
    }

    function injectButton() {
        if (!document.body) {
        return;
    }

    /*
     * IMPORTANT:
     * Never remove/recreate the button when it already exists.
     * MutationObserver can call this function very often.
     */
    if (document.getElementById('jfRandomMovieFab')) {
        return;
    }

    var btn = document.createElement('button');

    btn.id = 'jfRandomMovieFab';
    btn.type = 'button';
    btn.title = (I18N[lang] || I18N.hu).title;

    btn.style.cssText =
        'position:fixed;' +
        'bottom:24px;' +
        'right:24px;' +
        'z-index:200000;' +
        'width:58px;' +
        'height:58px;' +
        'border-radius:50%;' +
        'border:0;' +
        'font-size:24px;' +
        'cursor:pointer;' +
        'background:rgba(0,164,220,.9);' +
        'color:#fff;' +
        'box-shadow:0 4px 16px rgba(0,0,0,.4);';

    btn.textContent = '🎲';

    btn.addEventListener(
        'click',
        openDialog);

    document.body.appendChild(btn);
}

    function openDialog() {        close();
        overlay = document.createElement('div');
        overlay.id = 'jfRandomMovieOverlay';
        overlay.style.cssText = 'position:fixed;inset:0;z-index:300000;background:rgba(0,0,0,.6);' +
            'display:flex;align-items:center;justify-content:center;';
        overlay.addEventListener('click', function (e) { if (e.target === overlay) close(); });
        document.body.appendChild(overlay);
        renderDialog();
    }

    function renderDialog() {
        var b = I18N[lang] || I18N.hu;

        var gOpts = '<option value="">' + b.any + '</option>';
        GENRES.forEach(function (g) { gOpts += '<option value="' + g.id + '">' + (lang === 'en' ? g.en : g.hu) + '</option>'; });

        var yOpts = '<option value="">' + b.any + '</option>';
        YEARS.forEach(function (yy) { yOpts += '<option value="' + yy + '">' + yy + '</option>'; });

        var rOpts = '<option value="">' + b.any + '</option>';
        RATINGS.forEach(function (r) { rOpts += '<option value="' + r.v + '">' + r.l + '</option>'; });

        overlay.innerHTML =
            '<div style="width:min(92vw,520px);max-height:88vh;overflow:auto;background:#202020;color:#fff;' +
            'border-radius:14px;padding:22px;font-family:inherit;box-shadow:0 10px 50px rgba(0,0,0,.6);">' +

            '<div style="font-size:20px;font-weight:700;margin-bottom:16px;">' + t('title') + '</div>' +

            '<div style="font-size:12px;opacity:.8;margin-bottom:4px;">' + t('language') + '</div>' +
            '<select id="rmLang" style="width:100%;padding:8px;margin-bottom:12px;font-size:14px;color:#fff;background:#2d2d2d;">' +
            '<option value="hu">Magyar</option><option value="en">English</option></select>' +

            '<div style="font-size:12px;opacity:.8;margin-bottom:4px;">' + t('genre') + '</div>' +
            '<select id="rmGenre" style="width:100%;padding:8px;margin-bottom:12px;font-size:14px;color:#fff;background:#2d2d2d;">' + gOpts + '</select>' +

            '<div style="font-size:12px;opacity:.8;margin-bottom:4px;">' + t('year') + '</div>' +
            '<select id="rmYear" style="width:100%;padding:8px;margin-bottom:12px;font-size:14px;color:#fff;background:#2d2d2d;">' + yOpts + '</select>' +

            '<div style="font-size:12px;opacity:.8;margin-bottom:4px;">' + t('rating') + '</div>' +
            '<select id="rmRating" style="width:100%;padding:8px;margin-bottom:16px;font-size:14px;color:#fff;background:#2d2d2d;">' + rOpts + '</select>' +

            '<button id="rmGet" type="button" style="width:100%;padding:12px;border:0;border-radius:8px;' +
            'background:#00a4dc;color:#fff;font-size:15px;font-weight:700;cursor:pointer;">' + escapeHtml(b.getMovie) + '</button>' +

            '<div id="rmOut" style="display:none;margin-top:16px;"></div>' +
            '</div>';

        var langSel = overlay.querySelector('#rmLang');
        langSel.value = lang;
        langSel.addEventListener('change', function () {
            lang = langSel.value;
            renderDialog();
        });

        overlay.querySelector('#rmGet').addEventListener('click', onGet);
        overlay.querySelector('#rmOut').innerHTML = '';
    }

    function onGet() {
        var btn = overlay.querySelector('#rmGet');
        var out = overlay.querySelector('#rmOut');
        var genre = overlay.querySelector('#rmGenre').value;
        var year = overlay.querySelector('#rmYear').value;
        var rating = overlay.querySelector('#rmRating').value;

        btn.disabled = true;
        btn.textContent = (I18N[lang] || I18N.hu).loading;

        var q = [];
        if (genre) q.push('genreId=' + encodeURIComponent(genre));
        if (year) q.push('year=' + encodeURIComponent(year));
        if (rating) q.push('minRating=' + encodeURIComponent(rating));
        var url = apiBase() + 'RandomMovie/random' + (q.length ? '?' + q.join('&') : '');

        fetchMovie(url).then(function (m) {
            if (!m || !m.Title) throw new Error('empty');
            renderResult(out, m);
        }).catch(function () {
            renderMessage(out, t('noResult'));
        }).finally(function () {
            btn.disabled = false;
            btn.textContent = (I18N[lang] || I18N.hu).getMovie;
        });
    }

    function renderResult(out, m) {
        var html = '<div style="display:flex;gap:14px;">';
        if (m.PosterUrl) {
            html += '<img src="' + escapeHtml(m.PosterUrl) + '" style="width:120px;border-radius:8px;flex:0 0 auto;">';
        }
        html += '<div style="min-width:0;">';
        if (m.Title) html += '<div style="font-size:18px;font-weight:700;">' + escapeHtml(m.Title) + '</div>';
        if (m.Year) html += '<div style="opacity:.85;font-size:14px;">' + String(m.Year) + '</div>';
        if (m.Rating) html += '<div style="color:#f5c518;font-size:14px;">★ ' + m.Rating.toFixed(1) + '</div>';
        if (m.Overview) html += '<p style="font-size:13px;opacity:.85;margin:8px 0;">' + escapeHtml(m.Overview) + '</p>';
        if (m.ImdbId) {
            html += '<a href="https://www.imdb.com/title/' + encodeURIComponent(m.ImdbId) + '" target="_blank" rel="noopener" ' +
                'style="color:#00a4dc;font-size:13px;">' + escapeHtml(t('onImdb')) + ' ↗</a>';
        }
        html += '</div></div>';
        out.innerHTML = html;
        out.style.display = 'block';
    }

    function renderMessage(out, msg) {
        out.innerHTML = '<div style="white-space:pre-wrap;opacity:.9;">' + escapeHtml(msg) + '</div>';
        out.style.display = 'block';
    }

    function close() {
        if (overlay && overlay.parentNode) {
            overlay.parentNode.removeChild(overlay);
        }
        overlay = null;
    }

function loadLang() {
        var api = window.ApiClient;
        if (api && api.getPluginConfiguration) {
            api.getPluginConfiguration(PLUGIN_ID)
                .then(function (cfg) {
                    if (cfg && (cfg.UiLanguage === 'hu' || cfg.UiLanguage === 'en')) {
                        lang = cfg.UiLanguage;
                        var btn = document.getElementById('jfRandomMovieFab');
                        if (btn) btn.title = (I18N[lang] || I18N.hu).title;
                    }
                }).catch(function () { });
        }
    }

    function scheduleInject() {
        injectButton();
        if (window.MutationObserver) {
            var obs = new MutationObserver(function () {
                if (document.body) injectButton();
            });
            obs.observe(document.documentElement, { childList: true, subtree: true });
        }
        if (window.addEventListener) {
            window.addEventListener('viewchange', function () { injectButton(); });
        }
    }

    function init() {
        loadLang();
        if (document.body) {
            scheduleInject();
        } else {
            document.addEventListener('DOMContentLoaded', scheduleInject);
        }
    }

    if (document.body) {
        init();
    } else {
        document.addEventListener('DOMContentLoaded', init);
    }
})();
