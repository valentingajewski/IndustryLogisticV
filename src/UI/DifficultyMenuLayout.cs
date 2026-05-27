using System.Collections.Generic;

namespace LSOL.UI
{
    internal enum DifficultyRootMenuEntryKind
    {
        StartingBalance = 0,
        EnableAll = 1,
        DisableAll = 2,
        Templates = 3,
    }

    internal enum DifficultyTemplateMenuEntryKind
    {
        SaveTemplate = 0,
        LoadTemplate = 1,
        Back = 2,
    }

    internal static class DifficultyMenuLayout
    {
        private static readonly DifficultyRootMenuEntryKind[] LiveRootPrefix =
        {
            DifficultyRootMenuEntryKind.EnableAll,
            DifficultyRootMenuEntryKind.DisableAll,
            DifficultyRootMenuEntryKind.Templates,
        };

        private static readonly DifficultyRootMenuEntryKind[] PendingRootPrefix =
        {
            DifficultyRootMenuEntryKind.StartingBalance,
            DifficultyRootMenuEntryKind.EnableAll,
            DifficultyRootMenuEntryKind.DisableAll,
            DifficultyRootMenuEntryKind.Templates,
        };

        private static readonly DifficultyTemplateMenuEntryKind[] TemplateMenuActions =
        {
            DifficultyTemplateMenuEntryKind.SaveTemplate,
            DifficultyTemplateMenuEntryKind.LoadTemplate,
            DifficultyTemplateMenuEntryKind.Back,
        };

        public static IReadOnlyList<DifficultyRootMenuEntryKind> BuildRootPrefix(bool includeStartingBalance)
        {
            return includeStartingBalance
                ? PendingRootPrefix
                : LiveRootPrefix;
        }

        public static IReadOnlyList<DifficultyTemplateMenuEntryKind> BuildTemplateMenuActions()
        {
            return TemplateMenuActions;
        }
    }
}