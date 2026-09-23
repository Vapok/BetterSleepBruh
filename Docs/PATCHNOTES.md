# 2.0.8 - Internalized Library & Dependency Updates
* **Dependency Updates**:
  * Updated internalized `Vapok.Valheim.Common` to 3.19.1015.
  * Updated `JotunnLib` dependency to 2.30.2.
* **Code Cleanliness**:
  * Replaced lazy `var` declarations with explicit types across mod startup and UI construction.

# 2.0.7 - Dedicated Server Sleep Tracking Hardening & Valheim 1.0.15 Alignment
* **SleepTracker & GamePatches Null Safety**:
  * In `Game.cs` (`UpdateSleepingPatch.Prefix`), guarded `ZRoutedRpc.instance?.InvokeRoutedRPC(...)` against null instances when `SleepStop` or `SleepStart` RPCs trigger.
  * In `SleepTracker.cs`, added null and validity checks for character `ZDO` instances in `IsCharacterInBedForBoost` and `GetSleepOccupancyCounts`.
  * Added null-conditional invocation for all `ZRoutedRpc.instance` registrations and RPC dispatches in `SleepTracker`.
* **Valheim 1.0.15 Game Reference Alignment**:
  * Updated game assembly bindings to target Valheim `1.0.15`.
  * Internalized `Vapok.Valheim.Common` v3.13.1015.

# 2.0.6 - Splash Window Updates & Valheim 1.0.14 Alignment
* **Splash Window Updates**:
  * Updated telemetry default to unchecked on first launch (Opt-In).
  * Added Send Error Logs toggle (Opt-Out) to capture anonymous crash diagnostics and error reports.
  * Added in-game scrollable Privacy Policy overlay with responsive mouse wheel support.
  * Added interactive tooltip data disclaimers on checkbox hover.
* **Valheim 1.0.14 Alignment**:
  * Aligned publicized game assembly and UnityEngine references to Valheim 1.0.14.
  * Updated internalized  dependency to 3.12.1014.

# 2.0.5 - Jewelcrafting Font Compatibility
* **Compatibility Fix**: Fixed issue where Jewelcrafting packages its own font which was overriding part of a vanilla font, causing the Splash screen to appear blank.
* **Vapok.Common Dependency Bump**: Updated internalized dependency to `Vapok.Valheim.Common` 3.11.1012.

# 2.0.4 - Updated README with Telemetry Information
* **Documentation Update**: Updated the README.md with Anonymous Telemetry and Privacy section per request of mod stores.
* **Vapok.Common Dependency Bump**: Updated internalized dependency to `Vapok.Valheim.Common` 3.9.1012.

# 2.0.3 - Unified Splash Screen & Telemetry Controls
* **Unified Startup Splash Screen & Telemetry**:
  * Updated `Vapok.Valheim.Common` dependency reference to `v3.5.1012`.
  * Registered mod metadata with centralized `ModSplashManager`.
  * Added `ShowSplashOnStartup` and `Enable Anonymous Telemetry` configuration bindings to `ConfigRegistry`.

# 2.0.1 - Dependency & Compatibility Maintenance
* **Runtime & Dependency Updates**:
  * Synchronized package manifest and project references with Jotunn `2.30.0` and BepInEx `5.4.2350`.
  * Verified build pipeline and ILRepack bundling with `Vapok.Valheim.Common` `3.2.1012`.
* **Compatibility & Documentation**:
  * Validated environment time progression, sleep rate calculations, and HUD overlay against current Valheim 1.0 builds.
  * Standardized mod documentation, changelog tiers, and release staging.

# 2.0.0 - Valheim 1.0 Update & Environment System Refactoring
* **Valheim 1.0 Compatibility**:
  * Updated assembly references for Valheim 1.0 (`1.0.12`), BepInEx 5.4.2350, and Jotunn 2.30.0.
  * Rebuilt on .NET Framework 4.8.
  * Bundled latest `Vapok.Valheim.Common` 3.2.1012 via ILRepack.
* **Environment & Time Synchronization (`EnvMan` / `ZNet`)**:
  * Updated Harmony patches targeting `EnvMan.Update` and `EnvMan.UpdateTime` for Valheim 1.0 internal timing loop changes.
  * Refactored world time progression calculations to ensure smooth transition during sleep acceleration without client desync.
  * Synchronized weather and environment state changes across dedicated servers and connected clients via network RPCs.
* **Sleep & Bed Mechanics Patches**:
  * Updated `Game.UpdateSleeping` and `Bed.Interact` patches to properly handle Valheim 1.0 dream/sleep state transitions.
  * Added defensive null checks when querying local player bed references and nearby enemies during sleep eligibility checks.

# 1.0.2 - Time Advancement Fixes & Dependency Updates
* Resolved edge-case issue where day cycle counters failed to increment under rapid sleep transitions.
* Updated Jotunn and BepInEx references.

# 1.0.1 - Dedicated Server Sync & Null Safety
* Fixed configuration sync on dedicated servers.
* Added null checks to avoid errors when sleeping without active environment managers initialized.

# 1.0.0 - Initial Release of BetterSleepBruh
* Initial release of time, environment, and sleep manipulation mod.
* Implemented configurable sleep speed, bed requirements, and day cycle skipping.
