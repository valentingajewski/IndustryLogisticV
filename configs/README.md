This root `configs/` folder is archived legacy reference content and is not part of the current LSOL runtime loader.

Current builds load loose runtime content from `LSOL_Config/*.xml` and `LSOL_Config/missions/*.xml`, plus packaged add-on content from `LSOL_Addons/*/content/*`.

The build removes this folder from output. Keep material here only when it is needed as repo-side reference input for manual workflows, especially `tools/migrate-legacy-configs.ps1`.

Do not edit files here expecting runtime behavior to change. Runtime edits belong in `LSOL_Config` or packaged `LSOL_Addons` content.