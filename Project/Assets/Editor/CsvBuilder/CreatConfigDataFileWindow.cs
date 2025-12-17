using System.IO;
using UnityEditor;
using UnityEngine;

//  编辑器Window界面
public class CreatConfigDataFileWindow : EditorWindow
{

    static string writePath = "/Scripts/Data/LocalCsvData/";
    static string difficultyConfigWritePath = "/Scripts/Data/LocalCsvData/DifficultyConfig/";
    static Object selectObj;


    [MenuItem("Tools/Create csv data class")]
    static void ByWindow()
    {
        EditorWindow.GetWindow<CreatConfigDataFileWindow>();
    }

    private void OnGUI()
    {

        GUILayout.Label("设置本地配置数据结构类的输出路径");
        writePath = GUILayout.TextField(writePath);
        GUILayout.Label("请选择一个合法的csv文件");

        if (GUILayout.Button("生成 C#协议 数据结构类"))
        {
            if (selectObj != null)
            {
                Debug.Log("生成 C#协议 数据结构类----------");
                CreatConfigUitl.CreatLocalConfigFile(selectObj, writePath);
            }

        }

        GUILayout.Label("设置 难度 数据结构类的输出路径");
        difficultyConfigWritePath = GUILayout.TextField(difficultyConfigWritePath);
        GUILayout.Label("请选择一个合法的csv文件");

        if (GUILayout.Button("生成 难度配置 数据结构类"))
        {
            if (selectObj != null)
            {
                Debug.Log("生成 难度配置 数据结构类----------");
                CreatConfigUitl.CreatDifficultyConfigFile(selectObj, difficultyConfigWritePath);
            }

        }

        if (Selection.activeObject != null)
        {
            string path = AssetDatabase.GetAssetPath(Selection.activeObject);

            // 防止因为选中的对象不是资源路径下的对象而一直报错
            if (!string.IsNullOrEmpty(path))
            {
                if (path.ToLower().Substring(path.Length - 4, 4) == ".csv")
                {
                    GUILayout.Label("已选中CSV文件：");
                    // 检查文件锁定状态
                    string fullPath = Path.GetFullPath(path);
                    bool fileLocked = false;

                    try
                    {
                        using (FileStream fs = File.Open(fullPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                        {
                            fileLocked = false;
                        }
                    }
                    catch (IOException)
                    {
                        fileLocked = true;
                    }

                    if (fileLocked)
                    {
                        selectObj = null;
                        GUILayout.Label("请关闭文件！");
                    }
                    else
                    {
                        selectObj = Selection.activeObject;
                        GUILayout.Label(path);
                    }
                }
            }
        }
    }

    private void OnSelectionChange()
    {
        Repaint();
    }
}
