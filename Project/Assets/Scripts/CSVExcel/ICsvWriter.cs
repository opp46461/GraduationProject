/// <summary>
/// CSV写入器接口（统一编辑器与运行时）
/// </summary>
public interface ICsvWriter
{
    void WriteCommentLine(string comment);
    void WriteEmptyLine();
    void BeginNewLine();
    void AddField(object fieldValue);
}