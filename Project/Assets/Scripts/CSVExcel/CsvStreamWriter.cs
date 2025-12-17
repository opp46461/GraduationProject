using System.IO;
using System.Text;
using UnityEngine;

public class CsvStreamWriter : ICsvWriter
{
    private StreamWriter writer;
    private StringBuilder currentLine;

    public CsvStreamWriter(string filePath, Encoding encoding = null)
    {
        if (encoding == null)
            encoding = Encoding.UTF8;

        // 确保目录存在
        string directory = Path.GetDirectoryName(filePath);
        if (!Directory.Exists(directory))
            Directory.CreateDirectory(directory);

        writer = new StreamWriter(filePath, false, encoding);
        currentLine = new StringBuilder();
    }

    /// <summary>
    /// 写入注释行（自动添加#前缀）
    /// </summary>
    public void WriteCommentLine(string comment)
    {
        writer.WriteLine("# " + comment);
    }

    /// <summary>
    /// 写入空行
    /// </summary>
    public void WriteEmptyLine()
    {
        writer.WriteLine();
    }

    /// <summary>
    /// 开始新行
    /// </summary>
    public void BeginNewLine()
    {
        if (currentLine.Length > 0)
        {
            WriteCurrentLine();
        }
        currentLine.Clear();
    }

    /// <summary>
    /// 添加字段到当前行
    /// </summary>
    public void AddField(object fieldValue)
    {
        if (currentLine.Length > 0)
            currentLine.Append(",");

        string fieldString = fieldValue?.ToString() ?? "";

        // 处理需要引号的情况：包含逗号、换行符、引号
        if (fieldString.Contains(",") || fieldString.Contains("\"") ||
            fieldString.Contains("\n") || fieldString.Contains("\r"))
        {
            // 转义引号：把 " 替换为 ""
            fieldString = fieldString.Replace("\"", "\"\"");
            currentLine.Append($"\"{fieldString}\"");
        }
        else
        {
            currentLine.Append(fieldString);
        }
    }

    /// <summary>
    /// 写入当前行
    /// </summary>
    private void WriteCurrentLine()
    {
        writer.WriteLine(currentLine.ToString());
    }

    /// <summary>
    /// 写入完整的行（自动开始新行）
    /// </summary>
    public void WriteLine(params object[] fields)
    {
        BeginNewLine();
        foreach (object field in fields)
        {
            AddField(field);
        }
        WriteCurrentLine();
    }

    /// <summary>
    /// 关闭写入器
    /// </summary>
    public void Close()
    {
        if (currentLine.Length > 0)
            WriteCurrentLine();

        writer?.Close();
        writer?.Dispose();
    }
}