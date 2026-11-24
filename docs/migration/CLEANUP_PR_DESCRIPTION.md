# Retire legacy shooter assets and compatibility code

The build now starts in MotionMatchingTest. Retire the original shooter scene, rifle prefab, broken unused Ch15 variant and stale recovery scene, then remove their nine compatibility scripts and the unused Runtime Rigging reference. Repair the two missing script stubs on the retained Ren prefab while preserving its GUID.

Validation: 7 EditMode, 6 PlayMode and 31 Python tests passed. Unity reports no compilation failure; all 16 locomotion comparisons passed over 1,141 frames. Retained prefab/scene audit and fresh Console check: 0 errors, 0 warnings. The older Editor assertion did not reproduce; no root-cause fix is claimed.

Local tests include user scene/assets excluded from this commit. Character FBX/skybox prerequisites are still not published; no clean-checkout Unity run or target player build is certified. See docs/migration/FINAL_CLEANUP_REPORT.md for deletion inventory, recovery copies, exact evidence and limits.
