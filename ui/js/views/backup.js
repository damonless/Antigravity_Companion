(function () {
  'use strict';

  var AGY = window.AGY = window.AGY || {};
  AGY.views = AGY.views || {};

  function getSessionStatusBadge(status) {
    switch (status) {
      case 'New': return '<span class="badge badge-blue">备份新增</span>';
      case 'Newer': return '<span class="badge badge-blue">备份较新</span>';
      case 'LocalNewer': return '<span class="badge badge-amber">本机较新</span>';
      case 'Modified': return '<span class="badge badge-accent">两边有差异</span>';
      case 'LocalOnly': return '<span class="badge badge-neutral">仅本机有</span>';
      case 'Aligned': return '<span class="badge badge-green">已一致</span>';
      default: return '<span class="badge badge-neutral">' + AGY.ui.escapeHtml(status || '-') + '</span>';
    }
  }

  function getCapDirectionBadge(it) {
    if (it.direction === 'NeedsReview' || it.direction === 'Conflict') {
      return '<span class="badge badge-red">需人工确认</span>';
    }
    if (it.direction === 'RemoteOnly') return '<span class="badge badge-blue">仅备份有</span>';
    if (it.direction === 'RemoteNewer') return '<span class="badge badge-blue">备份较新</span>';
    if (it.direction === 'LocalOnly') return '<span class="badge badge-amber">仅本机有</span>';
    if (it.direction === 'LocalNewer') return '<span class="badge badge-amber">本机较新</span>';
    if (it.direction === 'Aligned') return '<span class="badge badge-green">已一致</span>';
    return '<span class="badge badge-accent">' + AGY.ui.escapeHtml(it.directionBadge || it.direction) + '</span>';
  }

  function renderArchiveInnerTableHtml(bd) {
    var st = AGY.state;
    var esc = AGY.ui.escapeHtml;
    var q = (st.backupSearch || '').trim().toLowerCase();

    if (st.backupTab === 'caps') {
      var capList = (bd.capabilityDiffs || []).filter(function (c) {
        if (!q) return true;
        return (c.displayName || '').toLowerCase().indexOf(q) >= 0 ||
               (c.kind || '').toLowerCase().indexOf(q) >= 0;
      });
      return '<table class="data-table"><thead><tr><th></th><th>插件 / 技能 / 规则 / MCP / 钩子</th><th>差异状态</th><th>操作</th></tr></thead><tbody>' +
        (capList.map(function (c) {
          var chk = !!st.backupCheckedCaps[c.key];
          return '<tr class="' + (chk ? 'selected' : '') + '">' +
            '<td style="width:36px"><input type="checkbox" class="bk-cap-chk" data-key="' + esc(c.key) + '"' + (chk ? ' checked' : '') + ' /></td>' +
            '<td><span class="badge badge-neutral">' + esc(AGY.ui.kindLabel(c.kind)) + '</span> <strong>' + esc(c.displayName) + '</strong></td>' +
            '<td>' + getCapDirectionBadge(c) + '</td>' +
            '<td><button class="btn btn-xs" data-bk-diff="' + esc(c.key) + '" type="button">对比源码</button></td>' +
          '</tr>';
        }).join('') || '<tr><td colspan="4" class="empty-state">备份文件中无匹配的扩展配置项</td></tr>') +
        '</tbody></table>';
    }

    var projGroups = bd.sessionDiffGroups || [];
    var sRows = [];
    for (var pi = 0; pi < projGroups.length; pi++) {
      var pg = projGroups[pi];
      var sessList = pg.sessions || [];
      for (var si = 0; si < sessList.length; si++) {
        var s = sessList[si];
        var children = s.children || [];
        var matchesSelf = !q ||
          (s.title || '').toLowerCase().indexOf(q) >= 0 ||
          (s.projectName || '').toLowerCase().indexOf(q) >= 0;
        var matchingChildren = !q ? children : children.filter(function (ch) {
          return (ch.title || '').toLowerCase().indexOf(q) >= 0;
        });

        if (!matchesSelf && matchingChildren.length === 0) continue;

        var chkS = !!st.backupCheckedConvs[s.cid];
        var hasChildren = children.length > 0;
        var isExpanded = !!st.backupExpandedParents[s.cid] || (q && matchingChildren.length > 0);
        var pLabel = (s.projectName === 'Outside of Project' || !s.projectName) ? '未关联项目' : s.projectName;

        sRows.push(
          '<tr class="' + (chkS ? 'selected' : '') + '">' +
            '<td style="width:36px"><input type="checkbox" class="bk-conv-chk" data-cid="' + esc(s.cid) + '"' + (chkS ? ' checked' : '') + ' /></td>' +
            '<td>' +
              '<span class="badge badge-neutral">' + esc(pLabel) + '</span> ' +
              (s.isSubagent ? '<span class="badge badge-neutral">后台子任务</span> ' : '') +
              '<strong>' + esc(s.title) + '</strong>' +
              (hasChildren ? ' <button class="btn btn-xs btn-ghost bk-toggle-sub" data-parent="' + esc(s.cid) + '" type="button">' + (isExpanded ? '▾ 收起 ' : '▸ 展开 ') + children.length + ' 个后台子任务</button>' : '') +
            '</td>' +
            '<td>' + esc(s.stepCount) + ' 步</td>' +
            '<td>' + getSessionStatusBadge(s.status) + '</td>' +
            '<td class="muted">' + esc(s.updatedTime) + '</td>' +
            '<td><button class="btn btn-xs" data-bk-md="' + esc(s.cid) + '" type="button">免导入预览</button></td>' +
          '</tr>'
        );

        if (hasChildren && isExpanded) {
          var subList = q ? matchingChildren : children;
          for (var ci = 0; ci < subList.length; ci++) {
            var ch = subList[ci];
            sRows.push(
              '<tr class="sub-row">' +
                '<td><span class="muted" title="随主对话自动一并恢复">↳</span></td>' +
                '<td><span class="badge badge-neutral">后台子任务</span> ' + esc(ch.title) + '</td>' +
                '<td>' + esc(ch.stepCount) + ' 步</td>' +
                '<td>' + getSessionStatusBadge(ch.status) + '</td>' +
                '<td class="muted">' + esc(ch.updatedTime) + '</td>' +
                '<td><button class="btn btn-xs" data-bk-md="' + esc(ch.cid) + '" type="button">预览</button></td>' +
              '</tr>'
            );
          }
        }
      }
    }

    return '<table class="data-table"><thead><tr><th></th><th>项目与对话标题</th><th>步数</th><th>相对本机</th><th>更新时间</th><th>操作</th></tr></thead><tbody>' +
      (sRows.join('') || '<tr><td colspan="6" class="empty-state">' + (bd.hasLegacyCliSummaryDb ? '此清理前安全备份包含旧版 CLI 历史摘要库，勾选上方「恢复旧版 CLI 历史摘要库」后即可一键还原。' : '备份文件中无匹配的对话记录') + '</td></tr>') +
      '</tbody></table>';
  }

  function updateBackupImportBtn() {
    var st = AGY.state;
    var btn = AGY.dom.el('btnBkImportExec');
    if (!btn) return;
    var bd = st.backupData || {};
    var selCapsCount = Object.keys(st.backupCheckedCaps).filter(function (k) { return st.backupCheckedCaps[k]; }).length;
    var selConvsCount = Object.keys(st.backupCheckedConvs).filter(function (k) { return st.backupCheckedConvs[k]; }).length;
    var restoreUs = !bd.isColdArchive && !!st.backupRestoreUserSettings;
    var restoreLegacy = !!(bd.hasLegacyCliSummaryDb && st.backupRestoreLegacyCli);

    btn.disabled = (selCapsCount + selConvsCount === 0) && !restoreUs && !restoreLegacy;
    if (bd.isColdArchive) {
      var parts = [];
      if (selConvsCount > 0) parts.push(selConvsCount + ' 个后台子任务');
      if (restoreLegacy) parts.push('旧版 CLI 摘要库');
      btn.textContent = '← 一键恢复到本机' + (parts.length ? (' (' + parts.join(' + ') + ')') : '');
    } else {
      var extra = restoreUs ? ' + 全局配置' : '';
      btn.textContent = '← 恢复所选到本机 (' + selCapsCount + ' 配置 + ' + selConvsCount + ' 对话' + extra + ')';
    }
  }

  function bindArchiveTableEvents(bd) {
    var host = AGY.dom.el('bkTableHost');
    if (!host) return;
    var st = AGY.state;

    var cChks = host.querySelectorAll('.bk-cap-chk');
    for (var ci = 0; ci < cChks.length; ci++) {
      cChks[ci].addEventListener('change', function () {
        st.backupCheckedCaps[this.getAttribute('data-key')] = this.checked;
        AGY.dom.patchCheckboxRow(this.closest('tr'), this.checked);
        updateBackupImportBtn();
      });
    }
    var sChks = host.querySelectorAll('.bk-conv-chk');
    for (var si = 0; si < sChks.length; si++) {
      sChks[si].addEventListener('change', function () {
        st.backupCheckedConvs[this.getAttribute('data-cid')] = this.checked;
        AGY.dom.patchCheckboxRow(this.closest('tr'), this.checked);
        updateBackupImportBtn();
      });
    }
    var subBtns = host.querySelectorAll('.bk-toggle-sub');
    for (var bi = 0; bi < subBtns.length; bi++) {
      subBtns[bi].addEventListener('click', function () {
        var pid = this.getAttribute('data-parent');
        st.backupExpandedParents[pid] = !st.backupExpandedParents[pid];
        host.innerHTML = renderArchiveInnerTableHtml(bd);
        bindArchiveTableEvents(bd);
      });
    }
    var dBtns = host.querySelectorAll('[data-bk-diff]');
    for (var di = 0; di < dBtns.length; di++) {
      dBtns[di].addEventListener('click', function () { AGY.ui.openCapabilityDiffDrawer(this.getAttribute('data-bk-diff'), 'backup'); });
    }
    var mBtns = host.querySelectorAll('[data-bk-md]');
    for (var mi = 0; mi < mBtns.length; mi++) {
      mBtns[mi].addEventListener('click', function () { AGY.ui.openMarkdownDrawer(this.getAttribute('data-bk-md'), 'backup'); });
    }
  }

  function renderBackup() {
    var el = AGY.dom.el;
    var st = AGY.state;
    var esc = AGY.ui.escapeHtml;
    var bd = st.backupData || { loaded: false };
    var snap = st.snapshotStatus || {};
    var existingSnaps = snap.existingSnapshots || [];
    var coldArchives = snap.coldArchives || [];

    var hero =
      '<div class="action-grid">' +
        '<div class="card">' +
          '<div class="between wrap gap-sm" style="margin-bottom:8px">' +
            '<div>' +
              '<div class="quick-card-title">创建本机备份文件 (.agy)</div>' +
              '<div class="quick-card-desc">支持在线热备，无需关闭 Antigravity 主窗口。日常备份更快，完整备份含全部后台子任务。</div>' +
            '</div>' +
            '<button class="btn btn-primary" id="btnTriggerFullBk" type="button">立即在线备份...</button>' +
          '</div>' +
          '<div class="pill-group">' +
            '<button class="filter-pill ' + (st.backupTier === 'fast' ? 'active' : '') + '" data-bktier="fast" type="button">日常备份（推荐·更快）</button>' +
            '<button class="filter-pill ' + (st.backupTier === 'full' ? 'active' : '') + '" data-bktier="full" type="button">完整备份（含全部后台子任务）</button>' +
          '</div>' +
        '</div>' +
        '<div class="quick-card">' +
          '<div>' +
            '<div class="quick-card-title">打开任意备份文件 (.agy)</div>' +
            '<div class="quick-card-desc">免解压直接浏览历史或跨机备份包里的对话与扩展源码，支持按需挑选恢复。</div>' +
          '</div>' +
          '<button class="btn" id="btnOpenAgyFile" type="button">选择 .agy 文件...</button>' +
        '</div>' +
      '</div>';

    // Auto-backup history & Cold Archives section
    var snapRowsHtml = existingSnaps.length === 0
      ? '<div class="muted" style="font-size:12px">暂无自动备份记录。点击右上「立即创建自动备份」或在「应用设置」中开启定时自动备份。</div>'
      : '<div style="border:1px solid var(--border);border-radius:8px;max-height:210px;overflow:auto">' +
          existingSnaps.map(function (sp, idx) {
            var whenStr = sp.createdUtc ? (new Date(sp.createdUtc).toLocaleString()) : (sp.modifiedTime || '-');
            return '<div class="snap-history-row">' +
              '<div><strong>' + esc(sp.fileName) + '</strong><div class="muted mono" style="font-size:11px">' + esc(whenStr) + ' · ' + esc(sp.sizeMegabytes) + ' MB</div></div>' +
              '<div class="row gap-xs">' +
                '<button class="btn btn-xs btn-primary btn-bk-open-snap" data-sidx="' + idx + '" type="button">查看并还原</button>' +
                '<button class="btn btn-xs btn-bk-explore-snap" data-sidx="' + idx + '" type="button">打开目录</button>' +
              '</div>' +
            '</div>';
          }).join('') +
        '</div>';

    var coldSectionHtml = '';
    if (coldArchives.length > 0) {
      coldSectionHtml =
        '<div style="margin-top:12px">' +
          '<div class="muted" style="font-size:12px;font-weight:600;margin-bottom:6px">清理前安全备份 (' + coldArchives.length + ')</div>' +
          '<div style="border:1px solid var(--border);border-radius:8px;max-height:170px;overflow:auto">' +
            coldArchives.map(function (ca, idx) {
              var whenStr = ca.createdUtc ? (new Date(ca.createdUtc).toLocaleString()) : (ca.modifiedTime || '-');
              return '<div class="snap-history-row">' +
                '<div><strong>' + esc(ca.fileName) + '</strong> <span class="badge badge-neutral">清理前安全备份</span><div class="muted mono" style="font-size:11px">' + esc(whenStr) + ' · ' + esc(ca.sizeMegabytes) + ' MB</div></div>' +
                '<div class="row gap-xs">' +
                  '<button class="btn btn-xs btn-primary btn-bk-open-cold" data-cidx="' + idx + '" type="button">查看并还原</button>' +
                  '<button class="btn btn-xs btn-bk-explore-cold" data-cidx="' + idx + '" type="button">打开目录</button>' +
                '</div>' +
              '</div>';
            }).join('') +
          '</div>' +
        '</div>';
    }

    var historyCard =
      '<div class="card" style="margin-bottom:16px">' +
        '<div class="between wrap gap-sm" style="margin-bottom:10px">' +
          '<div>' +
            '<strong>自动备份历史 (' + existingSnaps.length + ')</strong>' +
            '<div class="muted mono" style="font-size:11px">' + esc(snap.targetDirectory || '') + '</div>' +
          '</div>' +
          '<div class="row gap-xs">' +
            '<button class="btn btn-sm" id="btnBkRunAutoSnapNow" type="button">⚡ 立即创建自动备份</button>' +
            (snap.targetDirectory ? '<button class="btn btn-sm" id="btnBkOpenSnapDir" type="button">打开自动备份目录</button>' : '') +
            '<button class="btn btn-sm btn-ghost" id="btnBkGotoSettings" type="button">计划设置 →</button>' +
          '</div>' +
        '</div>' +
        snapRowsHtml +
        coldSectionHtml +
      '</div>';

    var archiveSection = '';
    if (!bd.loaded) {
      archiveSection = '<div class="card empty-state">尚未打开任何备份文件 (.agy)。可以从上方自动备份历史直接点「查看并还原」，或点击「选择 .agy 文件...」。</div>';
    } else {
      var capList = bd.capabilityDiffs || [];
      var tierLabel = bd.isColdArchive
        ? '清理前安全备份'
        : (bd.backupTier === 'FullForensic' ? '完整备份' : (bd.backupTier === 'FastDaily' ? '日常备份' : (bd.backupTier || '备份文件')));

      archiveSection =
        '<div class="card" style="display:flex;flex-direction:column;min-height:0">' +
          '<div class="between wrap gap-sm" style="margin-bottom:10px">' +
            '<div>' +
              '<strong>' + esc(bd.fileName) + '</strong> ' +
              '<span class="badge ' + (bd.isColdArchive ? 'badge-blue' : 'badge-neutral') + '">' + esc(tierLabel) + '</span>' +
              '<div class="muted mono" style="font-size:11.5px">来源: ' + esc(bd.sourceUser || '本机') + ' · 时间: ' + esc(bd.backupTime || '-') + ' · 体积: ' + esc(bd.sizeMegabytes) + ' MB</div>' +
            '</div>' +
            '<div class="row gap-sm wrap">' +
              '<button class="btn btn-sm" id="btnBkAutoPick" type="button">' + (bd.isColdArchive ? '全选可恢复项' : '仅选备份新增/较新') + '</button>' +
              '<button class="btn btn-sm" id="btnBkClearPick" type="button">清空勾选</button>' +
              '<button class="btn btn-sm btn-primary" id="btnBkImportExec" type="button">← 恢复所选到本机</button>' +
            '</div>' +
          '</div>' +
          (bd.isColdArchive ? '<div class="alert-banner success" style="margin-bottom:10px">🛡 这是空间清理前生成的 <strong>清理前安全备份 (.agy)</strong>。勾选所需条目即可无损还原回本机数据库目录（还原前会自动备份冲突文件）。</div>' : '') +
          '<div class="between wrap gap-sm" style="margin-bottom:10px">' +
            '<div class="pill-group">' +
              (!bd.isColdArchive ? '<button class="filter-pill ' + (st.backupTab === 'caps' ? 'active' : '') + '" data-bktab="caps" type="button">扩展配置差异 (' + capList.length + ')</button>' : '') +
              '<button class="filter-pill ' + (st.backupTab === 'sessions' || bd.isColdArchive ? 'active' : '') + '" data-bktab="sessions" type="button">' + (bd.isColdArchive ? '归档的后台子任务' : '项目与对话') + ' (' + (bd.conversationsCount || 0) + ')</button>' +
            '</div>' +
            '<div class="row gap-sm wrap">' +
              (!bd.isColdArchive ? '<label class="row gap-xs" style="font-size:12px;cursor:pointer"><input type="checkbox" id="chkRestoreUserSettings"' + (st.backupRestoreUserSettings ? ' checked' : '') + ' /> 同时恢复全局运行配置与插件开关</label>' : '') +
              (bd.hasLegacyCliSummaryDb ? '<label class="row gap-xs" style="font-size:12px;cursor:pointer"><input type="checkbox" id="chkRestoreLegacyCli"' + (st.backupRestoreLegacyCli ? ' checked' : '') + ' /> 恢复旧版 CLI 历史摘要库</label>' : '') +
              '<input class="input" id="txtBkSearch" style="width:190px" placeholder="搜索备份内容..." value="' + esc(st.backupSearch) + '" />' +
            '</div>' +
          '</div>' +
          '<div id="bkTableHost" style="max-height:420px;overflow:auto;border:1px solid var(--border);border-radius:8px">' + renderArchiveInnerTableHtml(bd) + '</div>' +
        '</div>';
    }

    var root = el('mainCanvas');
    root.innerHTML = hero + historyCard + archiveSection;

    var tierBtns = root.querySelectorAll('[data-bktier]');
    for (var tb = 0; tb < tierBtns.length; tb++) {
      tierBtns[tb].addEventListener('click', function () {
        st.backupTier = this.getAttribute('data-bktier');
        for (var j = 0; j < tierBtns.length; j++) tierBtns[j].classList.remove('active');
        this.classList.add('active');
      });
    }

    el('btnTriggerFullBk').addEventListener('click', function () { triggerFullBackup(this); });
    el('btnOpenAgyFile').addEventListener('click', function () { openBackupArchiveByPath(null, this); });

    el('btnBkRunAutoSnapNow').addEventListener('click', function () {
      var btn = this;
      AGY.api.withButtonBusy(btn, '备份中...', async function () {
        try {
          var res = await AGY.api.apiFetch('/api/snapshots/run-now', { method: 'POST', body: '{}' });
          st.snapshotStatus = await AGY.api.apiFetch('/api/snapshots/status');
          renderBackup();
          AGY.api.showToast('自动备份已完成（' + res.sizeMegabytes + ' MB）✓', 'success');
        } catch (e) { AGY.api.showToast('自动备份失败：' + e.message, 'error'); }
      });
    });

    if (el('btnBkOpenSnapDir') && snap.targetDirectory) {
      el('btnBkOpenSnapDir').addEventListener('click', async function () {
        try {
          await AGY.api.apiFetch('/api/native/open-in-explorer', {
            method: 'POST',
            body: JSON.stringify({ path: snap.targetDirectory })
          });
        } catch (e) { AGY.api.showToast(e.message, 'error'); }
      });
    }

    el('btnBkGotoSettings').addEventListener('click', function () {
      AGY.app.switchView('settings');
    });

    var openSnapBtns = root.querySelectorAll('.btn-bk-open-snap');
    for (var os = 0; os < openSnapBtns.length; os++) {
      openSnapBtns[os].addEventListener('click', function () {
        var sp = existingSnaps[parseInt(this.getAttribute('data-sidx'), 10)];
        if (sp) openBackupArchiveByPath(sp.filePath || sp.fullPath, this);
      });
    }
    var expSnapBtns = root.querySelectorAll('.btn-bk-explore-snap');
    for (var es = 0; es < expSnapBtns.length; es++) {
      expSnapBtns[es].addEventListener('click', async function () {
        var sp = existingSnaps[parseInt(this.getAttribute('data-sidx'), 10)];
        if (!sp) return;
        try {
          await AGY.api.apiFetch('/api/native/open-in-explorer', { method: 'POST', body: JSON.stringify({ path: sp.filePath || sp.fullPath }) });
        } catch (e) { AGY.api.showToast(e.message, 'error'); }
      });
    }

    var openColdBtns = root.querySelectorAll('.btn-bk-open-cold');
    for (var oc = 0; oc < openColdBtns.length; oc++) {
      openColdBtns[oc].addEventListener('click', function () {
        var ca = coldArchives[parseInt(this.getAttribute('data-cidx'), 10)];
        if (ca) openBackupArchiveByPath(ca.filePath || ca.fullPath, this);
      });
    }
    var expColdBtns = root.querySelectorAll('.btn-bk-explore-cold');
    for (var ec = 0; ec < expColdBtns.length; ec++) {
      expColdBtns[ec].addEventListener('click', async function () {
        var ca = coldArchives[parseInt(this.getAttribute('data-cidx'), 10)];
        if (!ca) return;
        try {
          await AGY.api.apiFetch('/api/native/open-in-explorer', { method: 'POST', body: JSON.stringify({ path: ca.filePath || ca.fullPath }) });
        } catch (e) { AGY.api.showToast(e.message, 'error'); }
      });
    }

    if (bd.loaded) {
      if (bd.isColdArchive && st.backupTab !== 'sessions') {
        st.backupTab = 'sessions';
        el('bkTableHost').innerHTML = renderArchiveInnerTableHtml(bd);
      }
      var tabs = root.querySelectorAll('[data-bktab]');
      for (var i = 0; i < tabs.length; i++) {
        tabs[i].addEventListener('click', function () { st.backupTab = this.getAttribute('data-bktab'); renderBackup(); });
      }
      el('btnBkAutoPick').addEventListener('click', function () { preselectRecommendedBackupItems(bd); renderBackup(); });
      el('btnBkClearPick').addEventListener('click', function () {
        st.backupCheckedCaps = {};
        st.backupCheckedConvs = {};
        st.backupRestoreLegacyCli = false;
        renderBackup();
      });
      el('btnBkImportExec').addEventListener('click', function () { triggerBackupImport(false, false, this); });
      if (el('chkRestoreUserSettings')) {
        el('chkRestoreUserSettings').addEventListener('change', function () {
          st.backupRestoreUserSettings = this.checked;
          updateBackupImportBtn();
        });
      }
      if (el('chkRestoreLegacyCli')) {
        el('chkRestoreLegacyCli').addEventListener('change', function () {
          st.backupRestoreLegacyCli = this.checked;
          updateBackupImportBtn();
        });
      }
      el('txtBkSearch').addEventListener('input', function () {
        st.backupSearch = this.value;
        el('bkTableHost').innerHTML = renderArchiveInnerTableHtml(bd);
        bindArchiveTableEvents(bd);
      });
      bindArchiveTableEvents(bd);
      updateBackupImportBtn();
    }
  }

  function preselectRecommendedBackupItems(bd) {
    AGY.state.backupCheckedCaps = {};
    AGY.state.backupCheckedConvs = {};
    if (bd.isColdArchive) {
      var pgsC = bd.sessionDiffGroups || [];
      for (var pc = 0; pc < pgsC.length; pc++) {
        var lc = pgsC[pc].sessions || [];
        for (var sc = 0; sc < lc.length; sc++) {
          AGY.state.backupCheckedConvs[lc[sc].cid] = true;
        }
      }
      AGY.state.backupRestoreLegacyCli = !!bd.hasLegacyCliSummaryDb;
      return;
    }
    var caps = bd.capabilityDiffs || [];
    for (var i = 0; i < caps.length; i++) {
      if (caps[i].direction === 'RemoteOnly' || caps[i].direction === 'RemoteNewer')
        AGY.state.backupCheckedCaps[caps[i].key] = true;
    }
    var pgs = bd.sessionDiffGroups || [];
    for (var p = 0; p < pgs.length; p++) {
      var list = pgs[p].sessions || [];
      for (var s = 0; s < list.length; s++) {
        if (list[s].autoSelected) AGY.state.backupCheckedConvs[list[s].cid] = true;
      }
    }
  }

  async function triggerFullBackup(btn) {
    AGY.api.withButtonBusy(btn, '准备备份...', async function () {
      try {
        var res = await AGY.api.apiFetch('/api/backup/full', {
          method: 'POST',
          body: JSON.stringify({ tier: AGY.state.backupTier || 'fast', onlineHotBackup: true })
        });
        if (!res.cancelled) AGY.api.showToast('正在后台在线打包备份文件 (' + (AGY.state.backupTier === 'full' ? '完整备份' : '日常备份') + ')...', '');
      } catch (e) {
        AGY.api.showToast(e.message, 'error');
      }
    });
  }

  async function openBackupArchiveByPath(filePath, btn) {
    AGY.api.withButtonBusy(btn, '读取中...', async function () {
      try {
        var payload = filePath ? { archivePath: filePath } : {};
        var res = await AGY.api.apiFetch('/api/backup/open', { method: 'POST', body: JSON.stringify(payload) });
        if (res.cancelled) return;
        AGY.state.backupData = res;
        AGY.state.backupTab = res.isColdArchive ? 'sessions' : ((res.capabilityDiffs && res.capabilityDiffs.length > 0) ? 'caps' : 'sessions');
        preselectRecommendedBackupItems(res);
        if (AGY.state.view === 'backup') renderBackup();
        else AGY.app.switchView('backup');
        AGY.api.showToast('已打开备份文件：' + res.fileName + ' ✓', 'success');
      } catch (e) { AGY.api.showToast('打开备份文件失败：' + e.message, 'error'); }
    });
  }

  async function triggerBackupImport(closeIde, forceKill, btn) {
    var st = AGY.state;
    var bd = st.backupData || {};
    var cids = Object.keys(st.backupCheckedConvs).filter(function (k) { return st.backupCheckedConvs[k]; });
    var capKeys = Object.keys(st.backupCheckedCaps).filter(function (k) { return st.backupCheckedCaps[k]; });
    AGY.api.withButtonBusy(btn, '提交中...', async function () {
      try {
        await AGY.api.apiFetch('/api/backup/import', {
          method: 'POST',
          body: JSON.stringify({
            cids: cids,
            capKeys: capKeys,
            restoreUserSettings: !bd.isColdArchive && !!st.backupRestoreUserSettings,
            restoreLegacyCliArchive: !!(bd.hasLegacyCliSummaryDb && st.backupRestoreLegacyCli),
            closeIdeIfRunning: !!closeIde,
            forceKill: !!forceKill
          })
        });
        AGY.api.showToast('已开始后台恢复，请查看顶部进度条...', '');
      } catch (e) {
        if (!AGY.ui.handleIdeConflictError(
          e,
          '从备份文件 (.agy) 恢复到本地',
          function () { triggerBackupImport(true, false, null); },
          function () { triggerBackupImport(true, true, null); }
        )) { AGY.api.showToast(e.message, 'error'); }
      }
    });
  }

  AGY.views.backup = renderBackup;
  AGY.views.openBackupArchiveByPath = openBackupArchiveByPath;
})();
