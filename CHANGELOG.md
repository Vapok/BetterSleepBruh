# Better Sleep _Bruh!_ Changelog

### 1.0.2 - Client Gui Display Fix
* Fixed: Sometimes the Better Sleep UI would not appear on initial load and then never appear.
* Note: This mod does NOT require connect clients to install the mod. But if they want the user interface, the mod must be installed client side.
  * All functions of sleep are managed on the server and controlled through vanilla actions.

### 1.0.1 - Bug Fixes and Sleep Calculation Refactor
* Refactored Sleep Calculations
* Refactored Mod Load and Initializers
* Added "Use Vanilla Start Sleep" config to not change Valheim's Start Sleep calucation.
* Added Additional Bonus Configuration Variables
  * Bonus Increment Scale - Default: 20x
    * Adjust the scale factor of benefit of the sleep bonus. This allows server admins to adjust the overall sleep benefit to their liking.
  * Boost Fade (in Second) - Default 3 seconds
    * Adjusts when the boosting will scale down prior to morning, this allows a smooth ramp down to normal speed as morning approaches.
* Added Testing Capabilities
  * All New Settings Override Server Values for this Mod ONLY. Doesn't actually affect number of players on server or in bed. Used for testing the GUI and Timepseed on the server.
  * Testing Configuration Settings:
    * Enable Testing Mode - Default: false
      * You should NOT enable this on a real public server without warning players. 
      * Used for testing settings and making sure you have the right speed for your server.
    * Fake Total Players
      * Spoofs the total number of players on the server that the mod sees.
    * Simulate Players In Bed
      * Spoofs number of players in bed. Does NOT count number of REAL players in bed.
* Fixed Bug: Not All Players would Register as Players. This caused incorrect max percentages.
* Fixed Bug: When Player logs out (but not quit Valheim game) mod would not reload.

### 1.0.0 - Better Sleep _Bruh!_ Initial Release
Gone are the days of asking for everyone to run to a bed, or to log off, just to make night go away faster.  Get Better Sleep Bruh! The mod that allows players on a server help make night go faster without completely eliminating night. This allows players to still enjoy benefits of night but also not have to run to a bed to make morning come faster!
* Provides the following Configurations:
  * Adjusts when players are allowed to sleep.  Default vanilla is Noon.
  * Adjust the maximum Bonus Multiplier provided when some players are sleeping.
    * Here's an Example:
      * Let's say you have 5 players on your server.  And Maximum Bonus Multiplier is 60% of the time speed if everyone was sleeping.
        * If 5 players jump into bed, then you fall into a dark dream state and hope you can read your dream fast enough before it goes away.
        * If 4 players are in bed, but one player is stuck on a boat, the Bonus Multipler of 60% is applied.
        * If 3 players are in bed, but two players are stuck on a boat, the Bonus Multipler of 45% is applied.
        * If 2 players are in bed, but one player is stuck in a boat, one player is stuck in a mine, and another player is AFK, the Bonus Multiplier of 30% is applied.
        * If 1 player are in  bed, and they've been abandoned by all of the other players who went AFK to look at TikTok video, the Bonus Multiplier of 15% is applied.
        * If 0 players are in bed, night passes like normally, with no speed bonus, and clearly no dreams.  You'll be tired tomorrow. I'm sure of it.
    * If you've ever played Enshrouded and wished "Man, I wish I could sleep like in Enshrouded!" This mod is for you.
* Pillow Icons are displayed for each player on the server.  As players jump into bed, pillows highlight showing you the number of players in bed.
  * To be clear, this not been tested on very large servers.  So if you complain about why this doesn't work with 100 players, I'm just going to wrinkle my eyebrow at you.

