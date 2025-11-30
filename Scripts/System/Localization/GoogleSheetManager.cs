using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using Newtonsoft.Json.Linq;
using System.Linq;

public class GoogleSheetManager : MonoBehaviour
{
    [Tooltip("true: google sheet, false: local json")]
    [SerializeField] bool isAccessGoogleSheet = true;
    [Tooltip("Google sheet appsscript webapp url")]
    [SerializeField] string googleSheetUrl;
    [Tooltip("Google sheet avail sheet tabs. seperate `/`. For example `Sheet1/Sheet2`")]
    [SerializeField] string availSheets = "Sheet1/Sheet2";
    [Tooltip("For example `/GenerateGoogleSheet`")]
    [SerializeField] string generateFolderPath = "/GenerateGoogleSheet";
    [Tooltip("You must approach through `GoogleSheetManager.SO<GoogleSheetSO>()`")]
    public ScriptableObject googleSheetSO;

    string JsonPath => $"{Application.dataPath}{generateFolderPath}/GoogleSheetJson.json";
    string ClassPath => $"{Application.dataPath}{generateFolderPath}/GoogleSheetClass.cs";
    string SOPath => $"Assets{generateFolderPath}/GoogleSheetSO.asset";

    string[] availSheetArray;
    string json;
    bool refeshTrigger;
    static GoogleSheetManager instance;



    public static T SO<T>() where T : ScriptableObject
    {
        if (GetInstance().googleSheetSO == null)
        {
            Debug.Log($"googleSheetSO is null");
            return null;
        }

        return GetInstance().googleSheetSO as T;
    }



#if UNITY_EDITOR
    [ContextMenu("FetchGoogleSheet")]
    async void FetchGoogleSheet()
    {
        //Init
        availSheetArray = availSheets.Split('/');

        if (isAccessGoogleSheet)
        {
            Debug.Log($"Loading from google sheet..");
            json = await LoadDataGoogleSheet(googleSheetUrl);
        }
        else
        {
            Debug.Log($"Loading from local json..");
            json = LoadDataLocalJson();
        }
        if (json == null) return;

        bool isJsonSaved = SaveFileOrSkip(JsonPath, json);
        string allClassCode = GenerateCSharpClass(json);
        bool isClassSaved = SaveFileOrSkip(ClassPath, allClassCode);

        if (isJsonSaved || isClassSaved)
        {
            refeshTrigger = true;
            UnityEditor.AssetDatabase.Refresh();
        }
        else
        {
            CreateGoogleSheetSO();
            Debug.Log($"Fetch done.");
        }
    }

    async Task<string> LoadDataGoogleSheet(string url)
    {
        using (HttpClient client = new HttpClient())
        {
            try
            {
                byte[] dataBytes = await client.GetByteArrayAsync(url);
                return Encoding.UTF8.GetString(dataBytes);
            }
            catch (HttpRequestException e)
            {
                Debug.LogError($"Request error: {e.Message}");
                return null;
            }
        }
    }

    string LoadDataLocalJson()
    {
        if (File.Exists(JsonPath))
        {
            return File.ReadAllText(JsonPath);
        }

        Debug.Log($"File not exist.\n{JsonPath}");
        return null;
    }

    bool SaveFileOrSkip(string path, string contents)
    {
        string directoryPath = Path.GetDirectoryName(path);
        if (!Directory.Exists(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        if (File.Exists(path) && File.ReadAllText(path).Equals(contents))
            return false;

        File.WriteAllText(path, contents);
        return true;
    }

    bool IsExistAvailSheets(string sheetName)
    {
        return Array.Exists(availSheetArray, x => x == sheetName);
    }

    string GenerateCSharpClass(string jsonInput)
    {
        JObject jsonObject = JObject.Parse(jsonInput);
        StringBuilder sb = new();

        // using & ScriptableObject 선언
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using UnityEngine;");
        sb.AppendLine();
        sb.AppendLine("/// <summary>You must approach through `GoogleSheetManager.SO<GoogleSheetSO>()`</summary>");
        sb.AppendLine("public class GoogleSheetSO : ScriptableObject");
        sb.AppendLine("{");

        // SO 필드: 시트별 List 선언
        foreach (var sheet in jsonObject)
        {
            string className = sheet.Key;
            if (!IsExistAvailSheets(className)) continue;
            sb.AppendLine($"\tpublic List<{className}> {className}List;");
        }
        sb.AppendLine("}");
        sb.AppendLine();

        // 각 시트에 대한 클래스 생성
        foreach (var j in jsonObject)
        {
            string className = j.Key;
            if (!IsExistAvailSheets(className)) continue;

            var items = (JArray)j.Value;
            if (items.Count == 0)
            {
                sb.AppendLine($"[Serializable]\npublic class {className}\n{{\n}}\n");
                continue;
            }

            var first = (JObject)items[0];
            var headers = first.Properties().Select(p => p.Name).ToArray();

            sb.AppendLine("[Serializable]");
            sb.AppendLine($"public class {className}");
            sb.AppendLine("{");

            if (className == "PanelUI_Table")
            {
                foreach (var h in headers)
                    sb.AppendLine($"\tpublic string {h};");
            }
            else
            {
                int propertyCount = headers.Length;
                string[] colTypes = new string[propertyCount];

                int rowIdx = 0;
                foreach (JToken row in items)
                {
                    rowIdx = 0;
                    foreach (var prop in ((JObject)row).Properties())
                    {
                        string type = GetCSharpType(prop.Value.Type);
                        string old = colTypes[rowIdx];

                        if (old == null) colTypes[rowIdx] = type;
                        else if (old == "int")
                        {
                            colTypes[rowIdx] = (type == "int") ? "int" :
                                               (type == "float") ? "float" :
                                               (type == "bool") ? "string" : "string";
                        }
                        else if (old == "float")
                        {
                            colTypes[rowIdx] = (type == "int" || type == "float") ? "float" :
                                               (type == "bool") ? "string" : "string";
                        }
                        else if (old == "bool")
                        {
                            colTypes[rowIdx] = (type == "bool") ? "bool" : "string";
                        }
                        else
                        {
                            colTypes[rowIdx] = "string";
                        }
                        rowIdx++;
                    }
                }

                for (int i = 0; i < headers.Length; i++)
                {
                    string h = headers[i];
                    string t = string.IsNullOrEmpty(colTypes[i]) ? "string" : colTypes[i];
                    sb.AppendLine($"\tpublic {t} {h};");
                }
            }

            sb.AppendLine("}");
            sb.AppendLine();
        }

        return sb.ToString();
    }

    string GetCSharpType(JTokenType jsonType)
    {
        switch (jsonType)
        {
            case JTokenType.Integer:
                return "int";
            case JTokenType.Float:
                return "float";
            case JTokenType.Boolean:
                return "bool";
            default:
                return "string";
        }
    }

    bool CreateGoogleSheetSO()
    {
        if (Type.GetType("GoogleSheetSO") == null)
            return false;

        googleSheetSO = ScriptableObject.CreateInstance("GoogleSheetSO");
        JObject jsonObject = JObject.Parse(json);

        try
        {
            foreach (var sheet in jsonObject)
            {
                string className = sheet.Key;
                if (!IsExistAvailSheets(className)) continue;

                Type rowType = Type.GetType(className);
                if (rowType == null)
                {
                    Debug.LogError($"Type not found for sheet: {className}");
                    continue;
                }

                Type listType = typeof(List<>).MakeGenericType(rowType);
                IList listInst = (IList)Activator.CreateInstance(listType);

                var items = (JArray)sheet.Value;

                foreach (var jrow in items)
                {
                    object rowObj = Activator.CreateInstance(rowType);
                    foreach (var prop in ((JObject)jrow).Properties())
                    {
                        var field = rowType.GetField(prop.Name);
                        if (field == null) continue;

                        object value;
                        if (className == "PanelUI_Table")
                        {
                            value = prop.Value.Type == JTokenType.Null ? "" : prop.Value.ToString();
                        }
                        else
                        {
                            var ft = field.FieldType;
                            string text = prop.Value.Type == JTokenType.Null ? "" : prop.Value.ToString();
                            try
                            {
                                if (ft == typeof(string))
                                    value = text;
                                else if (ft == typeof(int))
                                    value = string.IsNullOrEmpty(text) ? 0 : Convert.ToInt32(text);
                                else if (ft == typeof(float))
                                    value = string.IsNullOrEmpty(text) ? 0f : Convert.ToSingle(text);
                                else if (ft == typeof(bool))
                                    value = string.Equals(text, "true", StringComparison.OrdinalIgnoreCase);
                                else
                                    value = Convert.ChangeType(text, ft);
                            }
                            catch
                            {
                                value = text;
                            }
                        }

                        field.SetValue(rowObj, value);
                    }
                    listInst.Add(rowObj);
                }

                googleSheetSO.GetType().GetField($"{className}List").SetValue(googleSheetSO, listInst);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"CreateGoogleSheetSO error: {e.Message}\n{e.StackTrace}");
        }

        UnityEditor.AssetDatabase.CreateAsset(googleSheetSO, SOPath);
        UnityEditor.AssetDatabase.SaveAssets();
        Debug.Log("CreateGoogleSheetSO completed.");
        return true;
    }

    void OnValidate()
    {
        if (refeshTrigger)
        {
            bool isCompleted = CreateGoogleSheetSO();
            if (isCompleted)
            {
                refeshTrigger = false;
                Debug.Log($"Fetch done.");
            }
        }
    }
#endif

    static GoogleSheetManager GetInstance()
    {
        if (instance == null)
        {
            instance = FindFirstObjectByType<GoogleSheetManager>();
        }
        return instance;
    }
}