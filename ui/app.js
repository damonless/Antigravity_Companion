(function () {
  'use strict';

  var AGY = window.AGY = window.AGY || {};

  var VIEW_META = {
    overview: ['概览', '查看本机状态、工程启动台、同步差异与存储健康度'],
    cockpit: ['每日驾驶舱', '管理 Antigravity 全局配置、Plugin/MCP/Hook 开关与 MCP 健康体检'],
    sessions: ['会话管理', '统一浏览 IDE/CLI/历史归档对话、全文检索、Markdown 导出与工程维护'],
    rename: ['智能重命名', '三步式会话命名向导、本地多仓规则、Zero-Key agy CLI 与离线 AI 润色'],
    vault: ['能力与规则', '在线编辑 Rule/Skill/.agents/、参考字节/Token 统计与 Portable Vault 双向同步'],
    backup: ['存储与备份', '分级 .agy 归档往返迁移、空间诊断、孤儿清理与高级微创瘦身'],
    settings: ['系统设置', 'Portable Vault、agy CLI、系统托盘、全局快捷键、开机自启与定时静默快照']
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
    AGY.dom.setText('navSessionTotalLabel', '正式主会话: ' + (counts.formalPrimarySessions || 0));

    var cs = counts.capabilitySummary || {};
    var capDiffCount = (cs.remoteOnly || 0) + (cs.remoteNewer || 0) + (cs.localNewer || 0) + (cs.bothModified || 0) + (cs.conflict || 0);
    AGY.dom.setText('navCountCapDiffs', capDiffCount);

    var ide = ov.ide || {};
    var dot = el('ideStatusDot');
    var txt = el('ideStatusText');
    var closeBtn = el('btnQuickCloseIde');
    if (dot && txt && closeBtn) {
      if (ide.running) {
        dot.className = 'status-dot busy';
        txt.textContent = 'IDE 运行中 (' + ((ide.parentCount || 0) + (ide.lsCount || 0)) + ' 进程)';
        closeBtn.classList.remove('hidden');
      } else {
        dot.className = 'status-dot idle';
        txt.textContent = 'IDE 空闲 · 可安全全写';
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
        AGY.dom.setText('jobBannerText', job.message || '后台任务执行中...');
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
    if (activeView === 'backup') {
      if (AGY.state.backupSubView === 'storage') dirty.storage = false;
      else dirty.backup = false;
    } else if (activeView) {
      dirty[activeView] = false;
    }
    AGY.state.dirty = dirty;
  }

  function numSettingOrDefault(v, def) {
    var n = parseFloat(v);
    return (!isNaN(n) && isFinite(n) && n >= 0) ? n : def;
  }

  async function fetchViewDataOnly(view) {
    var api = AGY.api.apiFetch;
    var st = AGY.state;
    if (view === 'overview') {
      var ovPair = await Promise.all([
        api('/api/sessions'),
        api('/api/cockpit/summary')
      ]);
      st.sessionsData = ovPair[0];
      st.cockpitData = ovPair[1];
      st.dirty.overview = false;
    } else if (view === 'cockpit') {
      st.cockpitData = await api('/api/cockpit/summary');
      st.dirty.cockpit = false;
    } else if (view === 'sessions') {
      st.sessionsData = await api('/api/sessions');
      st.dirty.sessions = false;
    } else if (view === 'rename') {
      applyFreshRenameData(await api('/api/rename/candidates'));
      st.dirty.rename = false;
    } else if (view === 'vault') {
      var vRes = await Promise.all([
        api('/api/capabilities'),
        api('/api/vault/status'),
        api('/api/cockpit/summary')
      ]);
      st.capData = vRes[0];
      st.vaultStatus = vRes[1];
      st.cockpitData = vRes[2];
      st.dirty.vault = false;
    } else if (view === 'backup') {
      if (st.backupSubView === 'storage') {
        if (AGY.views && AGY.views.runStorageAnalyze) {
          await AGY.views.runStorageAnalyze(true);
        } else {
          st.storageData = await api('/api/storage/analyze', {
            method: 'POST',
            body: JSON.stringify({
              topN: 30,
              minAgeDays: numSettingOrDefault(st.storageMinAgeDays, 7),
              minSizeMb: numSettingOrDefault(st.storageMinSizeMb, 5)
            })
          });
          st.dirty.storage = false;
        }
      } else {
        st.backupData = await api('/api/backup/current');
        st.dirty.backup = false;
      }
    } else if (view === 'storage') {
      if (AGY.views && AGY.views.runStorageAnalyze) {
        await AGY.views.runStorageAnalyze(true);
      } else {
        st.storageData = await api('/api/storage/analyze', {
          method: 'POST',
          body: JSON.stringify({
            topN: 30,
            minAgeDays: numSettingOrDefault(st.storageMinAgeDays, 7),
            minSizeMb: numSettingOrDefault(st.storageMinSizeMb, 5)
          })
        });
        st.dirty.storage = false;
      }
    } else if (view === 'settings') {
      var sRes = await Promise.all([
        api('/api/settings/companion'),
        api('/api/snapshots/status'),
        api('/api/vault/status')
      ]);
      st.companionSettings = sRes[0];
      st.snapshotStatus = sRes[1];
      st.vaultStatus = sRes[2];
      st.dirty.settings = false;
    }
  }

  async function refreshCoreAndActiveView() {
    var api = AGY.api.apiFetch;
    var st = AGY.state;
    var active = st.view || 'overview';
    var coreRes = await Promise.all([
      api('/api/overview'),
      api('/api/settings/companion'),
      fetchViewDataOnly(active)
    ]);
    st.overview = coreRes[0];
    st.companionSettings = coreRes[1];
    updateShellBars();
  }

  async function refreshAllSilent() {
    await refreshCoreAndActiveView();
  }

  async function refreshAll(showToastMsg) {
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
  }

  function switchView(view) {
    var st = AGY.state;
    if (view === 'storage') {
      st.backupSubView = 'storage';
      view = 'backup';
    }
    st.view = view;
    updateSidebarActiveState(view);

    var needsLazy = false;
    if (view === 'backup' && st.backupSubView === 'storage') {
      needsLazy = !!(st.dirty.storage || !st.storageData);
    } else {
      needsLazy = !!(st.dirty[view]);
    }

    if (needsLazy) {
      if (view === 'backup' && st.backupSubView === 'storage') {
        st._lazyFetchingStorage = true;
      }
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

    var setBtn = el('btnOpenSettings');
    if (setBtn) {
      setBtn.addEventListener('click', function () {
        switchView('settings');
      });
    }

    el('btnToggleTheme').addEventListener('click', function () {
      applyTheme(st.theme === 'dark' ? 'light' : 'dark');
    });

    el('btnGlobalRefresh').addEventListener('click', async function () {
      await AGY.api.apiFetch('/api/refresh', { method: 'POST', body: '{}' });
      await refreshAll('已刷新本机与 Vault 数据 ✓');
    });

    el('btnQuickCloseIde').addEventListener('click', function () {
      var ide = (st.overview && st.overview.ide) || {};
      AGY.ui.confirmCloseIdeModal(ide.parentCount || 1, ide.lsCount || 0, '安全关闭 Antigravity IDE', async function () {
        try {
          await AGY.api.apiFetch('/api/ide/close', { method: 'POST', body: '{}' });
          await refreshAll('Antigravity IDE 已关闭 ✓');
        } catch (e) { AGY.api.showToast(e.message, 'error'); }
      });
    });

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
      onProgress: function (payload) {
        el('jobBanner').classList.remove('hidden');
        AGY.dom.setText('jobBannerText', (payload && payload.message) || '执行中...');
        el('jobProgressFill').style.width = ((payload && payload.percent) || 25) + '%';
      },
      onLog: function (item) {
        if (!st.overview) st.overview = { logs: [] };
        if (!st.overview.logs) st.overview.logs = [];
        st.overview.logs.push(item);
        updateShellBars();
      },
      onJobDone: async function () {
        el('jobBanner').classList.add('hidden');
        await refreshAll();
      }
    });

    setInterval(function () {
      if (st.token) AGY.api.apiFetch('/api/app/heartbeat', { method: 'POST', body: '{}' }).catch(function () {});
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
    renderCurrentView: renderCurrentView
  };

  boot().catch(function (err) {
    AGY.dom.el('mainCanvas').innerHTML =
      '<div class="card empty-state"><strong>初始化失败</strong><div class="muted" style="margin-top:8px">' +
      AGY.ui.escapeHtml(err && err.message ? err.message : String(err)) + '</div></div>';
  });
})();
