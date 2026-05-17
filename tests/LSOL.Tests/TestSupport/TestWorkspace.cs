using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LSOL.Tests.TestSupport
{
    internal static class TestWorkspace
    {
        public static string GetRepoRoot()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "LSOL.csproj")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }

            Assert.Fail("Could not locate the repository root from the test output directory.");
            return string.Empty;
        }

        public static string CreateTempFilePath(string fileName)
        {
            var directory = Path.Combine(Path.GetTempPath(), "LSOL.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            return Path.Combine(directory, fileName);
        }
    }
}