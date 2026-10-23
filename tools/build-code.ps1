param(
    [Parameter(Mandatory=$true)][string]$Sts2Dir,
    [Parameter(Mandatory=$true)][string]$BaseLibDir,
    [string]$RuntimePackPath = ''
)
$ErrorActionPreference='Stop'
$projectRoot=Split-Path -Parent $PSScriptRoot
foreach($dependency in @((Join-Path $Sts2Dir 'data_sts2_windows_x86_64/sts2.dll'),(Join-Path $BaseLibDir 'BaseLib.dll'))) {
    if(!(Test-Path -LiteralPath $dependency)){throw "Missing dependency: $dependency"}
}
& dotnet build (Join-Path $projectRoot 'SatoriCharacter.csproj') -c Release "-p:Sts2Dir=$Sts2Dir" "-p:BaseLibDir=$BaseLibDir"
if($LASTEXITCODE -ne 0){throw 'Source build failed.'}
$outputDir=Join-Path $projectRoot 'dist'
New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $projectRoot 'bin/Release/net9.0/SatoriCharacter.dll') -Destination $outputDir
Copy-Item -LiteralPath (Join-Path $projectRoot 'SatoriCharacter.json') -Destination $outputDir
if($RuntimePackPath){Copy-Item -LiteralPath $RuntimePackPath -Destination (Join-Path $outputDir 'SatoriCharacter.pck')}
Write-Output "Built code: $outputDir"
