param(
    [Parameter(Mandatory)] [string] $Compiler,
    [Parameter(Mandatory)] [string[]] $ReferenceDirectories,
    [Parameter(Mandatory)] [string] $OutputDirectory
)
$ErrorActionPreference = 'Stop'
$Compiler = (Resolve-Path -LiteralPath $Compiler).Path
$root = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Force $root | Out-Null
$references = @($ReferenceDirectories | ForEach-Object {
    Get-ChildItem -LiteralPath $_ -Filter *.winmd
} | Sort-Object FullName | ForEach-Object {
    @{ ItemSpec = $_.FullName; FullPath = $_.FullName; IsSystemReference = $true }
})
if (!$references.Count) { throw 'No WinMD references supplied.' }
$searchPaths = @($ReferenceDirectories + "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319" | ForEach-Object {
    @{ ItemSpec = $_; FullPath = $_ }
})
$xaml = @'
<Page xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
      xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
      x:Class="probe.MainPage">
  <Button x:Name="myButton" Content="Click" Click="onClick" />
</Page>
'@
$pagePath = Join-Path $root 'MainPage.xaml'
[IO.File]::WriteAllText($pagePath, $xaml)
$inputs = @{
    ProjectPath = "$root/probe.proj"; ProjectName = 'probe'; Language = 'Kotlin'; LanguageSourceExtension = '.kt'
    OutputPath = "$root/generated"; IsPass1 = $true; RootNamespace = 'probe'; OutputType = 'WinExe'
    TargetPlatformMinVersion = '10.0.19041.0'; ReferenceAssemblies = $references; ReferenceAssemblyPaths = $searchPaths
    XamlPages = @(@{ ItemSpec = $pagePath; FullPath = $pagePath }); SavedStateFile = "$root/state.xml"
    DisableXbfGeneration = $true
}
function Compile([string] $name, [int] $expectedExit = 0) {
    $inputPath = Join-Path $root "$name.input.json"
    $outputPath = Join-Path $root "$name.output.json"
    $inputs | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $inputPath -Encoding utf8
    & $Compiler $inputPath $outputPath
    $compilerExit = $LASTEXITCODE
    $result = Get-Content -LiteralPath $outputPath -Raw | ConvertFrom-Json
    if ($compilerExit -ne $expectedExit) {
        $errors = ($result.MSBuildLogEntries | Where-Object Type -EQ 2 | ForEach-Object Message) -join '; '
        throw "$name exit $compilerExit, expected ${expectedExit}: $errors"
    }
    return $result
}
function Assert([bool] $condition, [string] $message) {
    if (!$condition) { throw $message }
}
$first = (Compile 'first').KotlinDeclarations
Assert ($first.SchemaVersion -eq 1) 'Missing protocol version.'
Assert ($first.Pages.Count -eq 1) 'Expected exactly one page.'
$page = $first.Pages[0]
Assert ($page.ClassName -eq 'probe.MainPage') 'Incorrect x:Class.'
Assert ($page.ResourcePath -eq 'MainPage.xaml') 'Absolute or incorrect resource path.'
Assert ($page.BaseTypeName -eq 'Microsoft.UI.Xaml.Controls.Page') 'Incorrect root type.'
$button = @($page.Connections | Where-Object FieldName -EQ 'myButton')[0]
Assert ($button.TypeName -eq 'Microsoft.UI.Xaml.Controls.Button') 'Incorrect named-element type.'
Assert ($button.Events[0].HandlerName -eq 'onClick') 'Missing event handler.'
Assert ($button.Events[0].Location.Line -eq 4 -and $button.Events[0].Location.Column -eq 45) 'Incorrect source location.'
$second = (Compile 'second').KotlinDeclarations
Assert (($first | ConvertTo-Json -Depth 12 -Compress) -ceq ($second | ConvertTo-Json -Depth 12 -Compress)) 'Non-deterministic index.'

$inputs.KotlinSymbols = @{
    SchemaVersion = 1; DeclarationFingerprint = ('a' * 64); Declarations = $first
    Pages = @(@{ ClassName = 'probe.MainPage'; Handlers = @(@{
        Name = 'onClick'; ReturnTypeName = 'System.Void'
        ParameterTypeNames = @('System.Object', 'Microsoft.UI.Xaml.RoutedEventArgs')
    }) })
}
$null = Compile 'valid-handler'
$inputs.KotlinSymbols.Pages[0].Handlers[0].ParameterTypeNames = @('System.Object', 'System.String')
$invalid = Compile 'invalid-handler' 1
Assert (($invalid.MSBuildLogEntries | Where-Object Type -EQ 2).Message -match 'does not match') 'Wrong event signature was not diagnosed.'
$inputs.KotlinSymbols.Pages[0].Handlers[0].ParameterTypeNames = @('System.Object', 'Microsoft.UI.Xaml.RoutedEventArgs')
[IO.File]::WriteAllText($pagePath, $xaml.Replace('myButton', 'renamedButton'))
$stale = Compile 'stale-symbols' 1
Assert (($stale.MSBuildLogEntries | Where-Object Type -EQ 2).Message -match 'changed after semantic') 'Stale semantic declarations were accepted.'
$inputs.Remove('KotlinSymbols')

[IO.File]::WriteAllText($pagePath, $xaml.Replace('myButton', 'renamedButton'))
$renamed = (Compile 'rename').KotlinDeclarations
Assert (@($renamed.Pages[0].Connections | Where-Object FieldName -EQ 'myButton').Count -eq 0) 'Stale named element.'
Assert (@($renamed.Pages[0].Connections | Where-Object FieldName -EQ 'renamedButton').Count -eq 1) 'Missing renamed element.'

[IO.File]::WriteAllText($pagePath, $xaml.Replace('Content="Click"', 'Content="{x:Bind Value}"'))
$failed = Compile 'unsupported-binding' 1
Assert ($null -eq $failed.KotlinDeclarations) 'Failed compilation returned an index.'

$inputs.XamlPages = @()
$empty = (Compile 'deleted').KotlinDeclarations
Assert ($empty.Pages.Count -eq 0 -and $empty.Resources.Count -eq 0) 'Deleted input retained stale declarations.'
$inputs.IsPass1 = $false
$inputs.KotlinSymbols = @{ SchemaVersion = 1; DeclarationFingerprint = ('b' * 64); Declarations = $empty; Pages = @() }
$emptyFinal = Compile 'deleted-final'
Assert ($emptyFinal.KotlinImplementation.Declarations.Pages.Count -eq 0) 'Empty final compilation retained stale pages.'
Assert ($emptyFinal.GeneratedXbfFiles.Count -eq 0) 'Empty final compilation retained stale XBF outputs.'
$inputs.IsPass1 = $true
$inputs.Remove('KotlinSymbols')

$dictionaryPath = Join-Path $root 'Resources.xaml'
[IO.File]::WriteAllText($dictionaryPath, '<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" />')
$inputs.XamlPages = @(@{ ItemSpec = $dictionaryPath; FullPath = $dictionaryPath })
$resource = (Compile 'resource').KotlinDeclarations
Assert ($resource.Pages.Count -eq 0 -and $resource.Resources[0] -eq 'Resources.xaml') 'Classless dictionary became a page.'

[IO.File]::WriteAllText($pagePath, $xaml)
$inputs.XamlPages = @(@{ ItemSpec = $pagePath; FullPath = $pagePath })
foreach ($language in @('C#', 'CppWinRT')) {
    $inputs.Language = $language
    $inputs.LanguageSourceExtension = if ($language -eq 'C#') { '.cs' } else { '.cpp' }
    $inputs.OutputPath = Join-Path $root $language.Replace('#', 'Sharp')
    $inputs.SavedStateFile = Join-Path $inputs.OutputPath 'state.xml'
    $legacy = Compile $language.Replace('#', 'Sharp')
    Assert ($null -eq $legacy.KotlinDeclarations) 'Legacy language emitted Kotlin declarations.'
    Assert ($legacy.GeneratedCodeFiles.Count -gt 0) 'Legacy language no longer generated source.'
}
Write-Output 'Kotlin declaration, repeat, rename, deletion, resource, rejection and legacy-language smoke checks passed.'
