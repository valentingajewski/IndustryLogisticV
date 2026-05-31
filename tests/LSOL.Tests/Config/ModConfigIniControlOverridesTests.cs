using System.IO;
using LSOL.Config;
using LSOL.Tests.TestSupport;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WinForms = System.Windows.Forms;

namespace LSOL.Tests.Config
{
    [TestClass]
    public sealed class ModConfigIniControlOverridesTests
    {
        [TestMethod]
        public void Load_WithLsolIniControls_OverridesTheFourSupportedBindings()
        {
            var configDirectory = Path.Combine(TestWorkspace.GetRepoRoot(), "LSOL_Config");
            var iniPath = TestWorkspace.CreateTempFilePath("LSOL.ini");
            File.WriteAllText(
                iniPath,
                "[Controls]\r\nToggleDashboard=F10\r\nOpenModMenu=F11\r\nOpenDebugMenu=F12\r\nInteract=Q\r\n");

            var config = ModConfig.Load(configDirectory, null, iniPath);

            Assert.AreEqual(WinForms.Keys.F10, config.Controls.ToggleDashboard);
            Assert.AreEqual(WinForms.Keys.F11, config.Controls.OpenModMenu);
            Assert.AreEqual(WinForms.Keys.F12, config.Controls.OpenDebugMenu);
            Assert.AreEqual(WinForms.Keys.Q, config.Controls.Interact);
        }

        [TestMethod]
        public void Load_WithInvalidLsolIniControlValues_FallsBackSafelyToDefaults()
        {
            var configDirectory = Path.Combine(TestWorkspace.GetRepoRoot(), "LSOL_Config");
            var iniPath = TestWorkspace.CreateTempFilePath("LSOL.ini");
            File.WriteAllText(
                iniPath,
                "[Controls]\r\nToggleDashboard=Nope\r\nOpenModMenu=StillNope\r\nOpenDebugMenu=NotAKey\r\nInteract=BadValue\r\n");

            var config = ModConfig.Load(configDirectory, null, iniPath);

            Assert.AreEqual(WinForms.Keys.F8, config.Controls.ToggleDashboard);
            Assert.AreEqual(WinForms.Keys.F7, config.Controls.OpenModMenu);
            Assert.AreEqual(WinForms.Keys.F9, config.Controls.OpenDebugMenu);
            Assert.AreEqual(WinForms.Keys.E, config.Controls.Interact);
        }

        [TestMethod]
        public void Load_WithMissingLsolIniControlKeys_UsesDefaultsForUntouchedBindings()
        {
            var configDirectory = Path.Combine(TestWorkspace.GetRepoRoot(), "LSOL_Config");
            var iniPath = TestWorkspace.CreateTempFilePath("LSOL.ini");
            File.WriteAllText(
                iniPath,
                "[Controls]\r\nOpenModMenu=F11\r\n");

            var config = ModConfig.Load(configDirectory, null, iniPath);

            Assert.AreEqual(WinForms.Keys.F8, config.Controls.ToggleDashboard);
            Assert.AreEqual(WinForms.Keys.F11, config.Controls.OpenModMenu);
            Assert.AreEqual(WinForms.Keys.F9, config.Controls.OpenDebugMenu);
            Assert.AreEqual(WinForms.Keys.E, config.Controls.Interact);
        }
    }
}