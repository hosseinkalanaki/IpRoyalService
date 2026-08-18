[CmdletBinding()]
param(
  [Parameter(Mandatory=$true)][string]$Version,
  [Parameter(Mandatory=$true)][string]$SingBoxZip,
  [string]$Configuration = 'Release',
  [string]$ArtifactsDirectory,
  [string]$InnoCompiler = "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe"
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$engineArchive = (Resolve-Path -LiteralPath $SingBoxZip).Path
$ArtifactsDirectory = if ([string]::IsNullOrWhiteSpace($ArtifactsDirectory)) { Join-Path $root 'artifacts' } else { $ArtifactsDirectory }
$artifacts = [IO.Path]::GetFullPath($ArtifactsDirectory)
if (-not $artifacts.StartsWith($root, [StringComparison]::OrdinalIgnoreCase)) { throw 'ArtifactsDirectory must be inside the repository.' }
if (-not (Test-Path -LiteralPath $InnoCompiler)) {
  $perUserCompiler = Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe'
  if (Test-Path -LiteralPath $perUserCompiler) { $InnoCompiler = $perUserCompiler }
  else { throw "Inno Setup 6 compiler not found: $InnoCompiler" }
}
if ($Version -notmatch '^\d+\.\d+\.\d+([.-][0-9A-Za-z.-]+)?$') { throw 'Version must be a semantic version such as 1.2.3.' }

$servicePublish = Join-Path $artifacts 'publish-service-win-x64'
$controlPublish = Join-Path $artifacts 'publish-control-win-x64'
$stage = Join-Path $artifacts 'installer-stage-win-x64'
$output = Join-Path $artifacts 'installer'
foreach ($path in @($servicePublish, $controlPublish, $stage, $output)) { if (Test-Path -LiteralPath $path) { Remove-Item -LiteralPath $path -Recurse -Force } }

dotnet publish (Join-Path $root 'src\IpRoyalService\IpRoyalService.csproj') -c $Configuration -r win-x64 --self-contained true -p:PublishSingleFile=true -p:DebugType=None -p:DebugSymbols=false -o $servicePublish
if ($LASTEXITCODE -ne 0) { throw 'service publish failed.' }
dotnet publish (Join-Path $root 'src\IpRoyalControl\IpRoyalControl.csproj') -c $Configuration -r win-x64 --self-contained true -p:PublishSingleFile=true -p:DebugType=None -p:DebugSymbols=false -o $controlPublish
if ($LASTEXITCODE -ne 0) { throw 'control application publish failed.' }

New-Item -ItemType Directory -Path (Join-Path $stage 'engine') -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $servicePublish 'IpRoyalService.exe') -Destination $stage
Copy-Item -LiteralPath (Join-Path $controlPublish 'IpRoyalControl.exe') -Destination $stage
Copy-Item -LiteralPath (Join-Path $root 'README.md') -Destination (Join-Path $stage 'USER-GUIDE.md')
Copy-Item -LiteralPath (Join-Path $root 'deploy\Manage-Service.cmd') -Destination $stage

$temporary = Join-Path ([IO.Path]::GetTempPath()) ("iproyal-installer-" + [guid]::NewGuid())
New-Item -ItemType Directory -Path $temporary | Out-Null
try {
  Expand-Archive -LiteralPath $engineArchive -DestinationPath $temporary -Force
  $engine = Get-ChildItem -LiteralPath $temporary -Filter sing-box.exe -Recurse | Select-Object -First 1
  if (-not $engine) { throw 'The supplied sing-box archive does not contain sing-box.exe.' }
  Copy-Item -LiteralPath $engine.FullName -Destination (Join-Path $stage 'engine\sing-box.exe')
  $license = Get-ChildItem -LiteralPath $temporary -File -Recurse | Where-Object Name -Match '^LICENSE(\..+)?$' | Select-Object -First 1
  if ($license) { Copy-Item -LiteralPath $license.FullName -Destination (Join-Path $stage 'SING-BOX-LICENSE.txt') }
}
finally { Remove-Item -LiteralPath $temporary -Recurse -Force -ErrorAction SilentlyContinue }

$expected = @('IpRoyalService.exe','IpRoyalControl.exe','USER-GUIDE.md','Manage-Service.cmd','engine\sing-box.exe')
foreach ($relative in $expected) { if (-not (Test-Path -LiteralPath (Join-Path $stage $relative))) { throw "Installer staging failed: $relative is missing." } }
$unexpected = Get-ChildItem -LiteralPath $stage -Recurse -File | Where-Object { $_.Extension -in @('.cs','.csproj','.pdb','.json') }
if ($unexpected) { throw 'Installer staging contains source, symbols, or a prebuilt configuration file.' }

New-Item -ItemType Directory -Path $output -Force | Out-Null
& $InnoCompiler "/DMyAppVersion=$Version" "/DStageDir=$stage" "/DOutputDir=$output" (Join-Path $root 'installer\IpRoyalService.iss')
if ($LASTEXITCODE -ne 0) { throw 'Inno Setup compilation failed.' }
$installer = Join-Path $output "IpRoyalService-v$Version-win-x64-Setup.exe"
if (-not (Test-Path -LiteralPath $installer)) { throw "Expected installer was not created: $installer" }
$hash = (Get-FileHash -Algorithm SHA256 -LiteralPath $installer).Hash.ToLowerInvariant()
Set-Content -LiteralPath "$installer.sha256" -Value "$hash  $([IO.Path]::GetFileName($installer))" -Encoding ascii
Write-Host "Created $installer"
