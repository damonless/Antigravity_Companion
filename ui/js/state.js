(function () {
  'use strict';

  var AGY = window.AGY = window.AGY || {};

  AGY.state = {
    token: '',
    view: 'overview',
    theme: localStorage.getItem('agy_theme') || 'light',
    overview: null,
    cockpitData: null,
    sessionsData: null,
    renameData: null,
    storageData: null,
    capData: null,
    backupData: null,
    companionSettings: null,
    snapshotStatus: null,
    vaultStatus: null,

    // Lazy refresh dirty flags (ACC-07)
    dirty: {
      overview: false,
      cockpit: true,
      sessions: true,
      rename: true,
      vault: true,
      backup: true,
      storage: true,
      settings: true
    },

    // Sessions & Rename state
    selectedBucketKey: '__ALL__',
    auxExpanded: false,
    legacyCliExpanded: false,
    sessionSearchQuery: '',
    sessionSearchContent: false,
    sessionSearchMatchedIds: null,
    checkedSessions: {},

    renameStep: 1,
    renameProjectFilter: '__ALL__',
    renameScopedCids: {},
    renameShowTagged: false,
    renameShowAux: false,
    renameShowRunning: false,
    renameShowLowValue: false,
    renameChecked: {},
    renameEdits: {},
    renameAiModified: {},
    renameFocusedCid: '',
    renamePromptExpanded: false,
    renameTechExpanded: false,
    renameStrategy: 'CloseAndFullWrite',
    cliStatus: null,

    // Cockpit state
    cockpitSwitchFilter: 'ALL',
    cockpitSearch: '',
    cockpitIdeWarning: '',
    mcpDoctorResults: null,

    // Storage state
    storageMinAgeDays: 7,
    storageMinSizeMb: 5,
    storageCheckedCids: {},
    storageSlimMode: 'coldArchivePrune',
    storagePruneLegacyCli: false,
    lastSlimResult: null,

    // Vault & Capabilities state
    vaultTab: 'sync', // 'sync' | 'rules_editor'
    capCategory: 'ALL',
    capDirFilter: 'ALL',
    capSearch: '',
    capChecked: {},

    // Backup & Storage Sub-View state
    backupSubView: 'backup', // 'backup' | 'storage'
    backupTier: 'fast',
    backupTab: 'caps',
    backupSearch: '',
    backupRestoreUserSettings: false,
    backupExpandedParents: {},
    backupCheckedCaps: {},
    backupCheckedConvs: {},

    // Settings state
    settingsCliStatus: null,

    logsOpen: false,
    drawerDirtyCheck: null
  };

  function el(id) {
    return document.getElementById(id);
  }

  function setText(idOrEl, text) {
    var node = typeof idOrEl === 'string' ? el(idOrEl) : idOrEl;
    if (node) node.textContent = text === undefined || text === null ? '' : String(text);
  }

  function patchCheckboxRow(rowEl, checked) {
    if (!rowEl) return;
    if (checked) rowEl.classList.add('selected');
    else rowEl.classList.remove('selected');
  }

  function preserveScroll(containerSelector, renderFn) {
    var container = document.querySelector(containerSelector);
    var scrollTop = container ? container.scrollTop : 0;
    renderFn();
    var after = document.querySelector(containerSelector);
    if (after) after.scrollTop = scrollTop;
  }

  function preserveFocusInput(inputId, renderFn) {
    var prev = el(inputId);
    var hadFocus = prev && document.activeElement === prev;
    var selStart = prev && typeof prev.selectionStart === 'number' ? prev.selectionStart : null;
    var selEnd = prev && typeof prev.selectionEnd === 'number' ? prev.selectionEnd : null;
    renderFn();
    if (hadFocus) {
      var next = el(inputId);
      if (next) {
        next.focus();
        if (selStart !== null && selEnd !== null && typeof next.setSelectionRange === 'function') {
          try { next.setSelectionRange(selStart, selEnd); } catch (_e) {}
        }
      }
    }
  }

  AGY.dom = {
    el: el,
    setText: setText,
    patchCheckboxRow: patchCheckboxRow,
    preserveScroll: preserveScroll,
    preserveFocusInput: preserveFocusInput
  };
})();
