Base: b5e53e4
Head: 2e25217

2e25217 fix: wire net player input reader

 Assets/_Game/Prefabs/NetPlayer.prefab | 15 +++++++++++++++
 1 file changed, 15 insertions(+)

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
