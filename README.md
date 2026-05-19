# AHCodeSamples
Code samples of code I wrote from Art House for my portfolio. All scripts are in C#

### My role
I was responsible for prototyping and implementing obstacles including lightning clouds and rips (portals). Also continuously improved scripts on enemy behaviors to improve player experience especially on the shifters.

### Files
 -  LinkedRip.cs: handles the linkedrips which are portals essential to the games mechanics that allow the player and certain enemies to teleport. IE shifters spawn these.
 -  PaintableObject.cs: Another core mechanic I scripted which allows the player to paint an unpainted object and also store it in inventory to move it around to complete puzzles.
 -  shifter.cs: the shifter enemy I worked on that is able to create linked rips and finds teleportpaths close to the player. Will path to the nearest rip to reach the player. Script has serialized fields
 to allow for adjustability.