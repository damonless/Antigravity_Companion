(function () {
  'use strict';

  var AGY = window.AGY = window.AGY || {};
  AGY.views = AGY.views || {};

  function getSessionStatusBadge(status) {
    switch (status) {
      case 'New': return '<span class="badge badge-blue">归档新增</span>';
      case 'Newer': return '<span class="badge badge-blue">归档较新</span>';
      case 'LocalNewer': return '<span class="badge badge-amber">本机较新</span>';
      case 'Modified': return '<span class="badge badge-accent">双向有差异</span>';
      case 'LocalOnly': return '<span class="badge badge-neutral">仅本机有</span>';
      case 'Aligned': return '<span class="badge badge-green">已对齐</span>';
      default: return '<span class="badge badge-neutral">' + AGY.ui.escapeHtml(status || '-') + '</span>';
    }
  }

  function getCapDirectionBadge(it) {
    if (it.direction === 'NeedsReview' || it.direction === 'Conflict') {
      return '<span class="badge badge-red">待人工核验</span>';
    }
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
      return '<table class="data-table"><thead><tr><th></th><th>能力项</th><th>差异方向</th><th>操作</th></tr></thead><tbody>' +
        (capList.map(function (c) {
          var chk = !!st.backupCheckedCaps[c.key];
          return '<tr class="' + (chk ? 'selected' : '') + '">' +
            '<td style="width:36px"><input type="checkbox" class="bk-cap-chk" data-key="' + esc(c.key) + '"' + (chk ? ' checked' : '') + ' /></td>' +
            '<td><span class="badge badge-neutral">' + esc(c.kind) + '</span> <strong>' + esc(c.displayName) + '</strong></td>' +
            '<td>' + getCapDirectionBadge(c) + '</td>' +
            '<td><button class="btn btn-xs" data-bk-diff="' + esc(c.key) + '">对比源码</button></td>' +
          '</tr>';
        }).join('') || '<tr><td colspan="4" class="empty-state">归档中无匹配的能力配置</td></tr>') +
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

        sRows.push(
          '<tr class="' + (chkS ? 'selected' : '') + '">' +
            '<td style="width:36px"><input type="checkbox" class="bk-conv-chk" data-cid="' + esc(s.cid) + '"' + (chkS ? ' checked' : '') + ' /></td>' +
            '<td>' +
              '<span class="badge badge-neutral">' + esc(s.projectName) + '</span> ' +
              (s.isSubagent ? '<span class="badge badge-neutral">子任务</span> ' : '') +
              '<strong>' + esc(s.title) + '</strong>' +
              (hasChildren ? ' <button class="btn btn-xs btn-ghost bk-toggle-sub" data-parent="' + esc(s.cid) + '">' + (isExpanded ? '▾ 收起 ' : '▸ 展开 ') + children.length + ' 个子任务</button>' : '') +
            '</td>' +
            '<td>' + esc(s.stepCount) + ' 步</td>' +
            '<td>' + getSessionStatusBadge(s.status) + '</td>' +
            '<td class="muted">' + esc(s.updatedTime) + '</td>' +
            '<td><button class="btn btn-xs" data-bk-md="' + esc(s.cid) + '">免导入预览</button></td>' +
          '</tr>'
        );

        if (hasChildren && isExpanded) {
          var subList = q ? matchingChildren : children;
          for (var ci = 0; ci < subList.length; ci++) {
            var ch = subList[ci];
            sRows.push(
              '<tr class="sub-row">' +
                '<td><span class="muted" title="随父会话自动闭环打包导入">↳</span></td>' +
                '<td><span class="badge badge-neutral">子任务</span> ' + esc(ch.title) + '</td>' +
                '<td>' + esc(ch.stepCount) + ' 步</td>' +
                '<td>' + getSessionStatusBadge(ch.status) + '</td>' +
                '<td class="muted">' + esc(ch.updatedTime) + '</td>' +
                '<td><button class="btn btn-xs" data-bk-md="' + esc(ch.cid) + '">预览</button></td>' +
              '</tr>'
            );
          }
        }
      }
    }

    return '<table class="data-table"><thead><tr><th></th><th>工程与会话标题</th><th>步数</th><th>相对本机</th><th>更新时间</th><th>操作</th></tr></thead><tbody>' +
      (sRows.join('') || '<tr><td colspan="6" class="empty-state">归档中无匹配的会话记录</td></tr>') +
      '</tbody></table>';
  }

  function updateBackupImportBtn() {
    var st = AGY.state;
    var btn = AGY.dom.el('btnBkImportExec');
    if (!btn) return;
    var selCapsCount = Object.keys(st.backupCheckedCaps).filter(function (k) { return st.backupCheckedCaps[k]; }).length;
    var selConvsCount = Object.keys(st.backupCheckedConvs).filter(function (k) { return st.backupCheckedConvs[k]; }).length;
    var extra = st.backupRestoreUserSettings ? ' + 全局配置' : '';
    btn.disabled = (selCapsCount + selConvsCount === 0) && !st.backupRestoreUserSettings;
    btn.textContent = '← 导入所选到本地 (' + selCapsCount + ' 能力 + ' + selConvsCount + ' 会话' + extra + ')';
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

  function renderBackupMigrationBody(subHost) {
    var el = AGY.dom.el;
    var st = AGY.state;
    var esc = AGY.ui.escapeHtml;
    var bd = st.backupData || { loaded: false };

    var hero =
      '<div class="action-grid">' +
        '<div class="card">' +
          '<div class="between wrap gap-sm" style="margin-bottom:8px">' +
            '<div><div class="quick-card-title">分级备份本机环境 (.agy)</div><div class="quick-card-desc">支持极速日常备份（跳过子代理 DB）与全量法证归档。</div></div>' +
            '<button class="btn btn-primary" id="btnTriggerFullBk">立即创建备份...</button>' +
          '</div>' +
          '<div class="pill-group">' +
            '<button class="filter-pill ' + (st.backupTier === 'fast' ? 'active' : '') + '" data-bktier="fast">FastDaily 极速日常</button>' +
            '<button class="filter-pill ' + (st.backupTier === 'full' ? 'active' : '') + '" data-bktier="full">FullForensic 全量法证</button>' +
          '</div>' +
        '</div>' +
        '<div class="quick-card">' +
          '<div>' +
            '<div class="quick-card-title">打开历史 / 跨机备份包 (.agy)</div>' +
            '<div class="quick-card-desc">免解包流式预览归档内会话与能力源码，支持父子折叠审阅与按需合流导入。</div>' +
          '</div>' +
          '<button class="btn" id="btnOpenAgyFile">选择 .agy 文件...</button>' +
        '</div>' +
      '</div>';

    var archiveSection = '';
    if (!bd.loaded) {
      archiveSection = '<div class="card empty-state">尚未打开任何 .agy 备份包。点击右上「选择 .agy 文件...」即可安全审阅其内容。</div>';
    } else {
      var capList = bd.capabilityDiffs || [];
      archiveSection =
        '<div class="card" style="display:flex;flex-direction:column;min-height:0">' +
          '<div class="between wrap gap-sm" style="margin-bottom:10px">' +
            '<div>' +
              '<strong>' + esc(bd.fileName) + '</strong> ' +
              (bd.backupTier ? '<span class="badge badge-neutral">' + esc(bd.backupTier) + '</span>' : '') +
              '<div class="muted mono" style="font-size:11.5px">来源用户: ' + esc(bd.sourceUser || '-') + ' · 时间: ' + esc(bd.backupTime || '-') + ' · 体积: ' + esc(bd.sizeMegabytes) + ' MB</div>' +
            '</div>' +
            '<div class="row gap-sm wrap">' +
              '<button class="btn btn-sm" id="btnBkAutoPick">仅选新增/较新</button>' +
              '<button class="btn btn-sm" id="btnBkClearPick">清空</button>' +
              '<button class="btn btn-sm btn-primary" id="btnBkImportExec">← 导入所选到本地</button>' +
            '</div>' +
          '</div>' +
          '<div class="between wrap gap-sm" style="margin-bottom:10px">' +
            '<div class="pill-group">' +
              '<button class="filter-pill ' + (st.backupTab === 'caps' ? 'active' : '') + '" data-bktab="caps">能力差异 (' + capList.length + ')</button>' +
              '<button class="filter-pill ' + (st.backupTab === 'sessions' ? 'active' : '') + '" data-bktab="sessions">工程与会话 (' + (bd.conversationsCount || 0) + ')</button>' +
            '</div>' +
            '<div class="row gap-sm wrap">' +
              '<label class="row gap-xs" style="font-size:12px;cursor:pointer"><input type="checkbox" id="chkRestoreUserSettings"' + (st.backupRestoreUserSettings ? ' checked' : '') + ' /> 同时恢复全局 userSettings 与插件开关</label>' +
              '<input class="input" id="txtBkSearch" style="width:190px" placeholder="筛选归档条目..." value="' + esc(st.backupSearch) + '" />' +
            '</div>' +
          '</div>' +
          '<div id="bkTableHost" style="max-height:420px;overflow:auto;border:1px solid var(--border);border-radius:8px">' + renderArchiveInnerTableHtml(bd) + '</div>' +
        '</div>';
    }

    subHost.innerHTML = hero + archiveSection;

    var tierBtns = subHost.querySelectorAll('[data-bktier]');
    for (var tb = 0; tb < tierBtns.length; tb++) {
      tierBtns[tb].addEventListener('click', function () {
        st.backupTier = this.getAttribute('data-bktier');
        for (var j = 0; j < tierBtns.length; j++) tierBtns[j].classList.remove('active');
        this.classList.add('active');
      });
    }

    el('btnTriggerFullBk').addEventListener('click', function () { triggerFullBackup(false); });
    el('btnOpenAgyFile').addEventListener('click', openBackupArchiveDialog);

    if (bd.loaded) {
      var tabs = subHost.querySelectorAll('[data-bktab]');
      for (var i = 0; i < tabs.length; i++) {
        tabs[i].addEventListener('click', function () { st.backupTab = this.getAttribute('data-bktab'); renderBackup(); });
      }
      el('btnBkAutoPick').addEventListener('click', function () { preselectRecommendedBackupItems(bd); renderBackup(); });
      el('btnBkClearPick').addEventListener('click', function () { st.backupCheckedCaps = {}; st.backupCheckedConvs = {}; renderBackup(); });
      el('btnBkImportExec').addEventListener('click', function () { triggerBackupImport(false); });
      el('chkRestoreUserSettings').addEventListener('change', function () {
        st.backupRestoreUserSettings = this.checked;
        updateBackupImportBtn();
      });
      el('txtBkSearch').addEventListener('input', function () {
        st.backupSearch = this.value;
        el('bkTableHost').innerHTML = renderArchiveInnerTableHtml(bd);
        bindArchiveTableEvents(bd);
      });
      bindArchiveTableEvents(bd);
      updateBackupImportBtn();
    }
  }

  function renderBackup() {
    var el = AGY.dom.el;
    var st = AGY.state;
    var isStorage = st.backupSubView === 'storage';

    var root = el('mainCanvas');
    root.innerHTML =
      '<div class="between wrap gap-sm" style="margin-bottom:14px">' +
        '<div class="pill-group" id="backupSubTabs">' +
          '<button class="filter-pill ' + (!isStorage ? 'active' : '') + '" data-bksub="backup" type="button">备份与迁移</button>' +
          '<button class="filter-pill ' + (isStorage ? 'active' : '') + '" data-bksub="storage" type="button">存储诊断与瘦身</button>' +
        '</div>' +
        '<button class="btn btn-sm btn-ghost" id="btnBackupGotoSettings" type="button">⚙ 定时快照与托盘设置 →</button>' +
      '</div>' +
      '<div id="backupSubHost"></div>';

    var subBtns = root.querySelectorAll('[data-bksub]');
    for (var i = 0; i < subBtns.length; i++) {
      subBtns[i].addEventListener('click', function () {
        var next = this.getAttribute('data-bksub');
        if (next === st.backupSubView) return;
        st.backupSubView = next;
        if (next === 'backup' && (st.dirty.backup || !st.backupData)) {
          renderBackup();
          AGY.app.fetchViewDataOnly('backup').then(function () {
            if (AGY.state.view === 'backup' && AGY.state.backupSubView === 'backup') renderBackup();
          }).catch(function () {});
        } else {
          renderBackup();
        }
      });
    }

    el('btnBackupGotoSettings').addEventListener('click', function () {
      AGY.app.switchView('settings');
    });

    var subHost = el('backupSubHost');
    if (isStorage) {
      if (AGY.views.renderStorage) AGY.views.renderStorage(subHost);
    } else {
      renderBackupMigrationBody(subHost);
    }
  }

  function preselectRecommendedBackupItems(bd) {
    AGY.state.backupCheckedCaps = {};
    AGY.state.backupCheckedConvs = {};
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

  async function triggerFullBackup(closeIde) {
    try {
      var res = await AGY.api.apiFetch('/api/backup/full', {
        method: 'POST',
        body: JSON.stringify({ tier: AGY.state.backupTier || 'fast', closeIdeIfRunning: !!closeIde })
      });
      if (!res.cancelled) AGY.api.showToast('正在后台打包 .agy 备份 (' + (AGY.state.backupTier === 'full' ? 'FullForensic' : 'FastDaily') + ')...', '');
    } catch (e) {
      if (e.status === 409 && e.data && e.data.code === 'IDE_RUNNING') {
        AGY.ui.confirmCloseIdeModal(e.data.parentCount, e.data.lsCount, '备份本机环境', function () { triggerFullBackup(true); });
      } else { AGY.api.showToast(e.message, 'error'); }
    }
  }

  async function openBackupArchiveDialog() {
    try {
      var res = await AGY.api.apiFetch('/api/backup/open', { method: 'POST', body: '{}' });
      if (res.cancelled) return;
      AGY.state.backupData = res;
      preselectRecommendedBackupItems(res);
      renderBackup();
      AGY.api.showToast('已解析归档包：' + res.fileName, 'success');
    } catch (e) { AGY.api.showToast('解析备份包失败：' + e.message, 'error'); }
  }

  async function triggerBackupImport(closeIde) {
    var st = AGY.state;
    var cids = Object.keys(st.backupCheckedConvs).filter(function (k) { return st.backupCheckedConvs[k]; });
    var capKeys = Object.keys(st.backupCheckedCaps).filter(function (k) { return st.backupCheckedCaps[k]; });
    try {
      await AGY.api.apiFetch('/api/backup/import', {
        method: 'POST',
        body: JSON.stringify({
          cids: cids,
          capKeys: capKeys,
          restoreUserSettings: !!st.backupRestoreUserSettings,
          closeIdeIfRunning: !!closeIde
        })
      });
      AGY.api.showToast('已提交选择性合流任务，请观察进度条...', '');
    } catch (e) {
      if (e.status === 409 && e.data && e.data.code === 'IDE_RUNNING') {
        AGY.ui.confirmCloseIdeModal(e.data.parentCount, e.data.lsCount, '从归档包选择性导入', function () { triggerBackupImport(true); });
      } else { AGY.api.showToast(e.message, 'error'); }
    }
  }

  AGY.views.backup = renderBackup;
})();
