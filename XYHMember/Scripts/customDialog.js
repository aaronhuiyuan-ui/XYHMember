/**
 * CustomDialog - 炫酷弹窗，替换原生 alert/confirm
 * 使用方式：
 *   customDialog.alert('消息内容', '标题', function() { /* 关闭后回调 *\/ });
 *   customDialog.confirm('消息内容', '标题', function() { /* 确定 *\/ }, function() { /* 取消 *\/ });
 *   customDialog.success('消息内容', '标题', callback);
 *   customDialog.error('消息内容', '标题', callback);
 *   customDialog.info('消息内容', '标题', callback);
 *   customDialog.close();
 */
(function () {
    'use strict';

    // ===== 注入 CSS =====
    var css = [
        '@keyframes cdFadeIn {',
        '  from { opacity: 0; }',
        '  to { opacity: 1; }',
        '}',
        '@keyframes cdSlideIn {',
        '  from { margin-top: -20px; opacity: 0; }',
        '  to { margin-top: 0; opacity: 1; }',
        '}',
        '@keyframes cdPulse {',
        '  0%, 100% { transform: scale(1); }',
        '  50% { transform: scale(1.05); }',
        '}',
        '.custom-dialog-overlay {',
        '  position: fixed; top: 0; left: 0; right: 0; bottom: 0;',
        '  background: rgba(0,0,0,0.45);',
        '  backdrop-filter: blur(2px);',
        '  -webkit-backdrop-filter: blur(2px);',
        '  z-index: 999999;',
        '  text-align: center;',
        '  animation: cdFadeIn 0.2s ease;',
        '  margin: 0; padding: 0;',
        '}',
        '.custom-dialog-box {',
        '  position: fixed;',
        '  left: 600px; top: 300px;',
        '  transform: translate(-50%, -50%);',
        '  background: #fff;',
        '  border-radius: 10px;',
        '  box-shadow: 0 12px 48px rgba(0,0,0,0.25);',
        '  width: 420px;',
        '  max-width: 90vw;',
        '  min-width: 300px;',
        '  animation: cdSlideIn 0.3s cubic-bezier(0.34, 1.56, 0.64, 1);',
        '  overflow: hidden;',
        '}',
        '.custom-dialog-header {',
        '  display: flex; align-items: center;',
        '  padding: 20px 24px 0;',
        '}',
        '.custom-dialog-icon-wrap {',
        '  width: 40px; height: 40px;',
        '  border-radius: 50%;',
        '  display: flex; align-items: center; justify-content: center;',
        '  margin-right: 14px;',
        '  flex-shrink: 0;',
        '  font-size: 22px;',
        '  animation: cdPulse 0.6s ease 0.2s;',
        '}',
        '.custom-dialog-icon-info { background: #e8f0f8; color: #4d7496; }',
        '.custom-dialog-icon-success { background: #e8f5e8; color: #51a351; }',
        '.custom-dialog-icon-error { background: #fde8e8; color: #d9534f; }',
        '.custom-dialog-icon-warning { background: #fef5e7; color: #f0ad4e; }',
        '.custom-dialog-title {',
        '  font-size: 17px; font-weight: 600; color: #222;',
        '  line-height: 1.3;',
        '}',
        '.custom-dialog-body {',
        '  padding: 12px 24px 20px;',
        '  margin-left: 54px;',
        '  font-size: 14px;',
        '  color: #555;',
        '  line-height: 1.6;',
        '  word-break: break-word;',
        '  max-height: 55vh;',
        '  overflow-y: auto;',
        '}',
        '.custom-dialog-body pre {',
        '  margin: 6px 0; padding: 8px 12px;',
        '  background: #f5f5f5; border-radius: 4px;',
        '  font-size: 12px; line-height: 1.4;',
        '  max-height: 120px; overflow: auto;',
        '  white-space: pre-wrap; word-break: break-all;',
        '}',
        '.custom-dialog-footer {',
        '  display: flex; justify-content: flex-end; gap: 10px;',
        '  padding: 0 24px 20px;',
        '  margin-left: 54px;',
        '}',
        '.custom-dialog-btn {',
        '  padding: 7px 24px;',
        '  border-radius: 5px;',
        '  border: 1px solid #d9d9d9;',
        '  background: #fff;',
        '  color: #333;',
        '  font-size: 13px;',
        '  cursor: pointer;',
        '  transition: all 0.2s;',
        '  outline: none;',
        '  font-family: inherit;',
        '}',
        '.custom-dialog-btn:hover { opacity: 0.85; }',
        '.custom-dialog-btn:active { transform: scale(0.97); }',
        '.custom-dialog-btn-primary {',
        '  background: #4d7496; border-color: #4d7496; color: #fff;',
        '}',
        '.custom-dialog-btn-primary:hover { background: #3d6385; border-color: #3d6385; }',
        '.custom-dialog-btn-success {',
        '  background: #51a351; border-color: #51a351; color: #fff;',
        '}',
        '.custom-dialog-btn-success:hover { background: #429142; border-color: #429142; }',
        '.custom-dialog-btn-danger {',
        '  background: #d9534f; border-color: #d9534f; color: #fff;',
        '}',
        '.custom-dialog-btn-danger:hover { background: #c9433f; border-color: #c9433f; }',
        ''
    ].join('\n');

    var styleEl = document.createElement('style');
    styleEl.textContent = css;
    document.head.appendChild(styleEl);

    // ===== 图标映射 =====
    var ICONS = {
        info: 'i',
        success: '✓',
        error: '✕',
        warning: '!'
    };

    var ICON_CLASSES = {
        info: 'custom-dialog-icon-info',
        success: 'custom-dialog-icon-success',
        error: 'custom-dialog-icon-error',
        warning: 'custom-dialog-icon-warning'
    };

    var BTN_CLASSES = {
        info: 'custom-dialog-btn-primary',
        success: 'custom-dialog-btn-success',
        error: 'custom-dialog-btn-danger',
        warning: 'custom-dialog-btn-primary',
        confirm: 'custom-dialog-btn-primary'
    };

    // ===== 状态 =====
    var activeOverlay = null;

    function close() {
        var overlay = activeOverlay;
        if (!overlay) return;

        // 清理此弹窗注册的 ESC 处理函数
        if (overlay._escHandler) {
            document.removeEventListener('keydown', overlay._escHandler);
            overlay._escHandler = null;
        }

        activeOverlay = null;
        overlay.style.animation = 'cdFadeIn 0.15s ease reverse';
        var box = overlay.querySelector('.custom-dialog-box');
        if (box) {
            box.style.animation = 'cdSlideIn 0.15s ease reverse';
        }
        // 延迟移除DOM（让关闭动画播完），但立即标记 pointer-events: none 防止阻挡点击
        overlay.style.pointerEvents = 'none';
        setTimeout(function () {
            if (overlay && overlay.parentNode) {
                overlay.parentNode.removeChild(overlay);
            }
        }, 150);
    }

    function create(options) {
        // 关闭已有弹窗
        close();

        var type = options.type || 'info';
        var title = options.title || '提示';
        var msg = options.message || '';
        var confirmText = options.confirmText || '确定';
        var cancelText = options.cancelText || '取消';
        var showCancel = options.showCancel || false;
        var onConfirm = typeof options.onConfirm === 'function' ? options.onConfirm : null;
        var onCancel = typeof options.onCancel === 'function' ? options.onCancel : null;
        var onClose = typeof options.onClose === 'function' ? options.onClose : null;
        var btnClass = BTN_CLASSES[type] || 'custom-dialog-btn-primary';

        // 转换消息：错误描述 + API返回内容放在 <pre> 代码框内
        var msgHtml = '';
        if (msg) {
            var parts = msg.split('\n');
            var inPre = false;
            var preLines = [];
            for (var i = 0; i < parts.length; i++) {
                var line = parts[i];
                // 遇到"完整返回:"开头，开始收集pre内容
                if (line.indexOf('完整返回:') === 0 || line.indexOf('完整返回：') === 0) {
                    inPre = true;
                    preLines.push(line);
                } else if (inPre) {
                    // pre模式：收集后续行
                    preLines.push(line);
                } else {
                    // 普通文本行
                    if (line !== '' || i > 0) {
                        msgHtml += (msgHtml ? '<br>' : '') + escapeHtml(line);
                    }
                }
            }
            // 输出pre块
            if (preLines.length > 0) {
                if (msgHtml) msgHtml += '<br>';
                msgHtml += '<pre>' + escapeHtml(preLines.join('\n')) + '</pre>';
            }
        }

        var overlay = document.createElement('div');
        overlay.className = 'custom-dialog-overlay';
        overlay.innerHTML =
            '<div class="custom-dialog-box">' +
            '  <div class="custom-dialog-header">' +
            '    <div class="custom-dialog-icon-wrap ' + ICON_CLASSES[type] + '">' + ICONS[type] + '</div>' +
            '    <div class="custom-dialog-title">' + escapeHtml(title) + '</div>' +
            '  </div>' +
            '  <div class="custom-dialog-body">' + msgHtml + '</div>' +
            '  <div class="custom-dialog-footer">' +
            (showCancel ? '    <button class="custom-dialog-btn" data-cd-role="cancel">' + escapeHtml(cancelText) + '</button>' : '') +
            '    <button class="custom-dialog-btn ' + btnClass + '" data-cd-role="confirm">' + escapeHtml(confirmText) + '</button>' +
            '  </div>' +
            '</div>';

        document.body.appendChild(overlay);
        activeOverlay = overlay;

        // === 事件委托：用一个 overlay 级别的点击处理所有按钮 ===
        overlay.addEventListener('click', function (e) {
            var target = e.target;
            // 点击的是按钮？
            if (target && target.tagName === 'BUTTON') {
                var role = target.getAttribute('data-cd-role');
                if (role === 'confirm') {
                    e.stopPropagation();
                    close();
                    if (onConfirm) onConfirm();
                } else if (role === 'cancel') {
                    e.stopPropagation();
                    close();
                    if (onCancel) onCancel();
                }
                return;
            }
            // 点击背景遮罩 → 关闭（仅非 confirm 类型）
            if (e.target === overlay && !showCancel) {
                close();
                if (onClose) onClose();
            }
        });

        // ESC 关闭（将 handler 挂到 overlay 上方便 close() 清理）
        overlay._escHandler = function (e) {
            if (e.key === 'Escape') {
                if (showCancel) {
                    close();
                    if (onCancel) onCancel();
                } else {
                    close();
                    if (onClose) onClose();
                }
            }
        };
        document.addEventListener('keydown', overlay._escHandler);

        // 自动聚焦到确定按钮
        setTimeout(function () {
            var btn = overlay.querySelector('[data-cd-role="confirm"]');
            if (btn) btn.focus();
        }, 50);
    }

    function escapeHtml(str) {
        if (typeof str !== 'string') return str;
        return str.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
    }

    // ===== 公开 API =====
    window.customDialog = {
        alert: function (message, title, callback) {
            create({
                type: 'info',
                title: title || '提示',
                message: message,
                confirmText: '确定',
                onConfirm: callback,
                onClose: callback
            });
        },
        success: function (message, title, callback) {
            create({
                type: 'success',
                title: title || '成功',
                message: message,
                confirmText: '确定',
                onConfirm: callback,
                onClose: callback
            });
        },
        error: function (message, title, callback) {
            create({
                type: 'error',
                title: title || '错误',
                message: message,
                confirmText: '确定',
                onConfirm: callback,
                onClose: callback
            });
        },
        info: function (message, title, callback) {
            this.alert(message, title, callback);
        },
        warning: function (message, title, callback) {
            create({
                type: 'warning',
                title: title || '警告',
                message: message,
                confirmText: '确定',
                onConfirm: callback,
                onClose: callback
            });
        },
        confirm: function (message, title, onConfirm, onCancel) {
            create({
                type: 'info',
                title: title || '确认',
                message: message,
                confirmText: '确定',
                cancelText: '取消',
                showCancel: true,
                onConfirm: onConfirm,
                onCancel: onCancel
            });
        },
        close: close
    };
})();
