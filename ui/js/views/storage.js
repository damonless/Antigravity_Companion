(function () {
  'use strict';

  var AGY = window.AGY = window.AGY || {};
  AGY.views = AGY.views || {};

  function numOrDefault(val, def) {
    var n = parseFloat(val);
    return (!isNaN(n) && isFinite(n) && n >= 0) ? n : def;
  }

  async function runStorageAnalyze() {
    var st = AGY.state;
    try {
      var res = await AGY.api.apiFetch('/api/storage/analyze', {
        method: 'POST',
        body: JSON.stringify({
          topN: 30,
          minAgeDays: numOrDefault(st.storageMinAgeDays, 7),
          minSizeMb: numOrDefault(st.storageMinSizeMb, 5)
        })
      });
      st.storageData = res;
      // Default preselect eligible items if selection empty
      if (Object.keys(st.storageCheckedCids).length === 0 && res.topOffenders) {
        for (var i = 0; i < res.topOffenders.length; i++) {
          if (res.topOffenders[i].eligibleForSlim) {
            st.storageCheckedCids[res.topOffenders[i].cid] = true;
          }
        }
      }
      if (st.view === 'storage') renderStorage();
    } catch (e) {
      AGY.api.showToast('存储分析失败：' + e.message, 'error');
    }
  }

  function renderOffenderRowsHtml(offenders) {
    var st = AGY.state;
    var esc = AGY.ui.escapeHtml;
    var fmt = AGY.ui.formatBytes;
    if (!offenders || offenders.length === 0) {
      return '<tr><td colspan="7" class="empty-state">未发现大体积会话数据库。</td></tr>';
    }
    return offenders.map(function (o) {
      var checked = !!st.storageCheckedCids[o.cid];
      var execBytes = o.executorMetadataBytes || 0;
      var stepsBytes = o.stepsBytes || 0;
      var denom = execBytes + stepsBytes;
      var execPct = denom > 0 ? Math.round((execBytes / denom) * 100) : 0;
      var stepsPct = denom > 0 ? (100 - execPct) : 0;

      var ratioHtml = o.hasTableBreakdown
        ? ('<div class="row gap-xs" title="executor_metadata: ' + fmt(execBytes) + ' / steps: ' + fmt(stepsBytes) + '">' +
             '<div class="ratio-track"><div class="ratio-exec" style="width:' + execPct + '%"></div><div class="ratio-steps" style="width:' + stepsPct + '%"></div></div>' +
             '<span class="mono" style="font-size:11px">' + execPct + '%</span>' +
           '</div>')
        : '<span class="muted" style="font-size:11.5px">&lt;2MB 未拆表</span>';

      var eligBadge = o.eligibleForSlim
        ? '<span class="badge badge-green">可安全瘦身</span>'
        : '<span class="badge badge-neutral" title="' + esc(o.ineligibleReason) + '">' + esc(o.ineligibleReason || '受保护跳过') + '</span>';

      return '<tr class="' + (checked ? 'selected' : '') + '">' +
        '<td style="width:36px"><input type="checkbox" class="sto-off-chk" data-cid="' + esc(o.cid) + '"' + (checked ? ' checked' : '') + (!o.eligibleForSlim ? ' disabled' : '') + ' /></td>' +
        '<td><strong>' + esc(o.title) + '</strong><div class="muted mono" style="font-size:11px">' + esc(o.projectName) + (o.isSubagent ? ' · 子代理' : ' · 主会话') + '</div></td>' +
        '<td class="mono">' + fmt(o.totalBytes) + (o.walBytes > 0 ? '<div class="muted" style="font-size:10.5px">含 WAL ' + fmt(o.walBytes) + '</div>' : '') + '</td>' +
        '<td>' + ratioHtml + '</td>' +
        '<td class="mono">' + esc(o.ageDays) + ' 天</td>' +
        '<td>' + eligBadge + '</td>' +
        '<td class="mono muted" style="font-size:11px">' + esc(o.status || 'IDLE') + '</td>' +
      '</tr>';
    }).join('');
  }

  function updateSlimActionButton() {
    var btn = AGY.dom.el('btnRunStorageSlim');
    if (!btn) return;
    var cnt = Object.keys(AGY.state.storageCheckedCids).filter(function (k) { return AGY.state.storageCheckedCids[k]; }).length;
    btn.disabled = cnt === 0;
    btn.textContent = (AGY.state.storageSlimMode === 'coldArchivePrune' ? '执行 5A 冷归档裁剪 (' : '执行 5C 无损抽脂 (') + cnt + ')';
  }

  function renderStorage() {
    var el = AGY.dom.el;
    var st = AGY.state;
    var esc = AGY.ui.escapeHtml;
    var fmt = AGY.ui.formatBytes;
    var rep = st.storageData;

    if (!rep) {
      el('mainCanvas').innerHTML = '<div class="empty-state">正在扫描 .gemini 磁盘占用与 SQLite 表级分布...</div>';
      runStorageAnalyze();
      return;
    }

    var total = rep.totalBytes || 1;
    function pct(v) { return ((Number(v || 0) / total) * 100).toFixed(1); }

    var orph = rep.orphans || {};
    var leg = rep.legacyCliArchive || {};
    var slimRes = st.lastSlimResult;

    var resultBannerHtml = '';
    if (slimRes) {
      resultBannerHtml =
        '<div class="alert-banner success">' +
          '<div class="between wrap gap-sm">' +
            '<div>' +
              '<strong>上次瘦身结果（' + esc(slimRes.mode) + '）：</strong>' +
              '处理 <strong>' + esc(slimRes.processedCount) + '</strong> 项，跳过 ' + esc(slimRes.skippedCount) + ' 项，释放 <strong>' + esc(slimRes.freedMegabytes) + ' MB</strong>' +
              (slimRes.preFlightPassed ? ' · <span class="badge badge-green">预检通过</span>' : '') +
            '</div>' +
            (slimRes.coldArchivePath
              ? '<div class="row gap-xs">' +
                  '<button class="btn btn-xs" id="btnCopyColdPath">复制冷归档路径</button>' +
                  '<button class="btn btn-xs" id="btnOpenColdDir">打开冷归档目录</button>' +
                '</div>'
              : '') +
          '</div>' +
          (slimRes.coldArchivePath
            ? '<div class="mono" style="font-size:11.5px;margin-top:5px">🛡 强制冷归档备份包 (.agy)：' + esc(slimRes.coldArchivePath) + '</div>'
            : '') +
          (slimRes.legacyCliArchivePath
            ? '<div class="mono" style="font-size:11.5px;margin-top:3px">📦 Legacy CLI 冷归档：' + esc(slimRes.legacyCliArchivePath) + '</div>'
            : '') +
        '</div>';
    }

    el('mainCanvas').innerHTML =
      resultBannerHtml +
      '<div class="card" style="margin-bottom:16px">' +
        '<div class="between wrap gap-sm">' +
          '<div><strong>.gemini 存储空间总览：' + esc(rep.totalMegabytes) + ' MB</strong><div class="muted mono" style="font-size:11.5px">' + esc(rep.geminiPath) + '</div></div>' +
          '<div class="row gap-sm wrap">' +
            '<label class="row gap-xs" style="font-size:12px">最小天数 <input class="input" id="numStoAge" type="number" min="0" step="1" value="' + esc(st.storageMinAgeDays) + '" style="width:68px" /></label>' +
            '<label class="row gap-xs" style="font-size:12px">最小 MB <input class="input" id="numStoSize" type="number" min="0" step="0.5" value="' + esc(st.storageMinSizeMb) + '" style="width:74px" /></label>' +
            '<button class="btn btn-sm" id="btnReanalyzeStorage">重新扫描</button>' +
          '</div>' +
        '</div>' +
        '<div class="storage-bar-track">' +
          '<div class="storage-seg seg-root" style="width:' + pct(rep.rootDbsBytes) + '%" title="主会话 DB: ' + fmt(rep.rootDbsBytes) + '"></div>' +
          '<div class="storage-seg seg-sub" style="width:' + pct(rep.subagentDbsBytes) + '%" title="子代理 DB: ' + fmt(rep.subagentDbsBytes) + '"></div>' +
          '<div class="storage-seg seg-brain" style="width:' + pct(rep.brainDirsBytes) + '%" title="脑区产物: ' + fmt(rep.brainDirsBytes) + '"></div>' +
          '<div class="storage-seg seg-cli" style="width:' + pct(rep.cliDbsBytes) + '%" title="CLI 会话: ' + fmt(rep.cliDbsBytes) + '"></div>' +
          '<div class="storage-seg seg-annot" style="width:' + pct(rep.annotationsBytes) + '%" title="标注: ' + fmt(rep.annotationsBytes) + '"></div>' +
          '<div class="storage-seg seg-orphan" style="width:' + pct(rep.orphansBytes) + '%" title="孤儿垃圾: ' + fmt(rep.orphansBytes) + '"></div>' +
        '</div>' +
        '<div class="legend-row">' +
          '<span class="legend-item"><i class="legend-dot seg-root"></i>主会话 DB ' + fmt(rep.rootDbsBytes) + '</span>' +
          '<span class="legend-item"><i class="legend-dot seg-sub"></i>子代理 DB ' + fmt(rep.subagentDbsBytes) + '</span>' +
          '<span class="legend-item"><i class="legend-dot seg-brain"></i>脑区目录 ' + fmt(rep.brainDirsBytes) + '</span>' +
          '<span class="legend-item"><i class="legend-dot seg-cli"></i>CLI DB ' + fmt(rep.cliDbsBytes) + '</span>' +
          '<span class="legend-item"><i class="legend-dot seg-orphan"></i>孤儿残留 ' + fmt(rep.orphansBytes) + '</span>' +
        '</div>' +
      '</div>' +

      '<div class="action-grid">' +
        '<div class="card">' +
          '<div class="between" style="margin-bottom:8px"><strong>安全瘦身向导 (5C 无损抽脂 / 5A 冷归档裁剪)</strong></div>' +
          '<div class="pill-group" style="margin-bottom:10px">' +
            '<button class="filter-pill ' + (st.storageSlimMode === 'liposuction' ? 'active' : '') + '" data-slimmode="liposuction">Mode 5C: 清空 executor_metadata + VACUUM</button>' +
            '<button class="filter-pill ' + (st.storageSlimMode === 'coldArchivePrune' ? 'active' : '') + '" data-slimmode="coldArchivePrune">Mode 5A: 冷归档后移除子代理 DB</button>' +
          '</div>' +
          '<div class="muted" style="font-size:12px;margin-bottom:10px">' +
            '安全护栏：① 强制只处理闲置超龄子代理（跳过主会话与 not_fully_idle=1）；② 抽脂前自动在 %TEMP% 执行沙盒预检；③ 写入前强制生成完整冷归档 <span class="mono">.agy</span>。' +
          '</div>' +
          '<button class="btn btn-primary" id="btnRunStorageSlim">执行瘦身</button>' +
        '</div>' +

        '<div class="card">' +
          '<div class="between" style="margin-bottom:8px"><strong>全量孤儿 GC 与历史 CLI 归档裁剪</strong><span class="badge badge-neutral">' + esc(orph.totalItems || 0) + ' 项残留</span></div>' +
          '<div class="muted" style="font-size:12px;line-height:1.7;margin-bottom:10px">' +
            '孤儿 DB：' + esc(orph.orphanDbFilesCount || 0) + ' · 残留 .db.bak：' + esc(orph.orphanDbBakCount || 0) + ' · 残留 .db-journal：' + esc(orph.orphanDbJournalCount || 0) + '<br>' +
            '孤儿脑区：' + esc(orph.orphanBrainDirsCount || 0) + ' · 孤儿 .pbtxt：' + esc(orph.orphanAnnotationsCount || 0) +
            (leg.exists ? ('<br>历史 CLI 摘要库：' + esc(leg.entryCount || 0) + ' 条 (' + fmt(leg.totalBytes) + ')') : '') +
          '</div>' +
          '<div class="row gap-sm wrap">' +
            (leg.exists ? '<label class="row gap-xs" style="font-size:12px;cursor:pointer"><input type="checkbox" id="chkPruneLegacyCli"' + (st.storagePruneLegacyCli ? ' checked' : '') + ' /> 先归档并裁剪历史 CLI 摘要库</label>' : '') +
            '<button class="btn btn-danger-outline" id="btnRunCompleteOrphanGc">执行全量孤儿 GC</button>' +
          '</div>' +
        '</div>' +
      '</div>' +

      '<div class="card">' +
        '<div class="between wrap gap-sm" style="margin-bottom:10px">' +
          '<strong>大体积数据库 Top 占用榜 (含 executor_metadata 占比)</strong>' +
          '<div class="row gap-sm"><button class="btn btn-xs" id="btnStoSelectEligible">全选可瘦身项</button><button class="btn btn-xs" id="btnStoClearEligible">清空勾选</button></div>' +
        '</div>' +
        '<div style="max-height:380px;overflow:auto;border:1px solid var(--border);border-radius:8px">' +
          '<table class="data-table"><thead><tr><th></th><th>会话与工程</th><th>体积</th><th>executor_metadata 占比</th><th>静置天数</th><th>瘦身资格</th><th>状态</th></tr></thead>' +
            '<tbody id="stoOffendersTbody">' + renderOffenderRowsHtml(rep.topOffenders || []) + '</tbody>' +
          '</table>' +
        '</div>' +
      '</div>';

    el('btnReanalyzeStorage').addEventListener('click', function () {
      st.storageMinAgeDays = numOrDefault(el('numStoAge').value, 7);
      st.storageMinSizeMb = numOrDefault(el('numStoSize').value, 5);
      st.storageCheckedCids = {};
      runStorageAnalyze();
    });

    if (el('btnCopyColdPath') && slimRes && slimRes.coldArchivePath) {
      el('btnCopyColdPath').addEventListener('click', function () {
        AGY.api.copyToClipboard(slimRes.coldArchivePath, '冷归档路径已复制 ✓');
      });
    }
    if (el('btnOpenColdDir') && slimRes && slimRes.coldArchivePath) {
      el('btnOpenColdDir').addEventListener('click', async function () {
        try {
          await AGY.api.apiFetch('/api/native/open-in-explorer', {
            method: 'POST',
            body: JSON.stringify({ path: slimRes.coldArchivePath })
          });
        } catch (e) { AGY.api.showToast(e.message, 'error'); }
      });
    }

    var modeBtns = el('mainCanvas').querySelectorAll('[data-slimmode]');
    for (var m = 0; m < modeBtns.length; m++) {
      modeBtns[m].addEventListener('click', function () {
        st.storageSlimMode = this.getAttribute('data-slimmode');
        for (var j = 0; j < modeBtns.length; j++) modeBtns[j].classList.remove('active');
        this.classList.add('active');
        updateSlimActionButton();
      });
    }

    if (el('chkPruneLegacyCli')) {
      el('chkPruneLegacyCli').addEventListener('change', function () { st.storagePruneLegacyCli = this.checked; });
    }

    el('btnStoSelectEligible').addEventListener('click', function () {
      var list = rep.topOffenders || [];
      for (var i = 0; i < list.length; i++) {
        if (list[i].eligibleForSlim) st.storageCheckedCids[list[i].cid] = true;
      }
      el('stoOffendersTbody').innerHTML = renderOffenderRowsHtml(list);
      bindOffenderChks();
      updateSlimActionButton();
    });
    el('btnStoClearEligible').addEventListener('click', function () {
      st.storageCheckedCids = {};
      el('stoOffendersTbody').innerHTML = renderOffenderRowsHtml(rep.topOffenders || []);
      bindOffenderChks();
      updateSlimActionButton();
    });

    el('btnRunStorageSlim').addEventListener('click', function () {
      var cids = Object.keys(st.storageCheckedCids).filter(function (k) { return st.storageCheckedCids[k]; });
      if (cids.length === 0) return;
      executeStorageSlimRequest(st.storageSlimMode, cids, false, false, false);
    });

    el('btnRunCompleteOrphanGc').addEventListener('click', function () {
      executeStorageSlimRequest('orphanGcOnly', [], true, !!st.storagePruneLegacyCli, false);
    });

    function bindOffenderChks() {
      var chks = el('stoOffendersTbody').querySelectorAll('.sto-off-chk');
      for (var i = 0; i < chks.length; i++) {
        chks[i].addEventListener('change', function () {
          st.storageCheckedCids[this.getAttribute('data-cid')] = this.checked;
          AGY.dom.patchCheckboxRow(this.closest('tr'), this.checked);
          updateSlimActionButton();
        });
      }
    }

    bindOffenderChks();
    updateSlimActionButton();
  }

  async function executeStorageSlimRequest(mode, cids, cleanOrphans, pruneLegacy, closeIde) {
    try {
      var res = await AGY.api.apiFetch('/api/storage/slim', {
        method: 'POST',
        body: JSON.stringify({
          mode: mode,
          cids: cids,
          minAgeDays: numOrDefault(AGY.state.storageMinAgeDays, 7),
          minSizeMb: numOrDefault(AGY.state.storageMinSizeMb, 5),
          verifyPreFlight: true,
          cleanOrphans: !!cleanOrphans,
          pruneLegacyCliArchive: !!pruneLegacy,
          closeIdeIfRunning: !!closeIde
        })
      });
      AGY.state.lastSlimResult = res;
      AGY.state.storageCheckedCids = {};
      AGY.api.showToast('操作完成，释放 ' + res.freedMegabytes + ' MB ✓', 'success');
      await runStorageAnalyze();
      if (AGY.app && AGY.app.refreshAllSilent) await AGY.app.refreshAllSilent();
    } catch (e) {
      if (e.status === 409 && e.data && e.data.code === 'IDE_RUNNING') {
        AGY.ui.confirmCloseIdeModal(e.data.parentCount, e.data.lsCount, '执行存储瘦身/清理', function () {
          executeStorageSlimRequest(mode, cids, cleanOrphans, pruneLegacy, true);
        });
      } else {
        AGY.api.showToast('瘦身执行失败：' + e.message, 'error');
      }
    }
  }

  AGY.views.storage = renderStorage;
})();
