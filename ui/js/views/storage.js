(function () {
  'use strict';

  var AGY = window.AGY = window.AGY || {};
  AGY.views = AGY.views || {};

  function numOrDefault(val, def) {
    var n = parseFloat(val);
    return (!isNaN(n) && isFinite(n) && n >= 0) ? n : def;
  }

  function getStorageHost(explicitHost) {
    return explicitHost || AGY.dom.el('mainCanvas');
  }

  function isStorageViewVisible() {
    return AGY.state.view === 'storage';
  }

  function translateIneligibleReason(reason) {
    var r = String(reason || '').trim();
    if (!r) return '受保护跳过';
    if (r === 'Not a subagent session') return '主对话（自动保护）';
    if (r === 'CLI session') return 'CLI 对话（自动保留）';
    if (r === 'not_fully_idle != 0') return '进行中（自动跳过）';
    if (r === 'Missing summary metadata') return '缺少摘要信息（跳过）';
    if (r.indexOf('Status is not IDLE') === 0) return '非空闲状态（跳过）';
    var mAge = /^Age\s+([\d.]+)d\s*<\s*([\d.]+)d$/i.exec(r);
    if (mAge) return '静置未满 ' + mAge[2] + ' 天（当前 ' + mAge[1] + ' 天）';
    var mSize = /^Size\s+([\d.]+)MB\s*<\s*([\d.]+)MB$/i.exec(r);
    if (mSize) return '体积未达 ' + mSize[2] + ' MB（当前 ' + mSize[1] + ' MB）';
    if (/^[\x20-\x7E]+$/.test(r)) return '暂不满足清理条件';
    return r;
  }

  function isStorageThresholdDirty() {
    var ageInp = AGY.dom.el('numStoAge');
    var sizeInp = AGY.dom.el('numStoSize');
    if (!ageInp || !sizeInp) return false;
    var curAge = numOrDefault(ageInp.value, 7);
    var curSize = numOrDefault(sizeInp.value, 5);
    var scannedAge = AGY.state.storageScannedAgeDays !== undefined
      ? AGY.state.storageScannedAgeDays
      : numOrDefault(AGY.state.storageMinAgeDays, 7);
    var scannedSize = AGY.state.storageScannedSizeMb !== undefined
      ? AGY.state.storageScannedSizeMb
      : numOrDefault(AGY.state.storageMinSizeMb, 5);
    return curAge !== scannedAge || curSize !== scannedSize;
  }

  function syncStorageThresholdDirtyNotice() {
    var dirty = isStorageThresholdDirty();
    var notice = AGY.dom.el('stoThresholdDirtyNotice');
    if (notice) notice.classList.toggle('hidden', !dirty);
    var btnRescan = AGY.dom.el('btnReanalyzeStorage');
    if (btnRescan) btnRescan.classList.toggle('btn-primary', dirty);
  }

  function promptRescanForChangedThresholds() {
    AGY.ui.openModal(
      '筛选条件已修改，请先重新扫描',
      '<div style="line-height:1.7">您修改了「静置天数」或「单文件体积」筛选条件，当前列表仍为旧条件下的扫描结果。为避免误按旧列表执行清理，请先按新条件重新扫描。</div>',
      '<button class="btn" data-act="cancel">取消</button><button class="btn btn-primary" data-act="rescan">按新条件重新扫描</button>',
      function (_mb, mf) {
        mf.querySelector('[data-act="cancel"]').addEventListener('click', AGY.ui.closeModal);
        mf.querySelector('[data-act="rescan"]').addEventListener('click', function () {
          AGY.ui.closeModal();
          var ageInp = AGY.dom.el('numStoAge');
          var sizeInp = AGY.dom.el('numStoSize');
          if (ageInp) AGY.state.storageMinAgeDays = numOrDefault(ageInp.value, 7);
          if (sizeInp) AGY.state.storageMinSizeMb = numOrDefault(sizeInp.value, 5);
          AGY.state.storageCheckedCids = {};
          runStorageAnalyze(false).catch(function () {});
        });
      }
    );
  }

  async function runStorageAnalyze(skipRender) {
    var st = AGY.state;
    if (st._storageAnalyzePromise) return st._storageAnalyzePromise;
    st._storageAnalyzePromise = (async function () {
      try {
        var reqAge = numOrDefault(st.storageMinAgeDays, 7);
        var reqSize = numOrDefault(st.storageMinSizeMb, 5);
        var res = await AGY.api.apiFetch('/api/storage/analyze', {
          method: 'POST',
          body: JSON.stringify({
            topN: 30,
            minAgeDays: reqAge,
            minSizeMb: reqSize
          })
        });
        st.storageData = res;
        st.storageScannedAgeDays = reqAge;
        st.storageScannedSizeMb = reqSize;
        st.storageAnalyzeError = '';
        if (st.dirty) st.dirty.storage = false;
        if (Object.keys(st.storageCheckedCids).length === 0 && res.topOffenders) {
          for (var i = 0; i < res.topOffenders.length; i++) {
            if (res.topOffenders[i].eligibleForSlim) {
              st.storageCheckedCids[res.topOffenders[i].cid] = true;
            }
          }
        }
        try {
          st.coldArchivesData = await AGY.api.apiFetch('/api/storage/cold-archives');
        } catch (_e) {}
        if (!skipRender && isStorageViewVisible()) renderStorage();
        return res;
      } catch (e) {
        st.storageAnalyzeError = e && e.message ? e.message : String(e);
        AGY.api.showToast('空间分析失败：' + st.storageAnalyzeError, 'error');
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
      return '<tr><td colspan="7" class="empty-state">本机未发现大体积对话数据。</td></tr>';
    }
    return offenders.map(function (o) {
      var checked = !!st.storageCheckedCids[o.cid];
      var execBytes = o.executorMetadataBytes || 0;
      var stepsBytes = o.stepsBytes || 0;
      var denom = execBytes + stepsBytes;
      var execPct = denom > 0 ? Math.round((execBytes / denom) * 100) : 0;
      var stepsPct = denom > 0 ? (100 - execPct) : 0;

      var ratioHtml = o.hasTableBreakdown
        ? ('<div class="row gap-xs" title="执行元数据: ' + fmt(execBytes) + ' / 对话步骤: ' + fmt(stepsBytes) + '">' +
             '<div class="ratio-track"><div class="ratio-exec" style="width:' + execPct + '%"></div><div class="ratio-steps" style="width:' + stepsPct + '%"></div></div>' +
             '<span class="mono" style="font-size:11px">' + execPct + '%</span>' +
           '</div>')
        : '<span class="muted" style="font-size:11.5px">体积较小，无需拆解</span>';

      var cnReason = translateIneligibleReason(o.ineligibleReason);
      var eligBadge = o.eligibleForSlim
        ? '<span class="badge badge-green">可安全清理</span>'
        : '<span class="badge badge-neutral" title="' + esc(cnReason) + '">' + esc(cnReason) + '</span>';

      var statusBadge = (o.status && o.status !== 'IDLE')
        ? '<span class="badge badge-amber">进行中 (自动跳过)</span>'
        : '<span class="badge badge-neutral">空闲 (可操作)</span>';

      return '<tr class="' + (checked ? 'selected' : '') + '">' +
        '<td style="width:36px"><input type="checkbox" class="sto-off-chk" data-cid="' + esc(o.cid) + '"' + (checked ? ' checked' : '') + (!o.eligibleForSlim ? ' disabled' : '') + ' /></td>' +
        '<td><strong>' + esc(o.title) + '</strong><div class="muted mono" style="font-size:11px">' + esc(o.projectName === 'Outside of Project' ? '未关联项目' : o.projectName) + (o.isSubagent ? ' · 后台子任务' : ' · 主对话') + '</div></td>' +
        '<td class="mono">' + fmt(o.totalBytes) + (o.walBytes > 0 ? '<div class="muted" style="font-size:10.5px">含日志 ' + fmt(o.walBytes) + '</div>' : '') + '</td>' +
        '<td>' + ratioHtml + '</td>' +
        '<td class="mono">' + esc(o.ageDays) + ' 天</td>' +
        '<td>' + eligBadge + '</td>' +
        '<td>' + statusBadge + '</td>' +
      '</tr>';
    }).join('');
  }

  function updateSlimActionButtons() {
    var cnt = Object.keys(AGY.state.storageCheckedCids).filter(function (k) { return AGY.state.storageCheckedCids[k]; }).length;
    var btn5a = AGY.dom.el('btnRunMode5A');
    if (btn5a) {
      btn5a.disabled = cnt === 0;
      btn5a.textContent = '备份并清理已选后台子任务 (' + cnt + ')';
    }
    var btn5c = AGY.dom.el('btnRunMode5C');
    if (btn5c) {
      btn5c.disabled = cnt === 0;
      btn5c.textContent = '执行高级原位压缩 (' + cnt + ')';
    }
  }

  function confirmMode5AColdArchiveModal(cids, offenders) {
    var esc = AGY.ui.escapeHtml;
    var fmt = AGY.ui.formatBytes;
    var list = offenders || [];
    var byCid = {};
    for (var i = 0; i < list.length; i++) byCid[list[i].cid] = list[i];
    var totalBytes = 0;
    var rows = [];
    for (var j = 0; j < cids.length; j++) {
      var o = byCid[cids[j]];
      if (o) totalBytes += (o.totalBytes || 0);
      if (j < 8) {
        rows.push(
          '<div class="del-manifest-row">' +
            '<span>' + esc(o ? o.title : cids[j]) + '</span>' +
            '<span class="mono muted" style="white-space:nowrap">' + esc(o ? fmt(o.totalBytes) : '-') + '</span>' +
          '</div>'
        );
      }
    }
    if (cids.length > 8) {
      rows.push('<div class="muted" style="padding-top:4px">……以及其他 ' + (cids.length - 8) + ' 个后台子任务</div>');
    }

    AGY.ui.openModal(
      '确认备份并清理 ' + cids.length + ' 个后台子任务？',
      '<div class="alert-banner success" style="margin-bottom:10px">🛡 <strong>安全备份保障：</strong>清理前会先将所选后台子任务完整打包为 <strong>清理前安全备份 (.agy)</strong> 并校验可读，随时可从本页历史列表一键还原；主对话与进行中任务不受影响。</div>' +
      '<div style="line-height:1.7;margin-bottom:8px">待清理条目：<strong>' + esc(cids.length) + '</strong> 个 · 预计释放空间：<strong>' + esc(fmt(totalBytes)) + '</strong></div>' +
      (rows.length ? '<div class="del-manifest-list">' + rows.join('') + '</div>' : ''),
      '<button class="btn" data-act="cancel">取消</button><button class="btn btn-primary" data-act="confirm">确认备份并清理</button>',
      function (_mb, mf) {
        mf.querySelector('[data-act="cancel"]').addEventListener('click', AGY.ui.closeModal);
        mf.querySelector('[data-act="confirm"]').addEventListener('click', function () {
          AGY.ui.closeModal();
          executeStorageSlimRequest('coldArchivePrune', cids, false, false, false, false, null);
        });
      }
    );
  }

  function confirmOrphanGcModal(orph, leg, pruneLegacy) {
    var esc = AGY.ui.escapeHtml;
    var fmt = AGY.ui.formatBytes;
    var totalItems = (orph && orph.totalItems) || 0;
    var totalBytes = (orph && orph.totalBytes) || 0;
    var willPruneLegacy = !!(pruneLegacy && leg && leg.exists);

    var bullets = [];
    if (totalItems > 0) {
      bullets.push('<li>残留垃圾文件：<strong>' + esc(totalItems) + '</strong> 项（预计释放 <strong>' + esc(fmt(totalBytes)) + '</strong>）— 将先移入 <strong>回收站</strong>，30 天内随时可一键还原</li>');
    }
    if (willPruneLegacy) {
      bullets.push('<li>旧版 CLI 历史摘要库：<strong>' + esc(leg.entryCount || 0) + '</strong> 条（<strong>' + esc(fmt(leg.totalBytes)) + '</strong>）— 将先生成 <strong>清理前安全备份 (.agy)</strong> 再清理</li>');
    }

    AGY.ui.openModal(
      '确认清理残留垃圾文件？',
      '<div class="alert-banner success" style="margin-bottom:10px">🛡 <strong>可撤销清理：</strong>所有残留垃圾文件都会先移入 <strong>回收站</strong>，旧版 CLI 摘要库会先生成清理前安全备份，不会删除您的正常对话或项目源码。</div>' +
      '<ul style="margin:6px 0 8px 18px;line-height:1.75">' + bullets.join('') + '</ul>',
      '<button class="btn" data-act="cancel">取消</button><button class="btn btn-danger" data-act="confirm">确认移入回收站并清理</button>',
      function (_mb, mf) {
        mf.querySelector('[data-act="cancel"]').addEventListener('click', AGY.ui.closeModal);
        mf.querySelector('[data-act="confirm"]').addEventListener('click', function () {
          AGY.ui.closeModal();
          executeStorageSlimRequest('orphanGcOnly', [], true, willPruneLegacy, false, false, null);
        });
      }
    );
  }

  function confirmMode5CLiposuctionModal(cids) {
    var esc = AGY.ui.escapeHtml;
    var st = AGY.state;
    AGY.ui.openModal(
      '确认执行高级原位压缩（实验性）？',
      '<div class="alert-banner">' +
        '<strong>⚠ 风险说明：</strong>此操作将直接裁剪所选 <strong>' + esc(cids.length) + '</strong> 个闲置后台子任务数据库中的执行元数据并重整空间。' +
      '</div>' +
      '<div style="line-height:1.7;font-size:12.5px">' +
        '• 写入前会先在临时目录演练完整性与对话可读性；<br>' +
        '• 修改前会自动生成一份可随时还原的 <strong>清理前安全备份 (.agy)</strong>；<br>' +
        '• 仅处理静置 &ge; <strong>' + esc(numOrDefault(st.storageMinAgeDays, 7)) + '</strong> 天且体积 &ge; <strong>' + esc(numOrDefault(st.storageMinSizeMb, 5)) + ' MB</strong> 的空闲后台子任务，主对话与进行中任务强制跳过。' +
      '</div>',
      '<button class="btn" data-act="cancel">取消</button><button class="btn btn-danger" data-act="confirm">确认并执行</button>',
      function (_mb, mf) {
        mf.querySelector('[data-act="cancel"]').addEventListener('click', AGY.ui.closeModal);
        mf.querySelector('[data-act="confirm"]').addEventListener('click', function () {
          AGY.ui.closeModal();
          executeStorageSlimRequest('liposuction', cids, false, false, false, false, null);
        });
      }
    );
  }

  async function openColdArchiveInBackupView(archivePath, btn) {
    if (AGY.views && AGY.views.openBackupArchiveByPath) {
      return AGY.views.openBackupArchiveByPath(archivePath, btn);
    }
    AGY.api.withButtonBusy(btn, '打开中...', async function () {
      try {
        var res = await AGY.api.apiFetch('/api/backup/open', {
          method: 'POST',
          body: JSON.stringify({ archivePath: archivePath })
        });
        AGY.state.backupData = res;
        AGY.state.backupTab = 'sessions';
        AGY.state.backupCheckedCaps = {};
        AGY.state.backupCheckedConvs = {};
        AGY.state.backupRestoreLegacyCli = !!res.hasLegacyCliSummaryDb;
        AGY.app.switchView('backup');
        AGY.api.showToast('已打开清理前安全备份，勾选即可一键恢复 ✓', 'success');
      } catch (e) {
        AGY.api.showToast('打开备份文件失败：' + e.message, 'error');
      }
    });
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
            '<span>空间分析失败：' + esc(st.storageAnalyzeError) + '</span>' +
            '<button class="btn btn-sm btn-primary" id="btnRetryStorageAnalyze" type="button">重新扫描</button>' +
          '</div>';
        if (el('btnRetryStorageAnalyze')) {
          el('btnRetryStorageAnalyze').addEventListener('click', function () {
            st.storageAnalyzeError = '';
            renderStorage(host);
          });
        }
        return;
      }
      host.innerHTML = '<div class="empty-state">正在分析本机空间占用...</div>';
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
    var coldArchives = (st.coldArchivesData && st.coldArchivesData.items) || [];

    var resultBannerHtml = '';
    if (slimRes) {
      resultBannerHtml =
        '<div class="alert-banner success">' +
          '<div class="between wrap gap-sm">' +
            '<div>' +
              '<strong>上次清理结果：</strong>' +
              '已处理 <strong>' + esc(slimRes.processedCount) + '</strong> 项，跳过 ' + esc(slimRes.skippedCount) + ' 项，释放 <strong>' + esc(slimRes.freedMegabytes) + ' MB</strong>' +
              (slimRes.trashId ? ' · <span class="badge badge-green">残留已移入回收站</span>' : '') +
            '</div>' +
            (slimRes.coldArchivePath
              ? '<div class="row gap-xs">' +
                  '<button class="btn btn-xs btn-primary" id="btnInspectLastColdArchive" type="button">查看并恢复此安全备份</button>' +
                  '<button class="btn btn-xs" id="btnOpenColdDir" type="button">打开所在目录</button>' +
                '</div>'
              : '') +
          '</div>' +
          (slimRes.coldArchivePath
            ? '<div class="mono" style="font-size:11.5px;margin-top:5px">🛡 清理前安全备份 (.agy)：' + esc(slimRes.coldArchivePath) + '</div>'
            : '') +
          (slimRes.legacyCliArchivePath
            ? '<div class="mono" style="font-size:11.5px;margin-top:3px">📦 旧版 CLI 清理前安全备份：' + esc(slimRes.legacyCliArchivePath) + '</div>'
            : '') +
        '</div>';
    }

    var coldArchiveRowsHtml = coldArchives.length === 0
      ? '<div class="muted" style="font-size:12px">暂无清理前安全备份。执行后台子任务清理或旧版 CLI 裁剪时，伴侣会自动在此生成可恢复的 .agy 安全备份。</div>'
      : '<div style="border:1px solid var(--border);border-radius:8px;max-height:220px;overflow:auto">' +
          coldArchives.map(function (ca, idx) {
            var whenStr = ca.createdUtc ? (new Date(ca.createdUtc).toLocaleString()) : (ca.modifiedTime || '-');
            return '<div class="snap-history-row">' +
              '<div><strong>' + esc(ca.fileName) + '</strong><div class="muted mono" style="font-size:11px">' + esc(whenStr) + ' · ' + esc(ca.sizeMegabytes) + ' MB</div></div>' +
              '<div class="row gap-xs">' +
                '<button class="btn btn-xs btn-primary btn-open-cold-agy" data-cidx="' + idx + '" type="button">查看并恢复</button>' +
                '<button class="btn btn-xs btn-explore-cold-agy" data-cidx="' + idx + '" type="button">打开目录</button>' +
              '</div>' +
            '</div>';
          }).join('') +
        '</div>';

    var hasOrphans = (orph.totalItems || 0) > 0;
    var hasLegacy = !!leg.exists;
    var canCleanOrphan = hasOrphans || (hasLegacy && !!st.storagePruneLegacyCli);

    var orphanCardBodyHtml = (!hasOrphans && !hasLegacy)
      ? '<div class="muted" style="font-size:12px;line-height:1.7;margin-bottom:10px">未发现脱离记录的残留数据库、产物目录或临时文件，本机空间干净。</div>'
      : ('<div class="muted" style="font-size:12px;line-height:1.7;margin-bottom:10px">' +
          (hasOrphans
            ? ('残留数据库：' + esc(orph.orphanDbFilesCount || 0) + ' · 残留 .db.bak：' + esc(orph.orphanDbBakCount || 0) + ' · 残留临时日志：' + esc(orph.orphanDbJournalCount || 0) + '<br>' +
               '残留产物目录：' + esc(orph.orphanBrainDirsCount || 0) + ' · 残留标题文件：' + esc(orph.orphanAnnotationsCount || 0) + '（共 ' + esc(fmt(orph.totalBytes || 0)) + '）<br>' +
               '🛡 清理前先移入 <strong>回收站</strong>，30 天内随时可还原。')
            : '当前无残留垃圾文件；下方可按需先备份再清理旧版 CLI 历史摘要库。') +
          (hasLegacy ? ('<br>旧版 CLI 历史摘要库：' + esc(leg.entryCount || 0) + ' 条 (' + fmt(leg.totalBytes) + ')') : '') +
        '</div>');

    var orphanBadgeHtml = hasOrphans
      ? '<span class="badge badge-amber">' + esc(orph.totalItems) + ' 项残留 · ' + esc(fmt(orph.totalBytes || 0)) + '</span>'
      : '<span class="badge badge-green">无需清理</span>';

    var orphanBtnClass = canCleanOrphan ? 'btn btn-danger-outline' : 'btn';
    var orphanBtnLabel = (!hasOrphans && !hasLegacy) ? '暂无残留可清理' : '清理残留垃圾文件';

    host.innerHTML =
      resultBannerHtml +
      '<div class="card" style="margin-bottom:16px">' +
        '<div class="between wrap gap-sm">' +
          '<div><strong>本机数据总占用：' + esc(rep.totalMegabytes) + ' MB</strong><div class="muted mono" style="font-size:11.5px">' + esc(rep.geminiPath) + '</div></div>' +
          '<div class="row gap-sm wrap">' +
            '<label class="row gap-xs" style="font-size:12px">静置天数 ≥ <input class="input" id="numStoAge" type="number" min="0" step="1" value="' + esc(st.storageMinAgeDays) + '" style="width:68px" /></label>' +
            '<label class="row gap-xs" style="font-size:12px">单文件 ≥ MB <input class="input" id="numStoSize" type="number" min="0" step="0.5" value="' + esc(st.storageMinSizeMb) + '" style="width:74px" /></label>' +
            '<button class="btn btn-sm" id="btnReanalyzeStorage" type="button">重新扫描</button>' +
          '</div>' +
        '</div>' +
        '<div class="alert-banner hidden" id="stoThresholdDirtyNotice" style="margin-top:10px;margin-bottom:0">筛选条件已修改，请先点击「重新扫描」更新下方可清理列表后再执行清理。</div>' +
        '<div class="storage-bar-track">' +
          '<div class="storage-seg seg-root" style="width:' + pct(rep.rootDbsBytes) + '%" title="主对话: ' + fmt(rep.rootDbsBytes) + '"></div>' +
          '<div class="storage-seg seg-sub" style="width:' + pct(rep.subagentDbsBytes) + '%" title="后台子任务: ' + fmt(rep.subagentDbsBytes) + '"></div>' +
          '<div class="storage-seg seg-brain" style="width:' + pct(rep.brainDirsBytes) + '%" title="产物目录: ' + fmt(rep.brainDirsBytes) + '"></div>' +
          '<div class="storage-seg seg-cli" style="width:' + pct(rep.cliDbsBytes) + '%" title="CLI 对话: ' + fmt(rep.cliDbsBytes) + '"></div>' +
          '<div class="storage-seg seg-annot" style="width:' + pct(rep.annotationsBytes) + '%" title="标题记录: ' + fmt(rep.annotationsBytes) + '"></div>' +
          '<div class="storage-seg seg-orphan" style="width:' + pct(rep.orphansBytes) + '%" title="残留垃圾文件: ' + fmt(rep.orphansBytes) + '"></div>' +
        '</div>' +
        '<div class="legend-row">' +
          '<span class="legend-item"><i class="legend-dot seg-root"></i>主对话 ' + fmt(rep.rootDbsBytes) + '</span>' +
          '<span class="legend-item"><i class="legend-dot seg-sub"></i>后台子任务 ' + fmt(rep.subagentDbsBytes) + '</span>' +
          '<span class="legend-item"><i class="legend-dot seg-brain"></i>产物目录 ' + fmt(rep.brainDirsBytes) + '</span>' +
          '<span class="legend-item"><i class="legend-dot seg-cli"></i>CLI 对话 ' + fmt(rep.cliDbsBytes) + '</span>' +
          '<span class="legend-item"><i class="legend-dot seg-orphan"></i>残留垃圾文件 ' + fmt(rep.orphansBytes) + '</span>' +
        '</div>' +
      '</div>' +

      '<div class="action-grid">' +
        '<div class="card"' + (!hasOrphans && !hasLegacy ? ' style="opacity:0.82"' : '') + '>' +
          '<div class="between" style="margin-bottom:8px"><strong>清理残留垃圾文件</strong>' + orphanBadgeHtml + '</div>' +
          orphanCardBodyHtml +
          '<div class="row gap-sm wrap">' +
            (hasLegacy ? '<label class="row gap-xs" style="font-size:12px;cursor:pointer"><input type="checkbox" id="chkPruneLegacyCli"' + (st.storagePruneLegacyCli ? ' checked' : '') + ' /> 先生成清理前安全备份并清理旧版 CLI 历史库</label>' : '') +
            '<button class="' + orphanBtnClass + '" id="btnRunCompleteOrphanGc" type="button"' + (!canCleanOrphan ? ' disabled' : '') + '>' + orphanBtnLabel + '</button>' +
          '</div>' +
        '</div>' +

        '<div class="card">' +
          '<div class="between" style="margin-bottom:8px"><strong>安全清理陈旧后台子任务</strong><span class="badge badge-green">推荐 · 先备份后清理</span></div>' +
          '<div class="muted" style="font-size:12px;line-height:1.7;margin-bottom:10px">' +
            '自动先将选中的超龄空闲后台子任务完整打包为 <strong>清理前安全备份 (.agy)</strong> 并校验可读，再释放本机空间（完全保留您的主对话与产物目录）。' +
          '</div>' +
          '<button class="btn btn-primary" id="btnRunMode5A" type="button">备份并清理已选后台子任务 (0)</button>' +
        '</div>' +
      '</div>' +

      '<div class="card" style="margin-bottom:16px">' +
        '<div class="between wrap gap-sm" style="margin-bottom:10px">' +
          '<strong>大体积对话占用排行</strong>' +
          '<div class="row gap-sm"><button class="btn btn-xs" id="btnStoSelectEligible" type="button">全选可安全清理项</button><button class="btn btn-xs" id="btnStoClearEligible" type="button">清空勾选</button></div>' +
        '</div>' +
        '<div style="max-height:340px;overflow:auto;border:1px solid var(--border);border-radius:8px">' +
          '<table class="data-table"><thead><tr><th></th><th>对话与项目</th><th>占用体积</th><th>冗余元数据占比</th><th>静置天数</th><th>清理建议</th><th>运行状态</th></tr></thead>' +
            '<tbody id="stoOffendersTbody">' + renderOffenderRowsHtml(rep.topOffenders || []) + '</tbody>' +
          '</table>' +
        '</div>' +
      '</div>' +

      '<div class="card" style="margin-bottom:16px">' +
        '<div class="between wrap gap-sm" style="margin-bottom:8px">' +
          '<strong>清理前安全备份 (.agy) 历史 (' + coldArchives.length + ')</strong>' +
          '<span class="muted" style="font-size:12px">所有清理前自动留存的安全备份均可直接查看并一键还原</span>' +
        '</div>' +
        coldArchiveRowsHtml +
      '</div>' +

      '<details class="experimental-details" id="detailsMode5C">' +
        '<summary class="experimental-summary">' +
          '<span>▸ 高级选项（实验性） — 数据库原位压缩</span>' +
          '<span class="badge badge-amber">高级 · 默认折叠</span>' +
        '</summary>' +
        '<div class="experimental-body">' +
          '<div class="alert-banner" style="margin-bottom:12px">' +
            '<strong>⚠ 适用说明：</strong>原位压缩会直接删除所选空闲后台子任务数据库内的执行元数据并整理空间。日常建议直接使用上方「安全清理陈旧后台子任务」即可。' +
          '</div>' +
          '<div class="between wrap gap-sm">' +
            '<div class="muted" style="font-size:12px">作用对象：上方列表已勾选的空闲后台子任务（主对话与进行中对话强制跳过）。</div>' +
            '<button class="btn btn-danger-outline" id="btnRunMode5C" type="button">执行高级原位压缩 (0)</button>' +
          '</div>' +
        '</div>' +
      '</details>';

    el('numStoAge').addEventListener('input', syncStorageThresholdDirtyNotice);
    el('numStoAge').addEventListener('change', syncStorageThresholdDirtyNotice);
    el('numStoSize').addEventListener('input', syncStorageThresholdDirtyNotice);
    el('numStoSize').addEventListener('change', syncStorageThresholdDirtyNotice);
    syncStorageThresholdDirtyNotice();

    el('btnReanalyzeStorage').addEventListener('click', function () {
      var btn = this;
      st.storageMinAgeDays = numOrDefault(el('numStoAge').value, 7);
      st.storageMinSizeMb = numOrDefault(el('numStoSize').value, 5);
      st.storageCheckedCids = {};
      AGY.api.withButtonBusy(btn, '扫描中...', function () { return runStorageAnalyze(); });
    });

    if (el('btnInspectLastColdArchive') && slimRes && slimRes.coldArchivePath) {
      el('btnInspectLastColdArchive').addEventListener('click', function () {
        openColdArchiveInBackupView(slimRes.coldArchivePath, this);
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

    var openColdBtns = host.querySelectorAll('.btn-open-cold-agy');
    for (var ci = 0; ci < openColdBtns.length; ci++) {
      openColdBtns[ci].addEventListener('click', function () {
        var ca = coldArchives[parseInt(this.getAttribute('data-cidx'), 10)];
        if (ca) openColdArchiveInBackupView(ca.filePath || ca.fullPath, this);
      });
    }
    var expColdBtns = host.querySelectorAll('.btn-explore-cold-agy');
    for (var ei = 0; ei < expColdBtns.length; ei++) {
      expColdBtns[ei].addEventListener('click', async function () {
        var ca = coldArchives[parseInt(this.getAttribute('data-cidx'), 10)];
        if (!ca) return;
        try {
          await AGY.api.apiFetch('/api/native/open-in-explorer', {
            method: 'POST',
            body: JSON.stringify({ path: ca.filePath || ca.fullPath })
          });
        } catch (e) { AGY.api.showToast(e.message, 'error'); }
      });
    }

    if (el('chkPruneLegacyCli')) {
      el('chkPruneLegacyCli').addEventListener('change', function () {
        st.storagePruneLegacyCli = this.checked;
        var orphanBtn = el('btnRunCompleteOrphanGc');
        if (orphanBtn) {
          var canRun = hasOrphans || (hasLegacy && !!st.storagePruneLegacyCli);
          orphanBtn.disabled = !canRun;
          orphanBtn.className = canRun ? 'btn btn-danger-outline' : 'btn';
        }
      });
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
      if (isStorageThresholdDirty()) {
        promptRescanForChangedThresholds();
        return;
      }
      var cids = Object.keys(st.storageCheckedCids).filter(function (k) { return st.storageCheckedCids[k]; });
      if (cids.length === 0) return;
      confirmMode5AColdArchiveModal(cids, rep.topOffenders || []);
    });

    el('btnRunMode5C').addEventListener('click', function () {
      if (isStorageThresholdDirty()) {
        promptRescanForChangedThresholds();
        return;
      }
      var cids = Object.keys(st.storageCheckedCids).filter(function (k) { return st.storageCheckedCids[k]; });
      if (cids.length === 0) return;
      confirmMode5CLiposuctionModal(cids);
    });

    el('btnRunCompleteOrphanGc').addEventListener('click', function () {
      if (!hasOrphans && !(hasLegacy && st.storagePruneLegacyCli)) return;
      confirmOrphanGcModal(orph, leg, !!st.storagePruneLegacyCli);
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

  async function executeStorageSlimRequest(mode, cids, cleanOrphans, pruneLegacy, closeIde, forceKill, btn) {
    AGY.api.withButtonBusy(btn, '清理中...', async function () {
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
            closeIdeIfRunning: !!closeIde,
            forceKill: !!forceKill
          })
        });
        AGY.state.lastSlimResult = res;
        AGY.state.storageCheckedCids = {};
        AGY.api.showToast('清理完成，释放 ' + res.freedMegabytes + ' MB ✓', 'success');
        if (AGY.app && AGY.app.markNonActiveDirty) AGY.app.markNonActiveDirty('storage');
        await runStorageAnalyze(false);
        AGY.api.apiFetch('/api/overview').then(function (ov) {
          AGY.state.overview = ov;
          if (AGY.app.updateShellBars) AGY.app.updateShellBars();
        }).catch(function () {});
      } catch (e) {
        if (!AGY.ui.handleIdeConflictError(
          e,
          '执行空间清理',
          function () { executeStorageSlimRequest(mode, cids, cleanOrphans, pruneLegacy, true, false, null); },
          function () { executeStorageSlimRequest(mode, cids, cleanOrphans, pruneLegacy, true, true, null); }
        )) {
          AGY.api.showToast('清理失败：' + e.message, 'error');
        }
      }
    });
  }

  AGY.views.renderStorage = renderStorage;
  AGY.views.storage = renderStorage;
  AGY.views.runStorageAnalyze = runStorageAnalyze;
})();
