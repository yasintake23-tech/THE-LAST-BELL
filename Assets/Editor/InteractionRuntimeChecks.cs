using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TheLastBell.Cinematics;
using TheLastBell.Gameplay;
using TheLastBell.Interaction;
using TheLastBell.Player;
using TheLastBell.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TheLastBell.Editor
{
    // Separate Play Mode pass. The Android build's existing Edit Mode suite remains available.
    [InitializeOnLoad]
    public static class InteractionRuntimeChecks
    {
        private const string Pending = "TheLastBell.InteractionRuntimeChecks.Pending";
        private const string Level = "Assets/Scenes/AbandonedSchool.unity";

        static InteractionRuntimeChecks() => EditorApplication.playModeStateChanged += OnPlayModeChange;

        [MenuItem("The Last Bell/Tests/Mobile interaction Play Mode")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            MobileSetupHelper.ApplyMobileFixes();
            ProtagonistAnimationSetup.SetupAll();
            Chapter1EnvironmentBuilder.SetupChapter1();
            EditorSceneManager.OpenScene(Level);
            SessionState.SetBool(Pending, true);
            EditorApplication.EnterPlaymode();
        }

        private static void OnPlayModeChange(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Pending, false)) return;
            SessionState.SetBool(Pending, false);
            var player = UnityEngine.Object.FindAnyObjectByType<FirstPersonController>(FindObjectsInactive.Include);
            if (player == null)
            {
                Debug.LogError("[InteractionRuntimeChecks] Player is missing.");
                EditorApplication.ExitPlaymode();
                return;
            }
            player.StartCoroutine(Report(Scenarios(player)));
        }

        private static IEnumerator Scenarios(FirstPersonController player)
        {
            // Enter the chapter normally. Scene reloads isolate the two pickup postures and approach directions.
            yield return CompleteElectricalPuzzle(player, crouching: false, side: -1f);
            SceneManager.sceneLoaded += StartCrouchingScenario;
            SceneManager.LoadSceneAsync(SceneManager.GetActiveScene().name);
        }

        private static void StartCrouchingScenario(Scene scene, LoadSceneMode mode)
        {
            SceneManager.sceneLoaded -= StartCrouchingScenario;
            var player = UnityEngine.Object.FindAnyObjectByType<FirstPersonController>(FindObjectsInactive.Include);
            if (player == null)
            {
                Debug.LogError("[InteractionRuntimeChecks] Player missing after scene reload.");
                EditorApplication.ExitPlaymode();
                return;
            }
            player.StartCoroutine(Report(CompleteElectricalPuzzle(player, crouching: true, side: 1f), enterSecondScene: true));
        }

        private static IEnumerator CompleteElectricalPuzzle(FirstPersonController player, bool crouching, float side)
        {
            var intro = UnityEngine.Object.FindAnyObjectByType<SeamlessIntroManager>(FindObjectsInactive.Include);
            yield return Until(() => player.isActiveAndEnabled && (intro == null || intro.IsIntroFinished), 30f, "Intro");
            var input = player.GetComponent<PlayerInteractor>();
            var inventory = player.GetComponent<PlayerInventory>();
            var entrance = GameObject.Find("SchoolEntrance_Doorway").GetComponent<DoorInteractable>();
            var classroom = GameObject.Find("ClassroomDoor_C101").GetComponent<DoorInteractable>();
            var lever = UnityEngine.Object.FindAnyObjectByType<ElectricalPanelInteractable>();
            var item = UnityEngine.Object.FindAnyObjectByType<FestekItem>();
            var facility = UnityEngine.Object.FindAnyObjectByType<FacilityPowerManager>();
            var socket = UnityEngine.Object.FindObjectsByType<FestekSocketInteractable>(FindObjectsInactive.Include).Single(s => !s.IsElevatorSocket);

            PlaceFixture(player, new Vector3(0f, 0.04f, -1.6f), Vector3.forward);
            yield return TapWhenOffered(input, entrance);
            yield return Until(() => entrance.IsOpen && player.AllowMovement, 9f, "Entrance opening");

            PlaceFixture(player, new Vector3(0f, 0.04f, 2.2f), Vector3.forward);
            yield return null;
            PlaceFixture(player, new Vector3(0f, 0.04f, 4.4f), Vector3.forward);
            yield return null;
            PlaceFixture(player, classroom.transform.TransformPoint(new Vector3(0f, 0.035f, -1.28f)), classroom.transform.forward);
            yield return TapWhenOffered(input, classroom);
            yield return Until(() => classroom.IsOpen && player.AllowMovement, 9f, "Classroom opening");

            PoseNear(player, lever.LeverInteractionTarget, 0f);
            yield return TapWhenOffered(input, lever);
            yield return Until(() => item.HasLanded && player.AllowMovement, 10f, "Lever and physical release");

            var grip = item.GripPoint != null ? item.GripPoint.position : item.transform.position;
            Vector3 mark = grip + new Vector3(side * 0.48f, 0f, -0.56f);
            mark.y = 0.035f;
            PlaceFixture(player, mark, Vector3.ProjectOnPlane(grip - mark, Vector3.up));
            player.SetCrouch(crouching);
            yield return null;
            if (crouching)
            {
                yield return TapWhenOffered(input, item);
                item.enabled = false;
                yield return Until(() => player.AllowMovement && input.enabled, 2f, "Interrupted pickup input recovery");
                Require(inventory.PhysicalItems.Count == 0, "Interrupted pickup incorrectly granted the item.");
                item.enabled = true;
            }
            yield return TapWhenOffered(input, item, repeat: true);

            float previousYaw = player.transform.eulerAngles.y;
            float totalTurn = 0f;
            float elapsed = 0f;
            while (item.CurrentState != FestekItemState.HeldInLeftHand && elapsed < 8f)
            {
                player.MobileLookInput = new Vector2(30f, -20f);
                yield return null;
                float yaw = player.transform.eulerAngles.y;
                totalTurn += Mathf.Abs(Mathf.DeltaAngle(previousYaw, yaw));
                previousYaw = yaw;
                elapsed += Time.deltaTime;
            }
            Require(item.CurrentState == FestekItemState.HeldInLeftHand, "Pickup did not complete.");
            Require(totalTurn <= 188f, $"Excessive rotation during pickup: {totalTurn:F1}°");
            Require(inventory.PhysicalItems.Count == 1 && inventory.IsEquipped(item), "Duplicate or missing quest item.");
            yield return Until(() => player.AllowMovement, 2f, "Pickup input recovery");

            PoseNear(player, socket.GetComponent<InteractionTarget>(), 0f);
            yield return TapWhenOffered(input, socket);
            yield return Until(() => UnityEngine.Object.FindAnyObjectByType<FestekRotationControl>() != null, 9f, "Socket insertion");
            Require(!facility.IsPowerRestored, "Power restored before the player rotated the device.");
            TurnMobileDial(UnityEngine.Object.FindAnyObjectByType<FestekRotationControl>());
            yield return Until(() => facility.IsPowerRestored && player.AllowMovement, 9f, "Facility power restoration");
            yield return TapWhenOffered(input, socket);
            yield return Until(() => !socket.SeatedFestek && inventory.IsEquipped(item) && player.AllowMovement, 9f, "Powered withdrawal");
            Require(inventory.PhysicalItems.Count == 1, "Withdrawal created a second item.");
            var pocketButton = GameObject.Find("Inventory_Open")?.GetComponent<Button>();
            Require(pocketButton != null && pocketButton.IsInteractable(), "Inventory button missing.");
            Click(pocketButton.gameObject);
            yield return Until(() => item.CurrentState == FestekItemState.Stored && player.AllowMovement, 5f, "Pocket storage");
            Require(ChapterObjectiveManager.Instance.IsMilestoneComplete, "Television objective remained locked.");
            Debug.Log($"[InteractionRuntimeChecks] Electrical chain passed. Crouch={crouching}, pickup turn={totalTurn:F1}°.");
        }

        private static IEnumerator TapWhenOffered(PlayerInteractor input, IInteractable target, bool repeat = false)
        {
            yield return Until(() => input.CurrentInteractable == target &&
                GameObject.Find("Mobile_ActionButton")?.GetComponent<Button>()?.IsInteractable() == true, 3f,
                $"Mobile action for {(target as Component)?.name}");
            var button = GameObject.Find("Mobile_ActionButton");
            Click(button);
            if (repeat) Click(button);
        }

        private static void Click(GameObject button)
        {
            var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
            ExecuteEvents.Execute(button, pointer, ExecuteEvents.pointerClickHandler);
        }

        private static void TurnMobileDial(FestekRotationControl dial)
        {
            var rect = (RectTransform)dial.transform;
            var data = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left, pointerId = 92 };
            Vector2 Point(float angle) => RectTransformUtility.WorldToScreenPoint(null,
                rect.TransformPoint(new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad) * 67f,
                    18f + Mathf.Sin(angle * Mathf.Deg2Rad) * 67f, 0f)));
            data.position = Point(95f);
            ExecuteEvents.Execute(dial.gameObject, data, ExecuteEvents.pointerDownHandler);
            foreach (float angle in new[] { 65f, 35f, 5f, -10f })
            {
                data.position = Point(angle);
                ExecuteEvents.Execute(dial.gameObject, data, ExecuteEvents.dragHandler);
            }
            ExecuteEvents.Execute(dial.gameObject, data, ExecuteEvents.pointerUpHandler);
        }

        private static IEnumerator Until(Func<bool> ready, float seconds, string action)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            while (!ready() && Time.realtimeSinceStartup < deadline) yield return null;
            Require(ready(), action + " did not complete.");
        }

        private static void PoseNear(FirstPersonController player, InteractionTarget target, float side)
        {
            Vector3 mark = target.HandTarget.position +
                           target.transform.forward * 1.25f + target.transform.right * side;
            mark.y = 0.035f;
            PlaceFixture(player, mark, target.HandTarget.position - mark);
        }

        private static void PlaceFixture(FirstPersonController player, Vector3 position, Vector3 heading)
        {
            var controller = player.GetComponent<CharacterController>();
            controller.enabled = false;
            player.transform.SetPositionAndRotation(position,
                Quaternion.LookRotation(Vector3.ProjectOnPlane(heading, Vector3.up)));
            controller.enabled = true;
            Physics.SyncTransforms();
        }

        private static IEnumerator Report(IEnumerator scenario, bool enterSecondScene = false)
        {
            var stack = new Stack<IEnumerator>();
            stack.Push(scenario);
            while (stack.Count > 0)
            {
                object result = null;
                try
                {
                    var run = stack.Peek();
                    if (run.MoveNext()) result = run.Current;
                    else { stack.Pop(); continue; }
                }
                catch (Exception error)
                {
                    Debug.LogError("[InteractionRuntimeChecks] " + error);
                    EditorApplication.ExitPlaymode();
                    break;
                }
                if (result is IEnumerator nested) stack.Push(nested);
                else yield return result;
            }
            if (enterSecondScene) EditorApplication.ExitPlaymode();
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
