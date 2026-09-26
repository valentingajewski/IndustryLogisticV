using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using GTA.Math;
using LSOL.Config;

namespace LSOL.Systems
{
    /// <summary>
    /// One JobVehicles.xml entry of a single side job type. Shared by every side job so the file is
    /// opened, filtered and parsed in one place instead of once per job.
    /// </summary>
    public sealed class SideJobVehicleDefinition
    {
        public int Id { get; set; }

        public string Name { get; set; }

        public string ModelName { get; set; }

        /// <summary>The type="..." attribute the entry was loaded for.</summary>
        public string TypeName { get; set; }

        public int UnlockLevel { get; set; }

        /// <summary>Passenger seats of the type-specific <c>seats</c> attribute (Bus).</summary>
        public int Seats { get; set; }

        /// <summary>Capacity of the type-specific payload attribute (towCapacityTons, garbageTons, mealCapacity).</summary>
        public float CapacityTons { get; set; }

        public float Price { get; set; }

        public float DailyRent { get; set; }

        public float FuelCapacityLiters { get; set; }
    }

    /// <summary>
    /// Shared configuration plumbing for the side job systems: the JobVehicles.xml loader, the
    /// Districts.xml loader and the point-to-district resolver, plus the small XML/attribute and
    /// vector helpers every job needs. Extracted from BusSideJobSystem (which keeps its public
    /// wrappers delegating here) so garbage, bus and towing no longer each carry a copy.
    /// </summary>
    internal static class SideJobConfigLoader
    {
        public static XDocument TryLoadDocument(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                return null;
            }

            try
            {
                return XDocument.Load(filePath);
            }
            catch
            {
                return null;
            }
        }

        public static string BuildSideJobsPath(string configDirectory, string fileName)
        {
            return Path.Combine(configDirectory ?? string.Empty, "SideJobs", fileName ?? string.Empty);
        }

        /// <summary>
        /// Every JobVehicle element of the given type. A missing or unreadable file yields nothing, so
        /// a legacy install without the file still loads.
        /// </summary>
        public static IEnumerable<XElement> EnumerateJobVehicles(string configDirectory, string typeName)
        {
            var document = TryLoadDocument(BuildSideJobsPath(configDirectory, "JobVehicles.xml"));
            var root = document != null ? document.Root : null;
            if (root == null || string.IsNullOrWhiteSpace(typeName))
            {
                yield break;
            }

            foreach (var element in root.Elements("JobVehicle"))
            {
                if (!string.Equals(ReadAttribute(element, "type"), typeName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(ReadAttribute(element, "model")))
                {
                    continue;
                }

                yield return element;
            }
        }

        /// <summary>
        /// Loads the entries of one job type. <paramref name="capacityAttribute"/> names the
        /// type-specific payload attribute (garbageTons, mealCapacity, ...) and is optional.
        /// </summary>
        public static List<SideJobVehicleDefinition> LoadJobVehicles(
            string configDirectory,
            string typeName,
            string capacityAttribute = null,
            float defaultCapacity = 0f,
            float defaultFuelCapacityLiters = 120f)
        {
            var result = new List<SideJobVehicleDefinition>();
            foreach (var element in EnumerateJobVehicles(configDirectory, typeName))
            {
                var modelName = ReadAttribute(element, "model");
                result.Add(new SideJobVehicleDefinition
                {
                    Id = ReadIntAttribute(element, "id", 0),
                    Name = ReadAttribute(element, "name", modelName),
                    ModelName = modelName,
                    TypeName = typeName,
                    UnlockLevel = Math.Max(0, ReadIntAttribute(element, "unlockLevel", 0)),
                    Seats = Math.Max(0, ReadIntAttribute(element, "seats", 0)),
                    CapacityTons = string.IsNullOrWhiteSpace(capacityAttribute)
                        ? 0f
                        : Math.Max(0f, ReadFloatAttribute(element, capacityAttribute, defaultCapacity)),
                    Price = Math.Max(0f, ReadFloatAttribute(element, "price", 0f)),
                    DailyRent = Math.Max(0f, ReadFloatAttribute(element, "dailyRent", 0f)),
                    FuelCapacityLiters = Math.Max(0f, ReadFloatAttribute(element, "fuelCapacityLiters", defaultFuelCapacityLiters)),
                });
            }

            return result;
        }

        /// <summary>
        /// Loads the district polygons from Districts.xml, so a side job can resolve the district of a
        /// world point without a TerritoryManager dependency.
        /// </summary>
        public static List<DistrictConfig> LoadDistricts(string configDirectory)
        {
            var result = new List<DistrictConfig>();
            var document = TryLoadDocument(Path.Combine(configDirectory ?? string.Empty, "Districts.xml"));
            var root = document != null ? document.Root : null;
            if (root == null)
            {
                return result;
            }

            foreach (var element in root.Elements("District"))
            {
                var name = ReadAttribute(element, "name");
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                var district = new DistrictConfig
                {
                    Id = ReadAttribute(element, "id"),
                    Name = name,
                };

                foreach (var point in element.Elements("Point"))
                {
                    district.PolygonVertices.Add(new Vector2(
                        ReadFloatAttribute(point, "x", 0f),
                        ReadFloatAttribute(point, "y", 0f)));
                }

                if (district.PolygonVertices.Count >= 3)
                {
                    result.Add(district);
                }
            }

            return result;
        }

        /// <summary>
        /// Resolves the district of a world point: exact polygon containment first (the ray-cast used
        /// by the territory system), then the nearest polygon centroid, because the shipped district
        /// map has coverage gaps (Downtown/South Los Santos seam) and a self-intersecting Vinewood
        /// quad. When several polygons claim the point the nearest centroid wins.
        /// </summary>
        public static string ResolveDistrictName(Vector3 position, IReadOnlyList<DistrictConfig> districts)
        {
            if (districts == null || districts.Count == 0)
            {
                return string.Empty;
            }

            var point = new Vector2(position.X, position.Y);
            DistrictConfig containing = null;
            var containingDistance = float.MaxValue;
            DistrictConfig nearest = null;
            var nearestDistance = float.MaxValue;

            for (int i = 0; i < districts.Count; i++)
            {
                var district = districts[i];
                if (district == null || string.IsNullOrWhiteSpace(district.Name))
                {
                    continue;
                }

                var centroid = district.GetCentroid();
                var distance = DistanceSquared(point, centroid);
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearest = district;
                }

                var inside = false;
                try
                {
                    inside = district.Contains(point);
                }
                catch
                {
                    inside = false;
                }

                if (inside && distance < containingDistance)
                {
                    containingDistance = distance;
                    containing = district;
                }
            }

            var resolved = containing ?? nearest;
            return resolved != null ? resolved.Name : string.Empty;
        }

        public static bool IsKnownDistrict(string districtName, IReadOnlyList<DistrictConfig> districts)
        {
            if (string.IsNullOrWhiteSpace(districtName) || districts == null)
            {
                return false;
            }

            for (int i = 0; i < districts.Count; i++)
            {
                var district = districts[i];
                if (district != null && string.Equals(
                    NormalizeDistrictName(district.Name),
                    NormalizeDistrictName(districtName),
                    StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        public static float DistanceSquared(Vector2 left, Vector2 right)
        {
            var deltaX = left.X - right.X;
            var deltaY = left.Y - right.Y;
            return (deltaX * deltaX) + (deltaY * deltaY);
        }

        public static string ReadAttribute(XElement element, string name, string fallback = null)
        {
            if (element == null || string.IsNullOrWhiteSpace(name))
            {
                return fallback ?? string.Empty;
            }

            var attribute = element.Attribute(name);
            return attribute != null
                ? (attribute.Value ?? fallback ?? string.Empty)
                : (fallback ?? string.Empty);
        }

        public static float ReadFloatAttribute(XElement element, string name, float fallback)
        {
            var raw = ReadAttribute(element, name);
            if (string.IsNullOrWhiteSpace(raw))
            {
                return fallback;
            }

            float parsed;
            if (float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed))
            {
                return parsed;
            }

            if (float.TryParse(raw, NumberStyles.Float, CultureInfo.CurrentCulture, out parsed))
            {
                return parsed;
            }

            return fallback;
        }

        public static int ReadIntAttribute(XElement element, string name, int fallback)
        {
            var raw = ReadAttribute(element, name);
            if (string.IsNullOrWhiteSpace(raw))
            {
                return fallback;
            }

            int parsed;
            if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed))
            {
                return parsed;
            }

            if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.CurrentCulture, out parsed))
            {
                return parsed;
            }

            return fallback;
        }

        public static Vector3 ReadPosition(XElement element)
        {
            if (element == null)
            {
                return Vector3.Zero;
            }

            return new Vector3(
                ReadFloatAttribute(element, "x", 0f),
                ReadFloatAttribute(element, "y", 0f),
                ReadFloatAttribute(element, "z", 0f));
        }

        /// <summary>True when the element carries at least one coordinate attribute.</summary>
        public static bool HasAnyCoordinate(XElement element)
        {
            return element != null
                && (element.Attribute("x") != null || element.Attribute("y") != null || element.Attribute("z") != null);
        }

        /// <summary>Strips whitespace so "West Los Santos" matches the canonical "WestLosSantos".</summary>
        public static string NormalizeDistrictName(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return string.Empty;
            }

            var builder = new StringBuilder(raw.Length);
            for (int i = 0; i < raw.Length; i++)
            {
                var character = raw[i];
                if (!char.IsWhiteSpace(character))
                {
                    builder.Append(character);
                }
            }

            return builder.ToString();
        }

        /// <summary>Reads a comma list attribute (districts="a,b"), trimmed, de-duplicated and normalized.</summary>
        public static List<string> ReadDistrictList(XElement element, string attributeName = "districts")
        {
            var result = new List<string>();
            var raw = ReadAttribute(element, attributeName);
            if (string.IsNullOrWhiteSpace(raw))
            {
                return result;
            }

            var parts = raw.Split(',');
            for (int i = 0; i < parts.Length; i++)
            {
                var trimmed = NormalizeDistrictName(parts[i]);
                if (trimmed.Length == 0 || result.Contains(trimmed, StringComparer.OrdinalIgnoreCase))
                {
                    continue;
                }

                result.Add(trimmed);
            }

            return result;
        }

        /// <summary>
        /// GTA heading (degrees) from one point to another: 0 = north (+Y), 90 = west (-X),
        /// 180 = south, 270 = east (+X).
        /// </summary>
        public static float ComputeBearingDegrees(float fromX, float fromY, float toX, float toY)
        {
            var deltaX = toX - fromX;
            var deltaY = toY - fromY;
            if (Math.Abs(deltaX) < 0.0001f && Math.Abs(deltaY) < 0.0001f)
            {
                return 0f;
            }

            var heading = (float)(Math.Atan2(-deltaX, deltaY) * 180.0 / Math.PI);
            return NormalizeHeading(heading);
        }

        /// <summary>Wraps any heading into the [0, 360) range; invalid input becomes 0.</summary>
        public static float NormalizeHeading(float heading)
        {
            if (float.IsNaN(heading) || float.IsInfinity(heading))
            {
                return 0f;
            }

            var normalized = heading % 360f;
            if (normalized < 0f)
            {
                normalized += 360f;
            }

            return normalized;
        }

        /// <summary>
        /// Moves a point sideways relative to a heading, in metres. Used to step picked road points
        /// off the centre line.
        /// </summary>
        public static Vector3 OffsetPerpendicular(Vector3 position, float headingDegrees, float lateralMeters)
        {
            var radians = headingDegrees * Math.PI / 180.0;
            var rightX = (float)Math.Cos(radians);
            var rightY = (float)Math.Sin(radians);
            return new Vector3(
                position.X + (rightX * lateralMeters),
                position.Y + (rightY * lateralMeters),
                position.Z);
        }

        /// <summary>A point at a given distance and bearing from an origin (used by the fallbacks).</summary>
        public static Vector3 OffsetByBearing(Vector3 origin, float bearingDegrees, float distanceMeters)
        {
            var radians = bearingDegrees * Math.PI / 180.0;
            return new Vector3(
                origin.X - ((float)Math.Sin(radians) * distanceMeters),
                origin.Y + ((float)Math.Cos(radians) * distanceMeters),
                origin.Z);
        }

        public static float DistanceBetween(Vector3 left, Vector3 right)
        {
            var deltaX = left.X - right.X;
            var deltaY = left.Y - right.Y;
            var deltaZ = left.Z - right.Z;
            return (float)Math.Sqrt((deltaX * deltaX) + (deltaY * deltaY) + (deltaZ * deltaZ));
        }
    }
}
