namespace E3A.Application.Publishing.Security;

public static class ScanRuleIds
{
    public const string CredentialPathReference = "EXF001";
    public const string CredentialReadToNetworkSink = "EXF002";
    public const string EnvironmentDumpToNetworkSink = "EXF003";
    public const string KnownExfiltrationSinkHost = "EXF004";
    public const string RawInternetProtocolEndpoint = "EXF005";
    public const string Base64DecodeToShell = "ENC001";
    public const string DynamicEvaluationOfEncodedPayload = "ENC002";
    public const string Base64Wall = "ENC003";
    public const string RecursiveRootDeletion = "CMD001";
    public const string ForkBomb = "CMD002";
    public const string FilesystemDestruction = "CMD003";
    public const string SecurityControlTampering = "CMD004";
    public const string RemoteScriptToInterpreter = "CMD005";
    public const string IgnorePreviousInstructions = "INJ001";
    public const string InjectionWithExfiltration = "INJ002";
    public const string ConcealmentFromUser = "INJ003";
    public const string CovertAction = "INJ004";
    public const string OutsideWorkspaceReadAndTransmit = "INJ005";
    public const string ExecutableMagicBytes = "HYG001";
    public const string FileOverSizeCap = "HYG002";
    public const string LineOverLengthCap = "HYG003";
    public const string ScriptNetworkCall = "SCR001";
    public const string ScriptPersistence = "SCR002";
    public const string ScriptPrivilegeEscalation = "SCR003";
    public const string ScriptReverseShell = "SCR004";
}
