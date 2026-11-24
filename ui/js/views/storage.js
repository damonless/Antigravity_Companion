(function () {
  'use strict';

  var AGY = window.AGY = window.AGY || {};
  AGY.views = AGY.views || {};

  function numOrDefault(val, def) {
    var n = parseFloat(val);
    return (!isNaN(n) && isFinite(n) && n >= 0) ? n : def;
  }

  function getStorageHost(explicitHost) {
    return explicitHost || AGY.dom.el('backupSubHost') || AGY.dom.el('mainCanvas');
  }

  function isStorageViewVisible() {
    var st = AGY.state;
    return st.view === 'storage' || (st.view === 'backup' && st.backupSubView === 'storage');
  }

  async function runStorageAnalyze(skipRender) {
    var st = AGY.state;
    if (st._storageAnalyzePromise) return st._storageAnalyzePromise;
    st._storageAnalyzePromise = (async function () {
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
        st.storageAnalyzeError = '';
        if (st.dirty) st.dirty.storage = false;
        if (Object.keys(st.storageCheckedCids).length === 0 && res.topOffenders) {
          for (var i = 0; i < res.topOffenders.length; i++) {
            if (res.topOffenders[i].eligibleForSlim) {
              st.storageCheckedCids[res.topOffenders[i].cid] = true;
            }
          }
        }
        if (!skipRender && isStorageViewVisible()) renderStorage();
        return res;
      } catch (e) {
        st.storageAnalyzeError = e && e.message ? e.message : String(e);
        AGY.api.showToast('存储分析失败：' + st.storageAnalyzeError, 'error');
        if (!skipRender && isStorageViewVisible()) renderStorage();
        throw e;
      } finally {
        st._storageAnalyzePromise = null;
      }
    })();
    return st._storageAnalyzePromise;
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
        ? '<span class="badge badge-green">可安全处理</span>'
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

  function updateSlimActionButtons() {
    var cnt = Object.keys(AGY.state.storageCheckedCids).filter(function (k) { return AGY.state.storageCheckedCids[k]; }).length;
    var btn5a = AGY.dom.el('btnRunMode5A');
    if (btn5a) {
      btn5a.disabled = cnt === 0;
      btn5a.textContent = '执行 5A 冷归档裁剪 (' + cnt + ')';
    }
    var btn5c = AGY.dom.el('btnRunMode5C');
    if (btn5c) {
      btn5c.disabled = cnt === 0;
      btn5c.textContent = '执行 Mode 5C 微创抽脂 (' + cnt + ')';
    }
  }

  function confirmMode5CLiposuctionModal(cids) {
    var esc = AGY.ui.escapeHtml;
    var st = AGY.state;
    AGY.ui.openModal(
      '确认执行实验性 Mode 5C 微创抽脂？',
      '<div class="alert-banner">' +
        '<strong>⚠ 实验性高风险提示：</strong>Mode 5C 将直接修改选中的 <strong>' + esc(cids.length) + '</strong> 个闲置子代理 SQLite 数据库（清空 <span class="mono">executor_metadata</span> 并执行 <span class="mono">VACUUM</span>）。' +
      '</div>' +
      '<div style="line-height:1.7;font-size:12.5px">' +
        '• 写入前将先在 <span class="mono">%TEMP%</span> 进行沙盒完整性与 Markdown 渲染一致性预检；<br>' +
        '• 修改前强制生成冷归档备份包 (<span class="mono">~/.gemini/companion-cold-archives/subagents_*.agy</span>)；<br>' +
        '• 仅限已静置 &ge; <strong>' + esc(numOrDefault(st.storageMinAgeDays, 7)) + '</strong> 天、体积 &ge; <strong>' + esc(numOrDefault(st.storageMinSizeMb, 5)) + ' MB</strong> 的闲置子代理，主会话与运行中会话将被强制跳过。' +
      '</div>',
      '<button class="btn" data-act="cancel">取消</button><button class="btn btn-danger" data-act="confirm">确认风险并执行 5C 抽脂</button>',
      function (_mb, mf) {
        mf.querySelector('[data-act="cancel"]').addEventListener('click', AGY.ui.closeModal);
        mf.querySelector('[data-act="confirm"]').addEventListener('click', function () {
          AGY.ui.closeModal();
          executeStorageSlimRequest('liposuction', cids, false, false, false);
        });
      }
    );
  }

  function renderStorage(containerEl) {
    var el = AGY.dom.el;
    var st = AGY.state;
    var esc = AGY.ui.escapeHtml;
    var fmt = AGY.ui.formatBytes;
    var host = getStorageHost(containerEl);
    if (!host) return;

    var rep = st.storageData;
    if (!rep || (st.dirty && st.dirty.storage)) {
      if (st.storageAnalyzeError && !st._lazyFetchingStorage && !st._storageAnalyzePromise) {
        host.innerHTML =
          '<div class="alert-banner danger between wrap gap-sm">' +
            '<span>存储分析失败：' + esc(st.storageAnalyzeError) + '</span>' +
            '<button class="btn btn-sm btn-primary" id="btnRetryStorageAnalyze" type="button">重试扫描</button>' +
          '</div>';
        if (el('btnRetryStorageAnalyze')) {
          el('btnRetryStorageAnalyze').addEventListener('click', function () {
            st.storageAnalyzeError = '';
            renderStorage(host);
          });
        }
        return;
      }
      host.innerHTML = '<div class="empty-state">正在扫描 .gemini 磁盘占用与 SQLite 表级分布...</div>';
      if (!st._lazyFetchingStorage) {
        runStorageAnalyze(false).catch(function () {});
      }
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
              '<strong>上次处理结果（' + esc(slimRes.mode) + '）：</strong>' +
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
            ? '<div class="mono" style="font-size:11.5px;margin-top:5px">🛡 冷归档备份包 (.agy)：' + esc(slimRes.coldArchivePath) + '</div>'
            : '') +
          (slimRes.legacyCliArchivePath
            ? '<div class="mono" style="font-size:11.5px;margin-top:3px">📦 Legacy CLI 冷归档：' + esc(slimRes.legacyCliArchivePath) + '</div>'
            : '') +
        '</div>';
    }

    host.innerHTML =
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
          '<div class="between" style="margin-bottom:8px"><strong>全量孤儿 GC 与历史 CLI 冷归档</strong><span class="badge badge-neutral">' + esc(orph.totalItems || 0) + ' 项残留</span></div>' +
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

        '<div class="card">' +
          '<div class="between" style="margin-bottom:8px"><strong>Mode 5A：子代理冷归档裁剪</strong><span class="badge badge-green">推荐安全方案</span></div>' +
          '<div class="muted" style="font-size:12px;line-height:1.7;margin-bottom:10px">' +
            '在将超龄闲置子代理数据库完整冷归档为 <span class="mono">~/.gemini/companion-cold-archives/subagents_*.agy</span> 并通过可读性校验后，安全移除选中的子代理 <span class="mono">.db</span> 文件（保留主会话与 <span class="mono">brain/</span> 产物）。' +
          '</div>' +
          '<button class="btn btn-primary" id="btnRunMode5A">执行 5A 冷归档裁剪 (0)</button>' +
        '</div>' +
      '</div>' +

      '<div class="card">' +
        '<div class="between wrap gap-sm" style="margin-bottom:10px">' +
          '<strong>大体积数据库 Top 占用榜 (含 executor_metadata 占比)</strong>' +
          '<div class="row gap-sm"><button class="btn btn-xs" id="btnStoSelectEligible">全选可安全处理项</button><button class="btn btn-xs" id="btnStoClearEligible">清空勾选</button></div>' +
        '</div>' +
        '<div style="max-height:360px;overflow:auto;border:1px solid var(--border);border-radius:8px">' +
          '<table class="data-table"><thead><tr><th></th><th>会话与工程</th><th>体积</th><th>executor_metadata 占比</th><th>静置天数</th><th>处理资格</th><th>状态</th></tr></thead>' +
            '<tbody id="stoOffendersTbody">' + renderOffenderRowsHtml(rep.topOffenders || []) + '</tbody>' +
          '</table>' +
        '</div>' +
      '</div>' +

      '<details class="experimental-details" id="detailsMode5C">' +
        '<summary class="experimental-summary">' +
          '<span>▸ 高级微创瘦身（实验性） — Mode 5C 数据库原位抽脂</span>' +
          '<span class="badge badge-amber">实验性 · 默认折叠</span>' +
        '</summary>' +
        '<div class="experimental-body">' +
          '<div class="alert-banner" style="margin-bottom:12px">' +
            '<strong>⚠ 实验性功能风险提示：</strong>Mode 5C 会直接修改下方表格中所选闲置子代理的 SQLite 数据库（清空 <span class="mono">executor_metadata</span> 并执行 <span class="mono">VACUUM</span>）。虽有 <span class="mono">%TEMP%</span> 沙盒预检与强制 <span class="mono">.agy</span> 冷归档保护，仍建议优先使用上方的 <strong>Mode 5A 冷归档裁剪</strong>；执行前将弹出二次风险确认窗口。' +
          '</div>' +
          '<div class="between wrap gap-sm">' +
            '<div class="muted" style="font-size:12px">作用对象：上方列表已勾选的「可安全处理」子代理数据库（强制跳过主会话与运行中会话）。</div>' +
            '<button class="btn btn-danger-outline" id="btnRunMode5C">执行 Mode 5C 微创抽脂 (0)</button>' +
          '</div>' +
        '</div>' +
      '</details>';

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
      updateSlimActionButtons();
    });
    el('btnStoClearEligible').addEventListener('click', function () {
      st.storageCheckedCids = {};
      el('stoOffendersTbody').innerHTML = renderOffenderRowsHtml(rep.topOffenders || []);
      bindOffenderChks();
      updateSlimActionButtons();
    });

    el('btnRunMode5A').addEventListener('click', function () {
      var cids = Object.keys(st.storageCheckedCids).filter(function (k) { return st.storageCheckedCids[k]; });
      if (cids.length === 0) return;
      executeStorageSlimRequest('coldArchivePrune', cids, false, false, false);
    });

    el('btnRunMode5C').addEventListener('click', function () {
      var cids = Object.keys(st.storageCheckedCids).filter(function (k) { return st.storageCheckedCids[k]; });
      if (cids.length === 0) return;
      confirmMode5CLiposuctionModal(cids);
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
          updateSlimActionButtons();
        });
      }
    }

    bindOffenderChks();
    updateSlimActionButtons();
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
      if (AGY.app && AGY.app.markNonActiveDirty) AGY.app.markNonActiveDirty('backup');
      await runStorageAnalyze(false);
      if (AGY.app && AGY.app.refreshCoreAndActiveView) {
        AGY.api.apiFetch('/api/overview').then(function (ov) {
          AGY.state.overview = ov;
          if (AGY.app.updateShellBars) AGY.app.updateShellBars();
        }).catch(function () {});
      }
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

  AGY.views.renderStorage = renderStorage;
  AGY.views.storage = renderStorage;
  AGY.views.runStorageAnalyze = runStorageAnalyze;
})();
