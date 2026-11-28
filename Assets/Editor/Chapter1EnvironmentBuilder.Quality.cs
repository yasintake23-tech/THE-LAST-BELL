using System;
using System.Collections.Generic;
using TheLastBell.Interaction;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace TheLastBell.Editor
{
    public static partial class Chapter1EnvironmentBuilder
    {
        private static void RepairSedanMaterials(GameObject sedan, string folder, Dictionary<string, Material> mats)
        {
            var palette = new Dictionary<string, Material>(StringComparer.OrdinalIgnoreCase);
            var properties = new (string name, float metal, float smooth)[]
            {
                ("paint", 0.31f, 0.42f), ("rubber", 0.0f, 0.13f), ("black", 0.0f, 0.22f),
                ("interior", 0.0f, 0.23f), ("chrome", 0.72f, 0.62f), ("steel", 0.62f, 0.37f),
                ("white", 0.12f, 0.32f), ("clearlens", 0.14f, 0.66f), ("redlens", 0.05f, 0.63f)
            };
            foreach (var spec in properties)
            {
                string path = folder + (spec.name == "paint" ? "Sedan_paint_weathered.png" : "Sedan_" + spec.name + "_diff.png");
                var texture = LoadRequiredQualityTexture(path);
                var material = CreateSimpleStandardMat("Mat_Car_Refinish_" + spec.name, Color.white, spec.metal, spec.smooth);
                material.mainTexture = texture;
                EditorUtility.SetDirty(material);
                palette.Add(spec.name, material);
            }
            palette["glass"] = mats["CarGlass"];

            foreach (var renderer in sedan.GetComponentsInChildren<MeshRenderer>(true))
            {
                string part = renderer.name.Replace("Sedan_", "");
                var mesh = renderer.GetComponent<MeshFilter>()?.sharedMesh;
                int count = Mathf.Max(1, mesh != null ? mesh.subMeshCount : 1);
                Material[] imported = renderer.sharedMaterials;
                var repaired = new Material[count];
                for (int slot = 0; slot < count; slot++)
                {
                    string importerName = slot < imported.Length && imported[slot] != null ? imported[slot].name : "";
                    string key = importerName.Split('.')[0].ToLowerInvariant();
                    if (!palette.TryGetValue(key, out repaired[slot]))
                    {
                        // The body has black filler in slot 0 and the actual painted shell in slot 1.
                        if (part == "paint" && count > 1) repaired[slot] = palette[slot == 0 ? "black" : "paint"];
                        else repaired[slot] = palette.TryGetValue(part, out var material) ? material : palette["black"];
                    }
                }
                renderer.sharedMaterials = repaired;
            }
        }

        private static Texture2D LoadRequiredQualityTexture(string path)
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture != null) return texture;

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture == null)
            {
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                string settings = importer != null ? $"{importer.textureType}/{importer.textureShape}" : "no TextureImporter";
                throw new InvalidOperationException(
                    $"Could not load required Texture2D at {path} ({settings}, main asset: {AssetDatabase.GetMainAssetTypeAtPath(path)}).");
            }
            return texture;
        }

        private static void InstallClassroomDoors(Dictionary<string, Material> mats, Transform parent)
        {
            foreach (string obsolete in new[] { "C101_Door_Ajar", "C103_Door_Fallen", "Lounge_Door_Ajar" })
                GameObject.Find(obsolete)?.SetActive(false);
            var desk = GameObject.Find("C102_Barricade_Desk1");
            if (desk != null) desk.transform.position = new Vector3(-4.2f, desk.transform.position.y, 11.7f);
            var desk2 = GameObject.Find("C102_Barricade_Desk2");
            if (desk2 != null) desk2.transform.position = new Vector3(-4.9f, desk2.transform.position.y, 15.65f);

            CreateClassroomDoor("C101", "101", -2f, 6.25f, mats["PaintedWood"], mats, parent);
            CreateClassroomDoor("C102", "102", -2f, 14.25f, mats["DarkWood"], mats, parent);
            CreateClassroomDoor("C103", "103", 2f, 14.25f, mats["PaintedWood"], mats, parent);
            CreateClassroomDoor("Lounge", "PERSONEL", 2f, 6.25f, mats["DarkWood"], mats, parent);
        }

        private static void CreateClassroomDoor(string id, string sign, float x, float z, Material paint,
            Dictionary<string, Material> mats, Transform parent)
        {
            Transform root = CreateGroup("ClassroomDoor_" + id, parent).transform;
            root.localPosition = new Vector3(x, 0f, z);
            root.localRotation = Quaternion.Euler(0f, Mathf.Sign(x) * 90f, 0f);
            for (int side = -1; side <= 1; side += 2)
            {
                BuildDetail("Weathered_Oak_Jamb", root, new Vector3(side * 0.685f, 1.19f, 0f),
                    new Vector3(0.13f, 2.38f, 0.31f), mats["DarkWood"], true);
                BuildDetail("Door_Face_Casing", root, new Vector3(side * 0.69f, 1.185f, -0.16f),
                    new Vector3(0.105f, 2.37f, 0.047f), mats["DarkWood"]);
            }
            BuildDetail("Solid_Classroom_Transom", root, new Vector3(0f, 2.36f, 0f),
                new Vector3(1.51f, 0.30f, 0.31f), mats["Plaster"], true);
            BuildDetail("Weathered_Frame_Header", root, new Vector3(0f, 2.24f, -0.17f),
                new Vector3(1.47f, 0.105f, 0.065f), mats["DarkWood"]);
            BuildDetail("Recessed_Room_Plate", root, new Vector3(0.41f, 2.395f, -0.177f),
                new Vector3(0.37f, 0.108f, 0.034f), mats["PanelDark"]);
            var plate = CreateGroup("Room_Label", root);
            plate.transform.localPosition = new Vector3(0.41f, 2.393f, -0.209f);
            var label = plate.AddComponent<TextMesh>();
            label.text = sign;
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.fontSize = 60;
            label.characterSize = 0.073f;
            label.color = new Color(0.86f, 0.80f, 0.66f);

            Transform hinge = CreateGroup("Classroom_Physical_Hinge", root).transform;
            hinge.localPosition = new Vector3(-0.60f, 0f, 0f);
            var leaf = InstantiateFbx("Assets/Environment/Original/ClassroomDoor/Classroom_Service_Door.fbx",
                "Weathered_Classroom_Leaf", hinge, Vector3.zero, Quaternion.identity, Vector3.one, false);
            if (BoundsIn(leaf.transform).center.x < 0f) leaf.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            Bounds shape = BoundsIn(leaf.transform);
            if (shape.size.y < 2.05f || shape.size.x < 1.15f || shape.size.x > 1.23f)
                throw new InvalidOperationException($"Classroom door axes or scale invalid for {id}: {shape.size}");
            leaf.transform.localPosition += Vector3.right * (0.60f -
                hinge.InverseTransformPoint(leaf.transform.TransformPoint(shape.center)).x);
            foreach (var mesh in leaf.GetComponentsInChildren<MeshRenderer>(true))
                mesh.sharedMaterial = mesh.name.Contains("Steel") ? mats["RustedMetal"] :
                    mesh.name.Contains("Oak") ? mats["DarkWood"] : paint;

            for (int side = -1; side <= 1; side += 2)
            {
                var lever = CreateGroup(side < 0 ? "Door_Lever_Pivot" : "Door_Lever_Pivot_Back", hinge).transform;
                lever.localPosition = new Vector3(1.065f, 1.015f, side * 0.060f);
                BuildDetail("Aged_Handle_Backplate", hinge, lever.localPosition - side * Vector3.forward * 0.022f,
                    new Vector3(0.065f, 0.22f, 0.017f), mats["RustedMetal"]);
                var bar = BuildDetail("Steel_Lever_Bar", lever, new Vector3(-0.059f, 0f, side * 0.027f),
                    new Vector3(0.145f, 0.029f, 0.026f), mats["RustedMetal"]);
                GameObjectUtility.SetStaticEditorFlags(bar, 0);
                var contact = CreateGroup(side < 0 ? "Door_Handle_Target" : "Door_Handle_Opposite", hinge).transform;
                contact.localPosition = lever.localPosition + new Vector3(-0.062f, 0f, side * 0.032f);
                contact.localRotation = Quaternion.Euler(0f, side < 0 ? 180f : 0f, 0f);
            }

            var leafCollider = hinge.gameObject.AddComponent<BoxCollider>();
            leafCollider.center = new Vector3(0.60f, 1.10f, 0f);
            leafCollider.size = new Vector3(1.2f, 2.19f, 0.076f);
            var blocker = root.gameObject.AddComponent<BoxCollider>();
            blocker.center = new Vector3(0f, 1.1f, 0f);
            blocker.size = new Vector3(1.23f, 2.2f, 0.065f);
            var audio = CreateAudioEmitter(root, "Classroom_Door_Sound", new Vector3(0.41f, 1.01f, 0f), 0.75f, 10f, "Interactions");
            var door = root.gameObject.AddComponent<DoorInteractable>();
            door.Configure(hinge, null, blocker);
            door.ConfigureClassroomDoor();
            door.ConfigureAudio(audio, FinaleAudio("SFX_Door_HandleLatch"), FinaleAudio("SFX_Door_HeavyHingeOpen"),
                FinaleAudio("SFX_Door_HeavyHingeClose"));
        }

        private static void CompleteDoorwayArchitecture(Dictionary<string, Material> mats, Transform parent)
        {
            // The corridor ceiling terminates at z=26.1. The lower cab roof sits below its lintel.
            // Build the actual transition assembly with the existing header and wall bearings.
            BuildDetail("Lift_Masonry_Infilling_Header", parent, new Vector3(0f, 2.959f, 26.062f),
                new Vector3(2.32f, 1.158f, 0.32f), mats["Concrete"], true);
            BuildDetail("Lift_Cased_Soffit", parent, new Vector3(0f, 2.424f, 26.183f),
                new Vector3(2.29f, 0.105f, 0.49f), mats["PanelSteel"], true);
            BuildDetail("Lift_Shaft_Ceiling_Closure", parent, new Vector3(0f, 3.62f, 27.47f),
                new Vector3(3.22f, 0.24f, 2.88f), mats["Concrete"], true);
            // Close the returns between the 7 m classroom ceilings and the shared corridor slab.
            foreach (float x in new[] { -3.05f, 3.05f })
            foreach (float z in new[] { 2.455f, 9.99f, 17.54f })
                BuildDetail("Masonry_Ceiling_Return", parent, new Vector3(x, 3.57f, z),
                    new Vector3(2.16f, 0.16f, 1.18f), mats["Concrete"], true);
        }

        private static void GrowBoundaryVerge(Dictionary<string, Material> mats, Transform parent)
        {
            var rng = new System.Random(78524);
            var patch = CreateGroup("Layered_Courtyard_Overgrowth", parent).transform;
            float Next() => (float)rng.NextDouble();
            for (int i = 0; i < 23; i++)
            {
                float side = i % 2 == 0 ? -1 : 1;
                float x = side * (i < 15 ? 13.2f + Next() * 1.24f : 2.1f + Next() * 10.6f);
                float z = i < 15 ? -1.2f - Next() * 22.3f : -23.5f - Next() * 0.82f;
                float size = 0.28f + Next() * 0.31f;
                string id = i % 4 == 0 ? "fern_02" : "weed_plant_02";
                var model = InstantiateFbx($"Assets/Environment/External/Models/{id}/{id}.fbx", "Overgrowth_FieldPlant",
                    patch, new Vector3(x, 0f, z), Quaternion.Euler(0f, Next() * 360f, 0f), Vector3.one * size, true);
                AssignPolyHavenMats(model, mats[id], null);
                var range = model.AddComponent<LODGroup>();
                range.SetLODs(new[] { new LOD(0.027f, model.GetComponentsInChildren<Renderer>()) });
                range.RecalculateBounds();
            }

            var mesh = new Mesh { name = "Verge_Ivy_Litter_And_Scrub" };
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            var uv = new List<Vector2>();
            var tint = new List<Color>();
            void Leaf(Vector3 center, Vector3 axis, Vector3 up, float size, Color color)
            {
                int first = vertices.Count;
                axis = axis.normalized * size;
                up = up.normalized * size;
                vertices.Add(center - axis - up); vertices.Add(center + axis - up);
                vertices.Add(center + axis + up); vertices.Add(center - axis + up);
                uv.Add(Vector2.zero); uv.Add(Vector2.right); uv.Add(Vector2.one); uv.Add(Vector2.up);
                for (int v = 0; v < 4; v++) tint.Add(color);
                triangles.AddRange(new[] { first, first + 1, first + 2, first, first + 2, first + 3 });
            }
            for (int i = 0; i < 19; i++)
            {
                int side = i % 2 == 0 ? -1 : 1;
                float originZ = -1.1f - Next() * 22.3f;
                float growth = 0.48f + Next() * 1.62f;
                for (int j = 0; j < 38; j++)
                {
                    float h = Next() * growth;
                    float z = originZ + Mathf.Sin(h * 6f + i) * 0.22f + (Next() - 0.5f) * 0.67f * (1f - h / (growth + 0.35f));
                    Leaf(new Vector3(side * (14.765f - Next() * 0.058f), 0.055f + h, z), Vector3.forward + Vector3.up * (Next() - 0.5f),
                        Vector3.up, 0.052f + Next() * 0.10f, Color.Lerp(new Color(0.6f, 0.71f, 0.46f), Color.white, Next() * 0.66f));
                }
                for (int j = 0; j < 24; j++)
                {
                    float a = Next() * Mathf.PI * 2f;
                    float r = Next() * 0.68f;
                    var direction = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                    var center = new Vector3(side * 13.58f, 0.16f + Next() * 0.56f * (1f - r), originZ) + direction * r;
                    Leaf(center, direction, Vector3.Lerp(Vector3.up, Vector3.Cross(Vector3.up, direction), Next() * 0.72f),
                        0.103f + Next() * 0.13f, new Color(0.83f, 0.90f, 0.64f));
                }
            }
            for (int i = 0; i < 280; i++)
            {
                float z = -1.1f - Next() * 22.8f;
                float x = (i % 2 == 0 ? -1 : 1) * (i < 190 ? 12.5f + Next() * 2.1f : 1.25f + Next() * 2.9f);
                if (Vector2.Distance(new Vector2(x, z), new Vector2(2.85f, -17.6f)) <= 3.1f) continue;
                Quaternion angle = Quaternion.Euler(0, Next() * 360f, 0);
                Leaf(new Vector3(x, 0.016f + Next() * 0.016f, z), angle * Vector3.right, angle * Vector3.forward,
                    0.029f + Next() * 0.043f, Color.Lerp(new Color(1.28f, 0.71f, 0.36f), new Color(0.86f, 0.74f, 0.48f), Next()));
            }
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.SetUVs(0, uv);
            mesh.SetColors(tint);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            string path = MAT_DIR + "/Courtyard_Overgrowth_Combined.asset";
            var stored = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (stored == null) { AssetDatabase.CreateAsset(mesh, path); stored = mesh; }
            else { EditorUtility.CopySerialized(mesh, stored); UnityEngine.Object.DestroyImmediate(mesh); }
            var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Environment/Original/Overgrowth/Verge_Foliage.shader");
            string materialPath = MAT_DIR + "/Mat_Courtyard_Vergetone.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (mat == null) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, materialPath); }
            mat.mainTexture = LoadRequiredQualityTexture("Assets/Environment/Original/Overgrowth/Verge_Leaf.png");
            EditorUtility.SetDirty(mat);
            var meshObject = new GameObject("Combined_Ivy_Leaf_Litter_And_Scrub", typeof(MeshFilter), typeof(MeshRenderer));
            meshObject.transform.SetParent(patch, false);
            meshObject.GetComponent<MeshFilter>().sharedMesh = stored;
            var stage = meshObject.GetComponent<MeshRenderer>();
            stage.sharedMaterial = mat;
            stage.shadowCastingMode = ShadowCastingMode.Off;
        }
    }
}
