using System;
using System.Globalization;
using System.IO;
using System.Xml.Linq;
using LSOL.Domain;

namespace LSOL.Config
{
    internal static class XmlConfigImport
    {
        public static bool TryPopulateDealershipVehicles(string configDirectory, ExternalConfigCatalog catalog)
        {
            if (catalog == null)
            {
                return false;
            }

            var filePath = Path.Combine(configDirectory ?? string.Empty, "dealership.xml");
            if (!File.Exists(filePath))
            {
                catalog.ValidationMessages.Add("dealership.xml missing. Personal vehicle dealership will be unavailable.");
                return false;
            }

            XDocument document;
            try
            {
                document = XDocument.Load(filePath, LoadOptions.None);
            }
            catch (Exception ex)
            {
                catalog.ValidationMessages.Add(string.Format("dealership.xml could not be loaded: {0}", ex.Message));
                return false;
            }

            var modelNodes = document.Root != null
                ? document.Root.Element("Models")
                : null;
            if (modelNodes == null)
            {
                catalog.ValidationMessages.Add("dealership.xml is missing the Models section.");
                return false;
            }

            foreach (var element in modelNodes.Elements("Model"))
            {
                var displayName = ReadAttribute(element, "name");
                var modelName = ReadAttribute(element, "model");
                var category = ReadAttribute(element, "category");
                var priceRaw = ReadAttribute(element, "price");
                float price;

                if (string.IsNullOrWhiteSpace(modelName) || string.IsNullOrWhiteSpace(displayName))
                {
                    catalog.ValidationMessages.Add("dealership.xml contains a model entry with a missing name or model attribute.");
                    continue;
                }

                if (!float.TryParse(priceRaw, NumberStyles.Float, CultureInfo.InvariantCulture, out price) &&
                    !float.TryParse(priceRaw, NumberStyles.Float, CultureInfo.CurrentCulture, out price))
                {
                    catalog.ValidationMessages.Add(string.Format("dealership.xml vehicle '{0}' has an invalid price '{1}'.", displayName, priceRaw));
                    continue;
                }

                catalog.PersonalVehicleDefinitions.Add(new DealershipVehicleDefinition
                {
                    VehicleId = modelName.Trim(),
                    DisplayName = displayName.Trim(),
                    ModelName = modelName.Trim(),
                    Category = category,
                    Price = Math.Max(0f, price),
                });
            }

            return catalog.PersonalVehicleDefinitions.Count > 0;
        }

        private static string ReadAttribute(XElement element, string name)
        {
            return element != null && element.Attribute(name) != null
                ? (element.Attribute(name).Value ?? string.Empty).Trim()
                : string.Empty;
        }
    }
}