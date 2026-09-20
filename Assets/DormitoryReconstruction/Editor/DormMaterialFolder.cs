using UnityEditor;

namespace Anihome.Dormitory.EditorTools
{
    /// <summary>
    /// 재질이 들어 있는 폴더를 찾아 준다.
    /// 프로젝트에 따라 Materials 일 수도 Map_Materials 일 수도 있어서,
    /// 있는 쪽을 쓰고 둘 다 없으면 Materials 를 새로 만든다.
    /// </summary>
    public static class DormMaterialFolder
    {
        const string Root = "Assets/DormitoryReconstruction";

        static string cached;

        public static string Path
        {
            get
            {
                if (!string.IsNullOrEmpty(cached) && AssetDatabase.IsValidFolder(cached)) return cached;

                string[] candidates =
                {
                    Root + "/Map_Materials",
                    Root + "/Materials"
                };
                foreach (string c in candidates)
                    if (AssetDatabase.IsValidFolder(c)) { cached = c; return cached; }

                if (!AssetDatabase.IsValidFolder(Root)) AssetDatabase.CreateFolder("Assets", "DormitoryReconstruction");
                AssetDatabase.CreateFolder(Root, "Materials");
                cached = Root + "/Materials";
                return cached;
            }
        }
    }
}
