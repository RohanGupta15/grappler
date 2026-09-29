# Builds and launches the app unpackaged (no Developer Mode needed).
# Packaged/MSIX runs come later via the installer build.
$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot 'src\YtDownloader.App'

dotnet build $project -p:Platform=x64 -p:WindowsPackageType=None -p:WindowsAppSDKSelfContained=true
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

# Must run from the default bin folder: redirecting output with -o breaks XAML resource loading.
Start-Process (Join-Path $project 'bin\x64\Debug\net9.0-windows10.0.26100.0\win-x64\YtDownloader.App.exe')
