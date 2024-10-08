﻿namespace Sojj;

public enum JudgeStatus
{
    STATUS_ACCEPTED = 1,
    STATUS_WRONG_ANSWER = 2,
    STATUS_TIME_LIMIT_EXCEEDED = 3,
    STATUS_MEMORY_LIMIT_EXCEEDED = 4,
    STATUS_RUNTIME_ERROR = 6,
    STATUS_COMPILE_ERROR = 7,
    STATUS_SYSTEM_ERROR = 8,
    STATUS_JUDGING = 20,
    STATUS_COMPILING = 21
}

public static class JudgeStatusExtensions
{
    public static string ToStatusString(this JudgeStatus status)
    {
        return status switch
        {
            JudgeStatus.STATUS_ACCEPTED => Constants.Accepted,
            JudgeStatus.STATUS_WRONG_ANSWER => Constants.WrongAnswer,
            JudgeStatus.STATUS_TIME_LIMIT_EXCEEDED => Constants.TimeLimitExceeded,
            JudgeStatus.STATUS_MEMORY_LIMIT_EXCEEDED => Constants.MemoryLimitExceeded,
            JudgeStatus.STATUS_RUNTIME_ERROR => Constants.RuntimeError,
            JudgeStatus.STATUS_COMPILE_ERROR => Constants.CompilationError,
            JudgeStatus.STATUS_SYSTEM_ERROR => Constants.SystemError,
            JudgeStatus.STATUS_JUDGING => Constants.Judging,
            JudgeStatus.STATUS_COMPILING => Constants.Compiling,
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
        };
    }

    public static JudgeStatus ToJudgeStatus(this string status)
    {
        return status switch
        {
            Constants.Accepted => JudgeStatus.STATUS_ACCEPTED,
            Constants.WrongAnswer => JudgeStatus.STATUS_WRONG_ANSWER,
            Constants.TimeLimitExceeded => JudgeStatus.STATUS_TIME_LIMIT_EXCEEDED,
            Constants.MemoryLimitExceeded => JudgeStatus.STATUS_MEMORY_LIMIT_EXCEEDED,
            Constants.RuntimeError => JudgeStatus.STATUS_RUNTIME_ERROR,
            Constants.CompilationError => JudgeStatus.STATUS_COMPILE_ERROR,
            Constants.SystemError => JudgeStatus.STATUS_SYSTEM_ERROR,
            Constants.Judging => JudgeStatus.STATUS_JUDGING,
            Constants.Compiling => JudgeStatus.STATUS_COMPILING,
            Constants.NonzeroExitStatus => JudgeStatus.STATUS_RUNTIME_ERROR,
            Constants.FileError => JudgeStatus.STATUS_SYSTEM_ERROR,
            Constants.Signalled => JudgeStatus.STATUS_RUNTIME_ERROR,
            Constants.InternalError => JudgeStatus.STATUS_SYSTEM_ERROR,
            Constants.OutputLimitExceeded => JudgeStatus.STATUS_RUNTIME_ERROR,
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
        };
    }
}
