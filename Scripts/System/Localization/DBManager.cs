using System.Collections.Generic;
using System.Reflection;

/// <summary>
/// 구글 스프레드 시트에서 읽어온 데이터를, 언어설정에 따라 분할해서 return 해주는 역할
/// </summary>
public class DBManager : PersistentSingleton<DBManager>
{
    //언어 데이터는 게임 시작시 전부 읽어오는걸로 해보자.    
    //Korean
    private Dictionary<int, ETC_DialogData> _etcDatasKO = new Dictionary<int, ETC_DialogData>();
    private SortedDictionary<int, DialogData> _prologDatasKO = new SortedDictionary<int, DialogData>();
    private SortedDictionary<int, DialogData> _chapter01DatasKO = new SortedDictionary<int, DialogData>();
    private Dictionary<int, ChapterLobbyData> _chapterLobbyDataKO = new Dictionary<int, ChapterLobbyData>();
    
    //English
    private Dictionary<int, ETC_DialogData> _etcDatasEN = new Dictionary<int, ETC_DialogData>();
    private SortedDictionary<int, DialogData> _prologDatasEN = new SortedDictionary<int, DialogData>();
    private Dictionary<int, ChapterLobbyData> _chapterLobbyDataEN = new Dictionary<int, ChapterLobbyData>();
    
    //Common
    private SortedDictionary<int, Report_Table> _reportTable = new SortedDictionary<int, Report_Table>();
    private SortedDictionary<int, UI_Table> _uiTable = new SortedDictionary<int, UI_Table>();
    
    // ====== PanelUI 다국어 문자열(Key → Text) ======
    private readonly Dictionary<string, string> _panelUI_KO = new();
    private readonly Dictionary<string, string> _panelUI_EN = new();
    
    private GoogleSheetSO _so;
  
    private void Awake() => Init();

    private void Init()
    {
        _so = ResourceManager.Instance.Load<GoogleSheetSO>("Dialogs/DialogSO");

        SetKODatas(_so);
        SetENDatas(_so);
        SetCommonDatas(_so);
        
        BuildPanelUIDictionaries(_so);
    }

    //한국어로 된 데이터 전부 읽어오기
    private void SetKODatas(GoogleSheetSO googleSheetSo)
    {
        foreach (var etcTableKO in googleSheetSo.ETC_Table_KOList)
            _etcDatasKO.Add(etcTableKO.ID, new ETC_DialogData(etcTableKO));

        foreach (var prologueTableKo in googleSheetSo.Prologue_Table_KOList)
            _prologDatasKO.Add(prologueTableKo.ID, new DialogData(prologueTableKo));

        foreach (var chapter01TableKo in googleSheetSo.Chapter01_Table_KOList)
            _chapter01DatasKO.Add(chapter01TableKo.ID, new DialogData(chapter01TableKo));

        foreach (var chapterLobbyKO in googleSheetSo.ChapterLobby_KOList)
            _chapterLobbyDataKO.Add(chapterLobbyKO.ID, new ChapterLobbyData(chapterLobbyKO));
    }
    
    //영어로 된 데이터 전부 읽어오기
    private void SetENDatas(GoogleSheetSO googleSheetSo)
    {
        foreach (var etcTableEN in googleSheetSo.ETC_Table_ENList)
            _etcDatasEN.Add(etcTableEN.ID, new ETC_DialogData(etcTableEN));

        foreach (var prologueTableEn in googleSheetSo.Prologue_Table_ENList)
            _prologDatasEN.Add(prologueTableEn.ID, new DialogData(prologueTableEn));

        // TODO: 로비/챕터 영문 테이블 추가 시 확장
    }
    
    //한개의 Table에 모든 언어가 담긴 데이터 읽어오기
    private void SetCommonDatas(GoogleSheetSO googleSheetSo)
    {
        //기록 Table
        foreach (var reportTable in googleSheetSo.Report_TableList)
            _reportTable.Add(reportTable.ID, reportTable);
        
        //UI Table
        foreach (var uiTable in googleSheetSo.UI_TableList)
            _uiTable.Add(uiTable.ID, uiTable);
    }
    
    private void BuildPanelUIDictionaries(GoogleSheetSO so)
    {
        _panelUI_KO.Clear();
        _panelUI_EN.Clear();

        if (so == null || so.PanelUI_TableList == null)
            return;

        foreach (PanelUI_Table row in so.PanelUI_TableList)
        {
            if (row == null || string.IsNullOrWhiteSpace(row.Key))
                continue;

            // KO/EN 컬럼이 존재할 때만 채움 (코드젠에서 string으로 생성됨)
            string ko = GetFieldSafe(row, "KO");
            string en = GetFieldSafe(row, "EN");

            if (!string.IsNullOrEmpty(ko)) _panelUI_KO[row.Key] = ko;
            if (!string.IsNullOrEmpty(en)) _panelUI_EN[row.Key] = en;
        }
    }
    
    public bool TryGetPanelUIText(string key, out string value)
    {
        switch (LocalizationManager.Instance?.Current)
        {
            case LocalizationManager.Language.KO:
                return _panelUI_KO.TryGetValue(key, out value);
            case LocalizationManager.Language.EN:
                return _panelUI_EN.TryGetValue(key, out value);
            default:
                value = null;
                return false;
        }
    }

    // 폴백용: 모든 언어 사전 순회
    public IEnumerable<Dictionary<string, string>> EnumerateAllPanelUIDicts()
    {
        yield return _panelUI_KO;
        yield return _panelUI_EN;
        // 이후 언어 추가 시 yield return 추가
    }

    // string 필드 안전 접근 유틸(코드젠 필드명 기반)
    private static string GetFieldSafe(object rowObj, string fieldName)
    {
        FieldInfo f = rowObj.GetType().GetField(fieldName);
        if (f == null) return null;
        return f.GetValue(rowObj) as string;
    }

    public Dictionary<int, ETC_DialogData> GetETCDatas()
    {
        switch (LocalizationManager.Instance?.Current)
        {
            case LocalizationManager.Language.KO: return _etcDatasKO;
            case LocalizationManager.Language.EN: return _etcDatasEN;
            default:
                ConsoleLogger.LogWarning("언어 미설정");
                return _etcDatasKO;
        }
    }

    public ETC_DialogData GetETCData(int id)
    {
        if (!_etcDatasKO.ContainsKey(id))
        {
            ConsoleLogger.LogError($"{id}번 대사를 찾을 수 없습니다");
            return null;
        }
        
        switch (LocalizationManager.Instance?.Current)
        {
            case LocalizationManager.Language.KO: return _etcDatasKO[id];
            case LocalizationManager.Language.EN: return _etcDatasEN[id];
            default:
                ConsoleLogger.LogWarning("언어 미설정");
                return _etcDatasKO[id];
        }
    }

    public DialogData GetPrologueData(int id)
    {
        if (!_prologDatasKO.ContainsKey(id))
        {
            return null;
        }

        switch (LocalizationManager.Instance?.Current)
        {
            case LocalizationManager.Language.KO: return _prologDatasKO[id];
            case LocalizationManager.Language.EN: return _prologDatasEN[id];
            default:
                ConsoleLogger.LogWarning("언어 미설정");
                return _prologDatasKO[id];
        }
    }

    public SortedDictionary<int, DialogData> GetPrologueData()
    {
        switch (LocalizationManager.Instance?.Current)
        {
            case LocalizationManager.Language.KO: return _prologDatasKO;
            case LocalizationManager.Language.EN: return _prologDatasEN;
            default:
                ConsoleLogger.LogWarning("언어 미설정");
                return _prologDatasKO;
        }
    }

    public Dictionary<int, ChapterLobbyData> GetChapterLobbyData()
    {
        switch (LocalizationManager.Instance?.Current)
        {
            case LocalizationManager.Language.KO: return _chapterLobbyDataKO;
            case LocalizationManager.Language.EN: return _chapterLobbyDataEN;
            default:
                ConsoleLogger.LogWarning("언어 미설정");
                return _chapterLobbyDataKO;
        }
    }
    
    
    public SortedDictionary<int, DialogData> GetChapter01Data()
    {
        switch (LocalizationManager.Instance?.Current)
        {
            case LocalizationManager.Language.KO: return _chapter01DatasKO;
            case LocalizationManager.Language.EN:
                // TODO: EN 설정 추가하기
                // return _chapter01DatasKO;
            default:
                ConsoleLogger.LogWarning("언어 미설정");
                return _chapter01DatasKO;
        }
    }

    public string GetReportContent(int id)
    {
        switch (LocalizationManager.Instance?.Current)
        {
            case LocalizationManager.Language.KO: return _reportTable[id].KO;
            case LocalizationManager.Language.EN: return _reportTable[id].EN;
                
            default:
                ConsoleLogger.LogWarning("언어 미설정");
                return _reportTable[id].KO;
        }
    }
    
    public string GetUIContent(int id)
    {
        switch (LocalizationManager.Instance?.Current)
        {
            case LocalizationManager.Language.KO: return _uiTable[id].KO;
            case LocalizationManager.Language.EN: return _uiTable[id].EN;
                
            default:
                ConsoleLogger.LogWarning("언어 미설정");
                return _uiTable[id].KO;
        }
    }
}