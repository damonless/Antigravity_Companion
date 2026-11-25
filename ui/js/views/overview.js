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
      el('mainCanvas').innerHTML = '<div class="empty-state">正在加载本机工作台数据...</div>';
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
    var pendingPbCnt = c.pendingPbSyncCount || 0;
    var trashCnt = c.trashCount || 0;
    var trashMb = c.trashMegabytes || 0;

    var pathDescHtml = (localMiss > 0 || vaultMiss > 0)
      ? ('<div class="row gap-xs wrap">' +
           (localMiss > 0 ? '<span class="badge badge-red">本机缺失 ' + localMiss + '</span>' : '<span class="badge badge-green">本机正常</span>') +
           (vaultMiss > 0 ? '<span class="badge badge-amber">同步库缺失 ' + vaultMiss + '</span>' : '<span class="badge badge-neutral">同步库正常</span>') +
         '</div>')
      : '<span class="badge badge-green">本机与配置同步库路径均正常</span>';

    var gcHtml = gc
      ? ('<div class="metric-value">' + esc(gc.totalMegabytes) + ' <span style="font-size:14px;font-weight:500">MB</span></div>' +
         '<div class="metric-desc">数据库 ' + esc(gc.dbFilesCount || 0) + ' · 产物目录 ' + esc(gc.brainDirsCount || 0) + ' · 标注 ' + esc(gc.annotationFilesCount || 0) +
         (((gc.orphanDbBakCount || 0) + (gc.orphanDbJournalCount || 0)) > 0 ? (' · 临时 ' + esc((gc.orphanDbBakCount || 0) + (gc.orphanDbJournalCount || 0))) : '') + '</div>')
      : '<div class="metric-value" style="font-size:18px">待体检</div><div class="metric-desc">随时在线只读体检，不打断主窗口</div>';

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
      projectCardsHtml = '<div class="card empty-state" style="padding:20px">暂无已登记的本机项目。</div>';
    } else {
      projectCardsHtml = '<div class="project-cards-grid">' + formalGroups.map(function (fg, idx) {
        var ws = (fg.projectId && wsById[fg.projectId.toLowerCase()]) || (fg.fullPath && wsByPath[fg.fullPath.toLowerCase()]) || null;
        var hasAgents = ws && ws.hasAgentsDir;
        var hasRules = ws && (ws.hasGeminiMd || ws.hasAgentsMd);
        var folderExists = ws ? ws.exists : !!fg.fullPath;
        var subLabel = fg.isDuplicateBasename && fg.distinguishingLabel ? fg.distinguishingLabel : fg.shortPath;

        var missingDir = !folderExists && !!fg.fullPath;
        var canLaunch = !!fg.fullPath && folderExists;
        var actionButtonsHtml = missingDir
          ? ('<button class="btn btn-xs btn-primary ov-proj-reloc-inline" data-pidx="' + idx + '" type="button">修复目录位置</button>' +
             '<button class="btn btn-xs" data-ov-sessions="' + esc(fg.bucketKey) + '" type="button">看对话</button>')
          : (!canLaunch
            ? ('<button class="btn btn-xs ov-proj-reloc-inline" data-pidx="' + idx + '" type="button">关联本地目录</button>' +
               '<button class="btn btn-xs" data-ov-sessions="' + esc(fg.bucketKey) + '" type="button">看对话</button>')
            : ('<button class="btn btn-xs btn-primary" data-ov-launch="ide" data-path="' + esc(fg.fullPath || '') + '" type="button">主窗口打开</button>' +
               '<button class="btn btn-xs" data-ov-launch="explorer" data-path="' + esc(fg.fullPath || '') + '" type="button">打开目录</button>' +
               '<button class="btn btn-xs" data-ov-launch="powershell" data-path="' + esc(fg.fullPath || '') + '" type="button">开终端</button>' +
               '<button class="btn btn-xs" data-ov-sessions="' + esc(fg.bucketKey) + '" type="button">看对话</button>'));

        return '<div class="proj-launch-card">' +
          '<div class="between gap-xs">' +
            '<div class="proj-launch-title" title="' + esc(fg.projectName) + '">' + esc(fg.projectName) + '</div>' +
            '<div class="row gap-xs">' +
              '<span class="badge badge-accent">' + esc(fg.primaryCount) + ' 对话</span>' +
              '<div class="dropdown-wrap">' +
                '<button class="btn-icon ov-proj-more-btn" data-pidx="' + idx + '" title="更多项目操作" type="button">' + AGY.ui.ICONS.more + '</button>' +
                '<div class="dropdown-menu hidden" id="ovProjMenu_' + idx + '">' +
                  '<button class="dropdown-item ov-proj-reloc" data-pidx="' + idx + '" type="button">重新关联本机目录...</button>' +
                  (fg.projectId ? '<div class="dropdown-divider"></div><button class="dropdown-item danger ov-proj-del" data-pidx="' + idx + '" type="button">移除项目记录...</button>' : '') +
                '</div>' +
              '</div>' +
            '</div>' +
          '</div>' +
          '<div class="proj-launch-path" title="' + esc(fg.fullPath || '') + '">' + esc(subLabel || fg.fullPath || '(未关联本机目录)') + '</div>' +
          '<div class="row gap-xs wrap">' +
            (fg.isMultiRoot ? '<span class="badge badge-blue">多目录</span>' : '') +
            (hasAgents ? '<span class="badge badge-green">' + esc(ws.agentsDirName || '.agents') + '</span>' : '') +
            (hasRules ? '<span class="badge badge-neutral">含项目规则</span>' : '') +
            (missingDir ? '<span class="badge badge-red">目录不存在</span>' : '') +
          '</div>' +
          '<div class="proj-launch-actions">' + actionButtonsHtml + '</div>' +
        '</div>';
      }).join('') + '</div>';
    }

    var pendingPbHtml = '';
    if (pendingPbCnt > 0) {
      pendingPbHtml =
        '<div class="alert-banner between wrap gap-sm">' +
          '<div>🔄 有 <strong>' + esc(pendingPbCnt) + '</strong> 条新标题已写入本地数据，将在您关闭 Antigravity 主窗口后自动补刷到侧边栏列表。</div>' +
          '<button class="btn btn-xs btn-primary" id="btnOverviewFlushPending" type="button">立即关闭主窗口并同步</button>' +
        '</div>';
    }

    var lockedAlertHtml = '';
    if (lockedSessions.length > 0 || lockedUntagged > 0) {
      lockedAlertHtml =
        '<div class="alert-banner between wrap gap-sm">' +
          '<div>🔒 检测到 <strong>' + lockedSessions.length + '</strong> 条 <strong>进行中 (自动跳过)</strong> 对话（含待整理标题 <strong>' + lockedUntagged + '</strong> 条），伴侣已自动避开，不会打断正在运行的任务。</div>' +
          '<button class="btn btn-xs" id="btnOverviewInspectLocked" type="button">查看进行中列表</button>' +
        '</div>';
    }

    var html =
      '<div class="overview-grid">' +
        '<div class="metric-card">' +
          '<div class="metric-label">本机与配置同步库</div>' +
          '<div class="metric-value">' + esc(c.localCapabilitiesCount || 0) + ' <span class="muted" style="font-size:13px;font-weight:500">/ 同步库 ' + esc(c.repoCapabilitiesCount || 0) + '</span></div>' +
          '<div class="metric-desc">' + pathDescHtml + '</div>' +
        '</div>' +
        '<div class="metric-card">' +
          '<div class="metric-label">项目与主对话</div>' +
          '<div class="metric-value">' + esc(c.formalPrimarySessions || 0) + '</div>' +
          '<div class="metric-desc">' + esc(c.formalProjectsCount || 0) + ' 个项目 · CLI ' + (c.cliSessions || 0) + ' · 未关联项目 ' + (c.outsideSessions || 0) + ' · 后台子任务 ' + (c.subagentSessions || 0) + '</div>' +
        '</div>' +
        '<div class="metric-card">' +
          '<div class="metric-label">待整理标题与同步差异</div>' +
          '<div class="metric-value">' + esc(actionableUntagged) + ' <span class="muted" style="font-size:13px;font-weight:500">可整理 / ' + esc(lockedUntagged) + ' 进行中</span></div>' +
          '<div class="metric-desc">同步库差异：' + esc(diffTotal) + ' 项 · 常驻规则约 ' + esc(tb.totalAlwaysOnTokens || 0) + ' tokens</div>' +
        '</div>' +
        '<div class="metric-card">' +
          '<div class="metric-label">残留垃圾文件</div>' + gcHtml +
        '</div>' +
      '</div>' +

      pendingPbHtml +
      lockedAlertHtml +

      '<div class="section-header">' +
        '<strong>已登记项目快速启动台 (' + formalGroups.length + ')</strong>' +
        '<div class="row gap-sm">' +
          '<button class="btn btn-sm" id="btnOverviewGcScan" type="button">体检残留垃圾文件</button>' +
          (gc && gc.totalItems > 0 ? '<button class="btn btn-sm btn-danger-outline" id="btnOverviewGcClean" type="button">清理残留 (' + esc(gc.totalMegabytes) + ' MB)</button>' : '') +
          '<button class="btn btn-sm btn-primary" data-jump="cockpit" type="button">管理扩展与配置 →</button>' +
        '</div>' +
      '</div>' +
      projectCardsHtml +

      '<div class="action-grid">' +
        '<div class="quick-card">' +
          '<div>' +
            '<div class="quick-card-title">批量整理对话标题</div>' +
            '<div class="quick-card-desc">还有 <strong>' + esc(actionableUntagged) + '</strong> 条空闲 (可操作) 主对话待整理，支持规则自动补前缀、免密钥 agy CLI 与离线 AI 润色。</div>' +
          '</div>' +
          '<button class="btn btn-primary" data-jump-rename="1" type="button">去整理标题 →</button>' +
        '</div>' +
        '<div class="quick-card">' +
          '<div>' +
            '<div class="quick-card-title">空间清理与在线备份</div>' +
            '<div class="quick-card-desc">随时在线创建备份文件 (.agy) 无需关闭主窗口；回收站当前暂存 <strong>' + esc(trashCnt) + '</strong> 项（' + esc(trashMb) + ' MB），30 天内可随时还原。</div>' +
          '</div>' +
          '<div class="row gap-xs">' +
            '<button class="btn" data-jump="storage" type="button">空间清理</button>' +
            '<button class="btn" data-jump="backup" type="button">备份与同步</button>' +
          '</div>' +
        '</div>' +
      '</div>' +

      '<div class="card">' +
        '<div class="between wrap gap-sm" style="margin-bottom:8px">' +
          '<strong>本机数据与配置同步库路径</strong>' +
          '<div class="row gap-sm">' +
            '<button class="btn btn-sm" data-jump="vault" type="button">配置同步库</button>' +
            '<button class="btn btn-sm" data-jump="trash" type="button">回收站 (' + esc(trashCnt) + ')</button>' +
            '<button class="btn btn-sm" data-jump="settings" type="button">应用设置</button>' +
          '</div>' +
        '</div>' +
        '<div class="muted mono" style="font-size:12px;line-height:1.8">' +
          '<div>本机数据目录：' + esc(paths.gemini || '') + '</div>' +
          '<div>配置同步库：' + esc(paths.vault || paths.repository || '') + '</div>' +
        '</div>' +
      '</div>';

    var root = el('mainCanvas');
    root.innerHTML = html;

    var jumpBtns = root.querySelectorAll('[data-jump]');
    for (var i = 0; i < jumpBtns.length; i++) {
      jumpBtns[i].addEventListener('click', function () {
        var target = this.getAttribute('data-jump');
        if (target === 'cockpit') st.cockpitSubView = 'settings';
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
        st.sessionsSubView = 'browse';
        AGY.app.switchView('sessions');
      });
    }

    var launchBtns = root.querySelectorAll('[data-ov-launch]');
    for (var lb = 0; lb < launchBtns.length; lb++) {
      launchBtns[lb].addEventListener('click', function () {
        var btn = this;
        var tool = btn.getAttribute('data-ov-launch');
        var path = btn.getAttribute('data-path');
        AGY.api.withButtonBusy(btn, '启动中...', async function () {
          try {
            await AGY.api.apiFetch('/api/workspace/launch', {
              method: 'POST',
              body: JSON.stringify({ path: path, tool: tool })
            });
            AGY.api.showToast('已打开项目目录 ✓', 'success');
          } catch (e) {
            AGY.api.showToast('打开失败：' + e.message, 'error');
          }
        });
      });
    }

    var moreBtns = root.querySelectorAll('.ov-proj-more-btn');
    for (var mb = 0; mb < moreBtns.length; mb++) {
      moreBtns[mb].addEventListener('click', function (ev) {
        ev.stopPropagation();
        var idx = this.getAttribute('data-pidx');
        var menu = el('ovProjMenu_' + idx);
        var wasOpen = menu && !menu.classList.contains('hidden');
        AGY.ui.closeAllDropdowns();
        if (menu && !wasOpen) menu.classList.remove('hidden');
      });
    }
    var relocBtns = root.querySelectorAll('.ov-proj-reloc, .ov-proj-reloc-inline');
    for (var rb = 0; rb < relocBtns.length; rb++) {
      relocBtns[rb].addEventListener('click', function (ev) {
        ev.stopPropagation();
        AGY.ui.closeAllDropdowns();
        var fg = formalGroups[parseInt(this.getAttribute('data-pidx'), 10)];
        if (fg && AGY.views.openRelocateProjectModal) AGY.views.openRelocateProjectModal(fg);
      });
    }
    var delBtns = root.querySelectorAll('.ov-proj-del');
    for (var db = 0; db < delBtns.length; db++) {
      delBtns[db].addEventListener('click', function (ev) {
        ev.stopPropagation();
        AGY.ui.closeAllDropdowns();
        var fg = formalGroups[parseInt(this.getAttribute('data-pidx'), 10)];
        if (fg && AGY.views.openDeleteProjectModal) AGY.views.openDeleteProjectModal(fg);
      });
    }

    if (el('btnOverviewFlushPending')) {
      el('btnOverviewFlushPending').addEventListener('click', function () {
        flushPendingPbFromOverview(this, false, false);
      });
    }

    if (el('btnOverviewInspectLocked')) {
      el('btnOverviewInspectLocked').addEventListener('click', function () {
        var rows = lockedSessions.map(function (s) {
          return '<tr><td><span class="badge badge-neutral">' + esc(s.projectName) + '</span></td>' +
            '<td>' + esc(s.title) + '</td>' +
            '<td><span class="badge badge-amber">进行中 (自动跳过)</span></td></tr>';
        }).join('');
        AGY.ui.openModal(
          '当前进行中的对话 (' + lockedSessions.length + ')',
          '<div class="muted" style="margin-bottom:10px">这些对话或关联后台子任务仍在运行，整理标题与空间清理会自动跳过它们，避免产生冲突。</div>' +
          '<div style="max-height:320px;overflow:auto;border:1px solid var(--border);border-radius:8px">' +
            '<table class="data-table"><thead><tr><th>项目</th><th>对话标题</th><th>状态</th></tr></thead><tbody>' +
              (rows || '<tr><td colspan="3" class="empty-state">当前没有进行中的对话</td></tr>') +
            '</tbody></table>' +
          '</div>',
          '<button class="btn btn-primary" data-act="ok">知道了</button>',
          function (_b, mf) {
            mf.querySelector('[data-act="ok"]').addEventListener('click', AGY.ui.closeModal);
          }
        );
      });
    }

    el('btnOverviewGcScan').addEventListener('click', function () { handleReadOnlyGcScan(this); });
    var btnClean = el('btnOverviewGcClean');
    if (btnClean) btnClean.addEventListener('click', function () { confirmAndExecuteGcClean(gc, this); });
  }

  function confirmAndExecuteGcClean(gc, triggerBtn) {
    var esc = AGY.ui.escapeHtml;
    var res = gc || {};
    var tmpCnt = (res.orphanDbBakCount || 0) + (res.orphanDbJournalCount || 0);
    AGY.ui.openModal(
      '确认清理残留垃圾文件？',
      '<div class="alert-banner success" style="margin-bottom:10px">🛡 <strong>可撤销清理：</strong>所有残留垃圾文件都会先移入 <strong>回收站</strong>，30 天内随时可一键还原，不会影响正常对话或项目源码。</div>' +
      '<ul style="margin:6px 0 8px 18px;line-height:1.7">' +
        '<li>残留对话数：<strong>' + esc(res.sessionCount || 0) + '</strong> 个</li>' +
        '<li>残留数据库文件：<strong>' + esc(res.dbFilesCount || 0) + '</strong> 个</li>' +
        (tmpCnt > 0 ? '<li>残留备份/临时文件 (.bak / -journal)：<strong>' + esc(tmpCnt) + '</strong> 个</li>' : '') +
        '<li>残留产物目录：<strong>' + esc(res.brainDirsCount || 0) + '</strong> 个</li>' +
        '<li>残留标题记录 (.pbtxt)：<strong>' + esc(res.annotationFilesCount || 0) + '</strong> 个</li>' +
        '<li>预计释放空间：<strong>' + esc(res.totalMegabytes || 0) + ' MB</strong></li>' +
      '</ul>',
      '<button class="btn" data-act="cancel">取消</button><button class="btn btn-danger" data-act="confirm">确认移入回收站并清理</button>',
      function (_b, mf) {
        mf.querySelector('[data-act="cancel"]').addEventListener('click', AGY.ui.closeModal);
        mf.querySelector('[data-act="confirm"]').addEventListener('click', function () {
          AGY.ui.closeModal();
          promptAndExecuteGcClean(false, false, triggerBtn || null);
        });
      }
    );
  }

  async function flushPendingPbFromOverview(btn, closeIde, forceKill) {
    AGY.api.withButtonBusy(btn, '同步中...', async function () {
      try {
        var res = await AGY.api.apiFetch('/api/rename/flush-pending', {
          method: 'POST',
          body: JSON.stringify({ closeIdeIfRunning: !!closeIde, forceKill: !!forceKill })
        });
        await AGY.app.refreshAll('已将 ' + (res.flushedCount || 0) + ' 条改名记录同步到侧边栏 ✓');
      } catch (e) {
        if (!AGY.ui.handleIdeConflictError(
          e,
          '将待补刷的改名记录同步到侧边栏',
          function () { flushPendingPbFromOverview(null, true, false); },
          function () { flushPendingPbFromOverview(null, true, true); }
        )) {
          AGY.api.showToast('同步失败：' + e.message, 'error');
        }
      }
    });
  }

  async function handleReadOnlyGcScan(btn) {
    AGY.api.withButtonBusy(btn, '体检中...', async function () {
      try {
        var res = await AGY.api.apiFetch('/api/gc/scan', { method: 'POST', body: '{}' });
        if (!AGY.state.overview) AGY.state.overview = {};
        AGY.state.overview.lastOrphanScan = res;
        if (AGY.state.view === 'overview') renderOverview();
        if (res.totalItems === 0) {
          AGY.api.showToast('本机环境非常干净，未发现任何残留垃圾文件。', 'success');
        } else {
          var tmpCnt = (res.orphanDbBakCount || 0) + (res.orphanDbJournalCount || 0);
          AGY.ui.openModal(
            '残留垃圾文件体检结果',
            '<div>发现以下已脱离记录的残留垃圾文件（清理后将先移入回收站，30 天内可一键还原）：</div>' +
            '<ul style="margin:10px 0 12px 18px;line-height:1.7">' +
              '<li>残留对话数：<strong>' + res.sessionCount + '</strong> 个</li>' +
              '<li>残留数据库文件：<strong>' + res.dbFilesCount + '</strong> 个</li>' +
              (tmpCnt > 0 ? '<li>残留备份/临时文件 (.bak / -journal)：<strong>' + tmpCnt + '</strong> 个</li>' : '') +
              '<li>残留产物目录：<strong>' + res.brainDirsCount + '</strong> 个</li>' +
              '<li>残留标题记录 (.pbtxt)：<strong>' + res.annotationFilesCount + '</strong> 个</li>' +
              '<li>可释放空间：<strong>' + res.totalMegabytes + ' MB</strong></li>' +
            '</ul>',
            '<button class="btn" data-act="close">暂不清理</button><button class="btn btn-danger" data-act="clean">移入回收站并清理</button>',
            function (_b, mf) {
              mf.querySelector('[data-act="close"]').addEventListener('click', AGY.ui.closeModal);
              mf.querySelector('[data-act="clean"]').addEventListener('click', function () {
                AGY.ui.closeModal();
                promptAndExecuteGcClean(false, false, null);
              });
            }
          );
        }
      } catch (e) {
        AGY.api.showToast('体检失败：' + e.message, 'error');
      }
    });
  }

  async function promptAndExecuteGcClean(closeIde, forceKill, btn) {
    AGY.api.withButtonBusy(btn, '清理中...', async function () {
      try {
        var res = await AGY.api.apiFetch('/api/gc/clean', {
          method: 'POST',
          body: JSON.stringify({ closeIdeIfRunning: !!closeIde, forceKill: !!forceKill })
        });
        await AGY.app.refreshAll();
        AGY.api.showToast('已将 ' + (res.deletedDbFiles + res.deletedBrainDirs + res.deletedAnnotations) + ' 项残留垃圾文件移入回收站，释放 ' + res.freedMegabytes + ' MB ✓', 'success');
      } catch (e) {
        if (!AGY.ui.handleIdeConflictError(
          e,
          '清理残留垃圾文件',
          function () { promptAndExecuteGcClean(true, false, null); },
          function () { promptAndExecuteGcClean(true, true, null); }
        )) {
          AGY.api.showToast(e.message, 'error');
        }
      }
    });
  }

  AGY.views.overview = renderOverview;
})();
