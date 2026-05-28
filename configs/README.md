This root `configs/` folder is archived legacy reference content and is not part of the current LSOL runtime loader.

Current builds load loose runtime content from `scripts/LSOL/LSOL_Config/*.xml` and `scripts/LSOL/LSOL_Config/missions/*.xml`, plus packaged add-on content from `scripts/LSOL/LSOL_Addons/*/content/*`.

The build removes this folder from output. Keep material here only when it is needed as repo-side reference input for manual workflows, especially `tools/migrate-legacy-configs.ps1`.

Do not edit files here expecting runtime behavior to change. Runtime edits belong in `scripts/LSOL/LSOL_Config` or packaged `scripts/LSOL/LSOL_Addons` content.