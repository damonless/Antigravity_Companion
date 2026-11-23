(function () {
  'use strict';

  var ICONS = {
    home: '<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.9" stroke-linecap="round" stroke-linejoin="round"><path d="M3 9l9-7 9 7v11a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z"/><polyline points="9 22 9 12 15 12 15 22"/></svg>',
    message: '<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.9" stroke-linecap="round" stroke-linejoin="round"><path d="M21 15a2 2 0 0 1-2 2H7l-4 4V5a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2z"/></svg>',
    sparkles: '<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.9" stroke-linecap="round" stroke-linejoin="round"><path d="M12 3l1.9 5.8L20 11l-6.1 2.2L12 19l-1.9-5.8L4 11l6.1-2.2z"/></svg>',
    layers: '<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.9" stroke-linecap="round" stroke-linejoin="round"><polygon points="12 2 2 7 12 12 22 7 12 2"/><polyline points="2 17 12 22 22 17"/><polyline points="2 12 12 17 22 12"/></svg>',
    archive: '<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.9" stroke-linecap="round" stroke-linejoin="round"><polyline points="21 8 21 21 3 21 3 8"/><rect x="1" y="3" width="22" height="5"/><line x1="10" y1="12" x2="14" y2="12"/></svg>',
    moon: '<svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.9" stroke-linecap="round" stroke-linejoin="round"><path d="M21 12.79A9 9 0 1 1 11.21 3 7 7 0 0 0 21 12.79z"/></svg>',
    refresh: '<svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.9" stroke-linecap="round" stroke-linejoin="round"><polyline points="23 4 23 10 17 10"/><path d="M20.49 15a9 9 0 1 1-2.12-9.36L23 10"/></svg>',
    search: '<svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><circle cx="11" cy="11" r="8"/><line x1="21" y1="21" x2="16.65" y2="16.65"/></svg>',
    eye: '<svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z"/><circle cx="12" cy="12" r="3"/></svg>',
    edit: '<svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M12 20h9"/><path d="M16.5 3.5a2.121 2.121 0 0 1 3 3L7 19l-4 1 1-4L16.5 3.5z"/></svg>',
    folder: '<svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M22 19a2 2 0 0 1-2 2H4a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h5l2 3h9a2 2 0 0 1 2 2z"/></svg>',
    download: '<svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"/><polyline points="7 10 12 15 17 10"/><line x1="12" y1="15" x2="12" y2="3"/></svg>'
  };

  var state = {
    token: '',
    view: 'overview',
    theme: localStorage.getItem('agy_theme') || 'light',
    overview: null,
    sessionsData: null,
    selectedBucketKey: '__ALL__',
    auxExpanded: false,
    sessionSearchQuery: '',
    sessionSearchContent: false,
    sessionSearchMatchedIds: null,
    checkedSessions: {},
    renameData: null,
    renameStep: 1,
    renameProjectFilter: '__ALL__',
    renameShowTagged: false,
    renameShowAux: false,
    renameShowRunning: false,
    renameShowLowValue: false,
    renameChecked: {},
    renameEdits: {},
    renameAiModified: {},
    renameFocusedCid: '',
    renamePromptExpanded: false,
    renameTechExpanded: false,
    renameStrategy: 'CloseAndFullWrite',
    capData: null,
    capCategory: 'ALL',
    capDirFilter: 'ALL',
    capSearch: '',
    capChecked: {},
    backupData: null,
    backupTab: 'caps',
    backupCheckedCaps: {},
    backupCheckedConvs: {},
    logsOpen: false,
    drawerDirtyCheck: null
  };

  function el(id) { return document.getElementById(id); }

  function escapeHtml(str) {
    if (str === null || str === undefined) return '';
    return String(str)
      .replace(/&/g, '&amp;')
      .replace(/</g, '&lt;')
      .replace(/>/g, '&gt;')
      .replace(/"/g, '&quot;')
      .replace(/'/g, '&#39;');
  }

  function sanitizeUrl(url) {
    var s = String(url || '').trim();
    if (/^(https?:\/\/|#|\/)/i.test(s)) return s;
    return '#';
  }

  function renderMarkdownSafe(mdText, highlightQuery) {
    var src = String(mdText || '').replace(/\r\n/g, '\n').replace(/\r/g, '\n');
    var lines = src.split('\n');
    var html = [];
    var inPre = false;
    var preLines = [];
    var preLang = '';
    var inList = false;

    function flushList() {
      if (inList) { html.push('</ul>'); inList = false; }
    }

    function applyInline(raw) {
      var escaped = escapeHtml(raw);
      escaped = escaped.replace(/`([^`]+)`/g, '<code>$1</code>');
      escaped = escaped.replace(/\*\*([^*]+)\*\*/g, '<strong>$1</strong>');
      escaped = escaped.replace(/\[([^\]]+)\]\(([^)]+)\)/g, function (_m, label, url) {
        return '<a href="' + escapeHtml(sanitizeUrl(url)) + '" target="_blank" rel="noopener">' + label + '</a>';
      });
      if (highlightQuery && highlightQuery.trim().length > 0) {
        var qEsc = escapeHtml(highlightQuery.trim()).replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
        try {
          escaped = escaped.replace(new RegExp('(' + qEsc + ')', 'gi'), '<mark class="md-highlight">$1</mark>');
        } catch (_e) {}
      }
      return escaped;
    }

    for (var i = 0; i < lines.length; i++) {
      var line = lines[i];
      if (/^```/.test(line.trim())) {
        if (!inPre) {
          flushList();
          inPre = true;
          preLang = line.trim().slice(3).trim();
          preLines = [];
        } else {
          html.push('<pre>' + (preLang ? '<div class="muted" style="font-size:11px;margin-bottom:4px">' + escapeHtml(preLang) + '</div>' : '') + '<code>' + escapeHtml(preLines.join('\n')) + '</code></pre>');
          inPre = false;
          preLang = '';
        }
        continue;
      }
      if (inPre) {
        preLines.push(line);
        continue;
      }
      if (/^---+$/.test(line.trim())) {
        flushList();
        html.push('<hr style="border:none;border-top:1px solid var(--border);margin:12px 0">');
      } else if (/^###\s+/.test(line)) {
        flushList();
        html.push('<h3>' + applyInline(line.replace(/^###\s+/, '')) + '</h3>');
      } else if (/^##\s+/.test(line)) {
        flushList();
        html.push('<h2>' + applyInline(line.replace(/^##\s+/, '')) + '</h2>');
      } else if (/^#\s+/.test(line)) {
        flushList();
        html.push('<h1>' + applyInline(line.replace(/^#\s+/, '')) + '</h1>');
      } else if (/^>\s?/.test(line)) {
        flushList();
        html.push('<blockquote>' + applyInline(line.replace(/^>\s?/, '')) + '</blockquote>');
      } else if (/^[-*+]\s+/.test(line)) {
        if (!inList) { html.push('<ul>'); inList = true; }
        html.push('<li>' + applyInline(line.replace(/^[-*+]\s+/, '')) + '</li>');
      } else if (!line.trim()) {
        flushList();
        html.push('<div style="height:6px"></div>');
      } else {
        flushList();
        html.push('<div>' + applyInline(line) + '</div>');
      }
    }
    if (inPre) html.push('<pre><code>' + escapeHtml(preLines.join('\n')) + '</code></pre>');
    flushList();
    return '<div class="md-view">' + html.join('\n') + '</div>';
  }

  function showToast(msg, kind) {
    var stack = el('toastStack');
    var node = document.createElement('div');
    node.className = 'toast' + (kind ? (' ' + kind) : '');
    node.textContent = msg;
    stack.appendChild(node);
    setTimeout(function () {
      if (node.parentNode) node.parentNode.removeChild(node);
    }, 3200);
  }

  async function apiFetch(url, opts) {
    var options = opts || {};
    var headers = options.headers || {};
    if (state.token) headers['X-AGY-Token'] = state.token;
    if (options.body && !headers['Content-Type']) {
      headers['Content-Type'] = 'application/json; charset=utf-8';
    }
    options.headers = headers;
    options.credentials = 'same-origin';
    var res = await fetch(url, options);
    var text = await res.text();
    var json = null;
    try { json = text ? JSON.parse(text) : {}; } catch (_e) { json = { ok: false, error: text || ('HTTP ' + res.status) }; }
    if (!res.ok) {
      var err = new Error((json && json.error) ? json.error : ('HTTP ' + res.status));
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

  function applyTheme(theme) {
    state.theme = theme === 'dark' ? 'dark' : 'light';
    document.documentElement.setAttribute('data-theme', state.theme);
    localStorage.setItem('agy_theme', state.theme);
    el('lblThemeToggle').textContent = state.theme === 'dark' ? '浅色模式' : '深色模式';
  }

  function injectStaticIcons() {
    var nodes = document.querySelectorAll('[data-icon]');
    for (var i = 0; i < nodes.length; i++) {
      var k = nodes[i].getAttribute('data-icon');
      if (ICONS[k]) nodes[i].innerHTML = ICONS[k];
    }
  }

  function openModal(title, bodyHtml, footerHtml, onBind) {
    el('modalTitle').textContent = title;
    el('modalBody').innerHTML = bodyHtml;
    el('modalFooter').innerHTML = footerHtml || '';
    el('modalBackdrop').classList.remove('hidden');
    if (onBind) onBind(el('modalBody'), el('modalFooter'));
  }

  function closeModal() {
    el('modalBackdrop').classList.add('hidden');
  }

  function openDrawer(title, subtitle, bodyHtml, headerActionsHtml, onBind, dirtyGuard) {
    state.drawerDirtyCheck = dirtyGuard || null;
    el('drawerTitle').textContent = title;
    el('drawerSubtitle').textContent = subtitle || '';
    el('drawerHeaderActions').innerHTML = headerActionsHtml || '';
    el('drawerBody').innerHTML = bodyHtml;
    el('drawerBackdrop').classList.remove('hidden');
    if (onBind) onBind(el('drawerBody'), el('drawerHeaderActions'));
  }

  function closeDrawer(force) {
    if (!force && state.drawerDirtyCheck && state.drawerDirtyCheck()) {
      openModal(
        '放弃未保存的规则修改？',
        '<div>当前规则表单或 JSON 存在尚未保存的更改，关闭抽屉将丢失这些编辑。</div>',
        '<button class="btn" data-act="cancel-close">继续编辑</button><button class="btn btn-danger" data-act="confirm-discard">放弃并关闭</button>',
        function (_mb, mf) {
          mf.querySelector('[data-act="cancel-close"]').addEventListener('click', closeModal);
          mf.querySelector('[data-act="confirm-discard"]').addEventListener('click', function () {
            closeModal();
            closeDrawer(true);
          });
        }
      );
      return;
    }
    state.drawerDirtyCheck = null;
    el('drawerBackdrop').classList.add('hidden');
  }

  function confirmCloseIdeModal(parentCount, lsCount, actionDesc, onProceed) {
    openModal(
      '检测到 Antigravity IDE 正在运行',
      '<div style="margin-bottom:10px">准备执行 <strong>' + escapeHtml(actionDesc) + '</strong>。当前检测到 <strong>' +
        escapeHtml(parentCount) + '</strong> 个主进程与 <strong>' + escapeHtml(lsCount) + '</strong> 个语言服务进程正在占用数据库与索引。</div>' +
      '<div class="muted">为保证数据与 WAL 日志安全，是否先优雅关闭 Antigravity 再继续？</div>',
      '<button class="btn" data-act="cancel">取消</button><button class="btn btn-primary" data-act="close-ide">关闭 Antigravity 并继续</button>',
      function (_b, mf) {
        mf.querySelector('[data-act="cancel"]').addEventListener('click', closeModal);
        mf.querySelector('[data-act="close-ide"]').addEventListener('click', function () {
          closeModal();
          onProceed();
        });
      }
    );
  }

  function updateShellBars() {
    if (!state.overview) return;
    var ov = state.overview;
    var counts = ov.counts || {};
    el('navCountSessions').textContent = counts.formalPrimarySessions || 0;
    el('navCountUntagged').textContent = counts.untaggedFormalSessions || 0;

    var cs = counts.capabilitySummary || {};
    var capDiffCount = (cs.remoteOnly || 0) + (cs.remoteNewer || 0) + (cs.localNewer || 0) + (cs.bothModified || 0);
    el('navCountCapDiffs').textContent = capDiffCount;

    var ide = ov.ide || {};
    var dot = el('ideStatusDot');
    var txt = el('ideStatusText');
    var closeBtn = el('btnQuickCloseIde');
    if (ide.running) {
      dot.className = 'status-dot busy';
      txt.textContent = 'IDE 运行中 (' + ((ide.parentCount || 0) + (ide.lsCount || 0)) + ' 进程)';
      closeBtn.classList.remove('hidden');
    } else {
      dot.className = 'status-dot idle';
      txt.textContent = 'IDE 空闲 · 可安全全写';
      closeBtn.classList.add('hidden');
    }

    var logs = ov.logs || [];
    if (logs.length > 0) {
      var latest = logs[logs.length - 1];
      el('activityLatest').textContent = '[' + latest.time + '] ' + latest.message;
      el('activityDot').className = 'activity-dot' + (latest.level === 'warn' ? ' warn' : (latest.level === 'error' ? ' error' : ''));
      renderLogsDrawer(logs);
    }

    var job = ov.job || {};
    if (job.busy) {
      el('jobBanner').classList.remove('hidden');
      el('jobBannerText').textContent = job.message || '后台任务执行中...';
      el('jobProgressFill').style.width = (job.percent || 20) + '%';
    } else {
      el('jobBanner').classList.add('hidden');
    }
  }

  function renderLogsDrawer(logs) {
    var html = [];
    for (var i = logs.length - 1; i >= 0; i--) {
      var l = logs[i];
      html.push('<div class="log-line ' + escapeHtml(l.level) + '">[' + escapeHtml(l.time) + '] ' + escapeHtml(l.message) + '</div>');
    }
    el('logDrawerBody').innerHTML = html.join('') || '<div class="muted">暂无活动记录</div>';
  }

  async function refreshAll(showToastMsg) {
    var data = await Promise.all([
      apiFetch('/api/overview'),
      apiFetch('/api/sessions'),
      apiFetch('/api/rename/candidates'),
      apiFetch('/api/capabilities'),
      apiFetch('/api/backup/current')
    ]);
    state.overview = data[0];
    state.sessionsData = data[1];
    applyFreshRenameData(data[2]);
    state.capData = data[3];
    state.backupData = data[4];
    updateShellBars();
    renderCurrentView();
    if (showToastMsg) showToast(showToastMsg, 'success');
  }

  function applyFreshRenameData(data) {
    state.renameData = data;
    var cands = (data && data.candidates) || [];
    var newChecked = {};
    var foundFocused = false;
    for (var i = 0; i < cands.length; i++) {
      var c = cands[i];
      if (state.renameChecked[c.cid] !== undefined) newChecked[c.cid] = state.renameChecked[c.cid];
      else newChecked[c.cid] = !!c.selected;
      if (c.cid === state.renameFocusedCid) foundFocused = true;
    }
    state.renameChecked = newChecked;
    if (!foundFocused && cands.length > 0) state.renameFocusedCid = cands[0].cid;
  }

  function switchView(view) {
    state.view = view;
    var navBtns = document.querySelectorAll('#navList .nav-item');
    for (var i = 0; i < navBtns.length; i++) {
      var b = navBtns[i];
      if (b.getAttribute('data-view') === view) b.classList.add('active');
      else b.classList.remove('active');
    }
    renderCurrentView();
  }

  function renderCurrentView() {
    var titles = {
      overview: ['概览', '查看本机状态、同步差异与存储健康度'],
      sessions: ['会话管理', '浏览按工程与最短区分路径分组的对话，支持正文检索与 Markdown 渲染预览'],
      rename: ['智能重命名工作台', '三步完成规则预填、AI 润色精校与可回滚三层写入'],
      capabilities: ['能力仓库同步矩阵', '本机环境与内置 Repository/ 的方向性差异对比与安全同步'],
      backup: ['备份与迁移中心', '创建全量 .agy 备份或从归档包按需导入能力与会话']
    };
    var t = titles[state.view] || ['工作台', ''];
    el('pageTitle').textContent = t[0];
    el('pageSubtitle').textContent = t[1];
    el('topbarActions').innerHTML = '';

    if (state.view === 'overview') renderOverview();
    else if (state.view === 'sessions') renderSessions();
    else if (state.view === 'rename') renderRename();
    else if (state.view === 'capabilities') renderCapabilities();
    else if (state.view === 'backup') renderBackup();
  }

  /* ══════════════════════════════════════════════════════
   * 1. OVERVIEW VIEW
   * ══════════════════════════════════════════════════════ */
  function renderOverview() {
    if (!state.overview) {
      el('mainCanvas').innerHTML = '<div class="empty-state">正在加载环境体检数据...</div>';
      return;
    }
    var ov = state.overview;
    var c = ov.counts || {};
    var cs = c.capabilitySummary || {};
    var gc = ov.lastOrphanScan;
    var paths = ov.paths || {};
    var diffTotal = (cs.remoteOnly || 0) + (cs.remoteNewer || 0) + (cs.localNewer || 0) + (cs.bothModified || 0);

    var gcHtml = gc
      ? ('<div class="metric-value">' + escapeHtml(gc.totalMegabytes) + ' <span style="font-size:14px;font-weight:500">MB</span></div>' +
         '<div class="metric-desc">数据库 ' + gc.dbFilesCount + ' · 脑区 ' + gc.brainDirsCount + ' · 标注 ' + gc.annotationFilesCount + '</div>')
      : '<div class="metric-value" style="font-size:18px">待体检</div><div class="metric-desc">支持 IDE 运行时只读零风险扫描</div>';

    var html =
      '<div class="overview-grid">' +
        '<div class="metric-card">' +
          '<div class="metric-label">本机能力资产</div>' +
          '<div class="metric-value">' + escapeHtml(c.localCapabilitiesCount || 0) + '</div>' +
          '<div class="metric-desc">' + (cs.missingPaths > 0 ? '<span class="badge badge-red">⚠ ' + cs.missingPaths + ' 项路径缺失</span>' : '全部脚本路径正常') + '</div>' +
        '</div>' +
        '<div class="metric-card">' +
          '<div class="metric-label">正式工程与主会话</div>' +
          '<div class="metric-value">' + escapeHtml(c.formalPrimarySessions || 0) + '</div>' +
          '<div class="metric-desc">' + escapeHtml(c.formalProjectsCount || 0) + ' 个正式工程 · 另有 CLI ' + (c.cliSessions || 0) + ' / 项目外 ' + (c.outsideSessions || 0) + ' / 子任务 ' + (c.subagentSessions || 0) + '</div>' +
        '</div>' +
        '<div class="metric-card">' +
          '<div class="metric-label">待同步能力差异</div>' +
          '<div class="metric-value">' + escapeHtml(diffTotal) + '</div>' +
          '<div class="metric-desc">仓库较新/新增 ' + ((cs.remoteOnly || 0) + (cs.remoteNewer || 0)) + ' · 本机较新 ' + (cs.localNewer || 0) + '</div>' +
        '</div>' +
        '<div class="metric-card">' +
          '<div class="metric-label">孤儿残留体检</div>' + gcHtml +
        '</div>' +
      '</div>' +

      '<div class="action-grid">' +
        '<div class="quick-card">' +
          '<div>' +
            '<div class="quick-card-title">整理未规范命名的历史会话</div>' +
            '<div class="quick-card-desc">发现 <strong>' + escapeHtml(c.untaggedFormalSessions || 0) + '</strong> 条尚未带标准 <span class="mono">【机型·分支】</span> 前缀的正式对话，可规则生成或用 AI 批量润色。</div>' +
          '</div>' +
          '<button class="btn btn-primary" data-jump="rename">开始重命名 →</button>' +
        '</div>' +
        '<div class="quick-card">' +
          '<div>' +
            '<div class="quick-card-title">核对与同步内置能力仓库</div>' +
            '<div class="quick-card-desc">直观查看本机与 <span class="mono">Repository/</span> 的双向新旧方向与行级源码 Diff，避免误覆盖最新修改。</div>' +
          '</div>' +
          '<button class="btn" data-jump="capabilities">进入同步矩阵 →</button>' +
        '</div>' +
        '<div class="quick-card">' +
          '<div>' +
            '<div class="quick-card-title">浏览会话与 Markdown 导出</div>' +
            '<div class="quick-card-desc">支持按工程与最短区分路径分组、全文检索对话正文，并实时渲染导出完整对话为 <span class="mono">.md</span>。</div>' +
          '</div>' +
          '<button class="btn" data-jump="sessions">管理会话 →</button>' +
        '</div>' +
        '<div class="quick-card">' +
          '<div>' +
            '<div class="quick-card-title">存储健康体检（孤儿数据清理）</div>' +
            '<div class="quick-card-desc">先只读扫描已脱离索引的残留 <span class="mono">.db/.wal/brain/.pbtxt</span>，确有可回收空间后再选择关闭 IDE 清理。</div>' +
          '</div>' +
          '<div class="row gap-sm">' +
            '<button class="btn" id="btnOverviewGcScan">只读扫描</button>' +
            (gc && gc.totalItems > 0 ? '<button class="btn btn-danger-outline" id="btnOverviewGcClean">清理 (' + escapeHtml(gc.totalMegabytes) + ' MB)</button>' : '') +
          '</div>' +
        '</div>' +
      '</div>' +

      '<div class="card">' +
        '<div class="between" style="margin-bottom:10px">' +
          '<strong>工作环境路径与备份</strong>' +
          '<button class="btn btn-sm" id="btnOverviewFullBackup">一键创建全量备份 (.agy)...</button>' +
        '</div>' +
        '<div class="muted mono" style="font-size:12px;line-height:1.8">' +
          '<div>.gemini 目录：' + escapeHtml(paths.gemini || '') + '</div>' +
          '<div>内置能力仓库：' + escapeHtml(paths.repository || '') + '</div>' +
        '</div>' +
      '</div>';

    var root = el('mainCanvas');
    root.innerHTML = html;

    var jumpBtns = root.querySelectorAll('[data-jump]');
    for (var i = 0; i < jumpBtns.length; i++) {
      jumpBtns[i].addEventListener('click', function () { switchView(this.getAttribute('data-jump')); });
    }
    el('btnOverviewGcScan').addEventListener('click', handleReadOnlyGcScan);
    var btnClean = el('btnOverviewGcClean');
    if (btnClean) btnClean.addEventListener('click', function () { promptAndExecuteGcClean(false); });
    el('btnOverviewFullBackup').addEventListener('click', function () {
      switchView('backup');
      triggerFullBackup();
    });
  }

  async function handleReadOnlyGcScan() {
    try {
      var res = await apiFetch('/api/gc/scan', { method: 'POST', body: '{}' });
      if (!state.overview) state.overview = {};
      state.overview.lastOrphanScan = res;
      if (state.view === 'overview') renderOverview();
      if (res.totalItems === 0) {
        showToast('环境非常干净，未发现任何孤儿残留数据。', 'success');
      } else {
        openModal(
          '孤儿垃圾只读体检结果',
          '<div>发现以下已脱离索引的残留文件（不强制关闭 IDE 即可检出）：</div>' +
          '<ul style="margin:10px 0 12px 18px;line-height:1.7">' +
            '<li>残留会话 ID：<strong>' + res.sessionCount + '</strong> 个</li>' +
            '<li>孤儿数据库 (.db/.wal/.shm)：<strong>' + res.dbFilesCount + '</strong> 个</li>' +
            '<li>孤儿脑区目录 (brain/&lt;uuid&gt;)：<strong>' + res.brainDirsCount + '</strong> 个</li>' +
            '<li>孤儿标题标注 (.pbtxt)：<strong>' + res.annotationFilesCount + '</strong> 个</li>' +
            '<li>可回收空间：<strong>' + res.totalMegabytes + ' MB</strong></li>' +
          '</ul>',
          '<button class="btn" data-act="close">暂不清理</button><button class="btn btn-danger" data-act="clean">立即安全清理</button>',
          function (_b, mf) {
            mf.querySelector('[data-act="close"]').addEventListener('click', closeModal);
            mf.querySelector('[data-act="clean"]').addEventListener('click', function () {
              closeModal();
              promptAndExecuteGcClean(false);
            });
          }
        );
      }
    } catch (e) {
      showToast('孤儿体检失败：' + e.message, 'error');
    }
  }

  async function promptAndExecuteGcClean(closeIde) {
    try {
      var res = await apiFetch('/api/gc/clean', { method: 'POST', body: JSON.stringify({ closeIdeIfRunning: !!closeIde }) });
      await refreshAll();
      showToast('已清理 ' + (res.deletedDbFiles + res.deletedBrainDirs + res.deletedAnnotations) + ' 项残留，释放 ' + res.freedMegabytes + ' MB', 'success');
    } catch (e) {
      if (e.status === 409 && e.message.indexOf('IDE') >= 0 && !closeIde) {
        var ide = (state.overview && state.overview.ide) || {};
        confirmCloseIdeModal(ide.parentCount || 1, ide.lsCount || 0, '清理孤儿残留文件', function () {
          promptAndExecuteGcClean(true);
        });
      } else {
        showToast(e.message, 'error');
      }
    }
  }

  /* ══════════════════════════════════════════════════════
   * 2. SESSIONS VIEW
   * ══════════════════════════════════════════════════════ */
  function renderSessions() {
    var sd = state.sessionsData;
    if (!sd) {
      el('mainCanvas').innerHTML = '<div class="empty-state">正在加载工程与会话列表...</div>';
      return;
    }

    var groups = sd.groups || [];
    var formalGroups = [];
    var auxGroups = [];
    for (var i = 0; i < groups.length; i++) {
      if (groups[i].groupKind === 'formal') formalGroups.push(groups[i]);
      else auxGroups.push(groups[i]);
    }

    var projCardsHtml = [];
    projCardsHtml.push(
      '<div class="proj-card ' + (state.selectedBucketKey === '__ALL__' ? 'active' : '') + '" data-bucket="__ALL__">' +
        '<div class="proj-card-top"><span class="proj-card-name">全部正式工程</span><span class="badge badge-accent">' + escapeHtml(sd.formalPrimarySessions || 0) + '</span></div>' +
        '<div class="proj-card-path">' + escapeHtml(sd.formalProjectsCount || 0) + ' 个工作区（不含辅助分组）</div>' +
      '</div>'
    );

    for (var f = 0; f < formalGroups.length; f++) {
      var fg = formalGroups[f];
      var subLabel = fg.isDuplicateBasename && fg.distinguishingLabel ? fg.distinguishingLabel : fg.shortPath;
      projCardsHtml.push(
        '<div class="proj-card ' + (state.selectedBucketKey === fg.bucketKey ? 'active' : '') + '" data-bucket="' + escapeHtml(fg.bucketKey) + '" title="' + escapeHtml(fg.fullPath || '') + '">' +
          '<div class="proj-card-top">' +
            '<span class="proj-card-name">' + escapeHtml(fg.projectName) + '</span>' +
            '<span class="badge badge-neutral">' + escapeHtml(fg.primaryCount) + '</span>' +
          '</div>' +
          '<div class="proj-card-path">' + escapeHtml(subLabel) + '</div>' +
        '</div>'
      );
    }

    if (auxGroups.length > 0) {
      projCardsHtml.push(
        '<div class="aux-divider" id="btnToggleAuxGroups">' +
          '<span>辅助分组 (CLI / 项目外 / 子任务)</span>' +
          '<span>' + (state.auxExpanded ? '▾' : '▸') + '</span>' +
        '</div>'
      );
      if (state.auxExpanded) {
        for (var a = 0; a < auxGroups.length; a++) {
          var ag = auxGroups[a];
          projCardsHtml.push(
            '<div class="proj-card ' + (state.selectedBucketKey === ag.bucketKey ? 'active' : '') + '" data-bucket="' + escapeHtml(ag.bucketKey) + '">' +
              '<div class="proj-card-top">' +
                '<span class="proj-card-name">' + escapeHtml(ag.projectName) + '</span>' +
                '<span class="badge badge-neutral">' + escapeHtml(ag.totalCount) + '</span>' +
              '</div>' +
              '<div class="proj-card-path">' + escapeHtml(ag.shortPath) + '</div>' +
            '</div>'
          );
        }
      }
    }

    // Collect sessions to display
    var visibleSessions = [];
    var selectedGroupObj = null;
    for (var gi = 0; gi < groups.length; gi++) {
      var g = groups[gi];
      if (state.selectedBucketKey === '__ALL__') {
        if (g.groupKind !== 'formal') continue;
      } else if (g.bucketKey !== state.selectedBucketKey) {
        continue;
      } else {
        selectedGroupObj = g;
      }
      var list = g.sessions || [];
      for (var si = 0; si < list.length; si++) {
        var s = list[si];
        if (state.sessionSearchQuery.trim().length > 0) {
          var q = state.sessionSearchQuery.trim().toLowerCase();
          var inMatchedSet = state.sessionSearchMatchedIds ? !!state.sessionSearchMatchedIds[s.cid] : false;
          var localHit = (s.title && s.title.toLowerCase().indexOf(q) >= 0) ||
                         (s.projectName && s.projectName.toLowerCase().indexOf(q) >= 0);
          if (state.sessionSearchContent) {
            if (!inMatchedSet && !localHit) continue;
          } else if (!localHit) {
            continue;
          }
        }
        visibleSessions.push(s);
      }
    }

    visibleSessions.sort(function (x, y) { return String(y.updatedIso || '').localeCompare(String(x.updatedIso || '')); });

    var sessHtml = [];
    if (visibleSessions.length === 0) {
      sessHtml.push('<div class="empty-state">当前筛选条件下未找到匹配对话。' + (state.sessionSearchQuery ? '<div style="margin-top:8px"><button class="btn btn-sm" id="btnClearEmptySearch">清空搜索词</button></div>' : '') + '</div>');
    } else {
      for (var k = 0; k < visibleSessions.length; k++) {
        var row = visibleSessions[k];
        var isChecked = !!state.checkedSessions[row.cid];
        sessHtml.push(
          '<div class="session-row ' + (isChecked ? 'selected' : '') + '" data-cid="' + escapeHtml(row.cid) + '">' +
            '<input type="checkbox" class="sess-chk" data-cid="' + escapeHtml(row.cid) + '"' + (isChecked ? ' checked' : '') + ' />' +
            '<div class="session-main" data-preview-cid="' + escapeHtml(row.cid) + '">' +
              '<div class="session-title">' + escapeHtml(row.title) + '</div>' +
              '<div class="session-meta">' +
                '<span class="badge badge-neutral">' + escapeHtml(row.projectName) + '</span>' +
                (row.isCli ? '<span class="badge badge-blue">CLI</span>' : '') +
                (row.isRunning ? '<span class="badge badge-amber">运行中</span>' : '') +
                '<span>' + escapeHtml(row.stepCount) + ' 步</span>' +
                '<span>' + escapeHtml(row.updatedTime) + '</span>' +
              '</div>' +
            '</div>' +
            '<div class="session-actions">' +
              '<button class="btn-icon" data-sess-act="preview" data-cid="' + escapeHtml(row.cid) + '" title="阅读 Markdown 对话">' + ICONS.eye + '</button>' +
              '<button class="btn-icon" data-sess-act="rename" data-cid="' + escapeHtml(row.cid) + '" title="重命名此会话">' + ICONS.edit + '</button>' +
              '<button class="btn-icon" data-sess-act="move" data-cid="' + escapeHtml(row.cid) + '" title="移动到其他工程">' + ICONS.folder + '</button>' +
              '<button class="btn-icon" data-sess-act="exportmd" data-cid="' + escapeHtml(row.cid) + '" title="导出为 .md">' + ICONS.download + '</button>' +
            '</div>' +
          '</div>'
        );
      }
    }

    var checkedCids = Object.keys(state.checkedSessions).filter(function (id) { return state.checkedSessions[id]; });
    var projRelocateBtn = (selectedGroupObj && selectedGroupObj.groupKind === 'formal')
      ? '<button class="btn btn-sm" id="btnRelocateCurProj" title="修复或迁移该工程关联的本地目录">重定位工程目录</button>'
      : '';

    el('mainCanvas').innerHTML =
      '<div class="sessions-layout">' +
        '<section class="project-pane">' +
          '<div class="pane-header"><span>工程与工作区</span><span class="muted" style="font-weight:500">' + formalGroups.length + '</span></div>' +
          '<div class="project-scroll">' + projCardsHtml.join('') + '</div>' +
        '</section>' +
        '<section class="session-pane">' +
          '<div class="session-toolbar">' +
            '<div class="search-box">' +
              '<span class="muted" style="display:flex">' + ICONS.search + '</span>' +
              '<input id="txtSessionSearch" type="text" placeholder="搜索会话标题、工程名' + (state.sessionSearchContent ? '或对话正文 (FTS5)...' : '...') + '" value="' + escapeHtml(state.sessionSearchQuery) + '" />' +
              (state.sessionSearchQuery ? '<button class="btn-icon" id="btnClearSessSearch" style="width:20px;height:20px" title="清空">✕</button>' : '') +
            '</div>' +
            '<button class="filter-pill ' + (state.sessionSearchContent ? 'active' : '') + '" id="btnToggleSearchContent" type="button">搜对话正文</button>' +
            '<span class="muted" style="font-size:12px">命中 ' + visibleSessions.length + ' 条</span>' +
            '<button class="btn btn-sm" id="btnSessSelectVisible">全选当前</button>' +
            '<button class="btn btn-sm" id="btnSessClearChecked">清空勾选</button>' +
            projRelocateBtn +
          '</div>' +
          '<div class="session-scroll">' + sessHtml.join('') + '</div>' +
          (checkedCids.length > 0
            ? '<div class="floating-batch-bar">' +
                '<div class="row gap-sm"><strong>已选 ' + checkedCids.length + ' 条会话</strong></div>' +
                '<div class="row gap-sm">' +
                  '<button class="btn btn-sm btn-primary" id="btnBatchGotoRename">批量重命名</button>' +
                  '<button class="btn btn-sm" id="btnBatchMove">迁移工程</button>' +
                  '<button class="btn btn-sm" id="btnBatchExportAgy">导出 .agy</button>' +
                  '<button class="btn btn-sm btn-danger" id="btnBatchDelete">删除所选</button>' +
                '</div>' +
              '</div>'
            : '') +
        '</section>' +
      '</div>';

    // Bind left project cards
    var canvas = el('mainCanvas');
    var projCards = canvas.querySelectorAll('.proj-card[data-bucket]');
    for (var p = 0; p < projCards.length; p++) {
      projCards[p].addEventListener('click', function () {
        state.selectedBucketKey = this.getAttribute('data-bucket');
        renderSessions();
      });
    }
    var auxBtn = el('btnToggleAuxGroups');
    if (auxBtn) auxBtn.addEventListener('click', function () { state.auxExpanded = !state.auxExpanded; renderSessions(); });

    if (el('btnRelocateCurProj')) {
      el('btnRelocateCurProj').addEventListener('click', function () {
        openRelocateProjectModal(selectedGroupObj);
      });
    }

    var searchInput = el('txtSessionSearch');
    var searchTimer = null;
    searchInput.addEventListener('input', function () {
      state.sessionSearchQuery = this.value;
      clearTimeout(searchTimer);
      searchTimer = setTimeout(async function () {
        if (state.sessionSearchContent && state.sessionSearchQuery.trim().length > 0) {
          try {
            var sRes = await apiFetch('/api/sessions/search', {
              method: 'POST',
              body: JSON.stringify({ query: state.sessionSearchQuery, searchContent: true })
            });
            var map = {};
            var ids = sRes.matchedIds || [];
            for (var x = 0; x < ids.length; x++) map[ids[x]] = true;
            state.sessionSearchMatchedIds = map;
          } catch (_e) {}
        } else {
          state.sessionSearchMatchedIds = null;
        }
        renderSessionsKeepFocus();
      }, 180);
    });

    function renderSessionsKeepFocus() {
      renderSessions();
      var inp = el('txtSessionSearch');
      if (inp) {
        inp.focus();
        inp.setSelectionRange(inp.value.length, inp.value.length);
      }
    }

    if (el('btnClearSessSearch')) {
      el('btnClearSessSearch').addEventListener('click', function () {
        state.sessionSearchQuery = '';
        state.sessionSearchMatchedIds = null;
        renderSessions();
      });
    }
    if (el('btnClearEmptySearch')) {
      el('btnClearEmptySearch').addEventListener('click', function () {
        state.sessionSearchQuery = '';
        state.sessionSearchMatchedIds = null;
        renderSessions();
      });
    }
    el('btnToggleSearchContent').addEventListener('click', async function () {
      state.sessionSearchContent = !state.sessionSearchContent;
      if (state.sessionSearchContent && state.sessionSearchQuery.trim().length > 0) {
        var sRes = await apiFetch('/api/sessions/search', {
          method: 'POST',
          body: JSON.stringify({ query: state.sessionSearchQuery, searchContent: true })
        });
        var map = {};
        var ids = sRes.matchedIds || [];
        for (var x = 0; x < ids.length; x++) map[ids[x]] = true;
        state.sessionSearchMatchedIds = map;
      } else {
        state.sessionSearchMatchedIds = null;
      }
      renderSessions();
    });

    el('btnSessSelectVisible').addEventListener('click', function () {
      for (var i2 = 0; i2 < visibleSessions.length; i2++) state.checkedSessions[visibleSessions[i2].cid] = true;
      renderSessions();
    });
    el('btnSessClearChecked').addEventListener('click', function () {
      state.checkedSessions = {};
      renderSessions();
    });

    // Row checkboxes & actions
    var chks = canvas.querySelectorAll('.sess-chk');
    for (var ci = 0; ci < chks.length; ci++) {
      chks[ci].addEventListener('change', function () {
        var id = this.getAttribute('data-cid');
        state.checkedSessions[id] = this.checked;
        renderSessions();
      });
    }
    var mains = canvas.querySelectorAll('[data-preview-cid]');
    for (var mi = 0; mi < mains.length; mi++) {
      mains[mi].addEventListener('click', function () {
        openMarkdownDrawer(this.getAttribute('data-preview-cid'), 'local');
      });
    }
    var actBtns = canvas.querySelectorAll('[data-sess-act]');
    for (var ai = 0; ai < actBtns.length; ai++) {
      actBtns[ai].addEventListener('click', function (ev) {
        ev.stopPropagation();
        var act = this.getAttribute('data-sess-act');
        var cid = this.getAttribute('data-cid');
        var sessObj = findLocalSessionById(cid);
        if (act === 'preview') openMarkdownDrawer(cid, 'local');
        else if (act === 'rename') openSingleRenameModal(sessObj);
        else if (act === 'move') openMoveSessionsModal([cid]);
        else if (act === 'exportmd') exportSessionMarkdown(cid, 'local');
      });
    }

    if (checkedCids.length > 0) {
      el('btnBatchGotoRename').addEventListener('click', function () {
        var newChecked = {};
        for (var j = 0; j < checkedCids.length; j++) newChecked[checkedCids[j]] = true;
        state.renameChecked = newChecked;
        state.renameProjectFilter = '__SCOPED__';
        state.renameShowTagged = true;
        state.renameShowAux = true;
        state.renameShowRunning = true;
        state.renameShowLowValue = true;
        state.renameFocusedCid = checkedCids[0];
        state.renameStep = 2;
        switchView('rename');
      });
      el('btnBatchMove').addEventListener('click', function () { openMoveSessionsModal(checkedCids); });
      el('btnBatchExportAgy').addEventListener('click', function () { exportSelectiveAgyModal(checkedCids, []); });
      el('btnBatchDelete').addEventListener('click', function () { openDeleteSessionsModal(checkedCids); });
    }
  }

  function findLocalSessionById(cid) {
    if (!state.sessionsData || !state.sessionsData.groups) return null;
    var groups = state.sessionsData.groups;
    for (var i = 0; i < groups.length; i++) {
      var list = groups[i].sessions || [];
      for (var k = 0; k < list.length; k++) {
        if (list[k].cid === cid) return list[k];
      }
    }
    return null;
  }

  async function openMarkdownDrawer(cid, source) {
    try {
      var data = await apiFetch('/api/sessions/markdown?cid=' + encodeURIComponent(cid) + '&source=' + encodeURIComponent(source || 'local'));
      var sub = (data.projectName || '-') + '  ·  ' + data.stepCount + ' 步  ·  ' + data.updatedTime + (data.isCli ? '  ·  CLI 会话' : '');
      var findQuery = '';
      var hdrActions =
        '<button class="btn btn-sm" id="btnDrawerCopyMd">复制全文</button>' +
        '<button class="btn btn-sm btn-primary" id="btnDrawerExportMd">导出 .md</button>';
      var body =
        '<div class="between gap-sm" style="margin-bottom:12px">' +
          '<input class="input" id="txtMdFind" placeholder="在当前对话中查找关键词高亮..." style="max-width:340px" />' +
          (data.truncated ? '<span class="badge badge-amber">长对话预览已截断至 512KB，导出 .md 可得全文</span>' : '') +
        '</div>' +
        '<div id="mdRenderedHost">' + renderMarkdownSafe(data.markdown, '') + '</div>' +
        '<div class="accordion" style="margin-top:16px">' +
          '<button class="accordion-trigger" id="btnMdTechToggle">▸ 技术元数据</button>' +
          '<div class="accordion-body hidden mono" id="mdTechBody">会话 UUID: ' + escapeHtml(data.cid) + '<br>来源: ' + (source === 'backup' ? '.agy 备份归档 (免导入直读)' : '本机环境') + '</div>' +
        '</div>';

      openDrawer(data.title || '会话预览', sub, body, hdrActions, function (db, ha) {
        ha.querySelector('#btnDrawerCopyMd').addEventListener('click', function () {
          copyToClipboard(data.markdown || '', '对话全文已复制 ✓');
        });
        ha.querySelector('#btnDrawerExportMd').addEventListener('click', function () {
          exportSessionMarkdown(cid, source);
        });
        db.querySelector('#txtMdFind').addEventListener('input', function () {
          findQuery = this.value;
          db.querySelector('#mdRenderedHost').innerHTML = renderMarkdownSafe(data.markdown, findQuery);
        });
        db.querySelector('#btnMdTechToggle').addEventListener('click', function () {
          var tb = db.querySelector('#mdTechBody');
          tb.classList.toggle('hidden');
          this.textContent = tb.classList.contains('hidden') ? '▸ 技术元数据' : '▾ 技术元数据';
        });
      });
    } catch (e) {
      showToast('预览对话失败：' + e.message, 'error');
    }
  }

  async function exportSessionMarkdown(cid, source) {
    try {
      var res = await apiFetch('/api/sessions/export-md', {
        method: 'POST',
        body: JSON.stringify({ cid: cid, source: source || 'local' })
      });
      if (!res.cancelled) showToast('已导出 Markdown 至 ' + res.outputPath, 'success');
    } catch (e) {
      showToast('导出失败：' + e.message, 'error');
    }
  }

  function openSingleRenameModal(sessObj) {
    if (!sessObj) return;
    openModal(
      '单条会话快速重命名',
      '<div class="muted" style="margin-bottom:8px">同步更新 annotations/.pbtxt、conversation_summaries.db 与 .pb 索引，并自动生成快照备份。</div>' +
      '<label style="display:block;font-weight:600;margin-bottom:6px">新会话标题</label>' +
      '<input class="input" id="txtSingleNewTitle" value="' + escapeHtml(sessObj.title) + '" />',
      '<button class="btn" data-act="cancel">取消</button><button class="btn btn-primary" data-act="save">保存重命名</button>',
      function (mb, mf) {
        var inp = mb.querySelector('#txtSingleNewTitle');
        inp.focus();
        inp.setSelectionRange(inp.value.length, inp.value.length);
        mf.querySelector('[data-act="cancel"]').addEventListener('click', closeModal);
        mf.querySelector('[data-act="save"]').addEventListener('click', function () {
          submitSingleRename(sessObj.cid, inp.value, 'auto');
        });
      }
    );
  }

  async function submitSingleRename(cid, newTitle, strategy) {
    if (!String(newTitle || '').trim()) { showToast('新标题不能为空', 'error'); return; }
    try {
      await apiFetch('/api/sessions/rename-single', {
        method: 'POST',
        body: JSON.stringify({ cid: cid, newTitle: newTitle.trim(), strategy: strategy || 'auto' })
      });
      closeModal();
      await refreshAll('会话重命名成功 ✓');
    } catch (e) {
      if (e.status === 409 && e.data && e.data.code === 'IDE_RUNNING') {
        openDualModeRenameModal(e.data.parentCount, e.data.lsCount, function (chosenStrategy) {
          submitSingleRename(cid, newTitle, chosenStrategy);
        });
      } else {
        showToast(e.message, 'error');
      }
    }
  }

  function openDualModeRenameModal(parentCount, lsCount, onConfirm) {
    var chosen = 'CloseAndFullWrite';
    openModal(
      '选择 Antigravity 运行时写入模式',
      '<div>检测到 <strong>' + parentCount + '</strong> 个主进程与 <strong>' + lsCount + '</strong> 个语言服务进程正在运行：</div>' +
      '<div class="strategy-cards">' +
        '<div class="strategy-card selected" data-strat="CloseAndFullWrite">' +
          '<strong>1. 关闭 IDE 并三层全写（推荐）</strong>' +
          '<div class="muted" style="margin-top:4px;font-size:12px">安全关闭进程后同时写入 .pbtxt / sqlite / .pb 三层，打开即生效。</div>' +
        '</div>' +
        '<div class="strategy-card" data-strat="HotWritePersistentLayers">' +
          '<strong>2. 免关机热写持久化层</strong>' +
          '<div class="muted" style="margin-top:4px;font-size:12px">不关闭 IDE，写入 .pbtxt 与 SQLite（自动跳过运行中会话），重启 IDE 后刷新侧边栏。</div>' +
        '</div>' +
      '</div>',
      '<button class="btn" data-act="cancel">取消</button><button class="btn btn-primary" data-act="go">按所选模式写入</button>',
      function (mb, mf) {
        var cards = mb.querySelectorAll('.strategy-card');
        for (var i = 0; i < cards.length; i++) {
          cards[i].addEventListener('click', function () {
            chosen = this.getAttribute('data-strat');
            for (var j = 0; j < cards.length; j++) cards[j].classList.remove('selected');
            this.classList.add('selected');
          });
        }
        mf.querySelector('[data-act="cancel"]').addEventListener('click', closeModal);
        mf.querySelector('[data-act="go"]').addEventListener('click', function () {
          closeModal();
          onConfirm(chosen);
        });
      }
    );
  }

  function openMoveSessionsModal(cids) {
    var groups = (state.sessionsData && state.sessionsData.groups) || [];
    var opts = [];
    for (var i = 0; i < groups.length; i++) {
      if (groups[i].groupKind === 'formal') {
        opts.push({ projectId: groups[i].projectId, projectName: groups[i].projectName, fullPath: groups[i].fullPath, label: groups[i].projectName + ' (' + groups[i].shortPath + ')' });
      }
    }
    opts.push({ projectId: 'outside-of-project', projectName: 'Outside of Project', fullPath: '', label: 'Outside of Project (全局无工程)' });

    var optHtml = opts.map(function (o, idx) {
      return '<option value="' + idx + '">' + escapeHtml(o.label) + '</option>';
    }).join('') + '<option value="__CUSTOM__">+ 手动指定新工程与目录...</option>';

    openModal(
      '迁移 ' + cids.length + ' 条会话到其他工程',
      '<div style="display:flex;flex-direction:column;gap:10px">' +
        '<div><label style="font-weight:600;display:block;margin-bottom:4px">选择目标工程</label>' +
        '<select class="select" id="selMoveTarget">' + optHtml + '</select></div>' +
        '<div><label style="font-weight:600;display:block;margin-bottom:4px">目标工程名称</label>' +
        '<input class="input" id="txtMoveName" value="' + escapeHtml(opts[0] ? opts[0].projectName : '') + '" /></div>' +
        '<div><label style="font-weight:600;display:block;margin-bottom:4px">目标物理目录</label>' +
        '<div class="row gap-sm"><input class="input" id="txtMovePath" value="' + escapeHtml(opts[0] ? opts[0].fullPath : '') + '" /><button class="btn" id="btnMoveBrowse">浏览...</button></div></div>' +
      '</div>',
      '<button class="btn" data-act="cancel">取消</button><button class="btn btn-primary" data-act="go">确认迁移</button>',
      function (mb, mf) {
        var sel = mb.querySelector('#selMoveTarget');
        var inpName = mb.querySelector('#txtMoveName');
        var inpPath = mb.querySelector('#txtMovePath');
        var targetId = opts[0] ? opts[0].projectId : '';

        sel.addEventListener('change', function () {
          if (this.value === '__CUSTOM__') {
            targetId = '';
            inpName.value = '';
            inpPath.value = '';
            inpPath.disabled = false;
          } else {
            var chosen = opts[parseInt(this.value, 10)];
            targetId = chosen.projectId;
            inpName.value = chosen.projectName;
            inpPath.value = chosen.fullPath;
            inpPath.disabled = chosen.projectId === 'outside-of-project';
          }
        });
        mb.querySelector('#btnMoveBrowse').addEventListener('click', async function () {
          var pick = await apiFetch('/api/native/pick-folder', { method: 'POST', body: JSON.stringify({ title: '选择目标工程目录', initialPath: inpPath.value }) });
          if (!pick.cancelled && pick.path) {
            inpPath.value = pick.path;
            if (!inpName.value.trim()) inpName.value = pick.path.split(/[\\/]/).pop() || '';
          }
        });
        mf.querySelector('[data-act="cancel"]').addEventListener('click', closeModal);
        mf.querySelector('[data-act="go"]').addEventListener('click', async function () {
          async function doMove(closeIde) {
            try {
              await apiFetch('/api/sessions/move', {
                method: 'POST',
                body: JSON.stringify({ cids: cids, targetProjectId: targetId, targetProjectName: inpName.value.trim(), targetFolderPath: inpPath.value.trim(), closeIdeIfRunning: closeIde })
              });
              closeModal();
              state.checkedSessions = {};
              await refreshAll('会话迁移完成 ✓');
            } catch (e) {
              if (e.status === 409 && e.data && e.data.code === 'IDE_RUNNING') {
                confirmCloseIdeModal(e.data.parentCount, e.data.lsCount, '迁移会话工程归属', function () { doMove(true); });
              } else { showToast(e.message, 'error'); }
            }
          }
          doMove(false);
        });
      }
    );
  }

  function openRelocateProjectModal(groupObj) {
    if (!groupObj) return;
    openModal(
      '重定位工程物理目录 — ' + groupObj.projectName,
      '<div style="display:flex;flex-direction:column;gap:10px">' +
        '<div class="muted">同步更新 projects/*.json、.pb 索引及该工程名下全部会话数据库路径。</div>' +
        '<div><label style="font-weight:600;display:block;margin-bottom:4px">当前目录</label>' +
        '<div class="mono muted" style="font-size:12px">' + escapeHtml(groupObj.fullPath || '(未绑定)') + '</div></div>' +
        '<div><label style="font-weight:600;display:block;margin-bottom:4px">新的本地物理目录</label>' +
        '<div class="row gap-sm"><input class="input" id="txtRelocProjPath" value="' + escapeHtml(groupObj.fullPath || '') + '" /><button class="btn" id="btnRelocProjBrowse">浏览...</button></div></div>' +
      '</div>',
      '<button class="btn" data-act="cancel">取消</button><button class="btn btn-primary" data-act="go">确认重定位</button>',
      function (mb, mf) {
        var inp = mb.querySelector('#txtRelocProjPath');
        mb.querySelector('#btnRelocProjBrowse').addEventListener('click', async function () {
          var pick = await apiFetch('/api/native/pick-folder', { method: 'POST', body: JSON.stringify({ title: '选择新工程物理目录', initialPath: inp.value }) });
          if (!pick.cancelled && pick.path) inp.value = pick.path;
        });
        mf.querySelector('[data-act="cancel"]').addEventListener('click', closeModal);
        mf.querySelector('[data-act="go"]').addEventListener('click', function () {
          async function doReloc(closeIde) {
            try {
              await apiFetch('/api/projects/relocate', {
                method: 'POST',
                body: JSON.stringify({ projectId: groupObj.projectId, projectName: groupObj.projectName, newFolderPath: inp.value.trim(), closeIdeIfRunning: closeIde })
              });
              closeModal();
              await refreshAll('工程目录重定位完成 ✓');
            } catch (e) {
              if (e.status === 409 && e.data && e.data.code === 'IDE_RUNNING') {
                confirmCloseIdeModal(e.data.parentCount, e.data.lsCount, '重定位工程物理目录', function () { doReloc(true); });
              } else { showToast(e.message, 'error'); }
            }
          }
          doReloc(false);
        });
      }
    );
  }

  function openDeleteSessionsModal(cids) {
    openModal(
      '确认级联物理删除 ' + cids.length + ' 条会话？',
      '<div style="color:var(--red-text);font-weight:600;margin-bottom:8px">警告：此操作不可逆！</div>' +
      '<div>将连带删除所选会话的 <span class="mono">.pb</span> 摘要条目、对话数据库 (<span class="mono">.db*</span>)、脑区产物与关联子任务。</div>',
      '<button class="btn" data-act="cancel">取消</button><button class="btn btn-danger" data-act="go">确认永久删除</button>',
      function (_mb, mf) {
        mf.querySelector('[data-act="cancel"]').addEventListener('click', closeModal);
        mf.querySelector('[data-act="go"]').addEventListener('click', function () {
          async function doDel(closeIde) {
            try {
              var res = await apiFetch('/api/sessions/delete', {
                method: 'POST',
                body: JSON.stringify({ cids: cids, closeIdeIfRunning: closeIde })
              });
              closeModal();
              state.checkedSessions = {};
              await refreshAll('已删除 ' + res.deletedSessions + ' 条会话，释放 ' + res.freedMegabytes + ' MB');
            } catch (e) {
              if (e.status === 409 && e.data && e.data.code === 'IDE_RUNNING') {
                confirmCloseIdeModal(e.data.parentCount, e.data.lsCount, '物理删除选中会话', function () { doDel(true); });
              } else { showToast(e.message, 'error'); }
            }
          }
          doDel(false);
        });
      }
    );
  }

  function exportSelectiveAgyModal(cids, capKeys) {
    var includeCaps = false;
    openModal(
      '导出为轻量迁移包 (.agy)',
      '<div>已选会话：<strong>' + cids.length + '</strong> 条；已选能力：<strong>' + capKeys.length + '</strong> 项。</div>' +
      (capKeys.length === 0 ? '<label class="row gap-sm" style="margin-top:12px;cursor:pointer"><input type="checkbox" id="chkBundleAllCaps" /> 同时打包本机全部全局能力配置 (Skills / Rules / MCP / Hooks / Plugins)</label>' : ''),
      '<button class="btn" data-act="cancel">取消</button><button class="btn btn-primary" data-act="go">选择保存位置并导出...</button>',
      function (mb, mf) {
        var chk = mb.querySelector('#chkBundleAllCaps');
        if (chk) chk.addEventListener('change', function () { includeCaps = this.checked; });
        mf.querySelector('[data-act="cancel"]').addEventListener('click', closeModal);
        mf.querySelector('[data-act="go"]').addEventListener('click', function () {
          async function doExp(closeIde) {
            try {
              var res = await apiFetch('/api/backup/selective-export', {
                method: 'POST',
                body: JSON.stringify({ cids: cids, capKeys: capKeys, includeGlobalCaps: includeCaps, closeIdeIfRunning: closeIde })
              });
              closeModal();
              if (!res.cancelled) showToast('正在后台生成选择性备份包...', '');
            } catch (e) {
              if (e.status === 409 && e.data && e.data.code === 'IDE_RUNNING') {
                confirmCloseIdeModal(e.data.parentCount, e.data.lsCount, '导出选中会话至 .agy', function () { doExp(true); });
              } else { showToast(e.message, 'error'); }
            }
          }
          doExp(false);
        });
      }
    );
  }

  /* ══════════════════════════════════════════════════════
   * 3. SMART RENAME 3-STEP WORKBENCH
   * ══════════════════════════════════════════════════════ */
  function getFilteredRenameCandidates() {
    var rd = state.renameData;
    if (!rd || !rd.candidates) return [];
    var cands = rd.candidates;
    var out = [];
    for (var i = 0; i < cands.length; i++) {
      var c = cands[i];
      if (state.renameProjectFilter === '__SCOPED__') {
        if (!state.renameChecked[c.cid]) continue;
      } else {
        if (c.isSubagent) continue;
        if (!state.renameShowAux && (c.isCli || c.isOutside || c.isJunk)) continue;
        if (state.renameProjectFilter !== '__ALL__' && c.projectName !== state.renameProjectFilter) continue;
        if (!state.renameShowTagged && c.isTagged) continue;
        if (!state.renameShowRunning && c.isRunning) continue;
        if (!state.renameShowLowValue && c.isLowValue) continue;
      }
      out.push(c);
    }
    return out;
  }

  function getEffectiveNewTitle(c) {
    if (!c) return '';
    return state.renameEdits[c.cid] !== undefined ? state.renameEdits[c.cid] : c.suggestedTitle;
  }

  function renderRename() {
    var rd = state.renameData;
    if (!rd) {
      el('mainCanvas').innerHTML = '<div class="empty-state">正在加载重命名工作台...</div>';
      return;
    }

    var visible = getFilteredRenameCandidates();
    var selectedToApply = [];
    for (var i = 0; i < visible.length; i++) {
      if (state.renameChecked[visible[i].cid]) selectedToApply.push(visible[i]);
    }

    var projOpts = '<option value="__ALL__">全部正式工程</option>';
    if (state.renameProjectFilter === '__SCOPED__') projOpts += '<option value="__SCOPED__" selected>已带入的选中会话</option>';
    var projs = rd.formalProjects || [];
    for (var p = 0; p < projs.length; p++) {
      projOpts += '<option value="' + escapeHtml(projs[p]) + '"' + (state.renameProjectFilter === projs[p] ? ' selected' : '') + '>' + escapeHtml(projs[p]) + '</option>';
    }

    var stepperHtml =
      '<div class="rename-stepper-bar">' +
        '<div class="stepper">' +
          '<button class="step-btn ' + (state.renameStep === 1 ? 'active' : '') + '" data-rstep="1">① 筛选范围</button>' +
          '<span class="muted">→</span>' +
          '<button class="step-btn ' + (state.renameStep === 2 ? 'active' : '') + '" data-rstep="2">② 润色与精校 (' + selectedToApply.length + ')</button>' +
          '<span class="muted">→</span>' +
          '<button class="step-btn ' + (state.renameStep === 3 ? 'active' : '') + '" data-rstep="3">③ 确认写入</button>' +
        '</div>' +
        '<div class="row gap-sm">' +
          '<button class="btn btn-sm" id="btnOpenRulesDrawer">⚙ 命名规则</button>' +
          '<button class="btn btn-sm" id="btnOpenRollbackDrawer">↺ 回滚历史 (' + escapeHtml(rd.snapshotCount || 0) + ')</button>' +
        '</div>' +
      '</div>';

    var bodyHtml = '';
    if (state.renameStep === 3) {
      bodyHtml = renderRenameStep3(selectedToApply);
    } else {
      bodyHtml = renderRenameStep12(visible, selectedToApply, projOpts);
    }

    el('mainCanvas').innerHTML = '<div class="rename-shell">' + stepperHtml + bodyHtml + '</div>';
    bindRenameEvents(visible, selectedToApply);
  }

  function renderRenameStep12(visible, selectedToApply, projOpts) {
    var focused = null;
    for (var i = 0; i < visible.length; i++) {
      if (visible[i].cid === state.renameFocusedCid) { focused = visible[i]; break; }
    }
    if (!focused && visible.length > 0) { focused = visible[0]; state.renameFocusedCid = focused.cid; }

    var candHtml = [];
    if (visible.length === 0) {
      candHtml.push('<div class="empty-state">当前筛选下没有待重命名会话。可切换工程或开启「已带【】前缀 / 低价值水话」查看全部会话。</div>');
    } else {
      for (var k = 0; k < visible.length; k++) {
        var c = visible[k];
        var checked = !!state.renameChecked[c.cid];
        var effNew = getEffectiveNewTitle(c);
        var edited = state.renameEdits[c.cid] !== undefined && state.renameEdits[c.cid] !== c.suggestedTitle;
        var aiMod = !!state.renameAiModified[c.cid];
        var badge = c.isRunning
          ? '<span class="badge badge-amber">运行中</span>'
          : (c.isLowValue ? '<span class="badge badge-neutral">低价值未选</span>'
            : (aiMod ? '<span class="badge badge-green">AI 润色</span>'
              : (edited ? '<span class="badge badge-blue">已手改</span>' : '<span class="badge badge-accent">待写入</span>')));

        candHtml.push(
          '<div class="cand-card ' + (focused && focused.cid === c.cid ? 'focused' : '') + '" data-cand-focus="' + escapeHtml(c.cid) + '">' +
            '<input type="checkbox" class="ren-chk" data-cid="' + escapeHtml(c.cid) + '"' + (checked ? ' checked' : '') + ' style="margin-top:3px" />' +
            '<div class="cand-body">' +
              '<div class="cand-old">原：' + escapeHtml(c.oldTitle) + '</div>' +
              '<div class="cand-new">新：' + escapeHtml(effNew) + '</div>' +
              '<div class="cand-tags">' +
                '<span class="badge badge-neutral">' + escapeHtml(c.projectName) + '</span>' +
                badge +
                '<span class="muted" style="font-size:11px">' + escapeHtml(c.stepCount) + ' 步 · ' + escapeHtml(c.updatedTime) + '</span>' +
              '</div>' +
            '</div>' +
          '</div>'
        );
      }
    }

    var inspectorHtml = '<div class="empty-state">在左侧点击任意候选会话即可精校新标题</div>';
    if (focused) {
      var curEdit = getEffectiveNewTitle(focused);
      inspectorHtml =
        '<div class="between">' +
          '<div class="row gap-sm"><span class="badge badge-neutral">' + escapeHtml(focused.projectName) + '</span><span class="muted">' + escapeHtml(focused.stepCount) + ' 步 · ' + escapeHtml(focused.updatedTime) + '</span></div>' +
          '<button class="btn btn-xs" id="btnRestoreRuleTitle">恢复规则建议</button>' +
        '</div>' +
        '<div>' +
          '<div class="between" style="margin-bottom:5px"><label style="font-weight:600">拟定新标题（可直接修改）</label><span class="muted" id="lblNewTitleLen">' + curEdit.length + ' 字</span></div>' +
          '<textarea class="textarea" id="txtEditNewTitle" rows="3">' + escapeHtml(curEdit) + '</textarea>' +
          '<div class="row gap-xs wrap" style="margin-top:6px">' +
            '<button class="btn btn-xs" id="btnChipApplyPrefix">应用建议前缀: ' + escapeHtml(focused.suggestedPrefix) + '</button>' +
          '</div>' +
        '</div>' +
        '<div>' +
          '<div class="between" style="margin-bottom:5px"><label style="font-weight:600">首问内容摘要</label><button class="btn btn-xs btn-ghost" id="btnTogglePromptRaw">' + (state.renamePromptExpanded ? '查看清洗后' : '展开原始首问') + '</button></div>' +
          '<div class="prompt-card">' + escapeHtml(state.renamePromptExpanded ? (focused.firstPromptSnippet || '(暂无首问内容)') : (focused.cleanedPrompt || focused.firstPromptSnippet || '(暂无首问内容)')) + '</div>' +
        '</div>' +
        '<div class="accordion">' +
          '<button class="accordion-trigger" id="btnRenTechToggle">' + (state.renameTechExpanded ? '▾' : '▸') + ' 技术信息（路径 / 分支线索 / 运行状态）</button>' +
          '<div class="accordion-body mono ' + (state.renameTechExpanded ? '' : 'hidden') + '" id="renTechBody">' +
            '<div>工程目录: ' + escapeHtml(focused.fullPath || '-') + '</div>' +
            '<div>AST 分支线索: ' + escapeHtml(focused.gitBranchHint || '(无)') + '</div>' +
            '<div>状态: ' + escapeHtml(focused.runStatus || 'IDLE') + '</div>' +
            '<div>ID: ' + escapeHtml(focused.cid) + '</div>' +
          '</div>' +
        '</div>';
    }

    return '<div class="rename-split">' +
      '<div class="rename-left">' +
        '<div class="rename-filter-bar">' +
          '<div class="row gap-sm wrap">' +
            '<select class="select" id="selRenameProj" style="max-width:210px">' + projOpts + '</select>' +
            '<button class="filter-pill ' + (state.renameShowTagged ? 'active' : '') + '" data-rtoggle="tagged">已带【】</button>' +
            '<button class="filter-pill ' + (state.renameShowAux ? 'active' : '') + '" data-rtoggle="aux">CLI与项目外</button>' +
            '<button class="filter-pill ' + (state.renameShowRunning ? 'active' : '') + '" data-rtoggle="running">运行中</button>' +
            '<button class="filter-pill ' + (state.renameShowLowValue ? 'active' : '') + '" data-rtoggle="low">低价值水话</button>' +
          '</div>' +
          '<div class="between wrap gap-sm">' +
            '<div class="row gap-sm">' +
              '<button class="btn btn-xs" id="btnRenSelAll">全选当前</button>' +
              '<button class="btn btn-xs" id="btnRenSelNone">全不选</button>' +
              '<button class="btn btn-xs" id="btnRenRecompute">重算建议</button>' +
            '</div>' +
            '<div class="row gap-sm">' +
              '<button class="btn btn-sm" id="btnOpenAiDrawer">✨ AI 标题润色</button>' +
              '<button class="btn btn-sm btn-primary" id="btnNextToConfirm"' + (selectedToApply.length === 0 ? ' disabled' : '') + '>下一步：确认 (' + selectedToApply.length + ') →</button>' +
            '</div>' +
          '</div>' +
        '</div>' +
        '<div class="rename-list-scroll">' + candHtml.join('') + '</div>' +
      '</div>' +
      '<div class="rename-right">' +
        '<div class="pane-header"><span>选中项精校</span><span class="muted">显示 ' + visible.length + ' · 已选 ' + selectedToApply.length + '</span></div>' +
        '<div class="rename-inspector-scroll">' + inspectorHtml + '</div>' +
      '</div>' +
    '</div>';
  }

  function renderRenameStep3(selectedToApply) {
    var ide = (state.overview && state.overview.ide) || {};
    var rows = selectedToApply.map(function (c) {
      return '<tr>' +
        '<td style="width:130px"><span class="badge badge-neutral">' + escapeHtml(c.projectName) + '</span></td>' +
        '<td class="muted">' + escapeHtml(c.oldTitle) + '</td>' +
        '<td style="font-weight:600">' + escapeHtml(getEffectiveNewTitle(c)) + '</td>' +
      '</tr>';
    }).join('');

    var stratHtml = ide.running
      ? ('<div style="margin-top:14px">' +
           '<strong>检测到 Antigravity 正在运行（' + ((ide.parentCount || 0) + (ide.lsCount || 0)) + ' 进程），请选择写入模式：</strong>' +
           '<div class="strategy-cards">' +
             '<div class="strategy-card ' + (state.renameStrategy === 'CloseAndFullWrite' ? 'selected' : '') + '" data-rstrat="CloseAndFullWrite">' +
               '<strong>1. 关闭 IDE 并三层全写（推荐）</strong>' +
               '<div class="muted" style="font-size:12px;margin-top:4px">关闭进程后同步更新 .pbtxt / SQLite / .pb，重新打开立即显示新标题。</div>' +
             '</div>' +
             '<div class="strategy-card ' + (state.renameStrategy === 'HotWritePersistentLayers' ? 'selected' : '') + '" data-rstrat="HotWritePersistentLayers">' +
               '<strong>2. 免关机热写持久化层</strong>' +
               '<div class="muted" style="font-size:12px;margin-top:4px">不关闭 IDE，写入 .pbtxt 与 SQLite（跳过正在运行的会话），重启 IDE 后生效。</div>' +
             '</div>' +
           '</div>' +
         '</div>')
      : '<div class="badge badge-green" style="margin-top:12px">IDE 当前未运行，将直接执行 .pbtxt / SQLite / .pb 三层全量原子写入并自动生成快照备份。</div>';

    return '<div class="card" style="flex:1;display:flex;flex-direction:column;min-height:0">' +
      '<div class="between" style="margin-bottom:10px">' +
        '<strong>即将重命名以下 ' + selectedToApply.length + ' 条会话（自动创建行级回滚快照）</strong>' +
        '<div class="row gap-sm">' +
          '<button class="btn" id="btnBackToStep2">← 返回精校</button>' +
          '<button class="btn btn-primary" id="btnExecRenameFinal"' + (selectedToApply.length === 0 ? ' disabled' : '') + '>执行三层写入</button>' +
        '</div>' +
      '</div>' +
      stratHtml +
      '<div class="matrix-scroll" style="margin-top:12px;border:1px solid var(--border);border-radius:8px">' +
        '<table class="data-table"><thead><tr><th>工程</th><th>原标题</th><th>将写入新标题</th></tr></thead><tbody>' +
          (rows || '<tr><td colspan="3" class="empty-state">尚未勾选任何会话</td></tr>') +
        '</tbody></table>' +
      '</div>' +
    '</div>';
  }

  function bindRenameEvents(visible, selectedToApply) {
    var root = el('mainCanvas');
    var stepBtns = root.querySelectorAll('[data-rstep]');
    for (var i = 0; i < stepBtns.length; i++) {
      stepBtns[i].addEventListener('click', function () {
        state.renameStep = parseInt(this.getAttribute('data-rstep'), 10);
        renderRename();
      });
    }
    el('btnOpenRulesDrawer').addEventListener('click', openRuleEditorDrawer);
    el('btnOpenRollbackDrawer').addEventListener('click', openRollbackHistoryDrawer);

    if (state.renameStep === 3) {
      var sCards = root.querySelectorAll('[data-rstrat]');
      for (var sc = 0; sc < sCards.length; sc++) {
        sCards[sc].addEventListener('click', function () {
          state.renameStrategy = this.getAttribute('data-rstrat');
          renderRename();
        });
      }
      el('btnBackToStep2').addEventListener('click', function () { state.renameStep = 2; renderRename(); });
      el('btnExecRenameFinal').addEventListener('click', async function () {
        var payload = selectedToApply.map(function (c) { return { cid: c.cid, newTitle: getEffectiveNewTitle(c) }; });
        try {
          var ide = (state.overview && state.overview.ide) || {};
          var strat = ide.running ? state.renameStrategy : 'CloseAndFullWrite';
          var res = await apiFetch('/api/rename/execute', {
            method: 'POST',
            body: JSON.stringify({ items: payload, strategy: strat })
          });
          state.renameEdits = {};
          state.renameAiModified = {};
          state.renameChecked = {};
          state.renameStep = 2;
          await refreshAll();
          showToast('已更新 ' + payload.length + ' 条会话，快照已保存', 'success');
          if (res.snapshotDir) {
            openModal('三层重命名执行完成', '<div>请求更新：<strong>' + payload.length + '</strong> 条<br>跳过运行中：<strong>' + (res.skippedRunning ? res.skippedRunning.length : 0) + '</strong> 条<br>快照备份：<span class="mono" style="font-size:12px">' + escapeHtml(res.snapshotDir) + '</span></div>',
              '<button class="btn" data-act="rb">查看/回滚快照</button><button class="btn btn-primary" data-act="ok">完成</button>',
              function (_b, mf) {
                mf.querySelector('[data-act="ok"]').addEventListener('click', closeModal);
                mf.querySelector('[data-act="rb"]').addEventListener('click', function () { closeModal(); openRollbackHistoryDrawer(); });
              });
          }
        } catch (e) {
          if (e.status === 409 && e.data && e.data.code === 'IDE_RUNNING') {
            openDualModeRenameModal(e.data.parentCount, e.data.lsCount, function (chosen) {
              state.renameStrategy = chosen;
              el('btnExecRenameFinal').click();
            });
          } else { showToast(e.message, 'error'); }
        }
      });
      return;
    }

    el('selRenameProj').addEventListener('change', function () {
      state.renameProjectFilter = this.value;
      renderRename();
    });
    var rtoggles = root.querySelectorAll('[data-rtoggle]');
    for (var ti = 0; ti < rtoggles.length; ti++) {
      rtoggles[ti].addEventListener('click', function () {
        var k = this.getAttribute('data-rtoggle');
        if (k === 'tagged') state.renameShowTagged = !state.renameShowTagged;
        else if (k === 'aux') state.renameShowAux = !state.renameShowAux;
        else if (k === 'running') state.renameShowRunning = !state.renameShowRunning;
        else if (k === 'low') state.renameShowLowValue = !state.renameShowLowValue;
        renderRename();
      });
    }
    el('btnRenSelAll').addEventListener('click', function () {
      for (var j = 0; j < visible.length; j++) state.renameChecked[visible[j].cid] = true;
      renderRename();
    });
    el('btnRenSelNone').addEventListener('click', function () {
      for (var j = 0; j < visible.length; j++) state.renameChecked[visible[j].cid] = false;
      renderRename();
    });
    el('btnRenRecompute').addEventListener('click', async function () {
      state.renameEdits = {};
      state.renameAiModified = {};
      var rd = await apiFetch('/api/rename/candidates');
      applyFreshRenameData(rd);
      renderRename();
      showToast('已按最新规则重算建议标题', 'success');
    });
    el('btnNextToConfirm').addEventListener('click', function () { state.renameStep = 3; renderRename(); });
    el('btnOpenAiDrawer').addEventListener('click', function () { openAiPolishDrawer(visible, selectedToApply); });

    var chks = root.querySelectorAll('.ren-chk');
    for (var ci = 0; ci < chks.length; ci++) {
      chks[ci].addEventListener('click', function (ev) { ev.stopPropagation(); });
      chks[ci].addEventListener('change', function () {
        state.renameChecked[this.getAttribute('data-cid')] = this.checked;
        renderRename();
      });
    }
    var cards = root.querySelectorAll('[data-cand-focus]');
    for (var fi = 0; fi < cards.length; fi++) {
      cards[fi].addEventListener('click', function () {
        state.renameFocusedCid = this.getAttribute('data-cand-focus');
        renderRename();
      });
    }

    var focused = visible.filter(function (x) { return x.cid === state.renameFocusedCid; })[0];
    if (focused) {
      var ta = el('txtEditNewTitle');
      ta.addEventListener('input', function () {
        state.renameEdits[focused.cid] = this.value;
        el('lblNewTitleLen').textContent = this.value.length + ' 字';
      });
      ta.addEventListener('blur', function () { renderRename(); });
      el('btnRestoreRuleTitle').addEventListener('click', function () {
        delete state.renameEdits[focused.cid];
        delete state.renameAiModified[focused.cid];
        renderRename();
      });
      el('btnChipApplyPrefix').addEventListener('click', function () {
        var cur = getEffectiveNewTitle(focused);
        var stripped = cur.replace(/^【[^】]{1,34}】\s*/, '');
        state.renameEdits[focused.cid] = (focused.suggestedPrefix || '') + stripped;
        renderRename();
      });
      el('btnTogglePromptRaw').addEventListener('click', function () {
        state.renamePromptExpanded = !state.renamePromptExpanded;
        renderRename();
      });
      el('btnRenTechToggle').addEventListener('click', function () {
        state.renameTechExpanded = !state.renameTechExpanded;
        renderRename();
      });
    }
  }

  async function openAiPolishDrawer(visible, selectedToApply) {
    var cids = selectedToApply.map(function (c) { return c.cid; });
    if (cids.length === 0) { showToast('请先勾选至少 1 条会话', 'error'); return; }

    var promptRes = await apiFetch('/api/rename/ai-prompt', { method: 'POST', body: JSON.stringify({ cids: cids }) });
    var activeTab = 'clipboard';
    var parsedPreview = null;

    function renderDrawerContent() {
      var tabHtml =
        '<div class="pill-group" style="margin-bottom:14px">' +
          '<button class="filter-pill ' + (activeTab === 'clipboard' ? 'active' : '') + '" data-aitab="clipboard">剪贴板协同（推荐）</button>' +
          '<button class="filter-pill ' + (activeTab === 'file' ? 'active' : '') + '" data-aitab="file">JSON 文件交换（高级）</button>' +
        '</div>';

      var prevTable = '';
      if (parsedPreview && parsedPreview.changes) {
        prevTable =
          '<div style="margin-top:10px;border:1px solid var(--border);border-radius:8px;max-height:220px;overflow:auto">' +
            '<table class="data-table"><thead><tr><th>原标题</th><th>AI 润色新标题</th></tr></thead><tbody>' +
            parsedPreview.changes.map(function (ch) {
              return '<tr><td class="muted">' + escapeHtml(ch.oldTitle) + '</td><td style="font-weight:600">' + escapeHtml(ch.newTitle) + '</td></tr>';
            }).join('') +
            '</tbody></table>' +
          '</div>';
      }

      if (activeTab === 'clipboard') {
        return tabHtml +
          '<div class="card" style="margin-bottom:12px">' +
            '<div class="between"><strong>Step 1：复制待润色提示词（已包含 ' + promptRes.count + ' 条首问）</strong><button class="btn btn-sm btn-primary" id="btnCopyAiPrompt">复制提示词</button></div>' +
            '<textarea class="textarea mono" rows="6" readonly style="margin-top:8px;font-size:11.5px">' + escapeHtml(promptRes.prompt) + '</textarea>' +
          '</div>' +
          '<div class="card">' +
            '<strong>Step 2：粘贴大模型返回的 JSON 并校验应用</strong>' +
            '<textarea class="textarea mono" id="txtAiPaste" rows="5" placeholder=\'粘贴诸如 {"items":[{"id":"...","new_title":"..."}]} 或 [{"id":"...","new_title":"..."}] 的 JSON...\' style="margin-top:8px;font-size:12px"></textarea>' +
            '<div class="row gap-sm" style="margin-top:10px">' +
              '<button class="btn btn-sm" id="btnParseAiJson">校验并预览差异</button>' +
              '<button class="btn btn-sm btn-primary" id="btnApplyAiChanges"' + (parsedPreview ? '' : ' disabled') + '>应用到候选列表 (' + (parsedPreview ? parsedPreview.matchedCount : 0) + ')</button>' +
            '</div>' +
            prevTable +
          '</div>';
      }

      return tabHtml +
        '<div class="card">' +
          '<div style="margin-bottom:10px">支持将提示词保存为 <span class="mono">.txt</span> 交给外部批处理/大模型处理，再直接导入结果 JSON 文件回填候选标题。</div>' +
          '<div class="row gap-sm">' +
            '<button class="btn btn-sm" id="btnSaveAiPromptTxt">保存提示词为 .txt...</button>' +
            '<button class="btn btn-sm" id="btnLoadAiJsonFile">导入 AI 结果 JSON...</button>' +
            '<button class="btn btn-sm btn-primary" id="btnApplyAiChanges"' + (parsedPreview ? '' : ' disabled') + '>应用到候选列表 (' + (parsedPreview ? parsedPreview.matchedCount : 0) + ')</button>' +
          '</div>' +
          (parsedPreview && parsedPreview.inputPath ? '<div class="muted mono" style="margin-top:8px;font-size:11.5px">已载入：' + escapeHtml(parsedPreview.inputPath) + '</div>' : '') +
          prevTable +
        '</div>';
    }

    openDrawer('AI 标题润色助手', '两步快速借助任意大模型生成精准工程语义标题', renderDrawerContent(), '', bindAiDrawer);

    function bindAiDrawer(db) {
      var tabs = db.querySelectorAll('[data-aitab]');
      for (var i = 0; i < tabs.length; i++) {
        tabs[i].addEventListener('click', function () {
          activeTab = this.getAttribute('data-aitab');
          db.innerHTML = renderDrawerContent();
          bindAiDrawer(db);
        });
      }
      var btnCopy = db.querySelector('#btnCopyAiPrompt');
      if (btnCopy) btnCopy.addEventListener('click', function () { copyToClipboard(promptRes.prompt, 'AI 提示词已复制 ✓'); });
      var btnParse = db.querySelector('#btnParseAiJson');
      if (btnParse) btnParse.addEventListener('click', async function () {
        var raw = db.querySelector('#txtAiPaste').value;
        try {
          parsedPreview = await apiFetch('/api/rename/ai-parse', { method: 'POST', body: JSON.stringify({ rawText: raw, cids: cids }) });
          var keptText = raw;
          db.innerHTML = renderDrawerContent();
          db.querySelector('#txtAiPaste').value = keptText;
          bindAiDrawer(db);
          showToast('解析成功：命中 ' + parsedPreview.matchedCount + ' 条', 'success');
        } catch (e) { showToast(e.message, 'error'); }
      });
      var btnApply = db.querySelector('#btnApplyAiChanges');
      if (btnApply) btnApply.addEventListener('click', function () {
        if (!parsedPreview || !parsedPreview.changes) return;
        for (var k = 0; k < parsedPreview.changes.length; k++) {
          var ch = parsedPreview.changes[k];
          state.renameEdits[ch.cid] = ch.newTitle;
          state.renameAiModified[ch.cid] = true;
          state.renameChecked[ch.cid] = true;
        }
        closeDrawer(true);
        renderRename();
        showToast('已将 ' + parsedPreview.changes.length + ' 条 AI 标题应用至待改清单', 'success');
      });
      var btnSave = db.querySelector('#btnSaveAiPromptTxt');
      if (btnSave) btnSave.addEventListener('click', async function () {
        try {
          var res = await apiFetch('/api/rename/ai-save-prompt', { method: 'POST', body: JSON.stringify({ cids: cids }) });
          if (!res.cancelled && res.outputPath) showToast('已保存提示词（' + res.count + ' 条）：' + res.outputPath, 'success');
        } catch (e) { showToast(e.message, 'error'); }
      });
      var btnLoad = db.querySelector('#btnLoadAiJsonFile');
      if (btnLoad) btnLoad.addEventListener('click', async function () {
        try {
          var res = await apiFetch('/api/rename/ai-load-json', { method: 'POST', body: JSON.stringify({ cids: cids }) });
          if (res.cancelled) return;
          parsedPreview = res;
          db.innerHTML = renderDrawerContent();
          bindAiDrawer(db);
          showToast('解析成功：命中 ' + parsedPreview.matchedCount + ' 条', 'success');
        } catch (e) { showToast(e.message, 'error'); }
      });
    }
  }

  async function openRuleEditorDrawer() {
    var data = await apiFetch('/api/rename/rules');
    var mode = 'form';
    var rulesObj = JSON.parse(JSON.stringify(data.rulesObj || {}));
    var rawJsonText = data.rawJson || '';
    var dirty = false;
    var sandboxHtml = '';

    function renderRulesInner() {
      var modeSwitch =
        '<div class="between" style="margin-bottom:14px">' +
          '<div class="pill-group">' +
            '<button class="filter-pill ' + (mode === 'form' ? 'active' : '') + '" data-rmode="form">可视化规则表单</button>' +
            '<button class="filter-pill ' + (mode === 'json' ? 'active' : '') + '" data-rmode="json">原始 JSON 源码</button>' +
          '</div>' +
          '<button class="btn btn-sm" id="btnRunRuleSandbox">▶ 沙盒试跑规则</button>' +
        '</div>';

      var mainHtml = '';
      if (mode === 'form') {
        var aliasKeys = Object.keys(rulesObj.projectAliases || {});
        var aliasRows = aliasKeys.map(function (k, idx) {
          return '<tr>' +
            '<td><input class="input alias-k" data-idx="' + idx + '" value="' + escapeHtml(k) + '" /></td>' +
            '<td><input class="input alias-v" data-idx="' + idx + '" value="' + escapeHtml(rulesObj.projectAliases[k]) + '" /></td>' +
            '<td style="width:60px"><button class="btn btn-xs btn-danger-outline alias-del" data-idx="' + idx + '">删</button></td>' +
          '</tr>';
        }).join('');

        var multiCards = (rulesObj.multiRepoRules || []).map(function (mr, mIdx) {
          var matchRows = (mr.modelMatchers || []).map(function (mm, kIdx) {
            return '<tr>' +
              '<td style="width:140px"><input class="input mm-model" data-midx="' + mIdx + '" data-kidx="' + kIdx + '" value="' + escapeHtml(mm.model) + '" /></td>' +
              '<td><input class="input mm-kw" data-midx="' + mIdx + '" data-kidx="' + kIdx + '" value="' + escapeHtml((mm.keywords || []).join(', ')) + '" /></td>' +
              '<td style="width:52px"><button class="btn btn-xs btn-danger-outline mm-del" data-midx="' + mIdx + '" data-kidx="' + kIdx + '">删</button></td>' +
            '</tr>';
          }).join('');

          return '<div class="card" style="margin-top:12px">' +
            '<div class="row gap-md" style="margin-bottom:10px">' +
              '<div style="flex:1"><label style="font-size:12px;font-weight:600">多仓工程名</label><input class="input mr-proj" data-midx="' + mIdx + '" value="' + escapeHtml(mr.projectName) + '" /></div>' +
              '<div style="flex:1"><label style="font-size:12px;font-weight:600">未命中时默认机型标签</label><input class="input mr-def" data-midx="' + mIdx + '" value="' + escapeHtml(mr.defaultModel) + '" /></div>' +
            '</div>' +
            '<label style="font-size:12px;font-weight:600;display:block;margin-bottom:4px">分支提取正则（每行一条）</label>' +
            '<textarea class="textarea mono mr-regex" data-midx="' + mIdx + '" rows="3" style="font-size:11.5px">' + escapeHtml((mr.branchRegexes || []).join('\n')) + '</textarea>' +
            '<div class="between" style="margin:10px 0 6px 0"><strong style="font-size:12px">机型关键词优先匹配列表（从上到下优先）</strong><button class="btn btn-xs mm-add" data-midx="' + mIdx + '">+ 新增机型</button></div>' +
            '<table class="data-table"><thead><tr><th>机型标签</th><th>关键词（英文逗号分隔）</th><th></th></tr></thead><tbody>' + matchRows + '</tbody></table>' +
          '</div>';
        }).join('');

        mainHtml =
          '<div class="card">' +
            '<label class="row gap-sm" style="cursor:pointer;font-weight:600">' +
              '<input type="checkbox" id="chkRuleKeepPrefix"' + (rulesObj.keepExistingBracketedPrefix ? ' checked' : '') + ' />' +
              ' 保留已有行首【...】前缀（勾选后将跳过已含前缀的旧标题）' +
            '</label>' +
          '</div>' +
          '<div class="card" style="margin-top:12px">' +
            '<div class="between" style="margin-bottom:8px"><strong>单工程简称映射 (ProjectAliases)</strong><button class="btn btn-xs" id="btnAddAliasRow">+ 新增映射</button></div>' +
            '<table class="data-table"><thead><tr><th>原工程目录名</th><th>前缀简称</th><th></th></tr></thead><tbody>' + aliasRows + '</tbody></table>' +
          '</div>' + multiCards;
      } else {
        mainHtml =
          '<div class="card">' +
            '<div class="muted" style="margin-bottom:6px;font-size:12px">规则文件：' + escapeHtml(data.rulesPath) + '</div>' +
            '<textarea class="textarea mono" id="txtRawRulesJson" rows="20" style="font-size:12px">' + escapeHtml(rawJsonText) + '</textarea>' +
          '</div>';
      }

      return modeSwitch + mainHtml + sandboxHtml;
    }

    var hdrBtns = '<button class="btn btn-sm btn-primary" id="btnSaveRulesNow">保存并应用规则</button>';
    openDrawer('会话自动命名规则编辑器', '支持可视化增删工程简称、调整多仓机型优先级，并实时针对本机会话沙盒试跑', renderRulesInner(), hdrBtns, bindRuleEvents, function () { return dirty; });

    function collectFormIntoObj() {
      if (mode !== 'form') return;
      var db = el('drawerBody');
      rulesObj.keepExistingBracketedPrefix = !!db.querySelector('#chkRuleKeepPrefix').checked;
      var newAliases = {};
      var kInputs = db.querySelectorAll('.alias-k');
      var vInputs = db.querySelectorAll('.alias-v');
      for (var i = 0; i < kInputs.length; i++) {
        var k = kInputs[i].value.trim();
        var v = vInputs[i].value.trim();
        if (k) newAliases[k] = v;
      }
      rulesObj.projectAliases = newAliases;

      var projs = db.querySelectorAll('.mr-proj');
      for (var m = 0; m < projs.length; m++) {
        var mr = rulesObj.multiRepoRules[m];
        mr.projectName = projs[m].value.trim();
        mr.defaultModel = db.querySelectorAll('.mr-def')[m].value.trim();
        var rxLines = db.querySelectorAll('.mr-regex')[m].value.split(/\r?\n/).map(function (s) { return s.trim(); }).filter(Boolean);
        mr.branchRegexes = rxLines;
      }
      var mmModels = db.querySelectorAll('.mm-model');
      var mmKws = db.querySelectorAll('.mm-kw');
      for (var x = 0; x < mmModels.length; x++) {
        var mi = parseInt(mmModels[x].getAttribute('data-midx'), 10);
        var ki = parseInt(mmModels[x].getAttribute('data-kidx'), 10);
        rulesObj.multiRepoRules[mi].modelMatchers[ki].model = mmModels[x].value.trim();
        rulesObj.multiRepoRules[mi].modelMatchers[ki].keywords = mmKws[x].value.split(',').map(function (s) { return s.trim(); }).filter(Boolean);
      }
    }

    function bindRuleEvents(db, ha) {
      var modeBtns = db.querySelectorAll('[data-rmode]');
      for (var i = 0; i < modeBtns.length; i++) {
        modeBtns[i].addEventListener('click', function () {
          var nextMode = this.getAttribute('data-rmode');
          if (nextMode === mode) return;
          if (mode === 'form') {
            collectFormIntoObj();
            rawJsonText = JSON.stringify(rulesObj, null, 2);
          } else {
            rawJsonText = db.querySelector('#txtRawRulesJson').value;
            try { rulesObj = JSON.parse(rawJsonText); } catch (e) { showToast('JSON 语法错误，无法切回表单：' + e.message, 'error'); return; }
          }
          mode = nextMode;
          db.innerHTML = renderRulesInner();
          bindRuleEvents(db, ha);
        });
      }

      var inputs = db.querySelectorAll('input, textarea');
      for (var inIdx = 0; inIdx < inputs.length; inIdx++) {
        inputs[inIdx].addEventListener('input', function () { dirty = true; });
      }

      if (db.querySelector('#btnAddAliasRow')) {
        db.querySelector('#btnAddAliasRow').addEventListener('click', function () {
          collectFormIntoObj();
          rulesObj.projectAliases['NewProject_' + (Object.keys(rulesObj.projectAliases).length + 1)] = 'Alias';
          dirty = true;
          db.innerHTML = renderRulesInner();
          bindRuleEvents(db, ha);
        });
      }

      var delBtns = db.querySelectorAll('.alias-del');
      for (var d = 0; d < delBtns.length; d++) {
        delBtns[d].addEventListener('click', function () {
          collectFormIntoObj();
          var keys = Object.keys(rulesObj.projectAliases);
          delete rulesObj.projectAliases[keys[parseInt(this.getAttribute('data-idx'), 10)]];
          dirty = true;
          db.innerHTML = renderRulesInner();
          bindRuleEvents(db, ha);
        });
      }

      var mmAddBtns = db.querySelectorAll('.mm-add');
      for (var ma = 0; ma < mmAddBtns.length; ma++) {
        mmAddBtns[ma].addEventListener('click', function () {
          collectFormIntoObj();
          var mi = parseInt(this.getAttribute('data-midx'), 10);
          rulesObj.multiRepoRules[mi].modelMatchers.push({ model: 'NewModel', keywords: ['kw1'] });
          dirty = true;
          db.innerHTML = renderRulesInner();
          bindRuleEvents(db, ha);
        });
      }

      var mmDelBtns = db.querySelectorAll('.mm-del');
      for (var md = 0; md < mmDelBtns.length; md++) {
        mmDelBtns[md].addEventListener('click', function () {
          collectFormIntoObj();
          var mi = parseInt(this.getAttribute('data-midx'), 10);
          var ki = parseInt(this.getAttribute('data-kidx'), 10);
          rulesObj.multiRepoRules[mi].modelMatchers.splice(ki, 1);
          dirty = true;
          db.innerHTML = renderRulesInner();
          bindRuleEvents(db, ha);
        });
      }

      db.querySelector('#btnRunRuleSandbox').addEventListener('click', async function () {
        try {
          var payload;
          if (mode === 'form') { collectFormIntoObj(); payload = { rulesObj: rulesObj, sampleLimit: 18 }; }
          else { rawJsonText = db.querySelector('#txtRawRulesJson').value; payload = { rawJson: rawJsonText, sampleLimit: 18 }; }
          var testRes = await apiFetch('/api/rename/rules/test', { method: 'POST', body: JSON.stringify(payload) });
          sandboxHtml = '<div class="card" style="margin-top:12px"><strong>沙盒试跑效果预览（前 ' + (testRes.samples || []).length + ' 条）</strong>' +
            '<table class="data-table" style="margin-top:8px"><thead><tr><th>原标题</th><th>合成预览</th></tr></thead><tbody>' +
            (testRes.samples || []).map(function (s) {
              return '<tr><td class="muted">' + escapeHtml(s.oldTitle) + '</td><td style="font-weight:600">' + escapeHtml(s.suggestedTitle) + (s.isLowValue ? ' <span class="badge badge-neutral">低价值</span>' : '') + '</td></tr>';
            }).join('') +
            '</tbody></table></div>';
          db.innerHTML = renderRulesInner();
          bindRuleEvents(db, ha);
        } catch (e) { showToast('规则校验失败：' + e.message, 'error'); }
      });

      var saveBtn = ha.querySelector('#btnSaveRulesNow');
      if (saveBtn) {
        saveBtn.onclick = async function () {
          try {
            var payload;
            if (mode === 'form') { collectFormIntoObj(); payload = { rulesObj: rulesObj }; }
            else { rawJsonText = db.querySelector('#txtRawRulesJson').value; payload = { rawJson: rawJsonText }; }
            var saved = await apiFetch('/api/rename/rules', { method: 'POST', body: JSON.stringify(payload) });
            dirty = false;
            state.drawerDirtyCheck = null;
            rulesObj = saved.rulesObj;
            rawJsonText = saved.rawJson;
            var rd = await apiFetch('/api/rename/candidates');
            applyFreshRenameData(rd);
            if (state.view === 'rename') renderRename();
            showToast('命名规则已保存并应用 ✓', 'success');
          } catch (e) { showToast('保存规则失败：' + e.message, 'error'); }
        };
      }
    }
  }

  async function openRollbackHistoryDrawer() {
    var res = await apiFetch('/api/rename/history');
    var snaps = res.snapshots || [];

    var body = snaps.length === 0
      ? '<div class="empty-state">暂无重命名历史快照。每次执行重命名均会自动保存逐条回滚快照（最多保留 15 份）。</div>'
      : snaps.map(function (sp, idx) {
          var itemRows = (sp.items || []).map(function (it) {
            return '<tr><td class="muted">' + escapeHtml(it.oldTitle) + '</td><td>' + escapeHtml(it.newTitle) + '</td></tr>';
          }).join('');
          return '<div class="card" style="margin-bottom:12px">' +
            '<div class="between">' +
              '<div><strong>' + escapeHtml(sp.createdAtIso || sp.folderName) + '</strong> <span class="badge badge-neutral">' + sp.itemCount + ' 条</span>' +
              (sp.hasPbBackup ? ' <span class="badge badge-green">含 .pb 备份</span>' : '') + '</div>' +
              '<button class="btn btn-sm btn-primary btn-rb-snap" data-idx="' + idx + '">还原此快照</button>' +
            '</div>' +
            '<div class="muted" style="margin:4px 0 8px 0;font-size:12px">' + escapeHtml(sp.previewSummary) + '</div>' +
            '<div style="max-height:150px;overflow:auto;border:1px solid var(--border);border-radius:6px">' +
              '<table class="data-table"><thead><tr><th>还原回原标题</th><th>重命名后标题</th></tr></thead><tbody>' + itemRows + '</tbody></table>' +
            '</div>' +
          '</div>';
        }).join('');

    openDrawer('三层重命名快照与回滚历史', '支持从行级快照精准恢复 .pbtxt / SQLite / .pb，保留回滚后新建的其他会话', body, '', function (db) {
      var btns = db.querySelectorAll('.btn-rb-snap');
      for (var i = 0; i < btns.length; i++) {
        btns[i].addEventListener('click', function () {
          var sp = snaps[parseInt(this.getAttribute('data-idx'), 10)];
          executeSnapshotRollback(sp.snapshotDir, 'auto');
        });
      }
    });
  }

  async function executeSnapshotRollback(snapshotDir, strategy) {
    try {
      await apiFetch('/api/rename/rollback', { method: 'POST', body: JSON.stringify({ snapshotDir: snapshotDir, strategy: strategy }) });
      closeDrawer(true);
      await refreshAll('已从快照还原会话标题 ✓');
    } catch (e) {
      if (e.status === 409 && e.data && e.data.code === 'IDE_RUNNING') {
        openDualModeRenameModal(e.data.parentCount, e.data.lsCount, function (chosen) {
          executeSnapshotRollback(snapshotDir, chosen);
        });
      } else { showToast(e.message, 'error'); }
    }
  }

  /* ══════════════════════════════════════════════════════
   * 4. CAPABILITIES MATRIX VIEW
   * ══════════════════════════════════════════════════════ */
  function renderCapabilities() {
    var cd = state.capData;
    if (!cd) { el('mainCanvas').innerHTML = '<div class="empty-state">正在扫描能力仓库...</div>'; return; }

    var all = cd.items || [];
    var filtered = all.filter(function (it) {
      if (state.capCategory !== 'ALL' && it.kind !== state.capCategory) return false;
      if (state.capDirFilter === 'DIFF' && it.direction === 'Aligned') return false;
      else if (state.capDirFilter === 'REMOTE_NEW' && it.direction !== 'RemoteNewer' && it.direction !== 'RemoteOnly') return false;
      else if (state.capDirFilter === 'LOCAL_NEW' && it.direction !== 'LocalNewer' && it.direction !== 'LocalOnly') return false;
      else if (state.capDirFilter === 'MISSING' && !it.hasMissingLocalPaths && !it.hasMissingRemotePaths) return false;
      if (state.capSearch.trim()) {
        var q = state.capSearch.trim().toLowerCase();
        if (it.displayName.toLowerCase().indexOf(q) < 0 && it.description.toLowerCase().indexOf(q) < 0) return false;
      }
      return true;
    });

    function dirBadgeHtml(it) {
      var tip = it.directionReason === 'baseline' ? '基于上次对齐指纹三向推断' : (it.directionReason === 'mtime' ? '基于文件 UTC 修改时间推断（受 Git/共享配置影响）' : '内容指纹比较');
      var cls = 'badge-neutral';
      if (it.direction === 'Aligned') cls = 'badge-green';
      else if (it.direction === 'RemoteNewer' || it.direction === 'RemoteOnly') cls = 'badge-blue';
      else if (it.direction === 'LocalNewer' || it.direction === 'LocalOnly') cls = 'badge-amber';
      else if (it.direction === 'BothModified') cls = 'badge-accent';
      var warn = (it.hasMissingLocalPaths || it.hasMissingRemotePaths) ? ' <span class="badge badge-red">⚠ 路径缺失</span>' : '';
      return '<span class="badge ' + cls + '" title="' + escapeHtml(tip) + '">' + escapeHtml(it.directionBadge) + '</span>' + warn;
    }

    var rowsHtml = filtered.length === 0
      ? '<tr><td colspan="6" class="empty-state">当前筛选条件下无能力配置项。</td></tr>'
      : filtered.map(function (it) {
          var checked = !!state.capChecked[it.key];
          return '<tr>' +
            '<td style="width:36px"><input type="checkbox" class="cap-chk" data-key="' + escapeHtml(it.key) + '"' + (checked ? ' checked' : '') + ' /></td>' +
            '<td><div class="row gap-sm"><span class="badge badge-neutral">' + escapeHtml(it.kind) + '</span><strong>' + escapeHtml(it.displayName) + '</strong></div>' +
              (it.description ? '<div class="muted" style="font-size:11.5px;margin-top:2px;max-width:420px;overflow:hidden;text-overflow:ellipsis;white-space:nowrap">' + escapeHtml(it.description) + '</div>' : '') + '</td>' +
            '<td>' + dirBadgeHtml(it) + '</td>' +
            '<td class="mono muted cap-time-cell">' + (it.localExists ? escapeHtml(it.localTime) : '—') + '</td>' +
            '<td class="mono muted cap-time-cell">' + (it.remoteExists ? escapeHtml(it.remoteTime) : '—') + '</td>' +
            '<td style="white-space:nowrap"><div class="row gap-xs">' +
              '<button class="btn btn-xs" data-cap-act="diff" data-key="' + escapeHtml(it.key) + '">对比源码</button>' +
              (it.remoteExists ? '<button class="btn btn-xs" data-cap-act="import" data-key="' + escapeHtml(it.key) + '" title="从仓库同步到本机">← 本地</button>' : '') +
              (it.localExists ? '<button class="btn btn-xs" data-cap-act="push" data-key="' + escapeHtml(it.key) + '" title="推送本机配置到仓库">存仓 →</button>' : '') +
              (it.hasMissingLocalPaths ? '<button class="btn btn-xs btn-danger-outline" data-cap-act="fixpath" data-key="' + escapeHtml(it.key) + '">修路径</button>' : '') +
            '</div></td>' +
          '</tr>';
        }).join('');

    var selKeys = Object.keys(state.capChecked).filter(function (k) { return state.capChecked[k]; });
    var cats = ['ALL', 'Plugin', 'Skill', 'Rule', 'MCP', 'Hook'];
    var catNames = { ALL: '全部', Plugin: '插件包', Skill: '技能', Rule: '规则', MCP: 'MCP', Hook: 'Hooks' };
    var dirs = [
      ['ALL', '全部状态'],
      ['DIFF', '仅看有差异'],
      ['REMOTE_NEW', '仓库新增/较新'],
      ['LOCAL_NEW', '本机新增/较新'],
      ['MISSING', '路径缺失']
    ];

    el('mainCanvas').innerHTML =
      '<div class="matrix-card">' +
        '<div class="matrix-toolbar">' +
          '<div class="row gap-sm wrap">' +
            '<div class="pill-group">' + cats.map(function (c) {
              return '<button class="filter-pill ' + (state.capCategory === c ? 'active' : '') + '" data-ccat="' + c + '">' + catNames[c] + '</button>';
            }).join('') + '</div>' +
            '<span class="pill-divider"></span>' +
            '<div class="pill-group">' + dirs.map(function (d) {
              return '<button class="filter-pill ' + (state.capDirFilter === d[0] ? 'active' : '') + '" data-cdir="' + d[0] + '">' + d[1] + '</button>';
            }).join('') + '</div>' +
          '</div>' +
          '<div class="row gap-sm wrap">' +
            '<input class="input" id="txtCapSearch" style="width:190px" placeholder="筛选能力名称..." value="' + escapeHtml(state.capSearch) + '" />' +
            '<button class="btn btn-sm" id="btnCapSafeSelect">仅选仓库新增/较新</button>' +
            '<button class="btn btn-sm" id="btnCapPushSel"' + (selKeys.length === 0 ? ' disabled' : '') + '>存入仓库 (' + selKeys.length + ')</button>' +
            '<button class="btn btn-sm btn-primary" id="btnCapImportSel"' + (selKeys.length === 0 ? ' disabled' : '') + '>导入本机 (' + selKeys.length + ')</button>' +
            '<button class="btn btn-sm btn-danger-outline" id="btnCapDeleteMenu"' + (selKeys.length === 0 ? ' disabled' : '') + '>更多删除...</button>' +
          '</div>' +
        '</div>' +
        '<div class="matrix-scroll">' +
          '<table class="data-table"><thead><tr><th></th><th>能力配置</th><th>双向同步状态</th><th>本机修改 (本地时间)</th><th>仓库版本</th><th>操作</th></tr></thead><tbody>' + rowsHtml + '</tbody></table>' +
        '</div>' +
      '</div>';

    var root = el('mainCanvas');
    var catBtns = root.querySelectorAll('[data-ccat]');
    for (var i = 0; i < catBtns.length; i++) {
      catBtns[i].addEventListener('click', function () { state.capCategory = this.getAttribute('data-ccat'); renderCapabilities(); });
    }
    var dirBtns = root.querySelectorAll('[data-cdir]');
    for (var j = 0; j < dirBtns.length; j++) {
      dirBtns[j].addEventListener('click', function () { state.capDirFilter = this.getAttribute('data-cdir'); renderCapabilities(); });
    }
    el('txtCapSearch').addEventListener('input', function () {
      state.capSearch = this.value;
      renderCapabilities();
      var inp = el('txtCapSearch');
      inp.focus(); inp.setSelectionRange(inp.value.length, inp.value.length);
    });
    el('btnCapSafeSelect').addEventListener('click', function () {
      state.capChecked = {};
      for (var x = 0; x < all.length; x++) {
        if (all[x].direction === 'RemoteOnly' || all[x].direction === 'RemoteNewer')
          state.capChecked[all[x].key] = true;
      }
      renderCapabilities();
    });
    el('btnCapPushSel').addEventListener('click', function () { executeCapAction('/api/capabilities/push', { keys: selKeys }, '已写入内置仓库 ✓'); });
    el('btnCapImportSel').addEventListener('click', function () { executeCapAction('/api/capabilities/import', { keys: selKeys, source: 'repo' }, '已导入至本机 ✓'); });
    el('btnCapDeleteMenu').addEventListener('click', function () { openCapDeleteModal(selKeys); });

    var chks = root.querySelectorAll('.cap-chk');
    for (var ci = 0; ci < chks.length; ci++) {
      chks[ci].addEventListener('change', function () {
        state.capChecked[this.getAttribute('data-key')] = this.checked;
        renderCapabilities();
      });
    }
    var actBtns = root.querySelectorAll('[data-cap-act]');
    for (var ai = 0; ai < actBtns.length; ai++) {
      actBtns[ai].addEventListener('click', function () {
        var act = this.getAttribute('data-cap-act');
        var k = this.getAttribute('data-key');
        if (act === 'diff' || act === 'fixpath') openCapabilityDiffDrawer(k, 'repo');
        else if (act === 'import') executeCapAction('/api/capabilities/import', { keys: [k], source: 'repo' }, '已同步到本机 ✓');
        else if (act === 'push') executeCapAction('/api/capabilities/push', { keys: [k] }, '已存入内置仓库 ✓');
      });
    }
  }

  async function executeCapAction(endpoint, payload, msg) {
    try {
      await apiFetch(endpoint, { method: 'POST', body: JSON.stringify(payload) });
      state.capChecked = {};
      await refreshAll(msg);
    } catch (e) { showToast(e.message, 'error'); }
  }

  function openCapDeleteModal(keys) {
    openModal(
      '删除选中的 ' + keys.length + ' 项能力',
      '<div>请选择删除作用域（仅删仓库快照不会影响本机已配置能力）：</div>',
      '<button class="btn" data-act="cancel">取消</button>' +
      '<button class="btn btn-danger-outline" data-act="del-repo">仅从仓库移除</button>' +
      '<button class="btn btn-danger" data-act="del-local">删除本机配置</button>',
      function (_mb, mf) {
        mf.querySelector('[data-act="cancel"]').addEventListener('click', closeModal);
        mf.querySelector('[data-act="del-repo"]').addEventListener('click', function () {
          closeModal(); executeCapAction('/api/capabilities/delete-repo', { keys: keys }, '已从内置仓库移除 ✓');
        });
        mf.querySelector('[data-act="del-local"]').addEventListener('click', function () {
          closeModal(); executeCapAction('/api/capabilities/delete-local', { keys: keys }, '已删除本机能力配置 ✓');
        });
      }
    );
  }

  async function openCapabilityDiffDrawer(key, source) {
    var data = await apiFetch('/api/capabilities/diff?key=' + encodeURIComponent(key) + '&source=' + encodeURIComponent(source || 'repo'));
    var files = data.files || [];
    var activeFileIdx = 0;

    function buildDiffBody() {
      var warnHtml = '';
      if (data.missingLocalPaths && data.missingLocalPaths.length > 0) {
        var oldSel = data.missingLocalPaths[0];
        warnHtml =
          '<div class="card" style="margin-bottom:12px;border-color:rgba(220,38,38,0.4);background:var(--red-soft)">' +
            '<div style="font-weight:600;color:var(--red-text);margin-bottom:6px">⚠ 检测到本机不存在的路径：' + escapeHtml(oldSel) + '</div>' +
            '<div class="row gap-sm wrap">' +
              '<input class="input" id="txtCapRelocNew" placeholder="输入或浏览本机新的脚本/目录路径..." style="flex:1;min-width:220px" />' +
              '<button class="btn btn-sm" id="btnCapRelocBrowseFile">选文件</button>' +
              '<button class="btn btn-sm" id="btnCapRelocBrowseDir">选目录</button>' +
              '<button class="btn btn-sm btn-primary" id="btnCapRelocSave">立即修复</button>' +
            '</div>' +
          '</div>';
      }

      var fileTabs = files.map(function (f, i) {
        var badge = f.status === 'equal' ? '' : (' · ' + f.status);
        return '<button class="filter-pill ' + (i === activeFileIdx ? 'active' : '') + '" data-dfidx="' + i + '">' + escapeHtml(f.path + badge) + '</button>';
      }).join('');

      var fObj = files[activeFileIdx];
      var codeHtml = '<div class="empty-state">无可对比文本文件</div>';
      if (fObj) {
        if (fObj.isBinary) {
          codeHtml = '<div class="empty-state">二进制文件，请直接同步整体资产。</div>';
        } else {
          codeHtml = (fObj.truncated ? '<div class="badge badge-amber" style="margin-bottom:8px">文件较大，部分中间行已截断</div>' : '') +
            '<div style="border:1px solid var(--border);border-radius:8px;overflow:auto">' +
              '<table class="diff-table"><tbody>' +
                (fObj.lines || []).map(function (ln) {
                  var rCls = ln.type === 'delete' ? 'diff-row-delete' : (ln.type === 'insert' ? 'diff-row-insert' : '');
                  var sign = ln.type === 'delete' ? '-' : (ln.type === 'insert' ? '+' : ' ');
                  return '<tr class="' + rCls + '">' +
                    '<td class="diff-ln">' + (ln.oldLine > 0 ? ln.oldLine : '') + '</td>' +
                    '<td class="diff-ln">' + (ln.newLine > 0 ? ln.newLine : '') + '</td>' +
                    '<td class="diff-sign">' + sign + '</td>' +
                    '<td>' + escapeHtml(ln.text) + '</td>' +
                  '</tr>';
                }).join('') +
              '</tbody></table>' +
            '</div>';
        }
      }

      return warnHtml + '<div class="pill-group" style="margin-bottom:12px">' + fileTabs + '</div>' + codeHtml;
    }

    var hdrBtns =
      (data.remoteExists ? '<button class="btn btn-sm btn-primary" id="btnDiffAdoptRemote">← 同步到本机</button>' : '') +
      (data.localExists && source !== 'backup' ? '<button class="btn btn-sm" id="btnDiffPushLocal">存入仓库 →</button>' : '');

    openDrawer('源码差异对比 — [' + data.kind + '] ' + data.id,
      '左侧为本机环境 (-)，右侧为' + (source === 'backup' ? '备份归档 (+)' : '内置仓库 (+)') + ' · ' + data.directionBadge,
      buildDiffBody(), hdrBtns, bindDiffEvents);

    function bindDiffEvents(db, ha) {
      var dfBtns = db.querySelectorAll('[data-dfidx]');
      for (var i = 0; i < dfBtns.length; i++) {
        dfBtns[i].addEventListener('click', function () {
          activeFileIdx = parseInt(this.getAttribute('data-dfidx'), 10);
          db.innerHTML = buildDiffBody();
          bindDiffEvents(db, ha);
        });
      }
      var btnFile = db.querySelector('#btnCapRelocBrowseFile');
      if (btnFile) btnFile.addEventListener('click', async function () {
        var pick = await apiFetch('/api/native/pick-open-file', { method: 'POST', body: JSON.stringify({ title: '选择可执行脚本/文件' }) });
        if (!pick.cancelled && pick.path) db.querySelector('#txtCapRelocNew').value = pick.path.replace(/\\/g, '/');
      });
      var btnDir = db.querySelector('#btnCapRelocBrowseDir');
      if (btnDir) btnDir.addEventListener('click', async function () {
        var pick = await apiFetch('/api/native/pick-folder', { method: 'POST', body: JSON.stringify({ title: '选择本地工具目录' }) });
        if (!pick.cancelled && pick.path) db.querySelector('#txtCapRelocNew').value = pick.path.replace(/\\/g, '/');
      });
      var btnFix = db.querySelector('#btnCapRelocSave');
      if (btnFix) btnFix.addEventListener('click', async function () {
        var newP = db.querySelector('#txtCapRelocNew').value.trim();
        if (!newP) { showToast('请输入新路径', 'error'); return; }
        await apiFetch('/api/capabilities/relocate', {
          method: 'POST',
          body: JSON.stringify({ key: key, scope: 'local', oldPath: data.missingLocalPaths[0], newPath: newP })
        });
        await refreshAll('路径已重定位 ✓');
        openCapabilityDiffDrawer(key, source);
      });
      var btnAdopt = ha.querySelector('#btnDiffAdoptRemote');
      if (btnAdopt) btnAdopt.onclick = function () {
        closeDrawer(true);
        executeCapAction('/api/capabilities/import', { keys: [key], source: source || 'repo' }, '已采用此版本到本地 ✓');
      };
      var btnPush = ha.querySelector('#btnDiffPushLocal');
      if (btnPush) btnPush.onclick = function () {
        closeDrawer(true);
        executeCapAction('/api/capabilities/push', { keys: [key] }, '已存入内置仓库 ✓');
      };
    }
  }

  /* ══════════════════════════════════════════════════════
   * 5. BACKUP & MIGRATION VIEW
   * ══════════════════════════════════════════════════════ */
  function renderBackup() {
    var bd = state.backupData || { loaded: false };
    var hero =
      '<div class="action-grid">' +
        '<div class="quick-card">' +
          '<div>' +
            '<div class="quick-card-title">一键全量备份本机环境 (.agy)</div>' +
            '<div class="quick-card-desc">完整归档所有工程、会话数据库、脑区、标题标注、能力配置与最新规则。</div>' +
          '</div>' +
          '<button class="btn btn-primary" id="btnTriggerFullBk">立即全量备份...</button>' +
        '</div>' +
        '<div class="quick-card">' +
          '<div>' +
            '<div class="quick-card-title">打开历史 / 跨机备份包 (.agy)</div>' +
            '<div class="quick-card-desc">免解包流式预览归档内会话与能力源码，按需选择性无损合并至当前电脑。</div>' +
          '</div>' +
          '<button class="btn" id="btnOpenAgyFile">选择 .agy 文件...</button>' +
        '</div>' +
      '</div>';

    var archiveSection = '';
    if (!bd.loaded) {
      archiveSection = '<div class="card empty-state">尚未打开任何 .agy 备份包。点击右上「选择 .agy 文件...」即可安全审阅其内容。</div>';
    } else {
      var capList = bd.capabilityDiffs || [];
      var projGroups = bd.sessionDiffGroups || [];
      var selCapsCount = Object.keys(state.backupCheckedCaps).filter(function (k) { return state.backupCheckedCaps[k]; }).length;
      var selConvsCount = Object.keys(state.backupCheckedConvs).filter(function (k) { return state.backupCheckedConvs[k]; }).length;

      var innerTable = '';
      if (state.backupTab === 'caps') {
        innerTable = '<table class="data-table"><thead><tr><th></th><th>能力项</th><th>差异方向</th><th> 操作</th></tr></thead><tbody>' +
          (capList.map(function (c) {
            var chk = !!state.backupCheckedCaps[c.key];
            return '<tr>' +
              '<td style="width:36px"><input type="checkbox" class="bk-cap-chk" data-key="' + escapeHtml(c.key) + '"' + (chk ? ' checked' : '') + ' /></td>' +
              '<td><span class="badge badge-neutral">' + escapeHtml(c.kind) + '</span> <strong>' + escapeHtml(c.displayName) + '</strong></td>' +
              '<td><span class="badge badge-accent">' + escapeHtml(c.directionBadge) + '</span></td>' +
              '<td><button class="btn btn-xs" data-bk-diff="' + escapeHtml(c.key) + '">对比源码</button></td>' +
            '</tr>';
          }).join('') || '<tr><td colspan="4" class="empty-state">归档中不含自定义能力配置</td></tr>') +
          '</tbody></table>';
      } else {
        var sRows = [];
        for (var pi = 0; pi < projGroups.length; pi++) {
          var pg = projGroups[pi];
          var sessList = pg.sessions || [];
          for (var si = 0; si < sessList.length; si++) {
            var s = sessList[si];
            var chkS = !!state.backupCheckedConvs[s.cid];
            sRows.push(
              '<tr>' +
                '<td style="width:36px"><input type="checkbox" class="bk-conv-chk" data-cid="' + escapeHtml(s.cid) + '"' + (chkS ? ' checked' : '') + ' /></td>' +
                '<td><span class="badge badge-neutral">' + escapeHtml(s.projectName) + '</span> ' + (s.isSubagent ? '<span class="badge badge-neutral">子任务</span> ' : '') + '<strong>' + escapeHtml(s.title) + '</strong></td>' +
                '<td>' + escapeHtml(s.stepCount) + ' 步</td>' +
                '<td><span class="badge ' + (s.status === 'New' || s.status === 'Newer' ? 'badge-blue' : 'badge-neutral') + '">' + escapeHtml(s.status) + '</span></td>' +
                '<td class="muted">' + escapeHtml(s.updatedTime) + '</td>' +
                '<td><button class="btn btn-xs" data-bk-md="' + escapeHtml(s.cid) + '">免导入预览</button></td>' +
              '</tr>'
            );
          }
        }
        innerTable = '<table class="data-table"><thead><tr><th></th><th>工程与会话标题</th><th>步数</th><th>相对本机</th><th>更新时间</th><th>操作</th></tr></thead><tbody>' +
          (sRows.join('') || '<tr><td colspan="6" class="empty-state">归档中不含会话记录</td></tr>') + '</tbody></table>';
      }

      archiveSection =
        '<div class="card" style="display:flex;flex-direction:column;min-height:0">' +
          '<div class="between wrap gap-sm" style="margin-bottom:12px">' +
            '<div>' +
              '<strong>' + escapeHtml(bd.fileName) + '</strong>' +
              '<div class="muted mono" style="font-size:11.5px">来源用户: ' + escapeHtml(bd.sourceUser || '-') + ' · 时间: ' + escapeHtml(bd.backupTime || '-') + ' · 体积: ' + bd.sizeMegabytes + ' MB</div>' +
            '</div>' +
            '<div class="row gap-sm">' +
              '<button class="btn btn-sm" id="btnBkAutoPick">仅选新增/较新</button>' +
              '<button class="btn btn-sm" id="btnBkClearPick">清空</button>' +
              '<button class="btn btn-sm btn-primary" id="btnBkImportExec"' + (selCapsCount + selConvsCount === 0 ? ' disabled' : '') + '>← 导入所选到本地 (' + selCapsCount + ' 能力 + ' + selConvsCount + ' 会话)</button>' +
            '</div>' +
          '</div>' +
          '<div class="pill-group" style="margin-bottom:10px">' +
            '<button class="filter-pill ' + (state.backupTab === 'caps' ? 'active' : '') + '" data-bktab="caps">能力差异 (' + capList.length + ')</button>' +
            '<button class="filter-pill ' + (state.backupTab === 'sessions' ? 'active' : '') + '" data-bktab="sessions">工程与会话 (' + (bd.conversationsCount || 0) + ')</button>' +
          '</div>' +
          '<div style="max-height:420px;overflow:auto;border:1px solid var(--border);border-radius:8px">' + innerTable + '</div>' +
        '</div>';
    }

    el('mainCanvas').innerHTML = hero + archiveSection;
    el('btnTriggerFullBk').addEventListener('click', triggerFullBackup);
    el('btnOpenAgyFile').addEventListener('click', openBackupArchiveDialog);

    if (bd.loaded) {
      var tabs = el('mainCanvas').querySelectorAll('[data-bktab]');
      for (var i = 0; i < tabs.length; i++) {
        tabs[i].addEventListener('click', function () { state.backupTab = this.getAttribute('data-bktab'); renderBackup(); });
      }
      el('btnBkAutoPick').addEventListener('click', function () { preselectRecommendedBackupItems(bd); renderBackup(); });
      el('btnBkClearPick').addEventListener('click', function () { state.backupCheckedCaps = {}; state.backupCheckedConvs = {}; renderBackup(); });
      el('btnBkImportExec').addEventListener('click', function () { triggerBackupImport(false); });

      var cChks = el('mainCanvas').querySelectorAll('.bk-cap-chk');
      for (var ci = 0; ci < cChks.length; ci++) {
        cChks[ci].addEventListener('change', function () { state.backupCheckedCaps[this.getAttribute('data-key')] = this.checked; renderBackup(); });
      }
      var sChks = el('mainCanvas').querySelectorAll('.bk-conv-chk');
      for (var si = 0; si < sChks.length; si++) {
        sChks[si].addEventListener('change', function () { state.backupCheckedConvs[this.getAttribute('data-cid')] = this.checked; renderBackup(); });
      }
      var dBtns = el('mainCanvas').querySelectorAll('[data-bk-diff]');
      for (var di = 0; di < dBtns.length; di++) {
        dBtns[di].addEventListener('click', function () { openCapabilityDiffDrawer(this.getAttribute('data-bk-diff'), 'backup'); });
      }
      var mBtns = el('mainCanvas').querySelectorAll('[data-bk-md]');
      for (var mi = 0; mi < mBtns.length; mi++) {
        mBtns[mi].addEventListener('click', function () { openMarkdownDrawer(this.getAttribute('data-bk-md'), 'backup'); });
      }
    }
  }

  function preselectRecommendedBackupItems(bd) {
    state.backupCheckedCaps = {};
    state.backupCheckedConvs = {};
    var caps = bd.capabilityDiffs || [];
    for (var i = 0; i < caps.length; i++) {
      if (caps[i].direction === 'RemoteOnly' || caps[i].direction === 'RemoteNewer')
        state.backupCheckedCaps[caps[i].key] = true;
    }
    var pgs = bd.sessionDiffGroups || [];
    for (var p = 0; p < pgs.length; p++) {
      var list = pgs[p].sessions || [];
      for (var s = 0; s < list.length; s++) {
        if (list[s].autoSelected) state.backupCheckedConvs[list[s].cid] = true;
      }
    }
  }

  async function triggerFullBackup() {
    async function run(closeIde) {
      try {
        var res = await apiFetch('/api/backup/full', { method: 'POST', body: JSON.stringify({ closeIdeIfRunning: closeIde }) });
        if (!res.cancelled) showToast('正在后台打包全量 .agy 备份...', '');
      } catch (e) {
        if (e.status === 409 && e.data && e.data.code === 'IDE_RUNNING') {
          confirmCloseIdeModal(e.data.parentCount, e.data.lsCount, '全量备份本机环境', function () { run(true); });
        } else { showToast(e.message, 'error'); }
      }
    }
    run(false);
  }

  async function openBackupArchiveDialog() {
    try {
      var res = await apiFetch('/api/backup/open', { method: 'POST', body: '{}' });
      if (res.cancelled) return;
      state.backupData = res;
      preselectRecommendedBackupItems(res);
      renderBackup();
      showToast('已解析归档包：' + res.fileName, 'success');
    } catch (e) { showToast('解析备份包失败：' + e.message, 'error'); }
  }

  async function triggerBackupImport(closeIde) {
    var cids = Object.keys(state.backupCheckedConvs).filter(function (k) { return state.backupCheckedConvs[k]; });
    var capKeys = Object.keys(state.backupCheckedCaps).filter(function (k) { return state.backupCheckedCaps[k]; });
    try {
      await apiFetch('/api/backup/import', {
        method: 'POST',
        body: JSON.stringify({ cids: cids, capKeys: capKeys, closeIdeIfRunning: closeIde })
      });
      showToast('已提交选择性合流任务，请观察进度条...', '');
    } catch (e) {
      if (e.status === 409 && e.data && e.data.code === 'IDE_RUNNING') {
        confirmCloseIdeModal(e.data.parentCount, e.data.lsCount, '从归档包选择性导入', function () { triggerBackupImport(true); });
      } else { showToast(e.message, 'error'); }
    }
  }

  /* ══════════════════════════════════════════════════════
   * BOOTSTRAP & SSE WIREUP
   * ══════════════════════════════════════════════════════ */
  async function boot() {
    applyTheme(state.theme);
    injectStaticIcons();

    var params = new URLSearchParams(window.location.search);
    var urlToken = params.get('t');
    if (urlToken) {
      state.token = urlToken;
      sessionStorage.setItem('agy_tok', urlToken);
      window.history.replaceState(null, '', window.location.pathname);
    } else {
      state.token = sessionStorage.getItem('agy_tok') || '';
    }

    if (!state.token) {
      try {
        var auth = await fetch('/api/auth/session', { credentials: 'same-origin' });
        if (auth.ok) {
          var authJson = await auth.json();
          if (authJson && authJson.token) {
            state.token = authJson.token;
            sessionStorage.setItem('agy_tok', state.token);
          }
        }
      } catch (_e) {}
    }

    // Bind persistent shell chrome
    var navBtns = document.querySelectorAll('#navList .nav-item');
    for (var i = 0; i < navBtns.length; i++) {
      navBtns[i].addEventListener('click', function () { switchView(this.getAttribute('data-view')); });
    }
    el('btnToggleTheme').addEventListener('click', function () { applyTheme(state.theme === 'dark' ? 'light' : 'dark'); });
    el('btnGlobalRefresh').addEventListener('click', async function () {
      await apiFetch('/api/refresh', { method: 'POST', body: '{}' });
      await refreshAll('已刷新本机与仓库数据 ✓');
    });
    el('btnQuickCloseIde').addEventListener('click', function () {
      var ide = (state.overview && state.overview.ide) || {};
      confirmCloseIdeModal(ide.parentCount || 1, ide.lsCount || 0, '安全关闭 Antigravity IDE', async function () {
        try {
          await apiFetch('/api/ide/close', { method: 'POST', body: '{}' });
          await refreshAll('Antigravity IDE 已关闭 ✓');
        } catch (e) { showToast(e.message, 'error'); }
      });
    });

    el('btnToggleLogs').addEventListener('click', function () {
      state.logsOpen = !state.logsOpen;
      el('logDrawer').classList.toggle('hidden', !state.logsOpen);
      el('activityChevron').textContent = state.logsOpen ? '收起日志 ▾' : '展开日志 ▴';
    });
    el('btnCloseLogs').addEventListener('click', function () {
      state.logsOpen = false;
      el('logDrawer').classList.add('hidden');
      el('activityChevron').textContent = '展开日志 ▴';
    });
    el('btnCopyLogs').addEventListener('click', function () {
      var logs = (state.overview && state.overview.logs) || [];
      copyToClipboard(logs.map(function (l) { return '[' + l.time + '] ' + l.message; }).join('\n'), '日志已复制 ✓');
    });

    el('btnCloseDrawer').addEventListener('click', function () { closeDrawer(false); });
    el('drawerBackdrop').addEventListener('click', function (ev) { if (ev.target === this) closeDrawer(false); });
    el('btnCloseModal').addEventListener('click', closeModal);
    el('modalBackdrop').addEventListener('click', function (ev) { if (ev.target === this) closeModal(); });
    document.addEventListener('keydown', function (ev) {
      if (ev.key === 'Escape') {
        if (!el('modalBackdrop').classList.contains('hidden')) closeModal();
        else if (!el('drawerBackdrop').classList.contains('hidden')) closeDrawer(false);
        else if (state.logsOpen) el('btnCloseLogs').click();
      }
    });

    // SSE & Heartbeat
    try {
      var es = new EventSource('/api/events');
      es.addEventListener('progress', function (ev) {
        try {
          var payload = JSON.parse(ev.data).payload;
          el('jobBanner').classList.remove('hidden');
          el('jobBannerText').textContent = payload.message || '执行中...';
          el('jobProgressFill').style.width = (payload.percent || 25) + '%';
        } catch (_e) {}
      });
      es.addEventListener('log', function (ev) {
        try {
          var item = JSON.parse(ev.data).payload;
          if (!state.overview) state.overview = { logs: [] };
          if (!state.overview.logs) state.overview.logs = [];
          state.overview.logs.push(item);
          updateShellBars();
        } catch (_e) {}
      });
      es.addEventListener('job_done', async function (ev) {
        el('jobBanner').classList.add('hidden');
        try {
          var d = JSON.parse(ev.data).payload;
          if (d.ok) showToast('任务完成 ✓', 'success');
          else showToast('任务失败：' + (d.error || '未知错误'), 'error');
        } catch (_e) {}
        await refreshAll();
      });
    } catch (_e) {}

    setInterval(function () {
      if (state.token) apiFetch('/api/app/heartbeat', { method: 'POST', body: '{}' }).catch(function () {});
    }, 2500);

    await refreshAll();
  }

  boot().catch(function (err) {
    el('mainCanvas').innerHTML = '<div class="card empty-state"><strong>初始化失败</strong><div class="muted" style="margin-top:8px">' + escapeHtml(err.message) + '</div></div>';
  });
})();
