using System.Text;
using UnityEngine;

public class CsvStringWriter : ICsvWriter
{
    private StringBuilder csvBuilder;
    private StringBuilder currentLine;

    public CsvStringWriter()
    {
        csvBuilder = new StringBuilder();
        currentLine = new StringBuilder();
    }

    /// <summary>
    /// 写入注释行
    /// </summary>
    public void WriteCommentLine(string comment)
    {
        EnsureLineEnd();
        csvBuilder.AppendLine("# " + comment);
    }

    /// <summary>
    /// 写入空行
    /// </summary>
    public void WriteEmptyLine()
    {
        EnsureLineEnd();
        csvBuilder.AppendLine();
    }

    /// <summary>
    /// 开始新行
    /// </summary>
    public void BeginNewLine()
    {
        EnsureLineEnd();
    }

    /// <summary>
    /// 添加字段到当前行
    /// </summary>
    public void AddField(object fieldValue)
    {
        if (currentLine.Length > 0)
            currentLine.Append(",");

        string fieldString = fieldValue?.ToString() ?? "";

        if (NeedQuotes(fieldString))
        {
            fieldString = fieldString.Replace("\"", "\"\"");
            currentLine.Append($"\"{fieldString}\"");
        }
        else
        {
            currentLine.Append(fieldString);
        }
    }

    /// <summary>
    /// 判断字段是否需要引号
    /// </summary>
    private bool NeedQuotes(string field)
    {
        return field.Contains(",") || field.Contains("\"") ||
               field.Contains("\n") || field.Contains("\r");
    }

    /// <summary>
    /// 写入完整的行
    /// </summary>
    public void WriteLine(params object[] fields)
    {
        BeginNewLine();
        foreach (object field in fields)
        {
            AddField(field);
        }
        EnsureLineEnd();
    }

    /// <summary>
    /// 确保当前行结束
    /// </summary>
    private void EnsureLineEnd()
    {
        if (currentLine.Length > 0)
        {
            csvBuilder.AppendLine(currentLine.ToString());
            currentLine.Clear();
        }
    }

    /// <summary>
    /// 获取生成的CSV字符串
    /// </summary>
    public string GetCsvString()
    {
        EnsureLineEnd();
        return csvBuilder.ToString();
    }

    /// <summary>
    /// 保存到文件（运行时可用路径）
    /// </summary>
    public void SaveToFile(string fileName = "exported_data.csv")
    {
        string filePath = System.IO.Path.Combine(Application.persistentDataPath, fileName);
        System.IO.File.WriteAllText(filePath, GetCsvString(), Encoding.UTF8);
        Debug.Log($"CSV文件已保存到: {filePath}");
    }

    public void SaveToFullPath(string fullPath)
    {
        if (!PathHelper.EnsureDirectoryExists(fullPath)) return;
        System.IO.File.WriteAllText(fullPath, GetCsvString(), Encoding.UTF8);
    }
}