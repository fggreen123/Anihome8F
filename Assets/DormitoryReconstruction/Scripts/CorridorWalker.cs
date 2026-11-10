using System.Collections.Generic;
using UnityEngine;

namespace Anihome.Dormitory
{
    /// <summary>
    /// 복도를 지나가는 사람 (8번 출구의 아저씨 같은 역할).
    ///
    /// 층이 시작되면 반대편 아래층 계단 발치(플레이어 눈에 안 보이는 곳)에서
    /// 이미 걷고 있는 상태로 나타나, 계단을 빠르게 올라와 복도를 지나고,
    /// 전자레인지 앞 계단으로 올라가 윗층에서 사라진다.
    ///
    /// 플레이어가 몸으로 밀면 밀려나지만 멈추지 않고 계속 걸으며,
    /// 손이 떨어지는 대로 원래 경로로 돌아간다.
    /// 플레이어가 앞을 막고 서 있으면 옆으로 비켜 지나가고,
    /// 양옆이 다 막혔거나 계단 위라면 그때만 멈춰서 기다린다.
    ///
    /// Animator 가 있으면 "Speed"(float), "Stairs"(bool) 값을 넣어 준다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CapsuleCollider))]
    public sealed class CorridorWalker : MonoBehaviour
    {
        [Header("경로")]
        public WalkerRoute route;
        [Tooltip("층이 시작된 뒤 출발까지 기다리는 시간 (초)")]
        [Min(0f)] public float startDelay = 0f;

        [Header("등장")]
        [Tooltip("아래층 중간참이 아니라 첫 계단 발치에서, 이미 걷는 중으로 시작한다")]
        public bool startOnStairs = true;
        [Tooltip("보이지 않는 첫 계단을 오를 때의 속도 배율. 클수록 복도에 빨리 나온다")]
        [Range(1f, 3f)] public float emergeBoost = 1.8f;

        [Header("걸음")]
        [Min(0.1f)] public float walkSpeed = 1.15f;
        [Range(0.3f, 1.2f)] public float stairSpeedScale = 0.80f;
        [Tooltip("속도가 바뀔 때의 가속 (m/s²)")]
        [Min(0.1f)] public float acceleration = 1.6f;
        [Tooltip("1 m 를 걸을 때의 걸음 주기 수 (한 주기 = 두 걸음)")]
        [Min(0.05f)] public float stridesPerMeter = 0.75f;
        [Tooltip("몸을 돌리는 속도 (도/초)")]
        [Min(10f)] public float turnSpeed = 200f;
        [Tooltip("모서리를 둥글게 도는 정도 (m)")]
        [Min(0f)] public float cornerSmoothing = 0.22f;

        [Header("몸")]
        [Min(0.05f)] public float radius = 0.25f;

        [Header("밀림")]
        [Tooltip("플레이어가 밀 때 따라 밀려나는 비율. 1 이면 플레이어 속도 그대로")]
        [Range(0f, 1f)] public float pushStrength = 0.40f;
        [Tooltip("경로에서 옆으로 최대로 밀려날 수 있는 거리 (m)")]
        [Min(0f)] public float maxPushOffset = 0.9f;
        [Tooltip("경로로 돌아가는 속도 (m/s). 밀리는 중에도 계속 돌아가려고 한다")]
        [Min(0.05f)] public float returnSpeed = 0.55f;

        [Header("비켜 가기")]
        [Tooltip("이 거리 안에서 앞을 막으면 비켜 간다 (m)")]
        [Min(0.3f)] public float lookAhead = 1.3f;
        [Tooltip("플레이어와 스쳐 지나갈 때의 여유 (m)")]
        [Min(0f)] public float passGap = 0.18f;
        [Tooltip("옆으로 비켜서는 속도 (m/s)")]
        [Min(0.05f)] public float sideStepSpeed = 0.9f;

        [Header("참조")]
        public Transform player;
        public GameObject visual;
        public Animator animator;

        public float CurrentSpeed { get; private set; }
        public float StepPhase { get; private set; }
        public bool OnStairs { get; private set; }
        public bool Finished => finished;
        public float Progress => s;

        static readonly List<CorridorWalker> All = new List<CorridorWalker>();
        static readonly int SpeedId = Animator.StringToHash("Speed");
        static readonly int StairsId = Animator.StringToHash("Stairs");

        CapsuleCollider body;
        CharacterController playerBody;
        DormitoryWalkthrough playerWalk;

        float s;
        float emergeEnd;
        Vector3 offset;          // 경로에서 옆으로 벗어난 양 (앞뒤 성분은 s 로 옮겨 담는다)
        float delayLeft;
        bool finished = true;
        Vector3 lastForward = Vector3.forward;
        bool hasSpeedParam, hasStairsParam;
        readonly RaycastHit[] hits = new RaycastHit[16];

        // ------------------------------------------------------------ 등록

        /// 장면의 모든 NPC 를 처음부터 다시 걷게 한다. (층이 바뀔 때)
        public static void RestartAll()
        {
            for (int i = All.Count - 1; i >= 0; i--)
            {
                if (All[i] == null) { All.RemoveAt(i); continue; }
                All[i].Restart();
            }
        }

        /// 모든 NPC 를 숨긴다.
        public static void HideAll()
        {
            for (int i = All.Count - 1; i >= 0; i--)
            {
                if (All[i] == null) { All.RemoveAt(i); continue; }
                All[i].Finish();
            }
        }

        void Awake()
        {
            body = GetComponent<CapsuleCollider>();
            if (!animator) animator = GetComponentInChildren<Animator>();
            CacheAnimator();
        }

        void OnEnable() { if (!All.Contains(this)) All.Add(this); }
        void OnDisable() => All.Remove(this);

        void Start() => Restart();

        // ------------------------------------------------------------ 시작 / 끝

        public void Restart()
        {
            if (!route) { Finish(); return; }
            route.Rebuild();

            s = 0f;
            emergeEnd = 0f;
            CurrentSpeed = 0f;

            if (route.FirstStairs(out float stairStart, out float stairEnd))
            {
                emergeEnd = stairEnd;
                if (startOnStairs)
                {
                    s = stairStart;
                    CurrentSpeed = walkSpeed * stairSpeedScale * emergeBoost;   // 이미 걷는 중
                }
            }

            offset = Vector3.zero;
            StepPhase = 0f;
            delayLeft = startDelay;
            finished = false;

            if (visual) visual.SetActive(true);
            if (body) body.enabled = true;
            CacheAnimator();

            Vector3 p0 = route.Sample(s, out _);
            Vector3 fwd = Flat(route.Sample(s + 0.5f, out _) - p0);
            lastForward = fwd.sqrMagnitude > 1e-6f ? fwd.normalized : Vector3.forward;
            transform.SetPositionAndRotation(Smoothed(s, out _), Quaternion.LookRotation(lastForward, Vector3.up));
            FeedAnimator();
        }

        void Finish()
        {
            finished = true;
            CurrentSpeed = 0f;
            if (visual) visual.SetActive(false);
            if (body) body.enabled = false;
        }

        // ------------------------------------------------------------ 매 프레임

        void Update()
        {
            if (finished || !route) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            if (delayLeft > 0f) { delayLeft -= dt; return; }

            float len = route.Length;
            Vector3 basePos = Smoothed(s, out int seg);
            OnStairs = route.IsStairs(seg);

            Vector3 fwd = Flat(route.Sample(Mathf.Min(s + 0.45f, len), out _) - route.Sample(s, out _));
            if (fwd.sqrMagnitude > 1e-6f) lastForward = fwd.normalized;
            fwd = lastForward;
            Vector3 right = Vector3.Cross(Vector3.up, fwd);

            float want = walkSpeed * (OnStairs ? stairSpeedScale : 1f);
            if (s < emergeEnd) want *= emergeBoost;     // 안 보이는 첫 계단은 빨리
            bool steering = false;

            if (!OnStairs && PlayerState(out Vector3 pp, out Vector3 pv, out float pr, out bool sameLevel) && sameLevel)
            {
                Vector3 me = basePos + offset;
                Vector3 away = Flat(me - pp);
                float dist = away.magnitude;
                float touch = pr + radius;

                // ---- 밀림: 몸을 붙이고 이쪽으로 걸어오면 밀려난다. 걸음은 멈추지 않는다
                if (dist < touch + 0.06f && dist > 1e-4f)
                {
                    Vector3 n = away / dist;
                    float approach = Vector3.Dot(pv, n);
                    if (approach > 0.05f) offset += n * (approach * pushStrength * dt);
                    if (dist < touch) offset += n * (touch - dist);   // 겹치면 바로 떼어 낸다

                    // 앞뒤로 밀린 만큼은 경로 위 진행으로 옮긴다
                    // (그래야 손을 떼도 뒷걸음질하거나 순간이동하지 않는다)
                    float along = Vector3.Dot(offset, fwd);
                    s = Mathf.Clamp(s + along, 0f, len);
                    offset -= fwd * along;
                }

                // ---- 비켜 가기: 플레이어가 앞을 막고 있으면 옆으로
                Vector3 rel = Flat(pp - (basePos + offset));
                float ahead = Vector3.Dot(rel, fwd);
                float side = Vector3.Dot(rel, right);
                float need = pr + radius + passGap;

                if (ahead > -0.1f && ahead < lookAhead && Mathf.Abs(side) < need)
                {
                    steering = true;
                    float pLat = Vector3.Dot(Flat(pp - basePos), right);
                    float a = pLat - need, b = pLat + need;
                    float first = Mathf.Abs(a) <= Mathf.Abs(b) ? a : b;
                    float second = first == a ? b : a;

                    float curLat = Vector3.Dot(offset, right);
                    float target = curLat;
                    bool room = true;
                    if (Room(basePos, right, first)) target = first;
                    else if (Room(basePos, right, second)) target = second;
                    else room = false;

                    float newLat = Mathf.MoveTowards(curLat, target, sideStepSpeed * dt);
                    offset += right * (newLat - curLat);

                    bool clear = Mathf.Abs(Vector3.Dot(Flat(pp - (basePos + offset)), right)) >= need - 0.02f;
                    if (!room && ahead < touch + 0.25f) want = 0f;    // 양옆이 다 막혔으면 기다린다
                    else want *= clear ? 0.75f : 0.4f;                // 비켜서는 중엔 천천히
                }
            }

            // ---- 비켜 가는 중이 아니면 언제나 경로로 돌아가려고 한다 (밀리는 중에도)
            if (!steering && offset.sqrMagnitude > 1e-6f)
            {
                Vector3 back = Vector3.MoveTowards(offset, Vector3.zero, returnSpeed * dt);
                if (!WouldTouchPlayer(basePos + back)) offset = back;
            }

            offset.y = 0f;
            if (offset.magnitude > maxPushOffset) offset = offset.normalized * maxPushOffset;
            offset = ClampToWalls(basePos, offset);

            // ---- 앞으로
            CurrentSpeed = Mathf.MoveTowards(CurrentSpeed, want, acceleration * dt);
            s += CurrentSpeed * dt;
            StepPhase += CurrentSpeed * dt * stridesPerMeter * Mathf.PI * 2f;
            if (StepPhase > Mathf.PI * 2f) StepPhase -= Mathf.PI * 2f;

            if (s >= len) { Finish(); return; }

            Vector3 pos = Smoothed(s, out seg) + offset;
            Quaternion look = Quaternion.LookRotation(fwd, Vector3.up);
            transform.SetPositionAndRotation(pos,
                Quaternion.RotateTowards(transform.rotation, look, turnSpeed * dt));

            FeedAnimator();
        }

        // ------------------------------------------------------------ 도우미

        /// 모서리를 둥글게 — 앞뒤 두 점의 가로 위치를 평균 낸다. 높이는 경로 그대로.
        Vector3 Smoothed(float at, out int seg)
        {
            Vector3 c = route.Sample(at, out seg);
            if (cornerSmoothing <= 0f) return c;
            float len = route.Length;
            Vector3 a = route.Sample(Mathf.Max(0f, at - cornerSmoothing), out _);
            Vector3 b = route.Sample(Mathf.Min(len, at + cornerSmoothing), out _);
            return new Vector3((a.x + b.x) * 0.5f, c.y, (a.z + b.z) * 0.5f);
        }

        bool PlayerState(out Vector3 pos, out Vector3 vel, out float r, out bool sameLevel)
        {
            pos = default; vel = default; r = 0.3f; sameLevel = false;

            if (!player)
            {
                var mgr = FloorLoopManager.Instance;
                if (mgr && mgr.player) player = mgr.player;
                else
                {
                    var w = Object.FindFirstObjectByType<DormitoryWalkthrough>();
                    if (w) player = w.transform;
                }
                if (!player) return false;
            }
            if (!playerBody) playerBody = player.GetComponent<CharacterController>();
            if (!playerWalk) playerWalk = player.GetComponent<DormitoryWalkthrough>();

            float feet;
            if (playerBody)
            {
                Bounds b = playerBody.bounds;
                pos = b.center;
                feet = b.min.y;
                Vector3 ls = player.lossyScale;
                r = playerBody.radius * Mathf.Max(Mathf.Abs(ls.x), Mathf.Abs(ls.z));
            }
            else
            {
                pos = player.position;
                feet = pos.y - 0.9f;
            }

            vel = playerWalk ? playerWalk.DesiredVelocity : (playerBody ? playerBody.velocity : Vector3.zero);
            vel.y = 0f;
            sameLevel = Mathf.Abs(feet - transform.position.y) < 0.8f;
            pos.y = transform.position.y;
            return true;
        }

        bool WouldTouchPlayer(Vector3 at)
        {
            if (!PlayerState(out Vector3 pp, out _, out float pr, out bool same) || !same) return false;
            return Flat(at - pp).magnitude < pr + radius + 0.02f;
        }

        /// 경로에서 옆으로 lateral 만큼 가도 벽에 안 부딪히는지.
        bool Room(Vector3 basePos, Vector3 right, float lateral)
        {
            float d = Mathf.Abs(lateral);
            if (d < 1e-3f) return true;
            Vector3 dir = right * Mathf.Sign(lateral);
            return Cast(basePos, dir, d + 0.02f) >= d;
        }

        Vector3 ClampToWalls(Vector3 basePos, Vector3 off)
        {
            float d = off.magnitude;
            if (d < 1e-4f) return off;
            Vector3 dir = off / d;
            float free = Cast(basePos, dir, d);
            return free < d ? dir * Mathf.Max(0f, free) : off;
        }

        /// 허리 높이에서 dir 쪽으로 몸통만 한 구를 굴려, 막히기 전까지 갈 수 있는 거리.
        float Cast(Vector3 basePos, Vector3 dir, float maxDist)
        {
            Vector3 origin = basePos + Vector3.up * 0.9f;
            float r = radius * 0.9f;
            int n = Physics.SphereCastNonAlloc(origin, r, dir, hits, maxDist, ~0, QueryTriggerInteraction.Ignore);
            float best = maxDist;
            for (int i = 0; i < n; i++)
            {
                RaycastHit h = hits[i];
                if (h.distance <= 0f) continue;                 // 처음부터 겹쳐 있던 것 (경로가 지나는 차단벽 등)
                Collider c = h.collider;
                if (!c || c == body) continue;
                if (player && c.transform.IsChildOf(player)) continue;
                if (c.transform.IsChildOf(transform)) continue;
                string nm = c.name;
                if (nm == "UpStairBlocker" || nm == "DownStairBlocker") continue;   // 플레이어 전용 벽
                if (h.distance < best) best = h.distance;
            }
            return best;
        }

        void CacheAnimator()
        {
            hasSpeedParam = hasStairsParam = false;
            if (!animator || animator.runtimeAnimatorController == null) return;
            foreach (AnimatorControllerParameter p in animator.parameters)
            {
                if (p.nameHash == SpeedId && p.type == AnimatorControllerParameterType.Float) hasSpeedParam = true;
                if (p.nameHash == StairsId && p.type == AnimatorControllerParameterType.Bool) hasStairsParam = true;
            }
        }

        void FeedAnimator()
        {
            if (!animator || !animator.isActiveAndEnabled) return;
            if (hasSpeedParam) animator.SetFloat(SpeedId, CurrentSpeed);
            if (hasStairsParam) animator.SetBool(StairsId, OnStairs);
        }

        static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }
    }
}
