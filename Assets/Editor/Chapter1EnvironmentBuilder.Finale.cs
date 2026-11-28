using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using TheLastBell.Cinematics;
using TheLastBell.Gameplay;
using TheLastBell.Interaction;
using TheLastBell.Player;
using UnityEditor;
using UnityEngine;
using UnityEngine.Video;

namespace TheLastBell.Editor
{
    public static partial class Chapter1EnvironmentBuilder
    {
        private const string ElevatorAssets = "Assets/Environment/Original/Elevator/";

        private static void WireChapterFinale(Dictionary<string, Material> mats, Transform root,
            AbandonedTelevisionController crt, ChapterObjectiveManager objectives, ChapterDialogue voices, Transform actor)
        {
            const string videoFile = "Chapter1_Television.mp4";
            var screenShader = AssetDatabase.LoadAssetAtPath<Shader>(ElevatorAssets + "CRTScreening.shader");
            var eyeShader = AssetDatabase.LoadAssetAtPath<Shader>(ElevatorAssets + "Eyelids.shader");
            if (!File.Exists(Path.Combine(Application.streamingAssetsPath, videoFile)) || screenShader == null || eyeShader == null || actor == null)
                throw new InvalidOperationException("Chapter I finale is missing the TV clip, shader or player reference.");

            var screen = crt.ScreenRenderer;
            var tv = crt.gameObject;
            var hit = tv.AddComponent<BoxCollider>();
            var bounds = BoundsIn(tv.transform);
            hit.center = bounds.center;
            hit.size = bounds.size + Vector3.one * 0.035f;
            var watch = CreateGroup("CRT_Viewing_Position", tv.transform);
            var focus = CreateGroup("CRT_Visible_Glass_Center", tv.transform);
            focus.transform.position = screen.bounds.center;
            Vector3 screenSide = tv.transform.InverseTransformPoint(focus.transform.position) - bounds.center;
            Vector3 viewingDirection = Vector3.Dot(screenSide, Vector3.forward) >= 0f ? tv.transform.forward : -tv.transform.forward;
            float ratio = 1.31f;
            if (screen.TryGetComponent<MeshFilter>(out var glass) && glass.sharedMesh != null)
            {
                var localScreen = new Bounds();
                bool first = true;
                foreach (var point in glass.sharedMesh.vertices)
                {
                    Vector3 v = tv.transform.InverseTransformPoint(screen.transform.TransformPoint(point));
                    if (first) { localScreen = new Bounds(v, Vector3.zero); first = false; }
                    else localScreen.Encapsulate(v);
                }
                if (!first && localScreen.size.y > 0.01f)
                    ratio = Mathf.Clamp(localScreen.size.x / localScreen.size.y, 1.10f, 1.6f);
            }
            watch.transform.position = screen.bounds.center + viewingDirection * 1.08f;
            watch.transform.position = new Vector3(watch.transform.position.x, 0.025f, watch.transform.position.z);
            var videoSource = CreateAudioEmitter(tv.transform, "CRT_Video_Speaker", Vector3.zero, 1.05f, 9f, "Dialogue");
            videoSource.priority = 34;
            var video = videoSource.gameObject.AddComponent<VideoPlayer>();
            video.source = VideoSource.Url;
            video.renderMode = VideoRenderMode.RenderTexture;
            video.aspectRatio = VideoAspectRatio.Stretch;
            video.playOnAwake = false;
            video.isLooping = false;
            video.waitForFirstFrame = true;
            video.skipOnDrop = true;
            video.audioOutputMode = VideoAudioOutputMode.AudioSource;
            video.controlledAudioTrackCount = 1;
            video.SetTargetAudioSource(0, videoSource);
            // The player owns the video speaker; the TV wrapper owns the interactable and its collider.
            var screening = tv.AddComponent<TelevisionScreening>();
            screening.Configure(crt, objectives, video, screenShader, focus.transform, ratio, videoFile);
            var watching = tv.AddComponent<InteractionTarget>();
            watching.Configure(focus.transform, PreferredInteractionHand.Either, "", 1.2f, 0f, 1f);
            watching.ConfigureApproach(watch.transform, null);
            watching.ConfigurePhysicalContact(2.4f, 1.05f, HandGripProfile.Handle);

            foreach (string name in new[] { "Elevator_Door_L", "Elevator_Door_R", "Elevator_Frame_Top", "Elevator_Frame_L",
                "Elevator_Frame_R", "Elevator_Barricade_Barrel", "Shaft_Floor", "Shaft_Ceiling" })
                GameObject.Find(name)?.SetActive(false);

            var threshold = CreateGroup("Elevator_Cab_Threshold", root).transform;
            threshold.position = new Vector3(0f, 0.015f, 26.10f);
            var cab = InstantiateFbx(ElevatorAssets + "Elevator_Cab.fbx", "Elevator_Rolled_Steel_Cabin",
                threshold, Vector3.zero, Quaternion.identity, Vector3.one, isStatic: true);
            var backplate = FindDeep(cab.transform, "Steel_Control_Backplate");
            if (backplate == null) throw new InvalidOperationException("Elevator control-panel marker missing.");
            if (threshold.InverseTransformPoint(backplate.position).z < 0f)
                cab.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            ApplyCabMaterials(cab, mats);
            CabinCollider(threshold, "Cab_Floor_Collision", new Vector3(0f, -0.049f, 1.25f), new Vector3(2.31f, 0.098f, 2.66f));
            CabinCollider(threshold, "Cab_Left_Wall_Collision", new Vector3(-1.165f, 1.23f, 1.26f), new Vector3(0.07f, 2.47f, 2.61f));
            CabinCollider(threshold, "Cab_Right_Wall_Collision", new Vector3(1.165f, 1.23f, 1.26f), new Vector3(0.07f, 2.47f, 2.61f));
            CabinCollider(threshold, "Cab_Back_Wall_Collision", new Vector3(0f, 1.23f, 2.48f), new Vector3(2.31f, 2.47f, 0.07f));
            CabinCollider(threshold, "Cab_Lintel_Collision", new Vector3(0f, 2.42f, 0.07f), new Vector3(2.31f, 0.16f, 0.19f));
            var blocker = CabinCollider(threshold, "Elevator_Progression_Blocker", new Vector3(0f, 1.16f, -0.063f),
                new Vector3(2.34f, 2.32f, 0.12f));

            var left = InstantiateFbx(ElevatorAssets + "Elevator_Leaf.fbx", "Elevator_Sliding_Left", threshold,
                new Vector3(-0.525f, 1.18f, -0.069f), Quaternion.identity, Vector3.one, false);
            var right = InstantiateFbx(ElevatorAssets + "Elevator_Leaf.fbx", "Elevator_Sliding_Right", threshold,
                new Vector3(0.525f, 1.18f, -0.069f), Quaternion.identity, Vector3.one, false);
            ApplyCabMaterials(left, mats);
            ApplyCabMaterials(right, mats);
            // Fixed jambs conceal the leaf pockets while retaining the full two-metre opening.
            for (int side = -1; side <= 1; side += 2)
            {
                BuildDetail("Lift_Rolled_Face_Trim", threshold, new Vector3(side * 1.23f, 1.24f, -0.165f),
                    new Vector3(0.43f, 2.48f, 0.085f), mats["RustedMetal"], true);
                BuildDetail("Lift_Seal", threshold, new Vector3(side * 1.02f, 1.19f, -0.161f),
                    new Vector3(0.035f, 2.36f, 0.063f), mats["PanelDark"]);
            }
            CreateFinaleLight(threshold, "Cab_Practical_Fluorescent", new Vector3(0f, 2.24f, 1.4f),
                new Color(0.71f, 0.78f, 0.73f), 1.23f, 3.6f);

            Vector3 panel = threshold.InverseTransformPoint(backplate.position);
            var socket = InstantiateFbx("Assets/Environment/Original/Electrical/FestekSocket_Unit.fbx",
                "Lift_ServiceSocket", threshold, new Vector3(panel.x, 1.27f, panel.z - 0.19f),
                Quaternion.Euler(0f, 180f, 0f), new Vector3(0.69f, 0.69f, 0.69f), false);
            ApplyLiftSocketMaterials(socket, mats);
            var socketCollider = socket.AddComponent<BoxCollider>();
            var socketBounds = BoundsIn(socket.transform);
            socketCollider.center = socketBounds.center;
            socketCollider.size = socketBounds.size + Vector3.one * 0.028f;
            var raw = FindDeep(socket.transform, "Insertion_Transform");
            var insert = CreateGroup("Lift_Insertion_Unity_Anchor", threshold).transform;
            insert.SetPositionAndRotation(raw != null ? raw.position : socket.transform.position + socket.transform.forward * 0.10f,
                Quaternion.LookRotation(threshold.forward, Vector3.up));
            var socketAudio = CreateAudioEmitter(socket.transform, "Lift_Socket_Foley", Vector3.zero, 0.8f, 7.5f, "Interactions");
            var liftSocket = socket.AddComponent<FestekSocketInteractable>();
            liftSocket.Configure(insert, FindDeep(socket.transform, "Socket_StatusLens")?.GetComponent<Renderer>(), null,
                socketAudio, FinaleAudio("SFX_Festek_SocketInsert"), FinaleAudio("SFX_Festek_ButtonClick"));
            liftSocket.ConfigureElevatorSocket();

            var gas = BuildElevatorGas(threshold);
            var room = BuildAwakeningArea(mats, root, out Transform dragStart, out Transform awakening);
            var cabin = CreateAudioEmitter(threshold, "Lift_Cabin_Audio", new Vector3(0f, 1.35f, 1.28f), 1.7f, 9f, "Environment");
            var body = CreateAudioEmitter(actor, "Finale_Body_Audio", new Vector3(0f, 0.8f, 0f), 1.0f, 7f, "Interactions");
            var music = CreateAudioEmitter(root, "Finale_Original_Score", Vector3.zero, 1f, 1f, "Music");
            music.spatialBlend = 0f;

            var director = root.gameObject.AddComponent<ChapterFinale>();
            director.Configure(screening, objectives, voices, new[]
            {
                LoadDialogue("TV_Reaction", new SubtitleSegment(0.02f, 2.62f, "Bu ne lan böyle! İlk kez görüyorum bunu.")),
                LoadDialogue("Corridor_Return",
                    new SubtitleSegment(0.02f, 1.72f, "Pekâlâ... Pekâlâ..."),
                    new SubtitleSegment(1.98f, 2.99f, "Sakin olmalıyım."),
                    new SubtitleSegment(2.99f, 4.81f, "Etrafa bakayım, bir şeyler bulmam gerek."),
                    new SubtitleSegment(4.81f, 7.91f, "Ve... Ne zamandan beri burada asansör var?")),
                LoadDialogue("Elevator_Approach", new SubtitleSegment(0.02f, 2.68f, "Şu asansörün kapısı açılmış. Biraz içine baksam mı?")),
                LoadDialogue("Elevator_Inside", new SubtitleSegment(0.02f, 3.19f, "Şu Feştek zımbırtısını bir denemekten zarar gelmez sanırım..."))
            }, actor.GetComponent<FirstPersonController>(), liftSocket, threshold, left.transform, right.transform, blocker, gas,
                dragStart, awakening, room, eyeShader, HumanoidMotion("Fall_Forward_V2"), HumanoidMotion("Get_Up_V2"), cabin, body, music, new[]
                {
                    FinaleAudio("Finale/Elevator_Slam"), FinaleAudio("Finale/Vent_Hiss"), FinaleAudio("Finale/Distressed_Breath_Foley"),
                    FinaleAudio("SFX_Festek_MetalDropImpact"), FinaleAudio("Finale/Collapse_Impact"), FinaleAudio("Finale/Shaft_Descent"),
                    FinaleAudio("Finale/Original_Descent_Score"), FinaleAudio("Finale/Dragging_Floor"), FinaleAudio("Amb_Interior_RoomTone")
                });
            if (!director.AssetsReady)
                throw new InvalidOperationException("Chapter I finale media, animation or geometry references are incomplete.");
        }

        private static AnimationClip HumanoidMotion(string name) =>
            AssetDatabase.LoadAllAssetsAtPath($"Assets/Animations/Locomotion/{name}.fbx").OfType<AnimationClip>()
                .FirstOrDefault(clip => !clip.name.StartsWith("__preview__", StringComparison.Ordinal));

        private static AudioClip FinaleAudio(string name) =>
            AssetDatabase.LoadAssetAtPath<AudioClip>($"Assets/Audio/Chapter1/{name}.wav");

        private static BoxCollider CabinCollider(Transform parent, string name, Vector3 position, Vector3 size)
        {
            var root = CreateGroup(name, parent);
            root.transform.localPosition = position;
            var collider = root.AddComponent<BoxCollider>();
            collider.size = size;
            return collider;
        }

        private static Light CreateFinaleLight(Transform parent, string name, Vector3 position, Color color, float intensity, float range)
        {
            var root = CreateGroup(name, parent);
            root.transform.localPosition = position;
            var light = root.AddComponent<Light>();
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.shadows = LightShadows.None;
            return light;
        }

        private static void ApplyCabMaterials(GameObject model, Dictionary<string, Material> mats)
        {
            foreach (var renderer in model.GetComponentsInChildren<MeshRenderer>(true))
            {
                string name = renderer.name;
                renderer.sharedMaterial = name.Contains("Diffuser") ? mats["FluorescentTubeGlass"] :
                    name.Contains("Floor") ? mats["FloorTiles"] :
                    name.Contains("Vent") || name.Contains("Recess") || name.Contains("Track") ? mats["PanelDark"] :
                    name.Contains("Trim") || name.Contains("Bolt") ? mats["RustedMetal"] :
                    name.Contains("Amber") ? mats["PanelCopper"] : mats["PanelSteel"];
            }
        }

        private static void ApplyLiftSocketMaterials(GameObject model, Dictionary<string, Material> mats)
        {
            foreach (var renderer in model.GetComponentsInChildren<MeshRenderer>(true))
            {
                string name = renderer.name;
                renderer.sharedMaterial = name.Contains("StatusLens") ? mats["FluorescentTubeGlass"] :
                    name.Contains("Bore") ? mats["PanelDark"] : name.Contains("Collar") || name.Contains("Bezel")
                    ? mats["PanelCopper"] : mats["PanelSteel"];
            }
        }

        private static ParticleSystem BuildElevatorGas(Transform parent)
        {
            var root = CreateGroup("Vent_Gas", parent);
            root.transform.localPosition = new Vector3(0.70f, 1.95f, 2.407f);
            root.transform.localRotation = Quaternion.LookRotation(new Vector3(-0.29f, -0.15f, -1f), Vector3.up);
            var gas = root.AddComponent<ParticleSystem>();
            gas.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = gas.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 4.3f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.4f, 2.8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.36f, 0.94f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.46f, 1.16f);
            main.startRotation = new ParticleSystem.MinMaxCurve(-Mathf.PI, Mathf.PI);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.105f, 0.115f, 0.109f, 0.65f),
                new Color(0.04f, 0.043f, 0.038f, 0.91f));
            main.maxParticles = 86;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = gas.emission;
            emission.rateOverTime = 34f;
            var shape = gas.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.radius = 0.17f;
            shape.angle = 18f;
            var noise = gas.noise;
            noise.enabled = true;
            noise.strength = 0.29f;
            noise.frequency = 0.52f;
            noise.scrollSpeed = 0.29f;
            noise.quality = ParticleSystemNoiseQuality.Low;
            var size = gas.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.28f, 1f, 1.64f));
            var color = gas.colorOverLifetime;
            color.enabled = true;
            var life = new Gradient();
            life.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.88f, 0.24f),
                    new GradientAlphaKey(0.8f, 0.68f), new GradientAlphaKey(0f, 1f) });
            color.color = new ParticleSystem.MinMaxGradient(life);
            const string matPath = MAT_DIR + "/Elevator_Smoke_Particle.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(ElevatorAssets + "ElevatorGas.shader");
            if (shader == null) throw new InvalidOperationException("Elevator gas shader is missing.");
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, matPath);
            }
            mat.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(ElevatorAssets + "Gas_Opacity.png");
            EditorUtility.SetDirty(mat);
            gas.GetComponent<ParticleSystemRenderer>().sharedMaterial = mat;
            return gas;
        }

        private static GameObject BuildAwakeningArea(Dictionary<string, Material> mats, Transform parent,
            out Transform dragStart, out Transform awakening)
        {
            var room = CreateGroup("Chapter2_Awakening_Only", parent);
            room.transform.position = new Vector3(0f, -24f, 69f);
            Transform r = room.transform;
            BuildDetail("Sealed_Concrete_Floor", r, new Vector3(0f, -0.10f, 0.7f), new Vector3(3.5f, 0.20f, 7.8f), mats["FloorTiles"], true);
            BuildDetail("Sealed_Overhead_Slab", r, new Vector3(0f, 2.81f, 0.7f), new Vector3(3.56f, 0.18f, 7.85f), mats["Concrete"], true);
            for (int side = -1; side <= 1; side += 2)
            {
                BuildDetail("Sealed_Return_Wall", r, new Vector3(1.77f * side, 1.36f, 0.7f),
                    new Vector3(0.2f, 2.73f, 7.82f), mats["Plaster"], true);
                BuildDetail("Sealed_End_Wall", r, new Vector3(0f, 1.36f, 0.7f + side * 3.88f),
                    new Vector3(3.56f, 2.73f, 0.16f), mats["Concrete"], true);
                for (int k = 0; k < 2; k++)
                {
                    GameObject pipe = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    pipe.name = "Old_Overhead_Conduit";
                    pipe.transform.SetParent(r, false);
                    pipe.transform.localPosition = new Vector3(side * (1.14f + 0.16f * k), 2.62f - k * 0.05f, 0.7f);
                    pipe.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    pipe.transform.localScale = new Vector3(0.067f, 3.82f, 0.067f);
                    DestroyImmediateIfPresent(pipe.GetComponent<Collider>());
                    pipe.GetComponent<Renderer>().sharedMaterial = mats["RustedMetal"];
                }
            }
            for (int i = 0; i < 4; i++)
            {
                float z = -2.30f + i * 1.95f;
                BuildDetail("Overhead_Coffer_Beam", r, new Vector3(0f, 2.66f, z + 0.69f),
                    new Vector3(3.44f, 0.17f, 0.12f), mats["Concrete"]);
                var fixture = InstantiateFbx("Assets/Environment/External/Models/mounted_fluorescent_lights/mounted_fluorescent_lights.fbx",
                    "Industrial_Ceiling_Fitting_" + i, r, new Vector3(0f, 2.68f, z), Quaternion.identity, Vector3.one, true);
                AssignPolyHavenMats(fixture, mats["mounted_fluorescent_lights"], mats["FluorescentTubeGlass"]);
                CreateFinaleLight(r, "Weak_Industrial_Light_" + i, new Vector3(0f, 2.48f, z),
                    new Color(0.64f, 0.74f, 0.68f), 0.88f, 3.05f);
            }
            dragStart = CreateGroup("Dragging_Path_Start", r).transform;
            dragStart.localPosition = new Vector3(0f, 0f, -1.93f);
            dragStart.localRotation = Quaternion.identity;
            awakening = CreateGroup("Awakening_Stand_Position", r).transform;
            awakening.localPosition = new Vector3(0f, 0.012f, 1.2f);
            awakening.localRotation = Quaternion.Euler(0f, 180f, 0f);
            room.SetActive(false);
            return room;
        }

        private static void DestroyImmediateIfPresent(UnityEngine.Object component)
        {
            if (component != null) UnityEngine.Object.DestroyImmediate(component);
        }
    }
}
