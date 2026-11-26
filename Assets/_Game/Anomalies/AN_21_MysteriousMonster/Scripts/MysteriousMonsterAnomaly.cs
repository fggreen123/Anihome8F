using System.Collections.Generic;
using UnityEngine;

namespace Anihome.Dormitory
{
    [DisallowMultipleComponent]
    public sealed class MysteriousMonsterAnomaly : FloorAnomaly
    {
        public override int Number => 21;

        [SerializeField] GameObject monsterPrefab;
        [SerializeField] Transform corridorStart;
        [SerializeField] Transform corridorEnd;
        [SerializeField] Transform farStairs;
        [SerializeField] AudioClip footstep;
        [SerializeField] AudioClip sting;

        const float ScareSeconds = 1.18f;
        const float ImpactTime = 0.37f;
        const float RunSpeed = 2.82f;
        const float StrideLength = 1.55f;
        const int ApproachHistorySize = 18;
        const float RetreatPause = 1.25f;

        enum Bone
        {
            LeftThigh, RightThigh, LeftShin, RightShin,
            LeftArm, RightArm, RightForearm, Spine, Head
        }

        GameObject monster;
        Camera playerCamera;
        DormitoryWalkthrough walker;
        AudioSource steps;
        Transform head;
        Transform hips;
        Vector3 hipsRest;
        Transform[] limbs;
        Quaternion[] restPose;
        Vector3[] swingAxes;
        Vector3[] stairRoute;
        int nextPoint;
        float gaitDistance;
        float chaseSpeed;
        float gaitStrength;
        int footfall;
        float scareTime;
        bool capturing;
        bool impactPlayed;
        Vector3 scareOrigin;
        Quaternion cameraStart;
        float cameraStartFov;
        Light captureLight;
        Texture2D vignette;
        readonly MonsterChasePath chasePath = new MonsterChasePath();
        readonly List<(Light light, float intensity)> chaseLighting = new List<(Light, float)>();
        Light silhouetteLight;
        float lightingStarted;
        readonly List<Vector3> approachHistory = new List<Vector3>(ApproachHistorySize);
        readonly Collider[] doorOverlap = new Collider[12];
        DoorInteractable blockedDoor;
        DoorInteractable recoveringDoor;
        DoorInteractable bakedDoor;
        float blockedTime;
        float retryDoorAt;
        int retreatPoint = -1;
        Material bladeMaterial;
        Material gripMaterial;

        protected override bool OnBeginFloor(AnomalyContext context)
        {
            if (!context.Player || !monsterPrefab || !corridorStart || !corridorEnd || !farStairs)
                return false;

            walker = context.Player.GetComponent<DormitoryWalkthrough>();
            playerCamera = context.Player.GetComponentInChildren<Camera>();
            if (!playerCamera || !monsterPrefab.TryGetComponent(out Animator rig) ||
                !rig.avatar || !rig.avatar.isHuman || !rig.avatar.isValid)
                return false;

            // Local points follow the ascending flight of the mirrored stair module.
            stairRoute = new[]
            {
                farStairs.TransformPoint(new Vector3(-0.43f, 1.02f, 2.65f)),
                farStairs.TransformPoint(new Vector3(-0.47f, 0f, 1.02f)),
                farStairs.TransformPoint(new Vector3(-0.48f, 0f, 0.48f)),
                AtFloorHeight(corridorEnd.position)
            };
            return true;
        }

        void Update()
        {
            if (!CanTrigger || (walker && !walker.enabled)) return;

            if (!monster)
            {
                Vector3 axis = Vector3.ProjectOnPlane(corridorEnd.position - corridorStart.position, Vector3.up).normalized;
                Vector3 offset = Vector3.ProjectOnPlane(
                    Context.Player.position - (corridorStart.position + corridorEnd.position) * 0.5f, Vector3.up);
                float along = Vector3.Dot(offset, axis);
                if (Mathf.Abs(along) > 1.15f || (offset - axis * along).sqrMagnitude > 1.25f * 1.25f) return;
                RebuildChasePath();
                BeginChaseLighting();
                Spawn();
            }

            Transform body = monster.transform;
            bool onStairs = nextPoint < stairRoute.Length;
            chaseSpeed = Mathf.MoveTowards(chaseSpeed, onStairs ? 1.95f : RunSpeed, 4f * Time.deltaTime);
            Vector3 previous = body.position;
            if (onStairs)
            {
                MoveToward(body, stairRoute[nextPoint], chaseSpeed);
                if (body.position == stairRoute[nextPoint]) nextPoint++;
            }
            else
            {
                UpdateChaseMovement(body);
            }
            float dim = Mathf.SmoothStep(1f, 0.43f, Mathf.Clamp01((Time.time - lightingStarted) / 0.65f));
            foreach (var entry in chaseLighting)
                if (entry.light) entry.light.intensity = entry.intensity * dim;

            float traveled = Vector3.Distance(previous, body.position);
            gaitDistance += traveled;
            gaitStrength = Mathf.MoveTowards(gaitStrength, traveled > 0f ? 1f : 0f, 7f * Time.deltaTime);
            float cycle = gaitDistance / StrideLength;
            PoseRun(cycle, gaitStrength, Mathf.InverseLerp(1.95f, RunSpeed, chaseSpeed));

            int strike = Mathf.FloorToInt(cycle * 2f);
            if (strike != footfall)
            {
                footfall = strike;
                steps.pitch = strike % 2 == 0 ? 0.77f : 0.70f;
                if (footstep) steps.PlayOneShot(footstep, onStairs ? 0.68f : 0.91f);
            }

            Vector3 playerOffset = Context.Player.position - body.position;
            if (!onStairs && !recoveringDoor && Mathf.Abs(playerOffset.y) < 0.85f &&
                Vector3.ProjectOnPlane(playerOffset, Vector3.up).sqrMagnitude <= 0.8f * 0.8f &&
                IsPathClear(body.position + Vector3.up * 1.1f, playerCamera.transform.position) &&
                Context.Loop.RequestCapture(ScareSeconds))
            {
                BeginScare(body);
            }
        }

        Vector3 AtFloorHeight(Vector3 point) => new Vector3(point.x, farStairs.position.y, point.z);

        void UpdateChaseMovement(Transform body)
        {
            if (recoveringDoor)
            {
                if (retreatPoint >= 0)
                {
                    Vector3 retreat = approachHistory[retreatPoint];
                    MoveToward(body, retreat, 1.65f);
                    if ((body.position - retreat).sqrMagnitude < 0.001f) retreatPoint--;
                    if (retreatPoint < 0) retryDoorAt = Time.time + RetreatPause;
                    return;
                }
                if (Time.time < retryDoorAt || recoveringDoor.IsMoving) return;

                // Route around the settled leaf if it is still open. A closed door
                // remains a traversable portal that can be opened on a later approach.
                RebuildChasePath(recoveringDoor.IsOpen ? recoveringDoor : null);
                recoveringDoor = null;
                approachHistory.Clear();
            }
            if (bakedDoor && (!bakedDoor.IsOpen || bakedDoor.IsMoving)) RebuildChasePath();

            Vector3 position = body.position;
            if (approachHistory.Count == 0 || (position - approachHistory[approachHistory.Count - 1]).sqrMagnitude > 0.01f)
            {
                if (approachHistory.Count == ApproachHistorySize) approachHistory.RemoveAt(0);
                approachHistory.Add(position);
            }
            Vector3 target = chasePath.NextPoint(position, Context.Player.position, bakedDoor);
            Vector3 direction = Vector3.ProjectOnPlane(target - position, Vector3.up);
            float step = chaseSpeed * Time.deltaTime;
            if (CanAdvance(position, direction, step, out var door))
            {
                blockedDoor = null;
                blockedTime = 0f;
                MoveToward(body, target, chaseSpeed);
                return;
            }
            if (!door)
            {
                blockedDoor = null;
                blockedTime = 0f;
                return;
            }
            if (blockedDoor != door)
            {
                blockedDoor = door;
                blockedTime = 0f;
            }
            blockedTime += Time.deltaTime;
            if (blockedTime < (door.IsMoving ? 0.68f : 0.33f) || approachHistory.Count < 2) return;

            recoveringDoor = door;
            retreatPoint = approachHistory.Count - 2;
            blockedDoor = null;
            blockedTime = 0f;
        }

        void RebuildChasePath(DoorInteractable obstructingDoor = null)
        {
            bakedDoor = obstructingDoor;
            chasePath.Build(Context.Player, (corridorStart.position + corridorEnd.position) * 0.5f,
                farStairs.position.y, obstructingDoor);
        }

        static void MoveToward(Transform body, Vector3 target, float speed)
        {
            Vector3 direction = Vector3.ProjectOnPlane(target - body.position, Vector3.up);
            if (direction.sqrMagnitude > 0.0001f)
                body.rotation = Quaternion.RotateTowards(body.rotation, Quaternion.LookRotation(direction), 720f * Time.deltaTime);
            body.position = Vector3.MoveTowards(body.position, target, speed * Time.deltaTime);
        }

        bool CanAdvance(Vector3 position, Vector3 direction, float step, out DoorInteractable door)
        {
            door = null;
            Vector3 chest = position + Vector3.up * 0.86f;
            // A swinging leaf can already overlap the monster, which a sphere cast alone misses.
            int overlaps = Physics.OverlapSphereNonAlloc(chest, 0.14f, doorOverlap,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < overlaps; i++)
            {
                door = doorOverlap[i].GetComponentInParent<DoorInteractable>();
                if (door) return false;
            }
            if (direction.sqrMagnitude < 0.0001f) return true;
            float probe = Mathf.Min(direction.magnitude + 0.12f, 0.90f);
            if (!Physics.SphereCast(chest, 0.14f, direction.normalized,
                    out RaycastHit hit, probe, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) ||
                hit.transform.IsChildOf(Context.Player))
                return true;

            door = hit.collider.GetComponentInParent<DoorInteractable>();
            if (door && door.isActiveAndEnabled && !door.locked && !door.IsOpen && !door.IsMoving)
                door.SetOpen(true);
            return hit.distance > step + 0.025f;
        }

        void BeginChaseLighting()
        {
            lightingStarted = Time.time;
            foreach (var light in FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (light.gameObject.scene != gameObject.scene || !light.enabled ||
                    light.name == "Fire_Alarm_Light" ||
                    light.name.IndexOf("Emergency", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                    light.name.IndexOf("Exit", System.StringComparison.OrdinalIgnoreCase) >= 0) continue;
                chaseLighting.Add((light, light.intensity));
            }
        }

        bool IsPathClear(Vector3 from, Vector3 to)
        {
            return !Physics.Linecast(from, to, out RaycastHit hit, Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore) || hit.transform == Context.Player ||
                hit.transform.IsChildOf(Context.Player);
        }

        void Spawn()
        {
            Vector3 facing = Vector3.ProjectOnPlane(stairRoute[1] - stairRoute[0], Vector3.up);
            monster = Instantiate(monsterPrefab, stairRoute[0], Quaternion.LookRotation(facing), transform);
            monster.transform.localScale *= 0.9f;
            foreach (Collider collider in monster.GetComponentsInChildren<Collider>()) collider.enabled = false;
            foreach (SkinnedMeshRenderer skin in monster.GetComponentsInChildren<SkinnedMeshRenderer>())
                skin.updateWhenOffscreen = true;

            var rig = monster.GetComponent<Animator>();
            head = rig.GetBoneTransform(HumanBodyBones.Head);
            hips = rig.GetBoneTransform(HumanBodyBones.Hips);
            if (hips) hipsRest = hips.localPosition;
            Transform hand = rig.GetBoneTransform(HumanBodyBones.RightHand);
            limbs = new[]
            {
                rig.GetBoneTransform(HumanBodyBones.LeftUpperLeg), rig.GetBoneTransform(HumanBodyBones.RightUpperLeg),
                rig.GetBoneTransform(HumanBodyBones.LeftLowerLeg), rig.GetBoneTransform(HumanBodyBones.RightLowerLeg),
                rig.GetBoneTransform(HumanBodyBones.LeftUpperArm), rig.GetBoneTransform(HumanBodyBones.RightUpperArm),
                rig.GetBoneTransform(HumanBodyBones.RightLowerArm),
                rig.GetBoneTransform(HumanBodyBones.Spine), head
            };
            if (limbs[5] && limbs[6])
                AimBone(limbs[5], limbs[6], monster.transform.TransformDirection(new Vector3(0.27f, -1f, -0.32f)));
            if (limbs[6] && hand)
                AimBone(limbs[6], hand, monster.transform.TransformDirection(new Vector3(0.10f, -0.52f, 1f)));
            Transform leftElbow = rig.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            if (limbs[4] && leftElbow)
            {
                AimBone(limbs[4], leftElbow, monster.transform.TransformDirection(new Vector3(-0.22f, -1f, -0.1f)));
                Transform leftHand = rig.GetBoneTransform(HumanBodyBones.LeftHand);
                if (leftHand)
                    AimBone(leftElbow, leftHand, monster.transform.TransformDirection(new Vector3(0.08f, 0.05f, 1f)));
            }

            restPose = new Quaternion[limbs.Length];
            swingAxes = new Vector3[limbs.Length];
            for (int i = 0; i < limbs.Length; i++)
            {
                if (!limbs[i]) continue;
                restPose[i] = limbs[i].localRotation;
                swingAxes[i] = limbs[i].InverseTransformDirection(monster.transform.right);
            }
            if (hand) EquipKnife(hand);

            steps = monster.AddComponent<AudioSource>();
            steps.spatialBlend = 1f;
            steps.minDistance = 2f;
            steps.maxDistance = 18f;
            steps.pitch = 0.72f;

            silhouetteLight = new GameObject("Grinner Backlight").AddComponent<Light>();
            silhouetteLight.transform.SetParent(monster.transform, false);
            silhouetteLight.transform.localPosition = new Vector3(0.18f, 1.24f, -0.8f);
            silhouetteLight.color = new Color(0.32f, 0.44f, 1f);
            silhouetteLight.range = 2.1f;
            silhouetteLight.intensity = 0.68f;
        }

        static void AimBone(Transform bone, Transform child, Vector3 direction)
        {
            bone.rotation = Quaternion.FromToRotation(child.position - bone.position, direction) * bone.rotation;
        }

        void PoseRun(float cycle, float strength, float charge)
        {
            float phase = cycle * (2f * Mathf.PI);
            float swing = Mathf.Cos(phase);
            float recovery = Mathf.Sin(phase);
            SetSwing(Bone.LeftThigh, (-9f - 29f * swing) * strength);
            SetSwing(Bone.RightThigh, (-9f + 29f * swing) * strength);
            SetSwing(Bone.LeftShin, (9f + 53f * Mathf.Max(0f, -recovery)) * strength);
            SetSwing(Bone.RightShin, (9f + 53f * Mathf.Max(0f, recovery)) * strength);
            SetSwing(Bone.LeftArm, (7f + 23f * swing) * strength);
            SetSwing(Bone.RightArm, (5f - 8f * swing) * strength);
            SetSwing(Bone.RightForearm, 8f * recovery * strength);
            float lean = Mathf.Lerp(12f, 26f, charge);
            SetSwing(Bone.Spine, (lean + 2f * recovery) * strength);
            SetSwing(Bone.Head, (-lean * 0.67f + 1.5f * swing) * strength);
            if (hips)
            {
                Vector3 drop = Vector3.down * ((0.035f + 0.043f * swing * swing) * strength);
                hips.localPosition = hipsRest + hips.parent.InverseTransformVector(drop);
            }
        }

        void SetSwing(Bone bone, float angle)
        {
            int index = (int)bone;
            if (limbs[index])
                limbs[index].localRotation = restPose[index] * Quaternion.AngleAxis(angle, swingAxes[index]);
        }

        void EquipKnife(Transform hand)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (!shader) return;
            bladeMaterial = new Material(shader) { color = new Color(0.62f, 0.67f, 0.71f) };
            gripMaterial = new Material(shader) { color = new Color(0.09f, 0.07f, 0.06f) };
            bladeMaterial.SetFloat("_Metallic", 0.7f);
            AddKnifePart("Knife Grip", hand, PrimitiveType.Cube, new Vector3(0f, 0f, 0.025f),
                new Vector3(0.032f, 0.034f, 0.105f), gripMaterial);
            AddKnifePart("Knife Blade", hand, PrimitiveType.Cube, new Vector3(0f, 0f, 0.16f),
                new Vector3(0.043f, 0.012f, 0.17f), bladeMaterial);
            AddKnifePart("Knife Tip", hand, PrimitiveType.Cube, new Vector3(0.005f, 0f, 0.247f),
                new Vector3(0.027f, 0.01f, 0.039f), bladeMaterial, -35f);
        }

        void AddKnifePart(string partName, Transform hand, PrimitiveType shape, Vector3 offset, Vector3 scale,
            Material material, float angle = 0f)
        {
            GameObject part = GameObject.CreatePrimitive(shape);
            part.name = partName;
            part.GetComponent<Collider>().enabled = false;
            part.GetComponent<Renderer>().sharedMaterial = material;
            part.transform.SetPositionAndRotation(hand.position + monster.transform.rotation * offset,
                monster.transform.rotation * Quaternion.Euler(0f, angle, 0f));
            part.transform.localScale = scale;
            part.transform.SetParent(hand, true);
        }

        void BeginScare(Transform body)
        {
            capturing = true;
            scareOrigin = body.position;
            cameraStart = playerCamera.transform.rotation;
            cameraStartFov = playerCamera.fieldOfView;
            steps.Stop();
            steps.spatialBlend = 0f;
            silhouetteLight.enabled = false;
            steps.pitch = 0.88f;
            if (footstep) steps.PlayOneShot(footstep, 0.86f);

            captureLight = new GameObject("Capture Face Light").AddComponent<Light>();
            captureLight.transform.SetParent(body, false);
            captureLight.transform.localPosition = new Vector3(-0.22f, 1.47f, 0.55f);
            captureLight.color = new Color(0.74f, 0.81f, 1f);
            captureLight.range = 1.2f;
            captureLight.intensity = 0.18f;
        }

        void LateUpdate()
        {
            if (!capturing || Context == null || !Context.Loop.CaptureInProgress || !monster || !playerCamera) return;
            scareTime += Time.unscaledDeltaTime;
            Transform cameraTransform = playerCamera.transform;
            Transform body = monster.transform;
            Vector3 towardMonster = Vector3.ProjectOnPlane(scareOrigin - cameraTransform.position, Vector3.up).normalized;
            if (towardMonster.sqrMagnitude < 0.01f) towardMonster = -body.forward;
            float windup = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(scareTime / 0.24f));
            float lunge = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.24f, ImpactTime, scareTime));
            float afterImpact = Mathf.Max(0f, scareTime - ImpactTime);
            float recoil = lunge * Mathf.Exp(-afterImpact * 8f);
            Vector3 pulledBack = scareOrigin + towardMonster * (0.20f * windup) - Vector3.up * (0.16f * windup);
            Vector3 close = cameraTransform.position + towardMonster * (0.43f + (1f - recoil) * 0.10f);
            close.y -= 1.61f;
            body.position = Vector3.Lerp(pulledBack, close, lunge);
            body.rotation = Quaternion.LookRotation(-towardMonster);
            float reach = windup * (1f - lunge);
            SetSwing(Bone.LeftThigh, -18f * reach);
            SetSwing(Bone.RightThigh, -11f * reach);
            SetSwing(Bone.LeftShin, 28f * reach);
            SetSwing(Bone.RightShin, 21f * reach);
            SetSwing(Bone.Spine, -19f * reach + 21f * lunge);
            SetSwing(Bone.Head, 20f * reach - 14f * lunge);
            SetSwing(Bone.LeftArm, 34f * reach - 92f * lunge);
            SetSwing(Bone.RightArm, 22f * reach - 23f * lunge);
            SetSwing(Bone.RightForearm, 19f * reach - 12f * lunge);

            if (!impactPlayed && scareTime >= ImpactTime)
            {
                impactPlayed = true;
                steps.pitch = 1f;
                if (sting) steps.PlayOneShot(sting, 0.76f);
            }
            captureLight.intensity = Mathf.Lerp(0.18f, 1.7f, lunge) + 0.55f * recoil;

            Vector3 face = head ? head.position + Vector3.up * 0.10f : body.position + Vector3.up * 1.65f;
            Vector3 look = face - cameraTransform.position;
            if (look.sqrMagnitude < 0.01f) look = towardMonster;
            cameraTransform.rotation = Quaternion.Slerp(cameraStart, Quaternion.LookRotation(look),
                Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(scareTime / 0.18f))) *
                Quaternion.Euler(-10f * recoil, 4f * recoil, 11f * recoil);
            playerCamera.fieldOfView = Mathf.Clamp(cameraStartFov - 8f * windup + 15f * recoil, 30f, 115f);
        }

        void OnGUI()
        {
            if (!capturing || Event.current.type != EventType.Repaint) return;
            if (!vignette)
            {
                const int size = 128;
                vignette = new Texture2D(size, size, TextureFormat.RGBA32, false)
                {
                    wrapMode = TextureWrapMode.Clamp,
                    hideFlags = HideFlags.HideAndDontSave
                };
                var pixels = new Color32[size * size];
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    var point = new Vector2(x, y) / (size - 1) * 2f - Vector2.one;
                    float edge = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.34f, 1.12f, point.magnitude));
                    pixels[y * size + x] = new Color32(4, 6, 10, (byte)(255f * edge));
                }
                vignette.SetPixels32(pixels);
                vignette.Apply(false, true);
            }
            
            var fullScreen = new Rect(0f, 0f, Screen.width, Screen.height);
            Color previous = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, Mathf.Lerp(0.18f, 0.97f, Mathf.Clamp01(scareTime / ImpactTime)));
            GUI.DrawTexture(fullScreen, vignette);
            float cut = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.82f, 1.03f, scareTime));
            GUI.color = new Color(0f, 0f, 0f, cut);
            GUI.DrawTexture(fullScreen, Texture2D.whiteTexture);
            GUI.color = previous;
        }

        protected override void OnResetFloor()
        {
            chasePath.Release();
            approachHistory.Clear();
            blockedDoor = recoveringDoor = bakedDoor = null;
            blockedTime = retryDoorAt = 0f;
            retreatPoint = -1;
            foreach (var entry in chaseLighting)
                if (entry.light) entry.light.intensity = entry.intensity;
            chaseLighting.Clear();
            if (capturing && playerCamera)
            {
                playerCamera.transform.rotation = cameraStart;
                playerCamera.fieldOfView = cameraStartFov;
            }
            if (monster)
            {
                monster.SetActive(false);
                Destroy(monster);
            }
            if (bladeMaterial) Destroy(bladeMaterial);
            if (gripMaterial) Destroy(gripMaterial);
            monster = null;
            playerCamera = null;
            walker = null;
            nextPoint = 0;
            gaitDistance = 0f;
            chaseSpeed = 0f;
            gaitStrength = 0f;
            footfall = 0;
            scareTime = 0f;
            capturing = false;
            impactPlayed = false;
        }

        void OnDestroy()
        {
            if (vignette) Destroy(vignette);
        }
    }
}
