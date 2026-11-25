(function () {
  'use strict';

  var AGY = window.AGY = window.AGY || {};
  AGY.views = AGY.views || {};

  function renderCapRowsHtml(filtered) {
    var st = AGY.state;
    var esc = AGY.ui.escapeHtml;
    if (filtered.length === 0) {
      return '<tr><td colspan="6" class="empty-state">当前筛选条件下无匹配项。</td></tr>';
    }
    return filtered.map(function (it) {
      var checked = !!st.capChecked[it.key];
      var cls = 'badge-neutral';
      if (it.direction === 'Aligned') cls = 'badge-green';
      else if (it.direction === 'RemoteNewer' || it.direction === 'RemoteOnly') cls = 'badge-blue';
      else if (it.direction === 'LocalNewer' || it.direction === 'LocalOnly') cls = 'badge-amber';
      else if (it.direction === 'NeedsReview' || it.direction === 'Conflict') cls = 'badge-red';
      else if (it.direction === 'BothModified') cls = 'badge-accent';

      var dirLabel = it.directionBadge || it.direction;
      if (it.direction === 'Aligned') dirLabel = '已一致';
      else if (it.direction === 'RemoteOnly') dirLabel = '仅同步库有';
      else if (it.direction === 'RemoteNewer') dirLabel = '同步库较新';
      else if (it.direction === 'LocalOnly') dirLabel = '仅本机有';
      else if (it.direction === 'LocalNewer') dirLabel = '本机较新';
      else if (it.direction === 'BothModified') dirLabel = '两边均有改动';
      else if (it.direction === 'NeedsReview' || it.direction === 'Conflict') dirLabel = '需人工确认';

      var tip = it.directionReason === 'baseline'
        ? '基于该配置同步库的上次对齐记录判断'
        : (it.directionReason === 'no_baseline_review' ? '首次对比此配置同步库，建议先点「对比源码」确认' : '按内容指纹比对');

      var missCount = (it.missingLocalPaths || []).length;
      var warn = missCount > 0
        ? (' <span class="badge badge-red">⚠ ' + missCount + ' 处本机路径缺失</span>')
        : ((it.hasMissingRemotePaths) ? ' <span class="badge badge-amber">同步库含未匹配路径</span>' : '');

      return '<tr class="' + (checked ? 'selected' : '') + '">' +
        '<td style="width:36px"><input type="checkbox" class="cap-chk" data-key="' + esc(it.key) + '"' + (checked ? ' checked' : '') + ' /></td>' +
        '<td><div class="row gap-sm"><span class="badge badge-neutral">' + esc(AGY.ui.kindLabel(it.kind)) + '</span><strong>' + esc(it.displayName) + '</strong></div>' +
          (it.description ? '<div class="muted" style="font-size:11.5px;margin-top:2px;max-width:420px;overflow:hidden;text-overflow:ellipsis;white-space:nowrap">' + esc(it.description) + '</div>' : '') + '</td>' +
        '<td><span class="badge ' + cls + '" title="' + esc(tip) + '">' + esc(dirLabel) + '</span>' + warn + '</td>' +
        '<td class="mono muted cap-time-cell">' + (it.localExists ? esc(it.localTime) : '—') + '</td>' +
        '<td class="mono muted cap-time-cell">' + (it.remoteExists ? esc(it.remoteTime) : '—') + '</td>' +
        '<td style="white-space:nowrap"><div class="row gap-xs">' +
          '<button class="btn btn-xs" data-cap-act="diff" data-key="' + esc(it.key) + '" type="button">对比源码</button>' +
          (it.remoteExists ? '<button class="btn btn-xs" data-cap-act="import" data-key="' + esc(it.key) + '" title="从配置同步库同步到本机" type="button">← 同步到本机</button>' : '') +
          (it.localExists ? '<button class="btn btn-xs" data-cap-act="push" data-key="' + esc(it.key) + '" title="存入配置同步库" type="button">存入同步库 →</button>' : '') +
          (missCount > 0 ? '<button class="btn btn-xs btn-danger-outline" data-cap-act="fixpath" data-key="' + esc(it.key) + '" type="button">修复路径 (' + missCount + ')</button>' : '') +
        '</div></td>' +
      '</tr>';
    }).join('');
  }

  function getFilteredCapabilities() {
    var st = AGY.state;
    var all = (st.capData && st.capData.items) || [];
    return all.filter(function (it) {
      if (st.capCategory !== 'ALL' && it.kind !== st.capCategory) return false;
      if (st.capDirFilter === 'DIFF' && it.direction === 'Aligned') return false;
      else if (st.capDirFilter === 'REMOTE_NEW' && it.direction !== 'RemoteNewer' && it.direction !== 'RemoteOnly') return false;
      else if (st.capDirFilter === 'LOCAL_NEW' && it.direction !== 'LocalNewer' && it.direction !== 'LocalOnly') return false;
      else if (st.capDirFilter === 'NEEDS_REVIEW' && it.direction !== 'NeedsReview' && it.direction !== 'Conflict' && it.direction !== 'BothModified') return false;
      else if (st.capDirFilter === 'MISSING' && !it.hasMissingLocalPaths && !it.hasMissingRemotePaths) return false;
      if (st.capSearch.trim()) {
        var q = st.capSearch.trim().toLowerCase();
        if (it.displayName.toLowerCase().indexOf(q) < 0 && (it.description || '').toLowerCase().indexOf(q) < 0) return false;
      }
      return true;
    });
  }

  function updateCapBatchButtons() {
    var selKeys = Object.keys(AGY.state.capChecked).filter(function (k) { return AGY.state.capChecked[k]; });
    var pushBtn = AGY.dom.el('btnCapPushSel');
    var impBtn = AGY.dom.el('btnCapImportSel');
    var delBtn = AGY.dom.el('btnCapDeleteMenu');
    if (pushBtn) { pushBtn.disabled = selKeys.length === 0; pushBtn.textContent = '存入配置同步库 (' + selKeys.length + ')'; }
    if (impBtn) { impBtn.disabled = selKeys.length === 0; impBtn.textContent = '同步到本机 (' + selKeys.length + ')'; }
    if (delBtn) delBtn.disabled = selKeys.length === 0;
  }

  function bindCapMatrixRowEvents() {
    var tbody = AGY.dom.el('capMatrixTbody');
    if (!tbody) return;
    var chks = tbody.querySelectorAll('.cap-chk');
    for (var ci = 0; ci < chks.length; ci++) {
      chks[ci].addEventListener('change', function () {
        AGY.state.capChecked[this.getAttribute('data-key')] = this.checked;
        AGY.dom.patchCheckboxRow(this.closest('tr'), this.checked);
        updateCapBatchButtons();
      });
    }
    var actBtns = tbody.querySelectorAll('[data-cap-act]');
    for (var ai = 0; ai < actBtns.length; ai++) {
      actBtns[ai].addEventListener('click', function () {
        var act = this.getAttribute('data-cap-act');
        var k = this.getAttribute('data-key');
        if (act === 'diff' || act === 'fixpath') AGY.ui.openCapabilityDiffDrawer(k, 'repo');
        else if (act === 'import') executeCapAction('/api/capabilities/import', { keys: [k], source: 'repo' }, '已同步到本机 ✓', this);
        else if (act === 'push') executeCapAction('/api/capabilities/push', { keys: [k] }, '已存入配置同步库 ✓', this);
      });
    }
  }

  function getRuleThreshold() {
    var st = AGY.state;
    var cfg = st.companionSettings;
    if (cfg && cfg.ruleTokenReferenceThreshold !== undefined) {
      var t = Number(cfg.ruleTokenReferenceThreshold || 0);
      return (!isNaN(t) && isFinite(t) && t > 0) ? t : 0;
    }
    var cp = st.cockpitData || {};
    var tb = cp.tokenBudget || {};
    return tb.threshold > 0 ? tb.threshold : 0;
  }

  function renderInlineEditorHub(hostEl) {
    var esc = AGY.ui.escapeHtml;
    var fmt = AGY.ui.formatBytes;
    var st = AGY.state;
    var host = hostEl || AGY.dom.el('cockpitSubHost') || AGY.dom.el('mainCanvas');
    var cp = st.cockpitData;
    if (!cp) {
      host.innerHTML = '<div class="empty-state">正在扫描规则、技能与项目定制文件...</div>';
      return;
    }

    var tb = cp.tokenBudget || {};
    var rules = cp.rules || [];
    var skills = cp.skills || [];
    var workspaces = cp.workspaces || [];
    var threshold = getRuleThreshold();
    var totalAlways = tb.totalAlwaysOnTokens || 0;
    var meterPct = threshold > 0 ? Math.min(100, Math.round((totalAlways / threshold) * 100)) : 0;
    var meterCls = threshold > 0 ? (totalAlways > threshold ? 'over' : (totalAlways > threshold * 0.75 ? 'warn' : '')) : '';

    var totalRuleBytes = 0;
    var totalRuleChars = 0;
    var totalRuleLines = 0;
    var overCount = 0;
    for (var ri = 0; ri < rules.length; ri++) {
      totalRuleBytes += (rules[ri].byteCount || 0);
      totalRuleChars += (rules[ri].charCount || 0);
      totalRuleLines += (rules[ri].lineCount || 0);
      if (threshold > 0 && (rules[ri].estimatedTokens || 0) > threshold) overCount++;
    }

    var tokenBadgeHtml = '<span class="badge badge-neutral">未设提醒阈值</span>';
    if (threshold > 0) {
      if (overCount > 0) tokenBadgeHtml = '<span class="badge badge-red">⚠ ' + esc(overCount) + ' 份规则超过参考阈值 (' + esc(threshold) + ')</span>';
      else if (totalAlways > threshold) tokenBadgeHtml = '<span class="badge badge-amber">常驻合计超过参考阈值 (' + esc(threshold) + ')</span>';
      else tokenBadgeHtml = '<span class="badge badge-green">在参考阈值内 (' + esc(threshold) + ')</span>';
    }

    var rulesRows = rules.map(function (r) {
      var bytes = r.byteCount !== undefined ? r.byteCount : 0;
      var chars = r.charCount !== undefined ? r.charCount : 0;
      var lines = r.lineCount !== undefined ? r.lineCount : 0;
      var toks = r.estimatedTokens || 0;
      var tokBadge;
      if (threshold <= 0) tokBadge = '<span class="badge badge-neutral">参考约 ' + esc(toks) + ' tokens</span>';
      else if (toks > threshold) tokBadge = '<span class="badge badge-red">⚠ 参考约 ' + esc(toks) + ' / ' + esc(threshold) + ' tokens</span>';
      else if (toks > threshold * 0.75) tokBadge = '<span class="badge badge-amber">参考约 ' + esc(toks) + ' / ' + esc(threshold) + ' tokens</span>';
      else tokBadge = '<span class="badge badge-green">参考约 ' + esc(toks) + ' / ' + esc(threshold) + ' tokens</span>';
      return '<tr>' +
        '<td><strong>' + esc(r.id) + '</strong><div class="muted mono" style="font-size:11px">' + esc(r.sourcePath) + '</div></td>' +
        '<td><span class="badge badge-neutral">' + esc(r.scope) + '</span></td>' +
        '<td>' +
          '<div class="mono muted" style="font-size:11.5px;margin-bottom:3px">' + esc(fmt(bytes)) + ' · ' + esc(chars) + ' 字符 · ' + esc(lines) + ' 行</div>' +
          tokBadge +
        '</td>' +
        '<td><button class="btn btn-xs vault-edit-btn" data-path="' + esc(r.sourcePath) + '" data-title="规则: ' + esc(r.id) + '" type="button">在线编辑</button></td>' +
      '</tr>';
    }).join('') || '<tr><td colspan="4" class="muted">未扫描到规则文件</td></tr>';

    var skillRows = skills.filter(function (s) { return !!s.sourcePath; }).map(function (s) {
      return '<tr>' +
        '<td><strong>' + esc(s.name || s.id) + '</strong><div class="muted mono" style="font-size:11px">' + esc(s.sourcePath) + '</div></td>' +
        '<td><span class="badge badge-neutral">' + esc(s.scope) + '</span></td>' +
        '<td><button class="btn btn-xs vault-edit-btn" data-path="' + esc(s.sourcePath) + '" data-title="技能: ' + esc(s.id) + '" type="button">编辑 SKILL.md</button></td>' +
      '</tr>';
    }).join('') || '<tr><td colspan="3" class="muted">未扫描到独立技能文件</td></tr>';

    var wsRows = workspaces.filter(function (w) { return w.exists; }).map(function (w) {
      var btns = [];
      if (w.hasGeminiMd) btns.push('<button class="btn btn-xs vault-edit-btn" data-path="' + esc(w.folderPath + '\\GEMINI.md') + '" data-title="' + esc(w.projectName) + '/GEMINI.md" type="button">GEMINI.md</button>');
      if (w.hasAgentsMd) btns.push('<button class="btn btn-xs vault-edit-btn" data-path="' + esc(w.folderPath + '\\AGENTS.md') + '" data-title="' + esc(w.projectName) + '/AGENTS.md" type="button">AGENTS.md</button>');
      if (w.hasSkillsJson) {
        var skillsPath = w.skillsJsonPath || (w.folderPath + '\\' + (w.agentsDirName ? w.agentsDirName + '\\' : '') + 'skills.json');
        btns.push('<button class="btn btn-xs vault-edit-btn" data-path="' + esc(skillsPath) + '" data-title="' + esc(w.projectName) + '/skills.json" type="button">skills.json</button>');
      }
      if (w.hasPluginsJson) {
        var pluginsPath = w.pluginsJsonPath || (w.folderPath + '\\' + (w.agentsDirName ? w.agentsDirName + '\\' : '') + 'plugins.json');
        btns.push('<button class="btn btn-xs vault-edit-btn" data-path="' + esc(pluginsPath) + '" data-title="' + esc(w.projectName) + '/plugins.json" type="button">plugins.json</button>');
      }
      if (w.hasHooksJson) {
        var hooksPath = w.hooksJsonPath || (w.folderPath + '\\' + (w.agentsDirName ? w.agentsDirName + '\\' : '') + 'hooks.json');
        btns.push('<button class="btn btn-xs vault-edit-btn" data-path="' + esc(hooksPath) + '" data-title="' + esc(w.projectName) + '/hooks.json" type="button">hooks.json</button>');
      }
      return '<tr>' +
        '<td><strong>' + esc(w.projectName) + '</strong><div class="muted mono" style="font-size:11px">' + esc(w.folderPath) + '</div></td>' +
        '<td>' + (w.hasAgentsDir ? '<span class="badge badge-green">' + esc(w.agentsDirName) + '/</span>' : '<span class="muted">根目录</span>') + '</td>' +
        '<td><div class="row gap-xs wrap">' + (btns.join('') || '<span class="muted">无定制文件</span>') + '</div></td>' +
      '</tr>';
    }).join('') || '<tr><td colspan="3" class="muted">无已注册项目</td></tr>';

    host.innerHTML =
      '<div class="card" style="margin-bottom:14px">' +
        '<div class="between wrap gap-sm" style="margin-bottom:6px"><strong>常驻规则体量统计</strong>' + tokenBadgeHtml + '</div>' +
        '<div class="token-meter-wrap">' +
          '<div class="between wrap gap-sm" style="font-size:12px">' +
            '<span>全局约 ' + esc(tb.globalAlwaysOnTokens || 0) + ' + 已启用插件约 ' + esc(tb.enabledPluginRulesTokens || 0) + ' = <strong>' + esc(totalAlways) + '</strong>' + (threshold > 0 ? (' / ' + esc(threshold)) : '') + ' 参考 tokens</span>' +
            '<span class="muted">项目规则约 ' + esc(tb.projectRulesTokens || 0) + ' tokens</span>' +
          '</div>' +
          '<div class="muted mono" style="font-size:11.5px">共 ' + esc(rules.length) + ' 份规则 · ' + esc(fmt(totalRuleBytes)) + ' · ' + esc(totalRuleChars) + ' 字符 · ' + esc(totalRuleLines) + ' 行</div>' +
          (threshold > 0 ? '<div class="token-meter-track"><div class="token-meter-fill ' + meterCls + '" style="width:' + meterPct + '%"></div></div>' : '') +
        '</div>' +
      '</div>' +
      '<div class="card" style="margin-bottom:14px">' +
        '<div class="between" style="margin-bottom:8px"><strong>全局与插件规则</strong><span class="muted" style="font-size:12px">编辑保存自动生成历史版本 (.bak)</span></div>' +
        '<div style="max-height:240px;overflow:auto;border:1px solid var(--border);border-radius:8px">' +
          '<table class="data-table"><thead><tr><th>规则文件</th><th>作用域</th><th>体量与参考 Token</th><th>操作</th></tr></thead><tbody>' + rulesRows + '</tbody></table>' +
        '</div>' +
      '</div>' +
      '<div class="card" style="margin-bottom:14px">' +
        '<div class="between" style="margin-bottom:8px"><strong>项目规则与扩展清单 (.agents/ / GEMINI.md)</strong></div>' +
        '<div style="max-height:220px;overflow:auto;border:1px solid var(--border);border-radius:8px">' +
          '<table class="data-table"><thead><tr><th>项目目录</th><th>目录结构</th><th>可编辑文件</th></tr></thead><tbody>' + wsRows + '</tbody></table>' +
        '</div>' +
      '</div>' +
      '<div class="card">' +
        '<div class="between" style="margin-bottom:8px"><strong>本机技能 (Skills)</strong></div>' +
        '<div style="max-height:220px;overflow:auto;border:1px solid var(--border);border-radius:8px">' +
          '<table class="data-table"><thead><tr><th>技能名称</th><th>作用域</th><th>操作</th></tr></thead><tbody>' + skillRows + '</tbody></table>' +
        '</div>' +
      '</div>';

    var edBtns = host.querySelectorAll('.vault-edit-btn');
    for (var eb = 0; eb < edBtns.length; eb++) {
      edBtns[eb].addEventListener('click', function () {
        AGY.ui.openInlineEditorDrawer(this.getAttribute('data-path'), this.getAttribute('data-title'));
      });
    }
  }

  function renderVaultSync(hostEl) {
    var el = AGY.dom.el;
    var st = AGY.state;
    var esc = AGY.ui.escapeHtml;
    var host = hostEl || AGY.dom.el('cockpitSubHost') || AGY.dom.el('mainCanvas');
    var cd = st.capData;
    var vs = st.vaultStatus || { vaultPath: (cd && (cd.vaultPath || cd.repositoryPath)) || '' };

    var legacyBanner = '';
    if (vs.legacyMigrationNeeded) {
      legacyBanner =
        '<div class="alert-banner between wrap gap-sm">' +
          '<div>检测到旧版 <span class="mono">./Repository</span> 中还有未迁移的扩展配置，可一键合并到当前配置同步库。</div>' +
          '<button class="btn btn-sm btn-primary" id="btnMigrateLegacyRepo" type="button">一键迁移到配置同步库</button>' +
        '</div>';
    }

    var headerCard =
      legacyBanner +
      '<div class="card" style="margin-bottom:14px">' +
        '<div class="between wrap gap-sm">' +
          '<div><strong>配置同步库目录</strong><div class="mono muted" style="font-size:11.5px;margin-top:3px;word-break:break-all">' + esc(vs.vaultPath || '') + '</div></div>' +
          '<div class="row gap-xs"><span class="badge badge-green">按目录独立隔离同步基线</span><button class="btn btn-sm" id="btnSelectVaultDir" type="button">切换同步库目录...</button></div>' +
        '</div>' +
      '</div>';

    var bodyHtml = '';
    if (!cd) {
      bodyHtml = '<div class="empty-state">正在比对本机与配置同步库...</div>';
    } else {
      var filtered = getFilteredCapabilities();
      var cats = ['ALL', 'Plugin', 'Skill', 'Rule', 'MCP', 'Hook'];
      var catNames = { ALL: '全部', Plugin: '插件', Skill: '技能', Rule: '规则', MCP: 'MCP', Hook: '钩子' };
      var dirs = [
        ['ALL', '全部状态'],
        ['DIFF', '仅看有差异'],
        ['REMOTE_NEW', '同步库新增/较新'],
        ['LOCAL_NEW', '本机新增/较新'],
        ['NEEDS_REVIEW', '需人工确认'],
        ['MISSING', '路径缺失']
      ];

      bodyHtml =
        '<div class="matrix-card" style="height:calc(100vh - 285px);min-height:360px">' +
          '<div class="matrix-toolbar">' +
            '<div class="row gap-sm wrap">' +
              '<div class="pill-group" id="capCatPills">' + cats.map(function (c) {
                return '<button class="filter-pill ' + (st.capCategory === c ? 'active' : '') + '" data-ccat="' + c + '" type="button">' + catNames[c] + '</button>';
              }).join('') + '</div>' +
              '<span class="pill-divider"></span>' +
              '<div class="pill-group" id="capDirPills">' + dirs.map(function (d) {
                return '<button class="filter-pill ' + (st.capDirFilter === d[0] ? 'active' : '') + '" data-cdir="' + d[0] + '" type="button">' + d[1] + '</button>';
              }).join('') + '</div>' +
            '</div>' +
            '<div class="row gap-sm wrap">' +
              '<input class="input" id="txtCapSearch" style="width:180px" placeholder="搜索扩展名称..." value="' + esc(st.capSearch) + '" />' +
              '<button class="btn btn-sm" id="btnCapSafeSelect" type="button">仅选同步库较新</button>' +
              '<button class="btn btn-sm" id="btnCapPushSel" type="button">存入配置同步库 (0)</button>' +
              '<button class="btn btn-sm btn-primary" id="btnCapImportSel" type="button">同步到本机 (0)</button>' +
              '<button class="btn btn-sm btn-danger-outline" id="btnCapDeleteMenu" type="button">移除所选...</button>' +
            '</div>' +
          '</div>' +
          '<div class="matrix-scroll">' +
            '<table class="data-table"><thead><tr><th></th><th>插件 / 技能 / 规则 / MCP / 钩子</th><th>同步状态</th><th>本机修改时间</th><th>同步库时间</th><th>操作</th></tr></thead>' +
              '<tbody id="capMatrixTbody">' + renderCapRowsHtml(filtered) + '</tbody>' +
            '</table>' +
          '</div>' +
        '</div>';
    }

    host.innerHTML = headerCard + bodyHtml;

    if (el('btnMigrateLegacyRepo')) {
      el('btnMigrateLegacyRepo').addEventListener('click', function () {
        var btn = this;
        AGY.api.withButtonBusy(btn, '迁移中...', async function () {
          try {
            var res = await AGY.api.apiFetch('/api/vault/migrate-legacy', { method: 'POST', body: '{}' });
            AGY.api.showToast('已从旧版 ./Repository 迁移 ' + res.migratedFiles + ' 个文件 ✓', 'success');
            await AGY.app.refreshAll();
          } catch (e) { AGY.api.showToast('迁移失败：' + e.message, 'error'); }
        });
      });
    }

    el('btnSelectVaultDir').addEventListener('click', function () {
      var btn = this;
      AGY.api.withButtonBusy(btn, '切换中...', async function () {
        try {
          var res = await AGY.api.apiFetch('/api/vault/select', { method: 'POST', body: '{}' });
          if (!res.cancelled) {
            AGY.api.showToast('已切换配置同步库至 ' + res.vaultPath + ' ✓', 'success');
            await AGY.app.refreshAll();
          }
        } catch (e) { AGY.api.showToast('切换配置同步库失败：' + e.message, 'error'); }
      });
    });

    if (!cd) return;

    function patchMatrixPartial() {
      var tbody = el('capMatrixTbody');
      if (!tbody) return;
      tbody.innerHTML = renderCapRowsHtml(getFilteredCapabilities());
      bindCapMatrixRowEvents();
      updateCapBatchButtons();
    }

    var catBtns = el('capCatPills').querySelectorAll('[data-ccat]');
    for (var i = 0; i < catBtns.length; i++) {
      catBtns[i].addEventListener('click', function () {
        st.capCategory = this.getAttribute('data-ccat');
        for (var k = 0; k < catBtns.length; k++) catBtns[k].classList.remove('active');
        this.classList.add('active');
        patchMatrixPartial();
      });
    }
    var dirBtns = el('capDirPills').querySelectorAll('[data-cdir]');
    for (var j = 0; j < dirBtns.length; j++) {
      dirBtns[j].addEventListener('click', function () {
        st.capDirFilter = this.getAttribute('data-cdir');
        for (var k = 0; k < dirBtns.length; k++) dirBtns[k].classList.remove('active');
        this.classList.add('active');
        patchMatrixPartial();
      });
    }
    el('txtCapSearch').addEventListener('input', function () {
      st.capSearch = this.value;
      patchMatrixPartial();
    });
    el('btnCapSafeSelect').addEventListener('click', function () {
      st.capChecked = {};
      var all = cd.items || [];
      for (var x = 0; x < all.length; x++) {
        if (all[x].direction === 'RemoteOnly' || all[x].direction === 'RemoteNewer')
          st.capChecked[all[x].key] = true;
      }
      patchMatrixPartial();
    });

    el('btnCapPushSel').addEventListener('click', function () {
      var keys = Object.keys(st.capChecked).filter(function (k) { return st.capChecked[k]; });
      executeCapAction('/api/capabilities/push', { keys: keys }, '已存入配置同步库 ✓', this);
    });
    el('btnCapImportSel').addEventListener('click', function () {
      var keys = Object.keys(st.capChecked).filter(function (k) { return st.capChecked[k]; });
      executeCapAction('/api/capabilities/import', { keys: keys, source: 'repo' }, '已同步到本机 ✓', this);
    });
    el('btnCapDeleteMenu').addEventListener('click', function () {
      var keys = Object.keys(st.capChecked).filter(function (k) { return st.capChecked[k]; });
      openCapDeleteModal(keys);
    });

    bindCapMatrixRowEvents();
    updateCapBatchButtons();
  }

  async function executeCapAction(endpoint, payload, msg, btn) {
    AGY.api.withButtonBusy(btn, '处理中...', async function () {
      try {
        await AGY.api.apiFetch(endpoint, { method: 'POST', body: JSON.stringify(payload) });
        AGY.state.capChecked = {};
        await AGY.app.refreshAll(msg);
      } catch (e) { AGY.api.showToast(e.message, 'error'); }
    });
  }

  function openCapDeleteModal(keys) {
    AGY.ui.openModal(
      '移除选中的 ' + keys.length + ' 项扩展配置',
      '<div class="alert-banner success" style="margin-bottom:10px">🛡 <strong>可撤销删除：</strong>移除的配置项会先进入 <strong>回收站</strong>，30 天内随时可一键还原。</div>' +
      '<div>请选择移除范围（仅从配置同步库移除不会影响本机正在使用的配置）：</div>',
      '<button class="btn" data-act="cancel">取消</button>' +
      '<button class="btn btn-danger-outline" data-act="del-repo">仅从配置同步库移除</button>' +
      '<button class="btn btn-danger" data-act="del-local">移出本机配置</button>',
      function (_mb, mf) {
        mf.querySelector('[data-act="cancel"]').addEventListener('click', AGY.ui.closeModal);
        mf.querySelector('[data-act="del-repo"]').addEventListener('click', function () {
          AGY.ui.closeModal(); executeCapAction('/api/capabilities/delete-repo', { keys: keys }, '已从配置同步库移入回收站 ✓', null);
        });
        mf.querySelector('[data-act="del-local"]').addEventListener('click', function () {
          AGY.ui.closeModal(); executeCapAction('/api/capabilities/delete-local', { keys: keys }, '已将本机配置移入回收站 ✓', null);
        });
      }
    );
  }

  AGY.views.renderInlineEditorHub = renderInlineEditorHub;
  AGY.views.renderVaultSync = renderVaultSync;
  AGY.views.vault = function () {
    AGY.state.cockpitSubView = 'vault';
    if (AGY.views.cockpit) AGY.views.cockpit();
  };
})();
