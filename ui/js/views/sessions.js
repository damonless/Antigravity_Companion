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
    renderBrowseSessions();
  }

  /* ══════════════════════════════════════════════════════
   * UNIFIED SESSIONS BROWSER (PARTIAL DOM UPDATES)
   * ══════════════════════════════════════════════════════ */
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
      var isChecked = !!st.checkedSessions[row.cid];
      return '<div class="session-row ' + (isChecked ? 'selected' : '') + '" data-cid="' + esc(row.cid) + '">' +
        '<input type="checkbox" class="sess-chk" data-cid="' + esc(row.cid) + '"' + (isChecked ? ' checked' : '') + ' />' +
        '<div class="session-main" data-preview-cid="' + esc(row.cid) + '">' +
          '<div class="session-title">' + esc(row.title) + '</div>' +
          '<div class="session-meta">' +
            '<span class="badge badge-neutral">' + esc(row.projectName) + '</span>' +
            (row.isCli ? '<span class="badge badge-blue">CLI</span>' : '') +
            (row.isLegacyCli ? '<span class="badge badge-neutral">历史 CLI 归档</span>' : '') +
            (row.isRunning ? '<span class="badge badge-amber">运行中</span>' : '') +
            '<span>' + esc(row.stepCount) + ' 步</span>' +
            '<span>' + esc(row.updatedTime) + '</span>' +
          '</div>' +
        '</div>' +
        '<div class="session-actions">' +
          '<button class="btn-icon" data-sess-act="preview" data-cid="' + esc(row.cid) + '" title="阅读 Markdown 对话">' + ICONS.eye + '</button>' +
          '<button class="btn-icon" data-sess-act="rename" data-cid="' + esc(row.cid) + '" title="重命名此会话">' + ICONS.edit + '</button>' +
          '<button class="btn-icon" data-sess-act="move" data-cid="' + esc(row.cid) + '" title="移动到其他工程">' + ICONS.folder + '</button>' +
          '<button class="btn-icon" data-sess-act="exportmd" data-cid="' + esc(row.cid) + '" title="导出为 .md">' + ICONS.download + '</button>' +
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
      AGY.dom.setText('lblSessBatchCount', '已选 ' + checkedCids.length + ' 条会话');
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
        else if (act === 'exportmd') exportSessionMarkdown(cid, 'local');
      });
    }
  }

  function gotoRenameWithScope(cids, selectedGroupObj) {
    var st = AGY.state;
    if (cids && cids.length > 0) {
      var newChecked = {};
      var newScoped = {};
      for (var j = 0; j < cids.length; j++) {
        newChecked[cids[j]] = true;
        newScoped[cids[j]] = true;
      }
      st.renameChecked = newChecked;
      st.renameScopedCids = newScoped;
      st.renameProjectFilter = '__SCOPED__';
      st.renameShowTagged = true;
      st.renameShowAux = true;
      st.renameShowRunning = true;
      st.renameShowLowValue = true;
      st.renameFocusedCid = cids[0];
    } else {
      st.renameScopedCids = {};
      st.renameProjectFilter = (selectedGroupObj && selectedGroupObj.groupKind === 'formal' && selectedGroupObj.projectName)
        ? selectedGroupObj.projectName
        : '__ALL__';
    }
    st.renameStep = 1;
    AGY.app.switchView('rename');
  }

  function renderBrowseSessions() {
    var el = AGY.dom.el;
    var st = AGY.state;
    var esc = AGY.ui.escapeHtml;
    var ICONS = AGY.ui.ICONS;
    var sd = st.sessionsData;

    if (!sd) {
      el('mainCanvas').innerHTML = '<div class="empty-state">正在加载工程与会话列表...</div>';
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
            projectName: g.projectName,
            shortPath: g.shortPath,
            totalCount: activeCli
          });
        } else {
          auxGroups.push(g);
        }
      }
    }

    var projCardsHtml = [];
    projCardsHtml.push(
      '<div class="proj-card ' + (st.selectedBucketKey === '__ALL__' ? 'active' : '') + '" data-bucket="__ALL__">' +
        '<div class="proj-card-top"><span class="proj-card-name">全部正式工程</span><span class="badge badge-accent">' + esc(sd.formalPrimarySessions || 0) + '</span></div>' +
        '<div class="proj-card-path">' + esc(sd.formalProjectsCount || 0) + ' 个工作区（不含辅助分组）</div>' +
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
          '<span>辅助分组 (CLI / 项目外 / 子任务)</span>' +
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
          '<span>历史 CLI 只读归档 (' + legacyCliCount + ')</span>' +
          '<span>' + (st.legacyCliExpanded ? '▾' : '▸') + '</span>' +
        '</div>'
      );
      if (st.legacyCliExpanded) {
        projCardsHtml.push(
          '<div class="proj-card ' + (st.selectedBucketKey === '__LEGACY_CLI__' ? 'active' : '') + '" data-bucket="__LEGACY_CLI__">' +
            '<div class="proj-card-top"><span class="proj-card-name">antigravity-cli 历史归档</span><span class="badge badge-neutral">' + legacyCliCount + '</span></div>' +
            '<div class="proj-card-path">conversation_summaries.db (只读)</div>' +
          '</div>'
        );
      }
    }

    var info = collectVisibleSessions();
    var selectedGroupObj = info.selectedGroupObj;
    var isFormalSelected = !!(selectedGroupObj && selectedGroupObj.groupKind === 'formal');
    var projRelocateBtn = isFormalSelected
      ? '<button class="btn btn-sm" id="btnRelocateCurProj" title="修复或迁移该工程关联的本地目录">重定位目录</button>'
      : '';
    var projDeleteBtn = (isFormalSelected && selectedGroupObj.projectId)
      ? '<button class="btn btn-sm btn-danger-outline" id="btnDeleteCurProj" title="级联移除该工程记录及名下会话（不删磁盘代码目录）">删除工程</button>'
      : '';

    var topbar = el('topbarActions');
    if (topbar) {
      topbar.innerHTML = '<button class="btn btn-sm btn-primary" id="btnSessionsGotoRename">去智能重命名 →</button>';
      el('btnSessionsGotoRename').addEventListener('click', function () {
        var cids = Object.keys(st.checkedSessions).filter(function (id) { return st.checkedSessions[id]; });
        gotoRenameWithScope(cids, selectedGroupObj);
      });
    }

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
              '<input id="txtSessionSearch" type="text" placeholder="搜索会话标题、工程名' + (st.sessionSearchContent ? '或对话正文 (FTS5)...' : '...') + '" value="' + esc(st.sessionSearchQuery) + '" />' +
              '<button class="btn-icon ' + (st.sessionSearchQuery ? '' : 'hidden') + '" id="btnClearSessSearch" style="width:20px;height:20px" title="清空">✕</button>' +
            '</div>' +
            '<button class="filter-pill ' + (st.sessionSearchContent ? 'active' : '') + '" id="btnToggleSearchContent" type="button">搜对话正文</button>' +
            '<span class="muted" id="lblSessHitCount" style="font-size:12px">命中 ' + info.visible.length + ' 条</span>' +
            '<button class="btn btn-sm" id="btnSessSelectVisible">全选当前</button>' +
            '<button class="btn btn-sm" id="btnSessClearChecked">清空勾选</button>' +
            projRelocateBtn +
            projDeleteBtn +
          '</div>' +
          '<div class="session-scroll" id="sessScrollHost">' + renderSessionRowsHtml(info.visible) + '</div>' +
          '<div class="floating-batch-bar hidden" id="sessBatchBar">' +
            '<div class="row gap-sm"><strong id="lblSessBatchCount">已选 0 条会话</strong></div>' +
            '<div class="row gap-sm">' +
              '<button class="btn btn-sm btn-primary" id="btnBatchGotoRename">批量重命名 →</button>' +
              '<button class="btn btn-sm" id="btnBatchMove">迁移工程</button>' +
              '<button class="btn btn-sm" id="btnBatchExportAgy">导出 .agy</button>' +
              '<button class="btn btn-sm btn-danger" id="btnBatchDelete">删除所选</button>' +
            '</div>' +
          '</div>' +
        '</section>' +
      '</div>';

    var canvas = el('mainCanvas');
    var projCards = canvas.querySelectorAll('.proj-card[data-bucket]');
    for (var p = 0; p < projCards.length; p++) {
      projCards[p].addEventListener('click', function () {
        st.selectedBucketKey = this.getAttribute('data-bucket');
        renderBrowseSessions();
      });
    }
    if (el('btnToggleAuxGroups')) {
      el('btnToggleAuxGroups').addEventListener('click', function () { st.auxExpanded = !st.auxExpanded; renderBrowseSessions(); });
    }
    if (el('btnToggleLegacyCliGroup')) {
      el('btnToggleLegacyCliGroup').addEventListener('click', function () { st.legacyCliExpanded = !st.legacyCliExpanded; renderBrowseSessions(); });
    }
    if (el('btnRelocateCurProj')) {
      el('btnRelocateCurProj').addEventListener('click', function () { openRelocateProjectModal(selectedGroupObj); });
    }
    if (el('btnDeleteCurProj')) {
      el('btnDeleteCurProj').addEventListener('click', function () {
        openDeleteProjectModal(selectedGroupObj.projectId, selectedGroupObj.projectName);
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
      searchInput.placeholder = '搜索会话标题、工程名' + (st.sessionSearchContent ? '或对话正文 (FTS5)...' : '...');
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

    el('btnSessSelectVisible').addEventListener('click', function () {
      var curVis = collectVisibleSessions().visible;
      for (var j = 0; j < curVis.length; j++) st.checkedSessions[curVis[j].cid] = true;
      patchSessionListPartial();
    });
    el('btnSessClearChecked').addEventListener('click', function () {
      st.checkedSessions = {};
      patchSessionListPartial();
    });

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

  function openDeleteProjectModal(projectId, projectName) {
    var esc = AGY.ui.escapeHtml;
    AGY.ui.openModal(
      '确认删除工程 [' + projectName + '] 及其全部会话？',
      '<div style="color:var(--red-text);font-weight:600;margin-bottom:8px">警告：此操作不可逆！</div>' +
      '<div>将级联移除 <span class="mono">config/projects/' + esc(projectId) + '.json</span> 以及该工程名下的所有主会话、子任务 <span class="mono">.db/.pbtxt/brain</span>（不会删除您磁盘上的业务代码目录）。</div>',
      '<button class="btn" data-act="cancel">取消</button><button class="btn btn-danger" data-act="go">确认删除工程记录</button>',
      function (_mb, mf) {
        mf.querySelector('[data-act="cancel"]').addEventListener('click', AGY.ui.closeModal);
        mf.querySelector('[data-act="go"]').addEventListener('click', function () {
          async function doDel(closeIde) {
            try {
              var res = await AGY.api.apiFetch('/api/sessions/delete', {
                method: 'POST',
                body: JSON.stringify({ projectIds: [projectId], closeIdeIfRunning: closeIde })
              });
              AGY.ui.closeModal();
              AGY.state.selectedBucketKey = '__ALL__';
              await AGY.app.refreshAll('已删除工程记录，清理 ' + res.deletedSessions + ' 条会话（释放 ' + res.freedMegabytes + ' MB）');
            } catch (e) {
              if (e.status === 409 && e.data && e.data.code === 'IDE_RUNNING') {
                AGY.ui.confirmCloseIdeModal(e.data.parentCount, e.data.lsCount, '删除工程 [' + projectName + ']', function () { doDel(true); });
              } else {
                AGY.api.showToast(e.message, 'error');
              }
            }
          }
          doDel(false);
        });
      }
    );
  }

  async function exportSessionMarkdown(cid, source) {
    try {
      var res = await AGY.api.apiFetch('/api/sessions/export-md', {
        method: 'POST',
        body: JSON.stringify({ cid: cid, source: source || 'local' })
      });
      if (!res.cancelled) AGY.api.showToast('已导出 Markdown 至 ' + res.outputPath, 'success');
    } catch (e) { AGY.api.showToast('导出失败：' + e.message, 'error'); }
  }

  function openSingleRenameModal(sessObj) {
    if (!sessObj) return;
    AGY.ui.openModal(
      '单条会话快速重命名',
      '<div class="muted" style="margin-bottom:8px">同步更新 annotations/.pbtxt、conversation_summaries.db 与 .pb 索引，并自动生成快照备份。</div>' +
      '<label style="display:block;font-weight:600;margin-bottom:6px">新会话标题</label>' +
      '<input class="input" id="txtSingleNewTitle" value="' + AGY.ui.escapeHtml(sessObj.title) + '" />',
      '<button class="btn" data-act="cancel">取消</button><button class="btn btn-primary" data-act="save">保存重命名</button>',
      function (mb, mf) {
        var inp = mb.querySelector('#txtSingleNewTitle');
        inp.focus();
        inp.setSelectionRange(inp.value.length, inp.value.length);
        mf.querySelector('[data-act="cancel"]').addEventListener('click', AGY.ui.closeModal);
        mf.querySelector('[data-act="save"]').addEventListener('click', function () {
          submitSingleRename(sessObj.cid, inp.value, 'auto');
        });
      }
    );
  }

  async function submitSingleRename(cid, newTitle, strategy) {
    if (!String(newTitle || '').trim()) { AGY.api.showToast('新标题不能为空', 'error'); return; }
    try {
      await AGY.api.apiFetch('/api/sessions/rename-single', {
        method: 'POST',
        body: JSON.stringify({ cid: cid, newTitle: newTitle.trim(), strategy: strategy || 'auto' })
      });
      AGY.ui.closeModal();
      await AGY.app.refreshAll('会话重命名成功 ✓');
    } catch (e) {
      if (e.status === 409 && e.data && e.data.code === 'IDE_RUNNING') {
        AGY.ui.openDualModeRenameModal(e.data.parentCount, e.data.lsCount, function (chosenStrategy) {
          submitSingleRename(cid, newTitle, chosenStrategy);
        });
      } else { AGY.api.showToast(e.message, 'error'); }
    }
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
    opts.push({ projectId: 'outside-of-project', projectName: 'Outside of Project', fullPath: '', label: 'Outside of Project (全局无工程)' });

    var optHtml = opts.map(function (o, idx) {
      return '<option value="' + idx + '">' + esc(o.label) + '</option>';
    }).join('') + '<option value="__CUSTOM__">+ 手动指定新工程与目录...</option>';

    AGY.ui.openModal(
      '迁移 ' + cids.length + ' 条会话到其他工程',
      '<div style="display:flex;flex-direction:column;gap:10px">' +
        '<div><label class="field-label">选择目标工程</label><select class="select" id="selMoveTarget">' + optHtml + '</select></div>' +
        '<div><label class="field-label">目标工程名称</label><input class="input" id="txtMoveName" value="' + esc(opts[0] ? opts[0].projectName : '') + '" /></div>' +
        '<div><label class="field-label">目标物理目录</label><div class="row gap-sm"><input class="input" id="txtMovePath" value="' + esc(opts[0] ? opts[0].fullPath : '') + '" /><button class="btn" id="btnMoveBrowse">浏览...</button></div></div>' +
      '</div>',
      '<button class="btn" data-act="cancel">取消</button><button class="btn btn-primary" data-act="go">确认迁移</button>',
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
          var pick = await AGY.api.apiFetch('/api/native/pick-folder', { method: 'POST', body: JSON.stringify({ title: '选择目标工程目录', initialPath: inpPath.value }) });
          if (!pick.cancelled && pick.path) {
            inpPath.value = pick.path;
            if (!inpName.value.trim()) inpName.value = pick.path.split(/[\\/]/).pop() || '';
          }
        });
        mf.querySelector('[data-act="cancel"]').addEventListener('click', AGY.ui.closeModal);
        mf.querySelector('[data-act="go"]').addEventListener('click', function () {
          async function doMove(closeIde) {
            try {
              await AGY.api.apiFetch('/api/sessions/move', {
                method: 'POST',
                body: JSON.stringify({ cids: cids, targetProjectId: targetId, targetProjectName: inpName.value.trim(), targetFolderPath: inpPath.value.trim(), closeIdeIfRunning: closeIde })
              });
              AGY.ui.closeModal();
              AGY.state.checkedSessions = {};
              await AGY.app.refreshAll('会话迁移完成 ✓');
            } catch (e) {
              if (e.status === 409 && e.data && e.data.code === 'IDE_RUNNING') {
                AGY.ui.confirmCloseIdeModal(e.data.parentCount, e.data.lsCount, '迁移会话工程归属', function () { doMove(true); });
              } else { AGY.api.showToast(e.message, 'error'); }
            }
          }
          doMove(false);
        });
      }
    );
  }

  function openRelocateProjectModal(groupObj) {
    if (!groupObj) return;
    var esc = AGY.ui.escapeHtml;
    AGY.ui.openModal(
      '重定位工程物理目录 — ' + groupObj.projectName,
      '<div style="display:flex;flex-direction:column;gap:10px">' +
        '<div class="muted">同步更新 projects/*.json、.pb 索引及该工程名下全部会话数据库路径。</div>' +
        '<div><label class="field-label">当前目录</label><div class="mono muted" style="font-size:12px">' + esc(groupObj.fullPath || '(未绑定)') + '</div></div>' +
        '<div><label class="field-label">新的本地物理目录</label><div class="row gap-sm"><input class="input" id="txtRelocProjPath" value="' + esc(groupObj.fullPath || '') + '" /><button class="btn" id="btnRelocProjBrowse">浏览...</button></div></div>' +
      '</div>',
      '<button class="btn" data-act="cancel">取消</button><button class="btn btn-primary" data-act="go">确认重定位</button>',
      function (mb, mf) {
        var inp = mb.querySelector('#txtRelocProjPath');
        mb.querySelector('#btnRelocProjBrowse').addEventListener('click', async function () {
          var pick = await AGY.api.apiFetch('/api/native/pick-folder', { method: 'POST', body: JSON.stringify({ title: '选择新工程物理目录', initialPath: inp.value }) });
          if (!pick.cancelled && pick.path) inp.value = pick.path;
        });
        mf.querySelector('[data-act="cancel"]').addEventListener('click', AGY.ui.closeModal);
        mf.querySelector('[data-act="go"]').addEventListener('click', function () {
          async function doReloc(closeIde) {
            try {
              await AGY.api.apiFetch('/api/projects/relocate', {
                method: 'POST',
                body: JSON.stringify({ projectId: groupObj.projectId, projectName: groupObj.projectName, newFolderPath: inp.value.trim(), closeIdeIfRunning: closeIde })
              });
              AGY.ui.closeModal();
              await AGY.app.refreshAll('工程目录重定位完成 ✓');
            } catch (e) {
              if (e.status === 409 && e.data && e.data.code === 'IDE_RUNNING') {
                AGY.ui.confirmCloseIdeModal(e.data.parentCount, e.data.lsCount, '重定位工程物理目录', function () { doReloc(true); });
              } else { AGY.api.showToast(e.message, 'error'); }
            }
          }
          doReloc(false);
        });
      }
    );
  }

  function openDeleteSessionsModal(cids) {
    if (!cids || cids.length === 0) return;
    AGY.ui.openModal(
      '确认级联物理删除 ' + cids.length + ' 条会话？',
      '<div style="color:var(--red-text);font-weight:600;margin-bottom:8px">警告：此操作不可逆！</div>' +
      '<div>将连带删除所选会话的 <span class="mono">.pb</span> 摘要条目、对话数据库 (<span class="mono">.db*</span>)、脑区产物与关联子任务。</div>',
      '<button class="btn" data-act="cancel">取消</button><button class="btn btn-danger" data-act="go">确认永久删除</button>',
      function (_mb, mf) {
        mf.querySelector('[data-act="cancel"]').addEventListener('click', AGY.ui.closeModal);
        mf.querySelector('[data-act="go"]').addEventListener('click', function () {
          async function doDel(closeIde) {
            try {
              var res = await AGY.api.apiFetch('/api/sessions/delete', {
                method: 'POST',
                body: JSON.stringify({ cids: cids, closeIdeIfRunning: closeIde })
              });
              AGY.ui.closeModal();
              AGY.state.checkedSessions = {};
              await AGY.app.refreshAll('已删除 ' + res.deletedSessions + ' 条会话，释放 ' + res.freedMegabytes + ' MB');
            } catch (e) {
              if (e.status === 409 && e.data && e.data.code === 'IDE_RUNNING') {
                AGY.ui.confirmCloseIdeModal(e.data.parentCount, e.data.lsCount, '物理删除选中会话', function () { doDel(true); });
              } else { AGY.api.showToast(e.message, 'error'); }
            }
          }
          doDel(false);
        });
      }
    );
  }

  function exportSelectiveAgyModal(cids, capKeys) {
    var includeCaps = false;
    AGY.ui.openModal(
      '导出为轻量迁移包 (.agy)',
      '<div>已选会话：<strong>' + cids.length + '</strong> 条；已选能力：<strong>' + capKeys.length + '</strong> 项。</div>' +
      (capKeys.length === 0 ? '<label class="row gap-sm" style="margin-top:12px;cursor:pointer"><input type="checkbox" id="chkBundleAllCaps" /> 同时打包本机全部全局能力配置 (Skills / Rules / MCP / Hooks / Plugins)</label>' : ''),
      '<button class="btn" data-act="cancel">取消</button><button class="btn btn-primary" data-act="go">选择保存位置并导出...</button>',
      function (mb, mf) {
        var chk = mb.querySelector('#chkBundleAllCaps');
        if (chk) chk.addEventListener('change', function () { includeCaps = this.checked; });
        mf.querySelector('[data-act="cancel"]').addEventListener('click', AGY.ui.closeModal);
        mf.querySelector('[data-act="go"]').addEventListener('click', function () {
          async function doExp(closeIde) {
            try {
              var res = await AGY.api.apiFetch('/api/backup/selective-export', {
                method: 'POST',
                body: JSON.stringify({ cids: cids, capKeys: capKeys, includeGlobalCaps: includeCaps, closeIdeIfRunning: closeIde })
              });
              AGY.ui.closeModal();
              if (!res.cancelled) AGY.api.showToast('正在后台生成选择性备份包...', '');
            } catch (e) {
              if (e.status === 409 && e.data && e.data.code === 'IDE_RUNNING') {
                AGY.ui.confirmCloseIdeModal(e.data.parentCount, e.data.lsCount, '导出选中会话至 .agy', function () { doExp(true); });
              } else { AGY.api.showToast(e.message, 'error'); }
            }
          }
          doExp(false);
        });
      }
    );
  }

  AGY.views.sessions = renderSessions;
})();
