(function () {
  'use strict';

  var AGY = window.AGY = window.AGY || {};

  function showToast(msg, kind) {
    var stack = document.getElementById('toastStack');
    if (!stack) return;
    var node = document.createElement('div');
    node.className = 'toast' + (kind ? (' ' + kind) : '');
    node.textContent = msg;
    stack.appendChild(node);
    setTimeout(function () {
      if (node.parentNode) node.parentNode.removeChild(node);
    }, 3800);
  }

  async function withButtonBusy(btn, busyLabel, asyncFn) {
    if (typeof busyLabel === 'function') {
      asyncFn = busyLabel;
      busyLabel = '处理中...';
    }
    if (!btn) {
      return await asyncFn();
    }
    if (btn.dataset && btn.dataset.agyBusy === '1') {
      return undefined;
    }
    var origDisabled = !!btn.disabled;
    var origText = btn.textContent;
    if (btn.dataset) btn.dataset.agyBusy = '1';
    btn.disabled = true;
    btn.classList.add('btn-busy');
    if (busyLabel && btn.children.length === 0) {
      btn.textContent = busyLabel;
    }
    try {
      return await asyncFn();
    } finally {
      if (btn.dataset) delete btn.dataset.agyBusy;
      btn.classList.remove('btn-busy');
      btn.disabled = origDisabled;
      if (busyLabel && btn.children.length === 0) {
        btn.textContent = origText;
      }
    }
  }

  async function apiFetch(url, opts) {
    var options = opts || {};
    var headers = {};
    if (options.headers) {
      var keys = Object.keys(options.headers);
      for (var i = 0; i < keys.length; i++) headers[keys[i]] = options.headers[keys[i]];
    }
    if (AGY.state && AGY.state.token) {
      headers['X-AGY-Token'] = AGY.state.token;
    }
    if (options.body && !headers['Content-Type']) {
      headers['Content-Type'] = 'application/json; charset=utf-8';
    }
    options.headers = headers;
    options.credentials = 'same-origin';

    var res = await fetch(url, options);
    var text = await res.text();
    var json = null;
    try {
      json = text ? JSON.parse(text) : {};
    } catch (_e) {
      json = { ok: false, error: text || ('HTTP ' + res.status) };
    }

    if (!res.ok) {
      var errMsg = (json && (json.error || json.errorMessage)) ? (json.error || json.errorMessage) : ('HTTP ' + res.status);
      var err = new Error(errMsg);
      err.status = res.status;
      err.data = json;
      throw err;
    }
    return json;
  }

  async function copyToClipboard(text, okMsg) {
    try {
      await navigator.clipboard.writeText(text);
      showToast(okMsg || '已复制到剪贴板 ✓', 'success');
      return true;
    } catch (_e) {
      var ta = document.createElement('textarea');
      ta.value = text;
      ta.style.position = 'fixed';
      ta.style.left = '-9999px';
      document.body.appendChild(ta);
      ta.select();
      var ok = false;
      try { ok = document.execCommand('copy'); } catch (_e2) {}
      document.body.removeChild(ta);
      if (ok) showToast(okMsg || '已复制到剪贴板 ✓', 'success');
      else showToast('剪贴板写入受限，请手动全选复制', 'error');
      return ok;
    }
  }

  function connectSse(handlers) {
    var h = handlers || {};
    try {
      var es = new EventSource('/api/events');
      es.onopen = function () {
        if (h.onOpen) h.onOpen();
      };
      es.onerror = function () {
        if (h.onError) h.onError();
      };
      es.addEventListener('progress', function (ev) {
        try {
          var payload = JSON.parse(ev.data).payload;
          if (h.onProgress) h.onProgress(payload);
        } catch (_e) {}
      });
      es.addEventListener('log', function (ev) {
        try {
          var item = JSON.parse(ev.data).payload;
          if (h.onLog) h.onLog(item);
        } catch (_e) {}
      });
      es.addEventListener('ide_status', function (ev) {
        try {
          var payload = JSON.parse(ev.data).payload;
          if (h.onIdeStatus) h.onIdeStatus(payload);
        } catch (_e) {}
      });
      es.addEventListener('fs_changed', function (ev) {
        try {
          var payload = JSON.parse(ev.data).payload;
          if (h.onFsChanged) h.onFsChanged(payload);
        } catch (_e) {}
      });
      es.addEventListener('job_done', function (ev) {
        try {
          var done = JSON.parse(ev.data).payload || {};
          if (done.ok === false) {
            var errDetail = done.errorMessage || done.error || '后台任务执行失败';
            showToast('任务失败：' + errDetail, 'error');
          } else {
            showToast('任务完成 ✓', 'success');
          }
          if (done.warnings && done.warnings.length > 0) {
            var warnPreview = done.warnings.slice(0, 2).join('；');
            if (done.warnings.length > 2) warnPreview += '（共 ' + done.warnings.length + ' 条告警）';
            showToast('提示：' + warnPreview, 'warn');
          }
          if (h.onJobDone) h.onJobDone(done);
        } catch (_e) {}
      });
      return es;
    } catch (_e) {
      return null;
    }
  }

  function installGlobalErrorHandlers() {
    if (AGY._errorHandlersInstalled) return;
    AGY._errorHandlersInstalled = true;
    window.addEventListener('unhandledrejection', function (ev) {
      var reason = ev && ev.reason;
      var msg = reason && reason.message ? reason.message : String(reason || '未知异步错误');
      showToast('操作异常：' + msg, 'error');
    });
    window.addEventListener('error', function (ev) {
      if (ev && ev.message) {
        showToast('界面异常：' + ev.message, 'error');
      }
    });
  }

  AGY.api = {
    apiFetch: apiFetch,
    withButtonBusy: withButtonBusy,
    connectSse: connectSse,
    showToast: showToast,
    copyToClipboard: copyToClipboard,
    installGlobalErrorHandlers: installGlobalErrorHandlers
  };
})();
