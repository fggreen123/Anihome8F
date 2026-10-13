using UnityEngine;

namespace Anihome.Dormitory
{
    /// <summary>
    /// 임시 마네킹의 팔다리를 걸음에 맞춰 흔든다.
    /// 진짜 사람 모델(Mixamo 등)로 바꾸면 이 컴포넌트와 마네킹은 지우면 된다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MannequinAnimator : MonoBehaviour
    {
        public CorridorWalker walker;
        public Transform leftLeg, rightLeg, leftArm, rightArm;
        [Tooltip("위아래로 들썩이는 부분 (몸통·머리)")]
        public Transform upper;

        [Min(0f)] public float legSwing = 24f;
        [Min(0f)] public float armSwing = 14f;
        [Tooltip("계단에서 다리를 더 들어 올리는 양 (도)")]
        [Min(0f)] public float stairLift = 18f;
        [Min(0f)] public float bob = 0.018f;

        Vector3 upperHome;
        bool cached;

        void Awake() => Cache();

        void Cache()
        {
            if (cached) return;
            if (upper) upperHome = upper.localPosition;
            cached = true;
        }

        void LateUpdate()
        {
            if (!walker) return;
            Cache();

            float w = Mathf.Clamp01(walker.CurrentSpeed / Mathf.Max(0.1f, walker.walkSpeed));
            float ph = walker.StepPhase;
            float sw = Mathf.Sin(ph) * w;
            float lift = walker.OnStairs ? stairLift * w : 0f;

            // 계단에서는 앞으로 내딛는 다리를 더 든다
            float l = -sw * legSwing - Mathf.Max(0f, Mathf.Sin(ph)) * lift;
            float r = sw * legSwing - Mathf.Max(0f, -Mathf.Sin(ph)) * lift;

            if (leftLeg) leftLeg.localRotation = Quaternion.Euler(l, 0f, 0f);
            if (rightLeg) rightLeg.localRotation = Quaternion.Euler(r, 0f, 0f);
            if (leftArm) leftArm.localRotation = Quaternion.Euler(sw * armSwing, 0f, 0f);
            if (rightArm) rightArm.localRotation = Quaternion.Euler(-sw * armSwing, 0f, 0f);

            if (upper) upper.localPosition = upperHome + Vector3.down * (Mathf.Abs(Mathf.Sin(ph)) * bob * w);
        }
    }
}
