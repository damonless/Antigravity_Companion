(function () {
  'use strict';

  var AGY = window.AGY = window.AGY || {};
  AGY.views = AGY.views || {};

  var ENUM_OPTS = {
    autoExecutionPolicy: [
      ['CASCADE_COMMANDS_AUTO_EXECUTION_OFF', '每次执行前都询问 (OFF)'],
      ['CASCADE_COMMANDS_AUTO_EXECUTION_AUTO', '智能判断后自动执行 (AUTO)'],
      ['CASCADE_COMMANDS_AUTO_EXECUTION_EAGER', '积极自动执行 (EAGER)']
    ],
    artifactReviewMode: [
      ['ARTIFACT_REVIEW_MODE_ALWAYS', '始终人工确认 (ALWAYS)'],
      ['ARTIFACT_REVIEW_MODE_AUTO', '按需确认 (AUTO)'],
      ['ARTIFACT_REVIEW_MODE_TURBO', '自动通过 (TURBO)']
    ],
    browserJsExecutionPolicy: [
      ['BROWSER_JS_EXECUTION_POLICY_DISABLED', '禁用浏览器脚本 (DISABLED)'],
      ['BROWSER_JS_EXECUTION_POLICY_ASK', '运行前询问 (ASK)'],
      ['BROWSER_JS_EXECUTION_POLICY_TURBO', '自动允许 (TURBO)']
    ],
    nonWorkspaceFileAccessPolicy: [
      ['AGENT_SETTING_POLICY_DENY', '禁止访问项目外文件 (DENY)'],
      ['AGENT_SETTING_POLICY_ASK', '访问前询问 (ASK)'],
      ['AGENT_SETTING_POLICY_ALLOW', '允许访问项目外文件 (ALLOW)']
    ],
    themeMode: [
      ['THEME_MODE_INHERIT', '跟随系统/主窗口 (INHERIT)'],
      ['THEME_MODE_LIGHT', '浅色外观 (LIGHT)'],
      ['THEME_MODE_DARK', '深色外观 (DARK)']
    ]
  };

  function buildEnumOptionsHtml(key, currentVal) {
    var esc = AGY.ui.escapeHtml;
    var list = ENUM_OPTS[key] || [];
    var cur = String(currentVal || '');
    var found = false;
    var html = [];
    if (!cur) {
      html.push('<option value="">(未设置 / 保持默认)</option>');
      found = true;
    }
    for (var i = 0; i < list.length; i++) {
      var val = list[i][0];
      var lbl = list[i][1];
      if (val === cur) found = true;
      html.push('<option value="' + esc(val) + '"' + (val === cur ? ' selected' : '') + '>' + esc(lbl) + '</option>');
    }
    if (cur && !found) {
      html.push('<option value="' + esc(cur) + '" selected>保留原自定义项: ' + esc(cur) + '</option>');
    }
    return html.join('');
  }

  function collectSwitchboardItems(cp) {
    var items = [];
    var plugins = cp.plugins || [];
    for (var i = 0; i < plugins.length; i++) {
      var p = plugins[i];
      items.push({
        kind: 'Plugin',
        toggleKind: 'plugin',
        id: p.id,
        title: p.displayName || p.id,
        scope: '全局',
        detail: (p.version ? 'v' + p.version + ' · ' : '') + (p.description || ('规则:' + p.rulesCount + ' 技能:' + p.skillsCount + ' MCP:' + p.mcpCount + ' 钩子:' + p.hooksCount)),
        enabled: !!p.enabled,
        sourcePath: p.configPath || '',
        editPath: p.pluginJsonPath || p.configPath || '',
        duplicate: false,
        duplicateSources: []
      });
    }
    var mcps = cp.mcpServers || [];
    for (var m = 0; m < mcps.length; m++) {
      var s = mcps[m];
      var endpointDesc = s.transport === 'http'
        ? (s.url || 'http')
        : ((s.command || '') + (s.args && s.args.length ? (' ' + s.args.join(' ')) : ''));
      items.push({
        kind: 'MCP',
        toggleKind: 'mcp',
        id: s.id,
        title: s.id,
        scope: s.scope || '全局',
        detail: '[' + (s.transport || 'stdio') + '] ' + endpointDesc,
        enabled: !s.disabled && !!s.enabled,
        sourcePath: s.sourcePath || '',
        editPath: s.sourcePath || '',
        duplicate: !!s.duplicate,
        duplicateSources: s.duplicateSources || []
      });
    }
    var hooks = cp.hooks || [];
    for (var h = 0; h < hooks.length; h++) {
      var hk = hooks[h];
      var evNames = (hk.events || []).map(function (ev) { return ev.eventName; }).join(', ');
      items.push({
        kind: 'Hook',
        toggleKind: 'hook',
        id: hk.id,
        title: hk.id,
        scope: hk.scope || '全局',
        detail: evNames || 'hooks.json',
        enabled: !!hk.enabled,
        sourcePath: hk.sourcePath || '',
        editPath: hk.sourcePath || '',
        duplicate: false,
        duplicateSources: []
      });
    }
    return items;
  }

  function renderSwitchboardRowsHtml() {
    var st = AGY.state;
    var esc = AGY.ui.escapeHtml;
    var cp = st.cockpitData || {};
    var allItems = collectSwitchboardItems(cp);
    var q = (st.cockpitSearch || '').trim().toLowerCase();

    var filtered = allItems.filter(function (it) {
      if (st.cockpitSwitchFilter !== 'ALL' && it.kind !== st.cockpitSwitchFilter) return false;
      if (q) {
        return it.title.toLowerCase().indexOf(q) >= 0 ||
               it.scope.toLowerCase().indexOf(q) >= 0 ||
               it.detail.toLowerCase().indexOf(q) >= 0;
      }
      return true;
    });

    if (filtered.length === 0) {
      return '<tr><td colspan="5" class="empty-state">当前筛选条件下无插件 / MCP / 钩子条目。</td></tr>';
    }

    return filtered.map(function (it) {
      var rowCls = it.duplicate ? ' style="background:var(--amber-soft)"' : '';
      return '<tr' + rowCls + '>' +
        '<td><span class="badge badge-neutral">' + esc(AGY.ui.kindLabel(it.kind)) + '</span></td>' +
        '<td><strong>' + esc(it.title) + '</strong>' +
          (it.duplicate ? ' <span class="badge badge-amber" title="' + esc((it.duplicateSources || []).join('\n')) + '">⚠ 重复定义</span>' : '') +
          '<div class="muted mono" style="font-size:11px;max-width:440px;overflow:hidden;text-overflow:ellipsis;white-space:nowrap">' + esc(it.detail) + '</div>' +
        '</td>' +
        '<td class="mono muted" style="font-size:11.5px">' + esc(it.scope) + '</td>' +
        '<td>' +
          '<label class="toggle-switch">' +
            '<input type="checkbox" class="cp-toggle-chk" data-kind="' + esc(it.toggleKind) + '" data-id="' + esc(it.id) + '" data-src="' + esc(it.sourcePath) + '"' + (it.enabled ? ' checked' : '') + ' />' +
            '<span class="toggle-slider"></span>' +
          '</label>' +
        '</td>' +
        '<td style="white-space:nowrap"><div class="row gap-xs">' +
          (it.kind === 'MCP' ? '<button class="btn btn-xs cp-probe-mcp" data-id="' + esc(it.id) + '" data-src="' + esc(it.sourcePath) + '" type="button">检测连通性</button>' : '') +
          (it.editPath ? '<button class="btn btn-xs cp-edit-file" data-path="' + esc(it.editPath) + '" data-title="' + esc(it.title) + '" type="button">编辑源码</button>' : '') +
        '</div></td>' +
      '</tr>';
    }).join('');
  }

  function renderDoctorResultsHtml() {
    var st = AGY.state;
    var esc = AGY.ui.escapeHtml;
    var doc = st.mcpDoctorResults;
    if (!doc) {
      return '<div class="muted" style="font-size:12px">点击右侧「一键检测全部 MCP」即可实测所有已启用 MCP 服务的连通性与响应延迟。</div>';
    }
    var list = doc.results || [];
    if (list.length === 0) {
      return '<div class="muted">本机未检测到已启用的 MCP 服务。</div>';
    }
    return '<div style="margin-bottom:8px" class="row gap-sm">' +
      '<span class="badge badge-green">正常 ' + esc(doc.healthyCount || 0) + '</span>' +
      '<span class="badge badge-neutral">共检测 ' + esc(doc.checkedCount || 0) + '</span>' +
    '</div>' +
    '<div style="max-height:280px;overflow:auto;border:1px solid var(--border);border-radius:8px">' +
      '<table class="data-table"><thead><tr><th>MCP 名称</th><th>连接方式</th><th>状态</th><th>延迟</th><th>详情</th></tr></thead><tbody>' +
      list.map(function (r) {
        var stBadge = r.disabled
          ? '<span class="badge badge-neutral">已禁用</span>'
          : (r.healthy ? '<span class="badge badge-green">正常</span>' : '<span class="badge badge-red">异常</span>');
        var detail = r.serverName ? (r.serverName + (r.serverVersion ? ' v' + r.serverVersion : '')) : (r.status || '');
        var errOrStd = [r.error || '', r.stderr || ''].filter(Boolean).join(' | ');
        return '<tr>' +
          '<td><strong>' + esc(r.id) + '</strong><div class="muted mono" style="font-size:11px">' + esc(r.scope) + '</div></td>' +
          '<td class="mono">' + esc(r.transport) + '</td>' +
          '<td>' + stBadge + '</td>' +
          '<td class="mono">' + esc(r.latencyMs) + ' ms</td>' +
          '<td><div>' + esc(detail) + '</div>' +
            (errOrStd ? '<details style="margin-top:3px"><summary class="muted" style="cursor:pointer;font-size:11px">查看错误详情</summary><pre class="mono" style="margin:4px 0 0 0;font-size:11px;white-space:pre-wrap;max-height:110px;overflow:auto">' + esc(errOrStd) + '</pre></details>' : '') +
          '</td>' +
        '</tr>';
      }).join('') +
      '</tbody></table></div>';
  }

  function applyCockpitIdeWarning(res) {
    var st = AGY.state;
    var banner = AGY.dom.el('cpIdeWarningBanner');
    var msgEl = AGY.dom.el('cpIdeWarningText');
    if (res && (res.ideRunning || res.ideRunningWarning)) {
      var msg = res.ideRunningWarning || 'Antigravity 主窗口正在运行，部分配置需重启主窗口或新开对话后生效。';
      st.cockpitIdeWarning = msg;
      if (banner && msgEl) {
        msgEl.textContent = msg;
        banner.classList.remove('hidden');
      }
      AGY.api.showToast(msg, 'warn');
    } else if (res && res.ideRunning === false) {
      st.cockpitIdeWarning = '';
      if (banner) banner.classList.add('hidden');
    }
  }

  function bindSwitchboardRowEvents() {
    var tbody = AGY.dom.el('cpSwitchTbody');
    if (!tbody) return;

    var chks = tbody.querySelectorAll('.cp-toggle-chk');
    for (var i = 0; i < chks.length; i++) {
      chks[i].addEventListener('change', async function () {
        var chk = this;
        var kind = chk.getAttribute('data-kind');
        var id = chk.getAttribute('data-id');
        var src = chk.getAttribute('data-src');
        var desired = !!chk.checked;
        chk.disabled = true;
        try {
          var res = await AGY.api.apiFetch('/api/cockpit/toggle', {
            method: 'POST',
            body: JSON.stringify({ kind: kind, id: id, enabled: desired, sourcePath: src })
          });
          AGY.api.showToast('已' + (desired ? '启用' : '停用') + ' [' + AGY.ui.kindLabel(kind) + '] ' + id + ' ✓', 'success');
          applyCockpitIdeWarning(res);
          if (AGY.app && AGY.app.markNonActiveDirty) AGY.app.markNonActiveDirty('cockpit');
          if (AGY.app && AGY.app.refreshCoreAndActiveView) await AGY.app.refreshCoreAndActiveView();
        } catch (e) {
          chk.checked = !desired;
          AGY.api.showToast('切换失败：' + e.message, 'error');
        } finally {
          chk.disabled = false;
        }
      });
    }

    var probeBtns = tbody.querySelectorAll('.cp-probe-mcp');
    for (var p = 0; p < probeBtns.length; p++) {
      probeBtns[p].addEventListener('click', function () {
        runMcpDoctor(this.getAttribute('data-id'), this.getAttribute('data-src'), this);
      });
    }

    var editBtns = tbody.querySelectorAll('.cp-edit-file');
    for (var e = 0; e < editBtns.length; e++) {
      editBtns[e].addEventListener('click', function () {
        AGY.ui.openInlineEditorDrawer(this.getAttribute('data-path'), '编辑配置 — ' + this.getAttribute('data-title'));
      });
    }
  }

  async function runMcpDoctor(serverId, sourcePath, btn) {
    var host = AGY.dom.el('cpDoctorHost');
    if (host) host.innerHTML = '<div class="muted">正在检测 MCP 服务连通性与响应延迟...</div>';
    AGY.api.withButtonBusy(btn, '检测中...', async function () {
      try {
        var res = await AGY.api.apiFetch('/api/mcp/doctor', {
          method: 'POST',
          body: JSON.stringify({
            id: serverId || '',
            sourcePath: sourcePath || '',
            includeDisabled: false,
            timeoutMs: 3500
          })
        });
        AGY.state.mcpDoctorResults = res;
        if (host) host.innerHTML = renderDoctorResultsHtml();
        AGY.api.showToast('MCP 检测完成：' + (res.healthyCount || 0) + '/' + (res.checkedCount || 0) + ' 正常', 'success');
      } catch (e) {
        if (host) host.innerHTML = '<div class="alert-banner danger">' + AGY.ui.escapeHtml(e.message) + '</div>';
        AGY.api.showToast('MCP 检测失败：' + e.message, 'error');
      }
    });
  }

  function renderCockpitSettingsBody(host) {
    var el = AGY.dom.el;
    var esc = AGY.ui.escapeHtml;
    var st = AGY.state;
    var cp = st.cockpitData;
    if (!cp) {
      host.innerHTML = '<div class="empty-state">正在扫描本机全局配置与插件/MCP/钩子开关...</div>';
      return;
    }

    var us = cp.userSettings || {};
    var conflicts = cp.mcpConflicts || [];
    var conflictBanner = '';
    if (conflicts.length > 0) {
      conflictBanner =
        '<div class="alert-banner">' +
          '<strong>⚠ 发现 ' + conflicts.length + ' 项重复 MCP 服务定义（在多个配置文件中同时出现）：</strong>' +
          conflicts.map(function (c) {
            return '<div class="mono" style="font-size:11.5px;margin-top:3px">• ' + esc(c.id) + ' (' + c.count + ' 处): ' + esc((c.sources || []).join(' ; ')) + '</div>';
          }).join('') +
        '</div>';
    }

    var cats = [
      ['ALL', '全部'],
      ['Plugin', '插件'],
      ['MCP', 'MCP'],
      ['Hook', '钩子']
    ];

    var ideWarnText = st.cockpitIdeWarning || '';
    var ideWarnBanner =
      '<div id="cpIdeWarningBanner" class="alert-banner between wrap gap-sm' + (ideWarnText ? '' : ' hidden') + '">' +
        '<div><strong>⚠ 提示：</strong><span id="cpIdeWarningText">' + esc(ideWarnText) + '</span></div>' +
        '<button class="btn btn-xs" id="btnDismissCpIdeWarn" type="button">知道了</button>' +
      '</div>';

    host.innerHTML =
      ideWarnBanner +
      conflictBanner +
      '<div class="card" style="margin-bottom:16px">' +
        '<div class="between wrap gap-sm" style="margin-bottom:12px">' +
          '<div><strong>Antigravity 主窗口运行配置</strong><div class="muted mono" style="font-size:11px">' + esc(us.configPath || '') + '</div></div>' +
          '<div class="row gap-sm">' +
            (us.configPath ? '<button class="btn btn-sm" id="btnCpEditConfigJson" type="button">源码编辑 config.json</button>' : '') +
            '<button class="btn btn-sm btn-primary" id="btnCpSaveUserSettings" type="button">保存全局配置</button>' +
          '</div>' +
        '</div>' +
        '<div class="form-grid" id="cpSettingsForm">' +
          '<div><label class="field-label">终端命令执行策略</label><select class="select" id="selUsAutoExec">' + buildEnumOptionsHtml('autoExecutionPolicy', us.autoExecutionPolicy) + '</select></div>' +
          '<div><label class="field-label">计划与产物审阅方式</label><select class="select" id="selUsArtifactReview">' + buildEnumOptionsHtml('artifactReviewMode', us.artifactReviewMode) + '</select></div>' +
          '<div><label class="field-label">浏览器脚本执行策略</label><select class="select" id="selUsBrowserJs">' + buildEnumOptionsHtml('browserJsExecutionPolicy', us.browserJsExecutionPolicy) + '</select></div>' +
          '<div><label class="field-label">项目外文件访问权限</label><select class="select" id="selUsNonWs">' + buildEnumOptionsHtml('nonWorkspaceFileAccessPolicy', us.nonWorkspaceFileAccessPolicy) + '</select></div>' +
          '<div><label class="field-label">Antigravity 主窗口主题</label><select class="select" id="selUsTheme">' + buildEnumOptionsHtml('themeMode', us.themeMode) + '</select></div>' +
          '<div><label class="field-label">对话栏宽度</label><input class="input" id="txtUsConvWidth" value="' + esc(us.conversationWidth || '') + '" placeholder="留空表示保持原配置" /></div>' +
        '</div>' +
        '<div class="cockpit-grid" style="margin-top:12px;margin-bottom:0" id="cpSettingsSwitches">' +
          '<label class="switch-row" style="cursor:pointer"><span>启用终端沙盒隔离</span><input type="checkbox" id="chkUsSandbox"' + (us.enableTerminalSandbox ? ' checked' : '') + ' /></label>' +
          '<label class="switch-row" style="cursor:pointer"><span>重要事件提示音</span><input type="checkbox" id="chkUsSounds"' + (us.enableSoundsForSpecialEvents ? ' checked' : '') + ' /></label>' +
          '<label class="switch-row" style="cursor:pointer"><span>启用 AI Credits 配额</span><input type="checkbox" id="chkUsCredits"' + (us.useAiCredits ? ' checked' : '') + ' /></label>' +
        '</div>' +
      '</div>' +

      '<div class="card" style="margin-bottom:16px">' +
        '<div class="between wrap gap-sm" style="margin-bottom:10px">' +
          '<strong>MCP 服务连通性检测</strong>' +
          '<button class="btn btn-sm btn-primary" id="btnCpRunMcpDoctor" type="button">一键检测全部 MCP</button>' +
        '</div>' +
        '<div id="cpDoctorHost">' + renderDoctorResultsHtml() + '</div>' +
      '</div>' +

      '<div class="card">' +
        '<div class="between wrap gap-sm" style="margin-bottom:10px">' +
          '<div class="row gap-sm wrap">' +
            '<strong>插件 / MCP / 钩子开关</strong>' +
            '<div class="pill-group" id="cpCatPills">' +
              cats.map(function (c) {
                return '<button class="filter-pill ' + (st.cockpitSwitchFilter === c[0] ? 'active' : '') + '" data-cpcat="' + c[0] + '" type="button">' + c[1] + '</button>';
              }).join('') +
            '</div>' +
          '</div>' +
          '<input class="input" id="txtCpSwitchSearch" style="width:220px" placeholder="搜索名称、作用域或命令..." value="' + esc(st.cockpitSearch) + '" />' +
        '</div>' +
        '<div style="max-height:420px;overflow:auto;border:1px solid var(--border);border-radius:8px">' +
          '<table class="data-table"><thead><tr><th>类别</th><th>名称与摘要</th><th>作用域</th><th>启用</th><th>操作</th></tr></thead>' +
            '<tbody id="cpSwitchTbody">' + renderSwitchboardRowsHtml() + '</tbody>' +
          '</table>' +
        '</div>' +
      '</div>';

    var formNodes = host.querySelectorAll('#cpSettingsForm select, #cpSettingsForm input, #cpSettingsSwitches input');
    for (var fi = 0; fi < formNodes.length; fi++) {
      formNodes[fi].addEventListener('change', function () { st.formDirty.cockpit = true; });
      formNodes[fi].addEventListener('input', function () { st.formDirty.cockpit = true; });
    }

    if (el('btnDismissCpIdeWarn')) {
      el('btnDismissCpIdeWarn').addEventListener('click', function () {
        st.cockpitIdeWarning = '';
        var b = el('cpIdeWarningBanner');
        if (b) b.classList.add('hidden');
      });
    }

    if (el('btnCpEditConfigJson')) {
      el('btnCpEditConfigJson').addEventListener('click', function () {
        AGY.ui.openInlineEditorDrawer(us.configPath, '编辑 config.json');
      });
    }

    el('btnCpSaveUserSettings').addEventListener('click', function () {
      var btn = this;
      AGY.api.withButtonBusy(btn, '保存中...', async function () {
        var updates = {
          enableTerminalSandbox: !!el('chkUsSandbox').checked,
          enableSoundsForSpecialEvents: !!el('chkUsSounds').checked,
          useAiCredits: !!el('chkUsCredits').checked
        };
        var vAuto = el('selUsAutoExec').value; if (vAuto) updates.autoExecutionPolicy = vAuto;
        var vArt = el('selUsArtifactReview').value; if (vArt) updates.artifactReviewMode = vArt;
        var vJs = el('selUsBrowserJs').value; if (vJs) updates.browserJsExecutionPolicy = vJs;
        var vNon = el('selUsNonWs').value; if (vNon) updates.nonWorkspaceFileAccessPolicy = vNon;
        var vTheme = el('selUsTheme').value; if (vTheme) updates.themeMode = vTheme;
        var vWidth = el('txtUsConvWidth').value.trim(); if (vWidth) updates.conversationWidth = vWidth;

        try {
          var res = await AGY.api.apiFetch('/api/cockpit/settings', {
            method: 'POST',
            body: JSON.stringify({ userSettings: updates })
          });
          st.cockpitData.userSettings = res.userSettings;
          st.formDirty.cockpit = false;
          AGY.api.showToast('全局配置已保存（并自动留存 .bak 历史版本）✓', 'success');
          applyCockpitIdeWarning(res);
          if (AGY.app && AGY.app.markNonActiveDirty) AGY.app.markNonActiveDirty('cockpit');
        } catch (e) {
          AGY.api.showToast('保存配置失败：' + e.message, 'error');
        }
      });
    });

    el('btnCpRunMcpDoctor').addEventListener('click', function () { runMcpDoctor('', '', this); });

    var catBtns = el('cpCatPills').querySelectorAll('[data-cpcat]');
    for (var i = 0; i < catBtns.length; i++) {
      catBtns[i].addEventListener('click', function () {
        st.cockpitSwitchFilter = this.getAttribute('data-cpcat');
        for (var j = 0; j < catBtns.length; j++) catBtns[j].classList.remove('active');
        this.classList.add('active');
        el('cpSwitchTbody').innerHTML = renderSwitchboardRowsHtml();
        bindSwitchboardRowEvents();
      });
    }

    el('txtCpSwitchSearch').addEventListener('input', function () {
      st.cockpitSearch = this.value;
      el('cpSwitchTbody').innerHTML = renderSwitchboardRowsHtml();
      bindSwitchboardRowEvents();
    });

    bindSwitchboardRowEvents();
  }

  function renderCockpit() {
    var el = AGY.dom.el;
    var st = AGY.state;
    var sub = st.cockpitSubView || 'settings';
    var cs = (st.overview && st.overview.counts && st.overview.counts.capabilitySummary) || {};
    var capDiffCount = (cs.remoteOnly || 0) + (cs.remoteNewer || 0) + (cs.localNewer || 0) + (cs.bothModified || 0) + (cs.conflict || 0);

    var root = el('mainCanvas');
    var useFlexShell = (sub === 'vault');
    root.innerHTML =
      (useFlexShell ? '<div class="subview-shell">' : '') +
      '<div class="subtab-bar">' +
        '<div class="pill-group" id="cockpitSubTabs">' +
          '<button class="filter-pill ' + (sub === 'settings' ? 'active' : '') + '" data-cpsub="settings" type="button">全局配置与开关</button>' +
          '<button class="filter-pill ' + (sub === 'editor' ? 'active' : '') + '" data-cpsub="editor" type="button">规则与技能编辑</button>' +
          '<button class="filter-pill ' + (sub === 'vault' ? 'active' : '') + '" data-cpsub="vault" type="button">配置同步库' + (capDiffCount > 0 ? (' (' + capDiffCount + ' 项差异)') : '') + '</button>' +
        '</div>' +
      '</div>' +
      '<div id="cockpitSubHost"' + (useFlexShell ? ' class="subview-body"' : '') + '></div>' +
      (useFlexShell ? '</div>' : '');

    function applySubTabSwitch(next) {
      st.formDirty.cockpit = false;
      st.cockpitSubView = next;
      if ((next === 'vault' && (st.dirty.vault || !st.capData)) ||
          (next !== 'vault' && (st.dirty.cockpit || !st.cockpitData))) {
        renderCockpit();
        AGY.app.fetchViewDataOnly('cockpit').then(function () {
          if (AGY.state.view === 'cockpit' && AGY.state.cockpitSubView === next) renderCockpit();
        }).catch(function () {});
      } else {
        renderCockpit();
      }
    }

    var subBtns = root.querySelectorAll('[data-cpsub]');
    for (var i = 0; i < subBtns.length; i++) {
      subBtns[i].addEventListener('click', function () {
        var next = this.getAttribute('data-cpsub');
        if (next === st.cockpitSubView) return;
        if (st.cockpitSubView === 'settings' && st.formDirty && st.formDirty.cockpit) {
          AGY.ui.openModal(
            '放弃尚未保存的配置修改？',
            '<div>当前「全局配置与开关」还有未保存的修改，切换标签页将放弃这些改动。</div>',
            '<button class="btn" data-act="cancel">继续编辑</button><button class="btn btn-danger" data-act="discard">放弃并切换</button>',
            function (_mb, mf) {
              mf.querySelector('[data-act="cancel"]').addEventListener('click', AGY.ui.closeModal);
              mf.querySelector('[data-act="discard"]').addEventListener('click', function () {
                AGY.ui.closeModal();
                applySubTabSwitch(next);
              });
            }
          );
          return;
        }
        applySubTabSwitch(next);
      });
    }

    var subHost = el('cockpitSubHost');
    if (sub === 'editor') {
      if (AGY.views.renderInlineEditorHub) AGY.views.renderInlineEditorHub(subHost);
    } else if (sub === 'vault') {
      if (AGY.views.renderVaultSync) AGY.views.renderVaultSync(subHost);
    } else {
      renderCockpitSettingsBody(subHost);
    }
  }

  AGY.views.cockpit = renderCockpit;
})();
