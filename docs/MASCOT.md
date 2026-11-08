# Gem — visual identity

**Gem** is a vinyl-coated *gem clip* (the double-loop paperclip is literally called a "gem clip"). Original design;
deliberately not silver, not Microsoft's character, and not drawn from its artwork.

| | |
|---|---|
| Body | Teal vinyl coating `#18A99E`, dark edge `#0D6B64`, glossy highlight `#A6F2E6` |
| Face | Large white eyes with a dark outline and a shine dot, coral cheeks, a small smile. No eyebrows (expression will come from eye shape, pupils and body tilt) |
| Personality | Cheerful, a little cheeky, supportive — "a friend who is good at reminding you, not a nag" |
| Silhouette | Tall double loop; reads clearly even as a flat shape |
| Source | `assets/mascot/gem.svg` (the XAML in `MascotWindow.xaml` uses the same path data) |
| Small sizes | `assets/icons/gem-icon.svg`: face removed, rotated 38°, bolder wire so it stays legible at 16–32 px (tray, exe icon) |

Design notes for Milestone 2 animation: eyes, pupils and mouth are separate shapes so blinking, looking around and
talking need no new art; the wire is a single path so squash/stretch and head-tilt are transforms. Planned states:
Idle, Thinking, Happy, Confused, Sleeping, Working, Celebrating, Talking.
