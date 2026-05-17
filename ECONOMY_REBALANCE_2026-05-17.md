# LSOL Economy Rebalance Report

## 1. Executive summary

The current LSOL economy is not just numerically off. It has four structural problems that flatten progression and make any pure XML rebalance incomplete:

1. Production-site pricing is almost completely flat.
   Standard production, permit, purchase, and capacity values are effectively the same on every production site: 32 productionRate, 13000 licencePrice, 450000 purchasePrice, 1200 inputCapacityTons, 1000 outputCapacityTons.
2. Store and gas-station difficulty presets are not actually applied at runtime.
   Their Standard values are baked in during load and the preset swap only runs for ExternalLocationKind.Industry.
3. The commodity price table is missing most active commodities.
   22 active commodities currently fall back to the hardcoded 400/t default, including Electronic, MechanicalParts, Lumber, Cement, Clothes, Water, Vehicles, and Furniture.
4. Sink pressure is out of scale with upstream production.
   Current Standard fuel supply is 32 t/h from the refinery, while gas stations drain about 3225 t/h. Stores drain about 732 t/h total, and construction drains about 798 t/h.

The biggest practical result is this:

- direct sink hauling is the only healthy money loop
- source-to-industry deliveries are too weak because they pay 15% of unit price, or 7.5% if owner cut is also applied
- progression only feels good when the player finds the few inflated loops, especially fuel and current TV/Computer runs
- the 30000 t tanker destroys the liquid-economy scale entirely

My recommendation is a two-step plan:

- Phase A: apply a small set of code fixes, then rebalance data
- Phase B: only if you want a deeper simulation, add per-commodity recipe weights and better optional-input semantics

This report focuses on a complete Phase A rebalance that fits the current repo structure.

## 2. Current model, quantified

### 2.1 Network shape

Current site counts:

| Role | Count |
| --- | ---: |
| RawProducer | 5 |
| ProcessingPlant | 13 |
| ManufacturingPlant | 8 |
| RecyclingHub | 1 |
| SpecialPlant | 1 |
| Warehouse | 7 |
| StoreSink | 6 |
| FuelSink | 25 |
| ConstructionSiteSink | 5 |

### 2.2 Uniform pricing problem

Current Standard site pricing is effectively flat:

| Role group | Standard purchase | Standard permit |
| --- | ---: | ---: |
| RawProducer | 450000 | 13000 |
| ProcessingPlant | 450000 | 13000 |
| ManufacturingPlant | 450000 | 13000 |
| RecyclingHub | 450000 | 13000 |
| SpecialPlant | 450000 | 13000 |
| Warehouse | 450000 | 0 |
| StoreSink | 450000 | 0 |
| FuelSink | 450000 | 0 |
| ConstructionSiteSink | 450000 | 0 |

That means ownership tier, chain depth, site complexity, and sink power barely matter to the economy.

### 2.3 Commodity-price problem

Current actual unit prices are not the prices you would infer from the site chains.

Configured high-value goods:

- Omega 12000
- Computer 8200
- TV 6500
- Medicine 4200
- Alloy 1500
- Metal 1200
- Plastic 900
- ProcessedFood 900
- Fuel 750
- Recyclable 500
- Oil 450
- Ore 380
- Coal 300

Everything below currently resolves to 400/t because there is no explicit base price after normalization:

- Alcohol
- Beam
- Bricks
- Cement
- Chemicals
- Clothes
- Concrete
- Crops
- Electronic
- Fabric
- Furniture
- Gravel
- LiquidFertilizer
- Livestock
- Lumber
- Meat
- MechanicalParts
- Paper
- Steel
- Vehicles
- Water
- Wood

The most important mismatch is Electronic.
Sites and vehicles normalize Processors to Electronic, but GlobalMarketManager still prices Processors instead of Electronic.

### 2.4 Sink pressure versus production

Current Standard supply and sink pressure, using the actual code rules:

| Commodity | Current supply t/h | Current sink demand t/h |
| --- | ---: | ---: |
| Fuel | 32 | 3225 |
| Alcohol | 32 | 219.6 |
| ProcessedFood | 32 | 219.6 |
| Beam | 32 | 187.5 |
| Bricks | 32 | 187.5 |
| Cement | 32 | 177.75 |
| Concrete | 32 | 153.75 |
| Clothes | 32 | 117.6 |
| TV | 32 | 117.6 |
| Lumber | 32 | 57.75 |
| Computer | 32 | 57.6 |
| Gravel | 32 | 33.75 |

Current total sink families:

- Fuel sinks: 3225 t/h total
- Store sinks: 732 t/h total
- Construction sinks: 798 t/h total

Current seeded sink stock lasts only:

- Fuel sinks: about 12.28 in-game hours
- Store sinks: about 0.12 in-game hours
- Construction sinks: about 0.19 in-game hours

So the consumer side starts starving almost immediately except for fuel.

### 2.5 Buffer and throughput problem

Because Industry.GetCommodityCapacityTons splits total capacity evenly across all inputs plus optional inputs, and across all outputs, complex sites are punished heavily.

Examples under current Standard values:

- RecyclingCenter: 320 cycles/h, one output share fills in about 1.04 h
- SmeltingFactory: 32 cycles/h, one output share fills in about 10.42 h
- ConsumerElectronicsFactory: 32 cycles/h, one output share fills in about 10.42 h
- VehicleFactory: per-input share only 200 t because five primaries plus Omega optional split the 1200 t input total

### 2.6 Current payout cadence

With the current code, sink deliveries pay 100% of unit price and production-site deliveries pay 15%.
If industry pricing difficulty is enabled and the site is unowned, the owner cut usually halves that again.

Examples for a 10 t truck:

| Delivery | Payout |
| --- | ---: |
| Sink delivery at 400/t | 4000 |
| Production-site delivery at 400/t | 600 |
| Unowned production-site delivery at 400/t with 50% owner cut | 300 |
| Sink delivery at 750/t | 7500 |
| Production-site delivery at 750/t | 1125 |
| Unowned production-site delivery at 750/t with 50% owner cut | 562.5 |

This is why early chain-feeding feels bad even before permits or purchase costs are applied.

### 2.7 Vehicle economy problem

The current tanker is the single worst outlier in the entire economy.

- current tanker capacity: 30000 t
- current fuel base value: 750/t
- current gross fuel trip value: 22500000
- current tanker daily rent: 500

That scale is incompatible with every other vehicle in the file.

## 3. Recommended balance philosophy

### 3.1 What should differ by difficulty

Casual, Standard, and Hardcore should not mainly differ in commodity prices or vehicle capacities.
Those values are player literacy values. Players should quickly learn what a ton of fuel, steel, or electronics is worth, and what each truck class can carry.

Difficulty should instead change:

- site permit and purchase friction
- owner-cut pressure on unowned sites
- site buffer sizes
- sink drain rates
- NPC wages and route-fee pressure
- how aggressively the network starves if the player ignores it

### 3.2 What should stay global across difficulties

Keep these global across Casual, Standard, Hardcore:

- commodity base prices
- vehicle capacities
- vehicle fuel capacities
- office prices and apartment prices unless you add a separate difficulty-aware property schema

### 3.3 Target run cadence for Standard

| Stage | Normal vehicle | Normal sink run | What should unlock |
| --- | --- | ---: | --- |
| Early | 5 t van or 10 t rental | 1500 to 4000 | first permit, first local sink, first owned van/light truck |
| Early-mid | 10 t owned or 20 t rental | 4000 to 9000 | first starter site, first warehouse, first small office |
| Mid | 20 t rigid or 24 t trailer | 12000 to 30000 | first regional site set, first stable warehouse loop, first NPC rookie |
| Late | 24 to 30 t specialized rig | 30000 to 90000 | core sites, better office, multi-site chain ownership |
| Endgame | 30 t specialized trailer fleet | 90000 to 170000 | expansion and special sites, veteran NPCs, Omega network |

### 3.4 Target affordability for Standard

- first accessible permit: 2 to 4 normal runs
- first local sink or warehouse: 8 to 14 normal runs
- first starter production site: 10 to 16 normal runs
- first regional production site: 14 to 24 normal runs
- first core production site: 20 to 35 normal runs
- expansion/special sites: 10 to 18 high-tier runs, not 40 to 80 grind runs

## 4. Recommended balance design

## 4.1 Minimal code fixes before the data pass

These are the minimum changes I would make before touching the XML values.

1. Apply site presets to all CSV-backed sites, not just ExternalLocationKind.Industry.
   Store and gas-station Casual and Hardcore blocks are currently effectively ignored.
2. Fix the base-price lookup mismatch by pricing Electronic explicitly and completing the commodity table.
3. Raise non-sink delivery payout from 0.15 to 0.35.
   That keeps chain-feeding relevant without overshadowing sinks.
4. Reduce default owner cut from the current implicit 0.5 to explicit per-site values in Sites.xml.
   Recommended range is 0.15 to 0.40 depending on site tier.
5. Normalize productionRatio to 1 in Sites.xml and encode the real effective rate directly in productionRate.
6. Reduce tanker capacity from 30000 t to 30 t.

Optional but valuable:

7. Make Omega boost available to CSV sites when Omega is configured as an optional input, not only when it is configured as a primary input.
8. Change OfficeObjectManager fuel delivery from a discount to a small premium.
   A convenience dispatch should not undercut the market price.

## 4.2 Commodity prices (global across all difficulties)

Use one clear global price table.
Do not vary these by difficulty unless the whole economy is moved to a market-driven model.

| Commodity | Standard price |
| --- | ---: |
| Coal | 250 |
| Gravel | 260 |
| Ore | 320 |
| Water | 220 |
| Wood | 320 |
| Crops | 340 |
| Oil | 380 |
| Livestock | 420 |
| Recyclable | 180 |
| Fuel | 700 |
| Cement | 450 |
| Lumber | 520 |
| Paper | 550 |
| Plastic | 800 |
| Alcohol | 650 |
| Chemicals | 750 |
| Fabric | 700 |
| ProcessedFood | 850 |
| Meat | 900 |
| LiquidFertilizer | 600 |
| Steel | 900 |
| Alloy | 1100 |
| Metal | 850 |
| Bricks | 650 |
| Concrete | 700 |
| Beam | 950 |
| MechanicalParts | 1100 |
| Electronic | 1400 |
| Clothes | 1200 |
| Furniture | 1500 |
| Medicine | 2400 |
| TV | 3000 |
| Computer | 3800 |
| Vehicles | 5000 |
| Omega | 7000 |

Notes:

- Paper, Furniture, Medicine, Vehicles, and Metal still need chain-end fixes or extra sinks. Price alone will not make them healthy.
- Recyclable should be cheap because it is a byproduct and RecyclingCenter multiplies it into several outputs.
- TV and Computer should remain valuable, but not so valuable that one trailer dominates every late-game decision.

## 4.3 Vehicle classes (global across all difficulties)

| Class | Capacity | Buy | Daily rent | Fuel |
| --- | ---: | ---: | ---: | ---: |
| Light van | 3 t | 8000 | 400 | 120 L |
| Van | 5 t | 14000 | 650 | 180 L |
| Light truck | 10 t | 30000 to 35000 | 1400 to 1600 | 300 L |
| Heavy rigid truck | 18 to 20 t | 65000 to 80000 | 2600 to 3200 | 450 L |
| Tractor | 0 t | 110000 | 4500 | 800 L |
| General trailer | 24 t | 22000 | 900 |
| Open trailer | 22 t | 20000 | 850 |
| Log trailer | 24 t | 24000 | 1000 |
| Refrigerated trailer | 24 t | 26000 | 1100 |
| Tanker trailer | 30 t | 35000 | 1750 |
| Car trailer | 4 vehicles | 45000 | 2000 |

Vehicle-entry recommendations for Vehicles.xml:

- keep all 3 t light vans aligned
- keep all 5 t box vans aligned
- keep Benson and Mule in the same 10 t band
- keep Pounder as the main owned heavy rigid option
- cap all liquid trailers at 30 t
- remove the 30000 t tanker immediately

## 4.4 Production sites, Standard targets

Implementation rule:

- set productionRatio to 1 on every site in Sites.xml
- encode the actual effective rate directly in productionRate

Recommended Standard site table:

| Site | Role | productionRate | inputCapacityTons | outputCapacityTons | licencePrice | purchasePrice | industryOwnerCut |
| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: |
| MineralMine | RawProducer | 24 | 60 | 360 | 4000 | 100000 | 0.20 |
| CementFactory | ProcessingPlant | 18 | 270 | 170 | 6000 | 120000 | 0.20 |
| Forest | RawProducer | 18 | 60 | 160 | 8000 | 160000 | 0.20 |
| Farm | RawProducer | 20 | 200 | 220 | 10000 | 180000 | 0.20 |
| OilField | RawProducer | 20 | 60 | 180 | 12000 | 220000 | 0.25 |
| Sawmill | ProcessingPlant | 18 | 240 | 240 | 12000 | 220000 | 0.25 |
| AlcoholFactory | ProcessingPlant | 16 | 240 | 150 | 14000 | 240000 | 0.25 |
| Slaughterhouse | ProcessingPlant | 16 | 240 | 150 | 14000 | 240000 | 0.25 |
| BricksFactory | ProcessingPlant | 16 | 360 | 160 | 15000 | 260000 | 0.25 |
| PlasticFactory | ProcessingPlant | 24 | 320 | 180 | 18000 | 300000 | 0.25 |
| FabricFactory | ProcessingPlant | 16 | 360 | 160 | 18000 | 300000 | 0.25 |
| ConcreteFactory | ProcessingPlant | 16 | 360 | 160 | 18000 | 300000 | 0.25 |
| MechanicalPartsFactory | ManufacturingPlant | 14 | 360 | 160 | 20000 | 320000 | 0.30 |
| ClothingFactory | ManufacturingPlant | 14 | 240 | 150 | 20000 | 320000 | 0.30 |
| LiquidFertilizerFactory | ProcessingPlant | 16 | 360 | 150 | 20000 | 340000 | 0.30 |
| WoodFurnitureFactory | ManufacturingPlant | 12 | 240 | 140 | 22000 | 350000 | 0.30 |
| Refinery | ProcessingPlant | 48 | 480 | 240 | 18000 | 350000 | 0.30 |
| [FoodProcessingFactory] | ProcessingPlant | 18 | 360 | 180 | 22000 | 420000 | 0.30 |
| BeamFactory | ManufacturingPlant | 14 | 240 | 220 | 22000 | 380000 | 0.30 |
| SmeltingFactory | ProcessingPlant | 20 | 360 | 360 | 28000 | 500000 | 0.30 |
| ChemicalPlant | ProcessingPlant | 16 | 500 | 160 | 24000 | 480000 | 0.30 |
| WaterSupply | RawProducer | 20 | 60 | 200 | 18000 | 300000 | 0.25 |
| ProcessorFactory | ManufacturingPlant | 14 | 480 | 240 | 35000 | 650000 | 0.35 |
| ConsumerElectronicsFactory | ManufacturingPlant | 12 | 360 | 360 | 45000 | 900000 | 0.35 |
| PharmaceuticalFactory | ManufacturingPlant | 10 | 360 | 120 | 50000 | 1000000 | 0.35 |
| RecyclingCenter | RecyclingHub | 24 | 320 | 360 | 55000 | 1100000 | 0.35 |
| VehicleFactory | ManufacturingPlant | 8 | 720 | 240 | 70000 | 1600000 | 0.40 |
| OmegaFactory | SpecialPlant | 12 | 360 | 180 | 90000 | 2000000 | 0.40 |

Why these values fit the progression better:

- early sites are cheap enough to own before the economy reaches trailers
- complex sites are no longer identical to simple sites
- late sites are late because of chain depth and cost, not because every site is the same 450000 wall
- multi-output sites keep larger total output buffers so they do not jam immediately
- refinery output is intentionally elevated because the fuel sink family is a city-wide backbone

## 4.5 Warehouses, Standard targets

| Site | purchasePrice | inputCapacityTons | outputCapacityTons | industryOwnerCut |
| --- | ---: | ---: | ---: | ---: |
| HarborTerminal | 240000 | 900 | 900 | 0.10 |
| Warehouse2 Grain Silo | 120000 | 400 | 400 | 0.10 |
| Warehouse3 Secure Unit Storage | 280000 | 1100 | 1100 | 0.10 |
| Warehouse4 Oil Storage Tanks | 320000 | 600 | 600 | 0.10 |
| Warehouse7 Ore Storage Unit | 220000 | 600 | 600 | 0.10 |
| Warehouse5 Cold Storage Unit | 180000 | 300 | 300 | 0.10 |
| Warehouse6 Grand Senora Container Storage | 200000 | 600 | 600 | 0.10 |

Warehouses should remain permit-free utility assets.
They are progression stabilizers, not end goals.

## 4.6 Sink templates, Standard targets

### Gas stations

Use these values by density. Keep the Port Office station free and zero-drain.

| Density | inputCapacityTons | emptyingRate | purchasePrice base |
| --- | ---: | ---: | ---: |
| VeryLow | 20 | 0.02 | 45000 |
| Low | 35 | 0.04 | 70000 |
| Medium | 60 | 0.07 | 110000 |
| High | 90 | 0.10 | 160000 |
| Office starter | 40 | 0 | 0 |

Apply a tier modifier to purchasePrice:

- Local x1.00
- Regional x1.20
- Core x1.35

### Stores

| Density | inputCapacityTons | emptyingRate | purchasePrice base |
| --- | ---: | ---: | ---: |
| VeryLow | 6 | 0.03 | 35000 |
| Low | 12 | 0.07 | 55000 |
| Medium | 24 | 0.12 | 85000 |
| High | 40 | 0.18 | 130000 |

Apply a tier modifier to purchasePrice:

- Local x1.00
- Regional x1.20
- Core x1.40

### Construction sinks

| Density | inputCapacityTons | emptyingRate | purchasePrice |
| --- | ---: | ---: | ---: |
| Low | 20 | 0.05 | 140000 |
| Medium | 40 | 0.10 | 200000 |
| High | 60 | 0.15 | 260000 |

These sink rates are intentionally far below the current values.
The current city-wide drain is too large for the configured production network.

## 4.7 Sink and chain-end fixes

The following content fixes are needed because some commodities are true dead ends or close to it.
Numbers alone cannot solve that.

Recommended content changes in Sites.xml:

1. Add Medicine to Davis Carson Store and Paleto Supermarket.
2. Add Furniture to Burton Mall, Davis Carson Store, and Paleto Store.
3. Change ProcessorFactory primary inputs from Plastic, Alloy, Chemicals to Plastic, Metal, Chemicals so Metal is not a dead-end output.
4. Add a real Vehicles sink later, preferably a dealership or export yard site.
5. Add a Paper sink later, preferably an export terminal or print-distribution site.

If you do not want to add new sites yet, items 1 to 3 are the smallest content edits that materially improve chain closure.

## 4.8 Offices, apartments, office objects, and NPC workforce

### Offices

| Office | price | weeklyRent | maxCommercialVehicles |
| --- | ---: | ---: | ---: |
| Grand Senora | 45000 | 1800 | 4 |
| Sandy Shores | 70000 | 2800 | 6 |
| San Chianski | 110000 | 4200 | 8 |
| Paleto Bay | 180000 | 7000 | 10 |
| La Mesa | 260000 | 10000 | 12 |
| Vespucci | 300000 | 11500 | 10 |
| Strawberry | 420000 | 16000 | 16 |
| Vinewood | 550000 | 21000 | 22 |
| Banning | 700000 | 27000 | 24 |
| Elysian Island | 900000 | 36000 | 40 |

### Apartments

| Apartment class | price | weeklyRent |
| --- | ---: | ---: |
| Low Tier Apt | 90000 | 2500 |
| Low Range Apt | 90000 | 2500 |
| Mid Range Apt | 180000 | 5000 |
| Modern Apartment | 320000 | 9500 |

### Office objects

| Object | price | capacity | limit |
| --- | ---: | ---: | ---: |
| Dieseltank | 15000 | 15000 L | 1 |
| Construction Site Cabin | 25000 | 3 NPC | 4 |
| Maintenance Bay | 8000 | n/a | 1 |
| Decorative items | keep | keep | keep |

### NPC tiers

Recommended HiringNPC.xml values:

| Tier | cargoLossRate | speedMultiplier | priceMultiplier | weeklyWageCasual | weeklyWageStandard | weeklyWageHardcore |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Rookie | 0.35 | 0.65 | 4 | 1200 | 1600 | 2200 |
| Professional | 0.18 | 0.82 | 7 | 2500 | 3300 | 4500 |
| Veteran | 0.08 | 0.92 | 11 | 4500 | 5600 | 7500 |

Why lower priceMultiplier so aggressively:

The current fee is tier.PriceMultiplier multiplied by the commodity unit price for each route.
That means a Veteran TV route currently costs 650000 just to configure.
That is far outside the rest of the economy.

## 4.9 Difficulty transforms

Use Standard as the authored base.
Do not hand-author three unrelated economies.

### Casual

- productionRate x1.20
- inputCapacityTons x1.30
- outputCapacityTons x1.30
- licencePrice x0.70
- purchasePrice x0.75
- sink emptyingRate x0.90
- industryOwnerCut minus 0.05, floor 0.15
- NPC wages: Casual column

Casual should feel generous, but it should still use the same systems.
It should not simply turn the economy off.

### Standard

- use the tables in this report as-is
- NPC wages: Standard column

Standard should be the default intended progression.

### Hardcore

- productionRate x0.85
- inputCapacityTons x0.80
- outputCapacityTons x0.80
- licencePrice x1.35
- purchasePrice x1.40
- sink emptyingRate x1.15
- industryOwnerCut plus 0.05, cap 0.45
- NPC wages: Hardcore column

Hardcore should be tighter on cash flow and buffers, not just a bigger grind multiplier.
The first accessible permit still needs to remain affordable from a fresh save.

## 5. Concrete edit plan

### 5.1 Code

- src/Systems/GlobalMarketManager.cs
  - replace the BasePrices dictionary with the full commodity table above
  - price Electronic explicitly instead of Processors, or support both keys
- src/Systems/IndustryManager.cs
  - change ComputeDeliveryProfit so non-sink deliveries pay 0.35 of unit price instead of 0.15
  - apply preset-swapped purchase, permit, and capacity values to Store and GasStation locations too
  - optionally let Omega boost work when Omega is configured as an optional input
- src/Config/ModConfig.cs
  - keep as-is if IndustryManager starts applying presets to all CSV-backed sites
  - otherwise remove the Standard-only bake for non-industry locations and centralize that decision in IndustryManager
- src/Domain/Industry.cs
  - no Phase A change required if you rebalance around equal capacity sharing
  - Phase B only: per-commodity capacity weights or recipe weights
- src/Systems/OfficeObjectManager.cs
  - optional: change FuelDeliveryPriceMultiplier from 0.85 to 1.05
- src/LSOLScript.cs
  - strongly recommended: bundle EconomyDifficultyPreset with licensing, industry pricing, and NPC wage defaults so a preset is a real preset

### 5.2 Data

- LSOL_Config/Sites.xml
  - rewrite all production-site Economy blocks to the Standard table and transform rules above
  - set productionRatio to 1 everywhere unless you intentionally want a second multiplier
  - rewrite warehouse Economy blocks
  - rewrite store, fuel, and construction sink Economy capacities and purchase prices
  - rewrite sink emptyingRate values to the density templates above
  - add explicit industryOwnerCut values instead of relying on the current implicit 0.5 default
  - add the chain-end sink fixes listed in section 4.7
- LSOL_Config/Core.xml
  - keep density fallback values aligned with the same sink templates used in Sites.xml, or remove the duplication later
- LSOL_Config/Vehicles.xml
  - normalize vehicle classes to the table above
  - change tanker IDs 23 and 24 to 30 t capacity and new price/rent values
- LSOL_Config/HiringNPC.xml
  - replace priceMultiplier and wage columns with the values above
- LSOL_Config/Offices.xml
  - replace office price, rent, and slot caps with the values above
- LSOL_Config/Interiors.xml
  - replace apartment price and rent values with the values above
- LSOL_Config/OfficeObjects.xml
  - update diesel tank, cabin, and maintenance-bay values

## 6. Structural limits that still remain after Phase A

A good Phase A rebalance is possible after the small code fixes above.
A perfect simulation economy is not, because the current model still has these deeper constraints:

1. recipes are still generic 1:1 input-to-output mappings
2. multi-output sites still create equal output quantities for every output commodity
3. optional inputs still work only as throughput boosts, not as real secondary recipe inputs
4. capacities are still split evenly across every input, optional input, and output
5. the market can only rise above base price; it does not create true oversupply penalties

If you eventually want a deeper economy, Phase B should add:

- per-commodity recipe weights in the site schema
- per-site or per-role delivery payout multipliers
- a separate optional boost schema instead of overloading OptionalInputs
- external commodity price config instead of hardcoding the price table in C#

## 7. Bottom line

If you only do one thing, do not start by changing every number in Sites.xml.
Start by fixing the three controlling problems first:

- non-sink delivery payout is too low
- store and gas presets are not really difficulty-aware
- most commodities are accidentally stuck at 400/t

After that, the data rebalance above will give you a coherent progression curve:

- low-tier hauling becomes viable without making late game trivial
- permits become meaningful without soft-locking a 20000 start
- sink ownership becomes attractive instead of mandatory for basic profit
- larger cargo capacity matters at the right moment
- late-game goods stay better than raw goods without creating runaway single-run jackpots
