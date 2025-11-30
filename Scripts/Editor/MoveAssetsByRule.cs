using UnityEditor;
using UnityEngine;
using System.IO;
using System.Linq;

public class MoveAssetsByRule : EditorWindow
{
    [MenuItem("Tools/정리/Rule 기반 에셋 정리")]
    static void MoveAssets()
    {
        string[] allFiles = Directory.GetFiles("Assets", "*.*", SearchOption.AllDirectories)
            .Where(path => !path.EndsWith(".meta"))
            .Where(path => !path.StartsWith("Assets/Packages"))
            .ToArray();

        foreach (string filePath in allFiles)
        {
            string ext = Path.GetExtension(filePath).ToLower();
            string dir = Path.GetDirectoryName(filePath).Replace("\\", "/");
            string fileName = Path.GetFileName(filePath);

            // .mat 규칙
            if (ext == ".mat")
            {
                if (dir.StartsWith("Assets/Materials"))
                    Move(filePath, "Assets/Art/3D/Materials", fileName);
                else if (dir.StartsWith("Assets/Handmade"))
                    Move(filePath, "Assets/Art/3D/Materials/Handmade", fileName);
                else
                    Move(filePath, "Assets/Art/3D/Models/Materials", fileName);
            }

            // 3D 모델
            else if (ext == ".fbx" || ext == ".blend" || ext == ".obj")
                Move(filePath, "Assets/Art/3D/Models", fileName);

            // Animation 파일
            else if (ext == ".anim" || ext == ".controller")
            {
                if (dir.StartsWith("Assets/Animation"))
                    continue; // 통째로 옮길 예정이므로 무시
                Move(filePath, "Assets/Art/3D/Animations", fileName);
            }

            // Timeline 관련
            else if (ext == ".signal" || ext == ".playable")
            {
                if (dir.StartsWith("Assets/Timeline"))
                    continue;
                Move(filePath, "Assets/Timeline", fileName);
            }

            // Handmade 내 shader
            else if ((ext == ".shader" || ext == ".shadergraph") && dir.StartsWith("Assets/Handmade"))
                Move(filePath, "Assets/Shaders", fileName);

            // Image 파일
            else if (ext == ".png" || ext == ".jpg" || ext == ".jpeg")
            {
                if (dir.Contains("/Characters/") || dir.Contains("Art/2D/Characters"))
                    continue; // 이미 다 옮긴 상태라 가정
                else if (dir.ToLower().Contains("/ui/"))
                    Move(filePath, "Assets/Art/2D/UI", fileName);
                else
                {
                    string tailDir = Path.GetFileName(dir);
                    Move(filePath, $"Assets/Art/2D/Textures/{tailDir}", fileName);
                }
            }

            // Sound 파일
            else if (ext == ".wav" || ext == ".mp3")
            {
                if (dir.StartsWith("Assets/Sounds"))
                    continue;
                string tailDir = Path.GetFileName(dir);
                Move(filePath, $"Assets/Sounds/{tailDir}", fileName);
            }

            // Font
            else if (ext == ".ttf" || ext == ".sdf")
                Move(filePath, "Assets/UI/Fonts", fileName);
        }

        AssetDatabase.Refresh();
        Debug.Log("🎉 정리 완료!");
    }

    static void Move(string oldPath, string targetDir, string fileName)
    {
        if (!AssetDatabase.IsValidFolder(targetDir))
            Directory.CreateDirectory(targetDir);

        string newPath = $"{targetDir}/{fileName}";
        newPath = AvoidOverwrite(newPath);

        string error = AssetDatabase.MoveAsset(oldPath, newPath);
        if (!string.IsNullOrEmpty(error))
            Debug.LogWarning($"⚠️ 이동 실패: {oldPath} → {newPath}\n{error}");
    }

    static string AvoidOverwrite(string path)
    {
        if (!File.Exists(path))
            return path;

        string dir = Path.GetDirectoryName(path);
        string name = Path.GetFileNameWithoutExtension(path);
        string ext = Path.GetExtension(path);
        int i = 1;

        string newPath;
        do
        {
            newPath = $"{dir}/{name} ({i++}){ext}";
        } while (File.Exists(newPath));

        return newPath;
    }
}
