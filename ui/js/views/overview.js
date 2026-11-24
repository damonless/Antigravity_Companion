(function () {
  'use strict';

  var AGY = window.AGY = window.AGY || {};
  AGY.views = AGY.views || {};

  function renderOverview() {
    var el = AGY.dom.el;
    var esc = AGY.ui.escapeHtml;
    var st = AGY.state;
    var ov = st.overview;

    if (!ov) {
      el('mainCanvas').innerHTML = '<div class="empty-state">正在加载环境体检数据...</div>';
      return;
    }

    var c = ov.counts || {};
    var cs = c.capabilitySummary || {};
    var gc = ov.lastOrphanScan;
    var paths = ov.paths || {};
    var cp = st.cockpitData || {};
    var tb = cp.tokenBudget || {};
    var diffTotal = (cs.remoteOnly || 0) + (cs.remoteNewer || 0) + (cs.localNewer || 0) + (cs.bothModified || 0) + (cs.conflict || 0);

    var localMiss = cs.localMissingPaths || 0;
    var vaultMiss = cs.vaultMissingPaths || 0;
    var actionableUntagged = c.untaggedActionableCount !== undefined ? c.untaggedActionableCount : (c.untaggedFormalSessions || 0);
    var lockedUntagged = c.untaggedLockedActiveCount || 0;
    var lockedSessions = ov.lockedSessions || [];

    var pathDescHtml = (localMiss > 0 || vaultMiss > 0)
      ? ('<div class="row gap-xs wrap">' +
           (localMiss > 0 ? '<span class="badge badge-red">本机缺失 ' + localMiss + '</span>' : '<span class="badge badge-green">本机正常</span>') +
           (vaultMiss > 0 ? '<span class="badge badge-amber">Vault 缺失 ' + vaultMiss + '</span>' : '<span class="badge badge-neutral">Vault 正常</span>') +
         '</div>')
      : '<span class="badge badge-green">本机与 Vault 路径均正常</span>';

    var gcHtml = gc
      ? ('<div class="metric-value">' + esc(gc.totalMegabytes) + ' <span style="font-size:14px;font-weight:500">MB</span></div>' +
         '<div class="metric-desc">数据库 ' + gc.dbFilesCount + ' · 脑区 ' + gc.brainDirsCount + ' · 标注 ' + gc.annotationFilesCount + '</div>')
      : '<div class="metric-value" style="font-size:18px">待体检</div><div class="metric-desc">支持 IDE 运行时只读零风险扫描</div>';

    // Build unified project launch cards from sessionsData + cockpitData.workspaces
    var wsById = {};
    var wsByPath = {};
    var workspaces = cp.workspaces || [];
    for (var wi = 0; wi < workspaces.length; wi++) {
      var w = workspaces[wi];
      if (w.projectId) wsById[w.projectId.toLowerCase()] = w;
      if (w.folderPath) wsByPath[w.folderPath.toLowerCase()] = w;
    }

    var formalGroups = [];
    var groups = (st.sessionsData && st.sessionsData.groups) || [];
    for (var gi = 0; gi < groups.length; gi++) {
      if (groups[gi].groupKind === 'formal') formalGroups.push(groups[gi]);
    }

    var projectCardsHtml = '';
    if (formalGroups.length === 0) {
      projectCardsHtml = '<div class="card empty-state" style="padding:20px">暂无已登记的正式工程工作区。</div>';
    } else {
      projectCardsHtml = '<div class="project-cards-grid">' + formalGroups.map(function (fg) {
        var ws = (fg.projectId && wsById[fg.projectId.toLowerCase()]) || (fg.fullPath && wsByPath[fg.fullPath.toLowerCase()]) || null;
        var hasAgents = ws && ws.hasAgentsDir;
        var hasRules = ws && (ws.hasGeminiMd || ws.hasAgentsMd);
        var folderExists = ws ? ws.exists : !!fg.fullPath;
        var subLabel = fg.isDuplicateBasename && fg.distinguishingLabel ? fg.distinguishingLabel : fg.shortPath;

        return '<div class="proj-launch-card">' +
          '<div class="between gap-xs">' +
            '<div class="proj-launch-title" title="' + esc(fg.projectName) + '">' + esc(fg.projectName) + '</div>' +
            '<span class="badge badge-accent">' + esc(fg.primaryCount) + ' 会话</span>' +
          '</div>' +
          '<div class="proj-launch-path" title="' + esc(fg.fullPath || '') + '">' + esc(subLabel || fg.fullPath || '(无物理目录)') + '</div>' +
          '<div class="row gap-xs wrap">' +
            (fg.isMultiRoot ? '<span class="badge badge-blue">多目录</span>' : '') +
            (hasAgents ? '<span class="badge badge-green">' + esc(ws.agentsDirName || '.agents') + '</span>' : '') +
            (hasRules ? '<span class="badge badge-neutral">规则文件</span>' : '') +
            (!folderExists && fg.fullPath ? '<span class="badge badge-red">目录不存在</span>' : '') +
          '</div>' +
          '<div class="proj-launch-actions">' +
            '<button class="btn btn-xs btn-primary" data-ov-launch="ide" data-path="' + esc(fg.fullPath || '') + '"' + (!fg.fullPath ? ' disabled' : '') + '>IDE 打开</button>' +
            '<button class="btn btn-xs" data-ov-launch="explorer" data-path="' + esc(fg.fullPath || '') + '"' + (!fg.fullPath ? ' disabled' : '') + '>打开目录</button>' +
            '<button class="btn btn-xs" data-ov-launch="powershell" data-path="' + esc(fg.fullPath || '') + '"' + (!fg.fullPath ? ' disabled' : '') + '>开终端</button>' +
            '<button class="btn btn-xs" data-ov-sessions="' + esc(fg.bucketKey) + '">看会话</button>' +
          '</div>' +
        '</div>';
      }).join('') + '</div>';
    }

    var lockedAlertHtml = '';
    if (lockedSessions.length > 0 || lockedUntagged > 0) {
      lockedAlertHtml =
        '<div class="alert-banner between wrap gap-sm">' +
          '<div>🔒 检测到 <strong>' + lockedSessions.length + '</strong> 条运行中 / 未完全空闲会话（其中未标前缀 <strong>' + lockedUntagged + '</strong> 条），并发安全门禁已自动锁定，避免破坏进行中的子代理或对话。</div>' +
          '<button class="btn btn-xs" id="btnOverviewInspectLocked">查看锁定明细</button>' +
        '</div>';
    }

    var html =
      '<div class="overview-grid">' +
        '<div class="metric-card">' +
          '<div class="metric-label">本机与 Vault 能力健康</div>' +
          '<div class="metric-value">' + esc(c.localCapabilitiesCount || 0) + ' <span class="muted" style="font-size:13px;font-weight:500">/ Vault ' + esc(c.repoCapabilitiesCount || 0) + '</span></div>' +
          '<div class="metric-desc">' + pathDescHtml + '</div>' +
        '</div>' +
        '<div class="metric-card">' +
          '<div class="metric-label">正式工程与主会话</div>' +
          '<div class="metric-value">' + esc(c.formalPrimarySessions || 0) + '</div>' +
          '<div class="metric-desc">' + esc(c.formalProjectsCount || 0) + ' 个正式工程 · CLI ' + (c.cliSessions || 0) + ' · 项目外 ' + (c.outsideSessions || 0) + ' · 子任务 ' + (c.subagentSessions || 0) + '</div>' +
        '</div>' +
        '<div class="metric-card">' +
          '<div class="metric-label">未标前缀会话（可整理 vs 锁定）</div>' +
          '<div class="metric-value">' + esc(actionableUntagged) + ' <span class="muted" style="font-size:13px;font-weight:500">可改 / ' + esc(lockedUntagged) + ' 锁定</span></div>' +
          '<div class="metric-desc">待同步能力差异：' + esc(diffTotal) + ' 项 · 常驻参考约 ' + esc(tb.totalAlwaysOnTokens || 0) + ' tokens</div>' +
        '</div>' +
        '<div class="metric-card">' +
          '<div class="metric-label">孤儿残留体检</div>' + gcHtml +
        '</div>' +
      '</div>' +

      lockedAlertHtml +

      '<div class="section-header">' +
        '<strong>已登记工程快速启动台 (' + formalGroups.length + ')</strong>' +
        '<div class="row gap-sm">' +
          '<button class="btn btn-sm" id="btnOverviewGcScan">孤儿只读体检</button>' +
          (gc && gc.totalItems > 0 ? '<button class="btn btn-sm btn-danger-outline" id="btnOverviewGcClean">清理残留 (' + esc(gc.totalMegabytes) + ' MB)</button>' : '') +
          '<button class="btn btn-sm btn-primary" data-jump="cockpit">进入每日驾驶舱 →</button>' +
        '</div>' +
      '</div>' +
      projectCardsHtml +

      '<div class="action-grid">' +
        '<div class="quick-card">' +
          '<div>' +
            '<div class="quick-card-title">会话浏览与三步智能重命名</div>' +
            '<div class="quick-card-desc">还有 <strong>' + esc(actionableUntagged) + '</strong> 条可安全重命名会话，支持本地规则、Zero-Key <span class="mono">agy</span> CLI 与离线 AI 润色。</div>' +
          '</div>' +
          '<button class="btn btn-primary" data-jump-rename="1">整理会话 →</button>' +
        '</div>' +
        '<div class="quick-card">' +
          '<div>' +
            '<div class="quick-card-title">存储诊断、冷归档与孤儿清理</div>' +
            '<div class="quick-card-desc">可视化拆解占用体积、清理孤儿垃圾与安全冷归档子代理，保持环境整洁轻快。</div>' +
          '</div>' +
          '<button class="btn" data-jump="storage">存储体检 →</button>' +
        '</div>' +
      '</div>' +

      '<div class="card">' +
        '<div class="between wrap gap-sm" style="margin-bottom:8px">' +
          '<strong>工作环境与 Portable Vault 路径</strong>' +
          '<div class="row gap-sm">' +
            '<button class="btn btn-sm" data-jump="vault">能力与规则</button>' +
            '<button class="btn btn-sm" data-jump="backup">存储与备份</button>' +
            '<button class="btn btn-sm" data-jump="settings">⚙ 系统设置</button>' +
          '</div>' +
        '</div>' +
        '<div class="muted mono" style="font-size:12px;line-height:1.8">' +
          '<div>.gemini 目录：' + esc(paths.gemini || '') + '</div>' +
          '<div>Portable Vault：' + esc(paths.vault || paths.repository || '') + '</div>' +
        '</div>' +
      '</div>';

    var root = el('mainCanvas');
    root.innerHTML = html;

    var jumpBtns = root.querySelectorAll('[data-jump]');
    for (var i = 0; i < jumpBtns.length; i++) {
      jumpBtns[i].addEventListener('click', function () {
        var target = this.getAttribute('data-jump');
        if (target === 'backup') st.backupSubView = 'backup';
        AGY.app.switchView(target);
      });
    }
    var jumpRen = root.querySelector('[data-jump-rename]');
    if (jumpRen) {
      jumpRen.addEventListener('click', function () {
        AGY.app.switchView('rename');
      });
    }

    var sessBtns = root.querySelectorAll('[data-ov-sessions]');
    for (var sb = 0; sb < sessBtns.length; sb++) {
      sessBtns[sb].addEventListener('click', function () {
        st.selectedBucketKey = this.getAttribute('data-ov-sessions');
        AGY.app.switchView('sessions');
      });
    }

    var launchBtns = root.querySelectorAll('[data-ov-launch]');
    for (var lb = 0; lb < launchBtns.length; lb++) {
      launchBtns[lb].addEventListener('click', async function () {
        var tool = this.getAttribute('data-ov-launch');
        var path = this.getAttribute('data-path');
        try {
          await AGY.api.apiFetch('/api/workspace/launch', {
            method: 'POST',
            body: JSON.stringify({ path: path, tool: tool })
          });
          AGY.api.showToast('已启动 ' + tool.toUpperCase() + ' 打开工作区 ✓', 'success');
        } catch (e) {
          AGY.api.showToast('启动失败：' + e.message, 'error');
        }
      });
    }

    if (el('btnOverviewInspectLocked')) {
      el('btnOverviewInspectLocked').addEventListener('click', function () {
        var rows = lockedSessions.map(function (s) {
          return '<tr><td><span class="badge badge-neutral">' + esc(s.projectName) + '</span></td>' +
            '<td>' + esc(s.title) + '</td>' +
            '<td><span class="badge badge-amber">' + esc(s.runStatus) + '</span></td></tr>';
        }).join('');
        AGY.ui.openModal(
          '当前被并发锁定的活跃会话 (' + lockedSessions.length + ')',
          '<div class="muted" style="margin-bottom:10px">这些会话处于 RUNNING 或 not_fully_idle=1 状态，重命名与瘦身将自动跳过以确保并发安全。</div>' +
          '<div style="max-height:320px;overflow:auto;border:1px solid var(--border);border-radius:8px">' +
            '<table class="data-table"><thead><tr><th>工程</th><th>标题</th><th>运行状态</th></tr></thead><tbody>' +
              (rows || '<tr><td colspan="3" class="empty-state">无锁定会话</td></tr>') +
            '</tbody></table>' +
          '</div>',
          '<button class="btn btn-primary" data-act="ok">知道了</button>',
          function (_b, mf) {
            mf.querySelector('[data-act="ok"]').addEventListener('click', AGY.ui.closeModal);
          }
        );
      });
    }

    el('btnOverviewGcScan').addEventListener('click', handleReadOnlyGcScan);
    var btnClean = el('btnOverviewGcClean');
    if (btnClean) btnClean.addEventListener('click', function () { promptAndExecuteGcClean(false); });
  }

  async function handleReadOnlyGcScan() {
    try {
      var res = await AGY.api.apiFetch('/api/gc/scan', { method: 'POST', body: '{}' });
      if (!AGY.state.overview) AGY.state.overview = {};
      AGY.state.overview.lastOrphanScan = res;
      if (AGY.state.view === 'overview') renderOverview();
      if (res.totalItems === 0) {
        AGY.api.showToast('环境非常干净，未发现任何孤儿残留数据。', 'success');
      } else {
        AGY.ui.openModal(
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
            mf.querySelector('[data-act="close"]').addEventListener('click', AGY.ui.closeModal);
            mf.querySelector('[data-act="clean"]').addEventListener('click', function () {
              AGY.ui.closeModal();
              promptAndExecuteGcClean(false);
            });
          }
        );
      }
    } catch (e) {
      AGY.api.showToast('孤儿体检失败：' + e.message, 'error');
    }
  }

  async function promptAndExecuteGcClean(closeIde) {
    try {
      var res = await AGY.api.apiFetch('/api/gc/clean', { method: 'POST', body: JSON.stringify({ closeIdeIfRunning: !!closeIde }) });
      await AGY.app.refreshAll();
      AGY.api.showToast('已清理 ' + (res.deletedDbFiles + res.deletedBrainDirs + res.deletedAnnotations) + ' 项残留，释放 ' + res.freedMegabytes + ' MB', 'success');
    } catch (e) {
      if (e.status === 409 && e.message.indexOf('IDE') >= 0 && !closeIde) {
        var ide = (AGY.state.overview && AGY.state.overview.ide) || {};
        AGY.ui.confirmCloseIdeModal(ide.parentCount || 1, ide.lsCount || 0, '清理孤儿残留文件', function () {
          promptAndExecuteGcClean(true);
        });
      } else {
        AGY.api.showToast(e.message, 'error');
      }
    }
  }

  AGY.views.overview = renderOverview;
})();
