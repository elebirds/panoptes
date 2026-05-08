# Research notes

## Core takeaways

* Fog of war exists to force scouting, prediction, and incomplete-information play, so the AI should not treat unseen space as a single deterministic destination.
* Influence maps are a standard RTS tactic abstraction for turning nearby units, danger, and control into a spatial field.
* Spatial reasoning chapters in RTS AI literature explicitly call out chokepoints and multipronged attacks, which fits a front-line model better than a single file of units.
* RTS kiting / attack-flee work shows that influence fields are already used to coordinate movement decisions around threats, not just to chase targets.
* Hidden-information prediction papers reinforce the idea that a bot should reason over remembered enemy structures/units, not only currently visible enemies.

## Design direction implied by the sources

1. Build a spatial pressure score from friendly presence, enemy presence, remembered units, and enemy structures.
2. Let exploration prefer boundary tiles between known and unknown space.
3. Let multi-unit groups distribute across several high-value tiles instead of collapsing onto the same single tile.
4. Let direct attack still override all of the above when a real target is in range.

## Source links

* https://www.sharcnet.ca/my/publications/show/2188
* https://www.researchgate.net/publication/289646419_Kiting_in_RTS_games_using_influence_maps
* https://www.gameaipro.com/GameAIPro2/GameAIPro2_Chapter31_Spatial_Reasoning_for_Strategic_Decision_Making.pdf
* https://www.designthegame.com/learning/tutorial/the-art-science-fog-war-systems-video-games
* https://arxiv.org/abs/2003.01927
