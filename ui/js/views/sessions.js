(function () {
  'use strict';

  var AGY = window.AGY = window.AGY || {};
  AGY.views = AGY.views || {};

  function findLocalSessionById(cid) {
    var groups = (AGY.state.sessionsData && AGY.state.sessionsData.groups) || [];
    for (var i = 0; i < groups.length; i++) {
      var list = groups[i].sessions || [];
      for (var k = 0; k < list.length; k++) {
        if (list[k].cid === cid) return list[k];
      }
    }
    return null;
  }

  function renderSessions() {
    var el = AGY.dom.el;
    var st = AGY.state;
    var isRename = st.sessionsSubView === 'rename';
    var counts = (st.overview && st.overview.counts) || {};
    var actionable = counts.untaggedActionableCount !== undefined ? counts.untaggedActionableCount : (counts.untaggedFormalSessions || 0);

    var root = el('mainCanvas');
    root.innerHTML =
      '<div class="subview-shell">' +
        '<div class="subtab-bar">' +
          '<div class="pill-group" id="sessionsSubTabs">' +
            '<button class="filter-pill ' + (!isRename ? 'active' : '') + '" data-ssub="browse" type="button">对话列表</button>' +
            '<button class="filter-pill ' + (isRename ? 'active' : '') + '" data-ssub="rename" type="button">批量整理标题 (' + actionable + ')</button>' +
          '</div>' +
        '</div>' +
        '<div id="sessionsSubHost" class="subview-body"></div>' +
      '</div>';

    var subBtns = root.querySelectorAll('[data-ssub]');
    for (var i = 0; i < subBtns.length; i++) {
      subBtns[i].addEventListener('click', function () {
        var next = this.getAttribute('data-ssub');
        if (next === st.sessionsSubView) return;
        st.sessionsSubView = next;
        if ((next === 'rename' && (st.dirty.rename || !st.renameData)) ||
            (next === 'browse' && (st.dirty.sessions || !st.sessionsData))) {
          renderSessions();
          AGY.app.fetchViewDataOnly('sessions').then(function () {
            if (AGY.state.view === 'sessions' && AGY.state.sessionsSubView === next) renderSessions();
          }).catch(function () {});
        } else {
          renderSessions();
        }
      });
    }

    var subHost = el('sessionsSubHost');
    if (isRename) {
      if (AGY.views.renderRename) AGY.views.renderRename(subHost);
    } else {
      renderBrowseSessions(subHost);
    }
  }

  function collectVisibleSessions() {
    var st = AGY.state;
    var sd = st.sessionsData || {};
    var groups = sd.groups || [];
    var visible = [];
    var selectedGroupObj = null;

    for (var gi = 0; gi < groups.length; gi++) {
      var g = groups[gi];
      if (st.selectedBucketKey === '__ALL__') {
        if (g.groupKind !== 'formal') continue;
      } else if (st.selectedBucketKey === '__LEGACY_CLI__') {
        if (g.groupKind !== 'cli') continue;
      } else if (g.bucketKey !== st.selectedBucketKey) {
        continue;
      } else {
        selectedGroupObj = g;
      }

      var list = g.sessions || [];
      for (var si = 0; si < list.length; si++) {
        var s = list[si];
        if (st.selectedBucketKey === '__LEGACY_CLI__' && !s.isLegacyCli) continue;
        if (st.selectedBucketKey === 'AUX:CLI' && s.isLegacyCli) continue;

        if (st.sessionSearchQuery.trim().length > 0) {
          var q = st.sessionSearchQuery.trim().toLowerCase();
          var inMatchedSet = st.sessionSearchMatchedIds ? !!st.sessionSearchMatchedIds[s.cid] : false;
          var localHit = (s.title && s.title.toLowerCase().indexOf(q) >= 0) ||
                         (s.projectName && s.projectName.toLowerCase().indexOf(q) >= 0);
          if (st.sessionSearchContent) {
            if (!inMatchedSet && !localHit) continue;
          } else if (!localHit) {
            continue;
          }
        }
        visible.push(s);
      }
    }

    visible.sort(function (x, y) { return String(y.updatedIso || '').localeCompare(String(x.updatedIso || '')); });
    return { visible: visible, selectedGroupObj: selectedGroupObj };
  }

  function renderSessionRowsHtml(visible) {
    var st = AGY.state;
    var esc = AGY.ui.escapeHtml;
    var ICONS = AGY.ui.ICONS;
    if (visible.length === 0) {
      return '<div class="empty-state">当前筛选条件下未找到匹配对话。' +
        (st.sessionSearchQuery ? '<div style="margin-top:8px"><button class="btn btn-sm" id="btnClearEmptySearch">清空搜索词</button></div>' : '') +
        '</div>';
    }
    return visible.map(function (row) {
      var isReadOnly = !!row.isLegacyCli;
      var isChecked = !isReadOnly && !!st.checkedSessions[row.cid];
      var projText = (row.projectName === 'Outside of Project' || !row.projectName) ? '未关联项目' : row.projectName;
      return '<div class="session-row ' + (isChecked ? 'selected' : '') + '" data-cid="' + esc(row.cid) + '">' +
        (isReadOnly
          ? ''
          : '<input type="checkbox" class="sess-chk" data-cid="' + esc(row.cid) + '"' + (isChecked ? ' checked' : '') + ' />') +
        '<div class="session-main" data-preview-cid="' + esc(row.cid) + '">' +
          '<div class="session-title">' + esc(row.title) + '</div>' +
          '<div class="session-meta">' +
            '<span class="badge badge-neutral">' + esc(projText) + '</span>' +
            (row.isCli ? '<span class="badge badge-blue">CLI 对话</span>' : '') +
            (isReadOnly ? '<span class="badge badge-neutral">旧版 CLI 历史 (只读)</span>' : '') +
            (row.isRunning ? '<span class="badge badge-amber">进行中 (自动跳过)</span>' : '') +
            '<span>' + esc(row.stepCount) + ' 步</span>' +
            '<span>' + esc(row.updatedTime) + '</span>' +
          '</div>' +
        '</div>' +
        '<div class="session-actions">' +
          '<button class="btn-icon" data-sess-act="preview" data-cid="' + esc(row.cid) + '" title="阅读对话内容" type="button">' + ICONS.eye + '</button>' +
          (!isReadOnly ? '<button class="btn-icon" data-sess-act="rename" data-cid="' + esc(row.cid) + '" title="修改标题" type="button">' + ICONS.edit + '</button>' : '') +
          (!isReadOnly ? '<button class="btn-icon" data-sess-act="move" data-cid="' + esc(row.cid) + '" title="移动到其他项目" type="button">' + ICONS.folder + '</button>' : '') +
          '<button class="btn-icon" data-sess-act="exportmd" data-cid="' + esc(row.cid) + '" title="导出为 Markdown (.md)" type="button">' + ICONS.download + '</button>' +
        '</div>' +
      '</div>';
    }).join('');
  }

  function updateSessionBatchBar() {
    var el = AGY.dom.el;
    var st = AGY.state;
    var checkedCids = Object.keys(st.checkedSessions).filter(function (id) { return st.checkedSessions[id]; });
    var bar = el('sessBatchBar');
    if (!bar) return;
    if (checkedCids.length === 0) {
      bar.classList.add('hidden');
    } else {
      bar.classList.remove('hidden');
      AGY.dom.setText('lblSessBatchCount', '已选 ' + checkedCids.length + ' 条对话');
    }
  }

  function patchSessionListPartial() {
    var el = AGY.dom.el;
    var info = collectVisibleSessions();
    var scrollHost = el('sessScrollHost');
    if (!scrollHost) return;
    var prevScroll = scrollHost.scrollTop;
    scrollHost.innerHTML = renderSessionRowsHtml(info.visible);
    scrollHost.scrollTop = prevScroll;
    AGY.dom.setText('lblSessHitCount', '命中 ' + info.visible.length + ' 条');
    bindSessionRowEvents();
    updateSessionBatchBar();
  }

  function bindSessionRowEvents() {
    var scrollHost = AGY.dom.el('sessScrollHost');
    if (!scrollHost) return;
    var st = AGY.state;

    var clrEmpty = scrollHost.querySelector('#btnClearEmptySearch');
    if (clrEmpty) {
      clrEmpty.addEventListener('click', function () {
        st.sessionSearchQuery = '';
        st.sessionSearchMatchedIds = null;
        var inp = AGY.dom.el('txtSessionSearch');
        if (inp) inp.value = '';
        patchSessionListPartial();
      });
    }

    var chks = scrollHost.querySelectorAll('.sess-chk');
    for (var ci = 0; ci < chks.length; ci++) {
      chks[ci].addEventListener('change', function () {
        var id = this.getAttribute('data-cid');
        st.checkedSessions[id] = this.checked;
        AGY.dom.patchCheckboxRow(this.closest('.session-row'), this.checked);
        updateSessionBatchBar();
      });
    }

    var mains = scrollHost.querySelectorAll('[data-preview-cid]');
    for (var mi = 0; mi < mains.length; mi++) {
      mains[mi].addEventListener('click', function () {
        AGY.ui.openMarkdownDrawer(this.getAttribute('data-preview-cid'), 'local');
      });
    }

    var actBtns = scrollHost.querySelectorAll('[data-sess-act]');
    for (var ai = 0; ai < actBtns.length; ai++) {
      actBtns[ai].addEventListener('click', function (ev) {
        ev.stopPropagation();
        var act = this.getAttribute('data-sess-act');
        var cid = this.getAttribute('data-cid');
        var sessObj = findLocalSessionById(cid);
        if (act === 'preview') AGY.ui.openMarkdownDrawer(cid, 'local');
        else if (act === 'rename') openSingleRenameModal(sessObj);
        else if (act === 'move') openMoveSessionsModal([cid]);
        else if (act === 'exportmd') exportSessionMarkdown(cid, 'local', this);
      });
    }
  }

  function gotoRenameWithScope(cids, selectedGroupObj) {
    var st = AGY.state;
    if (cids && cids.length > 0) {
      var newChecked = {};
      var newScoped = {};
      var firstCid = '';
      for (var j = 0; j < cids.length; j++) {
        var sObj = findLocalSessionById(cids[j]);
        if (sObj && sObj.isLegacyCli) continue;
        if (!firstCid) firstCid = cids[j];
        newScoped[cids[j]] = true;
        if (!sObj || !sObj.isRunning) {
          newChecked[cids[j]] = true;
        }
      }
      st.renameChecked = newChecked;
      st.renameScopedCids = newScoped;
      st.renameProjectFilter = '__SCOPED__';
      st.renameFocusedCid = firstCid || cids[0];
    } else {
      st.renameScopedCids = {};
      st.renameProjectFilter = (selectedGroupObj && selectedGroupObj.groupKind === 'formal' && selectedGroupObj.projectName)
        ? selectedGroupObj.projectName
        : '__ALL__';
    }
    st.renameStep = 1;
    st.sessionsSubView = 'rename';
    AGY.app.switchView('sessions');
  }

  function renderBrowseSessions(hostEl) {
    var el = AGY.dom.el;
    var st = AGY.state;
    var esc = AGY.ui.escapeHtml;
    var ICONS = AGY.ui.ICONS;
    var sd = st.sessionsData;
    var host = hostEl || el('sessionsSubHost') || el('mainCanvas');

    if (!sd) {
      host.innerHTML = '<div class="empty-state">正在加载项目与对话列表...</div>';
      return;
    }

    var groups = sd.groups || [];
    var formalGroups = [];
    var auxGroups = [];
    var legacyCliCount = 0;

    for (var i = 0; i < groups.length; i++) {
      var g = groups[i];
      if (g.groupKind === 'formal') {
        formalGroups.push(g);
      } else {
        if (g.bucketKey === 'AUX:CLI') {
          var activeCli = 0;
          var list = g.sessions || [];
          for (var si = 0; si < list.length; si++) {
            if (list[si].isLegacyCli) legacyCliCount++;
            else activeCli++;
          }
          auxGroups.push({
            bucketKey: g.bucketKey,
            projectName: 'CLI 对话',
            shortPath: g.shortPath,
            totalCount: activeCli
          });
        } else {
          var auxLabel = g.projectName;
          if (g.bucketKey === 'AUX:OUTSIDE' || auxLabel === 'Outside of Project') auxLabel = '未关联项目';
          else if (g.bucketKey === 'AUX:SUBAGENT') auxLabel = '后台子任务';
          auxGroups.push({
            bucketKey: g.bucketKey,
            projectName: auxLabel,
            shortPath: g.shortPath,
            totalCount: g.totalCount
          });
        }
      }
    }

    var projCardsHtml = [];
    projCardsHtml.push(
      '<div class="proj-card ' + (st.selectedBucketKey === '__ALL__' ? 'active' : '') + '" data-bucket="__ALL__">' +
        '<div class="proj-card-top"><span class="proj-card-name">全部项目主对话</span><span class="badge badge-accent">' + esc(sd.formalPrimarySessions || 0) + '</span></div>' +
        '<div class="proj-card-path">' + esc(sd.formalProjectsCount || 0) + ' 个项目（不含后台子任务）</div>' +
      '</div>'
    );

    for (var f = 0; f < formalGroups.length; f++) {
      var fg = formalGroups[f];
      var subLabel = fg.isDuplicateBasename && fg.distinguishingLabel ? fg.distinguishingLabel : fg.shortPath;
      projCardsHtml.push(
        '<div class="proj-card ' + (st.selectedBucketKey === fg.bucketKey ? 'active' : '') + '" data-bucket="' + esc(fg.bucketKey) + '" title="' + esc(fg.fullPath || '') + '">' +
          '<div class="proj-card-top">' +
            '<span class="proj-card-name">' + esc(fg.projectName) + '</span>' +
            '<span class="badge badge-neutral">' + esc(fg.primaryCount) + '</span>' +
          '</div>' +
          '<div class="proj-card-path">' + esc(subLabel) + '</div>' +
        '</div>'
      );
    }

    if (auxGroups.length > 0) {
      projCardsHtml.push(
        '<div class="aux-divider" id="btnToggleAuxGroups">' +
          '<span>其他分组 (CLI / 未关联项目 / 后台子任务)</span>' +
          '<span>' + (st.auxExpanded ? '▾' : '▸') + '</span>' +
        '</div>'
      );
      if (st.auxExpanded) {
        for (var a = 0; a < auxGroups.length; a++) {
          var ag = auxGroups[a];
          projCardsHtml.push(
            '<div class="proj-card ' + (st.selectedBucketKey === ag.bucketKey ? 'active' : '') + '" data-bucket="' + esc(ag.bucketKey) + '">' +
              '<div class="proj-card-top">' +
                '<span class="proj-card-name">' + esc(ag.projectName) + '</span>' +
                '<span class="badge badge-neutral">' + esc(ag.totalCount) + '</span>' +
              '</div>' +
              '<div class="proj-card-path">' + esc(ag.shortPath) + '</div>' +
            '</div>'
          );
        }
      }
    }

    if (legacyCliCount > 0) {
      projCardsHtml.push(
        '<div class="aux-divider" id="btnToggleLegacyCliGroup">' +
          '<span>旧版 CLI 只读历史 (' + legacyCliCount + ')</span>' +
          '<span>' + (st.legacyCliExpanded ? '▾' : '▸') + '</span>' +
        '</div>'
      );
      if (st.legacyCliExpanded) {
        projCardsHtml.push(
          '<div class="proj-card ' + (st.selectedBucketKey === '__LEGACY_CLI__' ? 'active' : '') + '" data-bucket="__LEGACY_CLI__">' +
            '<div class="proj-card-top"><span class="proj-card-name">antigravity-cli 历史记录</span><span class="badge badge-neutral">' + legacyCliCount + '</span></div>' +
            '<div class="proj-card-path">只读历史摘要库</div>' +
          '</div>'
        );
      }
    }

    var info = collectVisibleSessions();
    var selectedGroupObj = info.selectedGroupObj;
    var isFormalSelected = !!(selectedGroupObj && selectedGroupObj.groupKind === 'formal');
    var projMaintMenu = isFormalSelected
      ? '<div class="dropdown-wrap">' +
          '<button class="btn btn-sm" id="btnProjMaintMenu" type="button">项目维护 ⋯</button>' +
          '<div class="dropdown-menu hidden" id="projMaintDropdown">' +
            '<button class="dropdown-item" id="btnRelocateCurProj" type="button">重新关联本地目录...</button>' +
            (selectedGroupObj.projectId ? '<div class="dropdown-divider"></div><button class="dropdown-item danger" id="btnDeleteCurProj" type="button">移除项目记录...</button>' : '') +
          '</div>' +
        '</div>'
      : '';

    var topbar = el('topbarActions');
    if (topbar) {
      topbar.innerHTML = '<button class="btn btn-sm btn-primary" id="btnSessionsGotoRename" type="button">去批量整理标题 →</button>';
      el('btnSessionsGotoRename').addEventListener('click', function () {
        var cids = Object.keys(st.checkedSessions).filter(function (id) { return st.checkedSessions[id]; });
        gotoRenameWithScope(cids, selectedGroupObj);
      });
    }

    var isLegacyCliBucket = st.selectedBucketKey === '__LEGACY_CLI__';

    host.innerHTML =
      '<div class="sessions-layout" style="flex:1;min-height:0">' +
        '<section class="project-pane">' +
          '<div class="pane-header"><span>项目列表</span><span class="muted" style="font-weight:500">' + formalGroups.length + '</span></div>' +
          '<div class="project-scroll">' + projCardsHtml.join('') + '</div>' +
        '</section>' +
        '<section class="session-pane">' +
          '<div class="session-toolbar">' +
            '<div class="search-box">' +
              '<span class="muted" style="display:flex">' + ICONS.search + '</span>' +
              '<input id="txtSessionSearch" type="text" placeholder="搜索对话标题、项目名' + (st.sessionSearchContent ? '或对话正文...' : '...') + '" value="' + esc(st.sessionSearchQuery) + '" />' +
              '<button class="btn-icon ' + (st.sessionSearchQuery ? '' : 'hidden') + '" id="btnClearSessSearch" style="width:20px;height:20px" title="清空" type="button">✕</button>' +
            '</div>' +
            '<button class="filter-pill ' + (st.sessionSearchContent ? 'active' : '') + '" id="btnToggleSearchContent" type="button">搜正文内容</button>' +
            '<span class="muted" id="lblSessHitCount" style="font-size:12px">命中 ' + info.visible.length + ' 条</span>' +
            (isLegacyCliBucket
              ? '<span class="badge badge-neutral">只读历史记录（仅支持阅读与导出）</span>'
              : '<button class="btn btn-sm" id="btnSessSelectVisible" type="button">全选当前</button>' +
                '<button class="btn btn-sm" id="btnSessClearChecked" type="button">清空勾选</button>') +
            projMaintMenu +
          '</div>' +
          '<div class="session-scroll" id="sessScrollHost">' + renderSessionRowsHtml(info.visible) + '</div>' +
          '<div class="floating-batch-bar hidden" id="sessBatchBar">' +
            '<div class="row gap-sm"><strong id="lblSessBatchCount">已选 0 条对话</strong></div>' +
            '<div class="row gap-sm">' +
              '<button class="btn btn-sm btn-primary" id="btnBatchGotoRename" type="button">批量整理标题 →</button>' +
              '<button class="btn btn-sm" id="btnBatchMove" type="button">更换所属项目</button>' +
              '<button class="btn btn-sm" id="btnBatchExportAgy" type="button">导出备份文件 (.agy)</button>' +
              '<button class="btn btn-sm btn-danger" id="btnBatchDelete" type="button">移入回收站</button>' +
            '</div>' +
          '</div>' +
        '</section>' +
      '</div>';

    var projCards = host.querySelectorAll('.proj-card[data-bucket]');
    for (var p = 0; p < projCards.length; p++) {
      projCards[p].addEventListener('click', function () {
        st.selectedBucketKey = this.getAttribute('data-bucket');
        renderBrowseSessions(host);
      });
    }
    if (el('btnToggleAuxGroups')) {
      el('btnToggleAuxGroups').addEventListener('click', function () { st.auxExpanded = !st.auxExpanded; renderBrowseSessions(host); });
    }
    if (el('btnToggleLegacyCliGroup')) {
      el('btnToggleLegacyCliGroup').addEventListener('click', function () { st.legacyCliExpanded = !st.legacyCliExpanded; renderBrowseSessions(host); });
    }
    if (el('btnProjMaintMenu')) {
      el('btnProjMaintMenu').addEventListener('click', function (ev) {
        ev.stopPropagation();
        var dd = el('projMaintDropdown');
        var wasOpen = dd && !dd.classList.contains('hidden');
        AGY.ui.closeAllDropdowns();
        if (dd && !wasOpen) dd.classList.remove('hidden');
      });
    }
    if (el('btnRelocateCurProj')) {
      el('btnRelocateCurProj').addEventListener('click', function (ev) {
        ev.stopPropagation();
        AGY.ui.closeAllDropdowns();
        openRelocateProjectModal(selectedGroupObj);
      });
    }
    if (el('btnDeleteCurProj')) {
      el('btnDeleteCurProj').addEventListener('click', function (ev) {
        ev.stopPropagation();
        AGY.ui.closeAllDropdowns();
        openDeleteProjectModal(selectedGroupObj);
      });
    }

    var searchInput = el('txtSessionSearch');
    var searchTimer = null;
    searchInput.addEventListener('input', function () {
      st.sessionSearchQuery = this.value;
      var clrBtn = el('btnClearSessSearch');
      if (clrBtn) clrBtn.classList.toggle('hidden', !st.sessionSearchQuery);
      clearTimeout(searchTimer);
      searchTimer = setTimeout(async function () {
        if (st.sessionSearchContent && st.sessionSearchQuery.trim().length > 0) {
          try {
            var sRes = await AGY.api.apiFetch('/api/sessions/search', {
              method: 'POST',
              body: JSON.stringify({ query: st.sessionSearchQuery, searchContent: true })
            });
            var map = {};
            var ids = sRes.matchedIds || [];
            for (var x = 0; x < ids.length; x++) map[ids[x]] = true;
            st.sessionSearchMatchedIds = map;
          } catch (_e) {}
        } else {
          st.sessionSearchMatchedIds = null;
        }
        patchSessionListPartial();
      }, 160);
    });

    el('btnClearSessSearch').addEventListener('click', function () {
      st.sessionSearchQuery = '';
      st.sessionSearchMatchedIds = null;
      searchInput.value = '';
      this.classList.add('hidden');
      patchSessionListPartial();
    });

    el('btnToggleSearchContent').addEventListener('click', async function () {
      st.sessionSearchContent = !st.sessionSearchContent;
      this.classList.toggle('active', st.sessionSearchContent);
      searchInput.placeholder = '搜索对话标题、项目名' + (st.sessionSearchContent ? '或对话正文...' : '...');
      if (st.sessionSearchContent && st.sessionSearchQuery.trim().length > 0) {
        var sRes = await AGY.api.apiFetch('/api/sessions/search', {
          method: 'POST',
          body: JSON.stringify({ query: st.sessionSearchQuery, searchContent: true })
        });
        var map = {};
        var ids = sRes.matchedIds || [];
        for (var x = 0; x < ids.length; x++) map[ids[x]] = true;
        st.sessionSearchMatchedIds = map;
      } else {
        st.sessionSearchMatchedIds = null;
      }
      patchSessionListPartial();
    });

    if (el('btnSessSelectVisible')) {
      el('btnSessSelectVisible').addEventListener('click', function () {
        var curVis = collectVisibleSessions().visible;
        for (var j = 0; j < curVis.length; j++) {
          if (!curVis[j].isLegacyCli) st.checkedSessions[curVis[j].cid] = true;
        }
        patchSessionListPartial();
      });
    }
    if (el('btnSessClearChecked')) {
      el('btnSessClearChecked').addEventListener('click', function () {
        st.checkedSessions = {};
        patchSessionListPartial();
      });
    }

    el('btnBatchGotoRename').addEventListener('click', function () {
      var cids = Object.keys(st.checkedSessions).filter(function (id) { return st.checkedSessions[id]; });
      if (cids.length === 0) return;
      gotoRenameWithScope(cids, selectedGroupObj);
    });
    el('btnBatchMove').addEventListener('click', function () {
      openMoveSessionsModal(Object.keys(st.checkedSessions).filter(function (id) { return st.checkedSessions[id]; }));
    });
    el('btnBatchExportAgy').addEventListener('click', function () {
      exportSelectiveAgyModal(Object.keys(st.checkedSessions).filter(function (id) { return st.checkedSessions[id]; }), []);
    });
    el('btnBatchDelete').addEventListener('click', function () {
      openDeleteSessionsModal(Object.keys(st.checkedSessions).filter(function (id) { return st.checkedSessions[id]; }));
    });

    bindSessionRowEvents();
    updateSessionBatchBar();
  }

  function openDeleteProjectModal(groupOrId, maybeName) {
    var esc = AGY.ui.escapeHtml;
    var projectId = typeof groupOrId === 'string' ? groupOrId : (groupOrId && groupOrId.projectId) || '';
    var projectName = typeof groupOrId === 'string' ? (maybeName || projectId) : (groupOrId && groupOrId.projectName) || projectId;
    var groupObj = typeof groupOrId === 'object' ? groupOrId : null;
    if (!groupObj && AGY.state.sessionsData && AGY.state.sessionsData.groups) {
      var allG = AGY.state.sessionsData.groups;
      for (var i = 0; i < allG.length; i++) {
        if (allG[i].projectId === projectId) { groupObj = allG[i]; break; }
      }
    }

    var sessList = (groupObj && groupObj.sessions) || [];
    var primaryCount = (groupObj && groupObj.primaryCount !== undefined) ? groupObj.primaryCount : sessList.length;
    var subCount = (groupObj && groupObj.subagentCount !== undefined) ? groupObj.subagentCount : 0;
    var previewRows = sessList.slice(0, 12).map(function (s) {
      return '<div class="del-manifest-row"><span>' + esc(s.title) + '</span><span class="muted" style="white-space:nowrap">' + esc(s.stepCount || 0) + ' 步</span></div>';
    }).join('');
    if (sessList.length > 12) {
      previewRows += '<div class="muted" style="padding-top:4px">……以及其他 ' + (sessList.length - 12) + ' 条对话</div>';
    }

    AGY.ui.openModal(
      '移除项目记录 — ' + projectName,
      '<div class="alert-banner success" style="margin-bottom:10px">🛡 <strong>安全提示：</strong>不会删除您磁盘上的源代码目录；移除的项目记录与对话会先进入 <strong>回收站</strong>，30 天内随时可以一键还原。</div>' +
      '<div style="line-height:1.7;margin-bottom:6px">' +
        '<div>项目名称：<strong>' + esc(projectName) + '</strong></div>' +
        (groupObj && groupObj.fullPath ? '<div class="mono muted" style="font-size:11.5px">关联目录（不会被删除）：' + esc(groupObj.fullPath) + '</div>' : '') +
        '<div>将移入回收站：<strong>' + esc(primaryCount) + '</strong> 条主对话' + (subCount > 0 ? (' + <strong>' + esc(subCount) + '</strong> 条关联后台子任务') : '') + ' 及项目配置记录</div>' +
      '</div>' +
      (previewRows ? '<div class="del-manifest-list">' + previewRows + '</div>' : ''),
      '<button class="btn" data-act="cancel">取消</button><button class="btn btn-danger" data-act="go">移入回收站</button>',
      function (_mb, mf) {
        mf.querySelector('[data-act="cancel"]').addEventListener('click', AGY.ui.closeModal);
        var goBtn = mf.querySelector('[data-act="go"]');
        goBtn.addEventListener('click', function () {
          async function doDel(closeIde, forceKill) {
            AGY.api.withButtonBusy(goBtn, '处理中...', async function () {
              try {
                var res = await AGY.api.apiFetch('/api/sessions/delete', {
                  method: 'POST',
                  body: JSON.stringify({ projectIds: [projectId], closeIdeIfRunning: !!closeIde, forceKill: !!forceKill })
                });
                AGY.ui.closeModal();
                AGY.state.selectedBucketKey = '__ALL__';
                await AGY.app.refreshAll('已将项目及 ' + res.deletedSessions + ' 条对话移入回收站（释放 ' + res.freedMegabytes + ' MB）✓');
              } catch (e) {
                if (!AGY.ui.handleIdeConflictError(
                  e,
                  '移除项目 [' + projectName + '] 的对话记录',
                  function () { doDel(true, false); },
                  function () { doDel(true, true); }
                )) {
                  AGY.api.showToast(e.message, 'error');
                }
              }
            });
          }
          doDel(false, false);
        });
      }
    );
  }

  async function exportSessionMarkdown(cid, source, btn) {
    AGY.api.withButtonBusy(btn, '导出中...', async function () {
      try {
        var res = await AGY.api.apiFetch('/api/sessions/export-md', {
          method: 'POST',
          body: JSON.stringify({ cid: cid, source: source || 'local' })
        });
        if (!res.cancelled) AGY.api.showToast('已导出 Markdown 至 ' + res.outputPath, 'success');
      } catch (e) { AGY.api.showToast('导出失败：' + e.message, 'error'); }
    });
  }

  function openSingleRenameModal(sessObj) {
    if (!sessObj) return;
    AGY.ui.openModal(
      '修改对话标题',
      '<div class="muted" style="margin-bottom:8px">自动保留改名撤销记录。即使 Antigravity 主窗口正在运行也会立即保存，主窗口退出后自动补刷侧边栏。</div>' +
      '<label class="field-label">新对话标题</label>' +
      '<input class="input" id="txtSingleNewTitle" value="' + AGY.ui.escapeHtml(sessObj.title) + '" />',
      '<button class="btn" data-act="cancel">取消</button><button class="btn btn-primary" data-act="save">保存标题</button>',
      function (mb, mf) {
        var inp = mb.querySelector('#txtSingleNewTitle');
        inp.focus();
        inp.setSelectionRange(inp.value.length, inp.value.length);
        mf.querySelector('[data-act="cancel"]').addEventListener('click', AGY.ui.closeModal);
        var saveBtn = mf.querySelector('[data-act="save"]');
        saveBtn.addEventListener('click', function () {
          submitSingleRename(sessObj.cid, inp.value, 'SmartAuto', false, saveBtn);
        });
      }
    );
  }

  async function submitSingleRename(cid, newTitle, strategy, forceKill, btn) {
    if (!String(newTitle || '').trim()) { AGY.api.showToast('新标题不能为空', 'error'); return; }
    AGY.api.withButtonBusy(btn, '保存中...', async function () {
      try {
        var res = await AGY.api.apiFetch('/api/sessions/rename-single', {
          method: 'POST',
          body: JSON.stringify({ cid: cid, newTitle: newTitle.trim(), strategy: strategy || 'SmartAuto', preferHotWrite: true, forceKill: !!forceKill })
        });
        AGY.ui.closeModal();
        var msg = res.deferredToPbFlush
          ? '标题已保存，关闭 Antigravity 主窗口后会自动更新侧边栏 ✓'
          : '对话标题已更新 ✓';
        await AGY.app.refreshAll(msg);
      } catch (e) {
        if (e.status === 409 && e.data && e.data.code === 'IDE_CLOSE_TIMEOUT') {
          AGY.ui.confirmForceKillIdeModal(e.data.parentCount, e.data.lsCount, '修改对话标题', function () {
            submitSingleRename(cid, newTitle, 'CloseAndFullWrite', true, null);
          });
        } else if (e.status === 409 && e.data && e.data.code === 'IDE_RUNNING') {
          AGY.ui.openDualModeRenameModal(e.data.parentCount, e.data.lsCount, function (chosenStrategy) {
            submitSingleRename(cid, newTitle, chosenStrategy, false, null);
          });
        } else { AGY.api.showToast(e.message, 'error'); }
      }
    });
  }

  function openMoveSessionsModal(cids) {
    if (!cids || cids.length === 0) return;
    var esc = AGY.ui.escapeHtml;
    var groups = (AGY.state.sessionsData && AGY.state.sessionsData.groups) || [];
    var opts = [];
    for (var i = 0; i < groups.length; i++) {
      if (groups[i].groupKind === 'formal') {
        opts.push({ projectId: groups[i].projectId, projectName: groups[i].projectName, fullPath: groups[i].fullPath, label: groups[i].projectName + ' (' + groups[i].shortPath + ')' });
      }
    }
    opts.push({ projectId: 'outside-of-project', projectName: 'Outside of Project', fullPath: '', label: '未关联项目 (全局独立对话)' });

    var optHtml = opts.map(function (o, idx) {
      return '<option value="' + idx + '">' + esc(o.label) + '</option>';
    }).join('') + '<option value="__CUSTOM__">+ 手动指定新项目目录...</option>';

    AGY.ui.openModal(
      '更换 ' + cids.length + ' 条对话的所属项目',
      '<div style="display:flex;flex-direction:column;gap:10px">' +
        '<div><label class="field-label">选择目标项目</label><select class="select" id="selMoveTarget">' + optHtml + '</select></div>' +
        '<div><label class="field-label">目标项目名称</label><input class="input" id="txtMoveName" value="' + esc(opts[0] ? opts[0].projectName : '') + '" /></div>' +
        '<div><label class="field-label">目标本地目录</label><div class="row gap-sm"><input class="input" id="txtMovePath" value="' + esc(opts[0] ? opts[0].fullPath : '') + '" /><button class="btn" id="btnMoveBrowse" type="button">浏览...</button></div></div>' +
      '</div>',
      '<button class="btn" data-act="cancel">取消</button><button class="btn btn-primary" data-act="go">确认移动</button>',
      function (mb, mf) {
        var sel = mb.querySelector('#selMoveTarget');
        var inpName = mb.querySelector('#txtMoveName');
        var inpPath = mb.querySelector('#txtMovePath');
        var targetId = opts[0] ? opts[0].projectId : '';

        sel.addEventListener('change', function () {
          if (this.value === '__CUSTOM__') {
            targetId = ''; inpName.value = ''; inpPath.value = ''; inpPath.disabled = false;
          } else {
            var chosen = opts[parseInt(this.value, 10)];
            targetId = chosen.projectId; inpName.value = chosen.projectName; inpPath.value = chosen.fullPath;
            inpPath.disabled = chosen.projectId === 'outside-of-project';
          }
        });
        mb.querySelector('#btnMoveBrowse').addEventListener('click', async function () {
          var pick = await AGY.api.apiFetch('/api/native/pick-folder', { method: 'POST', body: JSON.stringify({ title: '选择目标项目目录', initialPath: inpPath.value }) });
          if (!pick.cancelled && pick.path) {
            inpPath.value = pick.path;
            if (!inpName.value.trim()) inpName.value = pick.path.split(/[\\/]/).pop() || '';
          }
        });
        mf.querySelector('[data-act="cancel"]').addEventListener('click', AGY.ui.closeModal);
        var goBtn = mf.querySelector('[data-act="go"]');
        goBtn.addEventListener('click', function () {
          async function doMove(closeIde, forceKill) {
            AGY.api.withButtonBusy(goBtn, '移动中...', async function () {
              try {
                await AGY.api.apiFetch('/api/sessions/move', {
                  method: 'POST',
                  body: JSON.stringify({ cids: cids, targetProjectId: targetId, targetProjectName: inpName.value.trim(), targetFolderPath: inpPath.value.trim(), closeIdeIfRunning: !!closeIde, forceKill: !!forceKill })
                });
                AGY.ui.closeModal();
                AGY.state.checkedSessions = {};
                await AGY.app.refreshAll('已更换对话所属项目 ✓');
              } catch (e) {
                if (!AGY.ui.handleIdeConflictError(
                  e,
                  '更换对话所属项目',
                  function () { doMove(true, false); },
                  function () { doMove(true, true); }
                )) { AGY.api.showToast(e.message, 'error'); }
              }
            });
          }
          doMove(false, false);
        });
      }
    );
  }

  function openRelocateProjectModal(groupObj) {
    if (!groupObj) return;
    var esc = AGY.ui.escapeHtml;
    AGY.ui.openModal(
      '重新关联本地目录 — ' + groupObj.projectName,
      '<div style="display:flex;flex-direction:column;gap:10px">' +
        '<div class="muted">将同步更新项目配置、索引及该项目名下全部对话指向的本地文件夹路径。</div>' +
        '<div><label class="field-label">当前关联目录</label><div class="mono muted" style="font-size:12px">' + esc(groupObj.fullPath || '(未绑定)') + '</div></div>' +
        '<div><label class="field-label">新的本地目录</label><div class="row gap-sm"><input class="input" id="txtRelocProjPath" value="' + esc(groupObj.fullPath || '') + '" /><button class="btn" id="btnRelocProjBrowse" type="button">浏览...</button></div></div>' +
      '</div>',
      '<button class="btn" data-act="cancel">取消</button><button class="btn btn-primary" data-act="go">确认更新路径</button>',
      function (mb, mf) {
        var inp = mb.querySelector('#txtRelocProjPath');
        mb.querySelector('#btnRelocProjBrowse').addEventListener('click', async function () {
          var pick = await AGY.api.apiFetch('/api/native/pick-folder', { method: 'POST', body: JSON.stringify({ title: '选择新项目目录', initialPath: inp.value }) });
          if (!pick.cancelled && pick.path) inp.value = pick.path;
        });
        mf.querySelector('[data-act="cancel"]').addEventListener('click', AGY.ui.closeModal);
        var goBtn = mf.querySelector('[data-act="go"]');
        goBtn.addEventListener('click', function () {
          async function doReloc(closeIde, forceKill) {
            AGY.api.withButtonBusy(goBtn, '更新中...', async function () {
              try {
                await AGY.api.apiFetch('/api/projects/relocate', {
                  method: 'POST',
                  body: JSON.stringify({ projectId: groupObj.projectId, projectName: groupObj.projectName, newFolderPath: inp.value.trim(), closeIdeIfRunning: !!closeIde, forceKill: !!forceKill })
                });
                AGY.ui.closeModal();
                await AGY.app.refreshAll('项目目录关联已更新 ✓');
              } catch (e) {
                if (!AGY.ui.handleIdeConflictError(
                  e,
                  '重新关联项目本地目录',
                  function () { doReloc(true, false); },
                  function () { doReloc(true, true); }
                )) { AGY.api.showToast(e.message, 'error'); }
              }
            });
          }
          doReloc(false, false);
        });
      }
    );
  }

  function openDeleteSessionsModal(cids) {
    if (!cids || cids.length === 0) return;
    var esc = AGY.ui.escapeHtml;
    var previewItems = [];
    for (var i = 0; i < cids.length && i < 12; i++) {
      var s = findLocalSessionById(cids[i]);
      previewItems.push(
        '<div class="del-manifest-row">' +
          '<span>' + esc(s ? s.title : cids[i]) + '</span>' +
          '<span class="badge badge-neutral">' + esc(s ? (s.projectName === 'Outside of Project' ? '未关联项目' : s.projectName) : '-') + '</span>' +
        '</div>'
      );
    }
    if (cids.length > 12) {
      previewItems.push('<div class="muted" style="padding-top:4px">……以及其他 ' + (cids.length - 12) + ' 条对话</div>');
    }

    AGY.ui.openModal(
      '将 ' + cids.length + ' 条对话移入回收站？',
      '<div class="alert-banner success" style="margin-bottom:10px">🛡 <strong>可撤销删除：</strong>所选对话及关联的后台子任务将先移入 <strong>回收站</strong>，30 天内随时可以一键完整还原。</div>' +
      '<div>以下对话将移入回收站（不会影响您磁盘上的项目代码文件）：</div>' +
      '<div class="del-manifest-list">' + previewItems.join('') + '</div>',
      '<button class="btn" data-act="cancel">取消</button><button class="btn btn-danger" data-act="go">移入回收站</button>',
      function (_mb, mf) {
        mf.querySelector('[data-act="cancel"]').addEventListener('click', AGY.ui.closeModal);
        var goBtn = mf.querySelector('[data-act="go"]');
        goBtn.addEventListener('click', function () {
          async function doDel(closeIde, forceKill) {
            AGY.api.withButtonBusy(goBtn, '处理中...', async function () {
              try {
                var res = await AGY.api.apiFetch('/api/sessions/delete', {
                  method: 'POST',
                  body: JSON.stringify({ cids: cids, closeIdeIfRunning: !!closeIde, forceKill: !!forceKill })
                });
                AGY.ui.closeModal();
                AGY.state.checkedSessions = {};
                await AGY.app.refreshAll('已将 ' + res.deletedSessions + ' 条对话移入回收站（释放 ' + res.freedMegabytes + ' MB）✓');
              } catch (e) {
                if (!AGY.ui.handleIdeConflictError(
                  e,
                  '将所选对话移入回收站',
                  function () { doDel(true, false); },
                  function () { doDel(true, true); }
                )) { AGY.api.showToast(e.message, 'error'); }
              }
            });
          }
          doDel(false, false);
        });
      }
    );
  }

  function exportSelectiveAgyModal(cids, capKeys) {
    var includeCaps = false;
    AGY.ui.openModal(
      '导出备份文件 (.agy)',
      '<div class="alert-banner success" style="margin-bottom:10px">支持在线热备份：无需关闭 Antigravity 主窗口，自动安全读取当前数据。</div>' +
      '<div>已选对话：<strong>' + cids.length + '</strong> 条；已选扩展配置：<strong>' + capKeys.length + '</strong> 项。</div>' +
      (capKeys.length === 0 ? '<label class="row gap-sm" style="margin-top:12px;cursor:pointer"><input type="checkbox" id="chkBundleAllCaps" /> 同时打包本机全部插件 / 技能 / 规则 / MCP / 钩子配置</label>' : ''),
      '<button class="btn" data-act="cancel">取消</button><button class="btn btn-primary" data-act="go">选择保存位置并导出...</button>',
      function (mb, mf) {
        var chk = mb.querySelector('#chkBundleAllCaps');
        if (chk) chk.addEventListener('change', function () { includeCaps = this.checked; });
        mf.querySelector('[data-act="cancel"]').addEventListener('click', AGY.ui.closeModal);
        var goBtn = mf.querySelector('[data-act="go"]');
        goBtn.addEventListener('click', function () {
          AGY.api.withButtonBusy(goBtn, '准备导出...', async function () {
            try {
              var res = await AGY.api.apiFetch('/api/backup/selective-export', {
                method: 'POST',
                body: JSON.stringify({ cids: cids, capKeys: capKeys, includeGlobalCaps: includeCaps, onlineHotBackup: true })
              });
              AGY.ui.closeModal();
              if (!res.cancelled) AGY.api.showToast('正在后台生成备份文件 (.agy)...', '');
            } catch (e) {
              AGY.api.showToast(e.message, 'error');
            }
          });
        });
      }
    );
  }

  AGY.views.sessions = renderSessions;
  AGY.views.openDeleteProjectModal = openDeleteProjectModal;
  AGY.views.openRelocateProjectModal = openRelocateProjectModal;
})();
