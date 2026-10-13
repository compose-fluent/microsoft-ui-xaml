param(
    [Parameter(Mandatory)] [string] $Compiler,
    [Parameter(Mandatory)] [string] $SdkCompilerAssembly
)
$ErrorActionPreference = 'Stop'
# SDK GenXbf queries these private COM interfaces when metadata describes a
# component type. Built-in controls alone do not exercise the private ABI.
$actual = [Reflection.Assembly]::LoadFrom((Resolve-Path -LiteralPath $Compiler).Path)
$reference = [Reflection.Assembly]::LoadFrom((Resolve-Path -LiteralPath $SdkCompilerAssembly).Path)
foreach ($name in @('IXbfMember', 'IXbfType', 'IXbfMetadataProvider')) {
    $actualType = $actual.GetType("Microsoft.UI.Xaml.Markup.Compiler.XBF.$name", $true)
    $referenceType = $reference.GetType("Microsoft.Xaml.XBF.$name", $true)
    if ($actualType.GUID -ne $referenceType.GUID) { throw "$name has the wrong SDK COM IID." }
    $actualSlots = @($actualType.GetMethods() | Sort-Object MetadataToken)
    $referenceSlots = @($referenceType.GetMethods() | Sort-Object MetadataToken)
    if ($actualSlots.Count -ne $referenceSlots.Count) { throw "$name has the wrong SDK vtable length." }
    for ($i = 0; $i -lt $referenceSlots.Count; $i++) {
        $a = $actualSlots[$i]
        $r = $referenceSlots[$i]
        if ($a.Name -ne $r.Name -or $a.ReturnType.Name -ne $r.ReturnType.Name -or
            (($a.GetParameters() | ForEach-Object { $_.ParameterType.Name }) -join ',') -ne
            (($r.GetParameters() | ForEach-Object { $_.ParameterType.Name }) -join ',')) {
            throw "$name vtable slot $i differs from the SDK: $a versus $r."
        }
    }
}
Write-Output 'UWP XBF private COM interfaces match the selected Windows SDK.'
