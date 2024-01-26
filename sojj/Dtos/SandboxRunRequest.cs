using System.Text.Json.Serialization;

namespace Sojj.Dtos;

public class SandboxRunRequest
{
    [JsonPropertyName("cmd")]
    public Command[] Commands { get; set; }
}

public class Command
{
    [JsonPropertyName("args")]
    public string[] Args { get; set; }

    [JsonPropertyName("env")]
    public string[]? Env { get; set; }

    [JsonPropertyName("files")]
    public SandboxFile[]? Files { get; set; }

    [JsonPropertyName("clockLimit")]
    public long? ClockLimit { get; set; }

    [JsonPropertyName("cpuLimit")]
    public long? CpuLimit { get; set; }

    [JsonPropertyName("memoryLimit")]
    public long? MemoryLimit { get; set; }

    [JsonPropertyName("procLimit")]
    public int? ProcessLimit { get; set; }

    [JsonPropertyName("copyIn")]
    public Dictionary<string, SandboxFile>? CopyIn { get; set; }

    [JsonPropertyName("copyOut")]
    public string[]? CopyOut { get; set; }

    [JsonPropertyName("copyOutCached")]
    public string[]? CopyOutCached { get; set; }

    [JsonPropertyName("copyOutDir")]
    public string? CopyOutDir { get; set; }

    [JsonPropertyName("copyOutMax")]
    public int? CopyOutMax { get; set; }
}

[JsonDerivedType(typeof(SandboxMemoryFile), "content")]
[JsonDerivedType(typeof(SandboxLocalFile), "src")]
[JsonDerivedType(typeof(SandboxPreparedFile), "fileId")]
[JsonDerivedType(typeof(SandboxCollectorFile), "name")]
[JsonDerivedType(typeof(SandboxSymlinkFile), "symlink")]
public abstract class SandboxFile
{
}

public class SandboxMemoryFile : SandboxFile
{
    [JsonPropertyName("content")]
    public string Content { get; set; }
}

public class SandboxLocalFile : SandboxFile
{
    [JsonPropertyName("src")]
    public string Source { get; set; }
}

public class SandboxPreparedFile : SandboxFile
{
    [JsonPropertyName("fileId")]
    public string FileId { get; set; }
}

public class SandboxCollectorFile : SandboxFile
{
    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("max")]
    public long Max { get; set; }

    [JsonPropertyName("pipe")]
    public bool Pipe { get; set; }
}

public class SandboxSymlinkFile : SandboxFile
{
    [JsonPropertyName("symlink")]
    public string Symlink { get; set; }
}
