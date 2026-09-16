# 2.0.2 - Unified Splash Screen & Telemetry Controls
* **Unified Startup Splash Screen**: Integrated with a centralized startup splash screen.
  * Added configurable `Show on Game Startup` which can be enabled or disabled in the configuration file.
* **Anonymous Telemetry**: 
  * Added configurable `Enable Anonymous Telemetry` configuration which can be enabled or disabled in the configuration file.
    * Defaults to enabled with auto-opt-in on launch. Uncheck to Opt-Out
    * ANONYMOUS DATA ONLY - I track version number and usage data. No personal data is ever collected. For more information, see the [Privacy Policy](https://vapok.io/privacy-policy/).

<details>
<summary><b>2.0 Changelog History (Valheim Release)</b> (<i>click to expand</i>)</summary>

### 2.0.1 - Dependency & Compatibility Maintenance
* **Dependency Updates**: Updated Jotunn and BepInEx runtime package bindings.
* **Compatibility Maintenance**: Verified compatibility against the latest Valheim 1.0 release.
* **Documentation Improvements**: Standardized README, user guides, and technical patch documentation.

### 2.0.0 - Updating for Valheim 1.0+
* Updated codebase and game references for Valheim 1.0.
* Modernized world time and sleep progression mechanics.
* Improved dedicated server synchronization and stability.

</details>

<details>
<summary><b>1.0 Changelog History (Valheim Early Access)</b> (<i>click to expand</i>)</summary>

### 1.0.2 - Client GUI Display Fix
* Fixed: Resolved issue where the Better Sleep UI would occasionally fail to appear on initial load.
* Note: Server-side sleep mechanics function automatically; the client mod is only required if players want to view the pillow HUD display.

### 1.0.1 - Bug Fixes and Sleep Calculation Refactor
* Refactored sleep speed and time progression calculations.
* Added "Use Vanilla Start Sleep" configuration toggle.
* Added configurable Bonus Increment Scale and Boost Fade settings for smooth morning ramp-downs.
* Added server testing simulation configuration options.
* Fixed: Resolved player count calculation issues affecting bonus percentages.
* Fixed: Fixed mod state reload upon character logout.

### 1.0.0 - Better Sleep Bruh! Initial Release
* Initial release of dynamic sleep acceleration and time progression.
* Proportionally accelerates nighttime based on the percentage of players currently in bed.
* Added onscreen Pillow HUD indicator showing active sleeping players.
* Added configurable sleep start times and maximum bonus multipliers.

</details>
