document.addEventListener('DOMContentLoaded', function () {
    var select = document.getElementById('ups-site-select');
    if (!select) {
        return;
    }

    select.addEventListener('change', function () {
        var baseUrl = select.dataset.baseUrl || window.location.pathname;
        var value = select.value || 'all';

        if (value === 'all') {
            window.location.href = baseUrl;
            return;
        }

        var targetUrl = new URL(baseUrl, window.location.origin);
        targetUrl.searchParams.set('site_id', value);
        window.location.href = targetUrl.toString();
    });
});
