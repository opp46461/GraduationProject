
using UnityEngine;
using System.IO;
using UnityEditor;


// 配置类生成工具（配置数据结构类的生成）
public class CreatConfigUitl 
{
    public static void CreatLocalConfigFile(Object selectObj, string writePath)
    {
        string fileName = selectObj.name;
        string className = fileName;
        // 创建 C# 类文件
        StreamWriter sw = new StreamWriter(Application.dataPath + writePath + className + ".cs");

        sw.WriteLine("using UnityEngine;\nusing System.Collections;\n");
        // 继承自 LocalConfigDataBase
        sw.WriteLine("public partial class " + className + " : LocalConfigDataBase");
        sw.WriteLine("{");

        string filePath = AssetDatabase.GetAssetPath(selectObj);
        CsvStreamReader csr = new CsvStreamReader(filePath);
        // 遍历 CSV 列，生成字段定义（第3行是字段名，第2行是字段类型）
        for (int colNum = 1; colNum < csr.ColCount + 1; colNum++)
        {
            string fieldName = csr[3, colNum];
            string fieldType = csr[2, colNum];
            Debug.Log("\t" + "public " + fieldType + " " + fieldName + ";" + "");
            sw.WriteLine("\t" + "public " + fieldType + " " + fieldName + ";" + "");
        }
        sw.WriteLine("\t" + "public override string GetFilePath()");
        sw.WriteLine("\t" + "{");
        sw.WriteLine("\t\t" + "return " + "\"" + fileName + "\";");
        sw.WriteLine("\t" + "}");
        sw.WriteLine("}");

        sw.Flush();
        sw.Close();
        // 刷新资源数据库，让新文件立即显示
        AssetDatabase.Refresh();
    }

    public static void CreatDifficultyConfigFile(Object selectObj, string writePath)
    {
        string fileName = selectObj.name;
        string className = fileName;
        // 创建 C# 类文件
        StreamWriter sw = new StreamWriter(Application.dataPath + writePath + className + ".cs");

        sw.WriteLine("using UnityEngine;\nusing System.Collections;\n");
        // 继承自 LocalConfigDataBase
        sw.WriteLine("public partial class " + className + " : GameDifficultyConfigDataBase");
        sw.WriteLine("{");

        string filePath = AssetDatabase.GetAssetPath(selectObj);
        CsvStreamReader csr = new CsvStreamReader(filePath);
        // 遍历 CSV 列，生成字段定义（第3行是字段名，第2行是字段类型）
        for (int colNum = 8; colNum < csr.ColCount + 1; colNum++)
        {
            string fieldName = csr[3, colNum];
            string fieldType = csr[2, colNum];
            Debug.Log("\t" + "public " + fieldType + " " + fieldName + ";" + "");
            sw.WriteLine("\t" + "public " + fieldType + " " + fieldName + ";" + "");
        }
        sw.WriteLine("\t" + "public override string GetFilePath()");
        sw.WriteLine("\t" + "{");
        sw.WriteLine("\t\t" + "return " + "\"" + fileName + "\";");
        sw.WriteLine("\t" + "}");
        sw.WriteLine("}");

        sw.Flush();
        sw.Close();
        // 刷新资源数据库，让新文件立即显示
        AssetDatabase.Refresh();
    }
}
