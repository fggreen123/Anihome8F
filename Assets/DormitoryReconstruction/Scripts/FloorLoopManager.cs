using System.Collections;
using UnityEngine;

namespace Anihome.Dormitory
{
    /// <summary>
    /// 8번 출구식 반복 구조. 8층에서 시작해 1층까지 내려가는 것이 목표.
    ///
    ///   이상 현상 있음 + 전자레인지 앞 계단  → 한 층 내려감
    ///   이상 현상 있음 + 반대편 계단        → 처음으로
    ///   이상 현상 없음 + 반대편 계단        → 한 층 내려감
    ///   이상 현상 없음 + 전자레인지 앞 계단  → 처음으로
    ///
    /// 층을 넘길 때마다 맵을 초기화하고 표지판 층수를 바꾼 뒤,
    /// 항상 전자레인지 앞 계단 쪽에서 다시 시작한다.
    /// 이상 현상 자체는 아직 미구현이라, 해당 층이면 오른쪽 위에 표시만 띄운다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FloorLoopManager : MonoBehaviour
    {
        public static FloorLoopManager Instance { get; private set; }

        [Header("층")]
        public int startFloor = 8;
        public int goalFloor = 1;
        [Range(0f, 1f)] public float anomalyChance = 0.5f;

        [Header("참조")]
        public Transform player;
        public Transform spawnPoint;
        public FloorSign[] signs = new FloorSign[0];
        [Tooltip("칸 번호 = 표지판 위쪽에 적히는 층. 2~12층만 쓴다.")]
        public Material[] signByUpperFloor = new Material[13];

        [Header("연출")]
        [Min(0f)] public float fadeSeconds = 0.45f;
        [Min(0f)] public float messageSeconds = 2.2f;

        [Header("상태 (읽기 전용)")]
        [SerializeField] int currentFloor;
        [SerializeField] bool anomalyActive;
        [SerializeField] int loopCount;
        [SerializeField] bool cleared;

        public int CurrentFloor => currentFloor;
        public bool AnomalyActive => anomalyActive;
        public bool Busy => busy;
        public bool Cleared => cleared;

        CharacterController body;
        bool busy;
        float fade;              // 0 = 보임, 1 = 완전 검정
        string message;
        float messageLeft;

        GUIStyle big, mid, small, shade;
        Texture2D solid;
        Font uiFont;
        bool fontChecked;

        void Awake()
        {
            Instance = this;
            if (!player)
            {
                var w = Object.FindFirstObjectByType<DormitoryWalkthrough>();
                if (w) player = w.transform;
            }
            if (player) body = player.GetComponent<CharacterController>();
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        void Start() => BeginRun();

        /// 8층부터 새 판 시작.
        public void BeginRun()
        {
            cleared = false;
            loopCount = 0;
            currentFloor = startFloor;
            SetupLevel();
            fade = 1f;
            StartCoroutine(FadeTo(0f));
        }

        public void RequestDescend(StairSide side)
        {
            if (busy || cleared) return;
            StartCoroutine(DescendRoutine(side));
        }

        IEnumerator DescendRoutine(StairSide side)
        {
            busy = true;
            yield return FadeTo(1f);

            bool correct = anomalyActive
                ? side == StairSide.Microwave
                : side == StairSide.Opposite;

            loopCount++;

            if (correct)
            {
                currentFloor--;
                if (currentFloor <= goalFloor)
                {
                    currentFloor = goalFloor;
                    cleared = true;
                    ApplySigns();
                    Teleport();
                    message = null;
                    yield return FadeTo(0f);
                    busy = false;
                    yield break;
                }
                Say($"{currentFloor + 1}층 → {currentFloor}층");
            }
            else
            {
                currentFloor = startFloor;
                Say($"처음으로 돌아갑니다 — {startFloor}층");
            }

            SetupLevel();
            yield return new WaitForSeconds(0.12f);
            yield return FadeTo(0f);
            busy = false;
        }

        /// 맵 초기화 + 이상 현상 재추첨 + 표지판 갱신 + 시작 위치로.
        void SetupLevel()
        {
            anomalyActive = Random.value < anomalyChance;
            ResetDoors();
            ApplySigns();
            Teleport();
        }

        void ResetDoors()
        {
            var doors = Object.FindObjectsByType<DoorInteractable>(FindObjectsSortMode.None);
            for (int i = 0; i < doors.Length; i++) doors[i].SetOpen(false);
        }

        void ApplySigns()
        {
            if (signByUpperFloor == null || signByUpperFloor.Length == 0) return;
            int idx = Mathf.Clamp(currentFloor, 0, signByUpperFloor.Length - 1);
            Material m = signByUpperFloor[idx];
            if (!m) return;
            for (int i = 0; i < signs.Length; i++)
                if (signs[i]) signs[i].SetMaterial(m);
        }

        void Teleport()
        {
            if (!player || !spawnPoint) return;
            if (!body) body = player.GetComponent<CharacterController>();
            if (body) body.enabled = false;
            player.SetPositionAndRotation(spawnPoint.position, spawnPoint.rotation);
            if (body) body.enabled = true;
            var cam = player.GetComponentInChildren<Camera>();
            if (cam) cam.transform.localRotation = Quaternion.identity;
        }

        void Say(string s)
        {
            message = s;
            messageLeft = messageSeconds;
        }

        IEnumerator FadeTo(float target)
        {
            if (fadeSeconds <= 0.001f) { fade = target; yield break; }
            float from = fade, t = 0f;
            while (t < fadeSeconds)
            {
                t += Time.deltaTime;
                fade = Mathf.Lerp(from, target, t / fadeSeconds);
                yield return null;
            }
            fade = target;
        }

        void Update()
        {
            if (messageLeft > 0f) messageLeft -= Time.deltaTime;
        }

        // ------------------------------------------------------------ 화면

        void EnsureStyles()
        {
            if (!fontChecked)
            {
                fontChecked = true;
                try
                {
                    uiFont = Font.CreateDynamicFontFromOSFont(
                        new[] { "Malgun Gothic", "맑은 고딕", "Noto Sans KR", "NanumGothic", "Gulim", "Dotum" }, 30);
                }
                catch { uiFont = null; }
            }
            if (big != null) return;

            big = new GUIStyle(GUI.skin.label) { fontSize = 30, alignment = TextAnchor.MiddleLeft };
            mid = new GUIStyle(GUI.skin.label) { fontSize = 21, alignment = TextAnchor.MiddleRight };
            small = new GUIStyle(GUI.skin.label) { fontSize = 24, alignment = TextAnchor.MiddleCenter };
            if (uiFont) { big.font = uiFont; mid.font = uiFont; small.font = uiFont; }
            big.normal.textColor = new Color(1f, 1f, 1f, 0.95f);
            mid.normal.textColor = new Color(1f, 0.72f, 0.36f, 0.98f);
            small.normal.textColor = new Color(1f, 1f, 1f, 0.95f);
            shade = new GUIStyle(big);
            shade.normal.textColor = new Color(0f, 0f, 0f, 0.7f);

            solid = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            solid.SetPixel(0, 0, Color.white);
            solid.Apply();
            solid.hideFlags = HideFlags.HideAndDontSave;
        }

        void OnGUI()
        {
            EnsureStyles();

            // 층 표시 (왼쪽 위)
            var floorRect = new Rect(24f, 18f, 260f, 40f);
            string f = cleared ? (uiFont ? "탈출 성공" : "ESCAPED") : $"{currentFloor}F";
            GUI.Label(new Rect(floorRect.x + 1, floorRect.y + 1, floorRect.width, floorRect.height), f, shade);
            GUI.Label(floorRect, f, big);

            // 이상 현상 표시 (오른쪽 위) — 아직 미구현이라 글자만
            if (anomalyActive && !cleared)
            {
                string a = uiFont ? "이상 현상 층" : "ANOMALY FLOOR";
                var r = new Rect(Screen.width - 304f, 20f, 280f, 32f);
                GUI.Label(new Rect(r.x + 1, r.y + 1, r.width, r.height), a, shade);
                GUI.Label(r, a, mid);
            }

            // 가운데 안내
            if (messageLeft > 0f && !string.IsNullOrEmpty(message) && fade < 0.5f)
            {
                var r = new Rect(0f, Screen.height * 0.18f, Screen.width, 34f);
                GUI.Label(new Rect(r.x + 1, r.y + 1, r.width, r.height), message, shade);
                GUI.Label(r, message, small);
            }

            if (cleared)
            {
                string c = uiFont ? $"{goalFloor}층 도착 — 탈출 성공" : $"Reached floor {goalFloor}";
                var r = new Rect(0f, Screen.height * 0.42f, Screen.width, 40f);
                GUI.Label(r, c, small);
            }

            // 암전
            if (fade > 0.001f)
            {
                Color prev = GUI.color;
                GUI.color = new Color(0f, 0f, 0f, Mathf.Clamp01(fade));
                GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), solid);
                GUI.color = prev;
            }
        }
    }
}
