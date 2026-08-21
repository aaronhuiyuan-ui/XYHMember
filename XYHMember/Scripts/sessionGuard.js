/**
 * 会话守卫（全局）
 * 由 SessionGuardHtmlFilter 自动注入到每个 HTML 页面。
 *
 * 当会话过期后 AuthFilter 对异步请求返回 401：
 *   - 包装 window.fetch：自动附带 X-Requested-With 头，收到 401 时整页跳转登录
 *   - jQuery 全局 ajaxError：$.ajax 收到 401 时整页跳转登录
 */
(function () {
    'use strict';
    if (window.__sessionGuardInstalled) return;
    window.__sessionGuardInstalled = true;

    function redirectToLogin() {
        var dest = window.__LOGIN_URL__ || '/Home/Login';
        try {
            if (window.top !== window.self) {
                // 在 iframe 内 → 整页（含左侧菜单）跳登录
                window.top.location.href = dest;
            } else {
                window.location.href = dest;
            }
        } catch (e) {
            window.location.href = dest;
        }
    }

    // ===== 包装 fetch =====
    var origFetch = window.fetch;
    if (origFetch) {
        window.fetch = function (input, init) {
            init = init || {};
            var h = init.headers || {};

            if (typeof Headers !== 'undefined' && h instanceof Headers) {
                if (!h.has('X-Requested-With')) {
                    h.set('X-Requested-With', 'XMLHttpRequest');
                }
            } else {
                if (h && !('X-Requested-With' in h)) {
                    h['X-Requested-With'] = 'XMLHttpRequest';
                }
                init.headers = h;
            }

            return origFetch.call(this, input, init).then(function (resp) {
                if (resp.status === 401) {
                    redirectToLogin();
                    return Promise.reject(new Error('登录已过期，请重新登录'));
                }
                return resp;
            });
        };
    }

    // ===== jQuery 全局 ajaxError =====
    if (window.jQuery && window.jQuery.ajax) {
        window.jQuery(document).ajaxError(function (event, xhr) {
            if (xhr && xhr.status === 401) {
                redirectToLogin();
            }
        });
    }
})();
