# Spectre Maze
Manual can be found here: https://ktane.timwi.de/HTML/Spectre%20Maze.html

## Mission Settings
The settings are structured as follows: 

`[Spectre Maze]:3;64-80;1`

These values are in order:
- The number of layers that target position displays. Must be at least 1.
- The lower bound on the distance (scored by calculation difficulty), inclusive. Must not be negative.
- The upper bound on the distance (scored by calculation difficulty), exclusive. Must be greater that the lower bound.
- The maze porosity, or how many extra useful openings are added to a minimal maze. Must be within the range of 0-10, both bounds inclusive.

If everything is set up correctly, a test run should reveal so in the logfile. If not, mod settings will be relied on instead.

Not putting any settings on your mission may allow a player to cheese the module, so it is recommended to put settings on any mission you wish to put this module on.
