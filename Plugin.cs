using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace GK2Skateboard
{
    [BepInPlugin(Guid, "Skateboard", Version)]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "sean.gk2.skateboard";
        public const string Version = "1.0.0";

        internal static ManualLogSource Log;
        internal static ConfigEntry<KeyboardShortcut> ToggleKey;
        internal static ConfigEntry<float> TopSpeedMult;
        internal static ConfigEntry<float> AccelTime;
        internal static ConfigEntry<float> CoastTime;
        internal static ConfigEntry<float> TurnRate;
        internal static ConfigEntry<float> BrakeTime;
        internal static ConfigEntry<bool> DebugLog;
        internal static ConfigEntry<float> Volume;
        internal static ConfigEntry<string> PadButton;
        internal static ConfigEntry<string> OlliePadButton;
        internal static ConfigEntry<KeyboardShortcut> OllieKey;
        internal static ConfigEntry<int> RideHeightPx;
        internal static ConfigEntry<int> OllieHeightPx;

        private void Awake()
        {
            Log = Logger;
            ToggleKey = Config.Bind("Controls", "ToggleKey", new KeyboardShortcut(KeyCode.K), "Hop on / off the skateboard.");
            TopSpeedMult = Config.Bind("Feel", "TopSpeedMultiplier", 1.9f, "Top speed relative to the keeper's measured walking speed.");
            AccelTime = Config.Bind("Feel", "AccelSeconds", 0.45f, "Seconds of pushing to go from standstill to top speed.");
            CoastTime = Config.Bind("Feel", "CoastSeconds", 1.2f, "Seconds to roll from top speed to a stop with no input.");
            TurnRate = Config.Bind("Feel", "TurnDegreesPerSecond", 900f, "How fast the board carves toward the stick direction. Higher = tighter.");
            BrakeTime = Config.Bind("Feel", "BrakeSeconds", 0.3f, "Seconds to stop from top speed when pushing the opposite way.");
            PadButton = Config.Bind("Controls", "PadToggle", "L3", "Gamepad button that toggles the board (XInput names: A B X Y LB RB LT RT L3 R3 Back Start DpadUp/Down/Left/Right; empty = off). L3 is unused by GK2.");
            OlliePadButton = Config.Bind("Controls", "PadOllie", "RT", "Gamepad button for an ollie while riding. RT is unused by GK2; A is Interaction, so avoid it.");
            OllieKey = Config.Bind("Controls", "OllieKey", new KeyboardShortcut(KeyCode.Space), "Keyboard ollie while riding.");
            RideHeightPx = Config.Bind("Visual", "RideHeightPixels", 5, "Art pixels the keeper is lifted to stand on the deck.");
            OllieHeightPx = Config.Bind("Visual", "OllieHeightPixels", 14, "Peak height of an ollie, in art pixels.");
            Volume = Config.Bind("Audio", "Volume", 0.6f, new ConfigDescription("Board sound volume (on top of the game's SFX volume).", new AcceptableValueRange<float>(0f, 1f)));
            DebugLog = Config.Bind("Debug", "VerboseLog", false, "Log speeds and renderer setup.");

            var harmony = new Harmony(Guid);
            harmony.PatchAll(typeof(BodyPatches));
            harmony.PatchAll(typeof(ViewPatches));

            var runner = new GameObject("GK2Skateboard.Runner");
            runner.hideFlags = HideFlags.HideAndDontSave;
            DontDestroyOnLoad(runner);
            runner.AddComponent<SkateRunner>();
            Log.LogInfo($"Skateboard {Version} loaded. Toggle: {ToggleKey.Value}");
        }
    }

    internal class SkateRunner : MonoBehaviour
    {
        private static bool PadDown(string name) => XPad.Down(XPad.MaskFor(name));

        // KeyboardShortcut.IsDown() fails while any other key is held (e.g. WASD while moving), so test the main key directly.
        private static bool KeyDown(KeyboardShortcut k) => k.MainKey != KeyCode.None && Input.GetKeyDown(k.MainKey);

        private void Update()
        {
            XPad.Poll();
            if (KeyDown(Plugin.ToggleKey.Value) || PadDown(Plugin.PadButton.Value))
                Skate.Toggle();
            else if (Skate.Active && (KeyDown(Plugin.OllieKey.Value) || PadDown(Plugin.OlliePadButton.Value)))
                Skate.View?.Ollie();
            Skate.Validate();
        }
    }

    /// <summary>Skate state and per-physics-step momentum integration (in screen-space units, like the game's own movement).</summary>
    internal static class Skate
    {
        // The game squashes movement by (1, 0.6, 0.8) so on-screen speed is uniform; integrate in unsquashed space.
        private static readonly Vector3 WorldScale = new Vector3(1f, 0.5999999f, 0.8f);

        public static bool Active;
        public static PlayerPhysicalBody Body;
        private static float savedDamping;
        private static BoardView view;
        public static BoardView View => view;

        // Input recorded by MoveByDirection, in screen space (unit length or zero).
        public static Vector3 InputScreen;
        public static float LastMoveCallTime = -1f;

        // Integration bookkeeping.
        private static float integratedAt = -1f;
        private static Vector3 capturedWorldVel;
        private static bool captured;
        private static Vector3 resultScreen;
        public static Vector3 ScreenVelocity => resultScreen;

        // Walking speed reference, measured while not skating (steady-state samples only).
        private static float walkSpeed = 3.8f;
        private static bool walkMeasured;
        private static float dismountedAt = -10f;
        private static float walkInputSince = -1f;

        public static Vector3 ToScreen(Vector3 w) => new Vector3(w.x, w.y / WorldScale.y, w.z / WorldScale.z);
        public static Vector3 ToWorld(Vector3 s) => Vector3.Scale(s, WorldScale);

        private static PlayerController Controller => MainGame.Instance != null ? MainGame.PlayerController : null;

        public static void Toggle()
        {
            if (Active) { Dismount("toggled off"); return; }
            var pc = Controller;
            if (pc == null || pc.PhysicalBody == null || !pc.IsControlsEnabled) return;
            Mount(pc);
        }

        private static void Mount(PlayerController pc)
        {
            Body = pc.PhysicalBody;
            var rb = Body.Rb;
            savedDamping = rb.linearDamping;
            rb.linearDamping = 0f;
            resultScreen = ToScreen(Vector3.ProjectOnPlane(rb.linearVelocity, -Body.GravityNormalized));
            integratedAt = -1f;
            LastMoveCallTime = -1f;
            Active = true;
            var go = new GameObject("GK2Skateboard.Board");
            go.transform.SetParent(Body.PlayerView.transform, false);
            view = go.AddComponent<BoardView>();
            view.Init(Body.PlayerView);
            if (Plugin.DebugLog.Value) Plugin.Log.LogInfo($"Mounted. damping was {savedDamping:F2}, walk ref {walkSpeed:F2}{(walkMeasured ? "" : " (default)")}, top {TopSpeed:F2}");
        }

        public static void Dismount(string why)
        {
            if (!Active) return;
            Active = false;
            dismountedAt = Time.time;
            if (Body != null && Body.Rb != null) Body.Rb.linearDamping = savedDamping;
            if (view != null) Object.Destroy(view.gameObject);
            view = null;
            Body = null;
            if (Plugin.DebugLog.Value) Plugin.Log.LogInfo("Dismounted: " + why);
        }

        public static void Validate()
        {
            if (!Active) return;
            var pc = Controller;
            if (Body == null || pc == null || pc.PhysicalBody != Body) Dismount("player changed");
        }

        public static float TopSpeed => walkSpeed * Plugin.TopSpeedMult.Value;

        /// <summary>Called by the UpdatePhysics prefix on the first run of each fixed step.</summary>
        public static void Capture(PlayerPhysicalBody body)
        {
            if (integratedAt == Time.fixedTime) return;
            capturedWorldVel = body.Rb.linearVelocity;
            captured = true;
        }

        /// <summary>Called by the UpdatePhysics postfix after every run; integrates once per fixed step, re-applies always.</summary>
        public static void Apply(PlayerPhysicalBody body)
        {
            var rb = body.Rb;
            if (rb.isKinematic) return;
            float dt = Time.fixedDeltaTime;
            bool freeState = Time.fixedTime - LastMoveCallTime <= dt * 1.5f;
            if (!freeState)
            {
                // Not in free movement (dialog, work, ladder...): let the game stop the keeper as usual.
                resultScreen = Vector3.zero;
                return;
            }
            Vector3 n = -body.GravityNormalized;
            if (integratedAt != Time.fixedTime && captured)
            {
                integratedAt = Time.fixedTime;
                captured = false;
                Vector3 s = ToScreen(Vector3.ProjectOnPlane(capturedWorldVel, n));
                float top = TopSpeed;
                if (InputScreen.sqrMagnitude > 0.0001f)
                {
                    // Carve: rotate the velocity toward the stick at TurnRate, keep speed; push to accelerate; opposite stick brakes.
                    Vector3 dir = InputScreen.normalized;
                    float speed = s.magnitude;
                    Vector3 heading = speed > 0.3f ? s / speed : dir;
                    if (speed > 0.3f && Vector3.Angle(heading, dir) > 150f)
                    {
                        speed = Mathf.MoveTowards(speed, 0f, top / Mathf.Max(0.05f, Plugin.BrakeTime.Value) * dt);
                    }
                    else
                    {
                        heading = Vector3.RotateTowards(heading, dir, Plugin.TurnRate.Value * Mathf.Deg2Rad * dt, 0f);
                        speed += top / Mathf.Max(0.05f, Plugin.AccelTime.Value) * dt;
                    }
                    s = heading * speed;
                }
                else
                {
                    s = Vector3.MoveTowards(s, Vector3.zero, top / Mathf.Max(0.05f, Plugin.CoastTime.Value) * dt);
                }
                if (s.magnitude > top) s = s.normalized * top;
                resultScreen = s;
                if (Plugin.DebugLog.Value && Time.frameCount % 30 == 0)
                    Plugin.Log.LogInfo($"skate speed {s.magnitude:F2}/{top:F2}");
            }
            Vector3 v = rb.linearVelocity;
            Vector3 normalPart = v - Vector3.ProjectOnPlane(v, n);
            rb.linearVelocity = normalPart + Vector3.ProjectOnPlane(ToWorld(resultScreen), n);
        }

        /// <summary>Tracks the keeper's steady walking speed so skate top speed scales with buffs and patches.</summary>
        public static void ObserveWalk(PlayerPhysicalBody body, bool hasInput)
        {
            if (Active || !hasInput) { walkInputSince = -1f; return; }
            if (walkInputSince < 0f) walkInputSince = Time.time;
            // Skip leftover skate momentum after a dismount and the acceleration ramp at the start of a walk.
            if (Time.time - dismountedAt < 2f || Time.time - walkInputSince < 0.5f) return;
            float s = ToScreen(Vector3.ProjectOnPlane(body.Rb.linearVelocity, -body.GravityNormalized)).magnitude;
            if (s < 1f || s > 8f) return;
            if (!walkMeasured) { walkMeasured = true; walkSpeed = s; }
            else if (Mathf.Abs(s - walkSpeed) < walkSpeed * 0.4f) walkSpeed = Mathf.Lerp(walkSpeed, s, 0.02f);
        }
    }

    [HarmonyPatch(typeof(PlayerPhysicalBody))]
    internal static class BodyPatches
    {
        private static readonly AccessTools.FieldRef<PlayerPhysicalBody, bool> HasPhysUpdated =
            AccessTools.FieldRefAccess<PlayerPhysicalBody, bool>("hasPhysUpdatedThisFixedFrame");
        private static readonly AccessTools.FieldRef<PlayerPhysicalBody, bool> MovedThisFrame =
            AccessTools.FieldRefAccess<PlayerPhysicalBody, bool>("movedByDirectionThisFrame");
        private static readonly MethodInfo UpdatePhysicsM = AccessTools.Method(typeof(PlayerPhysicalBody), "UpdatePhysics");
        private static readonly MethodInfo HasMovementPossibilityM = AccessTools.Method(typeof(PlayerPhysicalBody), "HasMovementPossibility");
        private static readonly MethodInfo GetFacingDirectionM = AccessTools.Method(typeof(PlayerPhysicalBody), "GetFacingDirection");
        private static readonly MethodInfo SetAnimStateM = AccessTools.Method(typeof(PlayerPhysicalBody), "SetCurrentAnimationState");

        [HarmonyPrefix]
        [HarmonyPatch("MoveByDirection")]
        private static bool MoveByDirectionPrefix(PlayerPhysicalBody __instance, Vector2 direction)
        {
            if (!Skate.Active || __instance != Skate.Body)
            {
                return true;
            }
            if (__instance.PhysicsConfig == null || MainGame.IsGamePaused) return false;

            Skate.LastMoveCallTime = Time.fixedTime;
            UpdatePhysicsM.Invoke(__instance, null);
            bool canMove = (bool)HasMovementPossibilityM.Invoke(__instance, new object[] { direction });
            if (canMove)
            {
                // Same direction transform as the game: tilt the input onto the gravity plane.
                Vector3 g = __instance.GravityNormalized;
                Quaternion q1 = Quaternion.FromToRotation(Physics.gravity.normalized, Vector3.ProjectOnPlane(g, Vector3.right));
                Quaternion q2 = Quaternion.FromToRotation(Physics.gravity.normalized, Vector3.ProjectOnPlane(g, Vector3.back));
                Skate.InputScreen = q1 * q2 * new Vector3(direction.x, 0f, direction.y).normalized;
                __instance.PlayerView.UpdateAnimationDirection((Vector2)GetFacingDirectionM.Invoke(__instance, new object[] { direction }));
            }
            else
            {
                Skate.InputScreen = Vector3.zero;
            }
            // Standing on the board: no walk cycle.
            SetAnimStateM.Invoke(__instance, new object[] { AnimationState.Idle });
            MovedThisFrame(__instance) = true;
            return false;
        }

        [HarmonyPostfix]
        [HarmonyPatch("MoveByDirection")]
        private static void MoveByDirectionPostfix(PlayerPhysicalBody __instance, Vector2 direction)
        {
            Skate.ObserveWalk(__instance, direction.sqrMagnitude > 0.01f);
        }

        [HarmonyPrefix]
        [HarmonyPatch("UpdatePhysics")]
        private static void UpdatePhysicsPrefix(PlayerPhysicalBody __instance, out bool __state)
        {
            __state = Skate.Active && __instance == Skate.Body && __instance.PhysicsConfig != null && !HasPhysUpdated(__instance);
            if (__state) Skate.Capture(__instance);
        }

        [HarmonyPostfix]
        [HarmonyPatch("UpdatePhysics")]
        private static void UpdatePhysicsPostfix(PlayerPhysicalBody __instance, bool __state)
        {
            if (__state) Skate.Apply(__instance);
        }
    }

    [HarmonyPatch(typeof(PlayerView), "LateUpdate")]
    internal static class ViewPatches
    {
        [HarmonyPostfix]
        private static void Postfix(PlayerView __instance)
        {
            var v = Skate.View;
            if (Skate.Active && v != null && Skate.Body != null && Skate.Body.PlayerView == __instance)
                v.Place(__instance.transform.position);
        }
    }

    /// <summary>The board sprite: copies the keeper's sprite setup so it sits on the same pixel grid and material.</summary>
    internal class BoardView : MonoBehaviour
    {
        // Mirrors tools/make_art.py.
        private const int CellW = 26, CellH = 22;
        // Wheels sit on the ground; the keeper is lifted onto the deck instead (VerticalSprite is an upright billboard, so anything below the ground is depth-hidden).
        private const float SidePivotPx = 0f, FrontPivotPx = 0f;
        private const float PixelsPerFrame = 6f;

        private static Texture2D sheet;
        private static readonly Dictionary<float, Sprite[]> spritesByPpu = new Dictionary<float, Sprite[]>();

        private PlayerView playerView;
        private SpriteRenderer body;
        private SpriteRenderer sr;
        private Sprite[] sprites;
        private float travelled;
        private bool facingSide = true;
        private bool flip;
        private const float OllieDuration = 0.5f;
        private float ollieT = -1f;

        private SkateAudio audio;

        public void Ollie()
        {
            if (ollieT >= 0f) return;
            ollieT = 0f;
            audio?.OnOllie();
        }

        public void Init(PlayerView pv)
        {
            playerView = pv;
            sr = gameObject.AddComponent<SpriteRenderer>();
            try
            {
                audio = gameObject.AddComponent<SkateAudio>();
                audio.Init();
            }
            catch (System.Exception ex)
            {
                Plugin.Log.LogWarning("Skate audio disabled: " + ex.Message);
                audio = null;
            }
            body = FindBodyRenderer(pv);
            float ppu = body != null && body.sprite != null ? body.sprite.pixelsPerUnit : 100f;
            sprites = GetSprites(ppu);
            sr.sprite = sprites[0];
            if (body != null)
            {
                sr.sharedMaterial = body.sharedMaterial;
                sr.sortingLayerID = body.sortingLayerID;
                sr.sortingOrder = body.sortingOrder - 1;
                gameObject.layer = body.gameObject.layer;
                if (Plugin.DebugLog.Value) Plugin.Log.LogInfo($"Board copies '{body.name}': ppu {ppu}, mat '{body.sharedMaterial?.name}', layer {body.sortingLayerName}/{body.sortingOrder}, rot {body.transform.rotation.eulerAngles}, scale {body.transform.lossyScale}");
            }
            else
            {
                Plugin.Log.LogWarning("No keeper SpriteRenderer found; board uses defaults.");
            }
        }

        private static SpriteRenderer FindBodyRenderer(PlayerView pv)
        {
            var field = AccessTools.Field(typeof(PlayerAnimation), "spriteRenderers");
            var list = pv.PlayerAnimation != null ? field?.GetValue(pv.PlayerAnimation) as List<SpriteRenderer> : null;
            IEnumerable<SpriteRenderer> candidates = list ?? (IEnumerable<SpriteRenderer>)pv.GetComponentsInChildren<SpriteRenderer>();
            SpriteRenderer best = null;
            float bestArea = 0f;
            foreach (var r in candidates)
            {
                if (r == null || r.sprite == null || !r.gameObject.activeInHierarchy) continue;
                float area = r.sprite.rect.width * r.sprite.rect.height;
                if (area > bestArea) { bestArea = area; best = r; }
            }
            return best;
        }

        private static void LoadSheet()
        {
            if (sheet != null) return;
            sheet = new Texture2D(2, 2, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "GK2Skateboard" };
            using (var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("GK2Skateboard.skateboard.png"))
            using (var ms = new MemoryStream())
            {
                s.CopyTo(ms);
                sheet.LoadImage(ms.ToArray());
            }
            sheet.filterMode = FilterMode.Point;
        }

        private static Sprite[] GetSprites(float ppu)
        {
            if (spritesByPpu.TryGetValue(ppu, out var cached)) return cached;
            LoadSheet();
            var arr = new Sprite[4];
            for (int i = 0; i < 4; i++)
            {
                float pivotPx = i < 2 ? SidePivotPx : FrontPivotPx;
                arr[i] = Sprite.Create(sheet, new Rect(i * CellW, 0, CellW, CellH), new Vector2(0.5f, pivotPx / CellH), ppu, 0, SpriteMeshType.FullRect);
                arr[i].name = "Skateboard_" + i;
            }
            spritesByPpu[ppu] = arr;
            return arr;
        }

        /// <summary>Called after PlayerView.LateUpdate pixel-snaps the view: lift the keeper onto the deck, keep the board on the ground.</summary>
        public void Place(Vector3 groundPos)
        {
            float px = ArtPixelWorldY;
            float hop = 0f;
            if (ollieT >= 0f)
            {
                ollieT += Time.deltaTime;
                float u = ollieT / OllieDuration;
                if (u >= 1f) { ollieT = -1f; audio?.OnLand(); }
                else hop = Mathf.Round(4f * u * (1f - u) * Plugin.OllieHeightPx.Value) * px; // whole-pixel parabola
            }
            playerView.transform.position = groundPos + Vector3.up * (Plugin.RideHeightPx.Value * px + hop);
            Vector3 cam = Camera.main != null ? Camera.main.transform.forward : Vector3.forward;
            transform.position = groundPos + Vector3.up * hop + cam * 0.05f;
        }

        // One art pixel of the keeper's sprite in world Y (PPU from the copied renderer, times its vertical stretch).
        private float ArtPixelWorldY => (body != null ? body.transform.lossyScale.y : 1.6666667f) / sprites[0].pixelsPerUnit;

        private float bornAt = -1f;
        private bool reported;

        private void LateUpdate()
        {
            if (playerView == null) return;
            if (bornAt < 0f) bornAt = Time.time;
            if (!reported && Plugin.DebugLog.Value && Time.time - bornAt > 0.5f)
            {
                reported = true;
                var cam = Camera.main;
                string scr = cam != null ? $"board@{cam.WorldToScreenPoint(transform.position)} keeper@{cam.WorldToScreenPoint(playerView.transform.position)}" : "no camera";
                Plugin.Log.LogInfo($"Board check: visible={sr.isVisible} enabled={sr.enabled} sprite={sr.sprite?.name} bounds={sr.bounds} {scr}");
            }
            if (body != null)
            {
                // Match the keeper's billboard orientation and scale; tuck slightly behind so the feet overlap the deck.
                transform.rotation = body.transform.rotation;
                Vector3 parentScale = transform.parent.lossyScale;
                Vector3 bs = body.transform.lossyScale;
                // World X scale kept positive (parent may mirror via negative scale); flipX alone sets direction.
                transform.localScale = new Vector3(Mathf.Abs(bs.x) / parentScale.x, bs.y / parentScale.y, bs.z / parentScale.z);
                sr.sortingOrder = body.sortingOrder - 1;
            }
            Vector3 vs = Skate.ScreenVelocity;
            Vector2 heading = playerView.GetAnimationDirection();
            if (heading.sqrMagnitude > 0.0001f)
            {
                // Same 4-way quantization the game uses for the keeper (PlayerView.UpdateWispDirection), so board and body switch together.
                Direction d = (Mathf.Atan2(heading.y, heading.x) * Mathf.Rad2Deg).ConvertFromSignedAngle();
                if (d == Direction.Left || d == Direction.Right) { facingSide = true; flip = d == Direction.Left; }
                else if (d == Direction.Up || d == Direction.Down) facingSide = false;
            }
            travelled += vs.magnitude * Time.deltaTime * 100f;
            int frame = (int)(travelled / PixelsPerFrame) & 1;
            sr.sprite = sprites[(facingSide ? 0 : 2) + frame];
            sr.flipX = facingSide && flip;
        }
    }
}
