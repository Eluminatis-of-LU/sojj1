namespace Sojj.Dtos;

public class CompileResult
{
    public JudgeStatus Status { get; set; }

    public string Message { get; set; }

    public string RunId { get; set; }

    public string Language { get; set; }

    public string OutputFileId { get; set; }

    public string[] ExecuteArgs { get; set; }

    public string OutputFile { get; set; }

}
