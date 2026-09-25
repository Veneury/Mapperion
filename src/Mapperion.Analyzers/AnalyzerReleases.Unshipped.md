; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|------
MPR1001 | Mapperion | Warning | The same pair is declared more than once
MPR1002 | Mapperion | Warning | A destination member is configured more than once
MPR1003 | Mapperion | Warning | A member is both ignored and given a source
MPR1004 | Mapperion | Warning | ConstructUsing and ForCtorParam on the same map
