<div align="center">

# 🛏️ Better Sleep *Bruh!*

### *Dynamic sleep acceleration and multiplayer time progression for Valheim.*

[![GitHub Release](https://img.shields.io/github/v/release/Vapok/BetterSleepBruh?include_prereleases&logo=github&style=for-the-badge)](https://github.com/Vapok/BetterSleepBruh/releases)
[![Thunderstore Version](https://img.shields.io/thunderstore/v/Vapok/BetterSleepBruh?logo=thunderstore&style=for-the-badge)](https://thunderstore.io/c/valheim/p/Vapok/BetterSleepBruh/)
[![Nexus Mods](https://img.shields.io/badge/Nexus_Mods-Available-da8e35?logo=nexusmods&style=for-the-badge)](https://www.nexusmods.com/valheim/mods/3280)
[![Discord](https://img.shields.io/badge/Discord-Join%20Community-7289da?logo=discord&logoColor=white&style=for-the-badge)](https://discord.gg/5YAJkRFBXt)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg?style=for-the-badge)](https://opensource.org/licenses/MIT)

---

</div>

Gone are the days of yelling in chat for everyone to sprint to bed or log off just to make the dark, perilous night pass. **Better Sleep Bruh!** introduces dynamic time acceleration to Valheim: the more players resting in bed on a multiplayer server, the faster night ticks away!

---

## 🌙 How It Works

* 💤 **Proportional Acceleration**: Nighttime automatically accelerates proportionally to the percentage of players currently sleeping in beds.
* 🛌 **No 100% Requirement**: If some players are out sailing on a longship, mining in a crypt, or AFK, players back at base can still sleep to significantly speed up the night.
* 🪶 **On-Screen Pillow HUD**: An intuitive on-screen HUD displays pillow icons for all connected players, lighting up in real time as Vikings tuck themselves into bed.
* 🌅 **Smooth Morning Ramp-Down**: Time speed smoothly transitions back to 1x as dawn approaches so you never get jolted awake.

### 📊 Sleep Speed Example

| Players Connected | Players in Bed | Active Speed Bonus | Nighttime Experience |
| :---: | :---: | :---: | :--- |
| **5** | **5 (100%)** | **100%** | Full vanilla dream state; instant skip to morning. |
| **5** | **4 (80%)** | **80%** | Blazing-fast night progression. |
| **5** | **3 (60%)** | **60%** | Fast night acceleration while miners/sailors continue working. |
| **5** | **1 (20%)** | **20%** | Noticeable time speedup for the solo sleeper. |
| **5** | **0 (0%)** | **0%** | Normal vanilla passage of time. |

---

## ⚙️ Configuration & Settings

Configure via the in-game [BepInEx Configuration Manager](https://github.com/BepInEx/BepInEx.ConfigurationManager) (<kbd>F1</kbd>) or in `BetterSleepBruh.cfg`:

| Setting | Default | Description |
| :--- | :--- | :--- |
| **Sleep Start Hour** | `12.0` *(Noon)* | Earliest in-game time of day when players are permitted to sleep. |
| **Bonus Increment Scale** | `20x` | Multiplier scaling the speed bonus per sleeping player. |
| **Boost Fade Duration** | `3.0s` | Seconds before morning when time acceleration ramps down to standard speed. |
| **Use Vanilla Start Sleep** | `false` | When enabled, enforces standard vanilla nighttime sleep eligibility checks. |
| **Enable Testing Mode** | `false` | Developer testing mode to simulate connected and sleeping players. |

---

## 🛡️ Advanced Safeguards

* 🖥️ **Server-Driven Operation**: Time calculations run strictly on the server. Clients do not need the mod installed to benefit from sleep acceleration (the client mod is only required for the on-screen Pillow HUD).
* 🔄 **Smooth Environment Sync**: Weather, skybox lighting, and day counters transition smoothly across all clients without stutter or desync.
* 🌐 **ServerSync Enforced**: Configuration parameters are managed by server admins and synced to all clients automatically.

---

## 🤝 Verified Mod Compatibility

<div align="center">

| Mod | Compatibility Status |
| :--- | :--- |
| **AdventureBackpacks** | 🟢 Fully Supported |
| **NoFogBruh** | 🟢 Fully Supported |
| **Valheim Plus** | 🟢 Fully Supported |
| **Jotunn / JVL Mods** | 🟢 Fully Supported |

</div>

---

## 🌐 Available Translations

<div align="center">

🇺🇸 **English** (Default)

</div>

*Want to help translate Better Sleep Bruh? Community translations are welcome! Please submit a PR on [GitHub](https://github.com/Vapok/BetterSleepBruh) or stop by our [Discord](https://discord.gg/5YAJkRFBXt).*

---

## 📥 Installation & Server Setup

### Mod Manager (Recommended)
1. Install via **R2ModMan** or **Thunderstore Mod Manager**.
2. Dependencies (`BepInExPack`, `Jotunn (JVL)`) are installed automatically.

### Dedicated Servers
* **Server Installation**: Place the DLL on the dedicated server to enable sleep acceleration for all players.
* **Client Installation (Optional)**: Recommended for players who want to see the visual on-screen Pillow HUD.

---

<div align="center">

### 👨‍💻 Created by Vapok Gaming

[![Vapok Gaming](https://avatars.githubusercontent.com/u/1264136?s=120&v=4)](https://github.com/Vapok)

**Author**: [Vapok](https://github.com/Vapok)  
**Source Code**: [GitHub Repository](https://github.com/Vapok/BetterSleepBruh)  
**Community & Support**: [Discord Server](https://discord.gg/5YAJkRFBXt)  
**Changelog**: [Release Notes](https://github.com/Vapok/BetterSleepBruh/blob/main/CHANGELOG.md)

</div>
