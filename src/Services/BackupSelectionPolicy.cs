using System;

namespace AntigravityCompanion
{
    /// <summary>
    /// Strategy selected when Antigravity IDE processes are running during persistent metadata writes.
    /// Strictly C# 5.0 (.NET Framework 4.x).
    /// </summary>
    public enum RenameProcessStrategy
    {
        Cancel,
        CloseAndFullWrite,
        HotWritePersistentLayers
    }

    /// <summary>
    /// Single source of truth for auto-selecting backup sessions during restore/import comparison.
    /// </summary>
    public static class BackupSelectionPolicy
    {
        public static bool ShouldAutoSelectBackupSession(
            ConversationSummaryInfo backupConv,
            ConversationSummaryInfo localConv)
        {
            return ShouldAutoSelectBackupSession(backupConv, localConv, null);
        }

        public static bool ShouldAutoSelectBackupSession(
            ConversationSummaryInfo backupConv,
            ConversationSummaryInfo localConv,
            ProjectDiffNode pDiff)
        {
            if (backupConv == null) return false;
            if (localConv == null) return true;

            if (backupConv.StepCount > localConv.StepCount) return true;
            if (backupConv.StepCount < localConv.StepCount) return false;

            if (backupConv.UserInteractions > localConv.UserInteractions) return true;
            if (backupConv.UserInteractions < localConv.UserInteractions) return false;

            if (backupConv.UpdatedTime > DateTime.MinValue &&
                (localConv.UpdatedTime == DateTime.MinValue || backupConv.UpdatedTime > localConv.UpdatedTime.AddSeconds(2)))
            {
                return true;
            }

            return false;
        }
    }
}
