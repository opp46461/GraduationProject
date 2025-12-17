using UnityEngine;
using System.Collections.Generic;

public class ConfigExportTest : MonoBehaviour
{
    [Header("导出测试")]
    public bool exportOnStart = true;
    public string exportFileName = "test_export.csv";

    void Start()
    {
        if (exportOnStart)
        {
            TestRuntimeExport();
            //TestEditorFormatExport();
        }
    }

    /// <summary>
    /// 测试运行时导出
    /// </summary>
    void TestRuntimeExport()
    {
        Debug.Log("=== 运行时CSV导出测试 ===");

        // 创建测试数据
        var testData = new List<TestConfigData>();
        testData.Add(new TestConfigData { id = 1, name = "测试物品1", value = 100.5f, description = "这是一个测试物品" });
        testData.Add(new TestConfigData { id = 2, name = "测试物品2", value = 200.75f, description = "另一个测试,包含逗号" });
        testData.Add(new TestConfigData { id = 3, name = "测试物品3", value = 300.0f, description = "正常描述" });

        // 导出到字符串
        CsvStringWriter writer = ConfigDataExporter.ExportToCsvString(testData);
        writer.SaveToFile(exportFileName);
        Debug.Log("=== 运行时导出测试完成 ===");
    }

    /// <summary>
    /// 测试编辑器格式导出（匹配你的CSV格式）
    /// </summary>
    void TestEditorFormatExport()
    {
        Debug.Log("=== 编辑器格式CSV导出测试 ===");

        var testData = new List<TestConfigData>();
        testData.Add(new TestConfigData { id = 101, name = "编辑器物品1", value = 50.25f, description = "编辑器测试" });
        testData.Add(new TestConfigData { id = 102, name = "编辑器物品2", value = 75.50f, description = "另一个测试" });

        // 模拟编辑器格式：类型行 + 字段名行 + 数据行
        CsvStringWriter writer = new CsvStringWriter();

        writer.WriteCommentLine("Auto-generated Config File");
        writer.WriteCommentLine("Do not modify manually");
        writer.WriteEmptyLine();

        // 类型行
        writer.BeginNewLine();
        writer.AddField(""); // 空单元格
        writer.AddField("int");
        writer.AddField("string");
        writer.AddField("float");
        writer.AddField("string");

        // 字段名行  
        writer.BeginNewLine();
        writer.AddField(""); // 空单元格
        writer.AddField("ID");
        writer.AddField("Name");
        writer.AddField("Value");
        writer.AddField("Description");

        // 数据行
        int rowNum = 1;
        foreach (var item in testData)
        {
            writer.BeginNewLine();
            writer.AddField(""); // 空单元格
            writer.AddField(item.id);
            writer.AddField(item.name);
            writer.AddField(item.value);
            writer.AddField(item.description);
            rowNum++;
        }

        string csvContent = writer.GetCsvString();
        Debug.Log("编辑器格式CSV:\n" + csvContent);
        writer.SaveToFile("editor_format_" + exportFileName);

        Debug.Log("=== 编辑器格式导出测试完成 ===");
    }
}

/// <summary>
/// 测试配置数据结构
/// </summary>
[System.Serializable]
public class TestConfigData : LocalConfigDataBase
{
    public int id;
    public string name;
    public float value;
    public string description;
}