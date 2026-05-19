using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;

namespace LSOL.Config
{
    public sealed class LsolAddonCatalog
    {
        public const int CurrentApiVersion = 1;
        public const string CapabilityMissions = "content.missions";
        public const string CapabilityResources = "content.resources";
        public const string CapabilitySites = "content.sites";
        public const string CapabilityVehicles = "content.vehicles";
        public const string CapabilityOfficeObjects = "content.officeObjects";

        private const string CapabilityUiLocalization = "ui.localization";
        private const string CapabilityUiTheme = "ui.theme";
        private const string CapabilityModuleMissionType = "module.missionType";
        private const string CapabilityModuleTabletApp = "module.tabletApp";
        private static readonly Version FallbackRuntimeVersion = new Version(1, 0, 0);
        private static readonly Dictionary<string, string> CapabilityAliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { CapabilityMissions, CapabilityMissions },
            { CapabilityResources, CapabilityResources },
            { CapabilitySites, CapabilitySites },
            { CapabilityVehicles, CapabilityVehicles },
            { CapabilityOfficeObjects, CapabilityOfficeObjects },
            { "content.office-objects", CapabilityOfficeObjects },
            { CapabilityUiLocalization, CapabilityUiLocalization },
            { CapabilityUiTheme, CapabilityUiTheme },
            { "ui.themes", CapabilityUiTheme },
            { CapabilityModuleMissionType, CapabilityModuleMissionType },
            { CapabilityModuleTabletApp, CapabilityModuleTabletApp },
        };
        private static readonly Dictionary<string, string> DirectoryTypeToCapability = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "missions", CapabilityMissions },
            { "resources", CapabilityResources },
            { "sites", CapabilitySites },
            { "vehicles", CapabilityVehicles },
            { "office-objects", CapabilityOfficeObjects },
            { "officeObjects", CapabilityOfficeObjects },
            { "localization", CapabilityUiLocalization },
            { "theme", CapabilityUiTheme },
            { "themes", CapabilityUiTheme },
        };
        private static readonly HashSet<string> KnownCategories = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "mission-pack",
            "content-pack",
            "preset-pack",
            "ui-pack",
            "module",
            "plugin",
        };
        private static readonly HashSet<string> SupportedCapabilities = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            CapabilityMissions,
            CapabilityResources,
            CapabilitySites,
            CapabilityVehicles,
            CapabilityOfficeObjects,
        };

        public LsolAddonCatalog()
        {
            AddonsDirectory = string.Empty;
            RuntimeLsolVersion = ResolveRuntimeVersion();
            AllPackages = new List<LsolAddonPackage>();
            Packages = new List<LsolAddonPackage>();
            ValidationMessages = new List<string>();
        }

        private LsolAddonCatalog(string addonsDirectory)
            : this()
        {
            AddonsDirectory = addonsDirectory ?? string.Empty;
        }

        public string AddonsDirectory { get; private set; }

        public Version RuntimeLsolVersion { get; private set; }

        public List<LsolAddonPackage> AllPackages { get; }

        public List<LsolAddonPackage> Packages { get; }

        public List<string> ValidationMessages { get; }

        public IEnumerable<LsolAddonPackage> GetPackagesForCapability(string capability)
        {
            var normalizedCapability = NormalizeCapability(capability);
            return string.IsNullOrWhiteSpace(normalizedCapability)
                ? Enumerable.Empty<LsolAddonPackage>()
                : Packages.Where(package => package != null && package.IsEnabled && package.ActiveCapabilities.Contains(normalizedCapability));
        }

        public static string ResolveAddonsDirectory(string configDirectory)
        {
            if (string.IsNullOrWhiteSpace(configDirectory))
            {
                return string.Empty;
            }

            try
            {
                var runtimeDirectory = Path.GetDirectoryName(Path.GetFullPath(configDirectory));
                return string.IsNullOrWhiteSpace(runtimeDirectory)
                    ? string.Empty
                    : Path.Combine(runtimeDirectory, "LSOL_Addons");
            }
            catch
            {
                return string.Empty;
            }
        }

        public static LsolAddonCatalog Load(string configDirectory)
        {
            var catalog = new LsolAddonCatalog(ResolveAddonsDirectory(configDirectory));
            if (string.IsNullOrWhiteSpace(catalog.AddonsDirectory) || !Directory.Exists(catalog.AddonsDirectory))
            {
                return catalog;
            }

            var packageDirectories = Directory.GetDirectories(catalog.AddonsDirectory, "*", SearchOption.TopDirectoryOnly)
                .OrderBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var discoveredPackages = new List<LsolAddonPackage>();

            for (int i = 0; i < packageDirectories.Length; i++)
            {
                var packageDirectory = packageDirectories[i];
                var manifestPath = Path.Combine(packageDirectory, "addon.xml");
                if (!File.Exists(manifestPath))
                {
                    catalog.ValidationMessages.Add(string.Format("Add-on folder '{0}' is missing addon.xml and was skipped.", Path.GetFileName(packageDirectory)));
                    continue;
                }

                discoveredPackages.Add(ParsePackage(packageDirectory, manifestPath));
            }

            MarkDuplicateIds(discoveredPackages);

            var packagesById = discoveredPackages
                .Where(package => package != null && !package.HasBlockingFailure && !package.HasDuplicateId && !string.IsNullOrWhiteSpace(package.Id))
                .GroupBy(package => package.Id, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < discoveredPackages.Count; i++)
            {
                FinalizePackageAvailability(discoveredPackages[i], packagesById, catalog.RuntimeLsolVersion);
            }

            var orderedPackages = OrderPackages(discoveredPackages.Where(package => package != null && package.IsEnabled).ToList());
            var orderedPackageIds = new HashSet<string>(orderedPackages.Select(package => package.Id), StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < discoveredPackages.Count; i++)
            {
                var package = discoveredPackages[i];
                if (package == null || !package.IsEnabled)
                {
                    continue;
                }

                if (!orderedPackageIds.Contains(package.Id))
                {
                    package.IsEnabled = false;
                    package.ValidationMessages.Add(string.Format("Add-on '{0}' is part of a dependency cycle and will not be loaded.", package.DisplayLabel));
                }
            }

            catalog.AllPackages.AddRange(discoveredPackages.OrderBy(package => package.SortKey, StringComparer.OrdinalIgnoreCase));
            catalog.Packages.AddRange(orderedPackages);

            for (int i = 0; i < catalog.AllPackages.Count; i++)
            {
                catalog.ValidationMessages.AddRange(catalog.AllPackages[i].ValidationMessages);
            }

            return catalog;
        }

        internal static string NormalizeCapabilityForLookup(string capability)
        {
            return NormalizeCapability(capability);
        }

        private static LsolAddonPackage ParsePackage(string packageDirectory, string manifestPath)
        {
            var package = new LsolAddonPackage(packageDirectory, manifestPath);
            XDocument document;

            try
            {
                document = XDocument.Load(manifestPath, LoadOptions.None);
            }
            catch (Exception ex)
            {
                package.HasBlockingFailure = true;
                package.ValidationMessages.Add(string.Format("Add-on folder '{0}' could not load addon.xml: {1}", package.FolderName, ex.Message));
                return package;
            }

            if (document.Root == null || !document.Root.Name.LocalName.Equals("Addon", StringComparison.OrdinalIgnoreCase))
            {
                package.HasBlockingFailure = true;
                package.ValidationMessages.Add(string.Format("Add-on folder '{0}' has an invalid addon.xml root element.", package.FolderName));
                return package;
            }

            var root = document.Root;
            package.Id = ReadAttribute(root, "id");
            package.Category = ReadAttribute(root, "category");
            package.RawVersion = ReadAttribute(root, "version");

            var metadata = root.Element("Metadata");
            package.Name = ReadAttribute(metadata, "name");
            package.Author = ReadAttribute(metadata, "author");
            package.Description = ReadAttribute(metadata, "description", ReadElementValue(metadata != null ? metadata.Element("Description") : null));
            package.Website = ReadAttribute(metadata, "website");
            package.Source = ReadAttribute(metadata, "source");

            int apiVersion;
            if (!int.TryParse(ReadAttribute(root, "lsolApiVersion"), out apiVersion))
            {
                package.HasBlockingFailure = true;
                package.ValidationMessages.Add(string.Format("Add-on '{0}' has a missing or invalid lsolApiVersion.", package.DisplayLabel));
            }
            else
            {
                package.LsolApiVersion = apiVersion;
                if (package.LsolApiVersion != CurrentApiVersion)
                {
                    package.HasBlockingFailure = true;
                    package.ValidationMessages.Add(string.Format("Add-on '{0}' targets LSOL add-on API version {1}, but this build supports API version {2}.", package.DisplayLabel, package.LsolApiVersion, CurrentApiVersion));
                }
            }

            if (!TryParseVersion(package.RawVersion, out var packageVersion))
            {
                package.HasBlockingFailure = true;
                package.ValidationMessages.Add(string.Format("Add-on '{0}' has a missing or invalid version attribute.", package.DisplayLabel));
            }
            else
            {
                package.PackageVersion = packageVersion;
            }

            if (string.IsNullOrWhiteSpace(package.Id))
            {
                package.HasBlockingFailure = true;
                package.ValidationMessages.Add(string.Format("Add-on folder '{0}' is missing the required manifest id.", package.FolderName));
            }

            if (string.IsNullOrWhiteSpace(package.Name))
            {
                package.HasBlockingFailure = true;
                package.ValidationMessages.Add(string.Format("Add-on '{0}' is missing the required metadata name.", package.DisplayLabel));
            }

            if (string.IsNullOrWhiteSpace(package.Author))
            {
                package.HasBlockingFailure = true;
                package.ValidationMessages.Add(string.Format("Add-on '{0}' is missing the required metadata author.", package.DisplayLabel));
            }

            if (string.IsNullOrWhiteSpace(package.Description))
            {
                package.HasBlockingFailure = true;
                package.ValidationMessages.Add(string.Format("Add-on '{0}' is missing the required metadata description.", package.DisplayLabel));
            }

            if (string.IsNullOrWhiteSpace(package.Category) || !KnownCategories.Contains(package.Category))
            {
                package.HasBlockingFailure = true;
                package.ValidationMessages.Add(string.Format("Add-on '{0}' declares unsupported category '{1}'.", package.DisplayLabel, package.Category));
            }

            var compatibility = root.Element("Compatibility");
            if (!TryParseOptionalVersion(ReadAttribute(compatibility, "minLSOLVersion"), out var minLsolVersion))
            {
                package.HasBlockingFailure = true;
                package.ValidationMessages.Add(string.Format("Add-on '{0}' has a missing or invalid minLSOLVersion.", package.DisplayLabel));
            }
            else
            {
                package.MinLsolVersion = minLsolVersion;
            }

            if (!TryParseOptionalVersion(ReadAttribute(compatibility, "maxTestedLSOLVersion"), out var maxTestedVersion))
            {
                package.HasBlockingFailure = true;
                package.ValidationMessages.Add(string.Format("Add-on '{0}' has a missing or invalid maxTestedLSOLVersion.", package.DisplayLabel));
            }
            else
            {
                package.MaxTestedLsolVersion = maxTestedVersion;
            }

            ParseRequirements(root.Element("Dependencies"), "Dependency", package.Dependencies, package, "dependency");
            ParseRequirements(root.Element("Conflicts"), "Conflict", package.Conflicts, package, "conflict");
            ParseCapabilities(root.Element("Capabilities"), package);
            ParseContentDirectories(root.Element("Content"), package);
            ParsePluginMetadata(root.Element("Plugin"), package);
            ResolveActiveCapabilities(package);

            return package;
        }

        private static void MarkDuplicateIds(ICollection<LsolAddonPackage> packages)
        {
            if (packages == null)
            {
                return;
            }

            foreach (var group in packages
                .Where(package => package != null && !string.IsNullOrWhiteSpace(package.Id))
                .GroupBy(package => package.Id, StringComparer.OrdinalIgnoreCase)
                .Where(group => group.Count() > 1))
            {
                var duplicateFolders = string.Join(", ", group.Select(package => package.FolderName).OrderBy(name => name, StringComparer.OrdinalIgnoreCase));
                foreach (var package in group)
                {
                    package.HasBlockingFailure = true;
                    package.HasDuplicateId = true;
                    package.ValidationMessages.Add(string.Format("Add-on '{0}' is duplicated by multiple folders ({1}) and will not be loaded.", package.DisplayLabel, duplicateFolders));
                }
            }
        }

        private static void FinalizePackageAvailability(LsolAddonPackage package, IReadOnlyDictionary<string, LsolAddonPackage> packagesById, Version runtimeVersion)
        {
            if (package == null || package.HasBlockingFailure)
            {
                return;
            }

            if (package.MinLsolVersion == null)
            {
                package.HasBlockingFailure = true;
                package.ValidationMessages.Add(string.Format("Add-on '{0}' is missing the required minLSOLVersion compatibility value.", package.DisplayLabel));
                return;
            }

            if (package.MaxTestedLsolVersion == null)
            {
                package.HasBlockingFailure = true;
                package.ValidationMessages.Add(string.Format("Add-on '{0}' is missing the required maxTestedLSOLVersion compatibility value.", package.DisplayLabel));
                return;
            }

            if (runtimeVersion != null && runtimeVersion.CompareTo(package.MinLsolVersion) < 0)
            {
                package.HasBlockingFailure = true;
                package.ValidationMessages.Add(string.Format("Add-on '{0}' requires LSOL {1} or newer, but this build reports {2}.", package.DisplayLabel, package.MinLsolVersion, runtimeVersion));
                return;
            }

            if (runtimeVersion != null && package.MaxTestedLsolVersion != null && runtimeVersion.CompareTo(package.MaxTestedLsolVersion) > 0)
            {
                package.ValidationMessages.Add(string.Format("Add-on '{0}' was last tested against LSOL {1}; continuing on LSOL {2}.", package.DisplayLabel, package.MaxTestedLsolVersion, runtimeVersion));
            }

            for (int i = 0; i < package.Dependencies.Count; i++)
            {
                var dependency = package.Dependencies[i];
                if (dependency == null || string.IsNullOrWhiteSpace(dependency.Id))
                {
                    continue;
                }

                LsolAddonPackage dependencyPackage;
                if (!packagesById.TryGetValue(dependency.Id, out dependencyPackage) || dependencyPackage.HasBlockingFailure || dependencyPackage.ActiveCapabilities.Count <= 0)
                {
                    package.HasBlockingFailure = true;
                    package.ValidationMessages.Add(string.Format("Add-on '{0}' depends on '{1}', but that package is missing or unavailable.", package.DisplayLabel, dependency.Id));
                    return;
                }

                if (!MatchesRequirement(dependencyPackage.PackageVersion, dependency))
                {
                    package.HasBlockingFailure = true;
                    package.ValidationMessages.Add(string.Format("Add-on '{0}' depends on '{1}', but the installed version {2} does not satisfy the manifest requirement.", package.DisplayLabel, dependency.Id, dependencyPackage.PackageVersion));
                    return;
                }
            }

            for (int i = 0; i < package.Conflicts.Count; i++)
            {
                var conflict = package.Conflicts[i];
                if (conflict == null || string.IsNullOrWhiteSpace(conflict.Id))
                {
                    continue;
                }

                LsolAddonPackage conflictingPackage;
                if (!packagesById.TryGetValue(conflict.Id, out conflictingPackage) || conflictingPackage.HasBlockingFailure)
                {
                    continue;
                }

                if (!MatchesRequirement(conflictingPackage.PackageVersion, conflict))
                {
                    continue;
                }

                package.HasBlockingFailure = true;
                package.ValidationMessages.Add(string.Format("Add-on '{0}' conflicts with installed add-on '{1}'.", package.DisplayLabel, conflictingPackage.DisplayLabel));
                return;
            }

            if (package.ActiveCapabilities.Count <= 0)
            {
                package.ValidationMessages.Add(string.Format("Add-on '{0}' does not provide any currently loadable LSOL content. Metadata was discovered, but the package remains inactive.", package.DisplayLabel));
                return;
            }

            package.IsEnabled = true;
        }

        private static List<LsolAddonPackage> OrderPackages(ICollection<LsolAddonPackage> packages)
        {
            var ordered = new List<LsolAddonPackage>();
            if (packages == null || packages.Count <= 0)
            {
                return ordered;
            }

            var enabledPackages = packages
                .Where(package => package != null && !package.HasBlockingFailure && package.IsEnabled)
                .OrderBy(package => package.SortKey, StringComparer.OrdinalIgnoreCase)
                .ToList();
            var packagesById = enabledPackages.ToDictionary(package => package.Id, StringComparer.OrdinalIgnoreCase);
            var indegree = enabledPackages.ToDictionary(package => package.Id, package => 0, StringComparer.OrdinalIgnoreCase);
            var edges = enabledPackages.ToDictionary(package => package.Id, package => new List<string>(), StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < enabledPackages.Count; i++)
            {
                var package = enabledPackages[i];
                for (int j = 0; j < package.Dependencies.Count; j++)
                {
                    var dependency = package.Dependencies[j];
                    if (dependency == null || string.IsNullOrWhiteSpace(dependency.Id) || !packagesById.ContainsKey(dependency.Id))
                    {
                        continue;
                    }

                    edges[dependency.Id].Add(package.Id);
                    indegree[package.Id] = indegree[package.Id] + 1;
                }
            }

            var ready = new List<LsolAddonPackage>(enabledPackages.Where(package => indegree[package.Id] == 0));
            while (ready.Count > 0)
            {
                ready.Sort((left, right) => StringComparer.OrdinalIgnoreCase.Compare(left.SortKey, right.SortKey));
                var next = ready[0];
                ready.RemoveAt(0);
                ordered.Add(next);

                for (int i = 0; i < edges[next.Id].Count; i++)
                {
                    var dependentId = edges[next.Id][i];
                    indegree[dependentId] = indegree[dependentId] - 1;
                    if (indegree[dependentId] == 0)
                    {
                        ready.Add(packagesById[dependentId]);
                    }
                }
            }

            return ordered;
        }

        private static void ParseRequirements(XElement section, string elementName, ICollection<LsolAddonPackageRequirement> target, LsolAddonPackage package, string requirementLabel)
        {
            if (target == null || package == null || section == null)
            {
                return;
            }

            foreach (var element in section.Elements(elementName))
            {
                var id = ReadAttribute(element, "id");
                if (string.IsNullOrWhiteSpace(id))
                {
                    package.HasBlockingFailure = true;
                    package.ValidationMessages.Add(string.Format("Add-on '{0}' contains a {1} entry with no id.", package.DisplayLabel, requirementLabel));
                    continue;
                }

                if (!TryParseOptionalVersion(ReadAttribute(element, "minVersion"), out var minVersion)
                    || !TryParseOptionalVersion(ReadAttribute(element, "maxVersion"), out var maxVersion))
                {
                    package.HasBlockingFailure = true;
                    package.ValidationMessages.Add(string.Format("Add-on '{0}' contains an invalid version range for {1} '{2}'.", package.DisplayLabel, requirementLabel, id));
                    continue;
                }

                target.Add(new LsolAddonPackageRequirement
                {
                    Id = id,
                    MinVersion = minVersion,
                    MaxVersion = maxVersion,
                });
            }
        }

        private static void ParseCapabilities(XElement capabilitiesElement, LsolAddonPackage package)
        {
            if (package == null || capabilitiesElement == null)
            {
                return;
            }

            foreach (var capabilityElement in capabilitiesElement.Elements("Capability"))
            {
                var rawName = ReadAttribute(capabilityElement, "name");
                var normalizedCapability = NormalizeCapability(rawName);
                if (string.IsNullOrWhiteSpace(normalizedCapability))
                {
                    package.ValidationMessages.Add(string.Format("Add-on '{0}' declares unsupported capability '{1}'.", package.DisplayLabel, rawName));
                    continue;
                }

                package.DeclaredCapabilities.Add(normalizedCapability);
            }
        }

        private static void ParseContentDirectories(XElement contentElement, LsolAddonPackage package)
        {
            if (package == null || contentElement == null)
            {
                return;
            }

            foreach (var directoryElement in contentElement.Elements("Directory"))
            {
                var rawType = ReadAttribute(directoryElement, "type");
                var relativePath = ReadAttribute(directoryElement, "path");
                if (string.IsNullOrWhiteSpace(rawType) || string.IsNullOrWhiteSpace(relativePath))
                {
                    package.ValidationMessages.Add(string.Format("Add-on '{0}' contains a content directory entry with missing type or path.", package.DisplayLabel));
                    continue;
                }

                string capability;
                if (!TryNormalizeDirectoryType(rawType, out capability))
                {
                    package.ValidationMessages.Add(string.Format("Add-on '{0}' declares unsupported content directory type '{1}'.", package.DisplayLabel, rawType));
                    continue;
                }

                string resolvedPath;
                if (!TryResolvePackagePath(package.PackageDirectory, relativePath, out resolvedPath))
                {
                    package.ValidationMessages.Add(string.Format("Add-on '{0}' uses content path '{1}' that resolves outside the package folder.", package.DisplayLabel, relativePath));
                    continue;
                }

                if (package.ContentDirectories.ContainsKey(capability))
                {
                    package.ValidationMessages.Add(string.Format("Add-on '{0}' declares duplicate content directory entries for capability '{1}'.", package.DisplayLabel, capability));
                    continue;
                }

                package.ContentDirectories[capability] = new LsolAddonContentDirectory
                {
                    Type = rawType,
                    Capability = capability,
                    RelativePath = relativePath,
                    ResolvedPath = resolvedPath,
                };
            }
        }

        private static void ParsePluginMetadata(XElement pluginElement, LsolAddonPackage package)
        {
            if (package == null || pluginElement == null)
            {
                return;
            }

            package.Plugin = new LsolAddonPluginMetadata
            {
                AssemblyPath = ReadAttribute(pluginElement, "assembly"),
                EntryType = ReadAttribute(pluginElement, "entryType"),
                EnabledByDefault = ReadBoolAttribute(pluginElement, "enabledByDefault", false),
            };

            package.ValidationMessages.Add(string.Format("Add-on '{0}' includes plugin assembly metadata, but third-party assembly loading is not implemented in this LSOL version.", package.DisplayLabel));
        }

        private static void ResolveActiveCapabilities(LsolAddonPackage package)
        {
            if (package == null)
            {
                return;
            }

            foreach (var pair in package.ContentDirectories)
            {
                var contentDirectory = pair.Value;
                if (contentDirectory == null)
                {
                    continue;
                }

                if (!Directory.Exists(contentDirectory.ResolvedPath))
                {
                    package.ValidationMessages.Add(string.Format("Add-on '{0}' content directory '{1}' is missing.", package.DisplayLabel, contentDirectory.RelativePath));
                    continue;
                }

                if (!package.DeclaredCapabilities.Contains(contentDirectory.Capability))
                {
                    package.ValidationMessages.Add(string.Format("Add-on '{0}' provides content directory '{1}' without explicitly declaring capability '{2}'. Treating the directory as loadable content.", package.DisplayLabel, contentDirectory.RelativePath, contentDirectory.Capability));
                }

                if (SupportedCapabilities.Contains(contentDirectory.Capability))
                {
                    package.ActiveCapabilities.Add(contentDirectory.Capability);
                }
                else
                {
                    package.ValidationMessages.Add(string.Format("Add-on '{0}' declares content directory '{1}' for capability '{2}', but that capability is not loaded in this LSOL version.", package.DisplayLabel, contentDirectory.RelativePath, contentDirectory.Capability));
                }
            }

            foreach (var capability in package.DeclaredCapabilities)
            {
                if (!SupportedCapabilities.Contains(capability))
                {
                    package.ValidationMessages.Add(string.Format("Add-on '{0}' declares capability '{1}', but that capability is not loaded in this LSOL version.", package.DisplayLabel, capability));
                    continue;
                }

                if (CapabilityRequiresContentDirectory(capability) && !package.ContentDirectories.ContainsKey(capability))
                {
                    package.ValidationMessages.Add(string.Format("Add-on '{0}' declares capability '{1}' but does not provide a matching content directory.", package.DisplayLabel, capability));
                }
            }
        }

        private static Version ResolveRuntimeVersion()
        {
            try
            {
                var version = Assembly.GetExecutingAssembly().GetName().Version;
                if (version != null && version.CompareTo(new Version(0, 0, 0, 0)) > 0)
                {
                    return version;
                }
            }
            catch
            {
            }

            return FallbackRuntimeVersion;
        }

        private static bool MatchesRequirement(Version version, LsolAddonPackageRequirement requirement)
        {
            if (requirement == null)
            {
                return true;
            }

            if (requirement.MinVersion != null && version.CompareTo(requirement.MinVersion) < 0)
            {
                return false;
            }

            if (requirement.MaxVersion != null && version.CompareTo(requirement.MaxVersion) > 0)
            {
                return false;
            }

            return true;
        }

        private static bool CapabilityRequiresContentDirectory(string capability)
        {
            return SupportedCapabilities.Contains(capability);
        }

        private static bool TryNormalizeDirectoryType(string rawType, out string capability)
        {
            capability = string.Empty;
            if (string.IsNullOrWhiteSpace(rawType))
            {
                return false;
            }

            return DirectoryTypeToCapability.TryGetValue(rawType.Trim(), out capability);
        }

        private static string NormalizeCapability(string capability)
        {
            if (string.IsNullOrWhiteSpace(capability))
            {
                return string.Empty;
            }

            string normalizedCapability;
            return CapabilityAliases.TryGetValue(capability.Trim(), out normalizedCapability)
                ? normalizedCapability
                : string.Empty;
        }

        private static bool TryResolvePackagePath(string packageDirectory, string relativePath, out string resolvedPath)
        {
            resolvedPath = string.Empty;
            if (string.IsNullOrWhiteSpace(packageDirectory) || string.IsNullOrWhiteSpace(relativePath))
            {
                return false;
            }

            try
            {
                if (Path.IsPathRooted(relativePath))
                {
                    return false;
                }

                var baseDirectory = EnsureTrailingSeparator(Path.GetFullPath(packageDirectory));
                var candidatePath = Path.GetFullPath(Path.Combine(baseDirectory, relativePath));
                if (!candidatePath.StartsWith(baseDirectory, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                resolvedPath = candidatePath;
                return true;
            }
            catch
            {
                resolvedPath = string.Empty;
                return false;
            }
        }

        private static string EnsureTrailingSeparator(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            return path.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
                || path.EndsWith(Path.AltDirectorySeparatorChar.ToString(), StringComparison.Ordinal)
                ? path
                : path + Path.DirectorySeparatorChar;
        }

        private static bool TryParseVersion(string raw, out Version version)
        {
            version = null;
            return !string.IsNullOrWhiteSpace(raw)
                && Version.TryParse(raw.Trim(), out version);
        }

        private static bool TryParseOptionalVersion(string raw, out Version version)
        {
            version = null;
            return string.IsNullOrWhiteSpace(raw)
                || Version.TryParse(raw.Trim(), out version);
        }

        private static string ReadAttribute(XElement element, string name, string fallback = "")
        {
            return element != null && element.Attribute(name) != null
                ? (element.Attribute(name).Value ?? string.Empty).Trim()
                : fallback;
        }

        private static string ReadElementValue(XElement element, string fallback = "")
        {
            return element != null
                ? (element.Value ?? string.Empty).Trim()
                : fallback;
        }

        private static bool ReadBoolAttribute(XElement element, string name, bool fallback)
        {
            if (element == null || element.Attribute(name) == null)
            {
                return fallback;
            }

            bool parsed;
            return bool.TryParse(element.Attribute(name).Value, out parsed)
                ? parsed
                : fallback;
        }
    }

    public sealed class LsolAddonPackage
    {
        public LsolAddonPackage(string packageDirectory, string manifestPath)
        {
            PackageDirectory = packageDirectory ?? string.Empty;
            ManifestPath = manifestPath ?? string.Empty;
            Id = string.Empty;
            Name = string.Empty;
            RawVersion = string.Empty;
            Category = string.Empty;
            Author = string.Empty;
            Description = string.Empty;
            Website = string.Empty;
            Source = string.Empty;
            Dependencies = new List<LsolAddonPackageRequirement>();
            Conflicts = new List<LsolAddonPackageRequirement>();
            DeclaredCapabilities = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            ActiveCapabilities = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            ContentDirectories = new Dictionary<string, LsolAddonContentDirectory>(StringComparer.OrdinalIgnoreCase);
            ValidationMessages = new List<string>();
            PackageVersion = new Version(0, 0);
        }

        public string PackageDirectory { get; }

        public string ManifestPath { get; }

        public string Id { get; set; }

        public string Name { get; set; }

        public string RawVersion { get; set; }

        public Version PackageVersion { get; set; }

        public string Category { get; set; }

        public string Author { get; set; }

        public string Description { get; set; }

        public string Website { get; set; }

        public string Source { get; set; }

        public int LsolApiVersion { get; set; }

        public Version MinLsolVersion { get; set; }

        public Version MaxTestedLsolVersion { get; set; }

        public List<LsolAddonPackageRequirement> Dependencies { get; }

        public List<LsolAddonPackageRequirement> Conflicts { get; }

        public HashSet<string> DeclaredCapabilities { get; }

        public HashSet<string> ActiveCapabilities { get; }

        public Dictionary<string, LsolAddonContentDirectory> ContentDirectories { get; }

        public LsolAddonPluginMetadata Plugin { get; set; }

        public List<string> ValidationMessages { get; }

        public bool HasBlockingFailure { get; set; }

        public bool HasDuplicateId { get; set; }

        public bool IsEnabled { get; set; }

        public string FolderName
        {
            get { return Path.GetFileName(PackageDirectory); }
        }

        public string DisplayLabel
        {
            get { return string.IsNullOrWhiteSpace(Id) ? FolderName : Id; }
        }

        public string SortKey
        {
            get { return string.IsNullOrWhiteSpace(Id) ? FolderName : Id; }
        }

        public bool TryGetContentDirectory(string capability, out string directoryPath)
        {
            directoryPath = string.Empty;
            var normalizedCapability = LsolAddonCatalog.NormalizeCapabilityForLookup(capability);
            if (string.IsNullOrWhiteSpace(normalizedCapability))
            {
                return false;
            }

            LsolAddonContentDirectory contentDirectory;
            if (!ContentDirectories.TryGetValue(normalizedCapability, out contentDirectory) || contentDirectory == null)
            {
                return false;
            }

            directoryPath = contentDirectory.ResolvedPath ?? string.Empty;
            return !string.IsNullOrWhiteSpace(directoryPath);
        }
    }

    public sealed class LsolAddonPackageRequirement
    {
        public string Id { get; set; }

        public Version MinVersion { get; set; }

        public Version MaxVersion { get; set; }
    }

    public sealed class LsolAddonContentDirectory
    {
        public string Type { get; set; }

        public string Capability { get; set; }

        public string RelativePath { get; set; }

        public string ResolvedPath { get; set; }
    }

    public sealed class LsolAddonPluginMetadata
    {
        public string AssemblyPath { get; set; }

        public string EntryType { get; set; }

        public bool EnabledByDefault { get; set; }
    }
}