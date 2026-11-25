(function () {
  'use strict';

  var AGY = window.AGY = window.AGY || {};

  var VIEW_META = {
    overview: ['工作台', '查看本机状态、常用项目入口与安全提醒'],
    sessions: ['对话管理', '浏览本机全部对话、全文搜索、导出与批量整理标题'],
    cockpit: ['扩展与配置', '管理 Antigravity 全局配置、插件/技能/规则/MCP/钩子与配置同步库'],
    storage: ['空间清理', '查看磁盘占用、清理残留垃圾文件与管理清理前安全备份'],
    backup: ['备份与同步', '一键在线备份本机环境、浏览自动备份历史与跨机恢复备份文件 (.agy)'],
    trash: ['回收站', '30 天内随时一键还原已删除的对话、项目、扩展配置及 .bak 历史版本'],
    settings: ['应用设置', '配置同步库位置、agy CLI、系统托盘、全局快捷键与自动备份计划']
  };

  function applyTheme(theme) {
    AGY.state.theme = theme === 'dark' ? 'dark' : 'light';
    document.documentElement.setAttribute('data-theme', AGY.state.theme);
    localStorage.setItem('agy_theme', AGY.state.theme);
    var lbl = AGY.dom.el('lblThemeToggle');
    if (lbl) lbl.textContent = AGY.state.theme === 'dark' ? '浅色模式' : '深色模式';
  }

  function injectStaticIcons() {
    var nodes = document.querySelectorAll('[data-icon]');
    for (var i = 0; i < nodes.length; i++) {
      var k = nodes[i].getAttribute('data-icon');
      if (AGY.ui.ICONS[k]) nodes[i].innerHTML = AGY.ui.ICONS[k];
    }
  }

  function hasUnsavedWork() {
    var st = AGY.state;
    if (st.drawerDirtyCheck) {
      try { if (st.drawerDirtyCheck()) return true; } catch (_e) {}
    }
    if (st.formDirty && (st.formDirty.cockpit || st.formDirty.settings)) {
      return true;
    }
    if (st.view === 'sessions' && st.sessionsSubView === 'rename' && st.renameEdits && Object.keys(st.renameEdits).length > 0) {
      return true;
    }
    return false;
  }

  function showStaleDataNotice(msg) {
    AGY.state.staleDataNotice = true;
    var b = AGY.dom.el('staleDataBanner');
    if (b) {
      if (msg) AGY.dom.setText('staleDataText', msg);
      b.classList.remove('hidden');
    }
  }

  function hideStaleDataNotice() {
    AGY.state.staleDataNotice = false;
    var b = AGY.dom.el('staleDataBanner');
    if (b) b.classList.add('hidden');
  }

  function setDisconnectedState(disconnected, desc) {
    var st = AGY.state;
    var wasDisconnected = !!st.disconnected;
    st.disconnected = !!disconnected;
    var overlay = AGY.dom.el('disconnectBanner');
    if (overlay) {
      overlay.classList.toggle('hidden', !st.disconnected);
      if (desc) AGY.dom.setText('disconnectDesc', desc);
    }
    if (wasDisconnected && !st.disconnected) {
      AGY.api.showToast('已恢复与 Antigravity 伴侣后台的连接 ✓', 'success');
      if (!hasUnsavedWork()) {
        refreshAll();
      } else {
        showStaleDataNotice('后台连接已恢复，数据已更新。点击「立即刷新视图」载入最新状态。');
      }
    }
  }

  function renderLogsDrawer(logs) {
    var esc = AGY.ui.escapeHtml;
    var html = [];
    for (var i = logs.length - 1; i >= 0; i--) {
      var l = logs[i];
      html.push('<div class="log-line ' + esc(l.level) + '">[' + esc(l.time) + '] ' + esc(l.message) + '</div>');
    }
    var body = AGY.dom.el('logDrawerBody');
    if (body) body.innerHTML = html.join('') || '<div class="muted">暂无活动记录</div>';
  }

  function updateShellBars() {
    var st = AGY.state;
    var el = AGY.dom.el;
    if (!st.overview) return;
    var ov = st.overview;
    var counts = ov.counts || {};

    var actionable = counts.untaggedActionableCount !== undefined ? counts.untaggedActionableCount : (counts.untaggedFormalSessions || 0);
    AGY.dom.setText('navCountUntagged', actionable);
    AGY.dom.setText('navSessionTotalLabel', '主对话总数: ' + (counts.formalPrimarySessions || 0));

    var cs = counts.capabilitySummary || {};
    var capDiffCount = (cs.remoteOnly || 0) + (cs.remoteNewer || 0) + (cs.localNewer || 0) + (cs.bothModified || 0) + (cs.conflict || 0);
    AGY.dom.setText('navCountCapDiffs', capDiffCount);

    var trashCnt = counts.trashCount || 0;
    AGY.dom.setText('navCountTrash', trashCnt);

    var ide = ov.ide || {};
    var dot = el('ideStatusDot');
    var txt = el('ideStatusText');
    var closeBtn = el('btnQuickCloseIde');
    if (dot && txt && closeBtn) {
      if (ide.running) {
        dot.className = 'status-dot busy';
        txt.textContent = 'Antigravity 主窗口运行中 (' + ((ide.parentCount || 0) + (ide.lsCount || 0)) + ')';
        closeBtn.classList.remove('hidden');
      } else {
        dot.className = 'status-dot idle';
        txt.textContent = '主窗口空闲 (可操作)';
        closeBtn.classList.add('hidden');
      }
    }

    var logs = ov.logs || [];
    if (logs.length > 0) {
      var latest = logs[logs.length - 1];
      AGY.dom.setText('activityLatest', '[' + latest.time + '] ' + latest.message);
      var aDot = el('activityDot');
      if (aDot) aDot.className = 'activity-dot' + (latest.level === 'warn' ? ' warn' : (latest.level === 'error' ? ' error' : ''));
      renderLogsDrawer(logs);
    }

    var job = ov.job || {};
    var banner = el('jobBanner');
    if (banner) {
      if (job.busy) {
        banner.classList.remove('hidden');
        AGY.dom.setText('jobBannerText', job.message || '后台任务处理中...');
        el('jobProgressFill').style.width = (job.percent || 20) + '%';
      } else {
        banner.classList.add('hidden');
      }
    }
  }

  function applyFreshRenameData(data) {
    var st = AGY.state;
    st.renameData = data;
    var cands = (data && data.candidates) || [];
    var newChecked = {};
    var foundFocused = false;
    for (var i = 0; i < cands.length; i++) {
      var c = cands[i];
      if (st.renameChecked[c.cid] !== undefined) newChecked[c.cid] = st.renameChecked[c.cid];
      else newChecked[c.cid] = !!c.selected;
      if (c.cid === st.renameFocusedCid) foundFocused = true;
    }
    st.renameChecked = newChecked;
    if (!foundFocused && cands.length > 0) st.renameFocusedCid = cands[0].cid;
  }

  function markNonActiveDirty(activeView) {
    var dirty = AGY.state.dirty || {};
    var keys = Object.keys(dirty);
    for (var i = 0; i < keys.length; i++) {
      dirty[keys[i]] = true;
    }
    if (activeView === 'sessions') {
      if (AGY.state.sessionsSubView === 'rename') dirty.rename = false;
      else dirty.sessions = false;
    } else if (activeView === 'cockpit') {
      if (AGY.state.cockpitSubView === 'vault') dirty.vault = false;
      else dirty.cockpit = false;
    } else if (activeView) {
      dirty[activeView] = false;
    }
    AGY.state.dirty = dirty;
  }

  function numSettingOrDefault(v, def) {
    var n = parseFloat(v);
    return (!isNaN(n) && isFinite(n) && n >= 0) ? n : def;
  }

  async function settledOrNull(promise) {
    try {
      return { ok: true, value: await promise };
    } catch (err) {
      return { ok: false, error: err };
    }
  }

  async function fetchViewDataOnly(view) {
    var api = AGY.api.apiFetch;
    var st = AGY.state;
    var results;

    if (view === 'overview') {
      results = await Promise.allSettled([
        api('/api/sessions'),
        api('/api/cockpit/summary')
      ]);
      if (results[0].status === 'fulfilled') st.sessionsData = results[0].value;
      if (results[1].status === 'fulfilled') st.cockpitData = results[1].value;
      st.dirty.overview = false;
    } else if (view === 'sessions') {
      if (st.sessionsSubView === 'rename') {
        var rRes = await settledOrNull(api('/api/rename/candidates'));
        if (rRes.ok) {
          applyFreshRenameData(rRes.value);
          st.dirty.rename = false;
        }
      } else {
        var sRes = await settledOrNull(api('/api/sessions'));
        if (sRes.ok) {
          st.sessionsData = sRes.value;
          st.dirty.sessions = false;
        }
      }
    } else if (view === 'cockpit') {
      if (st.cockpitSubView === 'vault') {
        results = await Promise.allSettled([
          api('/api/capabilities'),
          api('/api/vault/status'),
          api('/api/cockpit/summary')
        ]);
        if (results[0].status === 'fulfilled') st.capData = results[0].value;
        if (results[1].status === 'fulfilled') st.vaultStatus = results[1].value;
        if (results[2].status === 'fulfilled') st.cockpitData = results[2].value;
        st.dirty.vault = false;
      } else {
        results = await Promise.allSettled([
          api('/api/cockpit/summary'),
          api('/api/vault/status')
        ]);
        if (results[0].status === 'fulfilled') st.cockpitData = results[0].value;
        if (results[1].status === 'fulfilled') st.vaultStatus = results[1].value;
        st.dirty.cockpit = false;
      }
    } else if (view === 'storage') {
      var analyzePromise = (AGY.views && AGY.views.runStorageAnalyze)
        ? AGY.views.runStorageAnalyze(true)
        : api('/api/storage/analyze', {
            method: 'POST',
            body: JSON.stringify({
              topN: 30,
              minAgeDays: numSettingOrDefault(st.storageMinAgeDays, 7),
              minSizeMb: numSettingOrDefault(st.storageMinSizeMb, 5)
            })
          }).then(function (r) { st.storageData = r; st.dirty.storage = false; return r; });
      results = await Promise.allSettled([
        analyzePromise,
        api('/api/storage/cold-archives')
      ]);
      if (results[1].status === 'fulfilled') st.coldArchivesData = results[1].value;
      st.dirty.storage = false;
    } else if (view === 'backup') {
      results = await Promise.allSettled([
        api('/api/backup/current'),
        api('/api/snapshots/status')
      ]);
      if (results[0].status === 'fulfilled') st.backupData = results[0].value;
      if (results[1].status === 'fulfilled') st.snapshotStatus = results[1].value;
      st.dirty.backup = false;
    } else if (view === 'trash') {
      results = await Promise.allSettled([
        api('/api/trash/list'),
        api('/api/config-backups/list')
      ]);
      if (results[0].status === 'fulfilled') st.trashData = results[0].value;
      if (results[1].status === 'fulfilled') st.configBackupsData = results[1].value;
      st.dirty.trash = false;
    } else if (view === 'settings') {
      results = await Promise.allSettled([
        api('/api/settings/companion'),
        api('/api/snapshots/status'),
        api('/api/vault/status')
      ]);
      if (results[0].status === 'fulfilled') st.companionSettings = results[0].value;
      if (results[1].status === 'fulfilled') st.snapshotStatus = results[1].value;
      if (results[2].status === 'fulfilled') st.vaultStatus = results[2].value;
      st.dirty.settings = false;
    }
  }

  async function refreshCoreAndActiveView() {
    var api = AGY.api.apiFetch;
    var st = AGY.state;
    var active = st.view || 'overview';
    var settled = await Promise.allSettled([
      api('/api/overview'),
      api('/api/settings/companion'),
      fetchViewDataOnly(active)
    ]);
    if (settled[0].status === 'fulfilled') {
      st.overview = settled[0].value;
    }
    if (settled[1].status === 'fulfilled') {
      st.companionSettings = settled[1].value;
    }
    updateShellBars();
    if (settled[0].status === 'rejected' && !st.overview) {
      throw settled[0].reason;
    }
  }

  async function refreshAllSilent() {
    await refreshCoreAndActiveView();
  }

  async function refreshAll(showToastMsg) {
    hideStaleDataNotice();
    markNonActiveDirty(AGY.state.view || 'overview');
    await refreshCoreAndActiveView();
    renderCurrentView();
    if (showToastMsg) AGY.api.showToast(showToastMsg, 'success');
  }

  function updateSidebarActiveState(view) {
    var navBtns = document.querySelectorAll('#navList .nav-item');
    for (var i = 0; i < navBtns.length; i++) {
      var b = navBtns[i];
      b.classList.toggle('active', b.getAttribute('data-view') === view);
    }
    var setBtn = AGY.dom.el('btnOpenSettings');
    if (setBtn) setBtn.classList.toggle('active', view === 'settings');
    var trashBtn = AGY.dom.el('btnOpenTrash');
    if (trashBtn) trashBtn.classList.toggle('active', view === 'trash');
  }

  function switchView(view) {
    var st = AGY.state;
    if (view === 'rename') {
      st.sessionsSubView = 'rename';
      view = 'sessions';
    } else if (view === 'vault') {
      st.cockpitSubView = 'vault';
      view = 'cockpit';
    }
    st.view = view;
    updateSidebarActiveState(view);

    var needsLazy = false;
    if (view === 'sessions') {
      needsLazy = st.sessionsSubView === 'rename'
        ? !!(st.dirty.rename || !st.renameData)
        : !!(st.dirty.sessions || !st.sessionsData);
    } else if (view === 'cockpit') {
      needsLazy = st.cockpitSubView === 'vault'
        ? !!(st.dirty.vault || !st.capData)
        : !!(st.dirty.cockpit || !st.cockpitData);
    } else if (view === 'storage') {
      needsLazy = !!(st.dirty.storage || !st.storageData);
    } else if (view === 'trash') {
      needsLazy = !!(st.dirty.trash || !st.trashData);
    } else {
      needsLazy = !!(st.dirty[view]);
    }

    if (needsLazy) {
      if (view === 'storage') st._lazyFetchingStorage = true;
      renderCurrentView();
      fetchViewDataOnly(view).then(function () {
        st._lazyFetchingStorage = false;
        if (AGY.state.view === view) renderCurrentView();
      }).catch(function (err) {
        st._lazyFetchingStorage = false;
        AGY.api.showToast('加载视图数据失败：' + (err && err.message ? err.message : String(err)), 'error');
      });
    } else {
      renderCurrentView();
    }
  }

  function renderCurrentView() {
    var st = AGY.state;
    var meta = VIEW_META[st.view] || ['工作台', ''];
    AGY.dom.setText('pageTitle', meta[0]);
    AGY.dom.setText('pageSubtitle', meta[1]);
    var topActions = AGY.dom.el('topbarActions');
    if (topActions) topActions.innerHTML = '';

    var viewFn = AGY.views && AGY.views[st.view];
    if (typeof viewFn === 'function') {
      viewFn();
    }
  }

  async function doQuickCloseIde(forceKill) {
    try {
      await AGY.api.apiFetch('/api/ide/close', {
        method: 'POST',
        body: JSON.stringify({ forceKill: !!forceKill })
      });
      await refreshAll('Antigravity 主窗口已关闭 ✓');
    } catch (e) {
      if (!AGY.ui.handleIdeConflictError(e, '关闭 Antigravity 主窗口', null, function () { doQuickCloseIde(true); })) {
        AGY.api.showToast(e.message, 'error');
      }
    }
  }

  async function boot() {
    var el = AGY.dom.el;
    var st = AGY.state;

    AGY.api.installGlobalErrorHandlers();
    applyTheme(st.theme);
    injectStaticIcons();

    var params = new URLSearchParams(window.location.search);
    var urlToken = params.get('t');
    if (urlToken) {
      st.token = urlToken;
      sessionStorage.setItem('agy_tok', urlToken);
      window.history.replaceState(null, '', window.location.pathname);
    } else {
      st.token = sessionStorage.getItem('agy_tok') || '';
    }

    if (!st.token) {
      try {
        var auth = await fetch('/api/auth/session', { credentials: 'same-origin' });
        if (auth.ok) {
          var authJson = await auth.json();
          if (authJson && authJson.token) {
            st.token = authJson.token;
            sessionStorage.setItem('agy_tok', st.token);
          }
        }
      } catch (_e) {}
    }

    var navBtns = document.querySelectorAll('#navList .nav-item');
    for (var i = 0; i < navBtns.length; i++) {
      navBtns[i].addEventListener('click', function () {
        switchView(this.getAttribute('data-view'));
      });
    }

    var trashBtn = el('btnOpenTrash');
    if (trashBtn) {
      trashBtn.addEventListener('click', function () { switchView('trash'); });
    }
    var setBtn = el('btnOpenSettings');
    if (setBtn) {
      setBtn.addEventListener('click', function () { switchView('settings'); });
    }

    el('btnToggleTheme').addEventListener('click', function () {
      applyTheme(st.theme === 'dark' ? 'light' : 'dark');
    });

    el('btnGlobalRefresh').addEventListener('click', function () {
      var btn = this;
      AGY.api.withButtonBusy(btn, '刷新中...', async function () {
        try {
          await AGY.api.apiFetch('/api/refresh', { method: 'POST', body: '{}' });
          await refreshAll('已刷新本机与配置同步库数据 ✓');
        } catch (e) {
          AGY.api.showToast('刷新失败：' + e.message, 'error');
        }
      });
    });

    el('btnQuickCloseIde').addEventListener('click', function () {
      var ide = (st.overview && st.overview.ide) || {};
      AGY.ui.confirmCloseIdeModal(ide.parentCount || 1, ide.lsCount || 0, '关闭 Antigravity 主窗口', function () {
        doQuickCloseIde(false);
      });
    });

    if (el('btnApplyStaleRefresh')) {
      el('btnApplyStaleRefresh').addEventListener('click', function () {
        st.formDirty.cockpit = false;
        st.formDirty.settings = false;
        refreshAll('已更新至最新状态 ✓');
      });
    }
    if (el('btnDismissStaleNotice')) {
      el('btnDismissStaleNotice').addEventListener('click', hideStaleDataNotice);
    }
    if (el('btnRetryReconnect')) {
      el('btnRetryReconnect').addEventListener('click', function () {
        var btn = this;
        AGY.api.withButtonBusy(btn, '连接中...', async function () {
          try {
            await AGY.api.apiFetch('/api/app/heartbeat', { method: 'POST', body: '{}' });
            st.heartbeatFailures = 0;
            setDisconnectedState(false);
          } catch (_e) {
            AGY.api.showToast('暂未连接到后台服务，请确认 Antigravity 伴侣已启动', 'error');
          }
        });
      });
    }

    el('btnToggleLogs').addEventListener('click', function () {
      st.logsOpen = !st.logsOpen;
      el('logDrawer').classList.toggle('hidden', !st.logsOpen);
      el('activityChevron').textContent = st.logsOpen ? '收起日志 ▾' : '展开日志 ▴';
    });
    el('btnCloseLogs').addEventListener('click', function () {
      st.logsOpen = false;
      el('logDrawer').classList.add('hidden');
      el('activityChevron').textContent = '展开日志 ▴';
    });
    el('btnCopyLogs').addEventListener('click', function () {
      var logs = (st.overview && st.overview.logs) || [];
      AGY.api.copyToClipboard(logs.map(function (l) { return '[' + l.time + '] ' + l.message; }).join('\n'), '日志已复制 ✓');
    });

    el('btnCloseDrawer').addEventListener('click', function () { AGY.ui.closeDrawer(false); });
    el('drawerBackdrop').addEventListener('click', function (ev) { if (ev.target === this) AGY.ui.closeDrawer(false); });
    el('btnCloseModal').addEventListener('click', AGY.ui.closeModal);
    el('modalBackdrop').addEventListener('click', function (ev) { if (ev.target === this) AGY.ui.closeModal(); });

    document.addEventListener('keydown', function (ev) {
      if (ev.key === 'Escape') {
        if (!el('modalBackdrop').classList.contains('hidden')) AGY.ui.closeModal();
        else if (!el('drawerBackdrop').classList.contains('hidden')) AGY.ui.closeDrawer(false);
        else if (st.logsOpen) el('btnCloseLogs').click();
      }
    });

    AGY.api.connectSse({
      onOpen: function () {
        st.heartbeatFailures = 0;
        if (st.disconnected) setDisconnectedState(false);
      },
      onProgress: function (payload) {
        if (!st.overview) st.overview = { logs: [] };
        st.overview.job = payload || { busy: false };
        updateShellBars();
      },
      onLog: function (item) {
        if (!st.overview) st.overview = { logs: [] };
        if (!st.overview.logs) st.overview.logs = [];
        st.overview.logs.push(item);
        updateShellBars();
      },
      onIdeStatus: function (payload) {
        if (!st.overview) st.overview = { logs: [] };
        var wasRunning = !!(st.overview.ide && st.overview.ide.running);
        st.overview.ide = payload || {};
        updateShellBars();
        if (wasRunning && !payload.running) {
          AGY.api.apiFetch('/api/overview').then(function (ov) {
            st.overview = ov;
            updateShellBars();
            if (!hasUnsavedWork() && st.view === 'overview') renderCurrentView();
          }).catch(function () {});
        }
      },
      onFsChanged: function () {
        if (hasUnsavedWork()) {
          markNonActiveDirty(null);
          showStaleDataNotice('检测到本机或配置同步库文件发生变化。为保护您的未保存修改，已暂缓刷新。');
          AGY.api.apiFetch('/api/overview').then(function (ov) {
            st.overview = ov;
            updateShellBars();
          }).catch(function () {});
        } else {
          refreshAll();
        }
      },
      onJobDone: async function () {
        if (st.overview) st.overview.job = { busy: false, percent: 100 };
        updateShellBars();
        if (hasUnsavedWork()) {
          markNonActiveDirty(null);
          showStaleDataNotice('后台任务已完成。您当前有未保存的编辑内容，保存后点击「立即刷新视图」即可看到最新结果。');
          AGY.api.apiFetch('/api/overview').then(function (ov) {
            st.overview = ov;
            updateShellBars();
          }).catch(function () {});
        } else {
          await refreshAll();
        }
      }
    });

    setInterval(function () {
      if (!st.token) return;
      AGY.api.apiFetch('/api/app/heartbeat', { method: 'POST', body: '{}' })
        .then(function () {
          st.heartbeatFailures = 0;
          if (st.disconnected) setDisconnectedState(false);
        })
        .catch(function () {
          st.heartbeatFailures = (st.heartbeatFailures || 0) + 1;
          if (st.heartbeatFailures >= 2 && !st.disconnected) {
            setDisconnectedState(true);
          }
        });
    }, 2500);

    await refreshCoreAndActiveView();
    renderCurrentView();
  }

  AGY.app = {
    switchView: switchView,
    refreshAll: refreshAll,
    refreshAllSilent: refreshAllSilent,
    refreshCoreAndActiveView: refreshCoreAndActiveView,
    fetchViewDataOnly: fetchViewDataOnly,
    markNonActiveDirty: markNonActiveDirty,
    applyFreshRenameData: applyFreshRenameData,
    updateShellBars: updateShellBars,
    renderCurrentView: renderCurrentView,
    hasUnsavedWork: hasUnsavedWork
  };

  boot().catch(function (err) {
    AGY.dom.el('mainCanvas').innerHTML =
      '<div class="card empty-state"><strong>初始化失败</strong><div class="muted" style="margin-top:8px">' +
      AGY.ui.escapeHtml(err && err.message ? err.message : String(err)) + '</div></div>';
  });
})();
