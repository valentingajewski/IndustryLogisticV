using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GTA;
using GTA.Math;
using GTA.Native;
using LSOL;
using LSOL.Config;
using LSOL.Domain;

namespace LSOL.Systems
{
    public sealed class NpcLogisticsManager
    {
        private const float ArrivalDistance = 18f;
        private const int DriveTaskRefreshIntervalMs = 4000;
        private const int InGameMinutesPerDay = 24 * 60;
        private const int InGameMinutesPerWeek = 7 * InGameMinutesPerDay;
        private const int LoadDelayMs = 2200;
        private const int UnloadDelayMs = 2400;
        private const int RetryDelayMs = 9000;
        private const float BaseDriveSpeed = 20f;
        private const int DriveStyle = 786603;
        private const string DefaultNpcModel = "s_m_m_trucker_01";

        private readonly IndustryManager _industryManager;
        private readonly FleetManager _fleetManager;
        private readonly GlobalMarketManager _globalMarket;
        private readonly TerritoryManager _territoryManager;
        private readonly Func<Vector3, Vector3> _getGroundPosition;
        private readonly Func<float> _getProfit;
        private readonly Action<float> _deductProfit;
        private readonly Action<float> _addProfit;
        private readonly Action<string> _showStatus;
        private readonly List<NpcDriverTierDefinition> _driverTiers;
        private readonly List<NpcLogisticsContract> _contracts;
        private readonly Random _random;

        private int _nextContractId;
        private int _lastObservedClockMinute;
        private NpcWeeklyWageDifficulty _weeklyWageDifficulty;

        public NpcLogisticsManager(
            string configPath,
            IndustryManager industryManager,
            FleetManager fleetManager,
            GlobalMarketManager globalMarket,
            Func<Vector3, Vector3> getGroundPosition,
            Func<float> getProfit,
            Action<float> deductProfit,
            Action<float> addProfit,
            Action<string> showStatus,
            TerritoryManager territoryManager = null)
        {
            _industryManager = industryManager;
            _fleetManager = fleetManager;
            _globalMarket = globalMarket;
            _territoryManager = territoryManager;
            _getGroundPosition = getGroundPosition;
            _getProfit = getProfit;
            _deductProfit = deductProfit;
            _addProfit = addProfit;
            _showStatus = showStatus;
            _driverTiers = LoadDriverTiers(configPath);
            _contracts = new List<NpcLogisticsContract>();
            _random = new Random();
            _nextContractId = 1;
            _lastObservedClockMinute = -1;
            _weeklyWageDifficulty = NpcWeeklyWageDifficulty.Standard;
        }

        public IReadOnlyList<NpcDriverTierDefinition> DriverTiers
        {
            get { return _driverTiers; }
        }

        public IReadOnlyList<NpcLogisticsContract> Contracts
        {
            get { return _contracts; }
        }

        public NpcWeeklyWageDifficulty WeeklyWageDifficulty
        {
            get { return _weeklyWageDifficulty; }
        }

        public List<Industry> GetOriginIndustryOptions()
        {
            return _industryManager.Industries
                .Where(CanUseAsOrigin)
                .OrderBy(industry => industry.Name)
                .ToList();
        }

        public List<Industry> GetDestinationIndustryOptions(Industry originIndustry)
        {
            if (originIndustry == null)
            {
                return new List<Industry>();
            }

            return _industryManager.Industries
                .Where(industry => CanUseAsDestination(originIndustry, industry))
                .OrderBy(industry => industry.Name)
                .ToList();
        }

        public List<string> GetResourceOptions(Industry originIndustry, Industry destinationIndustry)
        {
            if (originIndustry == null)
            {
                return new List<string>();
            }

            var resources = originIndustry
                .GetSortedOutputs()
                .Where(resource => !string.IsNullOrWhiteSpace(resource));

            if (destinationIndustry != null)
            {
                resources = resources.Where(destinationIndustry.AcceptsCommodity);
            }

            return resources
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(resource => resource)
                .ToList();
        }

        public float GetContractCost(string commodity, NpcDriverTierDefinition tier)
        {
            if (tier == null || string.IsNullOrWhiteSpace(commodity))
            {
                return 0f;
            }

            var normalizedCommodity = CommodityCatalog.Normalize(commodity);
            return Math.Max(0f, tier.PriceMultiplier * _globalMarket.GetUnitPrice(normalizedCommodity));
        }

        public void SetWeeklyWageDifficulty(NpcWeeklyWageDifficulty difficulty)
        {
            _weeklyWageDifficulty = difficulty;
        }

        public float GetWeeklyWage(NpcDriverTierDefinition tier)
        {
            return tier != null ? tier.GetWeeklyWage(_weeklyWageDifficulty) : 0f;
        }

        public int GetRemainingPayrollMinutes(NpcLogisticsContract contract)
        {
            if (contract == null)
            {
                return InGameMinutesPerWeek;
            }

            return Math.Max(0, InGameMinutesPerWeek - Math.Max(0, contract.PayrollElapsedInGameMinutes));
        }

        public string BuildPayrollStatus(NpcLogisticsContract contract)
        {
            if (contract == null || contract.Tier == null)
            {
                return "Payroll unavailable";
            }

            return string.Format(
                "Weekly {0} | {1}",
                ModFormatting.FormatMoney(GetWeeklyWage(contract.Tier)),
                FormatPayrollCountdown(GetRemainingPayrollMinutes(contract)));
        }

        public bool TryCreateContract(
            Industry originIndustry,
            Industry destinationIndustry,
            string commodity,
            NpcDriverTierDefinition tier,
            out string message)
        {
            return TryUpsertContract(null, originIndustry, destinationIndustry, commodity, tier, out message);
        }

        public bool TryModifyContract(
            NpcLogisticsContract contract,
            Industry originIndustry,
            Industry destinationIndustry,
            string commodity,
            NpcDriverTierDefinition tier,
            out string message)
        {
            return TryUpsertContract(contract, originIndustry, destinationIndustry, commodity, tier, out message);
        }

        public bool TryFireContract(NpcLogisticsContract contract, out string message)
        {
            if (contract == null || !_contracts.Contains(contract))
            {
                message = "Selected NPC route was not found.";
                return false;
            }

            CleanupContractEntities(contract);
            _contracts.Remove(contract);
            message = string.Format("Fired NPC on route {0}.", BuildContractLabel(contract));
            return true;
        }

        public void Update(int now, int currentClockMinute)
        {
            var elapsedPayrollMinutes = GetElapsedPayrollMinutes(currentClockMinute);
            for (int i = 0; i < _contracts.Count; i++)
            {
                if (elapsedPayrollMinutes > 0)
                {
                    UpdatePayroll(_contracts[i], elapsedPayrollMinutes);
                }

                UpdateContract(_contracts[i], now);
            }
        }

        public void ClearAll()
        {
            for (int i = 0; i < _contracts.Count; i++)
            {
                CleanupContractEntities(_contracts[i]);
            }

            _contracts.Clear();
        }

        private bool TryUpsertContract(
            NpcLogisticsContract contract,
            Industry originIndustry,
            Industry destinationIndustry,
            string commodity,
            NpcDriverTierDefinition tier,
            out string message)
        {
            message = string.Empty;
            var isNewContract = contract == null;

            if (originIndustry == null)
            {
                message = "Select a starting point first.";
                return false;
            }

            if (destinationIndustry == null)
            {
                message = "Select a destination first.";
                return false;
            }

            if (string.Equals(originIndustry.Id, destinationIndustry.Id, StringComparison.OrdinalIgnoreCase))
            {
                message = "Starting point and destination must be different industries.";
                return false;
            }

            if (!HasGameplayAccess(originIndustry) || !HasGameplayAccess(destinationIndustry))
            {
                message = "Unlock the required industry permits before assigning this route.";
                return false;
            }

            var normalizedCommodity = CommodityCatalog.Normalize(commodity);
            if (string.IsNullOrWhiteSpace(normalizedCommodity))
            {
                message = "Select a resource first.";
                return false;
            }

            var validResources = GetResourceOptions(originIndustry, destinationIndustry);
            if (!validResources.Contains(normalizedCommodity, StringComparer.OrdinalIgnoreCase))
            {
                message = "The selected resource cannot be transported on that route.";
                return false;
            }

            if (tier == null)
            {
                message = "Select an NPC tier first.";
                return false;
            }

            VehicleDefinition selectedVehicle;
            VehicleDefinition selectedTractor;
            if (!TryResolveVehicleForCommodity(normalizedCommodity, out selectedVehicle, out selectedTractor))
            {
                message = string.Format("No spawnable vehicle is configured for {0}.", normalizedCommodity);
                return false;
            }

            var totalCost = GetContractCost(normalizedCommodity, tier);
            var currentCost = contract != null ? contract.ContractCost : 0f;
            var additionalCost = Math.Max(0f, totalCost - currentCost);
            if (additionalCost > 0f && _getProfit != null && _getProfit() + 0.001f < additionalCost)
            {
                message = string.Format("Not enough profit. Need {0} more.", ModFormatting.FormatMoney(additionalCost - _getProfit()));
                return false;
            }

            if (additionalCost > 0f && _deductProfit != null)
            {
                _deductProfit(additionalCost);
            }

            if (isNewContract)
            {
                contract = new NpcLogisticsContract(_nextContractId++);
                _contracts.Add(contract);
                contract.PayrollElapsedInGameMinutes = 0;
                contract.CompletedPayrollCycles = 0;
                contract.TotalWeeklyWagesPaid = 0f;
            }
            else
            {
                CleanupContractEntities(contract);
            }

            contract.OriginIndustry = originIndustry;
            contract.DestinationIndustry = destinationIndustry;
            contract.Commodity = normalizedCommodity;
            contract.Tier = tier;
            contract.VehicleDefinition = selectedVehicle;
            contract.TractorDefinition = selectedTractor;
            contract.ContractCost = totalCost;
            contract.StatusText = "Preparing route";
            contract.Phase = NpcRoutePhase.PendingSpawn;
            contract.WaitUntilMs = 0;
            contract.NextDriveTaskRefreshMs = 0;
            contract.LastJourneyLossRatio = 0f;

            message = isNewContract
                ? string.Format("Hired {0} NPC for route {1}.", tier.DisplayName, BuildContractLabel(contract))
                : string.Format("Updated NPC route to {0}.", BuildContractLabel(contract));
            return true;
        }

        private void UpdateContract(NpcLogisticsContract contract, int now)
        {
            if (contract == null)
            {
                return;
            }

            if (!HasGameplayAccess(contract.OriginIndustry) || !HasGameplayAccess(contract.DestinationIndustry))
            {
                contract.StatusText = "Waiting for industry permits";
                contract.WaitUntilMs = now + RetryDelayMs;
                return;
            }

            if (!HasOperationalEntities(contract))
            {
                if (now < contract.WaitUntilMs)
                {
                    return;
                }

                string spawnStatus;
                if (!TrySpawnRouteEntities(contract, out spawnStatus))
                {
                    contract.StatusText = spawnStatus;
                    contract.Phase = NpcRoutePhase.PendingSpawn;
                    contract.WaitUntilMs = now + RetryDelayMs;
                    return;
                }

                contract.StatusText = spawnStatus;

                contract.Phase = NpcRoutePhase.DrivingToOrigin;
                contract.NextDriveTaskRefreshMs = 0;
            }

            RefreshBlip(contract);

            if (contract.WaitUntilMs > now)
            {
                return;
            }

            if (contract.Phase == NpcRoutePhase.PendingSpawn)
            {
                contract.Phase = NpcRoutePhase.DrivingToOrigin;
            }

            switch (contract.Phase)
            {
                case NpcRoutePhase.DrivingToOrigin:
                    UpdateDriveToOrigin(contract, now);
                    break;
                case NpcRoutePhase.Loading:
                    CompleteLoading(contract, now);
                    break;
                case NpcRoutePhase.DrivingToDestination:
                    UpdateDriveToDestination(contract, now);
                    break;
                case NpcRoutePhase.Unloading:
                    CompleteUnloading(contract, now);
                    break;
            }
        }

        private void UpdatePayroll(NpcLogisticsContract contract, int elapsedPayrollMinutes)
        {
            if (contract == null || contract.Tier == null || elapsedPayrollMinutes <= 0)
            {
                return;
            }

            contract.PayrollElapsedInGameMinutes += elapsedPayrollMinutes;

            var payrollCyclesDue = contract.PayrollElapsedInGameMinutes / InGameMinutesPerWeek;
            if (payrollCyclesDue <= 0)
            {
                return;
            }

            contract.PayrollElapsedInGameMinutes %= InGameMinutesPerWeek;

            var weeklyWage = GetWeeklyWage(contract.Tier);
            var totalCharge = weeklyWage * payrollCyclesDue;
            if (totalCharge > 0f && _deductProfit != null)
            {
                _deductProfit(totalCharge);
            }

            contract.CompletedPayrollCycles += payrollCyclesDue;
            contract.TotalWeeklyWagesPaid += totalCharge;

            if (_showStatus != null && totalCharge > 0f)
            {
                _showStatus(string.Format(
                    "NPC payroll charged {0} for {1}.",
                    ModFormatting.FormatMoney(totalCharge),
                    BuildContractLabel(contract)));
            }
        }

        private void UpdateDriveToOrigin(NpcLogisticsContract contract, int now)
        {
            var originPosition = GetOriginRoutePosition(contract);
            if (GetDriverVehicle(contract).Position.DistanceTo(originPosition) <= ArrivalDistance)
            {
                ClearPedTasks(contract.Driver);
                contract.Phase = NpcRoutePhase.Loading;
                contract.StatusText = "Loading cargo";
                contract.WaitUntilMs = now + LoadDelayMs;
                return;
            }

            EnsureDriveTask(contract, originPosition, now);
            contract.StatusText = string.Format("Driving to {0}", contract.OriginIndustry.Name);
        }

        private void CompleteLoading(NpcLogisticsContract contract, int now)
        {
            var cargoVehicle = GetCargoVehicle(contract);
            var cargoState = _fleetManager.GetOrCreateCargoState(cargoVehicle);
            if (cargoState == null)
            {
                contract.StatusText = "Waiting for cargo vehicle";
                contract.WaitUntilMs = now + RetryDelayMs;
                return;
            }

            var destinationCapacity = contract.DestinationIndustry.GetMaxTransferTonsForCommodity(contract.Commodity);
            var availableOriginStock = contract.OriginIndustry.GetStock(contract.Commodity);
            var loadTargetTons = Math.Min(cargoState.CapacityTons, Math.Min(destinationCapacity, availableOriginStock));
            if (loadTargetTons <= 0.001f)
            {
                contract.StatusText = availableOriginStock <= 0.001f
                    ? "Waiting for origin stock"
                    : "Waiting for destination capacity";
                contract.WaitUntilMs = now + RetryDelayMs;
                return;
            }

            cargoState.ClearCargo();
            float loadedTons;
            if (!_industryManager.TryLoadCommodity(contract.OriginIndustry, cargoState.CargoType, contract.Commodity, loadTargetTons, out loadedTons) || loadedTons <= 0.001f)
            {
                contract.StatusText = "Waiting for origin stock";
                contract.WaitUntilMs = now + RetryDelayMs;
                return;
            }

            var lossRatio = (float)(_random.NextDouble() * Math.Max(0f, contract.Tier.CargoLossRate));
            if (_territoryManager != null)
            {
                lossRatio = _territoryManager.AdjustNpcLossRatio(contract.OriginIndustry, contract.DestinationIndustry, lossRatio);
            }

            var deliveredTons = Math.Max(0.1f, loadedTons * (1f - lossRatio));

            cargoState.Commodity = contract.Commodity;
            cargoState.CargoType = CommodityCatalog.GetCargoTypeForCommodity(contract.Commodity);
            cargoState.WeightTons = deliveredTons;
            cargoState.TotalLostTons += Math.Max(0f, loadedTons - deliveredTons);
            cargoState.CargoCondition = Math.Max(0.25f, 1f - lossRatio);
            cargoState.SourceIndustryId = contract.OriginIndustry != null ? contract.OriginIndustry.Id : string.Empty;
            cargoState.SourceDistrictName = contract.OriginIndustry != null ? contract.OriginIndustry.DistrictName : string.Empty;
            _fleetManager.ApplyCargoVisuals(cargoVehicle, cargoState);
            if (_territoryManager != null)
            {
                _territoryManager.RegisterLoad(contract.OriginIndustry, contract.Commodity, loadedTons, true);
            }

            contract.LastJourneyLossRatio = lossRatio;
            contract.Phase = NpcRoutePhase.DrivingToDestination;
            contract.NextDriveTaskRefreshMs = 0;
            contract.WaitUntilMs = 0;
            contract.StatusText = string.Format("Delivering {0}", contract.Commodity);
            EnsureDriveTask(contract, GetDestinationRoutePosition(contract), now);
        }

        private void UpdateDriveToDestination(NpcLogisticsContract contract, int now)
        {
            var destinationPosition = GetDestinationRoutePosition(contract);
            if (GetDriverVehicle(contract).Position.DistanceTo(destinationPosition) <= ArrivalDistance)
            {
                ClearPedTasks(contract.Driver);
                contract.Phase = NpcRoutePhase.Unloading;
                contract.StatusText = "Unloading cargo";
                contract.WaitUntilMs = now + UnloadDelayMs;
                return;
            }

            EnsureDriveTask(contract, destinationPosition, now);
            contract.StatusText = string.Format("En route to {0}", contract.DestinationIndustry.Name);
        }

        private void CompleteUnloading(NpcLogisticsContract contract, int now)
        {
            var cargoVehicle = GetCargoVehicle(contract);
            var cargoState = _fleetManager.GetOrCreateCargoState(cargoVehicle);
            if (cargoState == null || cargoState.IsEmpty)
            {
                contract.Phase = NpcRoutePhase.DrivingToOrigin;
                contract.NextDriveTaskRefreshMs = 0;
                contract.StatusText = string.Format("Returning to {0}", contract.OriginIndustry.Name);
                return;
            }

            var unloadTargetTons = Math.Min(cargoState.WeightTons, contract.DestinationIndustry.GetMaxTransferTonsForCommodity(contract.Commodity));
            if (unloadTargetTons <= 0.001f)
            {
                contract.StatusText = "Waiting for destination capacity";
                contract.WaitUntilMs = now + RetryDelayMs;
                return;
            }

            float acceptedTons;
            if (!_industryManager.TryUnload(contract.DestinationIndustry, contract.Commodity, unloadTargetTons, out acceptedTons) || acceptedTons <= 0.001f)
            {
                contract.StatusText = "Waiting for destination capacity";
                contract.WaitUntilMs = now + RetryDelayMs;
                return;
            }

            var revenue = _industryManager.ComputeDeliveryProfit(contract.DestinationIndustry, contract.Commodity, acceptedTons, _globalMarket, now);
            if (_territoryManager != null)
            {
                revenue = _territoryManager.AdjustDeliveryRevenue(contract.DestinationIndustry, contract.Commodity, acceptedTons, revenue);
                _territoryManager.RegisterDelivery(
                    contract.DestinationIndustry,
                    contract.Commodity,
                    acceptedTons,
                    true,
                    contract.OriginIndustry != null ? contract.OriginIndustry.Id : cargoState.SourceIndustryId,
                    contract.OriginIndustry != null ? contract.OriginIndustry.DistrictName : cargoState.SourceDistrictName);
            }

            if (_addProfit != null && revenue > 0f)
            {
                _addProfit(revenue);
            }

            contract.TotalDeliveredTons += acceptedTons;

            cargoState.WeightTons = Math.Max(0f, cargoState.WeightTons - acceptedTons);
            if (cargoState.WeightTons <= 0.001f)
            {
                cargoState.ClearCargo();
                _fleetManager.ClearCargoVisuals(cargoState);
                contract.CompletedDeliveries += 1;
                contract.TotalProfitEarned += revenue;
                contract.Phase = NpcRoutePhase.DrivingToOrigin;
                contract.StatusText = string.Format(
                    "Delivered {0:0.0}t {1}",
                    acceptedTons,
                    contract.Commodity);
                contract.NextDriveTaskRefreshMs = 0;
                contract.WaitUntilMs = now + 1000;
                return;
            }

            _fleetManager.ApplyCargoVisuals(cargoVehicle, cargoState);
            contract.StatusText = "Partially unloaded cargo";
            contract.WaitUntilMs = now + RetryDelayMs;
        }

        private bool TrySpawnRouteEntities(NpcLogisticsContract contract, out string message)
        {
            message = string.Empty;
            if (contract == null || contract.VehicleDefinition == null || contract.Tier == null)
            {
                message = "Route configuration is incomplete.";
                return false;
            }

            Vehicle truck;
            Vehicle cargoVehicle;
            if (!_fleetManager.SpawnSelectedVehicle(
                contract.VehicleDefinition,
                contract.TractorDefinition,
                _getGroundPosition(GetOriginSpawnPosition(contract)),
                GetOriginSpawnHeading(contract),
                out truck,
                out cargoVehicle,
                out message))
            {
                return false;
            }

            var driver = CreateDriverPed(contract, truck);
            if (driver == null || !driver.Exists())
            {
                if (cargoVehicle != null && cargoVehicle.Exists())
                {
                    cargoVehicle.Delete();
                }

                if (truck != null && truck.Exists())
                {
                    truck.Delete();
                }

                message = "Failed to create NPC driver.";
                return false;
            }

            contract.Truck = truck;
            contract.CargoVehicle = cargoVehicle;
            contract.Driver = driver;
            if (contract.CargoVehicle != null && contract.CargoVehicle.Exists())
            {
                contract.CargoVehicle.IsPersistent = true;
            }

            contract.RouteBlip = CreateRouteBlip(contract);
            contract.StatusText = string.Format("Spawned {0}", contract.Tier.DisplayName);

            var cargoState = _fleetManager.GetOrCreateCargoState(GetCargoVehicle(contract));
            if (cargoState != null)
            {
                cargoState.ClearCargo();
                cargoState.CargoType = CommodityCatalog.GetCargoTypeForCommodity(contract.Commodity);
            }

            return true;
        }

        private Ped CreateDriverPed(NpcLogisticsContract contract, Vehicle truck)
        {
            if (truck == null || !truck.Exists())
            {
                return null;
            }

            var modelName = contract.Tier != null ? contract.Tier.NpcModel : DefaultNpcModel;
            var model = new Model(modelName);
            model.Request(1000);
            if (!model.IsLoaded)
            {
                model = new Model(DefaultNpcModel);
                model.Request(1000);
            }

            if (!model.IsLoaded)
            {
                return null;
            }

            var pedHandle = Function.Call<int>(Hash.CREATE_PED_INSIDE_VEHICLE, truck.Handle, 26, model.Hash, -1, true, true);
            var driver = Entity.FromHandle(pedHandle) as Ped;
            if (driver == null || !driver.Exists())
            {
                return null;
            }

            driver.IsPersistent = true;
            truck.IsPersistent = true;

            Function.Call(Hash.SET_BLOCKING_OF_NON_TEMPORARY_EVENTS, driver.Handle, true);
            Function.Call(Hash.SET_PED_KEEP_TASK, driver.Handle, true);
            Function.Call(Hash.SET_PED_CAN_BE_DRAGGED_OUT, driver.Handle, false);
            Function.Call(Hash.SET_DRIVER_ABILITY, driver.Handle, Math.Max(0f, Math.Min(1f, contract.Tier.SpeedMultiplier)));
            Function.Call(Hash.SET_DRIVER_AGGRESSIVENESS, driver.Handle, Math.Max(0f, Math.Min(1f, contract.Tier.SpeedMultiplier)));
            Function.Call(Hash.SET_VEHICLE_ENGINE_ON, truck.Handle, true, true, false);
            return driver;
        }

        private void EnsureDriveTask(NpcLogisticsContract contract, Vector3 targetPosition, int now)
        {
            if (contract == null || contract.Driver == null || !contract.Driver.Exists())
            {
                return;
            }

            if (contract.Truck == null || !contract.Truck.Exists())
            {
                return;
            }

            if (now < contract.NextDriveTaskRefreshMs)
            {
                return;
            }

            Function.Call(
                Hash.TASK_VEHICLE_DRIVE_TO_COORD_LONGRANGE,
                contract.Driver.Handle,
                contract.Truck.Handle,
                targetPosition.X,
                targetPosition.Y,
                targetPosition.Z,
                Math.Max(8f, BaseDriveSpeed * Math.Max(0.1f, contract.Tier.SpeedMultiplier)),
                DriveStyle,
                ArrivalDistance * 0.5f);
            contract.NextDriveTaskRefreshMs = now + DriveTaskRefreshIntervalMs;
        }

        private void ClearPedTasks(Ped driver)
        {
            if (driver == null || !driver.Exists())
            {
                return;
            }

            Function.Call(Hash.CLEAR_PED_TASKS, driver.Handle);
        }

        private Blip CreateRouteBlip(NpcLogisticsContract contract)
        {
            var driverVehicle = GetDriverVehicle(contract);
            if (driverVehicle == null || !driverVehicle.Exists())
            {
                return null;
            }

            var blip = World.CreateBlip(driverVehicle.Position);
            if (blip == null || !blip.Exists())
            {
                return null;
            }

            blip.Sprite = BlipSprite.Truck;
            blip.Color = BlipColor.Blue;
            blip.Name = string.Format("NPC Route: {0}", BuildContractLabel(contract));
            blip.Scale = 0.85f;
            blip.IsShortRange = false;
            blip.IsHiddenOnLegend = false;
            return blip;
        }

        private void RefreshBlip(NpcLogisticsContract contract)
        {
            var driverVehicle = GetDriverVehicle(contract);
            if (driverVehicle == null || !driverVehicle.Exists())
            {
                if (contract.RouteBlip != null && contract.RouteBlip.Exists())
                {
                    contract.RouteBlip.Delete();
                }

                contract.RouteBlip = null;
                return;
            }

            if (contract.RouteBlip == null || !contract.RouteBlip.Exists())
            {
                contract.RouteBlip = CreateRouteBlip(contract);
            }

            if (contract.RouteBlip != null && contract.RouteBlip.Exists())
            {
                contract.RouteBlip.Position = driverVehicle.Position;
            }
        }

        private bool HasOperationalEntities(NpcLogisticsContract contract)
        {
            return contract != null
                && contract.Driver != null
                && contract.Driver.Exists()
                && contract.Driver.Health > 0
                && contract.Truck != null
                && contract.Truck.Exists()
                && GetCargoVehicle(contract) != null
                && GetCargoVehicle(contract).Exists();
        }

        private void CleanupContractEntities(NpcLogisticsContract contract)
        {
            if (contract == null)
            {
                return;
            }

            var cargoVehicle = GetCargoVehicle(contract);
            var cargoState = _fleetManager.GetOrCreateCargoState(cargoVehicle);
            if (cargoState != null)
            {
                cargoState.ClearCargo();
                _fleetManager.ClearCargoVisuals(cargoState);
            }

            if (contract.RouteBlip != null && contract.RouteBlip.Exists())
            {
                contract.RouteBlip.Delete();
            }

            if (contract.Driver != null && contract.Driver.Exists())
            {
                contract.Driver.Delete();
            }

            if (contract.CargoVehicle != null && contract.CargoVehicle.Exists())
            {
                contract.CargoVehicle.Delete();
            }

            if (contract.Truck != null && contract.Truck.Exists() && (contract.CargoVehicle == null || contract.Truck.Handle != contract.CargoVehicle.Handle))
            {
                contract.Truck.Delete();
            }

            contract.Driver = null;
            contract.Truck = null;
            contract.CargoVehicle = null;
            contract.RouteBlip = null;
            contract.Phase = NpcRoutePhase.PendingSpawn;
            contract.WaitUntilMs = 0;
            contract.NextDriveTaskRefreshMs = 0;
        }

        private bool CanUseAsOrigin(Industry industry)
        {
            return industry != null
                && industry.Outputs != null
                && industry.Outputs.Count > 0
                && HasGameplayAccess(industry)
                && (_territoryManager == null || _territoryManager.IsAutomationReady(industry));
        }

        private bool CanUseAsDestination(Industry originIndustry, Industry destinationIndustry)
        {
            string reason;
            return destinationIndustry != null
                && originIndustry != null
                && !string.Equals(originIndustry.Id, destinationIndustry.Id, StringComparison.OrdinalIgnoreCase)
                && HasGameplayAccess(destinationIndustry)
                && GetResourceOptions(originIndustry, destinationIndustry).Count > 0
                && (_territoryManager == null || _territoryManager.CanCreateNpcRoute(originIndustry, destinationIndustry, out reason));
        }

        private bool HasGameplayAccess(Industry industry)
        {
            return industry != null && !_industryManager.RequiresContractorPermit(industry);
        }

        private bool TryResolveVehicleForCommodity(string commodity, out VehicleDefinition selectedVehicle, out VehicleDefinition selectedTractor)
        {
            selectedVehicle = null;
            selectedTractor = null;

            selectedVehicle = _fleetManager
                .GetSpawnableForCommodity(commodity)
                .OrderByDescending(definition => definition.CapacityTons)
                .FirstOrDefault();
            if (selectedVehicle == null)
            {
                return false;
            }

            if (selectedVehicle.IsTrailer)
            {
                selectedTractor = _fleetManager
                    .GetTractorDefinitions()
                    .OrderByDescending(definition => definition.CapacityTons)
                    .FirstOrDefault();
                return selectedTractor != null;
            }

            return true;
        }

        private Vector3 GetOriginSpawnPosition(NpcLogisticsContract contract)
        {
            var originIndustry = contract != null ? contract.OriginIndustry : null;
            if (originIndustry != null && originIndustry.VehicleSpawnPosition.HasValue)
            {
                return originIndustry.VehicleSpawnPosition.Value;
            }

            return originIndustry != null ? originIndustry.Position : Vector3.Zero;
        }

        private float GetOriginSpawnHeading(NpcLogisticsContract contract)
        {
            var originIndustry = contract != null ? contract.OriginIndustry : null;
            if (originIndustry != null && originIndustry.VehicleSpawnHeading.HasValue)
            {
                return originIndustry.VehicleSpawnHeading.Value;
            }

            return 0f;
        }

        private Vector3 GetOriginRoutePosition(NpcLogisticsContract contract)
        {
            return _getGroundPosition(GetOriginSpawnPosition(contract));
        }

        private Vector3 GetDestinationRoutePosition(NpcLogisticsContract contract)
        {
            if (contract == null || contract.DestinationIndustry == null)
            {
                return Vector3.Zero;
            }

            if (contract.DestinationIndustry.VehicleSpawnPosition.HasValue)
            {
                return _getGroundPosition(contract.DestinationIndustry.VehicleSpawnPosition.Value);
            }

            return _getGroundPosition(contract.DestinationIndustry.Position);
        }

        private Vehicle GetDriverVehicle(NpcLogisticsContract contract)
        {
            return contract != null && contract.Truck != null && contract.Truck.Exists()
                ? contract.Truck
                : (contract != null ? contract.CargoVehicle : null);
        }

        private Vehicle GetCargoVehicle(NpcLogisticsContract contract)
        {
            return contract != null && contract.CargoVehicle != null && contract.CargoVehicle.Exists()
                ? contract.CargoVehicle
                : (contract != null ? contract.Truck : null);
        }

        private int GetElapsedPayrollMinutes(int currentClockMinute)
        {
            var normalizedClockMinute = Math.Max(0, currentClockMinute);
            if (_lastObservedClockMinute < 0)
            {
                _lastObservedClockMinute = normalizedClockMinute;
                return 0;
            }

            var elapsedMinutes = normalizedClockMinute - _lastObservedClockMinute;
            if (elapsedMinutes < 0)
            {
                _lastObservedClockMinute = normalizedClockMinute;
                return 0;
            }

            _lastObservedClockMinute = normalizedClockMinute;
            return Math.Max(0, elapsedMinutes);
        }

        private static string FormatPayrollCountdown(int remainingMinutes)
        {
            if (remainingMinutes <= 0)
            {
                return "Payroll due";
            }

            var days = remainingMinutes / InGameMinutesPerDay;
            var hours = (remainingMinutes % InGameMinutesPerDay) / 60;
            var minutes = remainingMinutes % 60;
            if (days > 0)
            {
                return hours > 0
                    ? string.Format("Payroll in {0}d {1}h", days, hours)
                    : string.Format("Payroll in {0}d", days);
            }

            if (hours > 0)
            {
                return minutes > 0
                    ? string.Format("Payroll in {0}h {1}m", hours, minutes)
                    : string.Format("Payroll in {0}h", hours);
            }

            return string.Format("Payroll in {0}m", Math.Max(1, minutes));
        }

        private static string BuildContractLabel(NpcLogisticsContract contract)
        {
            if (contract == null || contract.OriginIndustry == null || contract.DestinationIndustry == null)
            {
                return "Unknown route";
            }

            return string.Format(
                "{0} -> {1} ({2})",
                contract.OriginIndustry.Name,
                contract.DestinationIndustry.Name,
                contract.Commodity);
        }

        private static List<NpcDriverTierDefinition> LoadDriverTiers(string configPath)
        {
            var configDirectory = string.IsNullOrWhiteSpace(configPath)
                ? string.Empty
                : Path.GetDirectoryName(configPath) ?? string.Empty;
            var filePath = Path.Combine(configDirectory, "configs", "HiringNPC.ini");
            var ini = IniFile.Load(filePath);

            return new List<NpcDriverTierDefinition>
            {
                BuildTier(ini, "RookieNPC", "Rookie", DefaultNpcModel, 0.40f, 0.60f, 25f, 1500f, 2000f, 3000f),
                BuildTier(ini, "ProfessionalNPC", "Professional", "s_m_y_construct_01", 0.20f, 0.80f, 50f, 3000f, 4000f, 6000f),
                BuildTier(ini, "VeteranNPC", "Veteran", "s_m_m_dockwork_01", 0.10f, 0.90f, 100f, 6000f, 8000f, 12000f),
            };
        }

        private static NpcDriverTierDefinition BuildTier(
            IniFile ini,
            string section,
            string displayName,
            string fallbackModel,
            float fallbackLossRate,
            float fallbackSpeedMultiplier,
            float fallbackPriceMultiplier,
            float fallbackWeeklyWageCasual,
            float fallbackWeeklyWageStandard,
            float fallbackWeeklyWageHardcore)
        {
            var modelName = ini.GetString(section, "NPCModel", fallbackModel).Trim().Trim('"');
            return new NpcDriverTierDefinition(
                displayName,
                displayName,
                string.IsNullOrWhiteSpace(modelName) ? fallbackModel : modelName,
                Math.Max(0f, ini.GetFloat(section, "NPCCargoLooseRate", fallbackLossRate)),
                Math.Max(0.1f, ini.GetFloat(section, "NPCSpeedMultiplier", fallbackSpeedMultiplier)),
                Math.Max(1f, ini.GetFloat(section, "NPCPriceMultiplier", fallbackPriceMultiplier)),
                Math.Max(0f, ini.GetFloat(section, "NPCWeeklyWageCasual", fallbackWeeklyWageCasual)),
                Math.Max(0f, ini.GetFloat(section, "NPCWeeklyWageStandard", fallbackWeeklyWageStandard)),
                Math.Max(0f, ini.GetFloat(section, "NPCWeeklyWageHardcore", fallbackWeeklyWageHardcore)));
        }
    }

    public enum NpcWeeklyWageDifficulty
    {
        Casual = 0,
        Standard = 1,
        Hardcore = 2,
    }

    public sealed class NpcDriverTierDefinition
    {
        public NpcDriverTierDefinition(
            string id,
            string displayName,
            string npcModel,
            float cargoLossRate,
            float speedMultiplier,
            float priceMultiplier,
            float weeklyWageCasual,
            float weeklyWageStandard,
            float weeklyWageHardcore)
        {
            Id = id;
            DisplayName = displayName;
            NpcModel = npcModel;
            CargoLossRate = cargoLossRate;
            SpeedMultiplier = speedMultiplier;
            PriceMultiplier = priceMultiplier;
            WeeklyWageCasual = weeklyWageCasual;
            WeeklyWageStandard = weeklyWageStandard;
            WeeklyWageHardcore = weeklyWageHardcore;
        }

        public string Id { get; private set; }

        public string DisplayName { get; private set; }

        public string NpcModel { get; private set; }

        public float CargoLossRate { get; private set; }

        public float SpeedMultiplier { get; private set; }

        public float PriceMultiplier { get; private set; }

        public float WeeklyWageCasual { get; private set; }

        public float WeeklyWageStandard { get; private set; }

        public float WeeklyWageHardcore { get; private set; }

        public float GetWeeklyWage(NpcWeeklyWageDifficulty difficulty)
        {
            switch (difficulty)
            {
                case NpcWeeklyWageDifficulty.Casual:
                    return WeeklyWageCasual;
                case NpcWeeklyWageDifficulty.Hardcore:
                    return WeeklyWageHardcore;
                default:
                    return WeeklyWageStandard;
            }
        }
    }

    public sealed class NpcLogisticsContract
    {
        internal NpcLogisticsContract(int id)
        {
            Id = id;
            StatusText = "Preparing route";
            Phase = NpcRoutePhase.PendingSpawn;
        }

        public int Id { get; private set; }

        public Industry OriginIndustry { get; internal set; }

        public Industry DestinationIndustry { get; internal set; }

        public string Commodity { get; internal set; }

        public NpcDriverTierDefinition Tier { get; internal set; }

        public VehicleDefinition VehicleDefinition { get; internal set; }

        public VehicleDefinition TractorDefinition { get; internal set; }

        public float ContractCost { get; internal set; }

        public int PayrollElapsedInGameMinutes { get; internal set; }

        public int CompletedPayrollCycles { get; internal set; }

        public float TotalWeeklyWagesPaid { get; internal set; }

        public int CompletedDeliveries { get; internal set; }

        public float TotalDeliveredTons { get; internal set; }

        public float TotalProfitEarned { get; internal set; }

        public string StatusText { get; internal set; }

        public float LastJourneyLossRatio { get; internal set; }

        internal NpcRoutePhase Phase { get; set; }

        internal int WaitUntilMs { get; set; }

        internal int NextDriveTaskRefreshMs { get; set; }

        internal Ped Driver { get; set; }

        internal Vehicle Truck { get; set; }

        internal Vehicle CargoVehicle { get; set; }

        internal Blip RouteBlip { get; set; }
    }

    internal enum NpcRoutePhase
    {
        PendingSpawn = 0,
        DrivingToOrigin = 1,
        Loading = 2,
        DrivingToDestination = 3,
        Unloading = 4,
    }
}