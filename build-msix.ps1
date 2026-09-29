# Builds a signed, self-contained MSIX of YT Downloader into artifacts/msix.
# Signs with a self-signed code-signing certificate kept in your personal certificate store
# (CurrentUser\My), creating it on the first run. Only its public half is exported, as a .cer
# next to the package; install that into Trusted People once before installing the MSIX.
#
#   pwsh ./build-msix.ps1                  # x64
#   pwsh ./build-msix.ps1 -Platform ARM64
param(
    [ValidateSet('x64', 'x86', 'ARM64')] [string]$Platform = 'x64'
)
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$project = Join-Path $root 'src\YtDownloader.App\YtDownloader.App.csproj'
$manifest = Join-Path $root 'src\YtDownloader.App\Package.appxmanifest'
$out = Join-Path $root 'artifacts\msix'

# The certificate subject must match the manifest's Publisher exactly.
$publisher = ([xml](Get-Content $manifest)).Package.Identity.Publisher
$cert = Get-ChildItem Cert:\CurrentUser\My |
    Where-Object { $_.Subject -eq $publisher -and $_.NotAfter -gt (Get-Date) -and $_.HasPrivateKey } |
    Sort-Object NotAfter -Descending | Select-Object -First 1
if (-not $cert) {
    Write-Host "Creating a self-signed code-signing certificate for $publisher"
    $cert = New-SelfSignedCertificate -Type Custom -Subject $publisher `
        -FriendlyName 'YT Downloader package signing' `
        -KeyUsage DigitalSignature -KeyAlgorithm RSA -KeyLength 3072 `
        -CertStoreLocation Cert:\CurrentUser\My -NotAfter (Get-Date).AddYears(5) `
        -TextExtension @('2.5.29.37={text}1.3.6.1.5.5.7.3.3', '2.5.29.19={text}')
}
Write-Host "Signing with $($cert.Subject), thumbprint $($cert.Thumbprint), valid until $($cert.NotAfter.ToString('yyyy-MM-dd'))"

if (Test-Path $out) { Remove-Item -Recurse -Force $out }
New-Item -ItemType Directory -Force $out | Out-Null

# Self-contained .NET and Windows App SDK, so the package installs with no other runtimes.
dotnet publish $project -c Release -p:Platform=$Platform -r "win-$($Platform.ToLowerInvariant())" `
    -p:WindowsPackageType=MSIX `
    -p:WindowsAppSDKSelfContained=true `
    -p:GenerateAppxPackageOnBuild=true `
    -p:AppxPackageSigningEnabled=true `
    -p:PackageCertificateThumbprint=$($cert.Thumbprint) `
    -p:AppxBundle=Never `
    -p:UapAppxPackageBuildMode=SideloadOnly `
    -p:AppxPackageTestDir="$out\" `
    -p:AppxPackageDir="$out\"
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed ($LASTEXITCODE)" }

# The build drops its own copy of the certificate; keep one, with a plain name.
Get-ChildItem $out -Recurse -Filter *.cer | Remove-Item
Export-Certificate -Cert $cert -FilePath (Join-Path $out 'YtDownloader.cer') | Out-Null
Get-ChildItem $out -Recurse -Include *.msix, *.cer | ForEach-Object {
    '{0}  {1:N1} MB' -f $_.FullName.Substring($root.Length + 1), ($_.Length / 1MB)
}
