using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>You must approach through `GoogleSheetManager.SO<GoogleSheetSO>()`</summary>
public class GoogleSheetSO : ScriptableObject
{
	public List<Prologue_Table_KO> Prologue_Table_KOList;
	public List<Prologue_Table_EN> Prologue_Table_ENList;
	public List<Chapter01_Table_KO> Chapter01_Table_KOList;
	public List<ETC_Table_KO> ETC_Table_KOList;
	public List<ETC_Table_EN> ETC_Table_ENList;
	public List<Report_Table> Report_TableList;
	public List<ChapterLobby_KO> ChapterLobby_KOList;
	public List<PanelUI_Table> PanelUI_TableList;
	public List<UI_Table> UI_TableList;
}

[Serializable]
public class Prologue_Table_KO
{
	public int ID;
	public string SpeakerName;
	public string Context;
	public bool TalkEnd;
	public string VoicePath;
	public string ImagePath;
	public string Expression;
	public int EventID;
}

[Serializable]
public class Prologue_Table_EN
{
	public int ID;
	public string SpeakerName;
	public string Context;
	public bool TalkEnd;
	public string VoicePath;
	public string ImagePath;
	public string Expression;
	public int EventID;
}

[Serializable]
public class Chapter01_Table_KO
{
	public int ID;
	public string SpeakerName;
	public string Context;
	public bool TalkEnd;
	public string VoicePath;
	public string ImagePath;
	public string Expression;
	public int EventID;
}

[Serializable]
public class ETC_Table_KO
{
	public int ID;
	public string SpeakerName;
	public string Context;
	public bool TalkEnd;
}

[Serializable]
public class ETC_Table_EN
{
	public int ID;
	public string SpeakerName;
	public string Context;
	public bool TalkEnd;
}

[Serializable]
public class Report_Table
{
	public int ID;
	public string KO;
	public string EN;
}

[Serializable]
public class ChapterLobby_KO
{
	public int ID;
	public string Content;
	public string ReportType;
	public string UnlockCondition;
	public string EventCondition;
}

[Serializable]
public class PanelUI_Table
{
	public string Key;
	public string KO;
	public string EN;
}

[Serializable]
public class UI_Table
{
	public int ID;
	public string KO;
	public string EN;
}

