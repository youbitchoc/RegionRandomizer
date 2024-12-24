Region Randomizer for Rain World

This was my first ever mod for Rain World, so its code is... questionable (especially since my philosophy was, "If it works half the time, it's good enough"). I wouldn't recommend copying anything from this mod, but feel free to do so.

Simple breakdown:
Randomizing:
  * Upon loading a new save file, the mod calls Options.UpdateForSlugcat() (which overrides the options to suit the slugcat campaign being used (e.g: Saint starts at karma 2)) immediately before calling RandomizeAllRegions().
  * RandomizeAllRegions() runs asyncronously and: 1. Determines which regions to randomize; 2. Determines which gates should be included in the randomization process; 3. Uses LogicalRando code to determine gate connections (left side); 4. Determines gate connections from the right side; 5. Writes randomizer files.

Switching gate destinations:
  * OverWorld_GateRequestsSwitchInitiation() determines how the gate should be redirected, and then... rewrites the game's function entirely (primary change: changes which region is requested to be loaded). (Hey, it was my first ever hook. This should be an IL hook, though.)
  * OverWorld_WorldLoaded used to teleport the player out of the gate, but now it runs through a long series of checks that attempt to ensure that the gate can be exited.
  * The gate is then abstractized (basically unloaded) immediately after all players exit it. This reduces the chance of crashing upon re-entering the gate (from 100% chance to like 10%).
