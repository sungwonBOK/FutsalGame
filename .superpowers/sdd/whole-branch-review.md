Base: 23f670718e219181e41e6e985fdd71b172d7efdf
Head: c45e06f

c45e06f feat: wire unified gameplay input
ae3fad5 refactor: route global controls through input actions
2e25217 fix: wire net player input reader
b5e53e4 refactor: wire player input reader in sample scene
53c02d7 refactor: route player controls through input actions
432f4d9 feat: define gameplay input actions
acfde4e test: strengthen gameplay input reader coverage
b5e4ceb feat: add gameplay input reader
6f6a5b1 docs: plan unified input migration
4628f8e docs: specify unified rebindable input

 .superpowers/sdd/task-1-report.md                  |  51 ++
 Assets/_Game/Prefabs/NetPlayer.prefab              |  15 +
 Assets/_Game/Scenes/SampleScene.unity              | 944 +++++++++++----------
 .../Scripts/Runtime/Camera/CameraViewSwitcher.cs   |   8 +-
 .../Scripts/Runtime/Input/GameplayInputAction.cs   |  28 +
 .../Runtime/Input/GameplayInputAction.cs.meta      |   2 +
 .../Scripts/Runtime/Input/GameplayInputReader.cs   |  68 ++
 .../Runtime/Input/GameplayInputReader.cs.meta      |   2 +
 .../Scripts/Runtime/Input/PlayerActionBindings.cs  |  40 -
 .../Runtime/Input/PlayerActionBindings.cs.meta     |   2 -
 .../Runtime/Input/PlayerActionInputReader.cs       |  62 --
 .../Runtime/Input/PlayerActionInputReader.cs.meta  |   2 -
 Assets/_Game/Scripts/Runtime/Input/PlayerInput.cs  |  53 +-
 Assets/_Game/Scripts/Runtime/Match/GameManager.cs  |  18 +-
 Assets/_Game/Scripts/Runtime/UI/ViewHintUI.cs      |   8 +-
 .../Tests/EditMode/GameplayInputReaderTests.cs     | 184 ++++
 .../EditMode/GameplayInputReaderTests.cs.meta      |   2 +
 .../Scripts/Tests/EditMode/MatchResetTests.cs      |  37 +
 .../Tests/EditMode/PlayerActionInputReaderTests.cs |  43 +-
 .../Settings/DefaultPlayerActionBindings.asset     |  23 -
 .../DefaultPlayerActionBindings.asset.meta         |   8 -
 .../Settings/InputSystem_Actions.inputactions      | 203 +++++
 IMPLEMENTATION_STATUS.md                           |   7 +
 .../2026-07-24-unified-runtime-rebindable-input.md | 274 ++++++
 ...7-24-unified-runtime-rebindable-input-design.md |  77 ++
 25 files changed, 1492 insertions(+), 669 deletions(-)

diff --git a/.superpowers/sdd/task-1-report.md b/.superpowers/sdd/task-1-report.md
new file mode 100644
index 0000000..2c0d6d8
--- /dev/null
+++ b/.superpowers/sdd/task-1-report.md
@@ -0,0 +1,51 @@
+# Task 1 report: gameplay input reader
+
+## Delivered files
+
+- `Assets/_Game/Scripts/Runtime/Input/GameplayInputAction.cs`
+  - Adds the eleven semantic `GameplayInputAction` values and the independent `GameplayInputButtonState` value type.
+- `Assets/_Game/Scripts/Runtime/Input/GameplayInputReader.cs`
+  - Serialized `InputActionAsset` reader that owns only the `Player` action map.
+  - Resolves semantic actions through one private mapping, exposes button/move/display reads, and returns neutral values for missing map/actions.
+- `Assets/_Game/Scripts/Tests/EditMode/GameplayInputReaderTests.cs`
+  - Covers effective binding override display, missing Player map, missing action, and Player map enable/disable lifecycle.
+
+## TDD evidence
+
+1. RED: Created the display-override contract before runtime production code. Unity compilation failed with `CS0246` for missing `GameplayInputReader`; the focused job `1f62c8a36780493f865955c7cf8fdf3a` found zero tests because that compile failure prevented discovery.
+2. GREEN: Implemented the reader boundary and final focused job `d9b72795c636413ab0d0eb69b35f7345` passed all 4 EditMode tests in 0.095 seconds.
+
+## Unity checks
+
+- Targeted and pinned editor: `develop_merge_test@498cbd09b717313e`, Unity `6000.5.3f1`.
+- Editor was idle, not in Play Mode, and compilation/domain reload completed before each focused run.
+- Final console check had no compile errors. An existing MCP package transport warning (`WebSocket is not initialised`) was observed; it is outside project code and did not affect test execution.
+
+## Self-review
+
+- No changes to `PlayerInput`, scenes, input assets, ProjectSettings, or existing consumers.
+- `OnEnable`/`OnDisable` manipulate only the resolved `Player` map; missing assets/maps/actions are safe neutral reads.
+- Effective display strings are obtained from the action itself, so Unity Input System binding overrides are reflected.
+
+## Concerns / follow-up
+
+- `FutsalGame.EditModeTests.asmdef` does not reference `Unity.InputSystem`, while Task 1 scope excludes asmdef changes. The focused test therefore constructs and invokes the real Input System asset APIs through reflection; runtime production code directly uses the package as intended.
+- This task intentionally does not wire a reader into the scene or migrate consumers; that remains later approved tasks.
+
+## Review-fix report
+
+### Corrected lifecycle contract
+
+- Replaced the prior `Reader_EnablesAndDisablesOnlyItsPlayerMap` assertion, which observed only the Player map, with `Reader_EnablesOnlyPlayerMap_AndLeavesOtherMapDisabled`.
+- The amended asset includes a distinct `Other` map. The test proves Player is enabled by the reader while Other stays disabled, then proves both maps are disabled after `OnDisable`.
+
+### Fixture reduction
+
+- Reduced the focused fixture from four tests and a generic overload-resolution helper to three required contracts with direct reflected Input System signatures.
+- Kept only: effective override display, neutral values for absent map/action, and isolated Player-map ownership. The concise test remains necessary because the EditMode asmdef does not reference `Unity.InputSystem`; broader input feel remains a manual Play Mode concern for the later wiring task.
+
+### Amended verification
+
+- The original RED evidence above remains the Task 1 implementation RED (`CS0246` before the reader existed).
+- This review amendment is a coverage correction, not a behavior change: the existing production implementation already enables only `playerMap`, so the newly precise contract passed on its first valid run without a production edit. No artificial failing assertion or temporary production regression was introduced.
+- Unity EditMode job `e16be6b7d9b74f4f9a06b1caa9c48ee3`: **3 passed, 0 failed** in 0.091 seconds.
diff --git a/Assets/_Game/Prefabs/NetPlayer.prefab b/Assets/_Game/Prefabs/NetPlayer.prefab
index 1bea6e4..4cb2b6b 100644
--- a/Assets/_Game/Prefabs/NetPlayer.prefab
+++ b/Assets/_Game/Prefabs/NetPlayer.prefab
@@ -779,20 +779,21 @@ GameObject:
   - component: {fileID: 6251665616604013365}
   - component: {fileID: 66404090215725709}
   - component: {fileID: 6156794776304056490}
   - component: {fileID: 4895522341653831104}
   - component: {fileID: 814188318427965981}
   - component: {fileID: 8936948511407146191}
   - component: {fileID: 6475598841151586546}
   - component: {fileID: 8454980551100531994}
   - component: {fileID: 9066333100707334323}
   - component: {fileID: 4199211009935589782}
+  - component: {fileID: 6233456049302109109}
   m_Layer: 0
   m_Name: NetPlayer
   m_TagString: Untagged
   m_Icon: {fileID: 0}
   m_NavMeshLayer: 0
   m_StaticEditorFlags: 0
   m_IsActive: 1
 --- !u!4 &6783210601534209352
 Transform:
   m_ObjectHideFlags: 0
@@ -979,20 +980,21 @@ MonoBehaviour:
   m_CorrespondingSourceObject: {fileID: 0}
   m_PrefabInstance: {fileID: 0}
   m_PrefabAsset: {fileID: 0}
   m_GameObject: {fileID: 3329367792369302264}
   m_Enabled: 1
   m_EditorHideFlags: 0
   m_Script: {fileID: 11500000, guid: 2d5a59d0eb4a035489856be869465b84, type: 3}
   m_Name: 
   m_EditorClassIdentifier: Assembly-CSharp::PlayerInput
   movementReference: {fileID: 0}
+  inputReader: {fileID: 6233456049302109109}
 --- !u!114 &8936948511407146191
 MonoBehaviour:
   m_ObjectHideFlags: 0
   m_CorrespondingSourceObject: {fileID: 0}
   m_PrefabInstance: {fileID: 0}
   m_PrefabAsset: {fileID: 0}
   m_GameObject: {fileID: 3329367792369302264}
   m_Enabled: 1
   m_EditorHideFlags: 0
   m_Script: {fileID: 11500000, guid: 6b5157a513975544f8b450ba589fd9d7, type: 3}
@@ -1095,20 +1097,33 @@ MonoBehaviour:
   m_CorrespondingSourceObject: {fileID: 0}
   m_PrefabInstance: {fileID: 0}
   m_PrefabAsset: {fileID: 0}
   m_GameObject: {fileID: 3329367792369302264}
   m_Enabled: 1
   m_EditorHideFlags: 0
   m_Script: {fileID: 11500000, guid: 10d21b299ee07a246b29073d7504a7b8, type: 3}
   m_Name: 
   m_EditorClassIdentifier: FutsalGame.Runtime::CharacterLocomotion
   config: {fileID: 11400000, guid: 34187b9fa43dc4a47b8057c92243fcc2, type: 2}
+--- !u!114 &6233456049302109109
+MonoBehaviour:
+  m_ObjectHideFlags: 0
+  m_CorrespondingSourceObject: {fileID: 0}
+  m_PrefabInstance: {fileID: 0}
+  m_PrefabAsset: {fileID: 0}
+  m_GameObject: {fileID: 3329367792369302264}
+  m_Enabled: 1
+  m_EditorHideFlags: 0
+  m_Script: {fileID: 11500000, guid: f8f46aaa5a60f0b48afc5a8287f590e2, type: 3}
+  m_Name: 
+  m_EditorClassIdentifier: FutsalGame.Runtime::GameplayInputReader
+  inputActions: {fileID: -944628639613478452, guid: 052faaac586de48259a63d0c4782560b, type: 3}
 --- !u!1 &3392525434865351877
 GameObject:
   m_ObjectHideFlags: 0
   m_CorrespondingSourceObject: {fileID: 0}
   m_PrefabInstance: {fileID: 0}
   m_PrefabAsset: {fileID: 0}
   serializedVersion: 6
   m_Component:
   - component: {fileID: 8156124440370758194}
   - component: {fileID: 2805715854115135992}
diff --git a/Assets/_Game/Scenes/SampleScene.unity b/Assets/_Game/Scenes/SampleScene.unity
index f857fb0..6959278 100644
--- a/Assets/_Game/Scenes/SampleScene.unity
+++ b/Assets/_Game/Scenes/SampleScene.unity
@@ -448,21 +448,199 @@ MeshRenderer:
   m_SortingOrder: 0
   m_MaskInteraction: 0
   m_AdditionalVertexStreams: {fileID: 0}
 --- !u!33 &109868317
 MeshFilter:
   m_ObjectHideFlags: 0
   m_CorrespondingSourceObject: {fileID: 0}
   m_PrefabInstance: {fileID: 0}
   m_PrefabAsset: {fileID: 0}
   m_GameObject: {fileID: 109868313}
-  m_Mesh: {fileID: 1093337673}
+  m_Mesh: {fileID: 209554495}
+--- !u!43 &209554495
+Mesh:
+  m_ObjectHideFlags: 0
+  m_CorrespondingSourceObject: {fileID: 0}
+  m_PrefabInstance: {fileID: 0}
+  m_PrefabAsset: {fileID: 0}
+  m_Name: GoalNetMesh
+  serializedVersion: 12
+  m_SubMeshes:
+  - serializedVersion: 2
+    firstByte: 0
+    indexCount: 576
+    topology: 0
+    baseVertex: 0
+    firstVertex: 0
+    vertexCount: 117
+    localAABB:
+      m_Center: {x: -16.25, y: 1.05, z: 0}
+      m_Extent: {x: 1.25, y: 0.95, z: 3}
+  m_Shapes:
+    vertices: []
+    shapes: []
+    channels: []
+    fullWeights: []
+  m_BindPose: []
+  m_BoneNameHashes: 
+  m_RootBoneNameHash: 0
+  m_BonesAABB: []
+  m_VariableBoneCountWeights:
+    m_Data: 
+  m_MeshCompression: 0
+  m_IsReadable: 1
+  m_KeepVertices: 1
+  m_KeepIndices: 1
+  m_IndexFormat: 0
+  m_IndexBuffer: 00000d00010001000d000e0001000e00020002000e000f0002000f00030003000f001000030010000400040010001100040011000500050011001200050012000600060012001300060013000700070013001400070014000800080014001500080015000900090015001600090016000a000a00160017000a0017000b000b00170018000b0018000c000c00180019000d001a000e000e001a001b000e001b000f000f001b001c000f001c00100010001c001d0010001d00110011001d001e0011001e00120012001e001f0012001f00130013001f0020001300200014001400200021001400210015001500210022001500220016001600220023001600230017001700230024001700240018001800240025001800250019001900250026001a0027001b001b00270028001b0028001c001c00280029001c0029001d001d0029002a001d002a001e001e002a002b001e002b001f001f002b002c001f002c00200020002c002d0020002d00210021002d002e0021002e00220022002e002f0022002f00230023002f003000230030002400240030003100240031002500250031003200250032002600260032003300270034002800280034003500280035002900290035003600290036002a002a00360037002a0037002b002b00370038002b0038002c002c00380039002c0039002d002d0039003a002d003a002e002e003a003b002e003b002f002f003b003c002f003c00300030003c003d0030003d00310031003d003e0031003e00320032003e003f0032003f00330033003f004000340041003500350041004200350042003600360042004300360043003700370043004400370044003800380044004500380045003900390045004600390046003a003a00460047003a0047003b003b00470048003b0048003c003c00480049003c0049003d003d0049004a003d004a003e003e004a004b003e004b003f003f004b004c003f004c00400040004c004d0041004e00420042004e004f0042004f00430043004f005000430050004400440050005100440051004500450051005200450052004600460052005300460053004700470053005400470054004800480054005500480055004900490055005600490056004a004a00560057004a0057004b004b00570058004b0058004c004c00580059004c0059004d004d0059005a004e005b004f004f005b005c004f005c00500050005c005d0050005d00510051005d005e0051005e00520052005e005f0052005f00530053005f006000530060005400540060006100540061005500550061006200550062005600560062006300560063005700570063006400570064005800580064006500580065005900590065006600590066005a005a00660067005b0068005c005c00680069005c0069005d005d0069006a005d006a005e005e006a006b005e006b005f005f006b006c005f006c00600060006c006d0060006d00610061006d006e0061006e00620062006e006f0062006f00630063006f007000630070006400640070007100640071006500650071007200650072006600660072007300660073006700670073007400
+  m_VertexData:
+    serializedVersion: 3
+    m_VertexCount: 117
+    m_Channels:
+    - stream: 0
+      offset: 0
+      format: 0
+      dimension: 3
+    - stream: 0
+      offset: 12
+      format: 0
+      dimension: 3
+    - stream: 0
+      offset: 0
+      format: 0
+      dimension: 0
+    - stream: 0
+      offset: 0
+      format: 0
+      dimension: 0
+    - stream: 0
+      offset: 24
+      format: 0
+      dimension: 2
+    - stream: 0
+      offset: 0
+      format: 0
+      dimension: 0
+    - stream: 0
+      offset: 0
+      format: 0
+      dimension: 0
+    - stream: 0
+      offset: 0
+      format: 0
+      dimension: 0
+    - stream: 0
+      offset: 0
+      format: 0
+      dimension: 0
+    - stream: 0
+      offset: 0
+      format: 0
+      dimension: 0
+    - stream: 0
+      offset: 0
+      format: 0
+      dimension: 0
+    - stream: 0
+      offset: 0
+      format: 0
+      dimension: 0
+    - stream: 0
+      offset: 0
+      format: 0
+      dimension: 0
+    - stream: 0
+      offset: 0
+      format: 0
+      dimension: 0
+    m_DataSize: 3744
+    _typelessdata: 000070c100000040000040c0bce61abf4ad14b3f000000000000000000000000000070c100000040000020c0bbe61abf4ad14b3f00000000abaaaa3d00000000000070c100000040000000c0bbe61abf4ad14b3f00000000abaa2a3e00000000000070c1000000400000c0bfbbe61abf4ad14b3f000000000000803e00000000000070c100000040ffff7fbfbbe61abf4ad14b3f00000000abaaaa3e00000000000070c100000040010000bfbbe61abf4bd14b3f000000005555d53e00000000000070c10000004000000000bbe61abf4ad14b3f000000000000003f00000000000070c100000040fcffff3ebbe61abf4ad14b3f000000005555153f00000000000070c1000000400100803fbbe61abf4ad14b3f00000000abaa2a3f00000000000070c1000000400000c03fbbe61abf4bd14b3f000000000000403f00000000000070c100000040ffffff3fbbe61abf4ad14b3f000000005555553f00000000000070c10000004000002040bbe61abf49d14b3f00000000abaa6a3f00000000000070c10000004000004040bce61abf4ad14b3f000000000000803f00000000000075c19a99e13f000040c0bde61abf4ad14b3f00000000000000000000003e000075c19a99e13f000020c0bce61abf48d14b3f00000000abaaaa3d0000003e000075c19a99e13f000000c0bce61abf48d14b3f00000000abaa2a3e0000003e000075c19a99e13f0000c0bfbce61abf48d14b3f000000000000803e0000003e000075c19a99e13fffff7fbfbde61abf4ad14b3f00000000abaaaa3e0000003e000075c19a99e13f010000bfbde61abf4ad14b3f000000005555d53e0000003e000075c19a99e13f00000000bde61abf4ad14b3f000000000000003f0000003e000075c19a99e13ffcffff3ebce61abf49d14b3f000000005555153f0000003e000075c19a99e13f0100803fbde61abf49d14b3f00000000abaa2a3f0000003e000075c19a99e13f0000c03fbce61abf49d14b3f000000000000403f0000003e000075c19a99e13fffffff3fbde61abf48d14b3f000000005555553f0000003e000075c19a99e13f00002040bde61abf49d14b3f00000000abaa6a3f0000003e000075c19a99e13f00004040bde61abf48d14b3f000000000000803f0000003e00007ac13333c33f000040c0bde61abf48d14b3f00000000000000000000803e00007ac13333c33f000020c0bce61abf48d14b3f00000000abaaaa3d0000803e00007ac13333c33f000000c0bce61abf48d14b3f00000000abaa2a3e0000803e00007ac13333c33f0000c0bfbde61abf48d14b3f000000000000803e0000803e00007ac13333c33fffff7fbfbde61abf4ad14b3f00000000abaaaa3e0000803e00007ac13333c33f010000bfbde61abf4ad14b3f000000005555d53e0000803e00007ac13333c33f00000000bde61abf4ad14b3f000000000000003f0000803e00007ac13333c33ffcffff3ebde61abf49d14b3f000000005555153f0000803e00007ac13333c33f0100803fbde61abf49d14b3f00000000abaa2a3f0000803e00007ac13333c33f0000c03fbce61abf49d14b3f000000000000403f0000803e00007ac13333c33fffffff3fbde61abf4ad14b3f000000005555553f0000803e00007ac13333c33f00002040bde61abf49d14b3f00000000abaa6a3f0000803e00007ac13333c33f00004040bde61abf4ad14b3f000000000000803f0000803e00007fc1cdcca43f000040c0bde61abf4ad14b3f00000000000000000000c03e00007fc1cdcca43f000020c0bce61abf48d14b3f00000000abaaaa3d0000c03e00007fc1cdcca43f000000c0bce61abf48d14b3f00000000abaa2a3e0000c03e00007fc1cdcca43f0000c0bfbce61abf48d14b3f000000000000803e0000c03e00007fc1cdcca43fffff7fbfbde61abf4ad14b3f00000000abaaaa3e0000c03e00007fc1cdcca43f010000bfbde61abf4ad14b3f000000005555d53e0000c03e00007fc1cdcca43f00000000bde61abf4ad14b3f000000000000003f0000c03e00007fc1cdcca43ffcffff3ebce61abf49d14b3f000000005555153f0000c03e00007fc1cdcca43f0100803fbde61abf49d14b3f00000000abaa2a3f0000c03e00007fc1cdcca43f0000c03fbce61abf49d14b3f000000000000403f0000c03e00007fc1cdcca43fffffff3fbde61abf48d14b3f000000005555553f0000c03e00007fc1cdcca43f00002040bde61abf49d14b3f00000000abaa6a3f0000c03e00007fc1cdcca43f00004040bde61abf48d14b3f000000000000803f0000c03e000082c16666863f000040c0bde61abf48d14b3f00000000000000000000003f000082c16666863f000020c0bce61abf48d14b3f00000000abaaaa3d0000003f000082c16666863f000000c0bce61abf48d14b3f00000000abaa2a3e0000003f000082c16666863f0000c0bfbde61abf48d14b3f000000000000803e0000003f000082c16666863fffff7fbfbde61abf4ad14b3f00000000abaaaa3e0000003f000082c16666863f010000bfbde61abf4ad14b3f000000005555d53e0000003f000082c16666863f00000000bde61abf4ad14b3f000000000000003f0000003f000082c16666863ffcffff3ebde61abf49d14b3f000000005555153f0000003f000082c16666863f0100803fbde61abf49d14b3f00000000abaa2a3f0000003f000082c16666863f0000c03fbce61abf49d14b3f000000000000403f0000003f000082c16666863fffffff3fbde61abf4ad14b3f000000005555553f0000003f000082c16666863f00002040bde61abf49d14b3f00000000abaa6a3f0000003f000082c16666863f00004040bde61abf4ad14b3f000000000000803f0000003f008084c10000503f000040c0bce61abf4ad14b3f00000000000000000000203f008084c10000503f000020c0bde61abf4ad14b3f00000000abaaaa3d0000203f008084c10000503f000000c0bde61abf4ad14b3f00000000abaa2a3e0000203f008084c10000503f0000c0bfbce61abf48d14b3f000000000000803e0000203f008084c10000503fffff7fbfbce61abf4ad14b3f00000000abaaaa3e0000203f008084c10000503f010000bfbce61abf4ad14b3f000000005555d53e0000203f008084c10000503f00000000bce61abf4ad14b3f000000000000003f0000203f008084c10000503ffcffff3ebce61abf49d14b3f000000005555153f0000203f008084c10000503f0100803fbce61abf49d14b3f00000000abaa2a3f0000203f008084c10000503f0000c03fbbe61abf4bd14b3f000000000000403f0000203f008084c10000503fffffff3fbce61abf4ad14b3f000000005555553f0000203f008084c10000503f00002040bbe61abf49d14b3f00000000abaa6a3f0000203f008084c10000503f00004040bde61abf4ad14b3f000000000000803f0000203f000087c13333133f000040c0bde61abf4ad14b3f00000000000000000000403f000087c13333133f000020c0bde61abf4ad14b3f00000000abaaaa3d0000403f000087c13333133f000000c0bde61abf4ad14b3f00000000abaa2a3e0000403f000087c13333133f0000c0bfbde61abf4ad14b3f000000000000803e0000403f000087c13333133fffff7fbfbde61abf4ad14b3f00000000abaaaa3e0000403f000087c13333133f010000bfbde61abf4ad14b3f000000005555d53e0000403f000087c13333133f00000000bde61abf4ad14b3f000000000000003f0000403f000087c13333133ffcffff3ebce61abf49d14b3f000000005555153f0000403f000087c13333133f0100803fbde61abf49d14b3f00000000abaa2a3f0000403f000087c13333133f0000c03fbce61abf49d14b3f000000000000403f0000403f000087c13333133fffffff3fbce61abf48d14b3f000000005555553f0000403f000087c13333133f00002040bde61abf49d14b3f00000000abaa6a3f0000403f000087c13333133f00004040bde61abf4ad14b3f000000000000803f0000403f008089c1cdccac3e000040c0bde61abf4ad14b3f00000000000000000000603f008089c1cdccac3e000020c0bde61abf4ad14b3f00000000abaaaa3d0000603f008089c1cdccac3e000000c0bde61abf4ad14b3f00000000abaa2a3e0000603f008089c1cdccac3e0000c0bfbce61abf48d14b3f000000000000803e0000603f008089c1cdccac3effff7fbfbde61abf4ad14b3f00000000abaaaa3e0000603f008089c1cdccac3e010000bfbde61abf4ad14b3f000000005555d53e0000603f008089c1cdccac3e00000000bde61abf4ad14b3f000000000000003f0000603f008089c1cdccac3efcffff3ebce61abf49d14b3f000000005555153f0000603f008089c1cdccac3e0100803fbde61abf49d14b3f00000000abaa2a3f0000603f008089c1cdccac3e0000c03fbce61abf4bd14b3f000000000000403f0000603f008089c1cdccac3effffff3fbde61abf4ad14b3f000000005555553f0000603f008089c1cdccac3e00002040bde61abf49d14b3f00000000abaa6a3f0000603f008089c1cdccac3e00004040bde61abf4ad14b3f000000000000803f0000603f00008cc1cdcccc3d000040c0bde61abf49d14b3f00000000000000000000803f00008cc1cdcccc3d000020c0bce61abf48d14b3f00000000abaaaa3d0000803f00008cc1cdcccc3d000000c0bce61abf48d14b3f00000000abaa2a3e0000803f00008cc1cdcccc3d0000c0bfbde61abf48d14b3f000000000000803e0000803f00008cc1cdcccc3dffff7fbfbde61abf49d14b3f00000000abaaaa3e0000803f00008cc1cdcccc3d010000bfbce61abf48d14b3f000000005555d53e0000803f00008cc1cdcccc3d00000000bde61abf49d14b3f000000000000003f0000803f00008cc1cdcccc3dfcffff3ebde61abf49d14b3f000000005555153f0000803f00008cc1cdcccc3d0100803fbce61abf49d14b3f00000000abaa2a3f0000803f00008cc1cdcccc3d0000c03fbde61abf4ad14b3f000000000000403f0000803f00008cc1cdcccc3dffffff3fbde61abf49d14b3f000000005555553f0000803f00008cc1cdcccc3d00002040bde61abf49d14b3f00000000abaa6a3f0000803f00008cc1cdcccc3d00004040bde61abf49d14b3f000000000000803f0000803f
+  m_CompressedMesh:
+    m_Vertices:
+      m_NumItems: 0
+      m_Range: 0
+      m_Start: 0
+      m_Data: 
+      m_BitSize: 0
+    m_UV:
+      m_NumItems: 0
+      m_Range: 0
+      m_Start: 0
+      m_Data: 
+      m_BitSize: 0
+    m_Normals:
+      m_NumItems: 0
+      m_Range: 0
+      m_Start: 0
+      m_Data: 
+      m_BitSize: 0
+    m_Tangents:
+      m_NumItems: 0
+      m_Range: 0
+      m_Start: 0
+      m_Data: 
+      m_BitSize: 0
+    m_Weights:
+      m_NumItems: 0
+      m_Data: 
+      m_BitSize: 0
+    m_NormalSigns:
+      m_NumItems: 0
+      m_Data: 
+      m_BitSize: 0
+    m_TangentSigns:
+      m_NumItems: 0
+      m_Data: 
+      m_BitSize: 0
+    m_FloatColors:
+      m_NumItems: 0
+      m_Range: 0
+      m_Start: 0
+      m_Data: 
+      m_BitSize: 0
+    m_BoneIndices:
+      m_NumItems: 0
+      m_Data: 
+      m_BitSize: 0
+    m_Triangles:
+      m_NumItems: 0
+      m_Data: 
+      m_BitSize: 0
+    m_UVInfo: 0
+  m_LocalAABB:
+    m_Center: {x: -16.25, y: 1.05, z: 0}
+    m_Extent: {x: 1.25, y: 0.95, z: 3}
+  m_MeshUsageFlags: 0
+  m_CookingOptions: 30
+  m_BakedConvexCollisionMesh: 
+  m_BakedTriangleCollisionMesh: 
+  'm_MeshMetrics[0]': 1
+  'm_MeshMetrics[1]': 1
+  m_MeshOptimizationFlags: 1
+  m_StreamData:
+    serializedVersion: 2
+    offset: 0
+    size: 0
+    path: 
+  m_MeshLodInfo:
+    serializedVersion: 2
+    m_LodSelectionCurve:
+      serializedVersion: 1
+      m_LodSlope: 0
+      m_LodBias: 0
+    m_NumLevels: 1
+    m_SubMeshes:
+    - serializedVersion: 2
+      m_Levels:
+      - serializedVersion: 1
+        m_IndexStart: 0
+        m_IndexCount: 0
 --- !u!1 &217735666
 GameObject:
   m_ObjectHideFlags: 0
   m_CorrespondingSourceObject: {fileID: 0}
   m_PrefabInstance: {fileID: 0}
   m_PrefabAsset: {fileID: 0}
   serializedVersion: 6
   m_Component:
   - component: {fileID: 217735667}
   - component: {fileID: 217735670}
@@ -797,233 +975,55 @@ MeshRenderer:
   m_AdditionalVertexStreams: {fileID: 0}
 --- !u!65 &252562000
 BoxCollider:
   m_ObjectHideFlags: 0
   m_CorrespondingSourceObject: {fileID: 0}
   m_PrefabInstance: {fileID: 0}
   m_PrefabAsset: {fileID: 0}
   m_GameObject: {fileID: 252561998}
   m_Material: {fileID: 0}
   m_IncludeLayers:
-    serializedVersion: 2
-    m_Bits: 0
-  m_ExcludeLayers:
-    serializedVersion: 2
-    m_Bits: 0
-  m_LayerOverridePriority: 0
-  m_IsTrigger: 0
-  m_ProvidesContacts: 0
-  m_Enabled: 1
-  serializedVersion: 3
-  m_Size: {x: 1, y: 1, z: 1}
-  m_Center: {x: 0, y: 0, z: 0}
---- !u!33 &252562001
-MeshFilter:
-  m_ObjectHideFlags: 0
-  m_CorrespondingSourceObject: {fileID: 0}
-  m_PrefabInstance: {fileID: 0}
-  m_PrefabAsset: {fileID: 0}
-  m_GameObject: {fileID: 252561998}
-  m_Mesh: {fileID: 10202, guid: 0000000000000000e000000000000000, type: 0}
---- !u!4 &252562002
-Transform:
-  m_ObjectHideFlags: 0
-  m_CorrespondingSourceObject: {fileID: 0}
-  m_PrefabInstance: {fileID: 0}
-  m_PrefabAsset: {fileID: 0}
-  m_GameObject: {fileID: 252561998}
-  serializedVersion: 2
-  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}
-  m_LocalPosition: {x: 18, y: 1, z: 0}
-  m_LocalScale: {x: 0.5, y: 2, z: 18}
-  m_ConstrainProportionsScale: 0
-  m_Children: []
-  m_Father: {fileID: 0}
-  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}
---- !u!43 &273397696
-Mesh:
-  m_ObjectHideFlags: 0
-  m_CorrespondingSourceObject: {fileID: 0}
-  m_PrefabInstance: {fileID: 0}
-  m_PrefabAsset: {fileID: 0}
-  m_Name: GoalNetMesh
-  serializedVersion: 12
-  m_SubMeshes:
-  - serializedVersion: 2
-    firstByte: 0
-    indexCount: 576
-    topology: 0
-    baseVertex: 0
-    firstVertex: 0
-    vertexCount: 117
-    localAABB:
-      m_Center: {x: 16.25, y: 1.05, z: 0}
-      m_Extent: {x: 1.25, y: 0.95, z: 3}
-  m_Shapes:
-    vertices: []
-    shapes: []
-    channels: []
-    fullWeights: []
-  m_BindPose: []
-  m_BoneNameHashes: 
-  m_RootBoneNameHash: 0
-  m_BonesAABB: []
-  m_VariableBoneCountWeights:
-    m_Data: 
-  m_MeshCompression: 0
-  m_IsReadable: 1
-  m_KeepVertices: 1
-  m_KeepIndices: 1
-  m_IndexFormat: 0
-  m_IndexBuffer: 00000d00010001000d000e0001000e00020002000e000f0002000f00030003000f001000030010000400040010001100040011000500050011001200050012000600060012001300060013000700070013001400070014000800080014001500080015000900090015001600090016000a000a00160017000a0017000b000b00170018000b0018000c000c00180019000d001a000e000e001a001b000e001b000f000f001b001c000f001c00100010001c001d0010001d00110011001d001e0011001e00120012001e001f0012001f00130013001f0020001300200014001400200021001400210015001500210022001500220016001600220023001600230017001700230024001700240018001800240025001800250019001900250026001a0027001b001b00270028001b0028001c001c00280029001c0029001d001d0029002a001d002a001e001e002a002b001e002b001f001f002b002c001f002c00200020002c002d0020002d00210021002d002e0021002e00220022002e002f0022002f00230023002f003000230030002400240030003100240031002500250031003200250032002600260032003300270034002800280034003500280035002900290035003600290036002a002a00360037002a0037002b002b00370038002b0038002c002c00380039002c0039002d002d0039003a002d003a002e002e003a003b002e003b002f002f003b003c002f003c00300030003c003d0030003d00310031003d003e0031003e00320032003e003f0032003f00330033003f004000340041003500350041004200350042003600360042004300360043003700370043004400370044003800380044004500380045003900390045004600390046003a003a00460047003a0047003b003b00470048003b0048003c003c00480049003c0049003d003d0049004a003d004a003e003e004a004b003e004b003f003f004b004c003f004c00400040004c004d0041004e00420042004e004f0042004f00430043004f005000430050004400440050005100440051004500450051005200450052004600460052005300460053004700470053005400470054004800480054005500480055004900490055005600490056004a004a00560057004a0057004b004b00570058004b0058004c004c00580059004c0059004d004d0059005a004e005b004f004f005b005c004f005c00500050005c005d0050005d00510051005d005e0051005e00520052005e005f0052005f00530053005f006000530060005400540060006100540061005500550061006200550062005600560062006300560063005700570063006400570064005800580064006500580065005900590065006600590066005a005a00660067005b0068005c005c00680069005c0069005d005d0069006a005d006a005e005e006a006b005e006b005f005f006b006c005f006c00600060006c006d0060006d00610061006d006e0061006e00620062006e006f0062006f00630063006f007000630070006400640070007100640071006500650071007200650072006600660072007300660073006700670073007400
-  m_VertexData:
-    serializedVersion: 3
-    m_VertexCount: 117
-    m_Channels:
-    - stream: 0
-      offset: 0
-      format: 0
-      dimension: 3
-    - stream: 0
-      offset: 12
-      format: 0
-      dimension: 3
-    - stream: 0
-      offset: 0
-      format: 0
-      dimension: 0
-    - stream: 0
-      offset: 0
-      format: 0
-      dimension: 0
-    - stream: 0
-      offset: 24
-      format: 0
-      dimension: 2
-    - stream: 0
-      offset: 0
-      format: 0
-      dimension: 0
-    - stream: 0
-      offset: 0
-      format: 0
-      dimension: 0
-    - stream: 0
-      offset: 0
-      format: 0
-      dimension: 0
-    - stream: 0
-      offset: 0
-      format: 0
-      dimension: 0
-    - stream: 0
-      offset: 0
-      format: 0
-      dimension: 0
-    - stream: 0
-      offset: 0
-      format: 0
-      dimension: 0
-    - stream: 0
-      offset: 0
-      format: 0
-      dimension: 0
-    - stream: 0
-      offset: 0
-      format: 0
-      dimension: 0
-    - stream: 0
-      offset: 0
-      format: 0
-      dimension: 0
-    m_DataSize: 3744
-    _typelessdata: 0000704100000040000040c0bce61abf4ad14bbf0000000000000000000000000000704100000040000020c0bbe61abf4ad14bbf00000000abaaaa3d000000000000704100000040000000c0bbe61abf4ad14bbf00000000abaa2a3e0000000000007041000000400000c0bfbbe61abf4ad14bbf000000000000803e000000000000704100000040ffff7fbfbbe61abf4ad14bbf00000000abaaaa3e000000000000704100000040010000bfbbe61abf4bd14bbf000000005555d53e00000000000070410000004000000000bbe61abf4ad14bbf000000000000003f000000000000704100000040fcffff3ebbe61abf4ad14bbf000000005555153f0000000000007041000000400100803fbbe61abf4ad14bbf00000000abaa2a3f0000000000007041000000400000c03fbbe61abf4bd14bbf000000000000403f000000000000704100000040ffffff3fbbe61abf4ad14bbf000000005555553f00000000000070410000004000002040bbe61abf49d14bbf00000000abaa6a3f00000000000070410000004000004040bce61abf4ad14bbf000000000000803f00000000000075419a99e13f000040c0bde61abf4ad14bbf00000000000000000000003e000075419a99e13f000020c0bce61abf48d14bbf00000000abaaaa3d0000003e000075419a99e13f000000c0bce61abf48d14bbf00000000abaa2a3e0000003e000075419a99e13f0000c0bfbce61abf48d14bbf000000000000803e0000003e000075419a99e13fffff7fbfbde61abf4ad14bbf00000000abaaaa3e0000003e000075419a99e13f010000bfbde61abf4ad14bbf000000005555d53e0000003e000075419a99e13f00000000bde61abf4ad14bbf000000000000003f0000003e000075419a99e13ffcffff3ebce61abf49d14bbf000000005555153f0000003e000075419a99e13f0100803fbde61abf49d14bbf00000000abaa2a3f0000003e000075419a99e13f0000c03fbce61abf49d14bbf000000000000403f0000003e000075419a99e13fffffff3fbde61abf48d14bbf000000005555553f0000003e000075419a99e13f00002040bde61abf49d14bbf00000000abaa6a3f0000003e000075419a99e13f00004040bde61abf48d14bbf000000000000803f0000003e00007a413333c33f000040c0bde61abf48d14bbf00000000000000000000803e00007a413333c33f000020c0bce61abf48d14bbf00000000abaaaa3d0000803e00007a413333c33f000000c0bce61abf48d14bbf00000000abaa2a3e0000803e00007a413333c33f0000c0bfbde61abf48d14bbf000000000000803e0000803e00007a413333c33fffff7fbfbde61abf4ad14bbf00000000abaaaa3e0000803e00007a413333c33f010000bfbde61abf4ad14bbf000000005555d53e0000803e00007a413333c33f00000000bde61abf4ad14bbf000000000000003f0000803e00007a413333c33ffcffff3ebde61abf49d14bbf000000005555153f0000803e00007a413333c33f0100803fbde61abf49d14bbf00000000abaa2a3f0000803e00007a413333c33f0000c03fbce61abf49d14bbf000000000000403f0000803e00007a413333c33fffffff3fbde61abf4ad14bbf000000005555553f0000803e00007a413333c33f00002040bde61abf49d14bbf00000000abaa6a3f0000803e00007a413333c33f00004040bde61abf4ad14bbf000000000000803f0000803e00007f41cdcca43f000040c0bde61abf4ad14bbf00000000000000000000c03e00007f41cdcca43f000020c0bce61abf48d14bbf00000000abaaaa3d0000c03e00007f41cdcca43f000000c0bce61abf48d14bbf00000000abaa2a3e0000c03e00007f41cdcca43f0000c0bfbce61abf48d14bbf000000000000803e0000c03e00007f41cdcca43fffff7fbfbde61abf4ad14bbf00000000abaaaa3e0000c03e00007f41cdcca43f010000bfbde61abf4ad14bbf000000005555d53e0000c03e00007f41cdcca43f00000000bde61abf4ad14bbf000000000000003f0000c03e00007f41cdcca43ffcffff3ebce61abf49d14bbf000000005555153f0000c03e00007f41cdcca43f0100803fbde61abf49d14bbf00000000abaa2a3f0000c03e00007f41cdcca43f0000c03fbce61abf49d14bbf000000000000403f0000c03e00007f41cdcca43fffffff3fbde61abf48d14bbf000000005555553f0000c03e00007f41cdcca43f00002040bde61abf49d14bbf00000000abaa6a3f0000c03e00007f41cdcca43f00004040bde61abf48d14bbf000000000000803f0000c03e000082416666863f000040c0bde61abf48d14bbf00000000000000000000003f000082416666863f000020c0bce61abf48d14bbf00000000abaaaa3d0000003f000082416666863f000000c0bce61abf48d14bbf00000000abaa2a3e0000003f000082416666863f0000c0bfbde61abf48d14bbf000000000000803e0000003f000082416666863fffff7fbfbde61abf4ad14bbf00000000abaaaa3e0000003f000082416666863f010000bfbde61abf4ad14bbf000000005555d53e0000003f000082416666863f00000000bde61abf4ad14bbf000000000000003f0000003f000082416666863ffcffff3ebde61abf49d14bbf000000005555153f0000003f000082416666863f0100803fbde61abf49d14bbf00000000abaa2a3f0000003f000082416666863f0000c03fbce61abf49d14bbf000000000000403f0000003f000082416666863fffffff3fbde61abf4ad14bbf000000005555553f0000003f000082416666863f00002040bde61abf49d14bbf00000000abaa6a3f0000003f000082416666863f00004040bde61abf4ad14bbf000000000000803f0000003f008084410000503f000040c0bce61abf4ad14bbf00000000000000000000203f008084410000503f000020c0bde61abf4ad14bbf00000000abaaaa3d0000203f008084410000503f000000c0bde61abf4ad14bbf00000000abaa2a3e0000203f008084410000503f0000c0bfbce61abf48d14bbf000000000000803e0000203f008084410000503fffff7fbfbce61abf4ad14bbf00000000abaaaa3e0000203f008084410000503f010000bfbce61abf4ad14bbf000000005555d53e0000203f008084410000503f00000000bce61abf4ad14bbf000000000000003f0000203f008084410000503ffcffff3ebce61abf49d14bbf000000005555153f0000203f008084410000503f0100803fbce61abf49d14bbf00000000abaa2a3f0000203f008084410000503f0000c03fbbe61abf4bd14bbf000000000000403f0000203f008084410000503fffffff3fbce61abf4ad14bbf000000005555553f0000203f008084410000503f00002040bbe61abf49d14bbf00000000abaa6a3f0000203f008084410000503f00004040bde61abf4ad14bbf000000000000803f0000203f000087413333133f000040c0bde61abf4ad14bbf00000000000000000000403f000087413333133f000020c0bde61abf4ad14bbf00000000abaaaa3d0000403f000087413333133f000000c0bde61abf4ad14bbf00000000abaa2a3e0000403f000087413333133f0000c0bfbde61abf4ad14bbf000000000000803e0000403f000087413333133fffff7fbfbde61abf4ad14bbf00000000abaaaa3e0000403f000087413333133f010000bfbde61abf4ad14bbf000000005555d53e0000403f000087413333133f00000000bde61abf4ad14bbf000000000000003f0000403f000087413333133ffcffff3ebce61abf49d14bbf000000005555153f0000403f000087413333133f0100803fbde61abf49d14bbf00000000abaa2a3f0000403f000087413333133f0000c03fbce61abf49d14bbf000000000000403f0000403f000087413333133fffffff3fbce61abf48d14bbf000000005555553f0000403f000087413333133f00002040bde61abf49d14bbf00000000abaa6a3f0000403f000087413333133f00004040bde61abf4ad14bbf000000000000803f0000403f00808941cdccac3e000040c0bde61abf4ad14bbf00000000000000000000603f00808941cdccac3e000020c0bde61abf4ad14bbf00000000abaaaa3d0000603f00808941cdccac3e000000c0bde61abf4ad14bbf00000000abaa2a3e0000603f00808941cdccac3e0000c0bfbce61abf48d14bbf000000000000803e0000603f00808941cdccac3effff7fbfbde61abf4ad14bbf00000000abaaaa3e0000603f00808941cdccac3e010000bfbde61abf4ad14bbf000000005555d53e0000603f00808941cdccac3e00000000bde61abf4ad14bbf000000000000003f0000603f00808941cdccac3efcffff3ebce61abf49d14bbf000000005555153f0000603f00808941cdccac3e0100803fbde61abf49d14bbf00000000abaa2a3f0000603f00808941cdccac3e0000c03fbce61abf4bd14bbf000000000000403f0000603f00808941cdccac3effffff3fbde61abf4ad14bbf000000005555553f0000603f00808941cdccac3e00002040bde61abf49d14bbf00000000abaa6a3f0000603f00808941cdccac3e00004040bde61abf4ad14bbf000000000000803f0000603f00008c41cdcccc3d000040c0bde61abf49d14bbf00000000000000000000803f00008c41cdcccc3d000020c0bce61abf48d14bbf00000000abaaaa3d0000803f00008c41cdcccc3d000000c0bce61abf48d14bbf00000000abaa2a3e0000803f00008c41cdcccc3d0000c0bfbde61abf48d14bbf000000000000803e0000803f00008c41cdcccc3dffff7fbfbde61abf49d14bbf00000000abaaaa3e0000803f00008c41cdcccc3d010000bfbce61abf48d14bbf000000005555d53e0000803f00008c41cdcccc3d00000000bde61abf49d14bbf000000000000003f0000803f00008c41cdcccc3dfcffff3ebde61abf49d14bbf000000005555153f0000803f00008c41cdcccc3d0100803fbce61abf49d14bbf00000000abaa2a3f0000803f00008c41cdcccc3d0000c03fbde61abf4ad14bbf000000000000403f0000803f00008c41cdcccc3dffffff3fbde61abf49d14bbf000000005555553f0000803f00008c41cdcccc3d00002040bde61abf49d14bbf00000000abaa6a3f0000803f00008c41cdcccc3d00004040bde61abf49d14bbf000000000000803f0000803f
-  m_CompressedMesh:
-    m_Vertices:
-      m_NumItems: 0
-      m_Range: 0
-      m_Start: 0
-      m_Data: 
-      m_BitSize: 0
-    m_UV:
-      m_NumItems: 0
-      m_Range: 0
-      m_Start: 0
-      m_Data: 
-      m_BitSize: 0
-    m_Normals:
-      m_NumItems: 0
-      m_Range: 0
-      m_Start: 0
-      m_Data: 
-      m_BitSize: 0
-    m_Tangents:
-      m_NumItems: 0
-      m_Range: 0
-      m_Start: 0
-      m_Data: 
-      m_BitSize: 0
-    m_Weights:
-      m_NumItems: 0
-      m_Data: 
-      m_BitSize: 0
-    m_NormalSigns:
-      m_NumItems: 0
-      m_Data: 
-      m_BitSize: 0
-    m_TangentSigns:
-      m_NumItems: 0
-      m_Data: 
-      m_BitSize: 0
-    m_FloatColors:
-      m_NumItems: 0
-      m_Range: 0
-      m_Start: 0
-      m_Data: 
-      m_BitSize: 0
-    m_BoneIndices:
-      m_NumItems: 0
-      m_Data: 
-      m_BitSize: 0
-    m_Triangles:
-      m_NumItems: 0
-      m_Data: 
-      m_BitSize: 0
-    m_UVInfo: 0
-  m_LocalAABB:
-    m_Center: {x: 16.25, y: 1.05, z: 0}
-    m_Extent: {x: 1.25, y: 0.95, z: 3}
-  m_MeshUsageFlags: 0
-  m_CookingOptions: 30
-  m_BakedConvexCollisionMesh: 
-  m_BakedTriangleCollisionMesh: 
-  'm_MeshMetrics[0]': 1
-  'm_MeshMetrics[1]': 1
-  m_MeshOptimizationFlags: 1
-  m_StreamData:
-    serializedVersion: 2
-    offset: 0
-    size: 0
-    path: 
-  m_MeshLodInfo:
-    serializedVersion: 2
-    m_LodSelectionCurve:
-      serializedVersion: 1
-      m_LodSlope: 0
-      m_LodBias: 0
-    m_NumLevels: 1
-    m_SubMeshes:
-    - serializedVersion: 2
-      m_Levels:
-      - serializedVersion: 1
-        m_IndexStart: 0
-        m_IndexCount: 0
+    serializedVersion: 2
+    m_Bits: 0
+  m_ExcludeLayers:
+    serializedVersion: 2
+    m_Bits: 0
+  m_LayerOverridePriority: 0
+  m_IsTrigger: 0
+  m_ProvidesContacts: 0
+  m_Enabled: 1
+  serializedVersion: 3
+  m_Size: {x: 1, y: 1, z: 1}
+  m_Center: {x: 0, y: 0, z: 0}
+--- !u!33 &252562001
+MeshFilter:
+  m_ObjectHideFlags: 0
+  m_CorrespondingSourceObject: {fileID: 0}
+  m_PrefabInstance: {fileID: 0}
+  m_PrefabAsset: {fileID: 0}
+  m_GameObject: {fileID: 252561998}
+  m_Mesh: {fileID: 10202, guid: 0000000000000000e000000000000000, type: 0}
+--- !u!4 &252562002
+Transform:
+  m_ObjectHideFlags: 0
+  m_CorrespondingSourceObject: {fileID: 0}
+  m_PrefabInstance: {fileID: 0}
+  m_PrefabAsset: {fileID: 0}
+  m_GameObject: {fileID: 252561998}
+  serializedVersion: 2
+  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}
+  m_LocalPosition: {x: 18, y: 1, z: 0}
+  m_LocalScale: {x: 0.5, y: 2, z: 18}
+  m_ConstrainProportionsScale: 0
+  m_Children: []
+  m_Father: {fileID: 0}
+  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}
 --- !u!1 &330585543
 GameObject:
   m_ObjectHideFlags: 0
   m_CorrespondingSourceObject: {fileID: 0}
   m_PrefabInstance: {fileID: 0}
   m_PrefabAsset: {fileID: 0}
   serializedVersion: 6
   m_Component:
   - component: {fileID: 330585546}
   - component: {fileID: 330585545}
@@ -1165,20 +1165,21 @@ MonoBehaviour:
   m_CorrespondingSourceObject: {fileID: 0}
   m_PrefabInstance: {fileID: 0}
   m_PrefabAsset: {fileID: 0}
   m_GameObject: {fileID: 330585543}
   m_Enabled: 0
   m_EditorHideFlags: 0
   m_Script: {fileID: 11500000, guid: 2af7523c5b5464043a79bca8f7bc0c4c, type: 3}
   m_Name: 
   m_EditorClassIdentifier: Assembly-CSharp::CameraViewSwitcher
   deferToActionCamera: 1
+  inputReader: {fileID: 887825636}
   target: {fileID: 887825628}
   distance: 5
   height: 3
   lookAtHeight: 1.2
   positionLerp: 10
   rotationLerp: 10
   yawSmoothTime: 0.28
 --- !u!114 &330585549
 MonoBehaviour:
   m_ObjectHideFlags: 0
@@ -1807,21 +1808,21 @@ MeshRenderer:
   m_SortingOrder: 0
   m_MaskInteraction: 0
   m_AdditionalVertexStreams: {fileID: 0}
 --- !u!33 &558602405
 MeshFilter:
   m_ObjectHideFlags: 0
   m_CorrespondingSourceObject: {fileID: 0}
   m_PrefabInstance: {fileID: 0}
   m_PrefabAsset: {fileID: 0}
   m_GameObject: {fileID: 558602401}
-  m_Mesh: {fileID: 273397696}
+  m_Mesh: {fileID: 1757711592}
 --- !u!1 &570747446
 GameObject:
   m_ObjectHideFlags: 0
   m_CorrespondingSourceObject: {fileID: 0}
   m_PrefabInstance: {fileID: 0}
   m_PrefabAsset: {fileID: 0}
   serializedVersion: 6
   m_Component:
   - component: {fileID: 570747450}
   - component: {fileID: 570747449}
@@ -2232,20 +2233,28 @@ MonoBehaviour:
   m_GameObject: {fileID: 620006437}
   m_Enabled: 1
   m_EditorHideFlags: 0
   m_Script: {fileID: 11500000, guid: aac32a67aba0dcf419816f640e278480, type: 3}
   m_Name: 
   m_EditorClassIdentifier: Assembly-CSharp::SimpleAIController
   attackGoal: {fileID: 395099843}
   ownGoal: {fileID: 2140412250}
   ball: {fileID: 1911587981}
   shootRange: 10
+  dribbleCommitTime: 0.6
+  interceptLeadTime: 0.35
+  goalAimSpread: 1.1
+  sprintDistance: 5
+  dodgeThreatRange: 3.2
+  dodgeReactionChance: 0.65
+  engageDistance: 4.5
+  goalSideOffset: 2.2
   tackleRange: 1.8
   punchDistance: 1.1
   arriveDistance: 0.3
   shootAlignDot: 0.9
   decisionInterval: 0.15
 --- !u!114 &620006448
 MonoBehaviour:
   m_ObjectHideFlags: 0
   m_CorrespondingSourceObject: {fileID: 0}
   m_PrefabInstance: {fileID: 0}
@@ -2523,20 +2532,21 @@ GameObject:
   - component: {fileID: 887825626}
   - component: {fileID: 887825625}
   - component: {fileID: 887825624}
   - component: {fileID: 887825629}
   - component: {fileID: 887825633}
   - component: {fileID: 887825632}
   - component: {fileID: 887825631}
   - component: {fileID: 887825630}
   - component: {fileID: 887825634}
   - component: {fileID: 887825635}
+  - component: {fileID: 887825636}
   m_Layer: 0
   m_Name: Player
   m_TagString: Untagged
   m_Icon: {fileID: 0}
   m_NavMeshLayer: 0
   m_StaticEditorFlags: 0
   m_IsActive: 1
 --- !u!54 &887825624
 Rigidbody:
   m_ObjectHideFlags: 0
@@ -2613,21 +2623,21 @@ MeshRenderer:
   m_SortingOrder: 0
   m_MaskInteraction: 0
   m_AdditionalVertexStreams: {fileID: 0}
 --- !u!136 &887825626
 CapsuleCollider:
   m_ObjectHideFlags: 0
   m_CorrespondingSourceObject: {fileID: 0}
   m_PrefabInstance: {fileID: 0}
   m_PrefabAsset: {fileID: 0}
   m_GameObject: {fileID: 887825622}
-  m_Material: {fileID: 2134320507}
+  m_Material: {fileID: 1733799863}
   m_IncludeLayers:
     serializedVersion: 2
     m_Bits: 0
   m_ExcludeLayers:
     serializedVersion: 2
     m_Bits: 0
   m_LayerOverridePriority: 0
   m_IsTrigger: 0
   m_ProvidesContacts: 0
   m_Enabled: 1
@@ -2682,21 +2692,21 @@ MonoBehaviour:
   m_CorrespondingSourceObject: {fileID: 0}
   m_PrefabInstance: {fileID: 0}
   m_PrefabAsset: {fileID: 0}
   m_GameObject: {fileID: 887825622}
   m_Enabled: 1
   m_EditorHideFlags: 0
   m_Script: {fileID: 11500000, guid: 2d5a59d0eb4a035489856be869465b84, type: 3}
   m_Name: 
   m_EditorClassIdentifier: Assembly-CSharp::PlayerInput
   movementReference: {fileID: 0}
-  actionBindings: {fileID: 11400000, guid: a0d3b780fd6f5f5469d556bfba2eae03, type: 2}
+  inputReader: {fileID: 887825636}
 --- !u!114 &887825631
 MonoBehaviour:
   m_ObjectHideFlags: 0
   m_CorrespondingSourceObject: {fileID: 0}
   m_PrefabInstance: {fileID: 0}
   m_PrefabAsset: {fileID: 0}
   m_GameObject: {fileID: 887825622}
   m_Enabled: 1
   m_EditorHideFlags: 0
   m_Script: {fileID: 11500000, guid: 4b5d736e114d31641bc3264bcef08366, type: 3}
@@ -2751,20 +2761,33 @@ MonoBehaviour:
   m_CorrespondingSourceObject: {fileID: 0}
   m_PrefabInstance: {fileID: 0}
   m_PrefabAsset: {fileID: 0}
   m_GameObject: {fileID: 887825622}
   m_Enabled: 1
   m_EditorHideFlags: 0
   m_Script: {fileID: 11500000, guid: 10d21b299ee07a246b29073d7504a7b8, type: 3}
   m_Name: 
   m_EditorClassIdentifier: FutsalGame.Runtime::CharacterLocomotion
   config: {fileID: 11400000, guid: 34187b9fa43dc4a47b8057c92243fcc2, type: 2}
+--- !u!114 &887825636
+MonoBehaviour:
+  m_ObjectHideFlags: 0
+  m_CorrespondingSourceObject: {fileID: 0}
+  m_PrefabInstance: {fileID: 0}
+  m_PrefabAsset: {fileID: 0}
+  m_GameObject: {fileID: 887825622}
+  m_Enabled: 1
+  m_EditorHideFlags: 0
+  m_Script: {fileID: 11500000, guid: f8f46aaa5a60f0b48afc5a8287f590e2, type: 3}
+  m_Name: 
+  m_EditorClassIdentifier: FutsalGame.Runtime::GameplayInputReader
+  inputActions: {fileID: -944628639613478452, guid: 052faaac586de48259a63d0c4782560b, type: 3}
 --- !u!1 &941027066
 GameObject:
   m_ObjectHideFlags: 0
   m_CorrespondingSourceObject: {fileID: 0}
   m_PrefabInstance: {fileID: 0}
   m_PrefabAsset: {fileID: 0}
   serializedVersion: 6
   m_Component:
   - component: {fileID: 941027067}
   - component: {fileID: 941027069}
@@ -2920,271 +2943,93 @@ MeshRenderer:
 MeshCollider:
   m_ObjectHideFlags: 0
   m_CorrespondingSourceObject: {fileID: 0}
   m_PrefabInstance: {fileID: 0}
   m_PrefabAsset: {fileID: 0}
   m_GameObject: {fileID: 1058964584}
   m_Material: {fileID: 0}
   m_IncludeLayers:
     serializedVersion: 2
     m_Bits: 0
-  m_ExcludeLayers:
-    serializedVersion: 2
-    m_Bits: 0
-  m_LayerOverridePriority: 0
-  m_IsTrigger: 0
-  m_ProvidesContacts: 0
-  m_Enabled: 1
-  serializedVersion: 5
-  m_Convex: 0
-  m_CookingOptions: 30
-  m_Mesh: {fileID: 10209, guid: 0000000000000000e000000000000000, type: 0}
---- !u!33 &1058964587
-MeshFilter:
-  m_ObjectHideFlags: 0
-  m_CorrespondingSourceObject: {fileID: 0}
-  m_PrefabInstance: {fileID: 0}
-  m_PrefabAsset: {fileID: 0}
-  m_GameObject: {fileID: 1058964584}
-  m_Mesh: {fileID: 10209, guid: 0000000000000000e000000000000000, type: 0}
---- !u!4 &1058964588
-Transform:
-  m_ObjectHideFlags: 0
-  m_CorrespondingSourceObject: {fileID: 0}
-  m_PrefabInstance: {fileID: 0}
-  m_PrefabAsset: {fileID: 0}
-  m_GameObject: {fileID: 1058964584}
-  serializedVersion: 2
-  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}
-  m_LocalPosition: {x: 0, y: 0, z: 0}
-  m_LocalScale: {x: 3.6, y: 1, z: 1.8}
-  m_ConstrainProportionsScale: 0
-  m_Children: []
-  m_Father: {fileID: 0}
-  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}
---- !u!43 &1070454979
-Mesh:
-  m_ObjectHideFlags: 0
-  m_CorrespondingSourceObject: {fileID: 0}
-  m_PrefabInstance: {fileID: 0}
-  m_PrefabAsset: {fileID: 0}
-  m_Name: 
-  serializedVersion: 12
-  m_SubMeshes:
-  - serializedVersion: 2
-    firstByte: 0
-    indexCount: 3
-    topology: 0
-    baseVertex: 0
-    firstVertex: 0
-    vertexCount: 3
-    localAABB:
-      m_Center: {x: 16.25, y: 1, z: 3}
-      m_Extent: {x: 1.25, y: 1, z: 0}
-  m_Shapes:
-    vertices: []
-    shapes: []
-    channels: []
-    fullWeights: []
-  m_BindPose: []
-  m_BoneNameHashes: 
-  m_RootBoneNameHash: 0
-  m_BonesAABB: []
-  m_VariableBoneCountWeights:
-    m_Data: 
-  m_MeshCompression: 0
-  m_IsReadable: 1
-  m_KeepVertices: 1
-  m_KeepIndices: 1
-  m_IndexFormat: 0
-  m_IndexBuffer: 000001000200
-  m_VertexData:
-    serializedVersion: 3
-    m_VertexCount: 3
-    m_Channels:
-    - stream: 0
-      offset: 0
-      format: 0
-      dimension: 3
-    - stream: 0
-      offset: 12
-      format: 0
-      dimension: 3
-    - stream: 0
-      offset: 0
-      format: 0
-      dimension: 0
-    - stream: 0
-      offset: 0
-      format: 0
-      dimension: 0
-    - stream: 0
-      offset: 24
-      format: 0
-      dimension: 2
-    - stream: 0
-      offset: 0
-      format: 0
-      dimension: 0
-    - stream: 0
-      offset: 0
-      format: 0
-      dimension: 0
-    - stream: 0
-      offset: 0
-      format: 0
-      dimension: 0
-    - stream: 0
-      offset: 0
-      format: 0
-      dimension: 0
-    - stream: 0
-      offset: 0
-      format: 0
-      dimension: 0
-    - stream: 0
-      offset: 0
-      format: 0
-      dimension: 0
-    - stream: 0
-      offset: 0
-      format: 0
-      dimension: 0
-    - stream: 0
-      offset: 0
-      format: 0
-      dimension: 0
-    - stream: 0
-      offset: 0
-      format: 0
-      dimension: 0
-    m_DataSize: 96
-    _typelessdata: 00007041000000400000404000000000000000000000803f000000000000803f00007041000000000000404000000000000000000000803f000000000000000000008c41cdcccc3d0000404000000000000000000000803f0000803f00000000
-  m_CompressedMesh:
-    m_Vertices:
-      m_NumItems: 0
-      m_Range: 0
-      m_Start: 0
-      m_Data: 
-      m_BitSize: 0
-    m_UV:
-      m_NumItems: 0
-      m_Range: 0
-      m_Start: 0
-      m_Data: 
-      m_BitSize: 0
-    m_Normals:
-      m_NumItems: 0
-      m_Range: 0
-      m_Start: 0
-      m_Data: 
-      m_BitSize: 0
-    m_Tangents:
-      m_NumItems: 0
-      m_Range: 0
-      m_Start: 0
-      m_Data: 
-      m_BitSize: 0
-    m_Weights:
-      m_NumItems: 0
-      m_Data: 
-      m_BitSize: 0
-    m_NormalSigns:
-      m_NumItems: 0
-      m_Data: 
-      m_BitSize: 0
-    m_TangentSigns:
-      m_NumItems: 0
-      m_Data: 
-      m_BitSize: 0
-    m_FloatColors:
-      m_NumItems: 0
-      m_Range: 0
-      m_Start: 0
-      m_Data: 
-      m_BitSize: 0
-    m_BoneIndices:
-      m_NumItems: 0
-      m_Data: 
-      m_BitSize: 0
-    m_Triangles:
-      m_NumItems: 0
-      m_Data: 
-      m_BitSize: 0
-    m_UVInfo: 0
-  m_LocalAABB:
-    m_Center: {x: 16.25, y: 1, z: 3}
-    m_Extent: {x: 1.25, y: 1, z: 0}
-  m_MeshUsageFlags: 0
-  m_CookingOptions: 30
-  m_BakedConvexCollisionMesh: 
-  m_BakedTriangleCollisionMesh: 
-  'm_MeshMetrics[0]': 1
-  'm_MeshMetrics[1]': 1
-  m_MeshOptimizationFlags: 1
-  m_StreamData:
-    serializedVersion: 2
-    offset: 0
-    size: 0
-    path: 
-  m_MeshLodInfo:
-    serializedVersion: 2
-    m_LodSelectionCurve:
-      serializedVersion: 1
-      m_LodSlope: 0
-      m_LodBias: 0
-    m_NumLevels: 1
-    m_SubMeshes:
-    - serializedVersion: 2
-      m_Levels:
-      - serializedVersion: 1
-        m_IndexStart: 0
-        m_IndexCount: 0
---- !u!43 &1093337673
+  m_ExcludeLayers:
+    serializedVersion: 2
+    m_Bits: 0
+  m_LayerOverridePriority: 0
+  m_IsTrigger: 0
+  m_ProvidesContacts: 0
+  m_Enabled: 1
+  serializedVersion: 5
+  m_Convex: 0
+  m_CookingOptions: 30
+  m_Mesh: {fileID: 10209, guid: 0000000000000000e000000000000000, type: 0}
+--- !u!33 &1058964587
+MeshFilter:
+  m_ObjectHideFlags: 0
+  m_CorrespondingSourceObject: {fileID: 0}
+  m_PrefabInstance: {fileID: 0}
+  m_PrefabAsset: {fileID: 0}
+  m_GameObject: {fileID: 1058964584}
+  m_Mesh: {fileID: 10209, guid: 0000000000000000e000000000000000, type: 0}
+--- !u!4 &1058964588
+Transform:
+  m_ObjectHideFlags: 0
+  m_CorrespondingSourceObject: {fileID: 0}
+  m_PrefabInstance: {fileID: 0}
+  m_PrefabAsset: {fileID: 0}
+  m_GameObject: {fileID: 1058964584}
+  serializedVersion: 2
+  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}
+  m_LocalPosition: {x: 0, y: 0, z: 0}
+  m_LocalScale: {x: 3.6, y: 1, z: 1.8}
+  m_ConstrainProportionsScale: 0
+  m_Children: []
+  m_Father: {fileID: 0}
+  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}
+--- !u!43 &1070454979
 Mesh:
   m_ObjectHideFlags: 0
   m_CorrespondingSourceObject: {fileID: 0}
   m_PrefabInstance: {fileID: 0}
   m_PrefabAsset: {fileID: 0}
-  m_Name: GoalNetMesh
+  m_Name: 
   serializedVersion: 12
   m_SubMeshes:
   - serializedVersion: 2
     firstByte: 0
-    indexCount: 576
+    indexCount: 3
     topology: 0
     baseVertex: 0
     firstVertex: 0
-    vertexCount: 117
+    vertexCount: 3
     localAABB:
-      m_Center: {x: -16.25, y: 1.05, z: 0}
-      m_Extent: {x: 1.25, y: 0.95, z: 3}
+      m_Center: {x: 16.25, y: 1, z: 3}
+      m_Extent: {x: 1.25, y: 1, z: 0}
   m_Shapes:
     vertices: []
     shapes: []
     channels: []
     fullWeights: []
   m_BindPose: []
   m_BoneNameHashes: 
   m_RootBoneNameHash: 0
   m_BonesAABB: []
   m_VariableBoneCountWeights:
     m_Data: 
   m_MeshCompression: 0
   m_IsReadable: 1
   m_KeepVertices: 1
   m_KeepIndices: 1
   m_IndexFormat: 0
-  m_IndexBuffer: 00000d00010001000d000e0001000e00020002000e000f0002000f00030003000f001000030010000400040010001100040011000500050011001200050012000600060012001300060013000700070013001400070014000800080014001500080015000900090015001600090016000a000a00160017000a0017000b000b00170018000b0018000c000c00180019000d001a000e000e001a001b000e001b000f000f001b001c000f001c00100010001c001d0010001d00110011001d001e0011001e00120012001e001f0012001f00130013001f0020001300200014001400200021001400210015001500210022001500220016001600220023001600230017001700230024001700240018001800240025001800250019001900250026001a0027001b001b00270028001b0028001c001c00280029001c0029001d001d0029002a001d002a001e001e002a002b001e002b001f001f002b002c001f002c00200020002c002d0020002d00210021002d002e0021002e00220022002e002f0022002f00230023002f003000230030002400240030003100240031002500250031003200250032002600260032003300270034002800280034003500280035002900290035003600290036002a002a00360037002a0037002b002b00370038002b0038002c002c00380039002c0039002d002d0039003a002d003a002e002e003a003b002e003b002f002f003b003c002f003c00300030003c003d0030003d00310031003d003e0031003e00320032003e003f0032003f00330033003f004000340041003500350041004200350042003600360042004300360043003700370043004400370044003800380044004500380045003900390045004600390046003a003a00460047003a0047003b003b00470048003b0048003c003c00480049003c0049003d003d0049004a003d004a003e003e004a004b003e004b003f003f004b004c003f004c00400040004c004d0041004e00420042004e004f0042004f00430043004f005000430050004400440050005100440051004500450051005200450052004600460052005300460053004700470053005400470054004800480054005500480055004900490055005600490056004a004a00560057004a0057004b004b00570058004b0058004c004c00580059004c0059004d004d0059005a004e005b004f004f005b005c004f005c00500050005c005d0050005d00510051005d005e0051005e00520052005e005f0052005f00530053005f006000530060005400540060006100540061005500550061006200550062005600560062006300560063005700570063006400570064005800580064006500580065005900590065006600590066005a005a00660067005b0068005c005c00680069005c0069005d005d0069006a005d006a005e005e006a006b005e006b005f005f006b006c005f006c00600060006c006d0060006d00610061006d006e0061006e00620062006e006f0062006f00630063006f007000630070006400640070007100640071006500650071007200650072006600660072007300660073006700670073007400
+  m_IndexBuffer: 000001000200
   m_VertexData:
     serializedVersion: 3
-    m_VertexCount: 117
+    m_VertexCount: 3
     m_Channels:
     - stream: 0
       offset: 0
       format: 0
       dimension: 3
     - stream: 0
       offset: 12
       format: 0
       dimension: 3
     - stream: 0
@@ -3228,22 +3073,22 @@ Mesh:
       format: 0
       dimension: 0
     - stream: 0
       offset: 0
       format: 0
       dimension: 0
     - stream: 0
       offset: 0
       format: 0
       dimension: 0
-    m_DataSize: 3744
-    _typelessdata: 000070c100000040000040c0bce61abf4ad14b3f000000000000000000000000000070c100000040000020c0bbe61abf4ad14b3f00000000abaaaa3d00000000000070c100000040000000c0bbe61abf4ad14b3f00000000abaa2a3e00000000000070c1000000400000c0bfbbe61abf4ad14b3f000000000000803e00000000000070c100000040ffff7fbfbbe61abf4ad14b3f00000000abaaaa3e00000000000070c100000040010000bfbbe61abf4bd14b3f000000005555d53e00000000000070c10000004000000000bbe61abf4ad14b3f000000000000003f00000000000070c100000040fcffff3ebbe61abf4ad14b3f000000005555153f00000000000070c1000000400100803fbbe61abf4ad14b3f00000000abaa2a3f00000000000070c1000000400000c03fbbe61abf4bd14b3f000000000000403f00000000000070c100000040ffffff3fbbe61abf4ad14b3f000000005555553f00000000000070c10000004000002040bbe61abf49d14b3f00000000abaa6a3f00000000000070c10000004000004040bce61abf4ad14b3f000000000000803f00000000000075c19a99e13f000040c0bde61abf4ad14b3f00000000000000000000003e000075c19a99e13f000020c0bce61abf48d14b3f00000000abaaaa3d0000003e000075c19a99e13f000000c0bce61abf48d14b3f00000000abaa2a3e0000003e000075c19a99e13f0000c0bfbce61abf48d14b3f000000000000803e0000003e000075c19a99e13fffff7fbfbde61abf4ad14b3f00000000abaaaa3e0000003e000075c19a99e13f010000bfbde61abf4ad14b3f000000005555d53e0000003e000075c19a99e13f00000000bde61abf4ad14b3f000000000000003f0000003e000075c19a99e13ffcffff3ebce61abf49d14b3f000000005555153f0000003e000075c19a99e13f0100803fbde61abf49d14b3f00000000abaa2a3f0000003e000075c19a99e13f0000c03fbce61abf49d14b3f000000000000403f0000003e000075c19a99e13fffffff3fbde61abf48d14b3f000000005555553f0000003e000075c19a99e13f00002040bde61abf49d14b3f00000000abaa6a3f0000003e000075c19a99e13f00004040bde61abf48d14b3f000000000000803f0000003e00007ac13333c33f000040c0bde61abf48d14b3f00000000000000000000803e00007ac13333c33f000020c0bce61abf48d14b3f00000000abaaaa3d0000803e00007ac13333c33f000000c0bce61abf48d14b3f00000000abaa2a3e0000803e00007ac13333c33f0000c0bfbde61abf48d14b3f000000000000803e0000803e00007ac13333c33fffff7fbfbde61abf4ad14b3f00000000abaaaa3e0000803e00007ac13333c33f010000bfbde61abf4ad14b3f000000005555d53e0000803e00007ac13333c33f00000000bde61abf4ad14b3f000000000000003f0000803e00007ac13333c33ffcffff3ebde61abf49d14b3f000000005555153f0000803e00007ac13333c33f0100803fbde61abf49d14b3f00000000abaa2a3f0000803e00007ac13333c33f0000c03fbce61abf49d14b3f000000000000403f0000803e00007ac13333c33fffffff3fbde61abf4ad14b3f000000005555553f0000803e00007ac13333c33f00002040bde61abf49d14b3f00000000abaa6a3f0000803e00007ac13333c33f00004040bde61abf4ad14b3f000000000000803f0000803e00007fc1cdcca43f000040c0bde61abf4ad14b3f00000000000000000000c03e00007fc1cdcca43f000020c0bce61abf48d14b3f00000000abaaaa3d0000c03e00007fc1cdcca43f000000c0bce61abf48d14b3f00000000abaa2a3e0000c03e00007fc1cdcca43f0000c0bfbce61abf48d14b3f000000000000803e0000c03e00007fc1cdcca43fffff7fbfbde61abf4ad14b3f00000000abaaaa3e0000c03e00007fc1cdcca43f010000bfbde61abf4ad14b3f000000005555d53e0000c03e00007fc1cdcca43f00000000bde61abf4ad14b3f000000000000003f0000c03e00007fc1cdcca43ffcffff3ebce61abf49d14b3f000000005555153f0000c03e00007fc1cdcca43f0100803fbde61abf49d14b3f00000000abaa2a3f0000c03e00007fc1cdcca43f0000c03fbce61abf49d14b3f000000000000403f0000c03e00007fc1cdcca43fffffff3fbde61abf48d14b3f000000005555553f0000c03e00007fc1cdcca43f00002040bde61abf49d14b3f00000000abaa6a3f0000c03e00007fc1cdcca43f00004040bde61abf48d14b3f000000000000803f0000c03e000082c16666863f000040c0bde61abf48d14b3f00000000000000000000003f000082c16666863f000020c0bce61abf48d14b3f00000000abaaaa3d0000003f000082c16666863f000000c0bce61abf48d14b3f00000000abaa2a3e0000003f000082c16666863f0000c0bfbde61abf48d14b3f000000000000803e0000003f000082c16666863fffff7fbfbde61abf4ad14b3f00000000abaaaa3e0000003f000082c16666863f010000bfbde61abf4ad14b3f000000005555d53e0000003f000082c16666863f00000000bde61abf4ad14b3f000000000000003f0000003f000082c16666863ffcffff3ebde61abf49d14b3f000000005555153f0000003f000082c16666863f0100803fbde61abf49d14b3f00000000abaa2a3f0000003f000082c16666863f0000c03fbce61abf49d14b3f000000000000403f0000003f000082c16666863fffffff3fbde61abf4ad14b3f000000005555553f0000003f000082c16666863f00002040bde61abf49d14b3f00000000abaa6a3f0000003f000082c16666863f00004040bde61abf4ad14b3f000000000000803f0000003f008084c10000503f000040c0bce61abf4ad14b3f00000000000000000000203f008084c10000503f000020c0bde61abf4ad14b3f00000000abaaaa3d0000203f008084c10000503f000000c0bde61abf4ad14b3f00000000abaa2a3e0000203f008084c10000503f0000c0bfbce61abf48d14b3f000000000000803e0000203f008084c10000503fffff7fbfbce61abf4ad14b3f00000000abaaaa3e0000203f008084c10000503f010000bfbce61abf4ad14b3f000000005555d53e0000203f008084c10000503f00000000bce61abf4ad14b3f000000000000003f0000203f008084c10000503ffcffff3ebce61abf49d14b3f000000005555153f0000203f008084c10000503f0100803fbce61abf49d14b3f00000000abaa2a3f0000203f008084c10000503f0000c03fbbe61abf4bd14b3f000000000000403f0000203f008084c10000503fffffff3fbce61abf4ad14b3f000000005555553f0000203f008084c10000503f00002040bbe61abf49d14b3f00000000abaa6a3f0000203f008084c10000503f00004040bde61abf4ad14b3f000000000000803f0000203f000087c13333133f000040c0bde61abf4ad14b3f00000000000000000000403f000087c13333133f000020c0bde61abf4ad14b3f00000000abaaaa3d0000403f000087c13333133f000000c0bde61abf4ad14b3f00000000abaa2a3e0000403f000087c13333133f0000c0bfbde61abf4ad14b3f000000000000803e0000403f000087c13333133fffff7fbfbde61abf4ad14b3f00000000abaaaa3e0000403f000087c13333133f010000bfbde61abf4ad14b3f000000005555d53e0000403f000087c13333133f00000000bde61abf4ad14b3f000000000000003f0000403f000087c13333133ffcffff3ebce61abf49d14b3f000000005555153f0000403f000087c13333133f0100803fbde61abf49d14b3f00000000abaa2a3f0000403f000087c13333133f0000c03fbce61abf49d14b3f000000000000403f0000403f000087c13333133fffffff3fbce61abf48d14b3f000000005555553f0000403f000087c13333133f00002040bde61abf49d14b3f00000000abaa6a3f0000403f000087c13333133f00004040bde61abf4ad14b3f000000000000803f0000403f008089c1cdccac3e000040c0bde61abf4ad14b3f00000000000000000000603f008089c1cdccac3e000020c0bde61abf4ad14b3f00000000abaaaa3d0000603f008089c1cdccac3e000000c0bde61abf4ad14b3f00000000abaa2a3e0000603f008089c1cdccac3e0000c0bfbce61abf48d14b3f000000000000803e0000603f008089c1cdccac3effff7fbfbde61abf4ad14b3f00000000abaaaa3e0000603f008089c1cdccac3e010000bfbde61abf4ad14b3f000000005555d53e0000603f008089c1cdccac3e00000000bde61abf4ad14b3f000000000000003f0000603f008089c1cdccac3efcffff3ebce61abf49d14b3f000000005555153f0000603f008089c1cdccac3e0100803fbde61abf49d14b3f00000000abaa2a3f0000603f008089c1cdccac3e0000c03fbce61abf4bd14b3f000000000000403f0000603f008089c1cdccac3effffff3fbde61abf4ad14b3f000000005555553f0000603f008089c1cdccac3e00002040bde61abf49d14b3f00000000abaa6a3f0000603f008089c1cdccac3e00004040bde61abf4ad14b3f000000000000803f0000603f00008cc1cdcccc3d000040c0bde61abf49d14b3f00000000000000000000803f00008cc1cdcccc3d000020c0bce61abf48d14b3f00000000abaaaa3d0000803f00008cc1cdcccc3d000000c0bce61abf48d14b3f00000000abaa2a3e0000803f00008cc1cdcccc3d0000c0bfbde61abf48d14b3f000000000000803e0000803f00008cc1cdcccc3dffff7fbfbde61abf49d14b3f00000000abaaaa3e0000803f00008cc1cdcccc3d010000bfbce61abf48d14b3f000000005555d53e0000803f00008cc1cdcccc3d00000000bde61abf49d14b3f000000000000003f0000803f00008cc1cdcccc3dfcffff3ebde61abf49d14b3f000000005555153f0000803f00008cc1cdcccc3d0100803fbce61abf49d14b3f00000000abaa2a3f0000803f00008cc1cdcccc3d0000c03fbde61abf4ad14b3f000000000000403f0000803f00008cc1cdcccc3dffffff3fbde61abf49d14b3f000000005555553f0000803f00008cc1cdcccc3d00002040bde61abf49d14b3f00000000abaa6a3f0000803f00008cc1cdcccc3d00004040bde61abf49d14b3f000000000000803f0000803f
+    m_DataSize: 96
+    _typelessdata: 00007041000000400000404000000000000000000000803f000000000000803f00007041000000000000404000000000000000000000803f000000000000000000008c41cdcccc3d0000404000000000000000000000803f0000803f00000000
   m_CompressedMesh:
     m_Vertices:
       m_NumItems: 0
       m_Range: 0
       m_Start: 0
       m_Data: 
       m_BitSize: 0
     m_UV:
       m_NumItems: 0
       m_Range: 0
@@ -3283,22 +3128,22 @@ Mesh:
     m_BoneIndices:
       m_NumItems: 0
       m_Data: 
       m_BitSize: 0
     m_Triangles:
       m_NumItems: 0
       m_Data: 
       m_BitSize: 0
     m_UVInfo: 0
   m_LocalAABB:
-    m_Center: {x: -16.25, y: 1.05, z: 0}
-    m_Extent: {x: 1.25, y: 0.95, z: 3}
+    m_Center: {x: 16.25, y: 1, z: 3}
+    m_Extent: {x: 1.25, y: 1, z: 0}
   m_MeshUsageFlags: 0
   m_CookingOptions: 30
   m_BakedConvexCollisionMesh: 
   m_BakedTriangleCollisionMesh: 
   'm_MeshMetrics[0]': 1
   'm_MeshMetrics[1]': 1
   m_MeshOptimizationFlags: 1
   m_StreamData:
     serializedVersion: 2
     offset: 0
@@ -4523,45 +4368,54 @@ MonoBehaviour:
   m_PrefabInstance: {fileID: 0}
   m_PrefabAsset: {fileID: 0}
   m_GameObject: {fileID: 1562705793}
   m_Enabled: 1
   m_EditorHideFlags: 0
   m_Script: {fileID: 11500000, guid: 7f3c9a1e5b2d44e8a9c06d1f83b47e52, type: 3}
   m_Name: 
   m_EditorClassIdentifier: Assembly-CSharp::AbilityCooldownUI
   playerCombat: {fileID: 887825631}
   playerState: {fileID: 0}
+  playerLocomotion: {fileID: 0}
   screenMargin: {x: 48, y: 40}
   pipSize: 88
   pipSpacing: 22
   punchColor: {r: 0.98, g: 0.62, b: 0.16, a: 1}
   slideColor: {r: 0.3, g: 0.68, b: 0.98, a: 1}
+  dodgeColor: {r: 0.55, g: 0.92, b: 0.55, a: 1}
+  staminaBarHeight: 12
+  staminaBarGap: 10
+  staminaColor: {r: 0.45, g: 0.85, b: 0.95, a: 1}
+  staminaLowColor: {r: 0.95, g: 0.45, b: 0.25, a: 1}
+  sprintHighlight: 0.35
+  staminaLowThreshold: 0.3
   cooldownDim: 0.42
   sweepColor: {r: 0.02, g: 0.03, b: 0.06, a: 0.78}
   rejectColor: {r: 0.95, g: 0.22, b: 0.22, a: 1}
   readyFlashDuration: 0.25
   readyPopScale: 1.18
   rejectShakeDuration: 0.2
   rejectShakeAmount: 5
 --- !u!114 &1562705801
 MonoBehaviour:
   m_ObjectHideFlags: 0
   m_CorrespondingSourceObject: {fileID: 0}
   m_PrefabInstance: {fileID: 0}
   m_PrefabAsset: {fileID: 0}
   m_GameObject: {fileID: 1562705793}
   m_Enabled: 1
   m_EditorHideFlags: 0
   m_Script: {fileID: 11500000, guid: 4130c03acaffa6146a610c5b52102d3c, type: 3}
   m_Name: 
   m_EditorClassIdentifier: Assembly-CSharp::ViewHintUI
   switcher: {fileID: 330585548}
+  inputReader: {fileID: 887825636}
   screenMargin: {x: 48, y: 40}
   fontSize: 20
 --- !u!43 &1574403300
 Mesh:
   m_ObjectHideFlags: 0
   m_CorrespondingSourceObject: {fileID: 0}
   m_PrefabInstance: {fileID: 0}
   m_PrefabAsset: {fileID: 0}
   m_Name: 
   serializedVersion: 12
@@ -5173,20 +5027,211 @@ MonoBehaviour:
   m_UseSpriteMesh: 0
   m_PixelsPerUnitMultiplier: 1
 --- !u!222 &1722160611
 CanvasRenderer:
   m_ObjectHideFlags: 0
   m_CorrespondingSourceObject: {fileID: 0}
   m_PrefabInstance: {fileID: 0}
   m_PrefabAsset: {fileID: 0}
   m_GameObject: {fileID: 1722160608}
   m_CullTransparentMesh: 1
+--- !u!134 &1733799863
+PhysicsMaterial:
+  m_ObjectHideFlags: 0
+  m_CorrespondingSourceObject: {fileID: 0}
+  m_PrefabInstance: {fileID: 0}
+  m_PrefabAsset: {fileID: 0}
+  m_Name: ' (Instance) (Instance) (Instance) (Instance)'
+  serializedVersion: 2
+  m_DynamicFriction: 0.6
+  m_StaticFriction: 0.6
+  m_Bounciness: 0
+  m_FrictionCombine: 0
+  m_BounceCombine: 0
+--- !u!43 &1757711592
+Mesh:
+  m_ObjectHideFlags: 0
+  m_CorrespondingSourceObject: {fileID: 0}
+  m_PrefabInstance: {fileID: 0}
+  m_PrefabAsset: {fileID: 0}
+  m_Name: GoalNetMesh
+  serializedVersion: 12
+  m_SubMeshes:
+  - serializedVersion: 2
+    firstByte: 0
+    indexCount: 576
+    topology: 0
+    baseVertex: 0
+    firstVertex: 0
+    vertexCount: 117
+    localAABB:
+      m_Center: {x: 16.25, y: 1.05, z: 0}
+      m_Extent: {x: 1.25, y: 0.95, z: 3}
+  m_Shapes:
+    vertices: []
+    shapes: []
+    channels: []
+    fullWeights: []
+  m_BindPose: []
+  m_BoneNameHashes: 
+  m_RootBoneNameHash: 0
+  m_BonesAABB: []
+  m_VariableBoneCountWeights:
+    m_Data: 
+  m_MeshCompression: 0
+  m_IsReadable: 1
+  m_KeepVertices: 1
+  m_KeepIndices: 1
+  m_IndexFormat: 0
+  m_IndexBuffer: 00000d00010001000d000e0001000e00020002000e000f0002000f00030003000f001000030010000400040010001100040011000500050011001200050012000600060012001300060013000700070013001400070014000800080014001500080015000900090015001600090016000a000a00160017000a0017000b000b00170018000b0018000c000c00180019000d001a000e000e001a001b000e001b000f000f001b001c000f001c00100010001c001d0010001d00110011001d001e0011001e00120012001e001f0012001f00130013001f0020001300200014001400200021001400210015001500210022001500220016001600220023001600230017001700230024001700240018001800240025001800250019001900250026001a0027001b001b00270028001b0028001c001c00280029001c0029001d001d0029002a001d002a001e001e002a002b001e002b001f001f002b002c001f002c00200020002c002d0020002d00210021002d002e0021002e00220022002e002f0022002f00230023002f003000230030002400240030003100240031002500250031003200250032002600260032003300270034002800280034003500280035002900290035003600290036002a002a00360037002a0037002b002b00370038002b0038002c002c00380039002c0039002d002d0039003a002d003a002e002e003a003b002e003b002f002f003b003c002f003c00300030003c003d0030003d00310031003d003e0031003e00320032003e003f0032003f00330033003f004000340041003500350041004200350042003600360042004300360043003700370043004400370044003800380044004500380045003900390045004600390046003a003a00460047003a0047003b003b00470048003b0048003c003c00480049003c0049003d003d0049004a003d004a003e003e004a004b003e004b003f003f004b004c003f004c00400040004c004d0041004e00420042004e004f0042004f00430043004f005000430050004400440050005100440051004500450051005200450052004600460052005300460053004700470053005400470054004800480054005500480055004900490055005600490056004a004a00560057004a0057004b004b00570058004b0058004c004c00580059004c0059004d004d0059005a004e005b004f004f005b005c004f005c00500050005c005d0050005d00510051005d005e0051005e00520052005e005f0052005f00530053005f006000530060005400540060006100540061005500550061006200550062005600560062006300560063005700570063006400570064005800580064006500580065005900590065006600590066005a005a00660067005b0068005c005c00680069005c0069005d005d0069006a005d006a005e005e006a006b005e006b005f005f006b006c005f006c00600060006c006d0060006d00610061006d006e0061006e00620062006e006f0062006f00630063006f007000630070006400640070007100640071006500650071007200650072006600660072007300660073006700670073007400
+  m_VertexData:
+    serializedVersion: 3
+    m_VertexCount: 117
+    m_Channels:
+    - stream: 0
+      offset: 0
+      format: 0
+      dimension: 3
+    - stream: 0
+      offset: 12
+      format: 0
+      dimension: 3
+    - stream: 0
+      offset: 0
+      format: 0
+      dimension: 0
+    - stream: 0
+      offset: 0
+      format: 0
+      dimension: 0
+    - stream: 0
+      offset: 24
+      format: 0
+      dimension: 2
+    - stream: 0
+      offset: 0
+      format: 0
+      dimension: 0
+    - stream: 0
+      offset: 0
+      format: 0
+      dimension: 0
+    - stream: 0
+      offset: 0
+      format: 0
+      dimension: 0
+    - stream: 0
+      offset: 0
+      format: 0
+      dimension: 0
+    - stream: 0
+      offset: 0
+      format: 0
+      dimension: 0
+    - stream: 0
+      offset: 0
+      format: 0
+      dimension: 0
+    - stream: 0
+      offset: 0
+      format: 0
+      dimension: 0
+    - stream: 0
+      offset: 0
+      format: 0
+      dimension: 0
+    - stream: 0
+      offset: 0
+      format: 0
+      dimension: 0
+    m_DataSize: 3744
+    _typelessdata: 0000704100000040000040c0bce61abf4ad14bbf0000000000000000000000000000704100000040000020c0bbe61abf4ad14bbf00000000abaaaa3d000000000000704100000040000000c0bbe61abf4ad14bbf00000000abaa2a3e0000000000007041000000400000c0bfbbe61abf4ad14bbf000000000000803e000000000000704100000040ffff7fbfbbe61abf4ad14bbf00000000abaaaa3e000000000000704100000040010000bfbbe61abf4bd14bbf000000005555d53e00000000000070410000004000000000bbe61abf4ad14bbf000000000000003f000000000000704100000040fcffff3ebbe61abf4ad14bbf000000005555153f0000000000007041000000400100803fbbe61abf4ad14bbf00000000abaa2a3f0000000000007041000000400000c03fbbe61abf4bd14bbf000000000000403f000000000000704100000040ffffff3fbbe61abf4ad14bbf000000005555553f00000000000070410000004000002040bbe61abf49d14bbf00000000abaa6a3f00000000000070410000004000004040bce61abf4ad14bbf000000000000803f00000000000075419a99e13f000040c0bde61abf4ad14bbf00000000000000000000003e000075419a99e13f000020c0bce61abf48d14bbf00000000abaaaa3d0000003e000075419a99e13f000000c0bce61abf48d14bbf00000000abaa2a3e0000003e000075419a99e13f0000c0bfbce61abf48d14bbf000000000000803e0000003e000075419a99e13fffff7fbfbde61abf4ad14bbf00000000abaaaa3e0000003e000075419a99e13f010000bfbde61abf4ad14bbf000000005555d53e0000003e000075419a99e13f00000000bde61abf4ad14bbf000000000000003f0000003e000075419a99e13ffcffff3ebce61abf49d14bbf000000005555153f0000003e000075419a99e13f0100803fbde61abf49d14bbf00000000abaa2a3f0000003e000075419a99e13f0000c03fbce61abf49d14bbf000000000000403f0000003e000075419a99e13fffffff3fbde61abf48d14bbf000000005555553f0000003e000075419a99e13f00002040bde61abf49d14bbf00000000abaa6a3f0000003e000075419a99e13f00004040bde61abf48d14bbf000000000000803f0000003e00007a413333c33f000040c0bde61abf48d14bbf00000000000000000000803e00007a413333c33f000020c0bce61abf48d14bbf00000000abaaaa3d0000803e00007a413333c33f000000c0bce61abf48d14bbf00000000abaa2a3e0000803e00007a413333c33f0000c0bfbde61abf48d14bbf000000000000803e0000803e00007a413333c33fffff7fbfbde61abf4ad14bbf00000000abaaaa3e0000803e00007a413333c33f010000bfbde61abf4ad14bbf000000005555d53e0000803e00007a413333c33f00000000bde61abf4ad14bbf000000000000003f0000803e00007a413333c33ffcffff3ebde61abf49d14bbf000000005555153f0000803e00007a413333c33f0100803fbde61abf49d14bbf00000000abaa2a3f0000803e00007a413333c33f0000c03fbce61abf49d14bbf000000000000403f0000803e00007a413333c33fffffff3fbde61abf4ad14bbf000000005555553f0000803e00007a413333c33f00002040bde61abf49d14bbf00000000abaa6a3f0000803e00007a413333c33f00004040bde61abf4ad14bbf000000000000803f0000803e00007f41cdcca43f000040c0bde61abf4ad14bbf00000000000000000000c03e00007f41cdcca43f000020c0bce61abf48d14bbf00000000abaaaa3d0000c03e00007f41cdcca43f000000c0bce61abf48d14bbf00000000abaa2a3e0000c03e00007f41cdcca43f0000c0bfbce61abf48d14bbf000000000000803e0000c03e00007f41cdcca43fffff7fbfbde61abf4ad14bbf00000000abaaaa3e0000c03e00007f41cdcca43f010000bfbde61abf4ad14bbf000000005555d53e0000c03e00007f41cdcca43f00000000bde61abf4ad14bbf000000000000003f0000c03e00007f41cdcca43ffcffff3ebce61abf49d14bbf000000005555153f0000c03e00007f41cdcca43f0100803fbde61abf49d14bbf00000000abaa2a3f0000c03e00007f41cdcca43f0000c03fbce61abf49d14bbf000000000000403f0000c03e00007f41cdcca43fffffff3fbde61abf48d14bbf000000005555553f0000c03e00007f41cdcca43f00002040bde61abf49d14bbf00000000abaa6a3f0000c03e00007f41cdcca43f00004040bde61abf48d14bbf000000000000803f0000c03e000082416666863f000040c0bde61abf48d14bbf00000000000000000000003f000082416666863f000020c0bce61abf48d14bbf00000000abaaaa3d0000003f000082416666863f000000c0bce61abf48d14bbf00000000abaa2a3e0000003f000082416666863f0000c0bfbde61abf48d14bbf000000000000803e0000003f000082416666863fffff7fbfbde61abf4ad14bbf00000000abaaaa3e0000003f000082416666863f010000bfbde61abf4ad14bbf000000005555d53e0000003f000082416666863f00000000bde61abf4ad14bbf000000000000003f0000003f000082416666863ffcffff3ebde61abf49d14bbf000000005555153f0000003f000082416666863f0100803fbde61abf49d14bbf00000000abaa2a3f0000003f000082416666863f0000c03fbce61abf49d14bbf000000000000403f0000003f000082416666863fffffff3fbde61abf4ad14bbf000000005555553f0000003f000082416666863f00002040bde61abf49d14bbf00000000abaa6a3f0000003f000082416666863f00004040bde61abf4ad14bbf000000000000803f0000003f008084410000503f000040c0bce61abf4ad14bbf00000000000000000000203f008084410000503f000020c0bde61abf4ad14bbf00000000abaaaa3d0000203f008084410000503f000000c0bde61abf4ad14bbf00000000abaa2a3e0000203f008084410000503f0000c0bfbce61abf48d14bbf000000000000803e0000203f008084410000503fffff7fbfbce61abf4ad14bbf00000000abaaaa3e0000203f008084410000503f010000bfbce61abf4ad14bbf000000005555d53e0000203f008084410000503f00000000bce61abf4ad14bbf000000000000003f0000203f008084410000503ffcffff3ebce61abf49d14bbf000000005555153f0000203f008084410000503f0100803fbce61abf49d14bbf00000000abaa2a3f0000203f008084410000503f0000c03fbbe61abf4bd14bbf000000000000403f0000203f008084410000503fffffff3fbce61abf4ad14bbf000000005555553f0000203f008084410000503f00002040bbe61abf49d14bbf00000000abaa6a3f0000203f008084410000503f00004040bde61abf4ad14bbf000000000000803f0000203f000087413333133f000040c0bde61abf4ad14bbf00000000000000000000403f000087413333133f000020c0bde61abf4ad14bbf00000000abaaaa3d0000403f000087413333133f000000c0bde61abf4ad14bbf00000000abaa2a3e0000403f000087413333133f0000c0bfbde61abf4ad14bbf000000000000803e0000403f000087413333133fffff7fbfbde61abf4ad14bbf00000000abaaaa3e0000403f000087413333133f010000bfbde61abf4ad14bbf000000005555d53e0000403f000087413333133f00000000bde61abf4ad14bbf000000000000003f0000403f000087413333133ffcffff3ebce61abf49d14bbf000000005555153f0000403f000087413333133f0100803fbde61abf49d14bbf00000000abaa2a3f0000403f000087413333133f0000c03fbce61abf49d14bbf000000000000403f0000403f000087413333133fffffff3fbce61abf48d14bbf000000005555553f0000403f000087413333133f00002040bde61abf49d14bbf00000000abaa6a3f0000403f000087413333133f00004040bde61abf4ad14bbf000000000000803f0000403f00808941cdccac3e000040c0bde61abf4ad14bbf00000000000000000000603f00808941cdccac3e000020c0bde61abf4ad14bbf00000000abaaaa3d0000603f00808941cdccac3e000000c0bde61abf4ad14bbf00000000abaa2a3e0000603f00808941cdccac3e0000c0bfbce61abf48d14bbf000000000000803e0000603f00808941cdccac3effff7fbfbde61abf4ad14bbf00000000abaaaa3e0000603f00808941cdccac3e010000bfbde61abf4ad14bbf000000005555d53e0000603f00808941cdccac3e00000000bde61abf4ad14bbf000000000000003f0000603f00808941cdccac3efcffff3ebce61abf49d14bbf000000005555153f0000603f00808941cdccac3e0100803fbde61abf49d14bbf00000000abaa2a3f0000603f00808941cdccac3e0000c03fbce61abf4bd14bbf000000000000403f0000603f00808941cdccac3effffff3fbde61abf4ad14bbf000000005555553f0000603f00808941cdccac3e00002040bde61abf49d14bbf00000000abaa6a3f0000603f00808941cdccac3e00004040bde61abf4ad14bbf000000000000803f0000603f00008c41cdcccc3d000040c0bde61abf49d14bbf00000000000000000000803f00008c41cdcccc3d000020c0bce61abf48d14bbf00000000abaaaa3d0000803f00008c41cdcccc3d000000c0bce61abf48d14bbf00000000abaa2a3e0000803f00008c41cdcccc3d0000c0bfbde61abf48d14bbf000000000000803e0000803f00008c41cdcccc3dffff7fbfbde61abf49d14bbf00000000abaaaa3e0000803f00008c41cdcccc3d010000bfbce61abf48d14bbf000000005555d53e0000803f00008c41cdcccc3d00000000bde61abf49d14bbf000000000000003f0000803f00008c41cdcccc3dfcffff3ebde61abf49d14bbf000000005555153f0000803f00008c41cdcccc3d0100803fbce61abf49d14bbf00000000abaa2a3f0000803f00008c41cdcccc3d0000c03fbde61abf4ad14bbf000000000000403f0000803f00008c41cdcccc3dffffff3fbde61abf49d14bbf000000005555553f0000803f00008c41cdcccc3d00002040bde61abf49d14bbf00000000abaa6a3f0000803f00008c41cdcccc3d00004040bde61abf49d14bbf000000000000803f0000803f
+  m_CompressedMesh:
+    m_Vertices:
+      m_NumItems: 0
+      m_Range: 0
+      m_Start: 0
+      m_Data: 
+      m_BitSize: 0
+    m_UV:
+      m_NumItems: 0
+      m_Range: 0
+      m_Start: 0
+      m_Data: 
+      m_BitSize: 0
+    m_Normals:
+      m_NumItems: 0
+      m_Range: 0
+      m_Start: 0
+      m_Data: 
+      m_BitSize: 0
+    m_Tangents:
+      m_NumItems: 0
+      m_Range: 0
+      m_Start: 0
+      m_Data: 
+      m_BitSize: 0
+    m_Weights:
+      m_NumItems: 0
+      m_Data: 
+      m_BitSize: 0
+    m_NormalSigns:
+      m_NumItems: 0
+      m_Data: 
+      m_BitSize: 0
+    m_TangentSigns:
+      m_NumItems: 0
+      m_Data: 
+      m_BitSize: 0
+    m_FloatColors:
+      m_NumItems: 0
+      m_Range: 0
+      m_Start: 0
+      m_Data: 
+      m_BitSize: 0
+    m_BoneIndices:
+      m_NumItems: 0
+      m_Data: 
+      m_BitSize: 0
+    m_Triangles:
+      m_NumItems: 0
+      m_Data: 
+      m_BitSize: 0
+    m_UVInfo: 0
+  m_LocalAABB:
+    m_Center: {x: 16.25, y: 1.05, z: 0}
+    m_Extent: {x: 1.25, y: 0.95, z: 3}
+  m_MeshUsageFlags: 0
+  m_CookingOptions: 30
+  m_BakedConvexCollisionMesh: 
+  m_BakedTriangleCollisionMesh: 
+  'm_MeshMetrics[0]': 1
+  'm_MeshMetrics[1]': 1
+  m_MeshOptimizationFlags: 1
+  m_StreamData:
+    serializedVersion: 2
+    offset: 0
+    size: 0
+    path: 
+  m_MeshLodInfo:
+    serializedVersion: 2
+    m_LodSelectionCurve:
+      serializedVersion: 1
+      m_LodSlope: 0
+      m_LodBias: 0
+    m_NumLevels: 1
+    m_SubMeshes:
+    - serializedVersion: 2
+      m_Levels:
+      - serializedVersion: 1
+        m_IndexStart: 0
+        m_IndexCount: 0
 --- !u!1 &1773673564
 GameObject:
   m_ObjectHideFlags: 0
   m_CorrespondingSourceObject: {fileID: 0}
   m_PrefabInstance: {fileID: 0}
   m_PrefabAsset: {fileID: 0}
   serializedVersion: 6
   m_Component:
   - component: {fileID: 1773673566}
   - component: {fileID: 1773673565}
@@ -5205,27 +5250,31 @@ MonoBehaviour:
   m_PrefabAsset: {fileID: 0}
   m_GameObject: {fileID: 1773673564}
   m_Enabled: 1
   m_EditorHideFlags: 0
   m_Script: {fileID: 11500000, guid: f9d2527b11b83864e9a65efe3e964b7b, type: 3}
   m_Name: 
   m_EditorClassIdentifier: Assembly-CSharp::GameManager
   ball: {fileID: 1911587981}
   player: {fileID: 887825628}
   opponent: {fileID: 620006446}
+  inputReader: {fileID: 887825636}
   matchDuration: 180
   targetScore: 0
   countdownFrom: 3
   countdownStep: 1
   startFlashDuration: 0.6
   goalFlashDuration: 1.4
   netCelebrationDelay: 0.7
+  ballBoundsHalfExtents: {x: 22, y: 13}
+  ballMinHeight: -3
+  ballMaxHeight: 25
   goalEffectPrefab: {fileID: 1728683305785374953, guid: 682574085df547f4f96916a48e7acb8b, type: 3}
   autoStartMatch: 0
 --- !u!4 &1773673566
 Transform:
   m_ObjectHideFlags: 0
   m_CorrespondingSourceObject: {fileID: 0}
   m_PrefabInstance: {fileID: 0}
   m_PrefabAsset: {fileID: 0}
   m_GameObject: {fileID: 1773673564}
   serializedVersion: 2
@@ -6232,33 +6281,20 @@ Transform:
   m_Children:
   - {fileID: 1500165851}
   - {fileID: 1133700394}
   - {fileID: 1997558858}
   - {fileID: 650808212}
   - {fileID: 1119801612}
   - {fileID: 1296423143}
   - {fileID: 558602402}
   m_Father: {fileID: 0}
   m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}
---- !u!134 &2134320507
-PhysicsMaterial:
-  m_ObjectHideFlags: 0
-  m_CorrespondingSourceObject: {fileID: 0}
-  m_PrefabInstance: {fileID: 0}
-  m_PrefabAsset: {fileID: 0}
-  m_Name: ' (Instance) (Instance)'
-  serializedVersion: 2
-  m_DynamicFriction: 0.6
-  m_StaticFriction: 0.6
-  m_Bounciness: 0
-  m_FrictionCombine: 0
-  m_BounceCombine: 0
 --- !u!1 &2140412247
 GameObject:
   m_ObjectHideFlags: 0
   m_CorrespondingSourceObject: {fileID: 0}
   m_PrefabInstance: {fileID: 0}
   m_PrefabAsset: {fileID: 0}
   serializedVersion: 6
   m_Component:
   - component: {fileID: 2140412250}
   - component: {fileID: 2140412249}
diff --git a/Assets/_Game/Scripts/Runtime/Camera/CameraViewSwitcher.cs b/Assets/_Game/Scripts/Runtime/Camera/CameraViewSwitcher.cs
index 545de3b..c12b110 100644
--- a/Assets/_Game/Scripts/Runtime/Camera/CameraViewSwitcher.cs
+++ b/Assets/_Game/Scripts/Runtime/Camera/CameraViewSwitcher.cs
@@ -1,26 +1,26 @@
 using UnityEngine;
-using UnityEngine.InputSystem;
 
 /// <summary>
-/// F5로 카메라 시점을 전환한다.
+/// ToggleLegacyCamera 액션으로 카메라 시점을 전환한다.
 /// 기본: 씬에 배치된 고정 시점(기존 화면 그대로).
 /// 전환: 3인칭 - 플레이어 뒤 위쪽에서 뒤통수를 내려다보는 시점.
 ///
 /// 카메라를 대상의 순간 방향(target.forward)에 그대로 붙이지 않고 yaw를 따로 감쇠시킨다.
 /// 대상은 720°/s로 회전하는데, 카메라는 그 뒤 distance만큼 떨어진 원호를 돌기 때문에
 /// 방향 전환/슬라이딩 종료처럼 방향이 급변하는 순간 회전이 그대로 증폭돼 화면이 휘둘린다.
 /// </summary>
 public class CameraViewSwitcher : MonoBehaviour
 {
     [Header("Compatibility")]
     [SerializeField] private bool deferToActionCamera = true;
+    [SerializeField] private GameplayInputReader inputReader;
 
     [Tooltip("따라갈 대상. 비우면 이름이 'Player'인 오브젝트를 찾는다.")]
     [SerializeField] private Transform target;
 
     [Header("3인칭 시점")]
     [Tooltip("대상 뒤로 떨어지는 거리(m).")]
     [SerializeField] private float distance = 5f;
     [Tooltip("대상 위로 올라가는 높이(m).")]
     [SerializeField] private float height = 3f;
     [Tooltip("바라보는 지점을 대상 발밑에서 얼마나 올릴지(m). 머리 근처를 겨냥.")]
@@ -54,22 +54,22 @@ public class CameraViewSwitcher : MonoBehaviour
 
         if (target == null)
         {
             GameObject go = GameObject.Find("Player");
             if (go != null) target = go.transform;
         }
     }
 
     private void Update()
     {
-        Keyboard kb = Keyboard.current;
-        if (kb != null && kb.f5Key.wasPressedThisFrame)
+        if (inputReader != null &&
+            inputReader.ReadButton(GameplayInputAction.ToggleLegacyCamera).WasPressed)
         {
             thirdPerson = !thirdPerson;
             if (thirdPerson) SnapToThirdPerson();
         }
     }
 
     private void LateUpdate()
     {
         if (deferToActionCamera && actionCamera != null && actionCamera.enabled)
             return;
diff --git a/Assets/_Game/Scripts/Runtime/Input/GameplayInputAction.cs b/Assets/_Game/Scripts/Runtime/Input/GameplayInputAction.cs
new file mode 100644
index 0000000..9888375
--- /dev/null
+++ b/Assets/_Game/Scripts/Runtime/Input/GameplayInputAction.cs
@@ -0,0 +1,28 @@
+public enum GameplayInputAction
+{
+    Move,
+    Sprint,
+    Pass,
+    Shot,
+    CancelCharge,
+    Dodge,
+    Punch,
+    SlideTackle,
+    Pause,
+    Restart,
+    ToggleLegacyCamera
+}
+
+public readonly struct GameplayInputButtonState
+{
+    public GameplayInputButtonState(bool wasPressed, bool isPressed, bool wasReleased)
+    {
+        WasPressed = wasPressed;
+        IsPressed = isPressed;
+        WasReleased = wasReleased;
+    }
+
+    public bool WasPressed { get; }
+    public bool IsPressed { get; }
+    public bool WasReleased { get; }
+}
diff --git a/Assets/_Game/Scripts/Runtime/Input/GameplayInputAction.cs.meta b/Assets/_Game/Scripts/Runtime/Input/GameplayInputAction.cs.meta
new file mode 100644
index 0000000..cf2d61b
--- /dev/null
+++ b/Assets/_Game/Scripts/Runtime/Input/GameplayInputAction.cs.meta
@@ -0,0 +1,2 @@
+fileFormatVersion: 2
+guid: c39b48c0fd949cd4f94a732e1a719d5b
\ No newline at end of file
diff --git a/Assets/_Game/Scripts/Runtime/Input/GameplayInputReader.cs b/Assets/_Game/Scripts/Runtime/Input/GameplayInputReader.cs
new file mode 100644
index 0000000..804a35c
--- /dev/null
+++ b/Assets/_Game/Scripts/Runtime/Input/GameplayInputReader.cs
@@ -0,0 +1,68 @@
+using System.Collections.Generic;
+using UnityEngine;
+using UnityEngine.InputSystem;
+
+public class GameplayInputReader : MonoBehaviour
+{
+    private static readonly IReadOnlyDictionary<GameplayInputAction, string> ActionNames =
+        new Dictionary<GameplayInputAction, string>
+        {
+            { GameplayInputAction.Move, "Move" },
+            { GameplayInputAction.Sprint, "Sprint" },
+            { GameplayInputAction.Pass, "Pass" },
+            { GameplayInputAction.Shot, "Shot" },
+            { GameplayInputAction.CancelCharge, "CancelCharge" },
+            { GameplayInputAction.Dodge, "Dodge" },
+            { GameplayInputAction.Punch, "Punch" },
+            { GameplayInputAction.SlideTackle, "SlideTackle" },
+            { GameplayInputAction.Pause, "Pause" },
+            { GameplayInputAction.Restart, "Restart" },
+            { GameplayInputAction.ToggleLegacyCamera, "ToggleLegacyCamera" }
+        };
+
+    [SerializeField] private InputActionAsset inputActions;
+
+    private InputActionMap playerMap;
+
+    private void OnEnable()
+    {
+        playerMap = inputActions != null ? inputActions.FindActionMap("Player", throwIfNotFound: false) : null;
+        playerMap?.Enable();
+    }
+
+    private void OnDisable()
+    {
+        playerMap?.Disable();
+    }
+
+    public GameplayInputButtonState ReadButton(GameplayInputAction action)
+    {
+        InputAction inputAction = ResolveAction(action);
+        return inputAction == null
+            ? default
+            : new GameplayInputButtonState(
+                inputAction.WasPressedThisFrame(),
+                inputAction.IsPressed(),
+                inputAction.WasReleasedThisFrame());
+    }
+
+    public Vector2 ReadMove()
+    {
+        InputAction inputAction = ResolveAction(GameplayInputAction.Move);
+        return inputAction != null ? inputAction.ReadValue<Vector2>() : Vector2.zero;
+    }
+
+    public string GetBindingDisplayString(GameplayInputAction action)
+    {
+        InputAction inputAction = ResolveAction(action);
+        return inputAction != null ? inputAction.GetBindingDisplayString() : string.Empty;
+    }
+
+    private InputAction ResolveAction(GameplayInputAction action)
+    {
+        if (playerMap == null || !ActionNames.TryGetValue(action, out string actionName))
+            return null;
+
+        return playerMap.FindAction(actionName, throwIfNotFound: false);
+    }
+}
diff --git a/Assets/_Game/Scripts/Runtime/Input/GameplayInputReader.cs.meta b/Assets/_Game/Scripts/Runtime/Input/GameplayInputReader.cs.meta
new file mode 100644
index 0000000..3bbdf9a
--- /dev/null
+++ b/Assets/_Game/Scripts/Runtime/Input/GameplayInputReader.cs.meta
@@ -0,0 +1,2 @@
+fileFormatVersion: 2
+guid: f8f46aaa5a60f0b48afc5a8287f590e2
\ No newline at end of file
diff --git a/Assets/_Game/Scripts/Runtime/Input/PlayerActionBindings.cs b/Assets/_Game/Scripts/Runtime/Input/PlayerActionBindings.cs
deleted file mode 100644
index c599be2..0000000
--- a/Assets/_Game/Scripts/Runtime/Input/PlayerActionBindings.cs
+++ /dev/null
@@ -1,40 +0,0 @@
-using System;
-using UnityEngine;
-using UnityEngine.InputSystem;
-
-public enum PlayerMouseButton
-{
-    None,
-    Left,
-    Right,
-    Middle
-}
-
-[Serializable]
-public struct PlayerActionBinding
-{
-    [SerializeField] private PlayerMouseButton mouseButton;
-    [SerializeField] private Key keyboardKey;
-
-    public PlayerActionBinding(PlayerMouseButton mouseButton, Key keyboardKey)
-    {
-        this.mouseButton = mouseButton;
-        this.keyboardKey = keyboardKey;
-    }
-
-    public PlayerMouseButton MouseButton => mouseButton;
-    public Key KeyboardKey => keyboardKey;
-    public string KeyboardKeyName => keyboardKey.ToString();
-}
-
-[CreateAssetMenu(menuName = "Futsal Brawl/Input/Player Action Bindings")]
-public class PlayerActionBindings : ScriptableObject
-{
-    [SerializeField] private PlayerActionBinding pass = new PlayerActionBinding(PlayerMouseButton.Left, Key.None);
-    [SerializeField] private PlayerActionBinding shot = new PlayerActionBinding(PlayerMouseButton.Right, Key.None);
-    [SerializeField] private PlayerActionBinding cancel = new PlayerActionBinding(PlayerMouseButton.None, Key.C);
-
-    public PlayerActionBinding Pass => pass;
-    public PlayerActionBinding Shot => shot;
-    public PlayerActionBinding Cancel => cancel;
-}
diff --git a/Assets/_Game/Scripts/Runtime/Input/PlayerActionBindings.cs.meta b/Assets/_Game/Scripts/Runtime/Input/PlayerActionBindings.cs.meta
deleted file mode 100644
index dd68da8..0000000
--- a/Assets/_Game/Scripts/Runtime/Input/PlayerActionBindings.cs.meta
+++ /dev/null
@@ -1,2 +0,0 @@
-fileFormatVersion: 2
-guid: 3a27b83d671d68846aacb9d2a0265062
\ No newline at end of file
diff --git a/Assets/_Game/Scripts/Runtime/Input/PlayerActionInputReader.cs b/Assets/_Game/Scripts/Runtime/Input/PlayerActionInputReader.cs
deleted file mode 100644
index c333ac8..0000000
--- a/Assets/_Game/Scripts/Runtime/Input/PlayerActionInputReader.cs
+++ /dev/null
@@ -1,62 +0,0 @@
-using UnityEngine.InputSystem;
-using UnityEngine.InputSystem.Controls;
-
-public readonly struct ActionButtonState
-{
-    public ActionButtonState(bool wasPressed, bool isPressed, bool wasReleased)
-    {
-        WasPressed = wasPressed;
-        IsPressed = isPressed;
-        WasReleased = wasReleased;
-    }
-
-    public bool WasPressed { get; }
-    public bool IsPressed { get; }
-    public bool WasReleased { get; }
-}
-
-public static class PlayerActionInputReader
-{
-    public static ActionButtonState Read(PlayerActionBinding binding)
-    {
-        return Combine(
-            ReadControl(ResolveMouseControl(binding.MouseButton)),
-            ReadControl(ResolveKeyboardControl(binding.KeyboardKey)));
-    }
-
-    public static ActionButtonState Combine(ActionButtonState first, ActionButtonState second)
-    {
-        bool isPressed = first.IsPressed || second.IsPressed;
-        return new ActionButtonState(
-            first.WasPressed || second.WasPressed,
-            isPressed,
-            !isPressed && (first.WasReleased || second.WasReleased));
-    }
-
-    private static ActionButtonState ReadControl(ButtonControl control)
-    {
-        return control == null
-            ? default
-            : new ActionButtonState(control.wasPressedThisFrame, control.isPressed, control.wasReleasedThisFrame);
-    }
-
-    private static ButtonControl ResolveMouseControl(PlayerMouseButton button)
-    {
-        Mouse mouse = Mouse.current;
-        if (mouse == null)
-            return null;
-
-        return button switch
-        {
-            PlayerMouseButton.Left => mouse.leftButton,
-            PlayerMouseButton.Right => mouse.rightButton,
-            PlayerMouseButton.Middle => mouse.middleButton,
-            _ => null
-        };
-    }
-
-    private static ButtonControl ResolveKeyboardControl(Key key)
-    {
-        return key == Key.None || Keyboard.current == null ? null : Keyboard.current[key];
-    }
-}
diff --git a/Assets/_Game/Scripts/Runtime/Input/PlayerActionInputReader.cs.meta b/Assets/_Game/Scripts/Runtime/Input/PlayerActionInputReader.cs.meta
deleted file mode 100644
index 4e39154..0000000
--- a/Assets/_Game/Scripts/Runtime/Input/PlayerActionInputReader.cs.meta
+++ /dev/null
@@ -1,2 +0,0 @@
-fileFormatVersion: 2
-guid: 88d48d42bbed2394c8f0038238589ee1
\ No newline at end of file
diff --git a/Assets/_Game/Scripts/Runtime/Input/PlayerInput.cs b/Assets/_Game/Scripts/Runtime/Input/PlayerInput.cs
index 79033e0..f6eea57 100644
--- a/Assets/_Game/Scripts/Runtime/Input/PlayerInput.cs
+++ b/Assets/_Game/Scripts/Runtime/Input/PlayerInput.cs
@@ -1,82 +1,58 @@
 using UnityEngine;
-using UnityEngine.InputSystem;
 
 public class PlayerInput : MonoBehaviour
 {
     [SerializeField] private Transform movementReference;
-    [SerializeField] private PlayerActionBindings actionBindings;
+    [SerializeField] private GameplayInputReader inputReader;
 
     private CharacterLocomotion locomotion;
     private CombatController combat;
     private PlayerBallHandler ball;
     private CharacterState state;
-    private PlayerActionBindings runtimeActionBindings;
-
-    private PlayerActionBindings ActionBindings
-    {
-        get
-        {
-            if (actionBindings != null)
-                return actionBindings;
-
-            if (runtimeActionBindings == null)
-                runtimeActionBindings = ScriptableObject.CreateInstance<PlayerActionBindings>();
-            return runtimeActionBindings;
-        }
-    }
 
     private void Awake()
     {
         locomotion = GetComponent<CharacterLocomotion>();
         if (locomotion == null)
             locomotion = gameObject.AddComponent<CharacterLocomotion>();
 
         combat = GetComponent<CombatController>();
         ball = GetComponent<PlayerBallHandler>();
         state = GetComponent<CharacterState>();
 
         if (movementReference == null && Camera.main != null)
             movementReference = Camera.main.transform;
     }
 
     private void Update()
     {
-        Keyboard kb = Keyboard.current;
-        if (kb == null && Mouse.current == null)
-            return;
-
         if (!GameManager.PlayActive || (state != null && state.IsStunned))
         {
             locomotion.SetPlayerMoveInput(Vector2.zero, sprint: false, hasBall: ball != null && ball.HasBall);
             if (ball != null)
                 ball.SetSprintDribbleInput(false, Vector3.zero);
             return;
         }
 
-        Vector2 moveInput = BuildMoveInput(
-            kb != null && (kb.aKey.isPressed || kb.leftArrowKey.isPressed),
-            kb != null && (kb.dKey.isPressed || kb.rightArrowKey.isPressed),
-            kb != null && (kb.sKey.isPressed || kb.downArrowKey.isPressed),
-            kb != null && (kb.wKey.isPressed || kb.upArrowKey.isPressed));
-
-        bool sprint = kb != null && (kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed);
+        Vector2 moveInput = inputReader != null ? inputReader.ReadMove() : Vector2.zero;
+        bool sprint = inputReader != null && inputReader.ReadButton(GameplayInputAction.Sprint).IsPressed;
         bool hasBall = ball != null && ball.HasBall;
         Vector3 moveDirection = BuildCameraRelativeMoveDirection(moveInput, movementReference);
         locomotion.SetPlayerMoveInput(moveInput, moveDirection, sprint, hasBall);
 
         Vector3 actionDirection = locomotion.ActionDirection;
-        if (kb != null && kb.lKey.wasPressedThisFrame)
+        if (inputReader != null && inputReader.ReadButton(GameplayInputAction.Dodge).WasPressed)
             locomotion.TryDodge(actionDirection);
-        if (kb != null && kb.jKey.wasPressedThisFrame && combat != null)
+        if (inputReader != null && inputReader.ReadButton(GameplayInputAction.Punch).WasPressed && combat != null)
             combat.Punch(actionDirection);
-        if (kb != null && kb.kKey.wasPressedThisFrame && combat != null)
+        if (inputReader != null && inputReader.ReadButton(GameplayInputAction.SlideTackle).WasPressed && combat != null)
             combat.SlideTackle(actionDirection);
 
         if (ball != null)
         {
             ball.SetSprintDribbleInput(sprint, actionDirection);
             HandleBallActions();
         }
     }
 
     public static Vector2 BuildMoveInput(bool leftPressed, bool rightPressed, bool downPressed, bool upPressed)
@@ -106,23 +82,29 @@ public class PlayerInput : MonoBehaviour
         direction = direction.normalized;
         if (direction.sqrMagnitude > 0.0001f)
             return direction;
 
         fallbackForward.y = 0f;
         return fallbackForward.sqrMagnitude > 0.0001f ? fallbackForward.normalized : Vector3.forward;
     }
 
     private void HandleBallActions()
     {
-        ActionButtonState cancel = PlayerActionInputReader.Read(ActionBindings.Cancel);
-        ActionButtonState pass = PlayerActionInputReader.Read(ActionBindings.Pass);
-        ActionButtonState shot = PlayerActionInputReader.Read(ActionBindings.Shot);
+        GameplayInputButtonState cancel = inputReader != null
+            ? inputReader.ReadButton(GameplayInputAction.CancelCharge)
+            : default;
+        GameplayInputButtonState pass = inputReader != null
+            ? inputReader.ReadButton(GameplayInputAction.Pass)
+            : default;
+        GameplayInputButtonState shot = inputReader != null
+            ? inputReader.ReadButton(GameplayInputAction.Shot)
+            : default;
 
         if (cancel.WasPressed)
         {
             ball.CancelCharge();
             return;
         }
 
         if (ball.IsCharging)
         {
             Vector3 cameraForward = BuildPlanarCameraForward(movementReference, transform.forward);
@@ -132,16 +114,11 @@ public class PlayerInput : MonoBehaviour
                 ball.ReleaseCharge(BallChargeAction.Shot, cameraForward);
             return;
         }
 
         if (pass.WasPressed)
             ball.StartCharge(BallChargeAction.Pass);
         else if (shot.WasPressed)
             ball.StartCharge(BallChargeAction.Shot);
     }
 
-    private void OnDestroy()
-    {
-        if (runtimeActionBindings != null)
-            Destroy(runtimeActionBindings);
-    }
 }
diff --git a/Assets/_Game/Scripts/Runtime/Match/GameManager.cs b/Assets/_Game/Scripts/Runtime/Match/GameManager.cs
index aa71838..4cf2f8d 100644
--- a/Assets/_Game/Scripts/Runtime/Match/GameManager.cs
+++ b/Assets/_Game/Scripts/Runtime/Match/GameManager.cs
@@ -1,17 +1,16 @@
 using System.Collections;
 using UnityEngine;
-using UnityEngine.InputSystem;
 
 /// <summary>
 /// 경기 전체 흐름(게임 루프)을 총괄한다.
-/// 상태: Kickoff(카운트다운) → Playing(진행) → GameOver(종료). ESC로 일시정지/재개.
+/// 상태: Kickoff(카운트다운) → Playing(진행) → GameOver(종료). Pause 액션으로 일시정지/재개.
 /// 점수·타이머·승패를 관리하고, 각 상태에 맞춰 PlayActive로 입력·AI·공 소유를 잠그거나 푼다.
 ///
 /// 표현(UI)은 이 매니저가 직접 그리지 않는다 — MatchUI가 아래 공개 상태
 /// (State/PlayerScore/OpponentScore/TimeRemaining/CenterMessage/IsPaused)를 읽어 담당한다.
 /// 즉 "로직=GameManager, 표시=MatchUI"로 분리한다.
 ///
 /// 기존 플레이 로직(이동/슛/전투/득점 판정)은 변경하지 않는다.
 /// 모든 플레이어/AI/공은 GameManager.PlayActive만 확인하므로, 상태 전환만으로 락이 걸린다.
 /// </summary>
 public class GameManager : MonoBehaviour
@@ -21,20 +20,21 @@ public class GameManager : MonoBehaviour
     public static GameManager Instance { get; private set; }
 
     /// <summary>플레이 활성 상태. Playing이면서 일시정지가 아닐 때만 true → 입력/AI/공 소유가 동작한다.</summary>
     public static bool PlayActive { get; private set; }
 
     [Header("Scene References")]
     [Tooltip("공 Rigidbody. 비우면 이름 'Ball'로 자동 검색.")]
     [SerializeField] private Rigidbody ball;
     [SerializeField] private Transform player;
     [SerializeField] private Transform opponent;
+    [SerializeField] private GameplayInputReader inputReader;
 
     [Header("Match Rules")]
     [Tooltip("경기 제한 시간(초). 기본 180초 = 3분. Playing 중에만 흐른다.")]
     [SerializeField] private float matchDuration = 180f;
     [Tooltip("이 점수에 먼저 도달하면 시간과 무관하게 즉시 경기 종료. 0 이하면 비활성(시간 제한만 사용).")]
     [SerializeField] private int targetScore = 0;
 
     [Header("Kickoff / Timing")]
     [Tooltip("킥오프 카운트다운 시작 숫자 (3 → \"3, 2, 1, START!\").")]
     [SerializeField] private int countdownFrom = 3;
@@ -106,30 +106,30 @@ public class GameManager : MonoBehaviour
 
     /// <summary>메뉴/로비에서 경기를 시작시킬 때 호출. (autoStartMatch를 끈 경우)</summary>
     public void BeginMatch()
     {
         StopAllCoroutines();
         StartCoroutine(NewMatchRoutine());
     }
 
     private void Update()
     {
-        Keyboard kb = Keyboard.current;
-        if (kb == null) return;
-
-        // ESC: 일시정지/재개 토글 (종료 화면에서는 무시).
-        if (kb.escapeKey.wasPressedThisFrame && State != MatchState.GameOver)
+        // Pause: 일시정지/재개 토글 (종료 화면에서는 무시).
+        if (inputReader != null &&
+            inputReader.ReadButton(GameplayInputAction.Pause).WasPressed &&
+            State != MatchState.GameOver)
             TogglePause();
 
-        // 종료 화면: R 또는 Space로 새 경기.
+        // 종료 화면: Restart 액션으로 새 경기.
         if (State == MatchState.GameOver && !IsPaused &&
-            (kb.rKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame))
+            inputReader != null &&
+            inputReader.ReadButton(GameplayInputAction.Restart).WasPressed)
         {
             BeginMatch();
             return;
         }
 
         // 경기 시간은 Playing 중에만 흐른다. (일시정지 시 timeScale=0이라 deltaTime=0이지만 상태로도 이중 차단.)
         if (State == MatchState.Playing && !IsPaused)
         {
             EnforceBallBounds();
             TimeRemaining -= Time.deltaTime;
diff --git a/Assets/_Game/Scripts/Runtime/UI/ViewHintUI.cs b/Assets/_Game/Scripts/Runtime/UI/ViewHintUI.cs
index 62bf0f4..ab2db7f 100644
--- a/Assets/_Game/Scripts/Runtime/UI/ViewHintUI.cs
+++ b/Assets/_Game/Scripts/Runtime/UI/ViewHintUI.cs
@@ -1,23 +1,24 @@
 using UnityEngine;
 using UnityEngine.UI;
 
 /// <summary>
-/// 화면 좌하단에 "F5: 시점 전환"과 현재 시점을 표시한다.
+/// 화면 좌하단에 현재 시점 전환 바인딩과 현재 시점을 표시한다.
 /// 로직은 갖지 않고 CameraViewSwitcher의 공개 상태(IsThirdPerson)만 읽어 그린다.
 /// (AbilityCooldownUI와 같은 이유로 계층을 코드로 만든다 — 씬 YAML을 건드리지 않는다.)
 /// </summary>
 public class ViewHintUI : MonoBehaviour
 {
     [Header("References")]
     [Tooltip("표시할 대상 시점 전환기. 비우면 메인 카메라에서 찾는다.")]
     [SerializeField] private CameraViewSwitcher switcher;
+    [SerializeField] private GameplayInputReader inputReader;
 
     [Header("Layout")]
     [Tooltip("화면 좌하단 모서리로부터의 여백(픽셀, 1920x1080 기준).")]
     [SerializeField] private Vector2 screenMargin = new Vector2(48f, 40f);
     [Tooltip("글자 크기(픽셀).")]
     [SerializeField] private int fontSize = 20;
 
     private Text label;
 
     private void Awake()
@@ -29,21 +30,24 @@ public class ViewHintUI : MonoBehaviour
     }
 
     private void Update()
     {
         if (label == null) return;
 
         bool visible = switcher != null;
         if (label.gameObject.activeSelf != visible) label.gameObject.SetActive(visible);
         if (!visible) return;
 
-        label.text = "F5: 시점 전환  —  현재: " + (switcher.IsThirdPerson ? "3인칭" : "기본");
+        string binding = inputReader != null
+            ? inputReader.GetBindingDisplayString(GameplayInputAction.ToggleLegacyCamera)
+            : string.Empty;
+        label.text = binding + ": 시점 전환  —  현재: " + (switcher.IsThirdPerson ? "3인칭" : "기본");
     }
 
     private void Build()
     {
         GameObject go = new GameObject("ViewHint", typeof(RectTransform), typeof(Text));
         RectTransform rt = (RectTransform)go.transform;
         rt.SetParent((RectTransform)transform, false);
         rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 0f);
         rt.sizeDelta = new Vector2(420f, 28f);
         rt.anchoredPosition = screenMargin;
diff --git a/Assets/_Game/Scripts/Tests/EditMode/GameplayInputReaderTests.cs b/Assets/_Game/Scripts/Tests/EditMode/GameplayInputReaderTests.cs
new file mode 100644
index 0000000..dfb30ab
--- /dev/null
+++ b/Assets/_Game/Scripts/Tests/EditMode/GameplayInputReaderTests.cs
@@ -0,0 +1,184 @@
+using System;
+using System.Collections;
+using System.Collections.Generic;
+using System.Reflection;
+using NUnit.Framework;
+using UnityEditor;
+using UnityEngine;
+
+public class GameplayInputReaderTests
+{
+    private const string InputActionsAssetPath = "Assets/_Game/Settings/InputSystem_Actions.inputactions";
+    private const string PlayerAndOtherMapsJson = @"{
+        ""name"": ""GameplayInputReaderTests"",
+        ""maps"": [
+            {
+                ""name"": ""Player"",
+                ""id"": ""9f5c8df2-8f9f-46db-8b7c-2bc95c6e3d90"",
+                ""actions"": [{ ""name"": ""ToggleLegacyCamera"", ""type"": ""Button"", ""id"": ""c1194513-d479-4684-8dc3-6c553ae94311"" }],
+                ""bindings"": [{ ""id"": ""d11a20e2-2a22-467e-9dc5-b51da781cb99"", ""path"": ""<Keyboard>/f5"", ""action"": ""ToggleLegacyCamera"" }]
+            },
+            {
+                ""name"": ""Other"",
+                ""id"": ""9bb8d8f6-e303-4f93-a582-9270c10e0ad9"",
+                ""actions"": [],
+                ""bindings"": []
+            }
+        ]
+    }";
+
+    [Test]
+    public void BindingDisplayString_UsesTheActionOverrideWhenPresent()
+    {
+        ScriptableObject asset = CreateInputAsset(PlayerAndOtherMapsJson);
+        GameplayInputReader reader = CreateReader(asset);
+        object action = FindAction(asset, "ToggleLegacyCamera");
+        ApplyBindingOverride(action, "<Keyboard>/f6");
+
+        try
+        {
+            Assert.That(reader.GetBindingDisplayString(GameplayInputAction.ToggleLegacyCamera), Is.EqualTo("F6"));
+        }
+        finally
+        {
+            DestroyReaderAndAsset(reader, asset);
+        }
+    }
+
+    [Test]
+    public void MissingMapOrAction_ReturnsNeutralStates()
+    {
+        ScriptableObject missingActionAsset = CreateInputAsset(PlayerAndOtherMapsJson);
+        GameplayInputReader missingActionReader = CreateReader(missingActionAsset);
+        ScriptableObject missingMapAsset = CreateInputAsset(@"{ ""name"": ""NoPlayerMap"", ""maps"": [] }");
+        GameplayInputReader missingMapReader = CreateReader(missingMapAsset);
+
+        try
+        {
+            GameplayInputButtonState missingAction = missingActionReader.ReadButton(GameplayInputAction.Pass);
+            GameplayInputButtonState missingMap = missingMapReader.ReadButton(GameplayInputAction.Pass);
+
+            Assert.That(missingAction.IsPressed || missingAction.WasPressed || missingAction.WasReleased, Is.False);
+            Assert.That(missingActionReader.ReadMove(), Is.EqualTo(Vector2.zero));
+            Assert.That(missingActionReader.GetBindingDisplayString(GameplayInputAction.Pass), Is.Empty);
+            Assert.That(missingMap.IsPressed || missingMap.WasPressed || missingMap.WasReleased, Is.False);
+        }
+        finally
+        {
+            DestroyReaderAndAsset(missingActionReader, missingActionAsset);
+            DestroyReaderAndAsset(missingMapReader, missingMapAsset);
+        }
+    }
+
+    [Test]
+    public void Reader_EnablesOnlyPlayerMap_AndLeavesOtherMapDisabled()
+    {
+        ScriptableObject asset = CreateInputAsset(PlayerAndOtherMapsJson);
+        GameplayInputReader reader = CreateReader(asset);
+        object playerMap = FindActionMap(asset, "Player");
+        object otherMap = FindActionMap(asset, "Other");
+
+        try
+        {
+            Assert.That(IsEnabled(playerMap), Is.True);
+            Assert.That(IsEnabled(otherMap), Is.False);
+
+            InvokeLifecycle(reader, "OnDisable");
+
+            Assert.That(IsEnabled(playerMap), Is.False);
+            Assert.That(IsEnabled(otherMap), Is.False);
+        }
+        finally
+        {
+            DestroyReaderAndAsset(reader, asset);
+        }
+    }
+
+    [Test]
+    public void PlayerMap_ContainsTheGameplayInputBindingContract()
+    {
+        ScriptableObject asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(InputActionsAssetPath);
+
+        Assert.That(asset, Is.Not.Null, $"Expected input action asset at {InputActionsAssetPath}.");
+        AssertActionBindings(asset, "Move", "<Keyboard>/w", "<Keyboard>/upArrow", "<Keyboard>/a", "<Keyboard>/leftArrow", "<Keyboard>/s", "<Keyboard>/downArrow", "<Keyboard>/d", "<Keyboard>/rightArrow");
+        AssertActionBindings(asset, "Sprint", "<Keyboard>/leftShift", "<Keyboard>/rightShift");
+        AssertActionBindings(asset, "Pass", "<Mouse>/leftButton");
+        AssertActionBindings(asset, "Shot", "<Mouse>/rightButton");
+        AssertActionBindings(asset, "CancelCharge", "<Keyboard>/c");
+        AssertActionBindings(asset, "Dodge", "<Keyboard>/l");
+        AssertActionBindings(asset, "Punch", "<Keyboard>/j");
+        AssertActionBindings(asset, "SlideTackle", "<Keyboard>/k");
+        AssertActionBindings(asset, "Pause", "<Keyboard>/escape");
+        AssertActionBindings(asset, "Restart", "<Keyboard>/r", "<Keyboard>/space");
+        AssertActionBindings(asset, "ToggleLegacyCamera", "<Keyboard>/f5");
+    }
+
+    private static GameplayInputReader CreateReader(ScriptableObject asset)
+    {
+        GameObject host = new GameObject("GameplayInputReaderTests");
+        host.SetActive(false);
+
+        GameplayInputReader reader = host.AddComponent<GameplayInputReader>();
+        typeof(GameplayInputReader).GetField("inputActions", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(reader, asset);
+        host.SetActive(true);
+        InvokeLifecycle(reader, "OnEnable");
+        return reader;
+    }
+
+    private static ScriptableObject CreateInputAsset(string json)
+    {
+        Type assetType = Type.GetType("UnityEngine.InputSystem.InputActionAsset, Unity.InputSystem");
+        return (ScriptableObject)assetType.GetMethod("FromJson", new[] { typeof(string) }).Invoke(null, new object[] { json });
+    }
+
+    private static object FindAction(ScriptableObject asset, string actionName)
+    {
+        return asset.GetType().GetMethod("FindAction", new[] { typeof(string), typeof(bool) }).Invoke(asset, new object[] { actionName, false });
+    }
+
+    private static object FindActionMap(ScriptableObject asset, string mapName)
+    {
+        return asset.GetType().GetMethod("FindActionMap", new[] { typeof(string), typeof(bool) }).Invoke(asset, new object[] { mapName, false });
+    }
+
+    private static void AssertActionBindings(ScriptableObject asset, string actionName, params string[] expectedPaths)
+    {
+        object action = FindAction(asset, actionName);
+        Assert.That(action, Is.Not.Null, $"Expected Player/{actionName} action.");
+
+        IEnumerable bindings = (IEnumerable)action.GetType().GetProperty("bindings").GetValue(action);
+        List<string> actualPaths = new List<string>();
+        foreach (object binding in bindings)
+        {
+            string path = (string)binding.GetType().GetProperty("effectivePath").GetValue(binding);
+            if (!string.IsNullOrEmpty(path))
+                actualPaths.Add(path);
+        }
+
+        foreach (string expectedPath in expectedPaths)
+            Assert.That(actualPaths, Does.Contain(expectedPath), $"Expected Player/{actionName} to bind {expectedPath}.");
+    }
+
+    private static void ApplyBindingOverride(object action, string path)
+    {
+        Type extensions = Type.GetType("UnityEngine.InputSystem.InputActionRebindingExtensions, Unity.InputSystem");
+        extensions.GetMethod("ApplyBindingOverride", new[] { action.GetType(), typeof(string), typeof(string), typeof(string) })
+            .Invoke(null, new object[] { action, path, null, null });
+    }
+
+    private static bool IsEnabled(object map)
+    {
+        return (bool)map.GetType().GetProperty("enabled").GetValue(map);
+    }
+
+    private static void InvokeLifecycle(GameplayInputReader reader, string methodName)
+    {
+        typeof(GameplayInputReader).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(reader, null);
+    }
+
+    private static void DestroyReaderAndAsset(GameplayInputReader reader, ScriptableObject asset)
+    {
+        UnityEngine.Object.DestroyImmediate(reader.gameObject);
+        UnityEngine.Object.DestroyImmediate(asset);
+    }
+}
diff --git a/Assets/_Game/Scripts/Tests/EditMode/GameplayInputReaderTests.cs.meta b/Assets/_Game/Scripts/Tests/EditMode/GameplayInputReaderTests.cs.meta
new file mode 100644
index 0000000..c787e84
--- /dev/null
+++ b/Assets/_Game/Scripts/Tests/EditMode/GameplayInputReaderTests.cs.meta
@@ -0,0 +1,2 @@
+fileFormatVersion: 2
+guid: 9ff307b99fc5b78418cf950801d39428
\ No newline at end of file
diff --git a/Assets/_Game/Scripts/Tests/EditMode/MatchResetTests.cs b/Assets/_Game/Scripts/Tests/EditMode/MatchResetTests.cs
index b8a5f2e..053475d 100644
--- a/Assets/_Game/Scripts/Tests/EditMode/MatchResetTests.cs
+++ b/Assets/_Game/Scripts/Tests/EditMode/MatchResetTests.cs
@@ -1,16 +1,53 @@
+using System.IO;
 using System.Reflection;
 using NUnit.Framework;
 using UnityEngine;
 
 public class MatchResetTests
 {
+    private static string GameManagerPath => Path.Combine(
+        Application.dataPath,
+        "_Game/Scripts/Runtime/Match/GameManager.cs");
+
+    private static string CameraSwitcherPath => Path.Combine(
+        Application.dataPath,
+        "_Game/Scripts/Runtime/Camera/CameraViewSwitcher.cs");
+
+    private static string ViewHintPath => Path.Combine(
+        Application.dataPath,
+        "_Game/Scripts/Runtime/UI/ViewHintUI.cs");
+
+    [Test]
+    public void GameManager_UsesThePauseInputAction()
+    {
+        Assert.That(
+            File.ReadAllText(GameManagerPath),
+            Does.Contain("GameplayInputAction.Pause"));
+    }
+
+    [Test]
+    public void CameraViewSwitcher_UsesTheCameraToggleInputAction()
+    {
+        Assert.That(
+            File.ReadAllText(CameraSwitcherPath),
+            Does.Contain("GameplayInputAction.ToggleLegacyCamera"));
+    }
+
+    [Test]
+    public void ViewHintUI_UsesTheCameraToggleBindingDisplay()
+    {
+        Assert.That(
+            File.ReadAllText(ViewHintPath),
+            Does.Contain("GetBindingDisplayString"));
+    }
+
     [Test]
     public void ResetCharacter_RestoresMobilityState()
     {
         GameObject managerObject = new GameObject("Game Manager");
         GameObject player = new GameObject("Player");
 
         try
         {
             GameManager manager = managerObject.AddComponent<GameManager>();
             player.AddComponent<Rigidbody>();
diff --git a/Assets/_Game/Scripts/Tests/EditMode/PlayerActionInputReaderTests.cs b/Assets/_Game/Scripts/Tests/EditMode/PlayerActionInputReaderTests.cs
index 9306cc1..3d9b6c8 100644
--- a/Assets/_Game/Scripts/Tests/EditMode/PlayerActionInputReaderTests.cs
+++ b/Assets/_Game/Scripts/Tests/EditMode/PlayerActionInputReaderTests.cs
@@ -1,36 +1,29 @@
+using System.IO;
 using NUnit.Framework;
 using UnityEngine;
 
 public class PlayerActionInputReaderTests
 {
-    [Test]
-    public void DefaultBindings_UseMouseForBallActionsAndCForCancel()
-    {
-        PlayerActionBindings bindings = ScriptableObject.CreateInstance<PlayerActionBindings>();
-        try
-        {
-            Assert.That(bindings.Pass.MouseButton, Is.EqualTo(PlayerMouseButton.Left));
-            Assert.That(bindings.Pass.KeyboardKeyName, Is.EqualTo("None"));
-            Assert.That(bindings.Shot.MouseButton, Is.EqualTo(PlayerMouseButton.Right));
-            Assert.That(bindings.Shot.KeyboardKeyName, Is.EqualTo("None"));
-            Assert.That(bindings.Cancel.MouseButton, Is.EqualTo(PlayerMouseButton.None));
-            Assert.That(bindings.Cancel.KeyboardKeyName, Is.EqualTo("C"));
-        }
-        finally
-        {
-            Object.DestroyImmediate(bindings);
-        }
-    }
+    private static string PlayerInputPath => Path.Combine(
+        Application.dataPath,
+        "_Game/Scripts/Runtime/Input/PlayerInput.cs");
 
     [Test]
-    public void Combine_ReportsReleaseOnlyAfterEveryConfiguredAlternativeIsReleased()
+    public void PlayerInput_UsesSemanticGameplayInputActionsInsteadOfRawControls()
     {
-        ActionButtonState mouseState = new ActionButtonState(wasPressed: false, isPressed: true, wasReleased: false);
-        ActionButtonState keyboardState = new ActionButtonState(wasPressed: false, isPressed: false, wasReleased: true);
-
-        ActionButtonState combined = PlayerActionInputReader.Combine(mouseState, keyboardState);
+        string source = File.ReadAllText(PlayerInputPath);
 
-        Assert.That(combined.IsPressed, Is.True);
-        Assert.That(combined.WasReleased, Is.False);
+        Assert.That(source, Does.Contain("inputReader.ReadMove()"));
+        Assert.That(source, Does.Contain("GameplayInputAction.Sprint"));
+        Assert.That(source, Does.Contain("GameplayInputAction.Pass"));
+        Assert.That(source, Does.Contain("GameplayInputAction.Shot"));
+        Assert.That(source, Does.Contain("GameplayInputAction.CancelCharge"));
+        Assert.That(source, Does.Contain("GameplayInputAction.Dodge"));
+        Assert.That(source, Does.Contain("GameplayInputAction.Punch"));
+        Assert.That(source, Does.Contain("GameplayInputAction.SlideTackle"));
+        Assert.That(source, Does.Not.Contain("Keyboard.current"));
+        Assert.That(source, Does.Not.Contain("Mouse.current"));
+        Assert.That(source, Does.Not.Contain("PlayerActionBindings"));
+        Assert.That(source, Does.Not.Contain("PlayerActionInputReader"));
     }
 }
diff --git a/Assets/_Game/Settings/DefaultPlayerActionBindings.asset b/Assets/_Game/Settings/DefaultPlayerActionBindings.asset
deleted file mode 100644
index d8c5e02..0000000
--- a/Assets/_Game/Settings/DefaultPlayerActionBindings.asset
+++ /dev/null
@@ -1,23 +0,0 @@
-%YAML 1.1
-%TAG !u! tag:unity3d.com,2011:
---- !u!114 &11400000
-MonoBehaviour:
-  m_ObjectHideFlags: 0
-  m_CorrespondingSourceObject: {fileID: 0}
-  m_PrefabInstance: {fileID: 0}
-  m_PrefabAsset: {fileID: 0}
-  m_GameObject: {fileID: 0}
-  m_Enabled: 1
-  m_EditorHideFlags: 0
-  m_Script: {fileID: 11500000, guid: 3a27b83d671d68846aacb9d2a0265062, type: 3}
-  m_Name: DefaultPlayerActionBindings
-  m_EditorClassIdentifier: FutsalGame.Runtime::PlayerActionBindings
-  pass:
-    mouseButton: 1
-    keyboardKey: 0
-  shot:
-    mouseButton: 2
-    keyboardKey: 0
-  cancel:
-    mouseButton: 0
-    keyboardKey: 17
diff --git a/Assets/_Game/Settings/DefaultPlayerActionBindings.asset.meta b/Assets/_Game/Settings/DefaultPlayerActionBindings.asset.meta
deleted file mode 100644
index bb3faa6..0000000
--- a/Assets/_Game/Settings/DefaultPlayerActionBindings.asset.meta
+++ /dev/null
@@ -1,8 +0,0 @@
-fileFormatVersion: 2
-guid: a0d3b780fd6f5f5469d556bfba2eae03
-NativeFormatImporter:
-  externalObjects: {}
-  mainObjectFileID: 11400000
-  userData: 
-  assetBundleName: 
-  assetBundleVariant: 
diff --git a/Assets/_Game/Settings/InputSystem_Actions.inputactions b/Assets/_Game/Settings/InputSystem_Actions.inputactions
index 1a12cb9..b54f78a 100644
--- a/Assets/_Game/Settings/InputSystem_Actions.inputactions
+++ b/Assets/_Game/Settings/InputSystem_Actions.inputactions
@@ -1,11 +1,12 @@
 {
+    "version": 1,
     "name": "InputSystem_Actions",
     "maps": [
         {
             "name": "Player",
             "id": "df70fa95-8a34-4494-b137-73ab6b9c7d37",
             "actions": [
                 {
                     "name": "Move",
                     "type": "Value",
                     "id": "351f2ccd-1f9f-44bf-9bec-d62ac5c5f408",
@@ -78,20 +79,101 @@
                     "initialStateCheck": false
                 },
                 {
                     "name": "Sprint",
                     "type": "Button",
                     "id": "641cd816-40e6-41b4-8c3d-04687c349290",
                     "expectedControlType": "Button",
                     "processors": "",
                     "interactions": "",
                     "initialStateCheck": false
+                },
+                {
+                    "name": "Pass",
+                    "type": "Button",
+                    "id": "5203143b-32fb-4c22-b28f-95f12b256bbd",
+                    "expectedControlType": "",
+                    "processors": "",
+                    "interactions": "",
+                    "initialStateCheck": false
+                },
+                {
+                    "name": "Shot",
+                    "type": "Button",
+                    "id": "35a8803a-f38e-4342-86e1-3f90014d42e8",
+                    "expectedControlType": "",
+                    "processors": "",
+                    "interactions": "",
+                    "initialStateCheck": false
+                },
+                {
+                    "name": "CancelCharge",
+                    "type": "Button",
+                    "id": "d1fd99e4-012e-4ab8-b25d-d215274822bb",
+                    "expectedControlType": "",
+                    "processors": "",
+                    "interactions": "",
+                    "initialStateCheck": false
+                },
+                {
+                    "name": "Dodge",
+                    "type": "Button",
+                    "id": "8133584f-c720-4dd1-9ccd-3e5330bb36b8",
+                    "expectedControlType": "",
+                    "processors": "",
+                    "interactions": "",
+                    "initialStateCheck": false
+                },
+                {
+                    "name": "Punch",
+                    "type": "Button",
+                    "id": "7481b8d5-28ca-4005-aab5-706cb0378ec2",
+                    "expectedControlType": "",
+                    "processors": "",
+                    "interactions": "",
+                    "initialStateCheck": false
+                },
+                {
+                    "name": "SlideTackle",
+                    "type": "Button",
+                    "id": "2a718105-47b7-4533-a629-027489effa25",
+                    "expectedControlType": "",
+                    "processors": "",
+                    "interactions": "",
+                    "initialStateCheck": false
+                },
+                {
+                    "name": "Pause",
+                    "type": "Button",
+                    "id": "54757210-5c16-4219-8f8e-6f7e6800c0c3",
+                    "expectedControlType": "",
+                    "processors": "",
+                    "interactions": "",
+                    "initialStateCheck": false
+                },
+                {
+                    "name": "Restart",
+                    "type": "Button",
+                    "id": "54040be0-8416-4656-83e1-b944c29b7286",
+                    "expectedControlType": "",
+                    "processors": "",
+                    "interactions": "",
+                    "initialStateCheck": false
+                },
+                {
+                    "name": "ToggleLegacyCamera",
+                    "type": "Button",
+                    "id": "80a4171f-307c-466d-b87f-3a140f92a1fd",
+                    "expectedControlType": "",
+                    "processors": "",
+                    "interactions": "",
+                    "initialStateCheck": false
                 }
             ],
             "bindings": [
                 {
                     "name": "",
                     "id": "978bfe49-cc26-4a3d-ab7b-7d7a29327403",
                     "path": "<Gamepad>/leftStick",
                     "interactions": "",
                     "processors": "",
                     "groups": ";Gamepad",
@@ -465,20 +547,141 @@
                 {
                     "name": "",
                     "id": "36e52cba-0905-478e-a818-f4bfcb9f3b9a",
                     "path": "<Keyboard>/c",
                     "interactions": "",
                     "processors": "",
                     "groups": "Keyboard&Mouse",
                     "action": "Crouch",
                     "isComposite": false,
                     "isPartOfComposite": false
+                },
+                {
+                    "name": "",
+                    "id": "844b139f-fa28-4c57-9815-e5cff92e0663",
+                    "path": "<Keyboard>/rightShift",
+                    "interactions": "",
+                    "processors": "",
+                    "groups": "Keyboard&Mouse",
+                    "action": "Sprint",
+                    "isComposite": false,
+                    "isPartOfComposite": false
+                },
+                {
+                    "name": "",
+                    "id": "f044cd22-e7ee-4d48-8929-310ce9386948",
+                    "path": "<Mouse>/leftButton",
+                    "interactions": "",
+                    "processors": "",
+                    "groups": "Keyboard&Mouse",
+                    "action": "Pass",
+                    "isComposite": false,
+                    "isPartOfComposite": false
+                },
+                {
+                    "name": "",
+                    "id": "1eb7686d-cea9-4ebf-8a28-457b95c96b07",
+                    "path": "<Mouse>/rightButton",
+                    "interactions": "",
+                    "processors": "",
+                    "groups": "Keyboard&Mouse",
+                    "action": "Shot",
+                    "isComposite": false,
+                    "isPartOfComposite": false
+                },
+                {
+                    "name": "",
+                    "id": "58b4b755-2a67-400f-b093-96b62d581a9b",
+                    "path": "<Keyboard>/c",
+                    "interactions": "",
+                    "processors": "",
+                    "groups": "Keyboard&Mouse",
+                    "action": "CancelCharge",
+                    "isComposite": false,
+                    "isPartOfComposite": false
+                },
+                {
+                    "name": "",
+                    "id": "356bbece-ba8a-4534-9496-5ed2e00950f4",
+                    "path": "<Keyboard>/l",
+                    "interactions": "",
+                    "processors": "",
+                    "groups": "Keyboard&Mouse",
+                    "action": "Dodge",
+                    "isComposite": false,
+                    "isPartOfComposite": false
+                },
+                {
+                    "name": "",
+                    "id": "0adb7143-c6da-4c45-98b1-0ca26b3cdedd",
+                    "path": "<Keyboard>/j",
+                    "interactions": "",
+                    "processors": "",
+                    "groups": "Keyboard&Mouse",
+                    "action": "Punch",
+                    "isComposite": false,
+                    "isPartOfComposite": false
+                },
+                {
+                    "name": "",
+                    "id": "7566c631-45d3-433f-9976-15e21071a730",
+                    "path": "<Keyboard>/k",
+                    "interactions": "",
+                    "processors": "",
+                    "groups": "Keyboard&Mouse",
+                    "action": "SlideTackle",
+                    "isComposite": false,
+                    "isPartOfComposite": false
+                },
+                {
+                    "name": "",
+                    "id": "3e660857-130d-4cfb-a695-be3e19c6f56a",
+                    "path": "<Keyboard>/escape",
+                    "interactions": "",
+                    "processors": "",
+                    "groups": "Keyboard&Mouse",
+                    "action": "Pause",
+                    "isComposite": false,
+                    "isPartOfComposite": false
+                },
+                {
+                    "name": "",
+                    "id": "af566c8a-1726-4bad-acc8-ae4dbb3c33ab",
+                    "path": "<Keyboard>/r",
+                    "interactions": "",
+                    "processors": "",
+                    "groups": "Keyboard&Mouse",
+                    "action": "Restart",
+                    "isComposite": false,
+                    "isPartOfComposite": false
+                },
+                {
+                    "name": "",
+                    "id": "f2982f8c-f6f3-4ac7-9be7-35b809fbd048",
+                    "path": "<Keyboard>/space",
+                    "interactions": "",
+                    "processors": "",
+                    "groups": "Keyboard&Mouse",
+                    "action": "Restart",
+                    "isComposite": false,
+                    "isPartOfComposite": false
+                },
+                {
+                    "name": "",
+                    "id": "839a1278-86f6-478d-82c6-637542beb25d",
+                    "path": "<Keyboard>/f5",
+                    "interactions": "",
+                    "processors": "",
+                    "groups": "Keyboard&Mouse",
+                    "action": "ToggleLegacyCamera",
+                    "isComposite": false,
+                    "isPartOfComposite": false
                 }
             ]
         },
         {
             "name": "UI",
             "id": "272f6d14-89ba-496f-b7ff-215263d3219f",
             "actions": [
                 {
                     "name": "Navigate",
                     "type": "PassThrough",
diff --git a/IMPLEMENTATION_STATUS.md b/IMPLEMENTATION_STATUS.md
index 3e58864..2aaf451 100644
--- a/IMPLEMENTATION_STATUS.md
+++ b/IMPLEMENTATION_STATUS.md
@@ -6,20 +6,27 @@
 현재 체크아웃 기준의 구현 상태만 기록한다. 세부 설계나 작업 이력은 이 문서에 길게 누적하지 않는다.
 
 ## 2026-07-20 Update
 
 - Combat tuning is separated into `CombatConfig` ScriptableObject data with `DefaultCombatConfig.asset` linked from scene and `NetPlayer` combat components.
 - Ball possession, dribble, shot, and physics tuning is separated into `BallConfig` ScriptableObject data with `DefaultBallConfig.asset` linked from scene/player ball components.
 - `Ball` now has an explicit `BallController` in the active scene instead of relying only on runtime attachment from `PlayerBallHandler`.
 - `PlayerBallHandler` remains the compatibility facade for `CurrentOwner`, `HasBall`, `Shoot`, `ForceRelease`, `ClearPossession`, `IsCharging`, and `ChargeAmount01`.
 - Player-specific initial acquisition, delayed reacquisition, release bookkeeping, and ownership cleanup now live in `BallPossessionController`; charge, shoot, dribble placement, and presentation remain in the facade.
 
+## 2026-07-25 Unified input scene wiring
+
+- `SampleScene` uses the single `GameplayInputReader` on `Player`, backed by `Assets/_Game/Settings/InputSystem_Actions.inputactions`.
+- `PlayerInput`, `GameManager`, `CameraViewSwitcher`, and `ViewHintUI` all reference that same scene reader; no second reader or legacy action-binding reference is introduced.
+- Verified in Unity EditMode: focused input tests `9/9` and the full suite `52/52` passed. Unity MCP's editor-state cache reported stale after the run, so `TestResults.xml` was also checked directly (`52/52`, failed `0`).
+- Manual Play Mode follow-up remains: confirm pause, camera-toggle hint/toggle, movement/actions, and no missing-reference messages using the active scene reader.
+
 ## 2026-07-24 Update
 
 - `CharacterLocomotion` owns stamina, sprint drain/regeneration, dodge timing, and dodge availability; `CharacterMotor` remains responsible for applying the resolved movement and dash velocity.
 - Dodge grants temporary invulnerability through `CharacterState`; combat rejects punch/slide attempts while dodging and ignores hits against an invulnerable target.
 - Ball dribble placement now uses bounded smooth follow and rolling rotation. Shots preserve owner momentum, add force-scaled loft, and receive a short first-touch bonus after possession.
 - `SimpleAIController` predicts free-ball motion, commits to a brief dribble before shooting, defends goal-side when distant, and can dodge an incoming slide.
 - `AbilityCooldownUI` reads `CharacterLocomotion` to render stamina and dodge status alongside the combat cooldowns.
 
 ## 2026-07-19 Update
 
diff --git a/docs/superpowers/plans/2026-07-24-unified-runtime-rebindable-input.md b/docs/superpowers/plans/2026-07-24-unified-runtime-rebindable-input.md
new file mode 100644
index 0000000..2bc2f7f
--- /dev/null
+++ b/docs/superpowers/plans/2026-07-24-unified-runtime-rebindable-input.md
@@ -0,0 +1,274 @@
+# Unified Runtime-Rebindable Input Implementation Plan
+
+> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.
+
+**Goal:** Route all listed controls, including WASD and arrow movement, through Unity Input System actions while preserving gameplay behavior and creating a stable boundary for future runtime rebinding.
+
+**Architecture:** `InputSystem_Actions.inputactions` owns default physical bindings. `GameplayInputReader` owns action-map state and exposes semantic states; player, match, camera, and UI consumers receive only those states. Runtime rebinding services and persistence remain deferred, but action names and effective display strings are provided now.
+
+**Tech Stack:** Unity 6000.5.3f1, Unity Input System, C#, Unity Test Framework, Unity MCP.
+
+## Global Constraints
+
+- Preserve the camera-relative movement calculation and every requested default control value.
+- Modify `.inputactions`, scene, and asset references only through Unity Editor/MCP operations.
+- Do not touch the pre-existing `ProjectSettings/ProjectSettings.asset` change.
+- Keep tests to small input-contract and consumer-routing coverage; manual Play Mode control verification is explicitly required.
+- Do not add runtime rebinding UI, persistence files, conflict policy, or gamepad expansion beyond existing bindings.
+
+---
+
+### Task 1: Define action names and create the input reader
+
+**Files:**
+- Create: `Assets/_Game/Scripts/Runtime/Input/GameplayInputAction.cs`
+- Create: `Assets/_Game/Scripts/Runtime/Input/GameplayInputReader.cs`
+- Create: `Assets/_Game/Scripts/Tests/EditMode/GameplayInputReaderTests.cs`
+
+**Interfaces:**
+- Produces: `GameplayInputAction` enum values `Move`, `Sprint`, `Pass`, `Shot`, `CancelCharge`, `Dodge`, `Punch`, `SlideTackle`, `Pause`, `Restart`, and `ToggleLegacyCamera`.
+- Produces: `GameplayInputReader.ReadButton(GameplayInputAction action)`, `ReadMove()`, and `GetBindingDisplayString(GameplayInputAction action)`.
+- Consumes: a serialized `InputActionAsset` with a `Player` map.
+
+- [ ] **Step 1: Write the failing reader-contract test**
+
+```csharp
+[Test]
+public void BindingDisplayString_UsesTheActionOverrideWhenPresent()
+{
+    InputActionAsset asset = ScriptableObject.CreateInstance<InputActionAsset>();
+    asset.AddActionMap("Player")
+        .AddAction("ToggleLegacyCamera", InputActionType.Button)
+        .AddBinding("<Keyboard>/f5");
+    GameplayInputReader reader = CreateReader(asset);
+    asset.FindAction("ToggleLegacyCamera").ApplyBindingOverride("<Keyboard>/f6");
+
+    Assert.That(reader.GetBindingDisplayString(GameplayInputAction.ToggleLegacyCamera), Is.EqualTo("F6"));
+}
+```
+
+- [ ] **Step 2: Run the focused test and verify RED**
+
+Run: Unity EditMode `GameplayInputReaderTests.BindingDisplayString_UsesTheActionOverrideWhenPresent`.
+
+Expected: compile failure because `GameplayInputReader` and `GameplayInputAction` do not exist.
+
+- [ ] **Step 3: Implement the minimal input boundary**
+
+```csharp
+public enum GameplayInputAction { Move, Sprint, Pass, Shot, CancelCharge, Dodge, Punch, SlideTackle, Pause, Restart, ToggleLegacyCamera }
+
+public ActionButtonState ReadButton(GameplayInputAction action);
+public Vector2 ReadMove();
+public string GetBindingDisplayString(GameplayInputAction action);
+```
+
+Resolve each enum value through one private action-name map, return neutral states for a missing map/action, and enable/disable only the reader's `Player` map.
+
+- [ ] **Step 4: Run the focused test and verify GREEN**
+
+Run: Unity EditMode `GameplayInputReaderTests`.
+
+Expected: PASS, including neutral-state and display-override assertions.
+
+- [ ] **Step 5: Commit the reader boundary**
+
+```powershell
+git add Assets/_Game/Scripts/Runtime/Input/GameplayInputAction.cs Assets/_Game/Scripts/Runtime/Input/GameplayInputReader.cs Assets/_Game/Scripts/Tests/EditMode/GameplayInputReaderTests.cs
+git commit -m "feat: add gameplay input reader"
+```
+
+### Task 2: Move every requested default binding into the Input Action asset
+
+**Files:**
+- Modify through Unity Editor/MCP: `Assets/_Game/Settings/InputSystem_Actions.inputactions`
+- Test: `Assets/_Game/Scripts/Tests/EditMode/GameplayInputReaderTests.cs`
+
+**Interfaces:**
+- Consumes: the `GameplayInputAction` names from Task 1.
+- Produces: the `Player` map actions used by every consumer.
+
+- [ ] **Step 1: Extend the failing binding-contract test**
+
+```csharp
+AssertActionBindings(asset, "Move", "<Keyboard>/w", "<Keyboard>/upArrow", "<Keyboard>/a", "<Keyboard>/leftArrow", "<Keyboard>/s", "<Keyboard>/downArrow", "<Keyboard>/d", "<Keyboard>/rightArrow");
+AssertActionBindings(asset, "Sprint", "<Keyboard>/leftShift", "<Keyboard>/rightShift");
+AssertActionBindings(asset, "Pass", "<Mouse>/leftButton");
+AssertActionBindings(asset, "Shot", "<Mouse>/rightButton");
+AssertActionBindings(asset, "CancelCharge", "<Keyboard>/c");
+AssertActionBindings(asset, "Dodge", "<Keyboard>/l");
+AssertActionBindings(asset, "Punch", "<Keyboard>/j");
+AssertActionBindings(asset, "SlideTackle", "<Keyboard>/k");
+AssertActionBindings(asset, "Pause", "<Keyboard>/escape");
+AssertActionBindings(asset, "Restart", "<Keyboard>/r", "<Keyboard>/space");
+AssertActionBindings(asset, "ToggleLegacyCamera", "<Keyboard>/f5");
+```
+
+- [ ] **Step 2: Run the focused test and verify RED**
+
+Run: Unity EditMode `GameplayInputReaderTests`.
+
+Expected: FAIL because the newly named actions/bindings are absent or `Sprint` lacks right Shift.
+
+- [ ] **Step 3: Update the action asset through Unity Editor/MCP**
+
+In the existing `Player` map, retain and configure `Move` as the current keyboard composite plus arrow alternatives; add the missing right-Shift sprint binding; add `Pass`, `Shot`, `CancelCharge`, `Dodge`, `Punch`, `SlideTackle`, `Pause`, `Restart`, and `ToggleLegacyCamera` with exactly the bindings listed in Step 1. Do not delete the existing generic actions.
+
+- [ ] **Step 4: Run the focused test and verify GREEN**
+
+Run: Unity EditMode `GameplayInputReaderTests`.
+
+Expected: PASS with every default binding present.
+
+- [ ] **Step 5: Commit the input asset contract**
+
+```powershell
+git add Assets/_Game/Settings/InputSystem_Actions.inputactions Assets/_Game/Settings/InputSystem_Actions.inputactions.meta Assets/_Game/Scripts/Tests/EditMode/GameplayInputReaderTests.cs
+git commit -m "feat: define gameplay input actions"
+```
+
+### Task 3: Route player actions through semantic input
+
+**Files:**
+- Modify: `Assets/_Game/Scripts/Runtime/Input/PlayerInput.cs`
+- Delete after replacement: `Assets/_Game/Scripts/Runtime/Input/PlayerActionBindings.cs`
+- Delete after replacement: `Assets/_Game/Scripts/Runtime/Input/PlayerActionInputReader.cs`
+- Delete through Unity Editor/MCP after replacement: `Assets/_Game/Settings/DefaultPlayerActionBindings.asset`
+- Modify: `Assets/_Game/Scripts/Tests/EditMode/PlayerActionInputReaderTests.cs`
+
+**Interfaces:**
+- Consumes: `GameplayInputReader.ReadMove()` and `ReadButton(GameplayInputAction action)` from Task 1.
+- Produces: unchanged calls to `CharacterLocomotion`, `PlayerBallHandler`, and `CombatController`.
+
+- [ ] **Step 1: Replace the legacy test with a failing semantic-routing test**
+
+```csharp
+[Test]
+public void PlayerInput_UsesMoveAndSprintActionsInsteadOfRawKeyboardControls()
+{
+    string source = File.ReadAllText(PlayerInputPath);
+    Assert.That(source, Does.Contain("inputReader.ReadMove()"));
+    Assert.That(source, Does.Not.Contain("Keyboard.current"));
+}
+```
+
+- [ ] **Step 2: Run the focused test and verify RED**
+
+Run: Unity EditMode `PlayerActionInputReaderTests`.
+
+Expected: FAIL because `PlayerInput` still reads keyboard and legacy bindings.
+
+- [ ] **Step 3: Implement minimal semantic routing**
+
+```csharp
+Vector2 moveInput = inputReader.ReadMove();
+bool sprint = inputReader.ReadButton(GameplayInputAction.Sprint).IsPressed;
+ActionButtonState pass = inputReader.ReadButton(GameplayInputAction.Pass);
+```
+
+Use the reader for `Move`, sprint, dodge, punch, slide, pass, shot, and cancel. Preserve the current `GameManager.PlayActive`, stun, charge-release, action-direction, and camera-relative movement logic. Remove raw `Keyboard`/`Mouse` use and the legacy binding asset reader only after the replacement compiles.
+
+- [ ] **Step 4: Run focused input and existing movement/ball tests**
+
+Run: Unity EditMode `PlayerActionInputReaderTests`, `CameraInputDirectionTests`, and `BallInteractionControllerTests`.
+
+Expected: PASS.
+
+- [ ] **Step 5: Commit player migration**
+
+```powershell
+git add Assets/_Game/Scripts/Runtime/Input/PlayerInput.cs Assets/_Game/Scripts/Runtime/Input Assets/_Game/Scripts/Tests/EditMode
+git commit -m "refactor: route player controls through input actions"
+```
+
+### Task 4: Route match, camera, and binding display consumers
+
+**Files:**
+- Modify: `Assets/_Game/Scripts/Runtime/Match/GameManager.cs`
+- Modify: `Assets/_Game/Scripts/Runtime/Camera/CameraViewSwitcher.cs`
+- Modify: `Assets/_Game/Scripts/Runtime/UI/ViewHintUI.cs`
+- Modify: `Assets/_Game/Scripts/Tests/EditMode/MatchResetTests.cs`
+
+**Interfaces:**
+- Consumes: a serialized `GameplayInputReader` reference and its semantic button states.
+- Produces: unchanged pause, restart, camera-toggle, and hint behavior.
+
+- [ ] **Step 1: Write failing consumer-routing checks**
+
+```csharp
+Assert.That(File.ReadAllText(GameManagerPath), Does.Contain("GameplayInputAction.Pause"));
+Assert.That(File.ReadAllText(CameraSwitcherPath), Does.Contain("GameplayInputAction.ToggleLegacyCamera"));
+Assert.That(File.ReadAllText(ViewHintPath), Does.Contain("GetBindingDisplayString"));
+```
+
+- [ ] **Step 2: Run focused tests and verify RED**
+
+Run: Unity EditMode `MatchResetTests` and the routing checks.
+
+Expected: FAIL because each consumer still reads a raw key or embeds `F5`.
+
+- [ ] **Step 3: Implement consumer routing**
+
+```csharp
+if (inputReader.ReadButton(GameplayInputAction.Pause).WasPressed)
+    TogglePause();
+
+if (inputReader.ReadButton(GameplayInputAction.ToggleLegacyCamera).WasPressed)
+    thirdPerson = !thirdPerson;
+```
+
+Keep pause/restart readable outside active gameplay. Replace the visible fixed `F5` text with `inputReader.GetBindingDisplayString(GameplayInputAction.ToggleLegacyCamera)`.
+
+- [ ] **Step 4: Run focused tests and verify GREEN**
+
+Run: Unity EditMode `MatchResetTests`, input routing checks, and `ThirdPersonActionCameraTests`.
+
+Expected: PASS.
+
+- [ ] **Step 5: Commit global-consumer migration**
+
+```powershell
+git add Assets/_Game/Scripts/Runtime/Match/GameManager.cs Assets/_Game/Scripts/Runtime/Camera/CameraViewSwitcher.cs Assets/_Game/Scripts/Runtime/UI/ViewHintUI.cs Assets/_Game/Scripts/Tests/EditMode
+git commit -m "refactor: route global controls through input actions"
+```
+
+### Task 5: Wire the active scene and verify the final boundary
+
+**Files:**
+- Modify through Unity Editor/MCP: `Assets/_Game/Scenes/SampleScene.unity`
+- Modify: `IMPLEMENTATION_STATUS.md`
+
+**Interfaces:**
+- Consumes: the action asset and `GameplayInputReader` from Tasks 1-4.
+- Produces: assigned reader references on PlayerInput, GameManager, CameraViewSwitcher, and ViewHintUI.
+
+- [ ] **Step 1: Inspect before scene mutation**
+
+Use Unity MCP to verify the active `SampleScene`, the Player, GameManager, Main Camera, and UI host components, then confirm the editor is idle.
+
+- [ ] **Step 2: Assign references through Unity Editor/MCP**
+
+Add `GameplayInputReader` to the selected scene input host, assign `InputSystem_Actions.inputactions`, and set the same reader reference on PlayerInput, GameManager, CameraViewSwitcher, and ViewHintUI. Remove the obsolete PlayerActionBindings reference only after all references resolve.
+
+- [ ] **Step 3: Wait for compilation and check the console**
+
+Poll `mcpforunity://editor/state` until compilation and domain reload complete, then query Unity console errors and warnings.
+
+Expected: no compile errors.
+
+- [ ] **Step 4: Run concise automated verification**
+
+Run: focused input tests, then the full Unity EditMode suite.
+
+Expected: all discovered EditMode tests pass.
+
+- [ ] **Step 5: Review and document**
+
+Run `git diff --check`, inspect the changed-file list, and update `IMPLEMENTATION_STATUS.md` with the action-asset/reader boundary and test result. Post the actual file scope, verification, manual Play Mode checklist, and risks to issue #1, with coordinated notes for issues #2, #3, #4, #5, and #7.
+
+- [ ] **Step 6: Commit the scene wiring and status**
+
+```powershell
+git add Assets/_Game/Scenes/SampleScene.unity IMPLEMENTATION_STATUS.md
+git commit -m "feat: wire unified gameplay input"
+```
diff --git a/docs/superpowers/specs/2026-07-24-unified-runtime-rebindable-input-design.md b/docs/superpowers/specs/2026-07-24-unified-runtime-rebindable-input-design.md
new file mode 100644
index 0000000..8d64fb3
--- /dev/null
+++ b/docs/superpowers/specs/2026-07-24-unified-runtime-rebindable-input-design.md
@@ -0,0 +1,77 @@
+# Unified Runtime-Rebindable Input Design
+
+Date: 2026-07-24
+
+## Goal
+
+Make every listed gameplay control configurable through Unity Input System actions, including WASD and arrow-key movement. Keep camera-relative movement behavior unchanged. Prepare the input boundary so a future settings screen can let a player rebind controls during play and preserve those choices across launches.
+
+## Scope
+
+The affected default controls are:
+
+- `Move`: W/A/S/D and arrow keys, camera-relative movement.
+- `Sprint`: left and right Shift; the existing possession dribble-touch behavior remains tied to the sprint action.
+- `Pass`: left mouse button.
+- `Shot`: right mouse button.
+- `CancelCharge`: C.
+- `Dodge`: L.
+- `Punch`: J.
+- `SlideTackle`: K.
+- `Pause`: Escape.
+- `Restart`: R and Space.
+- `ToggleLegacyCamera`: F5.
+
+Mouse look stays outside this change. `MouseLookInput` continues to read pointer delta because it is not one of the requested rebindable controls.
+
+## Architecture
+
+`Assets/_Game/Settings/InputSystem_Actions.inputactions` is the authoritative source of default bindings. The existing `Player` action map retains `Move` and `Sprint`; it gains explicit, gameplay-named actions for the remaining controls. Existing unused generic actions are not removed in this change.
+
+`Assets/_Game/Scripts/Runtime/Input/GameplayInputReader.cs` owns the enabled action map and exposes typed, key-name-free action states. It is assigned the action asset through a serialized reference and is placed on the scene's input host. It does not invoke gameplay behavior itself.
+
+Gameplay consumers keep their current responsibilities:
+
+- `PlayerInput` reads `Move`, `Sprint`, ball-action, dodge, and combat states from `GameplayInputReader`, then routes them to locomotion, ball, and combat APIs.
+- `GameManager` reads `Pause` and `Restart` from the reader.
+- `CameraViewSwitcher` reads `ToggleLegacyCamera` from the reader.
+- `ViewHintUI` queries the reader for the effective display binding of the camera-toggle action instead of embedding `F5` in its text.
+
+No consumer checks `Keyboard.current`, `Mouse.current`, or a key name for these actions after migration. Input dependencies point outward from `Input`; gameplay folders do not depend on each other to interpret controls.
+
+## Runtime Rebinding Preparation
+
+The default action asset ships in every build and is never mutated at runtime. Future rebinding will use `InputAction` binding overrides:
+
+1. A settings UI asks an `InputRebindService` in `Input/` to perform interactive rebinding for an action binding.
+2. The service applies the override to the active action asset copy.
+3. `InputBindingOverridesStore` serializes `SaveBindingOverridesAsJson()` data to an application persistent-data file.
+4. Startup loads that JSON through `LoadBindingOverridesFromJson()` after the default asset is enabled.
+5. Reset removes the overrides and restores the default asset bindings.
+
+Those two services and the settings UI are explicitly deferred. The initial implementation exposes stable action names and effective binding-display lookup so they can be added without rewriting gameplay consumers.
+
+## Scene and Asset Safety
+
+The `.inputactions` asset and SampleScene references are changed only through Unity Editor/MCP-safe operations. The existing `PlayerActionBindings` asset and its reader are removed only after the action asset, reader, scene references, and tests fully replace them. No `.unity`, `.prefab`, `.asset`, or `.inputactions` YAML is edited directly.
+
+The pre-existing `ProjectSettings/ProjectSettings.asset` modification is out of scope and must remain untouched.
+
+## Error and State Rules
+
+- A missing or disabled input reader produces neutral action states rather than throwing.
+- `Pause` and `Restart` remain readable even when gameplay movement is disabled by kickoff, stun, pause, or game-over state.
+- A charging pass or shot preserves the existing mutually-exclusive charge and cancellation behavior.
+- Multiple bindings for one action remain alternatives: either Shift sprints and either R or Space restarts.
+
+## Tests and Verification
+
+Tests are written first and prove:
+
+1. The action asset contains each required action with the requested default bindings, including both WASD/arrow movement and both Shift keys.
+2. `GameplayInputReader` produces action states from Input System test devices and supports alternative bindings.
+3. Player, match, and camera consumers route semantic input states without direct raw keyboard reads.
+4. The effective binding label changes when a binding override is applied.
+5. Existing camera-relative movement, charge cancellation, pause/restart, and camera-toggle behavior remain covered.
+
+After implementation, Unity compilation, focused EditMode tests, the full EditMode suite, console review, scene reference inspection, and diff review are required. Manual Play Mode confirmation of each listed control and the visible binding hint remains a separate check.
