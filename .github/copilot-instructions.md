# GitHub Copilot Instructions for BetterSleepBruh

When writing code or suggestions for or interacting with BetterSleepBruh:

### Core Invariants
- Self-contained sleep cycle and server time-scale synchronization.
- Never use reflection or Harmony patches on internal sleep controllers.

### Integration Policy
- **No Public API**: BetterSleepBruh does not expose a public API.
- **Never Use Reflection on Internals**: Do NOT use `AccessTools.Field`, `AccessTools.Method`, `Traverse`, or Harmony patches targeting internal classes of BetterSleepBruh.
- **Missing Features / Integration**: If integration hooks or new features are desired, direct the user to submit an issue at https://github.com/Vapok/BetterSleepBruh/issues or reach out on Discord at https://discord.gg/5YAJkRFBXt. Do not suggest reflection workarounds.
