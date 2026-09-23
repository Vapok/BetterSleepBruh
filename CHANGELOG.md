# 2.0.10 - Configuration Sync & Stability Updates

* **Sleep HUD Visibility**: Fixed an issue where the sleep HUD would not appear for some players on dedicated servers or multiplayer.
* **Configuration Sync**: Resolved a library configuration synchronization issue.
* **Dependency Updates**: Updated internal dependencies for stability.

<details>
<summary><b>2.0 Changelog History (Valheim Release)</b> (<i>click to expand</i>)</summary>

### 2.0.9 - Performance Optimizations & Time Sync Smoothness
* **Nighttime Stutter Fix**: Fixed game stuttering when players get into bed by updating player counts only when players enter or leave bed instead of checking every game frame.
* **Smoother Fast-Forward**: Network time is now synchronized more frequently during sleep acceleration to prevent sudden time jumps on multiplayer servers.
* **Time Acceleration Indicator**: Added animated flashing arrows in place of the bed icon that blink faster as sleep boost increases, giving a clear visual cue when nighttime is speeding up.
* **Testing Mode Improvements**: Testing mode now properly adds simulated players to real player counts, making it easier to test sleep acceleration when playing alone.
* **Display Improvements**: Cleaned up the sleep pillow display so it appears right away and doesn't show console warnings about fonts.

### 2.0.8 - Dependency Updates & Code Maintenance
* **Dependency Updates**: Updated Jotunn to 2.30.2 and internal dependencies for stability.
* **Code Maintenance**: Refactored internal code to adhere to workspace engineering standards.

### 2.0.7 - Dedicated Server Sleep Tracking Hardening & Valheim 1.0.15 Alignment
* **Dedicated Server Sleep Tracking Hardening**: Added null-safety checks across network calls and character bed occupancy checks, preventing crashes on dedicated servers during sleep cycles and time-skip transitions.
* **Valheim 1.0.15 Alignment**: Updated all game assembly references for stability with the latest game patch.

### 2.0.6 - Splash Window Updates & Valheim 1.0.14 Alignment
* **Splash Window Updates**:
  * Telemetry is now unchecked when first loaded (Opt-In visibility)
  * Added Send Error Logs (Opt-Out)
  * Privacy Policy is now available directly in-game
  * Added Data Disclaimers on hover over checkboxes for transparency on what data is sent
* **Valheim 1.0.14 Alignment**: Updated game assembly references for Valheim 1.0.14.

### 2.0.5 - Jewelcrafting Font Compatibility
* Fixed: Jewelcrafting packages its own font which was overriding part of a vanilla font, causing the Splash screen to appear blank.

### 2.0.4 - Updated README with Telemetry Information
* Updated the README.md with Anonymous Telemetry information per request of mod stores.

### 2.0.3 - Unified Splash Screen & Telemetry Controls
* **Unified Startup Splash Screen**: Integrated with a centralized startup splash screen.
  * Added configurable `Show on Game Startup` which can be enabled or disabled in the configuration file.
* **Anonymous Telemetry**: 
  * Added configurable `Enable Anonymous Telemetry` configuration which can be enabled or disabled in the configuration file.
    * Defaults to enabled with auto-opt-in on launch. Uncheck to Opt-Out
    * ANONYMOUS DATA ONLY - I track version number and usage data. No personal data is ever collected. For more information, see the [Privacy Policy](https://vapok.io/privacy-policy/).

### 2.0.1 - Dependency & Compatibility Maintenance
* **Dependency Updates**: Updated Jotunn and BepInEx runtime package bindings.
* **Compatibility Maintenance**: Verified compatibility against the latest Valheim 1.0 release.
* **Documentation Improvements**: Standardized README, user guides, and technical patch documentation.

### 2.0.0 - Updating for Valheim 1.0+
* Updated codebase and game references for Valheim 1.0.
* Modernized world time and sleep progression mechanics.
* Improved dedicated server synchronization and stability.

</details>
