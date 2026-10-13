param(
    [Parameter(Mandatory)] [string] $Compiler,
    [Parameter(Mandatory)] [string[]] $ReferenceDirectories,
    [Parameter(Mandatory)] [string] $OutputDirectory,
    [ValidateSet('winui', 'uwp')] [string] $Framework = 'winui',
    [string] $GenXbfDirectory
)
$ErrorActionPreference = 'Stop'
$xamlNamespace = if ($Framework -eq 'uwp') { 'Windows.UI.Xaml' } else { 'Microsoft.UI.Xaml' }
$Compiler = (Resolve-Path -LiteralPath $Compiler).Path
$root = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Force $root | Out-Null
$references = @($ReferenceDirectories | ForEach-Object {
    Get-ChildItem -LiteralPath $_ -Filter *.winmd
} | Sort-Object FullName | ForEach-Object {
    # These are individual SDK contracts, not the Windows.winmd facade that XAMLC
    # eagerly loads for MSBuild. Keep the complete resolved set visible in pass 1,
    # matching the Kotlin Gradle caller instead of deferring it as system extras.
    @{ ItemSpec = $_.FullName; FullPath = $_.FullName }
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
    XamlFramework = $Framework
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
Assert ($first.Framework -eq $Framework) 'Incorrect XAML framework.'
Assert ($first.SchemaVersion -eq 3) 'Missing protocol version.'
Assert ($first.Pages.Count -eq 1) 'Expected exactly one page.'
$page = $first.Pages[0]
Assert ($page.ClassName -eq 'probe.MainPage') 'Incorrect x:Class.'
Assert ($page.ResourcePath -eq 'MainPage.xaml') 'Absolute or incorrect resource path.'
Assert ($page.BaseTypeName -eq "$xamlNamespace.Controls.Page") 'Incorrect root type.'
$button = @($page.Connections | Where-Object FieldName -EQ 'myButton')[0]
Assert ($button.TypeName -eq "$xamlNamespace.Controls.Button") 'Incorrect named-element type.'
Assert ($button.Events[0].HandlerName -eq 'onClick') 'Missing event handler.'
Assert ($button.Events[0].Location.Line -eq 4 -and $button.Events[0].Location.Column -eq 45) 'Incorrect source location.'
$second = (Compile 'second').KotlinDeclarations
$inputs.Remove('XamlFramework')
$implicitFramework = (Compile 'implicit-framework').KotlinDeclarations
Assert ($implicitFramework.Framework -eq $Framework) 'Legacy input did not use the compiler artifact framework.'
$inputs.XamlFramework = $Framework
Assert (($first | ConvertTo-Json -Depth 12 -Compress) -ceq ($second | ConvertTo-Json -Depth 12 -Compress)) 'Non-deterministic index.'

$inputs.KotlinSymbols = @{
    SchemaVersion = $first.SchemaVersion; DeclarationFingerprint = ('a' * 64); Declarations = $first
    Pages = @(@{ ClassName = 'probe.MainPage'; Handlers = @(@{
        Name = 'onClick'; ReturnTypeName = 'System.Void'
        ParameterTypeNames = @('System.Object', "$xamlNamespace.RoutedEventArgs")
    }) })
}
$null = Compile 'valid-handler'
$inputs.KotlinSymbols.Declarations.Framework = if ($Framework -eq 'uwp') { 'winui' } else { 'uwp' }
$wrongFramework = Compile 'wrong-framework-symbols' 1
Assert (($wrongFramework.MSBuildLogEntries | Where-Object Type -EQ 2).Message -match 'changed after semantic') 'Symbols from another framework were accepted.'
$inputs.KotlinSymbols.Declarations.Framework = $Framework
$inputs.KotlinSymbols.Pages[0].Handlers[0].ParameterTypeNames = @('System.Object', 'System.String')
$invalid = Compile 'invalid-handler' 1
Assert (($invalid.MSBuildLogEntries | Where-Object Type -EQ 2).Message -match 'does not match') 'Wrong event signature was not diagnosed.'
$inputs.KotlinSymbols.Pages[0].Handlers[0].ParameterTypeNames = @('System.Object', "$xamlNamespace.RoutedEventArgs")
[IO.File]::WriteAllText($pagePath, $xaml.Replace('myButton', 'renamedButton'))
$stale = Compile 'stale-symbols' 1
Assert (($stale.MSBuildLogEntries | Where-Object Type -EQ 2).Message -match 'changed after semantic') 'Stale semantic declarations were accepted.'
$inputs.Remove('KotlinSymbols')

[IO.File]::WriteAllText($pagePath, $xaml.Replace('myButton', 'renamedButton'))
$renamed = (Compile 'rename').KotlinDeclarations
Assert (@($renamed.Pages[0].Connections | Where-Object FieldName -EQ 'myButton').Count -eq 0) 'Stale named element.'
Assert (@($renamed.Pages[0].Connections | Where-Object FieldName -EQ 'renamedButton').Count -eq 1) 'Missing renamed element.'

[IO.File]::WriteAllText($pagePath, $xaml.Replace('Content="Click"', 'Content="{x:Bind Value}"'))
$bound = (Compile 'compiled-binding').KotlinDeclarations
$binding = @($bound.Pages[0].Connections | Where-Object FieldName -EQ 'myButton')[0].Bindings[0]
Assert ($binding.Name -eq 'Content' -and $binding.Expression.Name -eq 'Value' -and $binding.Mode -eq 'OneTime') 'Compiled binding expression was not exported.'

$propertyXaml = $xaml.Replace('  <Button', @'
  <x:Properties>
    <x:Property Name="Title" Type="x:String" DefaultValue="escaped &quot;text&quot;" />
    <x:Property Name="Count" Type="x:Int32" IsReadOnly="True" DefaultValue="2" />
    <x:Property Name="ContentValue" Type="Button" ChangedHandler="ContentValueUpdated">
      <x:Property.DefaultValue><Button Content="Default" /></x:Property.DefaultValue>
    </x:Property>
  </x:Properties>
  <Button
'@)
[IO.File]::WriteAllText($pagePath, $propertyXaml)
$propertyIndex = (Compile 'properties').KotlinDeclarations
$properties = $propertyIndex.Pages[0].Properties
Assert ($properties.Count -eq 3 -and 'properties' -in $propertyIndex.Pages[0].Features) 'Missing x:Properties declarations.'
$title = @($properties | Where-Object Name -EQ 'Title')[0]
Assert ($title.DefaultValue -ceq 'escaped "text"' -and $title.ChangedHandler -eq 'TitleChanged' -and !$title.IsValueType) 'Incorrect string property.'
$count = @($properties | Where-Object Name -EQ 'Count')[0]
Assert ($count.DefaultValue -ceq '2' -and $count.IsReadOnly -and $count.IsValueType -and $null -eq $count.ChangedHandler) 'Incorrect read-only value property.'
$content = @($properties | Where-Object Name -EQ 'ContentValue')[0]
Assert ($content.ChangedHandler -eq 'ContentValueUpdated' -and $content.DefaultValueMarkup -match '<Button' -and
    $content.DefaultValueMarkup -match 'xmlns=') 'Missing complex-default fragment or namespace.'
$inputs.KotlinSymbols = @{ SchemaVersion = $propertyIndex.SchemaVersion; DeclarationFingerprint = ('c' * 64); Declarations = $propertyIndex
    Pages = @(@{ ClassName = 'probe.MainPage'; Handlers = @(@{ Name = 'onClick'; ReturnTypeName = 'System.Void'
        ParameterTypeNames = @('System.Object', "$xamlNamespace.RoutedEventArgs") }) }) }
$null = Compile 'properties-symbols'
[IO.File]::WriteAllText($pagePath, $propertyXaml.Replace('DefaultValue="2"', 'DefaultValue="3"'))
$changedDefault = Compile 'properties-stale-default' 1
Assert (($changedDefault.MSBuildLogEntries | Where-Object Type -EQ 2).Message -match 'changed after semantic') 'Changed default accepted stale symbols.'
$inputs.Remove('KotlinSymbols')
$inputs.XamlPages = @()
$empty = (Compile 'deleted').KotlinDeclarations
Assert ($empty.Pages.Count -eq 0 -and $empty.Resources.Count -eq 0) 'Deleted input retained stale declarations.'
$inputs.IsPass1 = $false
$inputs.KotlinSymbols = @{ SchemaVersion = $empty.SchemaVersion; DeclarationFingerprint = ('b' * 64); Declarations = $empty; Pages = @() }
$emptyFinal = Compile 'deleted-final'
Assert ($emptyFinal.KotlinImplementation.Declarations.Pages.Count -eq 0) 'Empty final compilation retained stale pages.'
Assert ($emptyFinal.GeneratedXbfFiles.Count -eq 0) 'Empty final compilation retained stale XBF outputs.'
$inputs.IsPass1 = $true
$inputs.Remove('KotlinSymbols')

$dictionaryPath = Join-Path $root 'Resources.xaml'
[IO.File]::WriteAllText($dictionaryPath, '<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"><SolidColorBrush x:Key="Accent" Color="#336699" /></ResourceDictionary>')
$inputs.XamlPages = @(@{ ItemSpec = $dictionaryPath; FullPath = $dictionaryPath })
$resource = (Compile 'resource').KotlinDeclarations
Assert ($resource.Pages.Count -eq 0 -and $resource.Resources[0] -eq 'Resources.xaml') 'Classless dictionary became a page.'
if ($GenXbfDirectory) {
    $inputs.IsPass1 = $false
    $inputs.DisableXbfGeneration = $false
    $inputs.GenXbfPath = (Resolve-Path -LiteralPath $GenXbfDirectory).Path
    $inputs.KotlinSymbols = @{ SchemaVersion = $resource.SchemaVersion; DeclarationFingerprint = ('d' * 64); Declarations = $resource; Pages = @() }
    $compiledResource = Compile 'resource-xbf'
    Assert ($compiledResource.GeneratedXbfFiles.Count -eq 1) 'Expected one compiled resource dictionary.'
    $xbf = [IO.File]::ReadAllBytes($compiledResource.GeneratedXbfFiles[0])
    Assert ($xbf.Length -gt 32 -and $xbf[0] -eq 0x58 -and $xbf[1] -eq 0x42 -and $xbf[2] -eq 0x46) 'GenXbf did not emit an XBF binary.'
    $inputs.Remove('KotlinSymbols')
    $inputs.Remove('GenXbfPath')
    $inputs.IsPass1 = $true
    $inputs.DisableXbfGeneration = $true
}

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
