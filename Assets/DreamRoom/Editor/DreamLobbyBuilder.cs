using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;
using static DreamStudio.Editor.DreamBuildUtils;

namespace DreamStudio.Editor
{
    /// <summary>
    /// Builds the Start Room: the cosy dream bedroom where the game begins, in the same scene as the
    /// Portal Room and joined to it by a corridor through the doorway in its +Z wall. It teaches the
    /// controls and the nightmare/dream idea, lets players practise grabbing, and holds the comfort console.
    /// Built by <see cref="DreamRoomBuilder"/>.
    /// </summary>
    public static class DreamLobbyBuilder
    {
        public const float Radius = 7f;
        public const float DoorWidth = 3f;
        public const float DoorHeight = 3.6f;
        const float WallHeight = 4.5f;

        /// <summary>Where the player starts, relative to the room's center: back from the door, facing it.</summary>
        public static readonly Vector3 SpawnOffset = new Vector3(0f, 0f, -3.5f);

        /// <summary>Builds the room under <paramref name="parent"/>, centered at <paramref name="center"/>, its doorway facing +Z.</summary>
        public static void BuildInto(Transform parent, Vector3 center)
        {
            var state = Random.state;
            Random.InitState(4242);

            var room = new GameObject("Start Room").transform;
            room.SetParent(parent, false);
            room.localPosition = center;
            var m = new Mats();

            BuildRoom(room, m);
            BuildBedCorner(room, m);
            BuildBookshelf(room, m);
            BuildSigns(room, m);
            BuildComfortConsole(room, m);
            Particles(room, "Dream Motes", new Vector3(0f, 2f, 0f), new Vector3(12f, 3.5f, 12f), 30f,
                new Color(1f, 0.85f, 0.95f), new Color(0.75f, 0.85f, 1f), 0.02f, 0.06f, m.sparkle);

            Random.state = state;
        }

        class Mats
        {
            public Material floor = Lit("Lobby_Floor", new Color(0.72f, 0.55f, 0.45f), 0.35f, new Color(0.04f, 0.02f, 0.02f));
            public Material wall = Lit("Lobby_Wall", new Color(0.92f, 0.82f, 0.95f), 0.25f, new Color(0.1f, 0.07f, 0.12f));
            public Material wood = Lit("Lobby_Wood", new Color(0.55f, 0.38f, 0.28f), 0.2f, new Color(0.03f, 0.02f, 0.01f));
            public Material rug = Lit("Lobby_Rug", new Color(0.95f, 0.6f, 0.75f), 0.1f, new Color(0.1f, 0.03f, 0.06f));
            public Material blanket = Lit("Lobby_Blanket", new Color(0.55f, 0.6f, 1f), 0.3f, new Color(0.05f, 0.06f, 0.12f));
            public Material sheet = Lit("Lobby_Sheet", new Color(0.98f, 0.96f, 1f), 0.3f, new Color(0.1f, 0.1f, 0.12f));
            public Material sign = Lit("Lobby_Sign", new Color(0.3f, 0.24f, 0.42f), 0.4f, new Color(0.05f, 0.03f, 0.08f));
            public Material trim = Lit("Trim", new Color(1f, 0.85f, 0.55f), 0.8f, new Color(0.9f, 0.65f, 0.3f));
            public Material moon = Lit("Moon", new Color(1f, 0.97f, 0.85f), 0.5f, new Color(1.6f, 1.45f, 1.1f));
            public Material cloud = Lit("Cloud", new Color(1f, 0.95f, 0.98f), 0.1f, new Color(0.35f, 0.28f, 0.38f));
            public Material button = Lit("Lobby_Button", new Color(0.5f, 0.85f, 1f), 0.6f, new Color(0.4f, 0.9f, 1.2f));
            public Material star = Lit("Lobby_PracticeStar", new Color(1f, 0.92f, 0.6f), 0.9f, new Color(1.8f, 1.5f, 0.6f));
            public Material sparkle = Sparkle();
            public Material[] books =
            {
                Lit("Lobby_Book_Red", new Color(0.8f, 0.3f, 0.35f), 0.3f, Color.black),
                Lit("Lobby_Book_Blue", new Color(0.3f, 0.45f, 0.8f), 0.3f, Color.black),
                Lit("Lobby_Book_Green", new Color(0.35f, 0.7f, 0.45f), 0.3f, Color.black),
                Lit("Lobby_Book_Gold", new Color(0.9f, 0.75f, 0.35f), 0.3f, Color.black),
            };
        }

        // ---------- Small helpers ----------

        static GameObject Group(string name, Transform parent, Vector3 position, float yaw = 0f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            return go;
        }

        static GameObject Part(PrimitiveType type, string name, Transform parent, Material mat, Vector3 pos, Vector3 scale, bool solid = true)
        {
            var go = CreatePrimitive(type, name, parent, mat, solid);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            return go;
        }

        static TextMeshPro Text(Transform parent, string text, Vector3 pos, float size, Vector2 box, Color color, TextAlignmentOptions align = TextAlignmentOptions.Center)
        {
            var go = new GameObject("Text");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(0f, 180f, 0f); // readable from the group's +Z side
            var tmp = go.AddComponent<TextMeshPro>();
            tmp.text = text;
            tmp.fontSize = size;
            // Shrink to fit rather than spill past the board.
            tmp.enableAutoSizing = true;
            tmp.fontSizeMax = size;
            tmp.fontSizeMin = size * 0.5f;
            tmp.alignment = align;
            tmp.color = color;
            tmp.rectTransform.sizeDelta = box;
            return tmp;
        }

        /// <summary>Yaw that turns a group's +Z to face <paramref name="target"/> from <paramref name="from"/>.</summary>
        static float FacingYaw(Vector3 from, Vector3 target) => Quaternion.LookRotation(target - from).eulerAngles.y;

        // ---------- The room ----------

        static void BuildRoom(Transform root, Mats m)
        {
            var floor = Part(PrimitiveType.Cylinder, "Floor", root, m.floor, new Vector3(0f, -0.05f, 0f), new Vector3(Radius * 2f + 0.6f, 0.05f, Radius * 2f + 0.6f), false);
            floor.AddComponent<MeshCollider>().sharedMesh = floor.GetComponent<MeshFilter>().sharedMesh;
            floor.AddComponent<TeleportationArea>();
            Part(PrimitiveType.Cylinder, "Rug", root, m.rug, new Vector3(0f, 0.005f, 0.3f), new Vector3(4.4f, 0.005f, 4.4f), false);
            Part(PrimitiveType.Cylinder, "Rug Border", root, m.trim, new Vector3(0f, 0.004f, 0.3f), new Vector3(4.6f, 0.004f, 4.6f), false);

            // A round wall of panels with four tall windows onto the dream sky, and the doorway to the
            // corridor straight ahead (+Z).
            const int segments = 28;
            float width = 2f * Mathf.PI * Radius / segments * 1.05f;
            var walls = Group("Walls", root, Vector3.zero);
            for (int i = 0; i < segments; i++)
            {
                float angle = i * 360f / segments;
                var rot = Quaternion.Euler(0f, angle, 0f);
                var panel = Group($"Wall {i}", walls.transform, rot * new Vector3(0f, 0f, Radius), angle + 180f);
                bool door = Mathf.Abs(Mathf.DeltaAngle(angle, 0f)) < 20f;
                bool window = !door && i % 7 == 3;
                if (door)
                {
                    Part(PrimitiveType.Cube, "Over Door", panel.transform, m.wall, new Vector3(0f, (DoorHeight + WallHeight) * 0.5f, 0f), new Vector3(width, WallHeight - DoorHeight, 0.3f));
                }
                else if (window)
                {
                    Part(PrimitiveType.Cube, "Sill", panel.transform, m.wall, new Vector3(0f, 0.5f, 0f), new Vector3(width, 1f, 0.3f));
                    Part(PrimitiveType.Cube, "Over Window", panel.transform, m.wall, new Vector3(0f, (3.4f + WallHeight) * 0.5f, 0f), new Vector3(width, WallHeight - 3.4f, 0.3f));
                    Part(PrimitiveType.Cube, "Window Frame", panel.transform, m.trim, new Vector3(0f, 2.2f, 0f), new Vector3(0.08f, 2.4f, 0.32f), false);
                    Part(PrimitiveType.Cube, "Window Frame", panel.transform, m.trim, new Vector3(0f, 2.2f, 0f), new Vector3(width, 0.08f, 0.32f), false);
                    // An invisible pane so nobody leans out of the window.
                    var pane = new GameObject("Pane");
                    pane.transform.SetParent(panel.transform, false);
                    pane.transform.localPosition = new Vector3(0f, 2.2f, 0f);
                    pane.AddComponent<BoxCollider>().size = new Vector3(width, 2.4f, 0.3f);
                }
                else
                {
                    Part(PrimitiveType.Cube, "Panel", panel.transform, m.wall, new Vector3(0f, WallHeight * 0.5f, 0f), new Vector3(width, WallHeight, 0.3f));
                }
                Part(PrimitiveType.Cube, "Top Trim", panel.transform, m.trim, new Vector3(0f, WallHeight + 0.05f, 0f), new Vector3(width, 0.1f, 0.4f), false);
                if (!door)
                    Part(PrimitiveType.Cube, "Skirting", panel.transform, m.wood, new Vector3(0f, 0.08f, -0.17f), new Vector3(width, 0.16f, 0.05f), false);
            }

            // Close the curved gap on each side of the doorway down to the corridor's width, and frame it.
            var doorway = Group("Doorway", root, Vector3.zero);
            float half = DoorWidth * 0.5f;
            float chord = Radius * Mathf.Sin(20f * Mathf.Deg2Rad) + 0.3f;
            for (int side = -1; side <= 1; side += 2)
            {
                float x = side * (half + 0.15f + (chord - half - 0.15f) * 0.5f);
                Part(PrimitiveType.Cube, "Door Filler", doorway.transform, m.wall, new Vector3(x, WallHeight * 0.5f, Radius - 0.35f),
                    new Vector3(chord - half - 0.15f, WallHeight, 0.7f));
                Part(PrimitiveType.Cylinder, "Door Column", doorway.transform, m.trim, new Vector3(side * (half + 0.15f), DoorHeight * 0.5f, Radius - 0.75f),
                    new Vector3(0.3f, DoorHeight * 0.5f, 0.3f));
            }
            Part(PrimitiveType.Cube, "Door Lintel", doorway.transform, m.trim, new Vector3(0f, DoorHeight + 0.1f, Radius - 0.75f), new Vector3(DoorWidth + 0.6f, 0.2f, 0.35f), false);

            // Clouds drifting past the windows (none on the doorway side, where the corridor runs).
            for (int i = 0; i < 12; i++)
            {
                float a = Random.Range(Mathf.PI * 1.05f, Mathf.PI * 1.95f); // behind and beside the room (-Z)
                float d = Random.Range(14f, 26f);
                var cloud = Group("Cloud", root, new Vector3(Mathf.Cos(a) * d, Random.Range(-2f, 9f), Mathf.Sin(a) * d), Random.Range(0f, 360f));
                for (int p = 0; p < 5; p++)
                {
                    float s = Random.Range(1.5f, 3f);
                    var puff = Part(PrimitiveType.Sphere, "Puff", cloud.transform, m.cloud,
                        new Vector3(Random.Range(-2f, 2f), Random.Range(-0.3f, 0.5f), Random.Range(-0.8f, 0.8f)), new Vector3(s * 1.2f, s * 0.75f, s), false);
                    puff.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                }
                var drift = cloud.AddComponent<DreamFloat>();
                drift.bobHeight = 0.3f;
                drift.bobSpeed = Random.Range(0.03f, 0.07f);
                drift.spin = new Vector3(0f, Random.Range(-1.5f, 1.5f), 0f);
            }
        }

        static void BuildBedCorner(Transform root, Mats m)
        {
            // The bed, headboard against the wall: where the dream began.
            var bed = Group("Bed", root, new Vector3(-Radius + 1.4f, 0f, -0.6f), -90f);
            Part(PrimitiveType.Cube, "Frame", bed.transform, m.wood, new Vector3(0f, 0.22f, 0f), new Vector3(1.5f, 0.3f, 2.1f));
            Part(PrimitiveType.Cube, "Mattress", bed.transform, m.sheet, new Vector3(0f, 0.47f, 0f), new Vector3(1.4f, 0.2f, 2f));
            Part(PrimitiveType.Cube, "Blanket", bed.transform, m.blanket, new Vector3(0f, 0.6f, -0.3f), new Vector3(1.46f, 0.08f, 1.35f));
            Part(PrimitiveType.Cube, "Blanket Fold", bed.transform, m.sheet, new Vector3(0f, 0.63f, 0.38f), new Vector3(1.44f, 0.06f, 0.15f));
            var pillow = Part(PrimitiveType.Sphere, "Pillow", bed.transform, m.sheet, new Vector3(0f, 0.66f, 0.72f), new Vector3(0.95f, 0.18f, 0.42f));
            pillow.transform.localRotation = Quaternion.Euler(-8f, 0f, 0f);
            Part(PrimitiveType.Cube, "Headboard", bed.transform, m.wood, new Vector3(0f, 0.8f, 1.05f), new Vector3(1.6f, 1.2f, 0.12f));
            Part(PrimitiveType.Sphere, "Headboard Moon", bed.transform, m.trim, new Vector3(0f, 1.15f, 0.98f), new Vector3(0.3f, 0.3f, 0.06f), false);
            foreach (var x in new[] { -0.72f, 0.72f })
                foreach (var z in new[] { -1f, 1.02f })
                    Part(PrimitiveType.Cylinder, "Post", bed.transform, m.wood, new Vector3(x, z > 0f ? 0.7f : 0.35f, z), new Vector3(0.1f, z > 0f ? 0.7f : 0.35f, 0.1f));

            // Nightstand with a lamp, and the practice star hovering above it.
            var stand = Group("Nightstand", root, new Vector3(-Radius + 1.05f, 0f, 1.25f), 90f);
            Part(PrimitiveType.Cube, "Cabinet", stand.transform, m.wood, new Vector3(0f, 0.3f, 0f), new Vector3(0.6f, 0.6f, 0.5f));
            Part(PrimitiveType.Cube, "Drawer", stand.transform, m.trim, new Vector3(0f, 0.42f, 0.26f), new Vector3(0.12f, 0.04f, 0.03f), false);
            Part(PrimitiveType.Cylinder, "Lamp Base", stand.transform, m.trim, new Vector3(-0.15f, 0.68f, -0.05f), new Vector3(0.14f, 0.08f, 0.14f));
            var shade = Part(PrimitiveType.Sphere, "Lamp Shade", stand.transform, m.moon, new Vector3(-0.15f, 0.9f, -0.05f), new Vector3(0.28f, 0.22f, 0.28f), false);
            shade.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            PointLight(shade.transform, "Lamp Light", Vector3.zero, new Color(1f, 0.8f, 0.55f), 5f, 1.2f);

            var hover = Group("Practice Star (Hover)", root, stand.transform.localPosition + new Vector3(0.05f, 1.1f, 0.15f));
            var bob = hover.AddComponent<DreamFloat>();
            bob.bobHeight = 0.06f;
            bob.bobSpeed = 0.4f;
            bob.spin = Vector3.zero;
            var starGo = new GameObject("Practice Star");
            starGo.transform.SetParent(hover.transform, false);
            starGo.AddComponent<SphereCollider>().radius = 0.18f;
            var body = starGo.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            starGo.AddComponent<XRGrabInteractable>().throwOnDetach = false;
            starGo.AddComponent<DreamPracticeGrab>();
            var visual = new GameObject("Visual");
            visual.transform.SetParent(starGo.transform, false);
            var spin = visual.AddComponent<DreamFloat>();
            spin.bobHeight = 0f;
            spin.spin = new Vector3(0f, 60f, 0f);
            MeshObject("Star", visual.transform, SaveMesh(CreateCrystal(0.12f, 0.24f), "PracticeStar"), m.star)
                .GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            PointLight(starGo.transform, "Glow", Vector3.zero, new Color(1f, 0.9f, 0.5f), 3f, 1f);
            Particles(hover.transform, "Glimmer", Vector3.zero, Vector3.one * 0.5f, 6f, new Color(1f, 0.9f, 0.5f), Color.white, 0.02f, 0.05f, m.sparkle);
        }

        static void BuildBookshelf(Transform root, Mats m)
        {
            var shelf = Group("Bookshelf", root, new Vector3(Radius - 0.75f, 0f, 1.6f), -90f);
            Part(PrimitiveType.Cube, "Back", shelf.transform, m.wood, new Vector3(0f, 1.1f, -0.2f), new Vector3(1.6f, 2.2f, 0.06f));
            foreach (var x in new[] { -0.78f, 0.78f })
                Part(PrimitiveType.Cube, "Side", shelf.transform, m.wood, new Vector3(x, 1.1f, 0f), new Vector3(0.06f, 2.2f, 0.45f));
            for (int s = 0; s < 4; s++)
            {
                float y = 0.05f + s * 0.55f;
                Part(PrimitiveType.Cube, "Shelf", shelf.transform, m.wood, new Vector3(0f, y, 0f), new Vector3(1.6f, 0.05f, 0.45f));
                if (s == 3)
                    continue;
                float x = -0.7f;
                while (x < 0.65f)
                {
                    float w = Random.Range(0.05f, 0.1f);
                    float h = Random.Range(0.3f, 0.45f);
                    var book = Part(PrimitiveType.Cube, "Book", shelf.transform, m.books[Random.Range(0, m.books.Length)],
                        new Vector3(x + w * 0.5f, y + 0.025f + h * 0.5f, 0f), new Vector3(w, h, 0.32f), false);
                    if (Random.value < 0.1f)
                        book.transform.localRotation = Quaternion.Euler(0f, 0f, -12f);
                    x += w + 0.01f;
                }
            }
            Part(PrimitiveType.Sphere, "Snow Globe", shelf.transform, m.moon, new Vector3(0.4f, 1.8f, 0f), Vector3.one * 0.22f, false);
            Part(PrimitiveType.Cylinder, "Globe Base", shelf.transform, m.trim, new Vector3(0.4f, 1.69f, 0f), new Vector3(0.18f, 0.03f, 0.18f), false);
        }

        // ---------- Tutorial ----------

        static void BuildSigns(Transform root, Mats m)
        {
            Sign(root, m, new Vector3(-2.8f, 0f, 0.6f), SpawnOffset,
                "<b>Welcome to Dream Studio</b>\n\nYou have drifted into a land of dreams, but six of them have turned into nightmares.\n\n" +
                "Walk down the corridor ahead to reach the <b>Portal Room</b>, then choose a world to rescue.");
            Sign(root, m, new Vector3(2.8f, 0f, 0.6f), SpawnOffset,
                "<b>How to Play</b>\n\n<b>Left stick</b>: move\n<b>Right stick</b>: turn, or push forward to aim a teleport\n" +
                "<b>Grip</b>: grab things and press buttons\nYou can walk around for real, too.\n\n" +
                "Try it: grab the star above the nightstand.");
            Sign(root, m, new Vector3(-2.2f, 0f, 3.6f), SpawnOffset,
                "<b>Nightmares & Dreams</b>\n\nEach world first appears through a dark nightmare filter. Find its token, " +
                "avoid its dangers, and the filter lifts to show the world as it really is.\n\n" +
                "Then follow the floating lights back to where you started: a portal home will be waiting.");
        }

        static void Sign(Transform root, Mats m, Vector3 position, Vector3 lookAt, string text)
        {
            var sign = Group("Sign", root, position, FacingYaw(position, new Vector3(lookAt.x, 0f, lookAt.z)));
            Part(PrimitiveType.Cylinder, "Post", sign.transform, m.wood, new Vector3(0f, 0.6f, -0.05f), new Vector3(0.08f, 0.6f, 0.08f));
            Part(PrimitiveType.Cube, "Board", sign.transform, m.sign, new Vector3(0f, 1.65f, 0f), new Vector3(2f, 1.5f, 0.06f));
            Part(PrimitiveType.Cube, "Board Trim", sign.transform, m.trim, new Vector3(0f, 2.42f, 0f), new Vector3(2.05f, 0.05f, 0.08f), false);
            Text(sign.transform, text, new Vector3(0f, 1.65f, 0.04f), 1.05f, new Vector2(1.8f, 1.3f), new Color(1f, 0.95f, 0.9f));
        }

        static void BuildComfortConsole(Transform root, Mats m)
        {
            var position = new Vector3(3.6f, 0f, -3.2f);
            var console = Group("Comfort Console", root, position, FacingYaw(position, SpawnOffset));
            // The stand sits behind the board so it never covers the bottom row of buttons.
            Part(PrimitiveType.Cube, "Stand", console.transform, m.wood, new Vector3(0f, 0.45f, -0.18f), new Vector3(0.9f, 0.9f, 0.2f));
            Part(PrimitiveType.Cube, "Board", console.transform, m.sign, new Vector3(0f, 1.55f, 0f), new Vector3(1.8f, 1.7f, 0.06f));
            Part(PrimitiveType.Cube, "Board Trim", console.transform, m.trim, new Vector3(0f, 2.42f, 0f), new Vector3(1.85f, 0.05f, 0.08f), false);
            Text(console.transform, "<b>Comfort Settings</b>\n<size=70%>Point at a button and squeeze the grip</size>",
                new Vector3(0f, 2.15f, 0.04f), 1.1f, new Vector2(1.6f, 0.35f), new Color(1f, 0.95f, 0.9f));

            var settings = new[] { ComfortSetting.SmoothTurn, ComfortSetting.Vignette, ComfortSetting.Seated, ComfortSetting.SwimSpeed, ComfortSetting.ShowFps };
            for (int i = 0; i < settings.Length; i++)
            {
                float y = 1.85f - i * 0.25f;
                var button = Part(PrimitiveType.Cube, $"Button {settings[i]}", console.transform, m.button, new Vector3(-0.62f, y, 0.06f), new Vector3(0.16f, 0.16f, 0.08f));
                button.AddComponent<XRSimpleInteractable>();
                var comfort = button.AddComponent<DreamComfortButton>();
                comfort.setting = settings[i];
                comfort.label = Text(console.transform, DreamComfort.Describe(settings[i]), new Vector3(0.12f, y, 0.04f), 0.9f,
                    new Vector2(1.2f, 0.2f), new Color(0.9f, 0.95f, 1f), TextAlignmentOptions.Left);
            }
        }
    }
}
