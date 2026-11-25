(function () {
  'use strict';

  var AGY = window.AGY = window.AGY || {};
  AGY.views = AGY.views || {};

  function trashKindLabel(kind) {
    if (kind === 'session' || kind === 'sessions') return '对话';
    if (kind === 'project') return '项目与对话';
    if (kind === 'orphan' || kind === 'orphans') return '残留垃圾文件';
    if (kind === 'capability_local' || kind === 'capabilities_local') return '本机扩展配置';
    if (kind === 'capability_repo' || kind === 'capabilities_repo') return '配置同步库项目';
    return kind || '已删除项';
  }

  function formatUtcShort(iso) {
    if (!iso) return '-';
    try {
      var d = new Date(iso);
      if (!isNaN(d.getTime())) return d.toLocaleString();
    } catch (_e) {}
    return String(iso);
  }

  function renderTrashTabBody(td) {
    var esc = AGY.ui.escapeHtml;
    var fmt = AGY.ui.formatBytes;
    if (!td) {
      return '<div class="empty-state">正在加载回收站...</div>';
    }
    var items = td.items || [];
    if (items.length === 0) {
      return '<div class="card empty-state">' +
        '<div>回收站当前为空。</div>' +
        '<div class="muted" style="font-size:12px;margin-top:6px">所有在伴侣中删除的对话、项目记录、残留垃圾文件及扩展配置都会先暂存在此处，默认保留 30 天，随时可以一键还原。</div>' +
      '</div>';
    }

    return '<div class="between wrap gap-sm" style="margin-bottom:12px">' +
      '<div class="muted" style="font-size:12.5px">共 <strong>' + esc(items.length) + '</strong> 条可还原记录，总占空间 <strong>' + esc(td.totalMegabytes || 0) + ' MB</strong>（超过 30 天将自动清理）。</div>' +
      '<div class="row gap-xs">' +
        (td.trashRoot ? '<button class="btn btn-sm" id="btnOpenTrashFolder" type="button">打开回收站目录</button>' : '') +
        '<button class="btn btn-sm btn-danger-outline" id="btnEmptyTrashAll" type="button">清空回收站</button>' +
      '</div>' +
    '</div>' +
    items.map(function (it) {
      var bytes = it.totalBytes !== undefined ? it.totalBytes : (it.sizeBytes || 0);
      var whenStr = formatUtcShort(it.createdAtUtc || it.deletedAtIso);
      var cntText = it.itemCount ? (' · 共 ' + esc(it.itemCount) + ' 项') : '';
      return '<div class="trash-item-card">' +
        '<div class="between wrap gap-sm">' +
          '<div class="row gap-sm wrap">' +
            '<span class="badge badge-blue">' + esc(trashKindLabel(it.kind)) + '</span>' +
            '<strong>' + esc(it.summary || it.id) + '</strong>' +
            '<span class="muted mono" style="font-size:11.5px">' + esc(fmt(bytes)) + '</span>' +
          '</div>' +
          '<div class="row gap-xs">' +
            '<button class="btn btn-xs btn-primary btn-trash-restore" data-tid="' + esc(it.id) + '" type="button">一键还原</button>' +
            '<button class="btn btn-xs btn-danger-outline btn-trash-purge" data-tid="' + esc(it.id) + '" type="button">彻底删除</button>' +
          '</div>' +
        '</div>' +
        '<div class="muted" style="font-size:11.5px">删除时间：' + esc(whenStr) + cntText + '</div>' +
      '</div>';
    }).join('');
  }

  function renderConfigBackupsTabBody(cb) {
    var esc = AGY.ui.escapeHtml;
    var fmt = AGY.ui.formatBytes;
    if (!cb) {
      return '<div class="empty-state">正在扫描本机配置历史版本 (.bak)...</div>';
    }
    var items = cb.items || [];
    if (items.length === 0) {
      return '<div class="card empty-state">' +
        '<div>暂无自动生成的配置历史版本 (.bak)。</div>' +
        '<div class="muted" style="font-size:12px;margin-top:6px">每次修改全局运行配置、切换插件/MCP/钩子开关或在线编辑规则文件时，伴侣都会先留存一份 .bak 备份版本。</div>' +
      '</div>';
    }

    return '<div class="card">' +
      '<div class="between wrap gap-sm" style="margin-bottom:10px">' +
        '<strong>本机配置历史版本 (' + items.length + ')</strong>' +
        '<span class="muted" style="font-size:12px">还原前会自动为当前版本再备份一份，放心随时回退</span>' +
      '</div>' +
      '<div style="max-height:480px;overflow:auto;border:1px solid var(--border);border-radius:8px">' +
        '<table class="data-table"><thead><tr><th>配置文件</th><th>备份时间</th><th>体积</th><th>操作</th></tr></thead><tbody>' +
        items.map(function (it, idx) {
          var fname = it.originalFileName || it.targetFileName || it.backupFileName;
          var opath = it.originalPath || it.targetPath || it.backupPath;
          var whenStr = it.modifiedLocal || formatUtcShort(it.modifiedUtc || it.modifiedTime);
          return '<tr>' +
            '<td><strong>' + esc(fname) + '</strong><div class="muted mono" style="font-size:11px;word-break:break-all">' + esc(opath) + '</div>' +
              (it.preview ? '<details style="margin-top:3px"><summary class="muted" style="cursor:pointer;font-size:11px">预览前 240 字符</summary><pre class="mono" style="margin:4px 0 0 0;font-size:11px;white-space:pre-wrap;max-height:90px;overflow:auto">' + esc(it.preview) + '</pre></details>' : '') +
            '</td>' +
            '<td class="mono">' + esc(whenStr) + '</td>' +
            '<td class="mono muted">' + esc(fmt(it.sizeBytes)) + '</td>' +
            '<td style="white-space:nowrap"><button class="btn btn-xs btn-primary btn-bak-restore" data-bidx="' + idx + '" type="button">还原此版本</button></td>' +
          '</tr>';
        }).join('') +
        '</tbody></table>' +
      '</div>' +
    '</div>';
  }

  async function restoreTrashItem(trashId, closeIde, forceKill, btn) {
    AGY.api.withButtonBusy(btn, '还原中...', async function () {
      try {
        var res = await AGY.api.apiFetch('/api/trash/restore', {
          method: 'POST',
          body: JSON.stringify({ id: trashId, closeIdeIfRunning: !!closeIde, forceKill: !!forceKill })
        });
        var countNum = (res.restoredFiles || 0) + (res.restoredProtoEntries || 0) + (res.restoredDbRows || 0);
        await AGY.app.refreshAll('已成功还原 ' + countNum + ' 项内容 ✓');
      } catch (e) {
        if (!AGY.ui.handleIdeConflictError(
          e,
          '从回收站还原内容',
          function () { restoreTrashItem(trashId, true, false, null); },
          function () { restoreTrashItem(trashId, true, true, null); }
        )) {
          AGY.api.showToast('还原失败：' + e.message, 'error');
        }
      }
    });
  }

  function confirmPurgeTrashModal(trashId, purgeAll) {
    AGY.ui.openModal(
      purgeAll ? '确认清空回收站？' : '确认彻底删除此记录？',
      '<div style="color:var(--red-text);font-weight:600;margin-bottom:8px">彻底删除后将无法再恢复！</div>' +
      '<div>' + (purgeAll ? '将永久清除回收站内的全部暂存文件并释放磁盘空间。' : '将永久删除选中的回收站记录。') + '</div>',
      '<button class="btn" data-act="cancel">取消</button><button class="btn btn-danger" data-act="go">确认彻底删除</button>',
      function (_mb, mf) {
        mf.querySelector('[data-act="cancel"]').addEventListener('click', AGY.ui.closeModal);
        var goBtn = mf.querySelector('[data-act="go"]');
        goBtn.addEventListener('click', function () {
          AGY.api.withButtonBusy(goBtn, '删除中...', async function () {
            try {
              await AGY.api.apiFetch('/api/trash/purge', {
                method: 'POST',
                body: JSON.stringify(purgeAll ? { all: true } : { id: trashId })
              });
              AGY.ui.closeModal();
              await AGY.app.refreshAll(purgeAll ? '回收站已清空 ✓' : '已彻底删除 ✓');
            } catch (e) { AGY.api.showToast('删除失败：' + e.message, 'error'); }
          });
        });
      }
    );
  }

  function renderTrash() {
    var el = AGY.dom.el;
    var st = AGY.state;
    var td = st.trashData;
    var cb = st.configBackupsData;
    var trashCnt = (td && td.items && td.items.length) || 0;
    var bakCnt = (cb && cb.items && cb.items.length) || 0;
    var activeTab = st.trashTab || 'trash';

    var root = el('mainCanvas');
    root.innerHTML =
      '<div class="subtab-bar">' +
        '<div class="pill-group">' +
          '<button class="filter-pill ' + (activeTab === 'trash' ? 'active' : '') + '" data-trtab="trash" type="button">已删除内容回收站 (' + trashCnt + ')</button>' +
          '<button class="filter-pill ' + (activeTab === 'backups' ? 'active' : '') + '" data-trtab="backups" type="button">配置历史版本 (.bak) (' + bakCnt + ')</button>' +
        '</div>' +
      '</div>' +
      '<div id="trashTabHost">' +
        (activeTab === 'backups' ? renderConfigBackupsTabBody(cb) : renderTrashTabBody(td)) +
      '</div>';

    var tabBtns = root.querySelectorAll('[data-trtab]');
    for (var i = 0; i < tabBtns.length; i++) {
      tabBtns[i].addEventListener('click', function () {
        st.trashTab = this.getAttribute('data-trtab');
        renderTrash();
      });
    }

    if (activeTab === 'trash') {
      if (el('btnOpenTrashFolder') && td && td.trashRoot) {
        el('btnOpenTrashFolder').addEventListener('click', async function () {
          try {
            await AGY.api.apiFetch('/api/native/open-in-explorer', {
              method: 'POST',
              body: JSON.stringify({ path: td.trashRoot })
            });
          } catch (e) { AGY.api.showToast(e.message, 'error'); }
        });
      }
      if (el('btnEmptyTrashAll')) {
        el('btnEmptyTrashAll').addEventListener('click', function () { confirmPurgeTrashModal('', true); });
      }
      var resBtns = root.querySelectorAll('.btn-trash-restore');
      for (var r = 0; r < resBtns.length; r++) {
        resBtns[r].addEventListener('click', function () {
          restoreTrashItem(this.getAttribute('data-tid'), false, false, this);
        });
      }
      var purgeBtns = root.querySelectorAll('.btn-trash-purge');
      for (var p = 0; p < purgeBtns.length; p++) {
        purgeBtns[p].addEventListener('click', function () {
          confirmPurgeTrashModal(this.getAttribute('data-tid'), false);
        });
      }
    } else {
      var bakBtns = root.querySelectorAll('.btn-bak-restore');
      var bakList = (cb && cb.items) || [];
      for (var b = 0; b < bakBtns.length; b++) {
        bakBtns[b].addEventListener('click', function () {
          var btn = this;
          var item = bakList[parseInt(btn.getAttribute('data-bidx'), 10)];
          if (!item) return;
          AGY.api.withButtonBusy(btn, '还原中...', async function () {
            try {
              await AGY.api.apiFetch('/api/config-backups/restore', {
                method: 'POST',
                body: JSON.stringify({ backupPath: item.backupPath })
              });
              await AGY.app.refreshAll('已还原 ' + (item.originalFileName || item.targetFileName || '配置文件') + ' 到所选历史版本 ✓');
            } catch (e) { AGY.api.showToast('还原失败：' + e.message, 'error'); }
          });
        });
      }
    }
  }

  AGY.views.trash = renderTrash;
})();
