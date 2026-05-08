using System;
using System.Collections.Generic;
using GTA;
using GTA.Math;
using LSOL.Domain;

namespace LSOL.Systems
{
    public sealed class IndustryRefuelService
    {
        private readonly FleetManager _fleetManager;
        private readonly VehicleFuelSystem _vehicleFuelSystem;
        private readonly IndustryManager _industryManager;
        private readonly GlobalMarketManager _globalMarket;
        private readonly Func<float> _getProfit;
        private readonly Action<float> _deductProfit;
        private readonly Func<Industry, Vector3> _getIndustryMarkerPosition;
        private readonly float _interactionDistance;

        public IndustryRefuelService(
            FleetManager fleetManager,
            VehicleFuelSystem vehicleFuelSystem,
            IndustryManager industryManager,
            GlobalMarketManager globalMarket,
            Func<float> getProfit,
            Action<float> deductProfit,
            Func<Industry, Vector3> getIndustryMarkerPosition,
            float interactionDistance)
        {
            _fleetManager = fleetManager ?? throw new ArgumentNullException(nameof(fleetManager));
            _vehicleFuelSystem = vehicleFuelSystem ?? throw new ArgumentNullException(nameof(vehicleFuelSystem));
            _industryManager = industryManager ?? throw new ArgumentNullException(nameof(industryManager));
            _globalMarket = globalMarket ?? throw new ArgumentNullException(nameof(globalMarket));
            _getProfit = getProfit;
            _deductProfit = deductProfit;
            _getIndustryMarkerPosition = getIndustryMarkerPosition ?? throw new ArgumentNullException(nameof(getIndustryMarkerPosition));
            _interactionDistance = interactionDistance;
        }

        public bool TryRefuel(Industry industry, Ped player, out string message)
        {
            message = string.Empty;

            Vehicle cargoVehicle;
            Vehicle poweredVehicle;
            VehicleFuelTelemetry fuelTelemetry;
            if (!TryGetRefuelContext(industry, player, out cargoVehicle, out poweredVehicle, out fuelTelemetry, out message))
            {
                return false;
            }

            var litersNeeded = Math.Max(0f, fuelTelemetry.CapacityLiters - fuelTelemetry.CurrentLiters);
            if (litersNeeded <= 0.05f)
            {
                message = string.Format("Fuel tank already full ({0:0}/{1:0}L).", fuelTelemetry.CurrentLiters, fuelTelemetry.CapacityLiters);
                return false;
            }

            var availableStockLiters = Math.Max(0f, industry.GetStock("Fuel") * 1000f);
            if (availableStockLiters <= 0.05f)
            {
                message = "Station out of fuel.";
                return false;
            }

            var currentProfit = _getProfit != null ? _getProfit() : 0f;
            var pricePerTon = Math.Max(0f, _globalMarket.GetUnitPrice("Fuel"));
            var affordableLiters = industry.RefuelIsFree || pricePerTon <= 0.001f
                ? litersNeeded
                : Math.Max(0f, (currentProfit / pricePerTon) * 1000f);

            if (!industry.RefuelIsFree && affordableLiters <= 0.05f)
            {
                message = "Insufficient funds to refuel.";
                return false;
            }

            var litersToDispense = Math.Min(litersNeeded, availableStockLiters);
            if (!industry.RefuelIsFree)
            {
                litersToDispense = Math.Min(litersToDispense, affordableLiters);
            }

            if (litersToDispense <= 0.05f)
            {
                message = industry.RefuelIsFree ? "Station out of fuel." : "Insufficient funds to refuel.";
                return false;
            }

            float dispensedLiters;
            if (!_industryManager.TryDispenseFuel(industry, litersToDispense, out dispensedLiters) || dispensedLiters <= 0.05f)
            {
                message = "Station out of fuel.";
                return false;
            }

            var addedLiters = _vehicleFuelSystem.AddFuel(poweredVehicle, dispensedLiters);
            if (addedLiters <= 0.05f)
            {
                industry.AddInput("Fuel", dispensedLiters / 1000f);
                message = "Fuel tank already full.";
                return false;
            }

            var pricePaid = industry.RefuelIsFree ? 0f : _industryManager.ComputeFuelRefillPrice(addedLiters, _globalMarket);
            if (pricePaid > 0f && _deductProfit != null)
            {
                _deductProfit(pricePaid);
            }

            var resultingTank = Math.Min(fuelTelemetry.CapacityLiters, fuelTelemetry.CurrentLiters + addedLiters);
            var notes = new List<string>();
            if (availableStockLiters + 0.05f < litersNeeded)
            {
                notes.Add("station out of fuel");
            }

            if (!industry.RefuelIsFree && affordableLiters + 0.05f < litersNeeded)
            {
                notes.Add("insufficient funds");
            }

            var priceText = industry.RefuelIsFree
                ? "Free at office station"
                : string.Format("Paid {0}", ModFormatting.FormatMoney(pricePaid));
            var noteText = notes.Count > 0
                ? string.Format(" | {0}", string.Join(" | ", notes))
                : string.Empty;

            message = string.Format(
                "Refueled {0:0}L. Tank {1:0}/{2:0}L | {3}{4}",
                addedLiters,
                resultingTank,
                fuelTelemetry.CapacityLiters,
                priceText,
                noteText);
            return true;
        }

        private bool TryGetRefuelContext(Industry industry, Ped player, out Vehicle cargoVehicle, out Vehicle poweredVehicle, out VehicleFuelTelemetry fuelTelemetry, out string error)
        {
            cargoVehicle = null;
            poweredVehicle = null;
            fuelTelemetry = null;
            error = string.Empty;

            if (industry == null)
            {
                error = "No target industry.";
                return false;
            }

            if (!industry.IsGasStation)
            {
                error = "Refueling is only available at petrol stations.";
                return false;
            }

            if (player == null || !player.Exists())
            {
                error = "Player unavailable.";
                return false;
            }

            if (player.Position.DistanceTo(_getIndustryMarkerPosition(industry)) > _interactionDistance + 1.2f)
            {
                error = "Move closer to the petrol station marker.";
                return false;
            }

            if (!_fleetManager.TryResolveVehicleContext(player, out poweredVehicle, out cargoVehicle)
                || poweredVehicle == null
                || !poweredVehicle.Exists())
            {
                error = "Bring a powered cargo vehicle close to the petrol station.";
                return false;
            }

            _vehicleFuelSystem.EnsureTrackedVehicle(poweredVehicle);
            fuelTelemetry = _vehicleFuelSystem.GetTelemetry(poweredVehicle, cargoVehicle);
            if (fuelTelemetry == null || fuelTelemetry.CapacityLiters <= 0.001f)
            {
                error = "No powered cargo vehicle with a fuel tank is in range.";
                return false;
            }

            return true;
        }
    }
}