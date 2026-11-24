(function () {
  'use strict';

  var AGY = window.AGY = window.AGY || {};
  AGY.views = AGY.views || {};

  function buildHotkeyBadgeHtml(cfg) {
    var esc = AGY.ui.escapeHtml;
    if (cfg && cfg.hotkeyRegistered === true) {
      return '<span class="badge badge-green">全局热键已注册</span>';
    }
    if (cfg && cfg.hotkeyError) {
      return '<span class="badge badge-red">' + esc(cfg.hotkeyError) + '</span>';
    }
    return '<span class="badge badge-neutral">未启用</span>';
  }

  function buildCliStatusHtml(cliStat) {
    var esc = AGY.ui.escapeHtml;
    if (!cliStat) {
      return '<span class="badge badge-neutral">未检测</span>';
    }
    if (cliStat.available) {
      return '<span class="badge badge-green">CLI 已就绪</span> <span class="muted mono" style="font-size:11.5px">' + esc(cliStat.resolvedPath || '') + '</span>';
    }
    return '<span class="badge badge-amber">未检测到 CLI</span> <span class="muted" style="font-size:11.5px">' + esc(cliStat.versionOrHint || '') + '</span>';
  }

  function renderSettings() {
    var el = AGY.dom.el;
    var st = AGY.state;
    var esc = AGY.ui.escapeHtml;
    var cfg = st.companionSettings || {};
    var snap = st.snapshotStatus || {};
    var vs = st.vaultStatus || { vaultPath: cfg.vaultPath || '' };

    var snapLastInfo = snap.lastRunUtc
      ? ('上次执行：' + esc(snap.lastRunUtc) + ' · 大小：' + esc(snap.lastSizeMegabytes || 0) + ' MB' +
         (snap.lastFilePath ? (' · 文件：' + esc(snap.lastFilePath)) : '') +
         (snap.lastError ? (' · <span style="color:var(--red-text)">错误：' + esc(snap.lastError) + '</span>') : ''))
      : '尚未执行过定时静默快照';

    var legacyHtml = '';
    if (vs.legacyMigrationNeeded) {
      legacyHtml =
        '<div class="alert-banner between wrap gap-sm" style="margin-top:10px;margin-bottom:0">' +
          '<div>检测到旧版 <span class="mono">./Repository</span> 存在未迁移文件（<span class="mono">' + esc(vs.legacyRepoPath || '') + '</span>）。</div>' +
          '<button class="btn btn-sm btn-primary" id="btnSetMigrateLegacy">一键迁移 → Portable Vault</button>' +
        '</div>';
    } else if (vs.legacyRepoExists) {
      legacyHtml = '<div class="muted" style="font-size:11.5px;margin-top:6px">迁移状态：检测到旧版 ./Repository（内容已同步或无需迁移）。</div>';
    } else {
      legacyHtml = '<div class="muted" style="font-size:11.5px;margin-top:6px">迁移状态：独立 Portable Vault 目录运行中。</div>';
    }

    var startupAlertHtml = st.settingsStartupError
      ? '<div class="alert-banner danger" id="setStartupErrorBanner">' + esc(st.settingsStartupError) + '</div>'
      : '';

    var root = el('mainCanvas');
    root.innerHTML =
      startupAlertHtml +
      '<div class="between wrap gap-sm" style="margin-bottom:14px">' +
        '<div class="muted" style="font-size:12px">配置文件：<span class="mono">' + esc(cfg.settingsPath || '%LOCALAPPDATA%\\AntigravityCompanion\\companion_settings.json') + '</span></div>' +
        '<div class="row gap-sm">' +
          '<button class="btn btn-sm" id="btnSetRunSnapNow" type="button">⚡ 立即执行极速快照</button>' +
          '<button class="btn btn-sm btn-primary" id="btnSaveCompanionSettings" type="button">保存系统设置</button>' +
        '</div>' +
      '</div>' +

      '<div class="cockpit-grid">' +
        '<div class="card">' +
          '<div class="between wrap gap-sm" style="margin-bottom:8px"><strong>Portable Vault 能力仓库</strong><button class="btn btn-xs" id="btnSetPickVault" type="button">选择目录并切换...</button></div>' +
          '<div class="mono muted" style="font-size:11.5px;word-break:break-all;padding:8px 10px;background:var(--bg-subtle);border-radius:7px;border:1px solid var(--border)">' + esc(vs.vaultPath || cfg.vaultPath || '') + '</div>' +
          legacyHtml +
        '</div>' +

        '<div class="card">' +
          '<div class="between wrap gap-sm" style="margin-bottom:8px"><strong>Zero-Key agy CLI 路径与可用性</strong><button class="btn btn-xs" id="btnSetCheckCli" type="button">检测可用性</button></div>' +
          '<div class="row gap-sm" style="margin-bottom:8px">' +
            '<input class="input" id="txtSetAgyCli" value="' + esc(cfg.customAgyCliPath || '') + '" placeholder="留空自动探测 PATH（例如 C:\\Users\\...\\agy.cmd）" />' +
            '<button class="btn btn-sm" id="btnSetPickCli" type="button">浏览...</button>' +
          '</div>' +
          '<div id="setCliStatusHost">' + buildCliStatusHtml(st.settingsCliStatus) + '</div>' +
        '</div>' +
      '</div>' +

      '<div class="card" style="margin-bottom:16px">' +
        '<div class="between wrap gap-sm" style="margin-bottom:10px">' +
          '<strong>托盘常驻、全局快捷键与开机自启（默认全部 Opt-In 关闭）</strong>' +
          '<div id="setHotkeyBadgeHost">' + buildHotkeyBadgeHtml(cfg) + '</div>' +
        '</div>' +
        '<div class="form-grid" style="margin-bottom:12px">' +
          '<div>' +
            '<label class="field-label">全局唤醒快捷键 (globalHotkey，留空则不注册)</label>' +
            '<input class="input" id="txtSetHotkey" value="' + esc(cfg.globalHotkey || '') + '" placeholder="留空不启用；示例: Ctrl+Alt+A" />' +
          '</div>' +
          '<div>' +
            '<label class="field-label">规则参考 Token 提醒阈值 (ruleTokenReferenceThreshold，0 = 不提醒)</label>' +
            '<input class="input" id="numSetTokenThreshold" type="number" min="0" max="100000" value="' + esc(cfg.ruleTokenReferenceThreshold !== undefined ? cfg.ruleTokenReferenceThreshold : 0) + '" />' +
          '</div>' +
        '</div>' +
        '<div class="cockpit-grid" style="margin-bottom:0">' +
          '<label class="switch-row" style="cursor:pointer"><span>关闭窗口后最小化常驻系统托盘 (minimizeToTray)</span><input type="checkbox" id="chkSetTray"' + (cfg.minimizeToTray ? ' checked' : '') + ' /></label>' +
          '<label class="switch-row" style="cursor:pointer"><span>随 Windows 登录静默开机自启 (--tray-only)</span><input type="checkbox" id="chkSetStartup"' + (cfg.startWithWindows ? ' checked' : '') + ' /></label>' +
        '</div>' +
      '</div>' +

      '<div class="card">' +
        '<div class="between wrap gap-sm" style="margin-bottom:10px">' +
          '<div><strong>定时静默只读快照</strong><div class="muted" style="font-size:12px;margin-top:2px">' + snapLastInfo + '（目录内现有 ' + esc(snap.existingCount || 0) + ' 份）</div></div>' +
        '</div>' +
        '<div class="form-grid" style="margin-bottom:12px">' +
          '<div>' +
            '<label class="field-label">快照目标目录 (targetDirectory)</label>' +
            '<div class="row gap-sm"><input class="input" id="txtSetTargetDir" value="' + esc(cfg.targetDirectory || snap.targetDirectory || '') + '" /><button class="btn btn-sm" id="btnSetPickTargetDir" type="button">浏览...</button></div>' +
          '</div>' +
          '<div>' +
            '<label class="field-label">默认快照等级 (tier) &amp; 保留份数 (retainCount)</label>' +
            '<div class="row gap-sm">' +
              '<select class="select" id="selSetTier" style="flex:1">' +
                '<option value="fast"' + (cfg.tier !== 'full' ? ' selected' : '') + '>FastDaily (极速只读)</option>' +
                '<option value="full"' + (cfg.tier === 'full' ? ' selected' : '') + '>FullForensic (全量法证)</option>' +
              '</select>' +
              '<input class="input" id="numSetRetain" type="number" min="1" max="100" value="' + esc(cfg.retainCount || 5) + '" style="width:90px" title="保留份数" />' +
            '</div>' +
          '</div>' +
          '<div>' +
            '<label class="field-label">定时周期（小时, intervalHours）</label>' +
            '<input class="input" id="numSetInterval" type="number" min="1" max="720" value="' + esc(cfg.intervalHours || 12) + '" />' +
          '</div>' +
          '<div style="display:flex;align-items:flex-end">' +
            '<label class="switch-row" style="cursor:pointer;width:100%"><span>启用后台定时静默只读快照（全量档在 IDE 运行时安全跳过）</span><input type="checkbox" id="chkSetSnapEnabled"' + (cfg.enabled ? ' checked' : '') + ' /></label>' +
          '</div>' +
        '</div>' +
      '</div>';

    el('btnSetPickVault').addEventListener('click', async function () {
      try {
        var res = await AGY.api.apiFetch('/api/vault/select', { method: 'POST', body: '{}' });
        if (!res.cancelled) {
          st.vaultStatus = res;
          if (AGY.app && AGY.app.markNonActiveDirty) AGY.app.markNonActiveDirty('settings');
          await AGY.app.refreshCoreAndActiveView();
          renderSettings();
          AGY.api.showToast('已切换 Vault 至 ' + res.vaultPath + ' ✓', 'success');
        }
      } catch (e) { AGY.api.showToast('切换 Vault 失败：' + e.message, 'error'); }
    });

    if (el('btnSetMigrateLegacy')) {
      el('btnSetMigrateLegacy').addEventListener('click', async function () {
        try {
          var res = await AGY.api.apiFetch('/api/vault/migrate-legacy', { method: 'POST', body: '{}' });
          st.vaultStatus = res;
          if (AGY.app && AGY.app.markNonActiveDirty) AGY.app.markNonActiveDirty('settings');
          if (AGY.app && AGY.app.refreshCoreAndActiveView) await AGY.app.refreshCoreAndActiveView();
          renderSettings();
          AGY.api.showToast('已从 ./Repository 迁移 ' + res.migratedFiles + ' 个文件 ✓', 'success');
        } catch (e) { AGY.api.showToast('迁移失败：' + e.message, 'error'); }
      });
    }

    el('btnSetPickCli').addEventListener('click', async function () {
      try {
        var pick = await AGY.api.apiFetch('/api/native/pick-open-file', { method: 'POST', body: JSON.stringify({ title: '选择 agy.exe / agy.cmd' }) });
        if (!pick.cancelled && pick.path) el('txtSetAgyCli').value = pick.path;
      } catch (e) { AGY.api.showToast('选择文件失败：' + e.message, 'error'); }
    });

    el('btnSetCheckCli').addEventListener('click', async function () {
      var customPath = el('txtSetAgyCli').value.trim();
      var host = el('setCliStatusHost');
      if (host) host.innerHTML = '<span class="muted">正在检测 agy CLI...</span>';
      try {
        var res = await AGY.api.apiFetch('/api/rename/cli-status?customCliPath=' + encodeURIComponent(customPath));
        st.settingsCliStatus = res;
        if (host) host.innerHTML = buildCliStatusHtml(res);
        AGY.api.showToast(res.available ? 'agy CLI 可用 ✓' : '未检测到可用的 agy CLI', res.available ? 'success' : 'warn');
      } catch (e) {
        if (host) host.innerHTML = '<span class="badge badge-red">' + esc(e.message) + '</span>';
        AGY.api.showToast('检测失败：' + e.message, 'error');
      }
    });

    el('btnSetPickTargetDir').addEventListener('click', async function () {
      try {
        var pick = await AGY.api.apiFetch('/api/native/pick-folder', {
          method: 'POST',
          body: JSON.stringify({ title: '选择快照存放目录', initialPath: el('txtSetTargetDir').value })
        });
        if (!pick.cancelled && pick.path) el('txtSetTargetDir').value = pick.path;
      } catch (e) { AGY.api.showToast('选择目录失败：' + e.message, 'error'); }
    });

    el('btnSetRunSnapNow').addEventListener('click', async function () {
      var btn = this;
      btn.disabled = true;
      btn.textContent = '快照创建中...';
      try {
        var res = await AGY.api.apiFetch('/api/snapshots/run-now', { method: 'POST', body: '{}' });
        st.snapshotStatus = await AGY.api.apiFetch('/api/snapshots/status');
        st.companionSettings = await AGY.api.apiFetch('/api/settings/companion');
        renderSettings();
        AGY.api.showToast('极速快照完成（' + res.sizeMegabytes + ' MB）✓', 'success');
      } catch (e) {
        btn.disabled = false;
        btn.textContent = '⚡ 立即执行极速快照';
        AGY.api.showToast('快照失败：' + e.message, 'error');
      }
    });

    el('btnSaveCompanionSettings').addEventListener('click', async function () {
      var prevHotkey = (st.companionSettings && st.companionSettings.globalHotkey) || '';
      var thresholdRaw = parseInt(el('numSetTokenThreshold').value, 10);
      var thresholdVal = (!isNaN(thresholdRaw) && thresholdRaw >= 0) ? thresholdRaw : 0;
      var payload = {
        enabled: !!el('chkSetSnapEnabled').checked,
        intervalHours: parseInt(el('numSetInterval').value, 10) || 12,
        targetDirectory: el('txtSetTargetDir').value.trim(),
        retainCount: parseInt(el('numSetRetain').value, 10) || 5,
        tier: el('selSetTier').value,
        minimizeToTray: !!el('chkSetTray').checked,
        globalHotkey: el('txtSetHotkey').value.trim(),
        customAgyCliPath: el('txtSetAgyCli').value.trim(),
        startWithWindows: !!el('chkSetStartup').checked,
        ruleTokenReferenceThreshold: thresholdVal
      };

      try {
        var saved = await AGY.api.apiFetch('/api/settings/companion', {
          method: 'POST',
          body: JSON.stringify(payload)
        });
        st.companionSettings = saved;
        st.snapshotStatus = await AGY.api.apiFetch('/api/snapshots/status');
        if (AGY.app && AGY.app.markNonActiveDirty) AGY.app.markNonActiveDirty('settings');

        if (saved.startupRollback || saved.startupError) {
          st.settingsStartupError = saved.startupError || '无法写入 Windows 开机自启注册表，已自动回滚为未勾选。';
          renderSettings();
          if (el('chkSetStartup')) el('chkSetStartup').checked = false;
          AGY.api.showToast(st.settingsStartupError, 'error');
        } else {
          st.settingsStartupError = '';
          renderSettings();
          AGY.api.showToast('系统设置已保存 ✓', 'success');
        }

        if (payload.globalHotkey !== prevHotkey) {
          var pollHotkeyStatus = async function (retryOnPending) {
            try {
              var freshCfg = await AGY.api.apiFetch('/api/settings/companion');
              st.companionSettings = freshCfg;
              var badgeHost = el('setHotkeyBadgeHost');
              if (badgeHost && AGY.state.view === 'settings') {
                badgeHost.innerHTML = buildHotkeyBadgeHtml(freshCfg);
              }
              if (retryOnPending && payload.globalHotkey && !freshCfg.hotkeyRegistered && !freshCfg.hotkeyError) {
                setTimeout(function () { pollHotkeyStatus(false); }, 550);
              }
            } catch (_e) {}
          };
          setTimeout(function () { pollHotkeyStatus(true); }, 450);
        }
      } catch (e) {
        AGY.api.showToast('保存设置失败：' + e.message, 'error');
      }
    });
  }

  AGY.views.settings = renderSettings;
})();
