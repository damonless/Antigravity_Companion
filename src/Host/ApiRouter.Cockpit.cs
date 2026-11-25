using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;

namespace AntigravityCompanion
{
    /// <summary>
    /// Daily Cockpit, MCP Doctor, Strict Inline Editor &amp; Workspace Launcher partial of ApiRouter.
    /// Strictly C# 5.0 (.NET Framework 4.x).
    /// </summary>
    internal sealed partial class ApiRouter
    {
        private bool TryRouteCockpit(NetworkStream stream, ParsedHttpRequest req, string path, Dictionary<string, object> body)
        {
            if (path == "/api/cockpit/summary" && req.Method == "GET")
            {
                OperationResult<Dictionary<string, object>> res = _dispatcher.Execute("cockpit.summary", null);
                if (!res.Ok) { HttpProtocolHelper.WriteJsonError(stream, 500, res.ErrorMessage); return true; }
                HttpProtocolHelper.WriteJsonResponse(stream, 200, res.Data);
                return true;
            }
            if (path == "/api/cockpit/settings" && req.Method == "POST")
            {
                WriteDispatcherResult(stream, _dispatcher.Execute("cockpit.settings", body));
                return true;
            }
            if (path == "/api/cockpit/toggle" && req.Method == "POST")
            {
                WriteDispatcherResult(stream, _dispatcher.Execute("cockpit.toggle", body));
                return true;
            }
            if (path == "/api/mcp/doctor" && req.Method == "POST")
            {
                OperationResult<Dictionary<string, object>> res = _dispatcher.Execute("mcp.doctor", body);
                if (!res.Ok) { HttpProtocolHelper.WriteJsonError(stream, 500, res.ErrorMessage); return true; }
                HttpProtocolHelper.WriteJsonResponse(stream, 200, res.Data);
                return true;
            }
            if (path == "/api/editor/file" && req.Method == "GET")
            {
                string filePath;
                req.Query.TryGetValue("path", out filePath);
                Dictionary<string, object> args = new Dictionary<string, object>();
                args["path"] = filePath ?? string.Empty;
                WriteDispatcherResult(stream, _dispatcher.Execute("editor.read", args));
                return true;
            }
            if (path == "/api/editor/save" && req.Method == "POST")
            {
                string filePath = UnifiedJson.GetString(body, "path", string.Empty).Trim();
                if (string.IsNullOrEmpty(filePath) || body == null || !body.ContainsKey("content"))
                {
                    HttpProtocolHelper.WriteJsonError(stream, 400, "path 与 content 参数不能为空。");
                    return true;
                }
                string content = body["content"] as string;
                if (content == null)
                {
                    HttpProtocolHelper.WriteJsonError(stream, 400, "content 必须是非空类型的字符串。");
                    return true;
                }

                if (!_host.TryAcquireJob("editor_save", "正在保存定制配置文件..."))
                {
                    HttpProtocolHelper.WriteJsonError(stream, 409, "当前有其他后台写入任务正在执行，请稍后再试。");
                    return true;
                }
                try
                {
                    int refThreshold = _host.SnapshotService != null
                        ? _host.SnapshotService.GetSettings().RuleTokenReferenceThreshold
                        : 0;
                    EditorFileResult saveRes;
                    lock (_host.MutationLock)
                    {
                        saveRes = AntigravityConfigService.SaveAllowedCustomizationFile(
                            _host.GeminiPath, _host.HomePath, _host.RepoPath, filePath, content ?? string.Empty, refThreshold);
                        if (saveRes.Ok) _host.ReloadAllLocalStateLocked();
                    }
                    if (!saveRes.Ok)
                    {
                        HttpProtocolHelper.WriteJsonError(stream, saveRes.HttpStatusCode > 0 ? saveRes.HttpStatusCode : 400, saveRes.Error ?? "保存失败。");
                        return true;
                    }
                    _host.AppendLog("success", string.Format("已保存配置文件：{0}（约 {1} tokens）", Path.GetFileName(saveRes.CanonicalPath), saveRes.EstimatedTokens));
                    Dictionary<string, object> resp = new Dictionary<string, object>();
                    resp["ok"] = true;
                    resp["path"] = saveRes.CanonicalPath;
                    resp["charCount"] = saveRes.CharCount;
                    resp["estimatedTokens"] = saveRes.EstimatedTokens;
                    resp["overBudget"] = saveRes.OverBudget;
                    resp["backupPath"] = saveRes.BackupPath ?? string.Empty;
                    HttpProtocolHelper.WriteJsonResponse(stream, 200, resp);
                }
                finally
                {
                    _host.ReleaseJob();
                }
                return true;
            }
            if (path == "/api/workspace/launch" && req.Method == "POST")
            {
                string targetDir = UnifiedJson.GetString(body, "path", string.Empty).Trim();
                if (string.IsNullOrEmpty(targetDir)) targetDir = UnifiedJson.GetString(body, "folderPath", string.Empty).Trim();
                string toolMode = UnifiedJson.GetString(body, "tool", string.Empty).Trim();
                if (string.IsNullOrEmpty(toolMode)) toolMode = UnifiedJson.GetString(body, "mode", "ide").Trim();

                if (string.IsNullOrEmpty(targetDir))
                {
                    HttpProtocolHelper.WriteJsonError(stream, 400, "工作区目录 path 不能为空。");
                    return true;
                }

                if (!_host.TryAcquireJob("workspace_launch", "正在启动工作区工具..."))
                {
                    HttpProtocolHelper.WriteJsonError(stream, 409, "当前有其他后台任务正在执行，请稍后再试。");
                    return true;
                }
                try
                {
                    WorkspaceLaunchResult launchRes = WorkspaceLauncherService.LaunchWorkspace(
                        _host.GeminiPath, _host.HomePath, _host.RepoPath, targetDir, toolMode);
                    if (!launchRes.Ok)
                    {
                        HttpProtocolHelper.WriteJsonError(stream, launchRes.HttpStatusCode > 0 ? launchRes.HttpStatusCode : 400, launchRes.Error ?? "启动失败。");
                        return true;
                    }
                    _host.AppendLog("info", string.Format("已使用 [{0}] 打开工作区：{1}", launchRes.Tool, launchRes.LaunchedPath));
                    Dictionary<string, object> resp = new Dictionary<string, object>();
                    resp["ok"] = true;
                    resp["tool"] = launchRes.Tool;
                    resp["executable"] = launchRes.Executable;
                    resp["launchedPath"] = launchRes.LaunchedPath;
                    HttpProtocolHelper.WriteJsonResponse(stream, 200, resp);
                }
                finally
                {
                    _host.ReleaseJob();
                }
                return true;
            }

            return false;
        }

        // ── Dispatcher Write Commands: cockpit.settings & cockpit.toggle ──

        private OperationResult<Dictionary<string, object>> ExecuteCockpitSettingsCommand(Dictionary<string, object> args)
        {
            if (!_host.TryAcquireJob("cockpit_settings", "正在保存 Antigravity 全局配置..."))
            {
                return FailJobBusy("当前有其他后台写入任务正在执行，请稍后再试。");
            }
            try
            {
                Dictionary<string, object> updates = null;
                object usObj;
                if (args != null && args.TryGetValue("userSettings", out usObj))
                {
                    updates = UnifiedJson.AsDict(usObj);
                    if (updates == null)
                    {
                        return FailCommand(400, "userSettings 必须是 JSON 对象。");
                    }
                }
                else
                {
                    updates = args;
                }

                if (updates == null || updates.Count == 0)
                {
                    return FailCommand(400, "未提交任何 userSettings 更新字段。");
                }

                string bakPath;
                string patchErr;
                CockpitSnapshot afterSnap;
                int refThreshold = _host.SnapshotService != null
                    ? _host.SnapshotService.GetSettings().RuleTokenReferenceThreshold
                    : 0;
                lock (_host.MutationLock)
                {
                    if (!AntigravityConfigService.PatchUserSettings(_host.GeminiPath, updates, out bakPath, out patchErr))
                    {
                        return FailCommand(400, patchErr ?? "保存 userSettings 失败。");
                    }
                    _host.ReloadAllLocalStateLocked();
                    afterSnap = AntigravityConfigService.ScanCockpit(_host.GeminiPath, _host.HomePath, _host.RepoPath, refThreshold);
                }
                _host.AppendLog("success", "已保存 Antigravity 全局运行配置。");
                Dictionary<string, object> serialized = AntigravityConfigService.SerializeCockpitSummary(afterSnap, _host.GeminiPath, refThreshold);
                bool ideRunning = _host.IsIdeRunning();
                Dictionary<string, object> resp = new Dictionary<string, object>();
                resp["ok"] = true;
                resp["backupPath"] = bakPath ?? string.Empty;
                resp["userSettings"] = serialized["userSettings"];
                resp["ideRunning"] = ideRunning;
                resp["ideRunningWarning"] = ideRunning
                    ? "检测到 Antigravity 主窗口正在运行，部分配置需重启 IDE 主窗口或新开对话后生效（运行中修改可能被主窗口内存状态覆盖）。"
                    : string.Empty;
                return OperationResult<Dictionary<string, object>>.Success(resp);
            }
            finally
            {
                _host.ReleaseJob();
            }
        }

        private OperationResult<Dictionary<string, object>> ExecuteCockpitToggleCommand(Dictionary<string, object> args)
        {
            string kind = UnifiedJson.GetString(args, "kind", string.Empty).Trim();
            string id = UnifiedJson.GetString(args, "id", string.Empty).Trim();
            string sourcePath = UnifiedJson.GetString(args, "sourcePath", string.Empty).Trim();
            if (string.IsNullOrEmpty(kind) || string.IsNullOrEmpty(id))
            {
                return FailCommand(400, "kind 与 id 不能为空。");
            }

            bool? desiredEnabled = null;
            if (args != null)
            {
                object enabledVal = null;
                bool hasEnabledKey = args.TryGetValue("enabled", out enabledVal);
                if (!hasEnabledKey)
                {
                    foreach (KeyValuePair<string, object> kvp in args)
                    {
                        if (string.Equals(kvp.Key, "enabled", StringComparison.OrdinalIgnoreCase))
                        {
                            enabledVal = kvp.Value;
                            hasEnabledKey = true;
                            break;
                        }
                    }
                }
                if (hasEnabledKey && enabledVal != null)
                {
                    desiredEnabled = UnifiedJson.GetBool(args, "enabled", true);
                }
            }

            if (!_host.TryAcquireJob("cockpit_toggle", "正在切换扩展配置启用状态..."))
            {
                return FailJobBusy("当前有其他后台写入任务正在执行，请稍后再试。");
            }
            try
            {
                bool resultingEnabled;
                string targetFile;
                string bakPath;
                int httpCode;
                string errMsg;
                lock (_host.MutationLock)
                {
                    if (!AntigravityConfigService.ToggleCapability(
                        _host.GeminiPath, kind, id, desiredEnabled, sourcePath,
                        out resultingEnabled, out targetFile, out bakPath, out httpCode, out errMsg))
                    {
                        return FailCommand(httpCode > 0 ? httpCode : 400, errMsg ?? "切换启用状态失败。");
                    }
                    _host.ReloadAllLocalStateLocked();
                }
                string kindCn = kind;
                if (string.Equals(kind, "plugin", StringComparison.OrdinalIgnoreCase)) kindCn = "插件";
                else if (string.Equals(kind, "mcp", StringComparison.OrdinalIgnoreCase)) kindCn = "MCP";
                else if (string.Equals(kind, "hook", StringComparison.OrdinalIgnoreCase)) kindCn = "钩子";
                else if (string.Equals(kind, "skill", StringComparison.OrdinalIgnoreCase)) kindCn = "技能";
                else if (string.Equals(kind, "rule", StringComparison.OrdinalIgnoreCase)) kindCn = "规则";
                _host.AppendLog("success", string.Format("已{0} [{1}] {2}", resultingEnabled ? "启用" : "停用", kindCn, id));
                bool ideRunning = _host.IsIdeRunning();
                Dictionary<string, object> resp = new Dictionary<string, object>();
                resp["ok"] = true;
                resp["kind"] = kind;
                resp["id"] = id;
                resp["enabled"] = resultingEnabled;
                resp["targetFile"] = targetFile ?? string.Empty;
                resp["backupPath"] = bakPath ?? string.Empty;
                resp["ideRunning"] = ideRunning;
                resp["ideRunningWarning"] = ideRunning
                    ? "检测到 Antigravity 主窗口正在运行，部分配置需重启 IDE 主窗口或新开对话后生效（运行中修改可能被主窗口内存状态覆盖）。"
                    : string.Empty;
                return OperationResult<Dictionary<string, object>>.Success(resp);
            }
            finally
            {
                _host.ReleaseJob();
            }
        }
    }
}
