using System.Linq;
using LSOL.UI;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LSOL.Tests.UI
{
    [TestClass]
    public sealed class DifficultyMenuLayoutTests
    {
        [TestMethod]
        public void BuildRootPrefix_ForLivePlacesBulkActionsBeforeTemplates()
        {
            CollectionAssert.AreEqual(
                new[]
                {
                    DifficultyRootMenuEntryKind.EnableAll,
                    DifficultyRootMenuEntryKind.DisableAll,
                    DifficultyRootMenuEntryKind.Templates,
                },
                DifficultyMenuLayout.BuildRootPrefix(false).ToArray());
        }

        [TestMethod]
        public void BuildRootPrefix_ForPendingKeepsStartingBalanceThenBulkActionsThenTemplates()
        {
            CollectionAssert.AreEqual(
                new[]
                {
                    DifficultyRootMenuEntryKind.StartingBalance,
                    DifficultyRootMenuEntryKind.EconomyMode,
                    DifficultyRootMenuEntryKind.EnableAll,
                    DifficultyRootMenuEntryKind.DisableAll,
                    DifficultyRootMenuEntryKind.Templates,
                },
                DifficultyMenuLayout.BuildRootPrefix(true).ToArray());
        }

        [TestMethod]
        public void BuildTemplateMenuActions_ContainsOnlyTemplateActionsAndBack()
        {
            CollectionAssert.AreEqual(
                new[]
                {
                    DifficultyTemplateMenuEntryKind.SaveTemplate,
                    DifficultyTemplateMenuEntryKind.LoadTemplate,
                    DifficultyTemplateMenuEntryKind.Back,
                },
                DifficultyMenuLayout.BuildTemplateMenuActions().ToArray());
        }
    }
}