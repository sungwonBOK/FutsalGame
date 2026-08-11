# African Football Player Replacement Design

## Goal

Replace the visible character mesh used by the local scene player and by the network-spawned player with the supplied African football player, while preserving the existing gameplay, animation-controller, and networking behaviour.

## Selected approach

Import the supplied FBX as a Humanoid model and use its avatar with the existing `FutsalCharacter.controller`. The existing action clips remain the animation source and Unity retargets them to the new Humanoid rig. This is the least invasive option because it retains all current Animator parameter names and gameplay references.

The model and supplied image assets are imported under `Assets/_Game/Characters/AfricanFootballPlayer/`. A material uses the supplied base-color maps, including `SHIRT TEXTURE - 15/7.jpg` for the shirt. Optional normal, roughness, height, and ambient-occlusion maps are excluded from this first pass so the render pipeline remains unchanged and the asset footprint stays bounded.

## Scope

- Replace only the visual `Model` child in `Assets/_Game/Scenes/SampleScene.unity` and `Assets/_Game/Prefabs/NetPlayer.prefab`.
- Preserve the root transforms, collider, Rigidbody, gameplay scripts, NetworkObject, NetworkRigidbody, Animator Controller, and existing gameplay animation clips.
- Calibrate only the visual child transform when needed to align the new model feet with the existing root/collider.
- Verify import/console state and inspect both scene and prefab. A manual Play Mode check remains necessary for foot contact, ball interaction, and animation presentation.

## Excluded

- No gameplay script, input, collider, physics, networking, animator-controller, or animation-clip changes.
- No direct YAML edits to Unity scenes, prefabs, or assets.
- No new effects, UI, or animation states.

## Risks and recovery

If the source model cannot produce a valid Humanoid avatar or retargeting visibly fails, stop before changing the player assets and report the import diagnostic. If it imports but is not aligned, adjust only the nested visual transform; do not alter the gameplay root or capsule. The original visual hierarchy is retained until the replacement has been inspected.
