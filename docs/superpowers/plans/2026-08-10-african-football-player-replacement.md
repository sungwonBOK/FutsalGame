# African Football Player Replacement Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Use the supplied African football player as the visual character in local and networked matches without changing gameplay behaviour.

**Architecture:** The gameplay roots remain intact. A Humanoid FBX becomes the new nested visual model, receives the existing Animator Controller, and relies on Unity Humanoid retargeting for the existing action clips. One asset folder contains the source model, textures, and generated materials.

**Tech Stack:** Unity 6000.5.3f1, Unity MCP, Humanoid Avatar, Unity Animator, existing Netcode prefab.

## Global Constraints

- Work in `D:\Unity Projects\FutsalGame` and preserve unrelated changes.
- Do not edit `.unity`, `.prefab`, or `.asset` YAML directly.
- Touch no gameplay scripts, input, physics, networking code, Animator Controller, or animation clips.
- Stop and report if the FBX cannot yield a valid Humanoid avatar or an import error repeats three times.

---

### Task 1: Import source visual assets

**Files:**
- Create: `Assets/_Game/Characters/AfricanFootballPlayer/AfricanFootballPlayer.fbx`
- Create: `Assets/_Game/Characters/AfricanFootballPlayer/Textures/*`
- Create: `Assets/_Game/Characters/AfricanFootballPlayer/Materials/*`

**Interfaces:**
- Consumes: `C:/Users/sungw/Downloads/African+Football+Soccer+Player+Male+RIG.fbx`, `TEXTURES/`, and `SHIRT TEXTURE - 15/`.
- Produces: A valid Humanoid avatar and materials usable by a SkinnedMeshRenderer.

- [x] **Step 1: Confirm the Unity Editor is idle and the current console has no errors.**
- [x] **Step 2: Import the FBX as `Humanoid` to `Assets/_Game/Characters/AfricanFootballPlayer`.**
- [x] **Step 3: Import base-color texture maps; use `7.jpg` as the shirt albedo.**
- [x] **Step 4: Inspect the imported model and generated materials, then check the Unity console.**

### Task 2: Replace the visual children

**Files:**
- Modify through Unity Editor: `Assets/_Game/Scenes/SampleScene.unity`
- Modify through Unity Editor: `Assets/_Game/Prefabs/NetPlayer.prefab`

**Interfaces:**
- Consumes: the Humanoid model/materials from Task 1 and `Assets/_Game/Animation/FutsalCharacter.controller`.
- Produces: Scene Player and NetPlayer visuals that preserve their root gameplay components and Animator Controller.

- [x] **Step 1: Inspect the current Player and NetPlayer model roots, including the existing Animator Controller reference.**
- [x] **Step 2: Replace only the old visual mesh hierarchy with the imported model; keep each gameplay root and Animator Controller.**
- [x] **Step 3: Adjust only the nested visual transform when required to place the feet at the existing root.**
- [x] **Step 4: Save the prefab and scene through Unity MCP, then inspect their hierarchy and component references.**

### Task 3: Verify the asset replacement

**Files:**
- Verify: `Assets/_Game/Scenes/SampleScene.unity`
- Verify: `Assets/_Game/Prefabs/NetPlayer.prefab`

**Interfaces:**
- Consumes: the completed assets from Tasks 1 and 2.
- Produces: editor-level evidence that the import is clean and both character entry points retain their gameplay boundaries.

- [x] **Step 1: Wait for asset import and compilation to finish, then read Unity console errors and warnings.**
- [x] **Step 2: Capture an editor screenshot of the local Player and inspect the NetPlayer prefab hierarchy.**
- [x] **Step 3: Review the Git diff and list the remaining manual Play Mode / two-client checks.**
