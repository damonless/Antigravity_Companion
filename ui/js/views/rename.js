(function () {
  'use strict';

  var AGY = window.AGY = window.AGY || {};
  AGY.views = AGY.views || {};

  /* ══════════════════════════════════════════════════════
   * STANDALONE 3-STEP SMART RENAME WIZARD
   * Step 1: Scope & Locked Visibility
   * Step 2: Local Rules (multiRepoRules CRUD) + Zero-Key agy CLI + Offline Clipboard
   * Step 3: Diff Preview + Commit + Rollback
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
      el('mainCanvas').innerHTML = '<div class="empty-state">正在加载智能重命名工作台...</div>';
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

  /* ── Local Rules Editor Drawer (with multiRepoRules CRUD) ── */
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
            if (AGY.state.view === 'rename') renderRenameWizard();
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

  AGY.views.rename = renderRenameWizard;
  AGY.views.renderRename = renderRenameWizard;
})();
