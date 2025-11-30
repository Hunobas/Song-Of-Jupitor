// EventCategories.cs
namespace EventGraph
{
    /// <summary>
    /// NodeMenuItem 경로 카테고리 모음.
    /// 사용 예: [NodeMenuItem(EventCategories.Rendering + "ScreenGlitch/Start Infinite")]
    /// </summary>
    public static class EventCategories
    {
        public const string Root      = "Custom/";

        public const string Misc       = Root + "기타/";
        public const string Content    = Root + "컨텐츠/";
        public const string Object     = Root + "오브젝트/";
        public const string Camera     = Root + "카메라/";
        public const string UI         = Root + "UI 및 효과/";
        public const string Input      = Root + "입력/";
        public const string Player     = Root + "플레이어/";
        public const string Rendering  = Root + "렌더링/";
        public const string Time       = Root + "시간/";
        public const string Display    = Root + "출력/";
        public const string CheckPoint = Root + "체크포인트/";
        public const string Level      = Root + "레벨/";
        public const string Sound      = Root + "사운드/";
        public const string Animation  = Root + "애니메이션/";
        public const string Debug      = Root + "디버깅/";
        public const string Timeline   = Root + "타임라인/";

        public static string Path(string category, string name) => category + name;
    }
}