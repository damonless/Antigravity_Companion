(function () {
  'use strict';

  var AGY = window.AGY = window.AGY || {};

  var ICONS = {
    home: '<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.9" stroke-linecap="round" stroke-linejoin="round"><path d="M3 9l9-7 9 7v11a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z"/><polyline points="9 22 9 12 15 12 15 22"/></svg>',
    sliders: '<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.9" stroke-linecap="round" stroke-linejoin="round"><line x1="4" y1="21" x2="4" y2="14"/><line x1="4" y1="10" x2="4" y2="3"/><line x1="12" y1="21" x2="12" y2="12"/><line x1="12" y1="8" x2="12" y2="3"/><line x1="20" y1="21" x2="20" y2="16"/><line x1="20" y1="12" x2="20" y2="3"/><line x1="1" y1="14" x2="7" y2="14"/><line x1="9" y1="8" x2="15" y2="8"/><line x1="17" y1="16" x2="23" y2="16"/></svg>',
    message: '<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.9" stroke-linecap="round" stroke-linejoin="round"><path d="M21 15a2 2 0 0 1-2 2H7l-4 4V5a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2z"/></svg>',
    harddrive: '<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.9" stroke-linecap="round" stroke-linejoin="round"><line x1="22" y1="12" x2="2" y2="12"/><path d="M5.45 5.11L2 12v6a2 2 0 0 0 2 2h16a2 2 0 0 0 2-2v-6l-3.45-6.89A2 2 0 0 0 16.76 4H7.24a2 2 0 0 0-1.79 1.11z"/><line x1="6" y1="16" x2="6.01" y2="16"/><line x1="10" y1="16" x2="10.01" y2="16"/></svg>',
    sparkles: '<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.9" stroke-linecap="round" stroke-linejoin="round"><path d="M12 3l1.9 5.8L20 11l-6.1 2.2L12 19l-1.9-5.8L4 11l6.1-2.2z"/></svg>',
    layers: '<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.9" stroke-linecap="round" stroke-linejoin="round"><polygon points="12 2 2 7 12 12 22 7 12 2"/><polyline points="2 17 12 22 22 17"/><polyline points="2 12 12 17 22 12"/></svg>',
    archive: '<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.9" stroke-linecap="round" stroke-linejoin="round"><polyline points="21 8 21 21 3 21 3 8"/><rect x="1" y="3" width="22" height="5"/><line x1="10" y1="12" x2="14" y2="12"/></svg>',
    trash: '<svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.9" stroke-linecap="round" stroke-linejoin="round"><polyline points="3 6 5 6 21 6"/><path d="M19 6l-1 14a2 2 0 0 1-2 2H8a2 2 0 0 1-2-2L5 6"/><path d="M10 11v6"/><path d="M14 11v6"/><path d="M9 6V4a1 1 0 0 1 1-1h4a1 1 0 0 1 1 1v2"/></svg>',
    more: '<svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><circle cx="12" cy="12" r="1"/><circle cx="19" cy="12" r="1"/><circle cx="5" cy="12" r="1"/></svg>',
    history: '<svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><polyline points="1 4 1 10 7 10"/><path d="M3.51 15a9 9 0 1 0 2.13-9.36L1 10"/></svg>',
    shield: '<svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z"/></svg>',
    moon: '<svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.9" stroke-linecap="round" stroke-linejoin="round"><path d="M21 12.79A9 9 0 1 1 11.21 3 7 7 0 0 0 21 12.79z"/></svg>',
    refresh: '<svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.9" stroke-linecap="round" stroke-linejoin="round"><polyline points="23 4 23 10 17 10"/><path d="M20.49 15a9 9 0 1 1-2.12-9.36L23 10"/></svg>',
    search: '<svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><circle cx="11" cy="11" r="8"/><line x1="21" y1="21" x2="16.65" y2="16.65"/></svg>',
    eye: '<svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z"/><circle cx="12" cy="12" r="3"/></svg>',
    edit: '<svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M12 20h9"/><path d="M16.5 3.5a2.121 2.121 0 0 1 3 3L7 19l-4 1 1-4L16.5 3.5z"/></svg>',
    folder: '<svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M22 19a2 2 0 0 1-2 2H4a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h5l2 3h9a2 2 0 0 1 2 2z"/></svg>',
    download: '<svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"/><polyline points="7 10 12 15 17 10"/><line x1="12" y1="15" x2="12" y2="3"/></svg>',
    settings: '<svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.9" stroke-linecap="round" stroke-linejoin="round"><circle cx="12" cy="12" r="3"/><path d="M19.4 15a1.65 1.65 0 0 0 .33 1.82l.06.06a2 2 0 1 1-2.83 2.83l-.06-.06a1.65 1.65 0 0 0-1.82-.33 1.65 1.65 0 0 0-1 1.51V21a2 2 0 1 1-4 0v-.09A1.65 1.65 0 0 0 9 19.4a1.65 1.65 0 0 0-1.82.33l-.06.06a2 2 0 1 1-2.83-2.83l.06-.06a1.65 1.65 0 0 0 .33-1.82 1.65 1.65 0 0 0-1.51-1H3a2 2 0 1 1 0-4h.09A1.65 1.65 0 0 0 4.6 9a1.65 1.65 0 0 0-.33-1.82l-.06-.06a2 2 0 1 1 2.83-2.83l.06.06a1.65 1.65 0 0 0 1.82.33H9a1.65 1.65 0 0 0 1-1.51V3a2 2 0 1 1 4 0v.09a1.65 1.65 0 0 0 1 1.51 1.65 1.65 0 0 0 1.82-.33l.06-.06a2 2 0 1 1 2.83 2.83l-.06.06a1.65 1.65 0 0 0-.33 1.82V9a1.65 1.65 0 0 0 1.51 1H21a2 2 0 1 1 0 4h-.09a1.65 1.65 0 0 0-1.51 1z"/></svg>'
  };

  function kindLabel(kind) {
    var k = String(kind || '');
    if (k === 'Plugin' || k === 'plugin') return '插件';
    if (k === 'Skill' || k === 'skill') return '技能';
    if (k === 'Rule' || k === 'rule') return '规则';
    if (k === 'MCP' || k === 'mcp') return 'MCP';
    if (k === 'Hook' || k === 'hook') return '钩子';
    return k;
  }

  function escapeHtml(str) {
    if (str === null || str === undefined) return '';
    return String(str)
      .replace(/&/g, '&amp;')
      .replace(/</g, '&lt;')
      .replace(/>/g, '&gt;')
      .replace(/"/g, '&quot;')
      .replace(/'/g, '&#39;');
  }

  function sanitizeUrl(url) {
    var s = String(url || '').trim();
    if (/^(https?:\/\/|#|\/)/i.test(s)) return s;
    return '#';
  }

  function formatBytes(bytes) {
    var n = Number(bytes) || 0;
    if (n < 1024) return n + ' B';
    if (n < 1024 * 1024) return (n / 1024).toFixed(1) + ' KB';
    if (n < 1024 * 1024 * 1024) return (n / (1024 * 1024)).toFixed(2) + ' MB';
    return (n / (1024 * 1024 * 1024)).toFixed(2) + ' GB';
  }

  function estimateTokens(text) {
    if (!text) return 0;
    var utf8Bytes;
    try {
      utf8Bytes = new TextEncoder().encode(String(text)).length;
    } catch (_e) {
      utf8Bytes = String(text).length * 2;
    }
    return Math.ceil(utf8Bytes / 3.5);
  }

  function renderMarkdownSafe(mdText) {
    var src = String(mdText || '').replace(/\r\n/g, '\n').replace(/\r/g, '\n');
    var lines = src.split('\n');
    var html = [];
    var inPre = false;
    var preLines = [];
    var preLang = '';
    var inList = false;

    function flushList() {
      if (inList) { html.push('</ul>'); inList = false; }
    }

    function applyInline(raw) {
      var escaped = escapeHtml(raw);
      escaped = escaped.replace(/`([^`]+)`/g, '<code>$1</code>');
      escaped = escaped.replace(/\*\*([^*]+)\*\*/g, '<strong>$1</strong>');
      escaped = escaped.replace(/\[([^\]]+)\]\(([^)]+)\)/g, function (_m, label, url) {
        return '<a href="' + escapeHtml(sanitizeUrl(url)) + '" target="_blank" rel="noopener">' + label + '</a>';
      });
      return escaped;
    }

    for (var i = 0; i < lines.length; i++) {
      var line = lines[i];
      if (/^```/.test(line.trim())) {
        if (!inPre) {
          flushList();
          inPre = true;
          preLang = line.trim().slice(3).trim();
          preLines = [];
        } else {
          html.push('<pre>' + (preLang ? '<div class="muted" style="font-size:11px;margin-bottom:4px">' + escapeHtml(preLang) + '</div>' : '') + '<code>' + escapeHtml(preLines.join('\n')) + '</code></pre>');
          inPre = false;
          preLang = '';
        }
        continue;
      }
      if (inPre) {
        preLines.push(line);
        continue;
      }
      if (/^---+$/.test(line.trim())) {
        flushList();
        html.push('<hr style="border:none;border-top:1px solid var(--border);margin:12px 0">');
      } else if (/^###\s+/.test(line)) {
        flushList();
        html.push('<h3>' + applyInline(line.replace(/^###\s+/, '')) + '</h3>');
      } else if (/^##\s+/.test(line)) {
        flushList();
        html.push('<h2>' + applyInline(line.replace(/^##\s+/, '')) + '</h2>');
      } else if (/^#\s+/.test(line)) {
        flushList();
        html.push('<h1>' + applyInline(line.replace(/^#\s+/, '')) + '</h1>');
      } else if (/^>\s?/.test(line)) {
        flushList();
        html.push('<blockquote>' + applyInline(line.replace(/^>\s?/, '')) + '</blockquote>');
      } else if (/^[-*+]\s+/.test(line)) {
        if (!inList) { html.push('<ul>'); inList = true; }
        html.push('<li>' + applyInline(line.replace(/^[-*+]\s+/, '')) + '</li>');
      } else if (!line.trim()) {
        flushList();
        html.push('<div style="height:6px"></div>');
      } else {
        flushList();
        html.push('<div>' + applyInline(line) + '</div>');
      }
    }
    if (inPre) html.push('<pre><code>' + escapeHtml(preLines.join('\n')) + '</code></pre>');
    flushList();
    return '<div class="md-view">' + html.join('\n') + '</div>';
  }

  function highlightTextNodes(container, query) {
    if (!container) return 0;
    var existingMarks = container.querySelectorAll('mark.md-highlight');
    for (var m = 0; m < existingMarks.length; m++) {
      var mk = existingMarks[m];
      var txt = document.createTextNode(mk.textContent || '');
      if (mk.parentNode) mk.parentNode.replaceChild(txt, mk);
    }
    container.normalize();

    var q = String(query || '').trim();
    if (!q) return 0;

    var qLower = q.toLowerCase();
    var qLen = q.length;
    var textNodes = [];
    var walker = document.createTreeWalker(container, NodeFilter.SHOW_TEXT, {
      acceptNode: function (node) {
        if (!node.parentNode) return NodeFilter.FILTER_REJECT;
        var tag = node.parentNode.nodeName;
        if (tag === 'SCRIPT' || tag === 'STYLE' || tag === 'MARK') return NodeFilter.FILTER_REJECT;
        return node.nodeValue && node.nodeValue.length > 0 ? NodeFilter.FILTER_ACCEPT : NodeFilter.FILTER_REJECT;
      }
    });
    while (walker.nextNode()) {
      textNodes.push(walker.currentNode);
    }

    var hits = 0;
    for (var i = 0; i < textNodes.length; i++) {
      var tNode = textNodes[i];
      var val = tNode.nodeValue;
      var lower = val.toLowerCase();
      if (lower.indexOf(qLower) < 0) continue;

      var frag = document.createDocumentFragment();
      var cursor = 0;
      while (cursor < val.length) {
        var idx = lower.indexOf(qLower, cursor);
        if (idx < 0) {
          frag.appendChild(document.createTextNode(val.slice(cursor)));
          break;
        }
        if (idx > cursor) {
          frag.appendChild(document.createTextNode(val.slice(cursor, idx)));
        }
        var mark = document.createElement('mark');
        mark.className = 'md-highlight';
        mark.textContent = val.slice(idx, idx + qLen);
        frag.appendChild(mark);
        hits++;
        cursor = idx + qLen;
      }
      if (tNode.parentNode) tNode.parentNode.replaceChild(frag, tNode);
    }
    return hits;
  }

  function closeAllDropdowns() {
    var menus = document.querySelectorAll('.dropdown-menu');
    for (var i = 0; i < menus.length; i++) {
      menus[i].classList.add('hidden');
    }
  }

  document.addEventListener('click', function () {
    closeAllDropdowns();
  });

  function openModal(title, bodyHtml, footerHtml, onBind) {
    var el = AGY.dom.el;
    el('modalTitle').textContent = title;
    el('modalBody').innerHTML = bodyHtml;
    el('modalFooter').innerHTML = footerHtml || '';
    el('modalBackdrop').classList.remove('hidden');
    if (onBind) onBind(el('modalBody'), el('modalFooter'));
  }

  function closeModal() {
    AGY.dom.el('modalBackdrop').classList.add('hidden');
  }

  function openDrawer(title, subtitle, bodyHtml, headerActionsHtml, onBind, dirtyGuard) {
    var el = AGY.dom.el;
    AGY.state.drawerDirtyCheck = dirtyGuard || null;
    el('drawerTitle').textContent = title;
    el('drawerSubtitle').textContent = subtitle || '';
    el('drawerHeaderActions').innerHTML = headerActionsHtml || '';
    el('drawerBody').innerHTML = bodyHtml;
    el('drawerBackdrop').classList.remove('hidden');
    if (onBind) onBind(el('drawerBody'), el('drawerHeaderActions'));
  }

  function closeDrawer(force) {
    if (!force && AGY.state.drawerDirtyCheck && AGY.state.drawerDirtyCheck()) {
      openModal(
        '放弃尚未保存的修改？',
        '<div>当前编辑区还有未保存的内容，直接关闭将会丢失这些改动。</div>',
        '<button class="btn" data-act="cancel-close">继续编辑</button><button class="btn btn-danger" data-act="confirm-discard">放弃并关闭</button>',
        function (_mb, mf) {
          mf.querySelector('[data-act="cancel-close"]').addEventListener('click', closeModal);
          mf.querySelector('[data-act="confirm-discard"]').addEventListener('click', function () {
            closeModal();
            closeDrawer(true);
          });
        }
      );
      return;
    }
    AGY.state.drawerDirtyCheck = null;
    AGY.dom.el('drawerBackdrop').classList.add('hidden');
  }

  function confirmCloseIdeModal(parentCount, lsCount, actionDesc, onProceed) {
    var total = (Number(parentCount) || 0) + (Number(lsCount) || 0);
    openModal(
      '需要先关闭 Antigravity 主窗口',
      '<div style="margin-bottom:10px">准备执行 <strong>' + escapeHtml(actionDesc) + '</strong>。当前检测到 Antigravity 主窗口正在运行（共 <strong>' +
        escapeHtml(total || 1) + '</strong> 个进程占用本地数据）。</div>' +
      '<div class="muted">为避免写入冲突或文件损坏，是否先自动关闭 Antigravity 主窗口再继续？</div>',
      '<button class="btn" data-act="cancel">先不操作</button><button class="btn btn-primary" data-act="close-ide">关闭主窗口并继续</button>',
      function (_b, mf) {
        mf.querySelector('[data-act="cancel"]').addEventListener('click', closeModal);
        mf.querySelector('[data-act="close-ide"]').addEventListener('click', function () {
          closeModal();
          if (onProceed) onProceed();
        });
      }
    );
  }

  function confirmForceKillIdeModal(parentCount, lsCount, actionDesc, onForceKill) {
    var total = (Number(parentCount) || 0) + (Number(lsCount) || 0);
    openModal(
      'Antigravity 主窗口未能正常退出',
      '<div class="alert-banner danger" style="margin-bottom:10px"><strong>优雅关闭超时：</strong>仍有 <strong>' +
        escapeHtml(total || 1) + '</strong> 个 Antigravity 主窗口或后台服务进程未响应退出指令（可能存在未保存的编辑器弹窗）。</div>' +
      '<div style="line-height:1.65">您可以先切回 Antigravity 主窗口手动保存并关闭；或者点击下方按钮<strong>强制结束残留进程</strong>后继续执行 <strong>' +
        escapeHtml(actionDesc) + '</strong>。</div>',
      '<button class="btn" data-act="cancel">我自己去关闭</button><button class="btn btn-danger" data-act="force-kill">强制结束进程并继续</button>',
      function (_b, mf) {
        mf.querySelector('[data-act="cancel"]').addEventListener('click', closeModal);
        mf.querySelector('[data-act="force-kill"]').addEventListener('click', function () {
          closeModal();
          if (onForceKill) onForceKill();
        });
      }
    );
  }

  function handleIdeConflictError(err, actionDesc, onCloseIde, onForceKill) {
    if (!err || err.status !== 409) return false;
    var data = err.data || {};
    var code = data.code || '';
    var ide = (AGY.state && AGY.state.overview && AGY.state.overview.ide) || {};
    var pCnt = data.parentCount !== undefined ? data.parentCount : (ide.parentCount || 1);
    var lsCnt = data.lsCount !== undefined ? data.lsCount : (ide.lsCount || 0);

    if (code === 'IDE_CLOSE_TIMEOUT' || data.canForceKill) {
      if (typeof onForceKill === 'function') {
        confirmForceKillIdeModal(pCnt, lsCnt, actionDesc, onForceKill);
        return true;
      }
    }
    if (code === 'IDE_RUNNING' || (err.message && err.message.indexOf('IDE') >= 0)) {
      if (typeof onCloseIde === 'function') {
        confirmCloseIdeModal(pCnt, lsCnt, actionDesc, onCloseIde);
        return true;
      }
    }
    return false;
  }

  function openDualModeRenameModal(parentCount, lsCount, onConfirm) {
    var chosen = 'SmartAuto';
    var total = (Number(parentCount) || 0) + (Number(lsCount) || 0);
    openModal(
      'Antigravity 主窗口正在运行',
      '<div>检测到 Antigravity 主窗口正在运行（' + escapeHtml(total || 1) + ' 个进程），请选择改名方式：</div>' +
      '<div class="strategy-cards">' +
        '<div class="strategy-card selected" data-strat="SmartAuto">' +
          '<strong>1. 不打断主窗口，退出后自动补齐（推荐）</strong>' +
          '<div class="muted" style="margin-top:4px;font-size:12px">立即写入本地记录（自动跳过进行中的对话），主窗口关闭后伴侣会自动补刷侧边栏显示。</div>' +
        '</div>' +
        '<div class="strategy-card" data-strat="CloseAndFullWrite">' +
          '<strong>2. 立即关闭主窗口并完整写入</strong>' +
          '<div class="muted" style="margin-top:4px;font-size:12px">先关闭 Antigravity 主窗口再同步全部索引，下次打开主窗口立刻显示新标题。</div>' +
        '</div>' +
      '</div>',
      '<button class="btn" data-act="cancel">取消</button><button class="btn btn-primary" data-act="go">开始改名</button>',
      function (mb, mf) {
        var cards = mb.querySelectorAll('.strategy-card');
        for (var i = 0; i < cards.length; i++) {
          cards[i].addEventListener('click', function () {
            chosen = this.getAttribute('data-strat');
            for (var j = 0; j < cards.length; j++) cards[j].classList.remove('selected');
            this.classList.add('selected');
          });
        }
        mf.querySelector('[data-act="cancel"]').addEventListener('click', closeModal);
        mf.querySelector('[data-act="go"]').addEventListener('click', function () {
          closeModal();
          onConfirm(chosen);
        });
      }
    );
  }

  async function openMarkdownDrawer(cid, source) {
    try {
      var data = await AGY.api.apiFetch('/api/sessions/markdown?cid=' + encodeURIComponent(cid) + '&source=' + encodeURIComponent(source || 'local'));
      var sub = (data.projectName || '未关联项目') + '  ·  ' + data.stepCount + ' 步  ·  ' + data.updatedTime + (data.isCli ? '  ·  CLI 对话' : '');
      var hdrActions =
        '<button class="btn btn-sm" id="btnDrawerCopyMd">复制全文</button>' +
        '<button class="btn btn-sm btn-primary" id="btnDrawerExportMd">导出为 .md</button>';
      var body =
        '<div class="between gap-sm" style="margin-bottom:12px">' +
          '<div class="row gap-sm" style="flex:1">' +
            '<input class="input" id="txtMdFind" placeholder="在当前对话中查找关键词高亮..." style="max-width:340px" />' +
            '<span class="muted" id="lblMdFindHits" style="font-size:11.5px"></span>' +
          '</div>' +
          (data.truncated ? '<span class="badge badge-amber">长对话预览已截断至 260KB，导出 .md 可得全文</span>' : '') +
        '</div>' +
        '<div id="mdRenderedHost">' + renderMarkdownSafe(data.markdown) + '</div>' +
        '<div class="accordion" style="margin-top:16px">' +
          '<button class="accordion-trigger" id="btnMdTechToggle">▸ 技术信息</button>' +
          '<div class="accordion-body hidden mono" id="mdTechBody">对话 ID: ' + escapeHtml(data.cid) + '<br>来源: ' + (source === 'backup' ? '备份文件 (.agy) 免导入直读' : '本机') + '</div>' +
        '</div>';

      openDrawer(data.title || '对话预览', sub, body, hdrActions, function (db, ha) {
        ha.querySelector('#btnDrawerCopyMd').addEventListener('click', function () {
          AGY.api.copyToClipboard(data.markdown || '', '对话全文已复制 ✓');
        });
        ha.querySelector('#btnDrawerExportMd').addEventListener('click', function () {
          var btn = this;
          AGY.api.withButtonBusy(btn, '导出中...', async function () {
            try {
              var res = await AGY.api.apiFetch('/api/sessions/export-md', {
                method: 'POST',
                body: JSON.stringify({ cid: cid, source: source || 'local' })
              });
              if (!res.cancelled) AGY.api.showToast('已导出 Markdown 至 ' + res.outputPath, 'success');
            } catch (e) { AGY.api.showToast('导出失败：' + e.message, 'error'); }
          });
        });

        var host = db.querySelector('#mdRenderedHost');
        var hitsLbl = db.querySelector('#lblMdFindHits');
        db.querySelector('#txtMdFind').addEventListener('input', function () {
          var q = this.value;
          var cnt = highlightTextNodes(host, q);
          hitsLbl.textContent = q.trim() ? ('命中 ' + cnt + ' 处') : '';
        });

        db.querySelector('#btnMdTechToggle').addEventListener('click', function () {
          var tb = db.querySelector('#mdTechBody');
          tb.classList.toggle('hidden');
          this.textContent = tb.classList.contains('hidden') ? '▸ 技术信息' : '▾ 技术信息';
        });
      });
    } catch (e) {
      AGY.api.showToast('预览对话失败：' + e.message, 'error');
    }
  }

  async function openConfigVersionHistoryModal(filePath, onRestored) {
    try {
      var res = await AGY.api.apiFetch('/api/config-backups/list?path=' + encodeURIComponent(filePath));
      var items = res.items || [];
      var listHtml = items.length === 0
        ? '<div class="empty-state" style="padding:20px">该配置文件暂无自动保存的历史版本 (.bak)。每次在线保存或开关切换都会自动生成可还原的备份版本。</div>'
        : '<div style="max-height:320px;overflow:auto;border:1px solid var(--border);border-radius:8px">' +
            '<table class="data-table"><thead><tr><th>备份时间</th><th>体积</th><th>操作</th></tr></thead><tbody>' +
            items.map(function (it, idx) {
              return '<tr>' +
                '<td class="mono">' + escapeHtml(it.modifiedLocal || it.modifiedUtc || it.backupFileName) + '</td>' +
                '<td class="mono muted">' + escapeHtml(formatBytes(it.sizeBytes)) + '</td>' +
                '<td><button class="btn btn-xs btn-primary btn-restore-bak-one" data-bidx="' + idx + '" type="button">还原此版本</button></td>' +
              '</tr>';
            }).join('') +
            '</tbody></table></div>';

      openModal(
        '历史版本 (.bak) — ' + (filePath.split(/[\\/]/).pop() || filePath),
        '<div class="muted mono" style="font-size:11.5px;margin-bottom:10px;word-break:break-all">' + escapeHtml(filePath) + '</div>' +
        listHtml +
        '<div class="muted" style="font-size:11.5px;margin-top:8px">还原前会自动为当前内容再生成一份新备份，随时可以再切回。</div>',
        '<button class="btn" data-act="close">关闭</button>',
        function (mb, mf) {
          mf.querySelector('[data-act="close"]').addEventListener('click', closeModal);
          var btns = mb.querySelectorAll('.btn-restore-bak-one');
          for (var i = 0; i < btns.length; i++) {
            btns[i].addEventListener('click', function () {
              var btn = this;
              var item = items[parseInt(btn.getAttribute('data-bidx'), 10)];
              AGY.api.withButtonBusy(btn, '还原中...', async function () {
                try {
                  await AGY.api.apiFetch('/api/config-backups/restore', {
                    method: 'POST',
                    body: JSON.stringify({ backupPath: item.backupPath })
                  });
                  closeModal();
                  AGY.api.showToast('已还原历史版本 ✓', 'success');
                  if (onRestored) await onRestored();
                } catch (e) {
                  AGY.api.showToast('还原失败：' + e.message, 'error');
                }
              });
            });
          }
        }
      );
    } catch (e) {
      AGY.api.showToast('读取历史版本失败：' + e.message, 'error');
    }
  }

  async function openInlineEditorDrawer(filePath, titleHint, onSaved) {
    try {
      var data = await AGY.api.apiFetch('/api/editor/file?path=' + encodeURIComponent(filePath));
      var initialContent = data.content || '';
      var currentContent = initialContent;
      var dirty = false;
      var isJson = /\.(json|jsonc)$/i.test(data.path || filePath);

      function countUtf8Bytes(text) {
        if (!text) return 0;
        try { return new TextEncoder().encode(String(text)).length; }
        catch (_e) { return String(text).length * 2; }
      }

      function countTextLines(text) {
        if (!text) return 0;
        return String(text).replace(/\r\n/g, '\n').replace(/\r/g, '\n').split('\n').length;
      }

      function getRefThreshold() {
        var cfg = AGY.state.companionSettings || {};
        var t = Number(cfg.ruleTokenReferenceThreshold || 0);
        return (!isNaN(t) && isFinite(t) && t > 0) ? t : 0;
      }

      function renderBudgetBanner(tokens, bytes, chars, lines) {
        var threshold = getRefThreshold();
        if (threshold <= 0) {
          return {
            hasThreshold: false,
            pct: 0,
            cls: '',
            tokenText: '参考估算约 ' + tokens + ' tokens',
            metricsText: formatBytes(bytes) + ' (' + bytes + ' B) · ' + chars + ' 字符 · ' + lines + ' 行',
            badgeHtml: '<span class="badge badge-neutral">体量统计</span>'
          };
        }
        var pct = Math.min(100, Math.round((tokens / threshold) * 100));
        var cls = tokens > threshold ? 'over' : (tokens > threshold * 0.75 ? 'warn' : '');
        var badge = tokens > threshold
          ? '<span class="badge badge-red">⚠ 超过参考阈值 (' + threshold + ')</span>'
          : (tokens > threshold * 0.75
            ? '<span class="badge badge-amber">接近参考阈值 (' + threshold + ')</span>'
            : '<span class="badge badge-green">在参考阈值内</span>');
        return {
          hasThreshold: true,
          pct: pct,
          cls: cls,
          tokenText: '参考约 ' + tokens + ' / ' + threshold + ' tokens',
          metricsText: formatBytes(bytes) + ' (' + bytes + ' B) · ' + chars + ' 字符 · ' + lines + ' 行',
          badgeHtml: badge
        };
      }

      var initTokens = data.estimatedTokens || estimateTokens(initialContent);
      var initBytes = countUtf8Bytes(initialContent);
      var initChars = data.charCount !== undefined ? data.charCount : initialContent.length;
      var initLines = countTextLines(initialContent);
      var bInit = renderBudgetBanner(initTokens, initBytes, initChars, initLines);
      var hdrActions =
        '<button class="btn btn-sm" id="btnEditorHistory" title="查看并还原该文件的 .bak 历史版本">历史版本</button>' +
        '<button class="btn btn-sm" id="btnEditorCopy">复制内容</button>' +
        '<button class="btn btn-sm btn-primary" id="btnEditorSave">保存（自动备份）</button>';

      var body =
        '<div class="card" style="margin-bottom:12px">' +
          '<div class="between wrap gap-sm" style="margin-bottom:6px">' +
            '<div class="mono muted" style="font-size:11.5px;word-break:break-all">' + escapeHtml(data.path) + '</div>' +
            '<div id="editorBudgetBadge">' + bInit.badgeHtml + '</div>' +
          '</div>' +
          '<div class="token-meter-wrap">' +
            '<div class="between wrap gap-sm" style="font-size:12px">' +
              '<span id="lblEditorTokenCount">' + escapeHtml(bInit.tokenText) + '</span>' +
              '<span class="muted mono" id="lblEditorCharCount">' + escapeHtml(bInit.metricsText) + '</span>' +
            '</div>' +
            '<div class="token-meter-track' + (bInit.hasThreshold ? '' : ' hidden') + '" id="editorBudgetTrack"><div class="token-meter-fill ' + bInit.cls + '" id="editorBudgetFill" style="width:' + bInit.pct + '%"></div></div>' +
          '</div>' +
        '</div>' +
        '<div id="editorSyntaxAlert" class="alert-banner danger hidden"></div>' +
        '<textarea class="code-editor-area" id="txtInlineEditor">' + escapeHtml(initialContent) + '</textarea>';

      openDrawer(titleHint || '在线配置编辑器', '每次保存自动生成历史版本 (.bak)，随时可还原', body, hdrActions, function (db, ha) {
        var ta = db.querySelector('#txtInlineEditor');
        var lblTok = db.querySelector('#lblEditorTokenCount');
        var lblChr = db.querySelector('#lblEditorCharCount');
        var track = db.querySelector('#editorBudgetTrack');
        var fill = db.querySelector('#editorBudgetFill');
        var badgeHost = db.querySelector('#editorBudgetBadge');
        var synAlert = db.querySelector('#editorSyntaxAlert');

        function updateMeters(text) {
          var toks = estimateTokens(text);
          var bts = countUtf8Bytes(text);
          var chs = text.length;
          var lns = countTextLines(text);
          var b = renderBudgetBanner(toks, bts, chs, lns);
          lblTok.textContent = b.tokenText;
          lblChr.textContent = b.metricsText;
          if (track) track.classList.toggle('hidden', !b.hasThreshold);
          fill.style.width = b.pct + '%';
          fill.className = 'token-meter-fill ' + b.cls;
          badgeHost.innerHTML = b.badgeHtml;
        }

        ta.addEventListener('input', function () {
          currentContent = this.value;
          dirty = currentContent !== initialContent;
          updateMeters(currentContent);
          synAlert.classList.add('hidden');
        });

        ha.querySelector('#btnEditorHistory').addEventListener('click', function () {
          openConfigVersionHistoryModal(data.path, async function () {
            var fresh = await AGY.api.apiFetch('/api/editor/file?path=' + encodeURIComponent(data.path));
            initialContent = fresh.content || '';
            currentContent = initialContent;
            dirty = false;
            ta.value = initialContent;
            updateMeters(initialContent);
            if (AGY.app && AGY.app.refreshAll) await AGY.app.refreshAll();
          });
        });

        ha.querySelector('#btnEditorCopy').addEventListener('click', function () {
          AGY.api.copyToClipboard(ta.value || '', '文件内容已复制 ✓');
        });

        ha.querySelector('#btnEditorSave').addEventListener('click', function () {
          var btn = this;
          AGY.api.withButtonBusy(btn, '保存中...', async function () {
            var textToSave = ta.value;
            if (isJson && !/\.jsonc$/i.test(data.path || '')) {
              try { JSON.parse(textToSave); } catch (je) {
                synAlert.textContent = 'JSON 语法校验未通过：' + je.message;
                synAlert.classList.remove('hidden');
                return;
              }
            }
            try {
              var saved = await AGY.api.apiFetch('/api/editor/save', {
                method: 'POST',
                body: JSON.stringify({ path: data.path, content: textToSave })
              });
              initialContent = textToSave;
              currentContent = textToSave;
              dirty = false;
              // Keep AGY.state.drawerDirtyCheck intact so subsequent edits are still guarded!
              AGY.api.showToast('已保存配置文件（已自动备份历史版本）✓', 'success');
              if (AGY.app && AGY.app.refreshAll) await AGY.app.refreshAll();
              if (onSaved) onSaved(saved);
            } catch (e) {
              synAlert.textContent = e.message;
              synAlert.classList.remove('hidden');
              AGY.api.showToast('保存失败：' + e.message, 'error');
            }
          });
        });
      }, function () { return dirty; });
    } catch (e) {
      AGY.api.showToast('打开编辑器失败：' + e.message, 'error');
    }
  }

  async function openCapabilityDiffDrawer(key, source) {
    try {
      var data = await AGY.api.apiFetch('/api/capabilities/diff?key=' + encodeURIComponent(key) + '&source=' + encodeURIComponent(source || 'repo'));
      var files = data.files || [];
      var activeFileIdx = 0;

      function buildDiffBody() {
        var warnHtml = '';
        var missingLocal = data.missingLocalPaths || [];
        if (missingLocal.length > 0) {
          var pathRows = missingLocal.map(function (mp, idx) {
            return '<div style="margin-top:8px;padding-top:8px;border-top:1px dashed rgba(220,38,38,0.28)">' +
              '<div class="mono" style="font-size:11.5px;color:var(--red-text);margin-bottom:5px">缺失路径 #' + (idx + 1) + '：' + escapeHtml(mp) + '</div>' +
              '<div class="row gap-sm wrap">' +
                '<input class="input reloc-new-input" data-ridx="' + idx + '" placeholder="输入或浏览本机新的脚本/目录路径..." style="flex:1;min-width:210px" />' +
                '<button class="btn btn-xs reloc-browse-file" data-ridx="' + idx + '">选文件</button>' +
                '<button class="btn btn-xs reloc-browse-dir" data-ridx="' + idx + '">选目录</button>' +
                '<button class="btn btn-xs btn-primary reloc-save-btn" data-ridx="' + idx + '">修复此路径</button>' +
              '</div>' +
            '</div>';
          }).join('');

          warnHtml =
            '<div class="card" style="margin-bottom:12px;border-color:rgba(220,38,38,0.4);background:var(--red-soft)">' +
              '<div style="font-weight:600;color:var(--red-text)">⚠ 检测到 ' + missingLocal.length + ' 条本机不存在的依赖路径：</div>' +
              pathRows +
            '</div>';
        }

        var missingRemote = data.missingRemotePaths || [];
        if (missingRemote.length > 0) {
          warnHtml +=
            '<div class="alert-banner">提示：' + (source === 'backup' ? '备份文件' : '配置同步库') + '侧包含 ' + missingRemote.length + ' 条未匹配到当前本机磁盘的路径（' +
            escapeHtml(missingRemote[0]) + (missingRemote.length > 1 ? ' 等' : '') + '）。</div>';
        }

        var fileTabs = files.map(function (f, i) {
          var badge = f.status === 'equal' ? '' : (' · ' + f.status);
          return '<button class="filter-pill ' + (i === activeFileIdx ? 'active' : '') + '" data-dfidx="' + i + '">' + escapeHtml(f.path + badge) + '</button>';
        }).join('');

        var fObj = files[activeFileIdx];
        var codeHtml = '<div class="empty-state">无可对比文本文件</div>';
        if (fObj) {
          if (fObj.isBinary) {
            codeHtml = '<div class="empty-state">二进制文件，请直接同步整体配置项。</div>';
          } else {
            codeHtml = (fObj.truncated ? '<div class="badge badge-amber" style="margin-bottom:8px">文件较大，部分中间行已截断</div>' : '') +
              '<div style="border:1px solid var(--border);border-radius:8px;overflow:auto">' +
                '<table class="diff-table"><tbody>' +
                  (fObj.lines || []).map(function (ln) {
                    var rCls = ln.type === 'delete' ? 'diff-row-delete' : (ln.type === 'insert' ? 'diff-row-insert' : '');
                    var sign = ln.type === 'delete' ? '-' : (ln.type === 'insert' ? '+' : ' ');
                    return '<tr class="' + rCls + '">' +
                      '<td class="diff-ln">' + (ln.oldLine > 0 ? ln.oldLine : '') + '</td>' +
                      '<td class="diff-ln">' + (ln.newLine > 0 ? ln.newLine : '') + '</td>' +
                      '<td class="diff-sign">' + sign + '</td>' +
                      '<td>' + escapeHtml(ln.text) + '</td>' +
                    '</tr>';
                  }).join('') +
                '</tbody></table>' +
              '</div>';
          }
        }

        return warnHtml + '<div class="pill-group" style="margin-bottom:12px">' + fileTabs + '</div>' + codeHtml;
      }

      var hdrBtns =
        (data.remoteExists ? '<button class="btn btn-sm btn-primary" id="btnDiffAdoptRemote">← 同步到本机</button>' : '') +
        (data.localExists && source !== 'backup' ? '<button class="btn btn-sm" id="btnDiffPushLocal">存入配置同步库 →</button>' : '');

      openDrawer('源码差异对比 — [' + kindLabel(data.kind) + '] ' + data.id,
        '左侧为本机 (-)，右侧为' + (source === 'backup' ? '备份文件 (.agy) (+)' : '配置同步库 (+)') + ' · ' + data.directionBadge,
        buildDiffBody(), hdrBtns, bindDiffEvents);

      function bindDiffEvents(db, ha) {
        var dfBtns = db.querySelectorAll('[data-dfidx]');
        for (var i = 0; i < dfBtns.length; i++) {
          dfBtns[i].addEventListener('click', function () {
            activeFileIdx = parseInt(this.getAttribute('data-dfidx'), 10);
            db.innerHTML = buildDiffBody();
            bindDiffEvents(db, ha);
          });
        }

        var fileBtns = db.querySelectorAll('.reloc-browse-file');
        for (var fb = 0; fb < fileBtns.length; fb++) {
          fileBtns[fb].addEventListener('click', async function () {
            var idx = this.getAttribute('data-ridx');
            var pick = await AGY.api.apiFetch('/api/native/pick-open-file', { method: 'POST', body: JSON.stringify({ title: '选择可执行脚本/文件' }) });
            if (!pick.cancelled && pick.path) {
              var inp = db.querySelector('.reloc-new-input[data-ridx="' + idx + '"]');
              if (inp) inp.value = pick.path.replace(/\\/g, '/');
            }
          });
        }

        var dirBtns = db.querySelectorAll('.reloc-browse-dir');
        for (var dIdx = 0; dIdx < dirBtns.length; dIdx++) {
          dirBtns[dIdx].addEventListener('click', async function () {
            var idx = this.getAttribute('data-ridx');
            var pick = await AGY.api.apiFetch('/api/native/pick-folder', { method: 'POST', body: JSON.stringify({ title: '选择本机工具目录' }) });
            if (!pick.cancelled && pick.path) {
              var inp = db.querySelector('.reloc-new-input[data-ridx="' + idx + '"]');
              if (inp) inp.value = pick.path.replace(/\\/g, '/');
            }
          });
        }

        var saveBtns = db.querySelectorAll('.reloc-save-btn');
        for (var sb = 0; sb < saveBtns.length; sb++) {
          saveBtns[sb].addEventListener('click', function () {
            var btn = this;
            var idx = parseInt(btn.getAttribute('data-ridx'), 10);
            var inp = db.querySelector('.reloc-new-input[data-ridx="' + idx + '"]');
            var newP = inp ? inp.value.trim() : '';
            if (!newP) { AGY.api.showToast('请输入新路径', 'error'); return; }
            AGY.api.withButtonBusy(btn, '修复中...', async function () {
              try {
                await AGY.api.apiFetch('/api/capabilities/relocate', {
                  method: 'POST',
                  body: JSON.stringify({ key: key, scope: 'local', oldPath: (data.missingLocalPaths || [])[idx], newPath: newP })
                });
                if (AGY.app && AGY.app.refreshAll) await AGY.app.refreshAll('路径已修复 ✓');
                openCapabilityDiffDrawer(key, source);
              } catch (e) { AGY.api.showToast(e.message, 'error'); }
            });
          });
        }

        var btnAdopt = ha.querySelector('#btnDiffAdoptRemote');
        if (btnAdopt) btnAdopt.onclick = function () {
          AGY.api.withButtonBusy(btnAdopt, '同步中...', async function () {
            try {
              await AGY.api.apiFetch('/api/capabilities/import', { method: 'POST', body: JSON.stringify({ keys: [key], source: source || 'repo' }) });
              closeDrawer(true);
              if (AGY.app && AGY.app.refreshAll) await AGY.app.refreshAll('已同步到本机 ✓');
            } catch (e) { AGY.api.showToast(e.message, 'error'); }
          });
        };
        var btnPush = ha.querySelector('#btnDiffPushLocal');
        if (btnPush) btnPush.onclick = function () {
          AGY.api.withButtonBusy(btnPush, '存入中...', async function () {
            try {
              await AGY.api.apiFetch('/api/capabilities/push', { method: 'POST', body: JSON.stringify({ keys: [key] }) });
              closeDrawer(true);
              if (AGY.app && AGY.app.refreshAll) await AGY.app.refreshAll('已存入配置同步库 ✓');
            } catch (e) { AGY.api.showToast(e.message, 'error'); }
          });
        };
      }
    } catch (e) {
      AGY.api.showToast('加载源码对比失败：' + e.message, 'error');
    }
  }

  AGY.ui = {
    ICONS: ICONS,
    kindLabel: kindLabel,
    escapeHtml: escapeHtml,
    sanitizeUrl: sanitizeUrl,
    formatBytes: formatBytes,
    estimateTokens: estimateTokens,
    withButtonBusy: AGY.api.withButtonBusy,
    closeAllDropdowns: closeAllDropdowns,
    renderMarkdownSafe: renderMarkdownSafe,
    highlightTextNodes: highlightTextNodes,
    openModal: openModal,
    closeModal: closeModal,
    openDrawer: openDrawer,
    closeDrawer: closeDrawer,
    confirmCloseIdeModal: confirmCloseIdeModal,
    confirmForceKillIdeModal: confirmForceKillIdeModal,
    handleIdeConflictError: handleIdeConflictError,
    openDualModeRenameModal: openDualModeRenameModal,
    openMarkdownDrawer: openMarkdownDrawer,
    openConfigVersionHistoryModal: openConfigVersionHistoryModal,
    openInlineEditorDrawer: openInlineEditorDrawer,
    openCapabilityDiffDrawer: openCapabilityDiffDrawer
  };
})();
