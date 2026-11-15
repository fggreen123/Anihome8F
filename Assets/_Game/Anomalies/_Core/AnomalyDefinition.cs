using UnityEngine;

namespace Anihome.Dormitory
{
    [CreateAssetMenu(menuName = "Anihome/Anomaly Definition", fileName = "AN_00")]
    public sealed class AnomalyDefinition : ScriptableObject
    {
        [Min(1)] public int number;
        public string displayName;
        public string category;
        public string location;
        [TextArea(2, 6)] public string description;
        [TextArea] public string trigger;
        [TextArea] public string resetCondition;
        [TextArea] public string requiredAssets;
        public string soundNotes;
        [Min(0f)] public float selectionWeight = 1f;
        [Tooltip("행동 코드와 씬 연결까지 완료했을 때만 켭니다. 모델만 준비된 경우는 끕니다.")]
        public bool implemented;
    }
}
