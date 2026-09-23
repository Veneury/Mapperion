; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|-----------|----------|-------
MPR0001 | Mapperion | Error | A class marked with [Mapper] must be partial
MPR0002 | Mapperion | Error | A destination member has no source
MPR0003 | Mapperion | Error | No conversion is available between two member types
MPR0004 | Mapperion | Error | The destination cannot be constructed
MPR0005 | Mapperion | Error | A mapping method has an unsupported signature
MPR0006 | Mapperion | Error | An attribute names a member that does not exist
