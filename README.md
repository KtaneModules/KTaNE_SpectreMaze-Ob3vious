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

If any of these constraints are not followed, the module will ignore the setting. If it has been done correctly, a test run should reveal so in the logfile.
