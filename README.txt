Industry Logistic V - README
============================

1) What This Script Does
------------------------
Industry Logistic V adds a logistics management loop to GTA V:
- Multiple industries produce/consume commodities.
- You transport goods with cargo vehicles.
- Profit is generated from deliveries.
- Industries can be upgraded with module purchases.
- A themed in-game tablet UI is used for industry operations.


2) Tablet UI (Current Behavior)
-------------------------------
The tablet now uses keyboard navigation only (mouse selector removed):
- Up / Down arrows: move selection
- Enter: activate selected row
- Backspace / Esc: go back (or close from main tablet page)
- E: close tablet

Tablet pages:
- Main page:
  - Load cargo truck
  - Unload Omega fluid
  - View statistics (toggle stats panel)
  - Open upgrades (switches to upgrades tablet page)
- Upgrades page:
  - Production Module
  - Input Storage Module
  - Output Storage Module
  - Omega Tank Module
  - Back to operations

Upgrade purchases are now applied directly inside the tablet upgrades page.


3) Installation
---------------
Required base mods:
- ScriptHookV
- ScriptHookVDotNet (v3)

Install steps:
1. Build the project OR use the compiled DLL.
2. Copy IndustryLogisticV.dll to your GTA V scripts folder:
   - GTA5/scripts/IndustryLogisticV.dll
3. Ensure LemonUI.SHVDN3.dll is present in scripts:
   - GTA5/scripts/LemonUI.SHVDN3.dll
4. Place config.ini where the script can find it:
   - Preferred: GTA5/scripts/config.ini
   - Also supported by this script: GTA5/config.ini
5. Place tablet fallback image (recommended path):
   - GTA5/scripts/tablet_images/tablet_template.png


4) Important Background/Image Note
----------------------------------
The tablet visuals can come from two sources:

A) Streamed texture dictionary
- Name expected by code: industry_tablet
- Textures expected: tablet_frame, tablet_background

B) PNG fallback image
- File expected: tablet_template.png
- Searched in these locations:
  - GTA5/tablet_images/tablet_template.png
  - GTA5/scripts/tablet_images/tablet_template.png
  - scripts folder next to the DLL and nearby parent folders

If you do not have a valid industry_tablet texture dictionary available,
keep tablet_template.png in GTA5/scripts/tablet_images/.


5) Controls (from config.ini)
-----------------------------
[Controls]
- ToggleDashboard = F8
- ToggleContext = F6
- Interact = E
- OpenUpgrade = U (legacy key; tablet now includes upgrades page)
- MenuUp = Up
- MenuDown = Down
- MenuLeft = Left
- MenuRight = Right
- MenuSelect = Enter
- MenuBack = Backspace


6) Config Highlights
--------------------
[General]
- OmegaMultiplier
- IndustryOmegaCapacityMultiplier

[Markers]
- MarkerRadius
- MarkerHeight

Industry sections define coordinates, inputs/outputs, and capacities.


7) Troubleshooting
------------------
Tablet background not visible:
1. Verify GTA5/scripts/tablet_images/tablet_template.png exists.
2. Verify filename is exactly tablet_template.png.
3. Verify IndustryLogisticV.dll is loaded from GTA5/scripts.
4. Check ScriptHookVDotNet log for load errors.
5. Ensure LemonUI.SHVDN3.dll exists in GTA5/scripts.

Tablet does not open:
1. Ensure Interact key in config.ini matches your intended key.
2. Move close to an industry marker and be on foot.

Upgrades unavailable:
1. Some modules can be unavailable depending on industry type.
2. Ensure you have enough profit for module cost.


8) Build
--------
A standard Release build from repo root:
- dotnet build IndustryLogisticV.csproj -c Release

Output DLL:
- bin/Release/net48/IndustryLogisticV.dll
