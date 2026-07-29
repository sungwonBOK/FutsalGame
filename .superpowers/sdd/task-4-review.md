Base: 2e25217
Head: ae3fad5

ae3fad5 refactor: route global controls through input actions

 .../Scripts/Runtime/Camera/CameraViewSwitcher.cs   |  8 ++---
 Assets/_Game/Scripts/Runtime/Match/GameManager.cs  | 18 +++++------
 Assets/_Game/Scripts/Runtime/UI/ViewHintUI.cs      |  8 +++--
 .../Scripts/Tests/EditMode/MatchResetTests.cs      | 37 ++++++++++++++++++++++
 4 files changed, 56 insertions(+), 15 deletions(-)

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
