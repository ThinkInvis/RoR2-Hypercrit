# Hypercrit

## Description
Inspired by the now-deprecated CritPlus mod. This mod is slightly smaller in scope and will not directly modify any item stats.

If a damage source has over 100% crit chance, this mod adds a chance for it to double-crit for 4x damage (by default). Past 200% crit chance, double-crit is guaranteed and there's a chance to triple-crit for 8x damage; etc.

Also provides config options to customize this behavior: base multiplier, stack multiplier, maximum stacks, exponential (default)/linear/asymptotic stacking.

## Issues/TODO

- Progressively deeper hitsound on stacked crits is intended, but PlayScaledSound doesn't seem to work :(
- See the GitHub repo for more!

## Changelog

**1.0.0**

- Initial version. Implements a stacking crit multiplier for crit chance exceeding multiples of 100%.