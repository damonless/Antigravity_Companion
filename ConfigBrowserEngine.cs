using System;
using System.Collections.Generic;
using System.IO;

namespace AntigravityCompanion
{
    // ══════════════════════════════════════════════════════════════════════
    // CONFIGURATION BROWSER & SCANNER ENGINE (Safe File Inspection)
    // ══════════════════════════════════════════════════════════════════════

    public class BrowserItem
    {
        public string DisplayName;
        public string FilePath;
        public BrowserItem(string name, string path) { DisplayName = name; FilePath = path; }
        public override string ToString() { return DisplayName; }
    }

    public class ScanRoot
    {
        public string Name;
        public string RootPath;
        public ScanRoot(string n, string p) { Name = n; RootPath = p; }
    }

    public static class ConfigBrowserScanner
    {
        public static List<ScanRoot> DetectScanRoots(string geminiPath, Dictionary<string, ProjectMetadata> localProjects)
        {
            List<ScanRoot> roots = new List<ScanRoot>();
            if (string.IsNullOrEmpty(geminiPath)) return roots;

            roots.Add(new ScanRoot("全局配置 (Global)", geminiPath));

            if (localProjects != null)
            {
                foreach (KeyValuePair<string, ProjectMetadata> kvp in localProjects)
                {
                    ProjectMetadata p = kvp.Value;
                    if (!string.IsNullOrEmpty(p.FolderUri))
                    {
                        try
                        {
                            string localPath = DirectStreamArchiver.UnescapeUriString(p.FolderUri);
                            if (localPath.StartsWith("file:///", StringComparison.OrdinalIgnoreCase))
                                localPath = localPath.Substring(8);
                            else if (localPath.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
                                localPath = localPath.Substring(7);
                            localPath = localPath.Replace('/', '\\');

                            if (Directory.Exists(localPath))
                            {
                                roots.Add(new ScanRoot("工程: " + p.Name, localPath));
                            }
                        }
                        catch {}
                    }
                }
            }

            return roots;
        }

        public static List<BrowserItem> ScanRules(List<ScanRoot> roots)
        {
            List<BrowserItem> list = new List<BrowserItem>();
            if (roots == null) return list;

            for (int i = 0; i < roots.Count; i++)
            {
                try
                {
                    ScanRoot r = roots[i];
                    string geminiRules = Path.Combine(r.RootPath, ".geminirules");
                    if (File.Exists(geminiRules)) list.Add(new BrowserItem(string.Format("[{0}] .geminirules", r.Name), geminiRules));

                    string geminiMd = Path.Combine(r.RootPath, "GEMINI.md");
                    if (File.Exists(geminiMd)) list.Add(new BrowserItem(string.Format("[{0}] GEMINI.md", r.Name), geminiMd));

                    string agentsMd = Path.Combine(r.RootPath, "AGENTS.md");
                    if (File.Exists(agentsMd)) list.Add(new BrowserItem(string.Format("[{0}] AGENTS.md", r.Name), agentsMd));

                    string rulesDir = Path.Combine(r.RootPath, "config\\rules");
                    if (Directory.Exists(rulesDir))
                    {
                        string[] mds = Directory.GetFiles(rulesDir, "*.md", SearchOption.AllDirectories);
                        for (int j = 0; j < mds.Length; j++)
                        {
                            list.Add(new BrowserItem(string.Format("[{0}] {1}", r.Name, Path.GetFileName(mds[j])), mds[j]));
                        }
                    }
                }
                catch {}
            }
            return list;
        }

        public static List<BrowserItem> ScanSkills(List<ScanRoot> roots)
        {
            List<BrowserItem> list = new List<BrowserItem>();
            if (roots == null) return list;

            for (int i = 0; i < roots.Count; i++)
            {
                try
                {
                    ScanRoot r = roots[i];

                    string bSkills = Path.Combine(r.RootPath, "antigravity\\builtin\\skills");
                    if (Directory.Exists(bSkills))
                    {
                        string[] dirs = Directory.GetDirectories(bSkills);
                        for (int j = 0; j < dirs.Length; j++)
                        {
                            string skMd = Path.Combine(dirs[j], "SKILL.md");
                            if (File.Exists(skMd)) list.Add(new BrowserItem(string.Format("[内置] {0}", Path.GetFileName(dirs[j])), skMd));
                        }
                    }

                    string uSkills = Path.Combine(r.RootPath, "config\\skills");
                    if (Directory.Exists(uSkills))
                    {
                        string[] dirs = Directory.GetDirectories(uSkills);
                        for (int j = 0; j < dirs.Length; j++)
                        {
                            string skMd = Path.Combine(dirs[j], "SKILL.md");
                            if (File.Exists(skMd)) list.Add(new BrowserItem(string.Format("[用户] {0}", Path.GetFileName(dirs[j])), skMd));
                        }
                    }
                }
                catch {}
            }
            return list;
        }

        public static List<BrowserItem> ScanMcp(List<ScanRoot> roots)
        {
            List<BrowserItem> list = new List<BrowserItem>();
            if (roots == null) return list;

            for (int i = 0; i < roots.Count; i++)
            {
                try
                {
                    ScanRoot r = roots[i];
                    string mcpCfg = Path.Combine(r.RootPath, "config\\mcp_config.json");
                    if (File.Exists(mcpCfg)) list.Add(new BrowserItem(string.Format("[{0}] mcp_config.json", r.Name), mcpCfg));

                    string mcpDir = Path.Combine(r.RootPath, "antigravity\\mcp");
                    if (Directory.Exists(mcpDir))
                    {
                        string[] mcpServers = Directory.GetDirectories(mcpDir);
                        for (int j = 0; j < mcpServers.Length; j++)
                        {
                            string sName = Path.GetFileName(mcpServers[j]);
                            string[] jsons = Directory.GetFiles(mcpServers[j], "*.json");
                            for (int k = 0; k < jsons.Length; k++)
                            {
                                list.Add(new BrowserItem(string.Format("[{0}] {1}/{2}", r.Name, sName, Path.GetFileName(jsons[k])), jsons[k]));
                            }
                        }
                    }
                }
                catch {}
            }
            return list;
        }
    }
}
