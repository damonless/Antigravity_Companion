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
    var topbar = el('topbarActions');
    if (topbar) {
      topbar.innerHTML =
        '<div class="pill-group">' +
          '<button class="filter-pill ' + (st.sessionsSubMode === 'browse' ? 'active' : '') + '" id="btnModeBrowse">会话浏览与导出</button>' +
          '<button class="filter-pill ' + (st.sessionsSubMode === 'rename' ? 'active' : '') + '" id="btnModeRename">三步智能重命名向导</button>' +
        '</div>';
      el('btnModeBrowse').addEventListener('click', function () { st.sessionsSubMode = 'browse'; renderSessions(); });
      el('btnModeRename').addEventListener('click', function () { st.sessionsSubMode = 'rename'; renderSessions(); });
    }

    if (st.sessionsSubMode === 'rename') {
      renderRenameWizard();
    } else {
      renderBrowseSessions();
    }
  }

  /* ══════════════════════════════════════════════════════
   * PART A: UNIFIED SESSIONS BROWSER (PARTIAL DOM UPDATES)
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
    var st = AGY.state;
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
    var projRelocateBtn = (selectedGroupObj && selectedGroupObj.groupKind === 'formal')
      ? '<button class="btn btn-sm" id="btnRelocateCurProj" title="修复或迁移该工程关联的本地目录">重定位目录</button>'
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
              '<input id="txtSessionSearch" type="text" placeholder="搜索会话标题、工程名' + (st.sessionSearchContent ? '或对话正文 (FTS5)...' : '...') + '" value="' + esc(st.sessionSearchQuery) + '" />' +
              '<button class="btn-icon ' + (st.sessionSearchQuery ? '' : 'hidden') + '" id="btnClearSessSearch" style="width:20px;height:20px" title="清空">✕</button>' +
            '</div>' +
            '<button class="filter-pill ' + (st.sessionSearchContent ? 'active' : '') + '" id="btnToggleSearchContent" type="button">搜对话正文</button>' +
            '<span class="muted" id="lblSessHitCount" style="font-size:12px">命中 ' + info.visible.length + ' 条</span>' +
            '<button class="btn btn-sm" id="btnSessSelectVisible">全选当前</button>' +
            '<button class="btn btn-sm" id="btnSessClearChecked">清空勾选</button>' +
            projRelocateBtn +
          '</div>' +
          '<div class="session-scroll" id="sessScrollHost">' + renderSessionRowsHtml(info.visible) + '</div>' +
          '<div class="floating-batch-bar hidden" id="sessBatchBar">' +
            '<div class="row gap-sm"><strong id="lblSessBatchCount">已选 0 条会话</strong></div>' +
            '<div class="row gap-sm">' +
              '<button class="btn btn-sm btn-primary" id="btnBatchGotoRename">批量重命名</button>' +
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
      st.renameStep = 1;
      st.sessionsSubMode = 'rename';
      renderSessions();
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

  /* ══════════════════════════════════════════════════════
   * PART B: TRUE 3-STEP RENAME WIZARD
   * Step 1: Scope & Locked Visibility (fixes __SCOPED__ uncheck bug)
   * Step 2: Local Rules (multiRepoRules CRUD) + Zero-Key agy CLI + Offline Clipboard
   * Step 3: Diff Preview + Commit + Rollback (real renamedCount toast)
   * ══════════════════════════════════════════════════════ */
  function getFilteredRenameCandidates() {
    var st = AGY.state;
    var rd = st.renameData;
    if (!rd || !rd.candidates) return [];
    var cands = rd.candidates;
    var out = [];
    for (var i = 0; i < cands.length; i++) {
      var c = cands[i];
      if (st.renameProjectFilter === '__SCOPED__') {
        // Fix __SCOPED__ uncheck bug: filter by immutable renameScopedCids, NOT mutable renameChecked!
        if (!st.renameScopedCids[c.cid]) continue;
      } else {
        if (c.isSubagent) continue;
        if (!st.renameShowAux && (c.isCli || c.isOutside || c.isJunk)) continue;
        if (st.renameProjectFilter !== '__ALL__' && c.projectName !== st.renameProjectFilter) continue;
        if (!st.renameShowTagged && c.isTagged) continue;
        if (!st.renameShowRunning && c.isRunning) continue;
        if (!st.renameShowLowValue && c.isLowValue) continue;
      }
      out.push(c);
    }
    return out;
  }

  function getEffectiveNewTitle(c) {
    if (!c) return '';
    return AGY.state.renameEdits[c.cid] !== undefined ? AGY.state.renameEdits[c.cid] : c.suggestedTitle;
  }

  function renderRenameWizard() {
    var el = AGY.dom.el;
    var st = AGY.state;
    var esc = AGY.ui.escapeHtml;
    var rd = st.renameData;

    if (!rd) {
      el('mainCanvas').innerHTML = '<div class="empty-state">正在加载重命名工作台...</div>';
      return;
    }

    var visible = getFilteredRenameCandidates();
    var selectedToApply = [];
    for (var i = 0; i < visible.length; i++) {
      if (st.renameChecked[visible[i].cid]) selectedToApply.push(visible[i]);
    }

    var stepperHtml =
      '<div class="rename-stepper-bar">' +
        '<div class="stepper">' +
          '<button class="step-btn ' + (st.renameStep === 1 ? 'active' : '') + '" data-rstep="1">① 筛选范围与锁定检查</button>' +
          '<span class="muted">→</span>' +
          '<button class="step-btn ' + (st.renameStep === 2 ? 'active' : '') + '" data-rstep="2">② 规则精校 / agy CLI / AI 润色 (' + selectedToApply.length + ')</button>' +
          '<span class="muted">→</span>' +
          '<button class="step-btn ' + (st.renameStep === 3 ? 'active' : '') + '" data-rstep="3">③ Diff 确认写入</button>' +
        '</div>' +
        '<div class="row gap-sm">' +
          '<button class="btn btn-sm" id="btnOpenRulesDrawer">⚙ 命名规则 (含多仓 CRUD)</button>' +
          '<button class="btn btn-sm" id="btnOpenRollbackDrawer">↺ 回滚历史 (' + esc(rd.snapshotCount || 0) + ')</button>' +
        '</div>' +
      '</div>';

    var bodyHtml = '';
    if (st.renameStep === 1) bodyHtml = renderRenameStep1(visible, selectedToApply);
    else if (st.renameStep === 2) bodyHtml = renderRenameStep2(visible, selectedToApply);
    else bodyHtml = renderRenameStep3(selectedToApply);

    el('mainCanvas').innerHTML = '<div class="rename-shell">' + stepperHtml + bodyHtml + '</div>';
    bindRenameEvents(visible, selectedToApply);
  }

  function renderRenameStep1(visible, selectedToApply) {
    var st = AGY.state;
    var esc = AGY.ui.escapeHtml;
    var rd = st.renameData;
    var lockedList = rd.lockedSessions || [];

    var projOpts = '<option value="__ALL__">全部正式工程</option>';
    if (Object.keys(st.renameScopedCids).length > 0) {
      projOpts += '<option value="__SCOPED__"' + (st.renameProjectFilter === '__SCOPED__' ? ' selected' : '') + '>已带入的指定会话 (' + Object.keys(st.renameScopedCids).length + ')</option>';
    }
    var projs = rd.formalProjects || [];
    for (var p = 0; p < projs.length; p++) {
      projOpts += '<option value="' + esc(projs[p]) + '"' + (st.renameProjectFilter === projs[p] ? ' selected' : '') + '>' + esc(projs[p]) + '</option>';
    }

    var lockedHtml = '';
    if (lockedList.length > 0) {
      lockedHtml =
        '<div class="alert-banner" style="margin-bottom:0">' +
          '<strong>🔒 锁定会话可见性（共 ' + lockedList.length + ' 条处于 RUNNING / not_fully_idle=1）：</strong>' +
          '<div class="muted" style="font-size:11.5px;margin-top:4px">' +
            lockedList.slice(0, 5).map(function (lk) {
              return '[' + esc(lk.projectName) + '] ' + esc(lk.title) + ' (' + esc(lk.runStatus) + ')';
            }).join(' · ') + (lockedList.length > 5 ? ' 等' : '') +
          '</div>' +
        '</div>';
    }

    var rowsHtml = visible.length === 0
      ? '<tr><td colspan="5" class="empty-state">当前筛选范围内没有会话，请调整上方筛选条件。</td></tr>'
      : visible.map(function (c) {
          var checked = !!st.renameChecked[c.cid];
          var tagState = c.isRunning
            ? '<span class="badge badge-amber">锁定: ' + esc(c.runStatus) + '</span>'
            : (c.isTagged ? '<span class="badge badge-green">已带【】</span>'
              : (c.isLowValue ? '<span class="badge badge-neutral">低价值水话</span>' : '<span class="badge badge-accent">待整理</span>'));
          return '<tr class="' + (checked ? 'selected' : '') + '">' +
            '<td style="width:36px"><input type="checkbox" class="ren-step1-chk" data-cid="' + esc(c.cid) + '"' + (checked ? ' checked' : '') + ' /></td>' +
            '<td><span class="badge badge-neutral">' + esc(c.projectName) + '</span></td>' +
            '<td>' + esc(c.oldTitle) + '</td>' +
            '<td>' + tagState + '</td>' +
            '<td class="muted">' + esc(c.stepCount) + ' 步 · ' + esc(c.updatedTime) + '</td>' +
          '</tr>';
        }).join('');

    return '<div class="card" style="flex:1;display:flex;flex-direction:column;min-height:0;gap:12px">' +
      '<div class="between wrap gap-sm">' +
        '<div class="row gap-sm wrap">' +
          '<select class="select" id="selRenameProj" style="width:220px">' + projOpts + '</select>' +
          '<button class="filter-pill ' + (st.renameShowTagged ? 'active' : '') + '" data-rtoggle="tagged">包含已带【】</button>' +
          '<button class="filter-pill ' + (st.renameShowAux ? 'active' : '') + '" data-rtoggle="aux">包含 CLI 与项目外</button>' +
          '<button class="filter-pill ' + (st.renameShowRunning ? 'active' : '') + '" data-rtoggle="running">显示锁定/运行中</button>' +
          '<button class="filter-pill ' + (st.renameShowLowValue ? 'active' : '') + '" data-rtoggle="low">包含低价值水话</button>' +
        '</div>' +
        '<div class="row gap-sm">' +
          '<button class="btn btn-sm" id="btnRenSelAll">全选当前</button>' +
          '<button class="btn btn-sm" id="btnRenSelNone">全不选</button>' +
          '<button class="btn btn-sm btn-primary" id="btnStep1To2"' + (selectedToApply.length === 0 ? ' disabled' : '') + '>下一步：润色与精校 (' + selectedToApply.length + ') →</button>' +
        '</div>' +
      '</div>' +
      lockedHtml +
      '<div class="matrix-scroll" style="border:1px solid var(--border);border-radius:8px">' +
        '<table class="data-table"><thead><tr><th></th><th>工程</th><th>当前原标题</th><th>状态与锁检查</th><th>步数与时间</th></tr></thead>' +
          '<tbody id="renStep1Tbody">' + rowsHtml + '</tbody>' +
        '</table>' +
      '</div>' +
    '</div>';
  }

  function renderRenameStep2(visible, selectedToApply) {
    var st = AGY.state;
    var esc = AGY.ui.escapeHtml;
    var focused = null;
    for (var i = 0; i < visible.length; i++) {
      if (visible[i].cid === st.renameFocusedCid) { focused = visible[i]; break; }
    }
    if (!focused && visible.length > 0) { focused = visible[0]; st.renameFocusedCid = focused.cid; }

    var candHtml = visible.length === 0
      ? '<div class="empty-state">当前范围下无候选会话，请返回 Step 1 调整范围。</div>'
      : visible.map(function (c) {
          var checked = !!st.renameChecked[c.cid];
          var effNew = getEffectiveNewTitle(c);
          var edited = st.renameEdits[c.cid] !== undefined && st.renameEdits[c.cid] !== c.suggestedTitle;
          var aiMod = !!st.renameAiModified[c.cid];
          var badge = c.isRunning
            ? '<span class="badge badge-amber">运行中(将跳过)</span>'
            : (aiMod ? '<span class="badge badge-green">AI/CLI 润色</span>'
              : (edited ? '<span class="badge badge-blue">已手改</span>' : '<span class="badge badge-accent">规则生成</span>'));
          return '<div class="cand-card ' + (focused && focused.cid === c.cid ? 'focused' : '') + '" data-cand-focus="' + esc(c.cid) + '">' +
            '<input type="checkbox" class="ren-chk" data-cid="' + esc(c.cid) + '"' + (checked ? ' checked' : '') + ' style="margin-top:3px" />' +
            '<div class="cand-body">' +
              '<div class="cand-old">原：' + esc(c.oldTitle) + '</div>' +
              '<div class="cand-new" data-cand-new-cid="' + esc(c.cid) + '">新：' + esc(effNew) + '</div>' +
              '<div class="cand-tags">' +
                '<span class="badge badge-neutral">' + esc(c.projectName) + '</span>' +
                badge +
                '<span class="muted" style="font-size:11px">' + esc(c.stepCount) + ' 步 · ' + esc(c.updatedTime) + '</span>' +
              '</div>' +
            '</div>' +
          '</div>';
        }).join('');

    var inspectorHtml = '<div class="empty-state">在左侧点击任意候选会话即可精校新标题</div>';
    if (focused) {
      var curEdit = getEffectiveNewTitle(focused);
      inspectorHtml =
        '<div class="between">' +
          '<div class="row gap-sm"><span class="badge badge-neutral">' + esc(focused.projectName) + '</span><span class="muted">' + esc(focused.stepCount) + ' 步 · ' + esc(focused.updatedTime) + '</span></div>' +
          '<button class="btn btn-xs" id="btnRestoreRuleTitle">恢复规则建议</button>' +
        '</div>' +
        '<div>' +
          '<div class="between" style="margin-bottom:5px"><label style="font-weight:600">拟定新标题（实时生效，无需失焦刷新）</label><span class="muted" id="lblNewTitleLen">' + curEdit.length + ' 字</span></div>' +
          '<textarea class="textarea" id="txtEditNewTitle" rows="3">' + esc(curEdit) + '</textarea>' +
          '<div class="row gap-xs wrap" style="margin-top:6px">' +
            '<button class="btn btn-xs" id="btnChipApplyPrefix">应用建议前缀: ' + esc(focused.suggestedPrefix) + '</button>' +
          '</div>' +
        '</div>' +
        '<div>' +
          '<div class="between" style="margin-bottom:5px"><label style="font-weight:600">首问内容摘要</label><button class="btn btn-xs btn-ghost" id="btnTogglePromptRaw">' + (st.renamePromptExpanded ? '查看清洗后' : '展开原始首问') + '</button></div>' +
          '<div class="prompt-card">' + esc(st.renamePromptExpanded ? (focused.firstPromptSnippet || '(暂无首问内容)') : (focused.cleanedPrompt || focused.firstPromptSnippet || '(暂无首问内容)')) + '</div>' +
        '</div>' +
        '<div class="accordion">' +
          '<button class="accordion-trigger" id="btnRenTechToggle">' + (st.renameTechExpanded ? '▾' : '▸') + ' 技术信息（路径 / 分支线索 / 运行状态）</button>' +
          '<div class="accordion-body mono ' + (st.renameTechExpanded ? '' : 'hidden') + '" id="renTechBody">' +
            '<div>工程目录: ' + esc(focused.fullPath || '-') + '</div>' +
            '<div>AST 分支线索: ' + esc(focused.gitBranchHint || '(无)') + '</div>' +
            '<div>状态: ' + esc(focused.runStatus || 'IDLE') + '</div>' +
            '<div>ID: ' + esc(focused.cid) + '</div>' +
          '</div>' +
        '</div>';
    }

    return '<div class="rename-split">' +
      '<div class="rename-left">' +
        '<div class="rename-filter-bar">' +
          '<div class="between wrap gap-sm">' +
            '<div class="row gap-sm">' +
              '<button class="btn btn-xs" id="btnBackToStep1">← 范围</button>' +
              '<button class="btn btn-xs" id="btnRenRecompute">重算规则建议</button>' +
            '</div>' +
            '<div class="row gap-sm">' +
              '<button class="btn btn-sm" id="btnOpenCliSuggestDrawer">⚡ Zero-Key agy CLI</button>' +
              '<button class="btn btn-sm" id="btnOpenAiDrawer">📋 离线 AI 剪贴板</button>' +
              '<button class="btn btn-sm btn-primary" id="btnNextToConfirm"' + (selectedToApply.length === 0 ? ' disabled' : '') + '>下一步：Diff 确认 (' + selectedToApply.length + ') →</button>' +
            '</div>' +
          '</div>' +
        '</div>' +
        '<div class="rename-list-scroll" id="renStep2List">' + candHtml + '</div>' +
      '</div>' +
      '<div class="rename-right">' +
        '<div class="pane-header"><span>选中项精校</span><span class="muted" id="lblRenStep2Count">显示 ' + visible.length + ' · 已选 ' + selectedToApply.length + '</span></div>' +
        '<div class="rename-inspector-scroll">' + inspectorHtml + '</div>' +
      '</div>' +
    '</div>';
  }

  function renderRenameStep3(selectedToApply) {
    var st = AGY.state;
    var esc = AGY.ui.escapeHtml;
    var ide = (st.overview && st.overview.ide) || {};
    var rows = selectedToApply.map(function (c) {
      return '<tr>' +
        '<td style="width:130px"><span class="badge badge-neutral">' + esc(c.projectName) + '</span></td>' +
        '<td class="muted">' + esc(c.oldTitle) + '</td>' +
        '<td style="font-weight:600">' + esc(getEffectiveNewTitle(c)) + '</td>' +
      '</tr>';
    }).join('');

    var stratHtml = ide.running
      ? ('<div style="margin-top:10px">' +
           '<strong>检测到 Antigravity 正在运行（' + ((ide.parentCount || 0) + (ide.lsCount || 0)) + ' 进程），请选择写入模式：</strong>' +
           '<div class="strategy-cards">' +
             '<div class="strategy-card ' + (st.renameStrategy === 'CloseAndFullWrite' ? 'selected' : '') + '" data-rstrat="CloseAndFullWrite">' +
               '<strong>1. 关闭 IDE 并三层全写（推荐）</strong>' +
               '<div class="muted" style="font-size:12px;margin-top:4px">关闭进程后同步更新 .pbtxt / SQLite / .pb，重新打开立即显示新标题。</div>' +
             '</div>' +
             '<div class="strategy-card ' + (st.renameStrategy === 'HotWritePersistentLayers' ? 'selected' : '') + '" data-rstrat="HotWritePersistentLayers">' +
               '<strong>2. 免关机热写持久化层</strong>' +
               '<div class="muted" style="font-size:12px;margin-top:4px">不关闭 IDE，写入 .pbtxt 与 SQLite（自动跳过运行中会话），重启 IDE 后生效。</div>' +
             '</div>' +
           '</div>' +
         '</div>')
      : '<div class="badge badge-green" style="margin-top:10px">IDE 当前未运行，将直接执行 .pbtxt / SQLite / .pb 三层全量原子写入并自动生成快照备份。</div>';

    return '<div class="card" style="flex:1;display:flex;flex-direction:column;min-height:0">' +
      '<div class="between wrap gap-sm" style="margin-bottom:10px">' +
        '<strong>即将重命名以下 ' + selectedToApply.length + ' 条会话（自动创建行级回滚快照）</strong>' +
        '<div class="row gap-sm">' +
          '<button class="btn" id="btnBackToStep2">← 返回精校</button>' +
          '<button class="btn btn-primary" id="btnExecRenameFinal"' + (selectedToApply.length === 0 ? ' disabled' : '') + '>执行三层写入</button>' +
        '</div>' +
      '</div>' +
      stratHtml +
      '<div class="matrix-scroll" style="margin-top:12px;border:1px solid var(--border);border-radius:8px">' +
        '<table class="data-table"><thead><tr><th>工程</th><th>原标题</th><th>将写入新标题 (Diff 预览)</th></tr></thead><tbody>' +
          (rows || '<tr><td colspan="3" class="empty-state">尚未勾选任何会话</td></tr>') +
        '</tbody></table>' +
      '</div>' +
    '</div>';
  }

  function bindRenameEvents(visible, selectedToApply) {
    var el = AGY.dom.el;
    var st = AGY.state;
    var root = el('mainCanvas');

    var stepBtns = root.querySelectorAll('[data-rstep]');
    for (var i = 0; i < stepBtns.length; i++) {
      stepBtns[i].addEventListener('click', function () {
        st.renameStep = parseInt(this.getAttribute('data-rstep'), 10);
        renderRenameWizard();
      });
    }
    el('btnOpenRulesDrawer').addEventListener('click', openRuleEditorDrawer);
    el('btnOpenRollbackDrawer').addEventListener('click', openRollbackHistoryDrawer);

    if (st.renameStep === 1) {
      el('selRenameProj').addEventListener('change', function () {
        st.renameProjectFilter = this.value;
        renderRenameWizard();
      });
      var rtoggles = root.querySelectorAll('[data-rtoggle]');
      for (var ti = 0; ti < rtoggles.length; ti++) {
        rtoggles[ti].addEventListener('click', function () {
          var k = this.getAttribute('data-rtoggle');
          if (k === 'tagged') st.renameShowTagged = !st.renameShowTagged;
          else if (k === 'aux') st.renameShowAux = !st.renameShowAux;
          else if (k === 'running') st.renameShowRunning = !st.renameShowRunning;
          else if (k === 'low') st.renameShowLowValue = !st.renameShowLowValue;
          renderRenameWizard();
        });
      }
      el('btnRenSelAll').addEventListener('click', function () {
        for (var j = 0; j < visible.length; j++) st.renameChecked[visible[j].cid] = true;
        renderRenameWizard();
      });
      el('btnRenSelNone').addEventListener('click', function () {
        for (var j = 0; j < visible.length; j++) st.renameChecked[visible[j].cid] = false;
        renderRenameWizard();
      });
      el('btnStep1To2').addEventListener('click', function () { st.renameStep = 2; renderRenameWizard(); });

      var s1Chks = root.querySelectorAll('.ren-step1-chk');
      for (var s1 = 0; s1 < s1Chks.length; s1++) {
        s1Chks[s1].addEventListener('change', function () {
          st.renameChecked[this.getAttribute('data-cid')] = this.checked;
          AGY.dom.patchCheckboxRow(this.closest('tr'), this.checked);
          var cnt = getFilteredRenameCandidates().filter(function (x) { return !!st.renameChecked[x.cid]; }).length;
          var btn = el('btnStep1To2');
          if (btn) {
            btn.disabled = cnt === 0;
            btn.textContent = '下一步：润色与精校 (' + cnt + ') →';
          }
          var step2Btn = root.querySelector('[data-rstep="2"]');
          if (step2Btn) step2Btn.textContent = '② 规则精校 / agy CLI / AI 润色 (' + cnt + ')';
        });
      }
      return;
    }

    if (st.renameStep === 3) {
      var sCards = root.querySelectorAll('[data-rstrat]');
      for (var sc = 0; sc < sCards.length; sc++) {
        sCards[sc].addEventListener('click', function () {
          st.renameStrategy = this.getAttribute('data-rstrat');
          for (var x = 0; x < sCards.length; x++) sCards[x].classList.remove('selected');
          this.classList.add('selected');
        });
      }
      el('btnBackToStep2').addEventListener('click', function () { st.renameStep = 2; renderRenameWizard(); });
      async function executeRenameCommit(chosenStrategy) {
        var payload = selectedToApply.map(function (c) { return { cid: c.cid, newTitle: getEffectiveNewTitle(c) }; });
        try {
          var ide = (st.overview && st.overview.ide) || {};
          var strat = chosenStrategy || (ide.running ? st.renameStrategy : 'auto');
          var res = await AGY.api.apiFetch('/api/rename/execute', {
            method: 'POST',
            body: JSON.stringify({ items: payload, strategy: strat })
          });
          var actualRenamed = res.renamedCount !== undefined ? res.renamedCount : payload.length;
          var skippedCount = res.skippedRunning ? res.skippedRunning.length : 0;
          st.renameEdits = {};
          st.renameAiModified = {};
          st.renameChecked = {};
          st.renameScopedCids = {};
          if (st.renameProjectFilter === '__SCOPED__') st.renameProjectFilter = '__ALL__';
          st.renameStep = 1;
          await AGY.app.refreshAll();
          AGY.api.showToast('实际更新 ' + actualRenamed + ' 条会话' + (skippedCount > 0 ? ('（跳过运行中 ' + skippedCount + ' 条）') : '，快照已保存 ✓'), 'success');
          if (res.snapshotDir) {
            AGY.ui.openModal(
              '三层重命名执行完成',
              '<div>请求提交：<strong>' + payload.length + '</strong> 条<br>实际更新：<strong>' + actualRenamed + '</strong> 条<br>跳过运行中：<strong>' + skippedCount + '</strong> 条<br>快照备份：<span class="mono" style="font-size:12px">' + AGY.ui.escapeHtml(res.snapshotDir) + '</span></div>',
              '<button class="btn" data-act="rb">查看/回滚快照</button><button class="btn btn-primary" data-act="ok">完成</button>',
              function (_b, mf) {
                mf.querySelector('[data-act="ok"]').addEventListener('click', AGY.ui.closeModal);
                mf.querySelector('[data-act="rb"]').addEventListener('click', function () { AGY.ui.closeModal(); openRollbackHistoryDrawer(); });
              }
            );
          }
        } catch (e) {
          if (e.status === 409 && e.data && e.data.code === 'IDE_RUNNING') {
            AGY.ui.openDualModeRenameModal(e.data.parentCount, e.data.lsCount, function (chosen) {
              st.renameStrategy = chosen;
              executeRenameCommit(chosen);
            });
          } else { AGY.api.showToast(e.message, 'error'); }
        }
      }
      el('btnExecRenameFinal').addEventListener('click', function () {
        executeRenameCommit(null);
      });
      return;
    }

    // Step 2 bindings (with partial DOM update!)
    function getLiveSelectedInStep2() {
      return getFilteredRenameCandidates().filter(function (x) { return !!st.renameChecked[x.cid]; });
    }
    el('btnBackToStep1').addEventListener('click', function () { st.renameStep = 1; renderRenameWizard(); });
    el('btnRenRecompute').addEventListener('click', async function () {
      st.renameEdits = {};
      st.renameAiModified = {};
      var rd = await AGY.api.apiFetch('/api/rename/candidates');
      AGY.app.applyFreshRenameData(rd);
      renderRenameWizard();
      AGY.api.showToast('已按最新规则重算建议标题', 'success');
    });
    el('btnNextToConfirm').addEventListener('click', function () { st.renameStep = 3; renderRenameWizard(); });
    el('btnOpenAiDrawer').addEventListener('click', function () { openAiPolishDrawer(visible, getLiveSelectedInStep2()); });
    el('btnOpenCliSuggestDrawer').addEventListener('click', function () { openCliSuggestDrawer(getLiveSelectedInStep2()); });

    var chks = root.querySelectorAll('.ren-chk');
    for (var ci = 0; ci < chks.length; ci++) {
      chks[ci].addEventListener('click', function (ev) { ev.stopPropagation(); });
      chks[ci].addEventListener('change', function () {
        st.renameChecked[this.getAttribute('data-cid')] = this.checked;
        var cnt = getFilteredRenameCandidates().filter(function (x) { return !!st.renameChecked[x.cid]; }).length;
        var nextBtn = el('btnNextToConfirm');
        if (nextBtn) {
          nextBtn.disabled = cnt === 0;
          nextBtn.textContent = '下一步：Diff 确认 (' + cnt + ') →';
        }
        AGY.dom.setText('lblRenStep2Count', '显示 ' + visible.length + ' · 已选 ' + cnt);
        var step2Btn = root.querySelector('[data-rstep="2"]');
        if (step2Btn) step2Btn.textContent = '② 规则精校 / agy CLI / AI 润色 (' + cnt + ')';
      });
    }

    var cards = root.querySelectorAll('[data-cand-focus]');
    for (var fi = 0; fi < cards.length; fi++) {
      cards[fi].addEventListener('click', function () {
        st.renameFocusedCid = this.getAttribute('data-cand-focus');
        AGY.dom.preserveScroll('#renStep2List', renderRenameWizard);
      });
    }

    var focused = visible.filter(function (x) { return x.cid === st.renameFocusedCid; })[0];
    if (focused) {
      var ta = el('txtEditNewTitle');
      ta.addEventListener('input', function () {
        st.renameEdits[focused.cid] = this.value;
        AGY.dom.setText('lblNewTitleLen', this.value.length + ' 字');
        var listTitleEl = root.querySelector('[data-cand-new-cid="' + focused.cid + '"]');
        if (listTitleEl) listTitleEl.textContent = '新：' + this.value;
      });
      el('btnRestoreRuleTitle').addEventListener('click', function () {
        delete st.renameEdits[focused.cid];
        delete st.renameAiModified[focused.cid];
        AGY.dom.preserveScroll('#renStep2List', renderRenameWizard);
      });
      el('btnChipApplyPrefix').addEventListener('click', function () {
        var cur = getEffectiveNewTitle(focused);
        var stripped = cur.replace(/^【[^】]{1,34}】\s*/, '');
        st.renameEdits[focused.cid] = (focused.suggestedPrefix || '') + stripped;
        AGY.dom.preserveScroll('#renStep2List', renderRenameWizard);
      });
      el('btnTogglePromptRaw').addEventListener('click', function () {
        st.renamePromptExpanded = !st.renamePromptExpanded;
        AGY.dom.preserveScroll('#renStep2List', renderRenameWizard);
      });
      el('btnRenTechToggle').addEventListener('click', function () {
        st.renameTechExpanded = !st.renameTechExpanded;
        var tb = el('renTechBody');
        tb.classList.toggle('hidden', !st.renameTechExpanded);
        this.textContent = (st.renameTechExpanded ? '▾' : '▸') + ' 技术信息（路径 / 分支线索 / 运行状态）';
      });
    }
  }

  /* ── Zero-Key `agy` CLI Suggest Drawer ── */
  async function openCliSuggestDrawer(selectedToApply) {
    var cids = selectedToApply.map(function (c) { return c.cid; });
    if (cids.length === 0) { AGY.api.showToast('请先勾选至少 1 条会话', 'error'); return; }

    var esc = AGY.ui.escapeHtml;
    var savedPath = (AGY.state.companionSettings && AGY.state.companionSettings.customAgyCliPath) || '';
    var cliStat = { available: false, resolvedPath: '', versionOrHint: '正在检测本地 agy CLI...' };
    var suggestions = null;

    function renderInner() {
      var statusBadge = cliStat.available
        ? '<span class="badge badge-green">CLI 已就绪</span>'
        : '<span class="badge badge-amber">未检测到 CLI</span>';

      var sugTable = '';
      if (suggestions && suggestions.length > 0) {
        sugTable =
          '<div class="card" style="margin-top:12px">' +
            '<div class="between" style="margin-bottom:8px"><strong>CLI 生成结果 (' + suggestions.length + ' 条)</strong><button class="btn btn-sm btn-primary" id="btnApplyCliSuggestions">应用到待改清单</button></div>' +
            '<div style="max-height:260px;overflow:auto;border:1px solid var(--border);border-radius:8px">' +
              '<table class="data-table"><thead><tr><th>原标题</th><th>CLI 建议标题</th><th>摘要</th></tr></thead><tbody>' +
              suggestions.map(function (s) {
                return '<tr><td class="muted">' + esc(s.oldTitle) + '</td><td style="font-weight:600">' + esc(s.newTitle || s.title) + '</td><td class="muted" style="font-size:11.5px">' + esc(s.summary || '') + '</td></tr>';
              }).join('') +
              '</tbody></table>' +
            '</div>' +
          '</div>';
      }

      return '<div class="card" style="margin-bottom:12px">' +
          '<div class="between wrap gap-sm" style="margin-bottom:8px"><strong>Zero-Key 本地 agy CLI 状态</strong>' + statusBadge + '</div>' +
          '<div class="muted mono" style="font-size:11.5px;margin-bottom:8px">' + esc(cliStat.resolvedPath || cliStat.versionOrHint || '') + '</div>' +
          '<label class="field-label">自定义 agy.exe / agy.cmd 路径（可选，留空自动探测 PATH）</label>' +
          '<div class="row gap-sm">' +
            '<input class="input" id="txtCliCustomPath" value="' + esc(savedPath) + '" placeholder="例如 C:\\Users\\...\\agy.cmd" />' +
            '<button class="btn btn-sm" id="btnCliBrowse">浏览...</button>' +
            '<button class="btn btn-sm" id="btnCliRecheck">保存并检测</button>' +
          '</div>' +
        '</div>' +
        '<div class="card">' +
          '<div class="between wrap gap-sm">' +
            '<div><strong>调用 <span class="mono">agy -p --output-format json</span></strong><div class="muted" style="font-size:12px">已选中 ' + cids.length + ' 条会话，零 API Key 直出结构化标题与摘要。</div></div>' +
            '<button class="btn btn-primary" id="btnRunCliSuggest">⚡ 立即生成建议</button>' +
          '</div>' +
        '</div>' +
        sugTable;
    }

    AGY.ui.openDrawer('Zero-Key agy CLI 智能命名', '利用本机已认证的 Antigravity CLI 无密钥批量生成标题', renderInner(), '', async function (db) {
      async function probeAndBind() {
        try {
          cliStat = await AGY.api.apiFetch('/api/rename/cli-status?customCliPath=' + encodeURIComponent(savedPath));
        } catch (_e) {}
        db.innerHTML = renderInner();
        bindEvents();
      }
      function bindEvents() {
        var inp = db.querySelector('#txtCliCustomPath');
        db.querySelector('#btnCliBrowse').addEventListener('click', async function () {
          var pick = await AGY.api.apiFetch('/api/native/pick-open-file', { method: 'POST', body: JSON.stringify({ title: '选择 agy.exe / agy.cmd' }) });
          if (!pick.cancelled && pick.path) {
            savedPath = pick.path;
            inp.value = savedPath;
          }
        });
        db.querySelector('#btnCliRecheck').addEventListener('click', async function () {
          savedPath = inp.value.trim();
          try {
            await AGY.api.apiFetch('/api/settings/companion', { method: 'POST', body: JSON.stringify({ customAgyCliPath: savedPath }) });
            if (AGY.state.companionSettings) AGY.state.companionSettings.customAgyCliPath = savedPath;
          } catch (_e) {}
          await probeAndBind();
        });
        db.querySelector('#btnRunCliSuggest').addEventListener('click', async function () {
          savedPath = inp.value.trim();
          var btn = this;
          btn.disabled = true;
          btn.textContent = '正在调用 agy CLI...';
          try {
            var res = await AGY.api.apiFetch('/api/rename/cli-suggest', {
              method: 'POST',
              body: JSON.stringify({ cids: cids, customCliPath: savedPath, timeoutSec: 60 })
            });
            if (!res.ok) {
              AGY.api.showToast('CLI 生成未成功：' + (res.errorOrHint || res.status), 'error');
            } else {
              suggestions = res.suggestions || [];
              AGY.api.showToast('agy CLI 生成了 ' + suggestions.length + ' 条建议 ✓', 'success');
              db.innerHTML = renderInner();
              bindEvents();
              return;
            }
          } catch (e) {
            AGY.api.showToast('调用失败：' + e.message, 'error');
          }
          btn.disabled = false;
          btn.textContent = '⚡ 立即生成建议';
        });
        var applyBtn = db.querySelector('#btnApplyCliSuggestions');
        if (applyBtn) {
          applyBtn.addEventListener('click', function () {
            for (var i = 0; i < suggestions.length; i++) {
              var s = suggestions[i];
              AGY.state.renameEdits[s.cid] = s.newTitle || s.title;
              AGY.state.renameAiModified[s.cid] = true;
              AGY.state.renameChecked[s.cid] = true;
            }
            AGY.ui.closeDrawer(true);
            renderRenameWizard();
            AGY.api.showToast('已将 ' + suggestions.length + ' 条 CLI 建议应用至清单 ✓', 'success');
          });
        }
      }
      await probeAndBind();
    });
  }

  /* ── Offline AI Clipboard / JSON Exchange Drawer ── */
  async function openAiPolishDrawer(_visible, selectedToApply) {
    var cids = selectedToApply.map(function (c) { return c.cid; });
    if (cids.length === 0) { AGY.api.showToast('请先勾选至少 1 条会话', 'error'); return; }

    var esc = AGY.ui.escapeHtml;
    var promptRes = await AGY.api.apiFetch('/api/rename/ai-prompt', { method: 'POST', body: JSON.stringify({ cids: cids }) });
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
              return '<tr><td class="muted">' + esc(ch.oldTitle) + '</td><td style="font-weight:600">' + esc(ch.newTitle) + '</td></tr>';
            }).join('') +
            '</tbody></table>' +
          '</div>';
      }

      if (activeTab === 'clipboard') {
        return tabHtml +
          '<div class="card" style="margin-bottom:12px">' +
            '<div class="between"><strong>Step 1：复制待润色提示词（已包含 ' + promptRes.count + ' 条首问）</strong><button class="btn btn-sm btn-primary" id="btnCopyAiPrompt">复制提示词</button></div>' +
            '<textarea class="textarea mono" rows="6" readonly style="margin-top:8px;font-size:11.5px">' + esc(promptRes.prompt) + '</textarea>' +
          '</div>' +
          '<div class="card">' +
            '<div class="between"><strong>Step 2：粘贴外部 AI 返回的 JSON</strong><button class="btn btn-sm" id="btnParseAiPaste">解析预览</button></div>' +
            '<textarea class="textarea mono" id="txtAiPaste" rows="6" style="margin-top:8px;font-size:11.5px" placeholder="在此粘贴外部模型输出的 JSON 数组或 ```json 代码块..."></textarea>' +
            prevTable +
            (parsedPreview ? '<div class="between" style="margin-top:10px"><span class="badge badge-green">命中 ' + parsedPreview.matchedCount + ' 条</span><button class="btn btn-sm btn-primary" id="btnApplyAiChanges">应用到待改清单</button></div>' : '') +
          '</div>';
      }

      return tabHtml +
        '<div class="card">' +
          '<div class="row gap-sm wrap">' +
            '<button class="btn btn-sm" id="btnSaveAiPromptTxt">导出提示词 (.txt)...</button>' +
            '<button class="btn btn-sm btn-primary" id="btnLoadAiJsonFile">导入 AI 结果 JSON...</button>' +
          '</div>' +
          prevTable +
          (parsedPreview ? '<div class="between" style="margin-top:10px"><span class="badge badge-green">命中 ' + parsedPreview.matchedCount + ' 条</span><button class="btn btn-sm btn-primary" id="btnApplyAiChanges">应用到待改清单</button></div>' : '') +
        '</div>';
    }

    AGY.ui.openDrawer('离线 AI 标题润色助手', '零 API Key · 剪贴板或文件双向安全交换', renderDrawerContent(), '', bindAiDrawer);

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
      if (btnCopy) btnCopy.addEventListener('click', function () {
        AGY.api.copyToClipboard(promptRes.prompt || '', '提示词已复制 ✓');
      });
      var btnParse = db.querySelector('#btnParseAiPaste');
      if (btnParse) btnParse.addEventListener('click', async function () {
        var raw = db.querySelector('#txtAiPaste').value;
        try {
          parsedPreview = await AGY.api.apiFetch('/api/rename/ai-parse', { method: 'POST', body: JSON.stringify({ rawText: raw, cids: cids }) });
          var keptText = raw;
          db.innerHTML = renderDrawerContent();
          db.querySelector('#txtAiPaste').value = keptText;
          bindAiDrawer(db);
          AGY.api.showToast('解析成功：命中 ' + parsedPreview.matchedCount + ' 条', 'success');
        } catch (e) { AGY.api.showToast(e.message, 'error'); }
      });
      var btnApply = db.querySelector('#btnApplyAiChanges');
      if (btnApply) btnApply.addEventListener('click', function () {
        if (!parsedPreview || !parsedPreview.changes) return;
        for (var k = 0; k < parsedPreview.changes.length; k++) {
          var ch = parsedPreview.changes[k];
          AGY.state.renameEdits[ch.cid] = ch.newTitle;
          AGY.state.renameAiModified[ch.cid] = true;
          AGY.state.renameChecked[ch.cid] = true;
        }
        AGY.ui.closeDrawer(true);
        renderRenameWizard();
        AGY.api.showToast('已将 ' + parsedPreview.changes.length + ' 条 AI 标题应用至待改清单', 'success');
      });
      var btnSave = db.querySelector('#btnSaveAiPromptTxt');
      if (btnSave) btnSave.addEventListener('click', async function () {
        try {
          var res = await AGY.api.apiFetch('/api/rename/ai-save-prompt', { method: 'POST', body: JSON.stringify({ cids: cids }) });
          if (!res.cancelled && res.outputPath) AGY.api.showToast('已保存提示词（' + res.count + ' 条）：' + res.outputPath, 'success');
        } catch (e) { AGY.api.showToast(e.message, 'error'); }
      });
      var btnLoad = db.querySelector('#btnLoadAiJsonFile');
      if (btnLoad) btnLoad.addEventListener('click', async function () {
        try {
          var res = await AGY.api.apiFetch('/api/rename/ai-load-json', { method: 'POST', body: JSON.stringify({ cids: cids }) });
          if (res.cancelled) return;
          parsedPreview = res;
          db.innerHTML = renderDrawerContent();
          bindAiDrawer(db);
          AGY.api.showToast('解析成功：命中 ' + parsedPreview.matchedCount + ' 条', 'success');
        } catch (e) { AGY.api.showToast(e.message, 'error'); }
      });
    }
  }

  /* ── Local Rules Editor Drawer (with multiRepoRules CRUD!) ── */
  async function openRuleEditorDrawer() {
    var esc = AGY.ui.escapeHtml;
    var data = await AGY.api.apiFetch('/api/rename/rules');
    function normalizeRulesObj(obj) {
      var o = (obj && typeof obj === 'object' && !Array.isArray(obj)) ? obj : {};
      if (!o.version) o.version = 1;
      o.keepExistingBracketedPrefix = !!o.keepExistingBracketedPrefix;
      if (!o.projectAliases || typeof o.projectAliases !== 'object' || Array.isArray(o.projectAliases)) o.projectAliases = {};
      if (!Array.isArray(o.multiRepoRules)) o.multiRepoRules = [];
      for (var i = 0; i < o.multiRepoRules.length; i++) {
        var mr = o.multiRepoRules[i];
        if (!mr || typeof mr !== 'object') {
          mr = { projectName: '', defaultModel: '', branchRegexes: [], modelMatchers: [] };
          o.multiRepoRules[i] = mr;
        }
        if (!Array.isArray(mr.branchRegexes)) mr.branchRegexes = [];
        if (!Array.isArray(mr.modelMatchers)) mr.modelMatchers = [];
      }
      return o;
    }

    var mode = 'form';
    var rulesObj = normalizeRulesObj(JSON.parse(JSON.stringify(data.rulesObj || {})));
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
            '<td><input class="input alias-k" data-idx="' + idx + '" value="' + esc(k) + '" /></td>' +
            '<td><input class="input alias-v" data-idx="' + idx + '" value="' + esc(rulesObj.projectAliases[k]) + '" /></td>' +
            '<td style="width:60px"><button class="btn btn-xs btn-danger-outline alias-del" data-idx="' + idx + '">删</button></td>' +
          '</tr>';
        }).join('') || '<tr><td colspan="3" class="muted">暂无工程简称映射，点击右上「+ 新增映射」添加。</td></tr>';

        var multiCards = (rulesObj.multiRepoRules || []).map(function (mr, mIdx) {
          var matchRows = (mr.modelMatchers || []).map(function (mm, kIdx) {
            return '<tr>' +
              '<td style="width:140px"><input class="input mm-model" data-midx="' + mIdx + '" data-kidx="' + kIdx + '" value="' + esc(mm.model) + '" /></td>' +
              '<td><input class="input mm-kw" data-midx="' + mIdx + '" data-kidx="' + kIdx + '" value="' + esc((mm.keywords || []).join(', ')) + '" /></td>' +
              '<td style="width:52px"><button class="btn btn-xs btn-danger-outline mm-del" data-midx="' + mIdx + '" data-kidx="' + kIdx + '">删</button></td>' +
            '</tr>';
          }).join('') || '<tr><td colspan="3" class="muted">暂无匹配关键词</td></tr>';

          return '<div class="card" style="margin-top:12px">' +
            '<div class="between" style="margin-bottom:8px"><strong>多仓规则 #' + (mIdx + 1) + '</strong><button class="btn btn-xs btn-danger-outline mr-del" data-midx="' + mIdx + '">删除此规则</button></div>' +
            '<div class="row gap-md" style="margin-bottom:10px">' +
              '<div style="flex:1"><label class="field-label">多仓工程名</label><input class="input mr-proj" data-midx="' + mIdx + '" value="' + esc(mr.projectName) + '" /></div>' +
              '<div style="flex:1"><label class="field-label">未命中时默认标签</label><input class="input mr-def" data-midx="' + mIdx + '" value="' + esc(mr.defaultModel) + '" /></div>' +
            '</div>' +
            '<label class="field-label">分支提取正则（每行一条）</label>' +
            '<textarea class="textarea mono mr-regex" data-midx="' + mIdx + '" rows="2" style="font-size:11.5px">' + esc((mr.branchRegexes || []).join('\n')) + '</textarea>' +
            '<div class="between" style="margin:10px 0 6px 0"><strong style="font-size:12px">子项目/分支关键词优先匹配列表</strong><button class="btn btn-xs mm-add" data-midx="' + mIdx + '">+ 新增匹配项</button></div>' +
            '<table class="data-table"><thead><tr><th>前缀标签</th><th>关键词（英文逗号分隔）</th><th></th></tr></thead><tbody>' + matchRows + '</tbody></table>' +
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
          '</div>' +
          '<div class="between" style="margin-top:14px"><strong>多仓/分支识别规则 (multiRepoRules)</strong><button class="btn btn-xs btn-primary" id="btnAddMultiRepoRule">+ 新增多仓规则</button></div>' +
          (multiCards || '<div class="card muted" style="margin-top:8px">当前无多仓规则，点击上方「+ 新增多仓规则」创建。</div>');
      } else {
        mainHtml =
          '<div class="card">' +
            '<div class="muted" style="margin-bottom:6px;font-size:12px">规则文件：' + esc(data.rulesPath) + '</div>' +
            '<textarea class="textarea mono" id="txtRawRulesJson" rows="20" style="font-size:12px">' + esc(rawJsonText) + '</textarea>' +
          '</div>';
      }

      return modeSwitch + mainHtml + sandboxHtml;
    }

    var hdrBtns = '<button class="btn btn-sm btn-primary" id="btnSaveRulesNow">保存并应用规则</button>';
    AGY.ui.openDrawer('会话自动命名规则编辑器', '支持可视化增删工程简称与 multiRepoRules，并实时针对本机会话沙盒试跑', renderRulesInner(), hdrBtns, bindRuleEvents, function () { return dirty; });

    function collectFormIntoObj() {
      if (mode !== 'form') return;
      var db = AGY.dom.el('drawerBody');
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
        mr.branchRegexes = db.querySelectorAll('.mr-regex')[m].value.split(/\r?\n/).map(function (s) { return s.trim(); }).filter(Boolean);
      }
      var mmModels = db.querySelectorAll('.mm-model');
      var mmKws = db.querySelectorAll('.mm-kw');
      for (var x = 0; x < mmModels.length; x++) {
        var mi = parseInt(mmModels[x].getAttribute('data-midx'), 10);
        var ki = parseInt(mmModels[x].getAttribute('data-kidx'), 10);
        if (rulesObj.multiRepoRules[mi] && rulesObj.multiRepoRules[mi].modelMatchers[ki]) {
          rulesObj.multiRepoRules[mi].modelMatchers[ki].model = mmModels[x].value.trim();
          rulesObj.multiRepoRules[mi].modelMatchers[ki].keywords = mmKws[x].value.split(',').map(function (s) { return s.trim(); }).filter(Boolean);
        }
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
            try { rulesObj = normalizeRulesObj(JSON.parse(rawJsonText)); } catch (e) { AGY.api.showToast('JSON 语法错误，无法切回表单：' + e.message, 'error'); return; }
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

      if (db.querySelector('#btnAddMultiRepoRule')) {
        db.querySelector('#btnAddMultiRepoRule').addEventListener('click', function () {
          collectFormIntoObj();
          normalizeRulesObj(rulesObj);
          rulesObj.multiRepoRules.push({
            projectName: 'MyMonorepo',
            defaultModel: 'Main',
            branchRegexes: ['(?:分支|branch)[:：\\s]+([A-Za-z0-9._-]+)'],
            modelMatchers: [{ model: 'Core', keywords: ['core', 'kernel'] }]
          });
          dirty = true;
          db.innerHTML = renderRulesInner();
          bindRuleEvents(db, ha);
        });
      }

      var mrDelBtns = db.querySelectorAll('.mr-del');
      for (var mr = 0; mr < mrDelBtns.length; mr++) {
        mrDelBtns[mr].addEventListener('click', function () {
          collectFormIntoObj();
          rulesObj.multiRepoRules.splice(parseInt(this.getAttribute('data-midx'), 10), 1);
          dirty = true;
          db.innerHTML = renderRulesInner();
          bindRuleEvents(db, ha);
        });
      }

      var mmAddBtns = db.querySelectorAll('.mm-add');
      for (var ma = 0; ma < mmAddBtns.length; ma++) {
        mmAddBtns[ma].addEventListener('click', function () {
          collectFormIntoObj();
          normalizeRulesObj(rulesObj);
          var mi = parseInt(this.getAttribute('data-midx'), 10);
          rulesObj.multiRepoRules[mi].modelMatchers.push({ model: 'NewTag', keywords: ['kw1'] });
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
          var testRes = await AGY.api.apiFetch('/api/rename/rules/test', { method: 'POST', body: JSON.stringify(payload) });
          sandboxHtml = '<div class="card" style="margin-top:12px"><strong>沙盒试跑效果预览（前 ' + (testRes.samples || []).length + ' 条）</strong>' +
            '<table class="data-table" style="margin-top:8px"><thead><tr><th>原标题</th><th>合成预览</th></tr></thead><tbody>' +
            (testRes.samples || []).map(function (s) {
              return '<tr><td class="muted">' + esc(s.oldTitle) + '</td><td style="font-weight:600">' + esc(s.suggestedTitle) + (s.isLowValue ? ' <span class="badge badge-neutral">低价值</span>' : '') + '</td></tr>';
            }).join('') +
            '</tbody></table></div>';
          db.innerHTML = renderRulesInner();
          bindRuleEvents(db, ha);
        } catch (e) { AGY.api.showToast('规则校验失败：' + e.message, 'error'); }
      });

      var saveBtn = ha.querySelector('#btnSaveRulesNow');
      if (saveBtn) {
        saveBtn.onclick = async function () {
          try {
            var payload;
            if (mode === 'form') { collectFormIntoObj(); payload = { rulesObj: rulesObj }; }
            else { rawJsonText = db.querySelector('#txtRawRulesJson').value; payload = { rawJson: rawJsonText }; }
            var saved = await AGY.api.apiFetch('/api/rename/rules', { method: 'POST', body: JSON.stringify(payload) });
            dirty = false;
            AGY.state.drawerDirtyCheck = null;
            rulesObj = saved.rulesObj;
            rawJsonText = saved.rawJson;
            var rd = await AGY.api.apiFetch('/api/rename/candidates');
            AGY.app.applyFreshRenameData(rd);
            if (AGY.state.view === 'sessions' && AGY.state.sessionsSubMode === 'rename') renderRenameWizard();
            AGY.api.showToast('命名规则已保存并应用 ✓', 'success');
          } catch (e) { AGY.api.showToast('保存规则失败：' + e.message, 'error'); }
        };
      }
    }
  }

  async function openRollbackHistoryDrawer() {
    var esc = AGY.ui.escapeHtml;
    var res = await AGY.api.apiFetch('/api/rename/history');
    var snaps = res.snapshots || [];

    var body = snaps.length === 0
      ? '<div class="empty-state">暂无重命名历史快照。每次执行重命名均会自动保存逐条回滚快照（最多保留 15 份）。</div>'
      : snaps.map(function (sp, idx) {
          var itemRows = (sp.items || []).map(function (it) {
            return '<tr><td class="muted">' + esc(it.oldTitle) + '</td><td>' + esc(it.newTitle) + '</td></tr>';
          }).join('');
          return '<div class="card" style="margin-bottom:12px">' +
            '<div class="between">' +
              '<div><strong>' + esc(sp.createdAtIso || sp.folderName) + '</strong> <span class="badge badge-neutral">' + sp.itemCount + ' 条</span>' +
              (sp.hasPbBackup ? ' <span class="badge badge-green">含 .pb 备份</span>' : '') + '</div>' +
              '<button class="btn btn-sm btn-primary btn-rb-snap" data-idx="' + idx + '">还原此快照</button>' +
            '</div>' +
            '<div class="muted" style="margin:4px 0 8px 0;font-size:12px">' + esc(sp.previewSummary) + '</div>' +
            '<div style="max-height:150px;overflow:auto;border:1px solid var(--border);border-radius:6px">' +
              '<table class="data-table"><thead><tr><th>还原回原标题</th><th>重命名后标题</th></tr></thead><tbody>' + itemRows + '</tbody></table>' +
            '</div>' +
          '</div>';
        }).join('');

    AGY.ui.openDrawer('三层重命名快照与回滚历史', '支持从行级快照精准恢复 .pbtxt / SQLite / .pb，保留回滚后新建的其他会话', body, '', function (db) {
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
      await AGY.api.apiFetch('/api/rename/rollback', { method: 'POST', body: JSON.stringify({ snapshotDir: snapshotDir, strategy: strategy }) });
      AGY.ui.closeDrawer(true);
      await AGY.app.refreshAll('已从快照还原会话标题 ✓');
    } catch (e) {
      if (e.status === 409 && e.data && e.data.code === 'IDE_RUNNING') {
        AGY.ui.openDualModeRenameModal(e.data.parentCount, e.data.lsCount, function (chosen) {
          executeSnapshotRollback(snapshotDir, chosen);
        });
      } else { AGY.api.showToast(e.message, 'error'); }
    }
  }

  AGY.views.sessions = renderSessions;
})();
