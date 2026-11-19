// Copyright 2026 Morozan Iulia. Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;
using AccessCalibration.Core;
using UnityEngine;
#if ACCESSCALIBRATE_URP
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
#endif

namespace AccessCalibration.Demo
{
    /// <summary>
    /// A short 3D level (1–2 minutes) that shows what every setting does:
    ///  1. a rolling ball to jump over — quick-time window × timingMultiplier, skippable / automatic by qteMode;
    ///  2. a lever to hold — a single press when holdToToggle is on;
    ///  3. a bridge to lower by tapping — hold (or automatic) by mashingMode;
    ///  4. a narrow path — movement uses the profile's stick deadzone.
    /// The calibration runs on first launch; "Redo calibration" is always available.
    /// Everything is built in code from the Quaternius models (CC0) in Resources.
    /// </summary>
    public sealed class DemoGame : MonoBehaviour
    {
        public enum Phase { Explore, Qte, Lever, Bridge, Done }

        // Level layout along +Z (metres).
        const float QteTriggerZ = 9f, LeverTriggerZ = 20.5f, DoorZ = 23f, BridgeTriggerZ = 26.5f;
        const float GapStartZ = 28f, GapEndZ = 34f, PathStartZ = 38f, PathEndZ = 52f, GoalZ = 56f;
        const float MoveSpeed = 4.5f, Gravity = -20f, FreeJumpHeight = 1.2f;

        public Phase CurrentPhase { get; private set; } = Phase.Explore;
        public Transform Player => _player != null ? _player.transform : null;
        public DemoHud Hud => _hud;
        public CalibrationRunner Runner => _runner;
        public Camera Camera => _camera;
        /// <summary>True while the player is in the air (free jump or falling).</summary>
        public bool IsAirborne => _player != null && !IsGrounded;

        // CharacterController.isGrounded flickers from frame to frame; treat "touched the ground
        // in the last 0.12 s" as grounded so a jump press is never lost ("coyote time").
        const float GroundedGraceSeconds = 0.12f;
        float _lastGroundedAt = -10f;
        bool IsGrounded => Time.time - _lastGroundedAt <= GroundedGraceSeconds;
        /// <summary>False when no Universal (3D) renderer was found: models then render without lighting.</summary>
        public bool Has3DRenderer { get; private set; } = true;

        AccessibilityProfile _profile;
        CalibrationRunner _runner;
        DemoHud _hud;
        DemoInput _input;
        Camera _camera;

        CharacterController _player;
        SimpleAnimator _anim;
        Transform _playerModel;
        float _verticalSpeed;
        Vector3 _checkpoint;
        float _levelTime;
        bool _timerRunning;

        // Level pieces that react to the stations.
        Transform _ball, _leverHandle, _door, _bridgePivot;
        Vector3 _doorClosed;
        float _ballRadius;
        Collider _doorBlock, _gapWall, _bridgeCollider;
        readonly List<Transform> _spinners = new List<Transform>();

        // Station state.
        bool _qteDone, _leverDone, _bridgeDone, _pathHintShown;
        float _qteClock, _qteArrival, _qteBallStartZ;
        bool _qteJumping, _qteMissed, _qteCommitted;
        float _doorHeight;
        float _jumpClock;
        Vector3 _jumpFrom;
        float _progress;
        bool _toggledOn;

        // ================================================================ setup

        void Awake()
        {
            _input = new DemoInput();
            SetUpWorld();
            BuildLevel();
            SpawnPlayer();

            var runnerGo = new GameObject("AccessCalibrate Runner");
            runnerGo.transform.SetParent(transform, false);
            _runner = runnerGo.AddComponent<CalibrationRunner>();
            _runner.Completed += _ => OnCalibrationClosed();

            _hud = DemoHud.Create(transform);
            _hud.RedoClicked += Redo;

            // Study mode: the demo is where a researcher switches it on and types the session label.
            _hud.StudyToggled += on => { StudyMode.Enabled = on; _hud.ShowStudyPath(StudyMode.FilePath); };
            _hud.StudyLabelChanged += label => StudyMode.Label = label;
            StudyMode.Recorded += OnStudyRecorded;

            AccessCalibrate.OnProfileChanged += OnProfileChanged;
            OnProfileChanged(AccessCalibrate.Profile);
            if (!Has3DRenderer)
                _hud.Toast("No Universal (3D) renderer found in the URP asset — models render without lighting.", 8f);
        }

        void Start()
        {
            // First launch → calibration. Otherwise straight into the level.
            AccessCalibrate.RunIfFirstLaunch();
        }

        void OnDestroy()
        {
            AccessCalibrate.OnProfileChanged -= OnProfileChanged;
            StudyMode.Recorded -= OnStudyRecorded;
            _input?.Dispose();
        }

        void OnProfileChanged(AccessibilityProfile p)
        {
            _profile = p;
            if (_hud != null) _hud.ShowSettings(p);
        }

        void OnCalibrationClosed() => _hud.Toast("Settings saved. Have fun!");

        void OnStudyRecorded(string path)
        {
            string label = string.IsNullOrWhiteSpace(StudyMode.Label) ? "(no label)" : StudyMode.Label;
            _hud.Toast($"Study row saved: {label}", 4f);
        }

        void Redo()
        {
            if (!IsPaused) AccessCalibrate.RequestCalibration();
        }

        bool IsPaused => _runner.IsCalibrating || _runner.IsReviewing;

        void SetUpWorld()
        {
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.78f, 0.86f, 1f);
            RenderSettings.ambientEquatorColor = new Color(0.62f, 0.68f, 0.72f);
            RenderSettings.ambientGroundColor = new Color(0.35f, 0.32f, 0.28f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.62f, 0.8f, 0.97f);
            RenderSettings.fogStartDistance = 30f;
            RenderSettings.fogEndDistance = 80f;

            var camGo = new GameObject("Demo Camera");
            camGo.transform.SetParent(transform, false);
            _camera = camGo.AddComponent<Camera>();
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(0.62f, 0.8f, 0.97f);
            _camera.fieldOfView = 55f;
            _camera.nearClipPlane = 0.1f;
            _camera.farClipPlane = 200f;
            camGo.tag = "MainCamera";
            camGo.AddComponent<AudioListener>();
            Use3DRenderer(_camera);

            var sunGo = new GameObject("Sun");
            sunGo.transform.SetParent(transform, false);
            sunGo.transform.rotation = Quaternion.Euler(50f, -35f, 0f);
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.25f;
            sun.color = new Color(1f, 0.96f, 0.88f);
            sun.shadows = LightShadows.Soft;
        }

        /// <summary>
        /// The project's default renderer may be the 2D Renderer, which doesn't light 3D models.
        /// Pick a Universal (3D) renderer from the URP asset's list for this camera only.
        /// </summary>
        void Use3DRenderer(Camera cam)
        {
#if ACCESSCALIBRATE_URP
            var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (urp == null) return; // built-in pipeline: nothing to choose
            var list = urp.rendererDataList;
            for (int i = 0; i < list.Length; i++)
            {
                if (list[i] is UniversalRendererData)
                {
                    cam.GetUniversalAdditionalCameraData().SetRenderer(i);
                    return;
                }
            }
            Has3DRenderer = false;
#endif
        }

        // ================================================================ level

        Transform _level;

        void BuildLevel()
        {
            _level = new GameObject("Level").transform;
            _level.SetParent(transform, false);

            // Solid ground: start → lever → edge of the gap.
            Ground(-2f, GapStartZ, -3f, 3f);
            // After the gap, then the narrow path (1 m wide), then the goal platform.
            Ground(GapEndZ, PathStartZ, -3f, 3f);
            Ground(PathStartZ, PathEndZ, -0.5f, 0.5f, tile: 1f);
            Ground(PathEndZ, GoalZ + 4f, -3f, 3f);

            // 1. Quick-time: a spiky ball rolls in later (spawned by the station).
            // 2. Lever + door.
            var lever = DemoAssets.Spawn("Lever", new Vector3(1.4f, 0f, LeverTriggerZ + 0.6f), 1.1f, _level, byHeight: true, yaw: -90f);
            _leverHandle = lever.transform;
            var door = DemoAssets.Spawn("Door", new Vector3(0f, 0f, DoorZ), 3.6f, _level, yaw: 0f, fallbackColor: new Color(0.55f, 0.35f, 0.2f));
            _door = door.transform;
            _doorClosed = _door.position;
            _doorHeight = DemoAssets.Bounds(door).size.y;
            _doorBlock = Wall(new Vector3(0, 1.5f, DoorZ), new Vector3(6f, 3f, 0.4f));
            DemoAssets.Spawn("Fence_Middle", new Vector3(-2.4f, 0, DoorZ), 1.2f, _level);
            DemoAssets.Spawn("Fence_Middle", new Vector3(2.4f, 0, DoorZ), 1.2f, _level);

            // 3. Bridge over the gap, hinged at the near edge, starts raised.
            var pivot = new GameObject("Bridge Pivot").transform;
            pivot.SetParent(_level, false);
            pivot.position = new Vector3(0, 0, GapStartZ);
            var bridge = DemoAssets.Spawn("Bridge_Small", new Vector3(0, -0.15f, (GapStartZ + GapEndZ) / 2f), GapEndZ - GapStartZ + 0.6f, pivot,
                                          yaw: 90f, fallbackColor: new Color(0.6f, 0.4f, 0.25f));
            _bridgePivot = pivot;
            var bridgeCol = new GameObject("Bridge Collider");
            bridgeCol.transform.SetParent(pivot, false);
            bridgeCol.transform.position = new Vector3(0, -0.25f, (GapStartZ + GapEndZ) / 2f);
            var bc = bridgeCol.AddComponent<BoxCollider>();
            bc.size = new Vector3(2.4f, 0.5f, GapEndZ - GapStartZ + 0.4f);
            _bridgeCollider = bc;
            _gapWall = Wall(new Vector3(0, 1.5f, GapStartZ + 0.2f), new Vector3(6f, 3f, 0.4f));

            // 4. Spikes far below the narrow path (decoration — falling only sends you back).
            for (float z = PathStartZ + 1f; z < PathEndZ; z += 2.5f)
            {
                DemoAssets.Spawn("Spikes", new Vector3(-2.2f, -4f, z), 1.6f, _level);
                DemoAssets.Spawn("Spikes", new Vector3(2.2f, -4f, z + 1.2f), 1.6f, _level);
            }

            // Goal.
            DemoAssets.Spawn("Goal_Flag", new Vector3(0, 0, GoalZ + 1.5f), 2.6f, _level, byHeight: true, fallback: PrimitiveType.Cylinder);

            // Coins along the way (they just spin — a little life).
            foreach (var z in new[] { 3f, 5f, 7f, 15f, 17f, 35.5f, 40f, 44f, 48f })
            {
                var coin = DemoAssets.Spawn("Coin", new Vector3(0, 0.8f, z), 0.6f, _level, byHeight: true, fallback: PrimitiveType.Cylinder,
                                            fallbackColor: new Color(1f, 0.8f, 0.1f));
                _spinners.Add(coin.transform);
            }
            var star = DemoAssets.Spawn("Star", new Vector3(0, 3.2f, GoalZ + 1.5f), 0.9f, _level, byHeight: true);
            _spinners.Add(star.transform);

            // Scenery.
            var rng = new System.Random(7);
            for (int i = 0; i < 14; i++)
            {
                float side = i % 2 == 0 ? -1f : 1f;
                float z = -2f + i * 4.2f;
                bool onGround = z < GapStartZ || (z > GapEndZ && z < PathStartZ) || z > PathEndZ;
                if (!onGround) continue;
                string model = i % 3 == 0 ? "Tree" : i % 3 == 1 ? "Bush" : "Rock_1";
                float size = model == "Tree" ? 3.2f : model == "Bush" ? 1.2f : 0.9f;
                float x = side * (2.3f + (float)rng.NextDouble() * 0.3f);
                DemoAssets.Spawn(model, new Vector3(x, 0, z), size, _level, byHeight: true, yaw: (float)rng.NextDouble() * 360f);
            }
            for (int i = 0; i < 6; i++)
            {
                var cloud = DemoAssets.Spawn("Cloud_1", new Vector3(-14f + i * 6f, 9f + (i % 3), 10f + i * 9f), 4f, _level);
                cloud.transform.localScale *= 1f + (i % 2) * 0.4f;
            }
        }

        /// <summary>A walkable floor from z0 to z1 (top at y = 0), tiled with grass cubes, with one box collider.</summary>
        void Ground(float z0, float z1, float x0, float x1, float tile = 2f)
        {
            for (float z = z0 + tile / 2f; z < z1; z += tile)
            for (float x = x0 + tile / 2f; x < x1 + 0.001f; x += tile)
            {
                var t = DemoAssets.Spawn("Cube_Grass_Center", new Vector3(x, 0, z), tile, _level, fallbackColor: new Color(0.35f, 0.7f, 0.3f));
                float h = DemoAssets.Bounds(t).size.y;
                t.transform.position -= new Vector3(0, h, 0);
            }
            var col = new GameObject("Ground Collider");
            col.transform.SetParent(_level, false);
            col.transform.position = new Vector3((x0 + x1) / 2f, -0.5f, (z0 + z1) / 2f);
            col.AddComponent<BoxCollider>().size = new Vector3(x1 - x0, 1f, z1 - z0);
        }

        Collider Wall(Vector3 centre, Vector3 size)
        {
            var go = new GameObject("Invisible Wall");
            go.transform.SetParent(_level, false);
            go.transform.position = centre;
            var c = go.AddComponent<BoxCollider>();
            c.size = size;
            return c;
        }

        void SpawnPlayer()
        {
            var go = new GameObject("Player");
            go.transform.SetParent(transform, false);
            go.transform.position = new Vector3(0, 0.05f, 0);
            _player = go.AddComponent<CharacterController>();
            _player.height = 1.6f;
            _player.radius = 0.35f;
            _player.center = new Vector3(0, 0.82f, 0);
            _player.stepOffset = 0.3f;
            // Default 0.001 drops any move smaller than 1 mm per frame, which at high frame rates
            // silently cuts jumps short. Unity recommends 0.
            _player.minMoveDistance = 0f;

            var model = DemoAssets.Spawn("Character", go.transform.position, 1.6f, go.transform, byHeight: true,
                                         fallback: PrimitiveType.Capsule, fallbackColor: new Color(0.2f, 0.5f, 1f));
            _playerModel = model.transform;
            _anim = model.AddComponent<SimpleAnimator>();
            _anim.Init(DemoAssets.Clips("Character"));
            _anim.Play("Idle");
            _checkpoint = go.transform.position;
            SnapCamera();
        }

        // ================================================================ frame

        void Update()
        {
            bool paused = IsPaused;
            _hud.SetVisible(!paused);
            foreach (var s in _spinners) s.Rotate(0, 90f * Time.deltaTime, 0, Space.World);
            if (paused) return;
            if (_hud.IsTyping) { UpdateCamera(Time.deltaTime); return; }   // keys go to the label field

            if (_input.Redo.WasPressedThisFrame()) { Redo(); return; }
            if (_timerRunning) _levelTime += Time.deltaTime;

            switch (CurrentPhase)
            {
                case Phase.Explore: UpdateExplore(); break;
                case Phase.Qte: UpdateQte(); break;
                case Phase.Lever: UpdateLever(); break;
                case Phase.Bridge: UpdateBridge(); break;
                case Phase.Done: UpdateDone(); break;
            }
            UpdateCamera(Time.deltaTime);
        }

        void UpdateExplore()
        {
            // Free jump with A / Space anywhere while exploring (separate from the quick-time event).
            if (IsGrounded && _input.Action.WasPressedThisFrame())
            {
                _verticalSpeed = Mathf.Sqrt(2f * -Gravity * FreeJumpHeight);
                _lastGroundedAt = -10f;   // no second jump from the grace window
                _anim.Play("Jump", loop: false, restart: true);
                _timerRunning = true;
            }

            Vector2 move = _input.ReadMove(_profile.stickDeadzone);
            MovePlayer(new Vector3(move.x, 0, move.y) * MoveSpeed);
            if (move.sqrMagnitude > 0.01f) _timerRunning = true;

            // Stations start only on the ground, so a jump never lands you mid-air in one.
            float z = _player.transform.position.z;
            if (IsGrounded)
            {
                if (!_qteDone && z >= QteTriggerZ) StartQte();
                else if (_qteDone && !_leverDone && z >= LeverTriggerZ) StartStation(Phase.Lever);
                else if (_leverDone && !_bridgeDone && z >= BridgeTriggerZ) StartStation(Phase.Bridge);
                else if (_bridgeDone && z >= GoalZ) Finish();
            }

            bool onPath = _bridgeDone && z >= PathStartZ - 0.5f && z <= PathEndZ;
            if (onPath)
            {
                _hud.SetPrompt("Narrow path", -1f,
                    $"Stick deadzone {_profile.stickDeadzone:0.###}: smaller stick movements are ignored");
                _pathHintShown = true;
            }
            else if (_pathHintShown)
            {
                _hud.SetPrompt(null);
                _pathHintShown = false;
            }

            if (_player.transform.position.y < -4f) Respawn();
        }

        void MovePlayer(Vector3 horizontal)
        {
            if (_player.isGrounded && _verticalSpeed < 0) _verticalSpeed = -1f;
            _verticalSpeed += Gravity * Time.deltaTime;
            _player.Move((horizontal + Vector3.up * _verticalSpeed) * Time.deltaTime);
            if (_player.isGrounded) _lastGroundedAt = Time.time;

            if (horizontal.sqrMagnitude > 0.05f)
                _playerModel.rotation = Quaternion.Slerp(_playerModel.rotation,
                    Quaternion.LookRotation(new Vector3(horizontal.x, 0, horizontal.z)), 12f * Time.deltaTime);

            if (!IsGrounded) _anim.Play("Jump", loop: false);   // keeps the jump pose in the air
            else if (horizontal.sqrMagnitude > 0.05f) _anim.Play("Run");
            else _anim.Play("Idle");
        }

        void FaceForward()
        {
            _playerModel.rotation = Quaternion.LookRotation(Vector3.forward);
            _anim.Play("Idle");
        }

        void Respawn()
        {
            _player.enabled = false;
            _player.transform.position = _checkpoint;
            _player.enabled = true;
            _verticalSpeed = 0;
            _hud.Toast("No problem — back you go.");
        }

        void SnapCamera()
        {
            Vector3 p = _player.transform.position;
            _camera.transform.position = p + new Vector3(0, 5.5f, -7.5f);
            _camera.transform.rotation = Quaternion.LookRotation(p + new Vector3(0, 1f, 3.5f) - _camera.transform.position);
        }

        void UpdateCamera(float dt)
        {
            Vector3 p = _player.transform.position;
            Vector3 want = p + new Vector3(0, 5.5f, -7.5f);
            _camera.transform.position = Vector3.Lerp(_camera.transform.position, want, 1f - Mathf.Exp(-6f * dt));
            _camera.transform.rotation = Quaternion.LookRotation(p + new Vector3(0, 1f, 3.5f) - _camera.transform.position);
        }

        // ================================================================ 1. quick-time

        void StartQte()
        {
            CurrentPhase = Phase.Qte;
            FaceForward();
            const float ballSpeed = 6f;
            _qteBallStartZ = _player.transform.position.z + 15f;
            _qteArrival = (_qteBallStartZ - _player.transform.position.z - 0.6f) / ballSpeed;
            _qteClock = 0;
            _qteJumping = _qteMissed = _qteCommitted = false;
            if (_ball == null)
            {
                // Wrap the model in a pivot at its centre so it can roll in place.
                var model = DemoAssets.Spawn("SpikyBall", Vector3.zero, 1.1f, _level,
                                             fallback: PrimitiveType.Sphere, fallbackColor: new Color(0.6f, 0.2f, 0.2f));
                var b = DemoAssets.Bounds(model);
                _ballRadius = b.extents.y;
                _ball = new GameObject("Rolling Ball").transform;
                _ball.SetParent(_level, false);
                _ball.position = b.center;
                model.transform.SetParent(_ball, true);
            }
            _ball.gameObject.SetActive(true);
            PlaceBall(0);
        }

        void PlaceBall(float t)
        {
            const float ballSpeed = 6f;
            _ball.position = new Vector3(0, _ballRadius, _qteBallStartZ - ballSpeed * t);
            _ball.rotation = Quaternion.Euler(-ballSpeed * t / _ballRadius * Mathf.Rad2Deg, 0, 0);
        }

        void UpdateQte()
        {
            _qteClock += Time.deltaTime;
            PlaceBall(_qteClock);
            float window = DemoMechanics.QteWindow(_profile);
            float opens = _qteArrival - window;

            if (_qteJumping) { UpdateJump(); return; }

            // Once the press was on time (or the event is skipped / off), the game performs the
            // jump at the right moment to clear the ball — like any quick-time event.
            if (_qteCommitted || DemoMechanics.QteIsAutomatic(_profile))
            {
                if (!_qteCommitted) _hud.SetPrompt("Quick-time events are off", -1f, "Jumping for you…");
                if (_qteClock >= _qteArrival - 0.45f) BeginJump();
                return;
            }

            string skipHint = DemoMechanics.QteCanBeSkipped(_profile) ? "Or skip it: Y / Tab" : null;
            if (skipHint != null && _input.SkipQte.WasPressedThisFrame()) { Commit("Skipped"); return; }

            if (_qteClock < opens)
            {
                _hud.SetPrompt("Get ready…", -1f, skipHint ?? $"Jump window: {window:0.0} s");
            }
            else if (_qteClock <= _qteArrival)
            {
                float left = (_qteArrival - _qteClock) / window;
                _hud.SetPrompt("Jump! A / Space", left, skipHint ?? $"Jump window: {window:0.0} s");
                if (_input.Action.WasPressedThisFrame()) Commit("Nice!");
            }
            else if (!_qteMissed)
            {
                _qteMissed = true;
                _anim.Play("HitReact", loop: false);
                _hud.SetPrompt(null);
                _hud.Toast("Missed it — no problem, let's try again.");
            }
            else if (_qteClock > _qteArrival + 1.6f)
            {
                StartQte(); // calm retry
            }
        }

        void Commit(string message)
        {
            _qteCommitted = true;
            _hud.SetPrompt(null);
            _hud.Toast(message, 1.5f);
            _anim.Play("Yes", loop: false);
        }

        void BeginJump()
        {
            _qteJumping = true;
            _jumpClock = 0;
            _jumpFrom = _player.transform.position;
            _player.enabled = false;
            _anim.Play("Jump", loop: false);
            _hud.SetPrompt(null);
        }

        void UpdateJump()
        {
            const float duration = 0.9f, height = 1.7f;
            _jumpClock += Time.deltaTime;
            float t = Mathf.Clamp01(_jumpClock / duration);
            _player.transform.position = _jumpFrom + new Vector3(0, 4f * height * t * (1f - t), 1.2f * t);
            if (t < 1f) return;

            _player.enabled = true;
            _qteJumping = false;
            _qteDone = true;
            _ball.gameObject.SetActive(false);
            _checkpoint = _player.transform.position;
            CurrentPhase = Phase.Explore;
        }

        // ================================================================ 2. lever, 3. bridge

        void StartStation(Phase phase)
        {
            CurrentPhase = phase;
            _progress = 0;
            _toggledOn = false;
            FaceForward();
            _checkpoint = _player.transform.position;
        }

        void UpdateLever()
        {
            MovePlayer(Vector3.zero);
            bool needsHolding = DemoMechanics.HoldNeedsHolding(_profile);
            float rate = 1f / DemoMechanics.LeverHoldSeconds;

            if (needsHolding)
            {
                bool held = _input.Use.IsPressed();
                _progress = Mathf.Clamp01(_progress + (held ? rate : -rate) * Time.deltaTime);
                _hud.SetPrompt("Hold X / E to pull the lever", _progress, $"Keep holding for {DemoMechanics.LeverHoldSeconds:0} s");
            }
            else
            {
                if (_input.Use.WasPressedThisFrame()) _toggledOn = !_toggledOn;
                if (_toggledOn) _progress = Mathf.Clamp01(_progress + rate * Time.deltaTime);
                _hud.SetPrompt(_toggledOn ? "Pulling the lever…" : "Press X / E to pull the lever", _progress,
                               "Hold to toggle is on: one press is enough (press again to stop)");
            }

            _leverHandle.localRotation = Quaternion.Euler(0, -90f, Mathf.Lerp(0, -50f, _progress));
            if (_progress < 1f) return;

            _leverDone = true;
            _doorBlock.enabled = false;
            StartCoroutine(Slide(_door, Vector3.down * (_doorHeight + 0.2f), 1.2f));  // sinks into the ground
            _hud.SetPrompt(null);
            _hud.Toast("The door is open!");
            CurrentPhase = Phase.Explore;
        }

        void UpdateBridge()
        {
            MovePlayer(Vector3.zero);
            var mode = DemoMechanics.Mashing(_profile);
            switch (mode)
            {
                case MashingMode.Normal:
                    if (_input.Action.WasPressedThisFrame())
                    {
                        _progress += 1f / DemoMechanics.BridgePresses;
                        _anim.Play("Punch", loop: false, restart: true);
                    }
                    _progress = Mathf.Clamp01(_progress - 0.12f * Time.deltaTime);
                    _hud.SetPrompt("Tap A / Space quickly to lower the bridge", _progress,
                                   $"About {DemoMechanics.BridgePresses} taps");
                    break;
                case MashingMode.HoldInstead:
                    if (_input.Action.IsPressed()) _progress += Time.deltaTime / DemoMechanics.BridgeHoldSeconds;
                    _hud.SetPrompt("Hold A / Space to lower the bridge", _progress,
                                   "Rapid presses → hold instead");
                    break;
                default:
                    _progress += Time.deltaTime / DemoMechanics.BridgeHoldSeconds;
                    _hud.SetPrompt("The bridge is lowering by itself", _progress, "Rapid presses: automatic");
                    break;
            }
            _progress = Mathf.Clamp01(_progress);
            _bridgePivot.localRotation = Quaternion.Euler(Mathf.Lerp(-80f, 0f, _progress), 0, 0);
            if (_progress < 1f) return;

            _bridgeDone = true;
            _gapWall.enabled = false;
            _hud.SetPrompt(null);
            _hud.Toast("The bridge is down!");
            _checkpoint = new Vector3(0, 0.05f, GapEndZ + 1.5f);
            CurrentPhase = Phase.Explore;
        }

        System.Collections.IEnumerator Slide(Transform t, Vector3 by, float seconds)
        {
            Vector3 from = t.position, to = from + by;
            for (float e = 0; e < seconds; e += Time.deltaTime)
            {
                t.position = Vector3.Lerp(from, to, Mathf.SmoothStep(0, 1, e / seconds));
                yield return null;
            }
            t.position = to;
        }

        // ================================================================ goal

        void Finish()
        {
            CurrentPhase = Phase.Done;
            _timerRunning = false;
            _playerModel.rotation = Quaternion.LookRotation(Vector3.back);
            _anim.Play("Wave");
        }

        void UpdateDone()
        {
            MovePlayer(Vector3.zero);
            int s = Mathf.RoundToInt(_levelTime);
            _hud.SetPrompt($"Level complete! {s / 60}:{s % 60:00}", -1f, "Play again: A / Space   ·   Redo calibration: Start / F1");
            if (_input.Action.WasPressedThisFrame()) Restart();
        }

        /// <summary>Back to the start with every station reset.</summary>
        public void Restart()
        {
            _qteDone = _leverDone = _bridgeDone = false;
            _doorBlock.enabled = true;
            _gapWall.enabled = true;
            StopAllCoroutines();
            _door.position = _doorClosed;
            _bridgePivot.localRotation = Quaternion.Euler(-80f, 0, 0);
            _leverHandle.localRotation = Quaternion.Euler(0, -90f, 0);
            _checkpoint = new Vector3(0, 0.05f, 0);
            Respawn();
            _levelTime = 0;
            _timerRunning = false;
            _hud.SetPrompt(null);
            CurrentPhase = Phase.Explore;
        }

        /// <summary>For tests and screenshots: put the player at a station (Explore = the narrow path).</summary>
        public void TeleportBefore(Phase station)
        {
            // Just past the trigger line, so the station starts on the next frame.
            float z = station == Phase.Qte ? QteTriggerZ + 0.05f
                    : station == Phase.Lever ? LeverTriggerZ + 0.05f
                    : station == Phase.Bridge ? BridgeTriggerZ + 0.05f
                    : PathStartZ + 2f;
            _qteDone = station != Phase.Qte;
            _leverDone = station == Phase.Bridge || station == Phase.Explore;
            _bridgeDone = station == Phase.Explore;
            if (_qteDone && _ball != null) _ball.gameObject.SetActive(false);
            if (_leverDone) { _doorBlock.enabled = false; _door.position = _doorClosed + Vector3.down * (_doorHeight + 0.2f); }
            if (_bridgeDone) { _gapWall.enabled = false; _bridgePivot.localRotation = Quaternion.identity; }
            _checkpoint = new Vector3(0, 0.05f, z);
            _player.enabled = false;
            _player.transform.position = _checkpoint;
            _player.enabled = true;
            SnapCamera();
            _hud.SetPrompt(null);
            _hud.ClearToast();
            CurrentPhase = Phase.Explore;
        }
    }
}
