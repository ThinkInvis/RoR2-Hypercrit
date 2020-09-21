# Hypercrit

## Description
Inspired by the now-deprecated CritPlus mod. This mod is slightly smaller in scope and will not directly modify any item stats.

If a damage source has over 100% crit chance, this mod adds a chance for it to double-crit for 3x damage (by default). Past 200% crit chance, double-crit is guaranteed and there's a chance to triple-crit for 4x damage; etc.

Also provides config options to customize this behavior: base multiplier, stack multiplier, maximum stacks, exponential/linear (default)/asymptotic stacking.

Has config options to modify Huntress' Flurry: base shots fired, extra shots fired per crit stack, whether to adjust total damage to only increase with the first crit stack.

Has config options to modify displayed damage numbers (lower hue for each crit stack, yellow --> orange --> red --> purple...): whether to modify at all, how many crit stacks are required for 1 full loop from yellow to yellow.

## Issues/TODO

- Progressively deeper hitsound on stacked crits is intended, but PlayScaledSound doesn't seem to work :(
- See the GitHub repo for more!

## Changelog

**2.0.2**

- Fixed several issues with calculation of nerfed Flurry damage.

**2.0.1**

- Fixed an issue causing crits on attacks which normally don't get them.
- Fixed inconsistent crit status on most attacks.

**2.0.0**

- Now has an option to affect the number of shots fired by Huntress' Flurry.
- Now has an option to affect damage number colors with progressively decreasing hue.
- Major changes to internal structure.
- Bumped R2API dependency to 2.5.14.

**1.0.2**

- Made additional tweaks to default config. Default crit damage progression is now x2, x3, x4, x5....
- Bumped R2API dependency to 2.5.7.

**1.0.1**

- Updated and repaired for RoR2 1.0.
- Default stacking mode is now Linear.

**1.0.0**

- Initial version. Implements a stacking crit multiplier for crit chance exceeding multiples of 100%.