# Research: Organized 2

Sorts research projects into tech-level tabs and lays each tab out as a tidy, readable tree.

## Features

- A research window of its own, in UI Rethemed's look: drag to pan and use the mouse wheel to zoom, with a Fit button, and each tab remembers its view.
- Cards show their era's color and their state at a glance (finished, available, being researched, locked), with up to three unlock icons, techprint and study counters, a progress strip and a hover card listing everything the project needs and unlocks.
- Lines between projects are smooth curves, and selecting a project lights up its whole prerequisite chain.
- A foldable details panel shows the selected project's requirements, unlocks and the Research button; a bar along the bottom shows the current project's progress.
- Search finds projects by name or by anything they unlock, and chips in the window show or hide locked, finished and future-era projects.
- Era bands tint each era's columns and name it along the top, on tabs laid out era by era.
- The active project bar shows roughly how long the current project has left, at the colony's research pace over the last day.
- Cards come in Compact, Normal and Large sizes.
- Projects are grouped into Primitive, Neolithic, Medieval, Industrial, High and Late Industrial, Spacer, Ultra, Archotech, Anomaly and Miscellaneous tabs.
- Each tab gets a clean, non-overlapping layout with fewer crossing lines. Follow-ups sit to the right of what they depend on, and branches fill the free space beside their prerequisites.
- **Combine All Tabs** puts every project except Anomaly, Gravship and Save Our Ship 2 on the Main tab, with each tech level in its own block from left to right. A gap between the blocks and the lines between eras can each be turned on or off; by default it looks like the vanilla tree, with no gap and every line drawn.
- **Combine Industrial** merges High and Late Industrial back into one Industrial tab.
- A project is raised to the tech level of its prerequisites, so a follow-up never sits in an earlier era than what it needs. Its research cost changes to match.
- Era colors are picked on UI Rethemed - Core's settings page, and can be turned off.
- Tab tooltips show completed projects and research points, and the counts can be shown on the tabs too.
- Empty tabs are hidden.
- Projects caught in a circular dependency are outlined in red and named in the log.
- **Full Screen Research Window** makes the window as tall as the screen; a button in its title bar toggles it.
- Schematics can teach projects from every era tab.
- Works with Node Research: foundation technologies get a gold corner mark, and emergence nodes stay beside their branch without cluttering lines. Its vanilla menu button opens this window, and a button in this window's title bar goes back.
- Semi Random Research - Continued and Research Queue open this window from their tree buttons, and a button in its title bar goes back.
- Loads after the ProgressionLESS suite, so its research lands in the right tabs.
- With VFE Tribals, its Basics tab takes the Primitive projects.
- With Save Our Ship 2, its Ship and Archotech tabs keep their own projects.