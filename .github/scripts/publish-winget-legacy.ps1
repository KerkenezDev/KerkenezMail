param(
    [string]$Tag = "v0.5.0",
    [string]$Token
)

$ErrorActionPreference = "Stop"

$version = $Tag.TrimStart('v')
$manifestDir = "manifests/i/ismlEraslan/EmailSummarizer/$version"
New-Item -ItemType Directory -Path $manifestDir -Force | Out-Null

$downloadUrl = "https://github.com/KerkenezDev/KerkenezMail/releases/download/$Tag/KerkenezMail.zip"
Write-Host "Resolving release asset from: $downloadUrl"
$zipFile = "$env:TEMP\EmailSummarizer-$version.zip"

# Use local publish zip if already built in workspace, otherwise download release asset
if (Test-Path "publish/KerkenezMail.zip") {
    Copy-Item "publish/KerkenezMail.zip" $zipFile -Force
} elseif (Test-Path "$env:GITHUB_WORKSPACE/publish/KerkenezMail.zip") {
    Copy-Item "$env:GITHUB_WORKSPACE/publish/KerkenezMail.zip" $zipFile -Force
} else {
    Write-Host "Downloading release asset from $downloadUrl..."
    Invoke-WebRequest -Uri $downloadUrl -OutFile $zipFile
}

$sha256 = (Get-FileHash -Path $zipFile -Algorithm SHA256).Hash
Write-Host "Calculated SHA256: $sha256"

$versionYaml = @"
# yaml-language-server: `$schema=https://aka.ms/winget-manifest.version.1.6.0.schema.json
PackageIdentifier: ismlEraslan.EmailSummarizer
PackageVersion: $version
DefaultLocale: en-US
ManifestType: version
ManifestVersion: 1.6.0
"@

$installerYaml = @"
# yaml-language-server: `$schema=https://aka.ms/winget-manifest.installer.1.6.0.schema.json
PackageIdentifier: ismlEraslan.EmailSummarizer
PackageVersion: $version
InstallerType: zip
NestedInstallerType: portable
NestedInstallerFiles:
  - RelativeFilePath: KerkenezMail.exe
    PortableCommandAlias: EmailSummarizer
Commands:
  - EmailSummarizer
ReleaseDate: $(Get-Date -Format "yyyy-MM-dd")
Dependencies:
  PackageDependencies:
    - PackageIdentifier: Microsoft.DotNet.DesktopRuntime.10
Installers:
  - Architecture: x64
    InstallerUrl: $downloadUrl
    InstallerSha256: $sha256
ManifestType: installer
ManifestVersion: 1.6.0
"@

$localeYaml = @"
# yaml-language-server: `$schema=https://aka.ms/winget-manifest.defaultLocale.1.6.0.schema.json
PackageIdentifier: ismlEraslan.EmailSummarizer
PackageVersion: $version
PackageLocale: en-US
Publisher: ismlEraslan
PublisherUrl: https://github.com/ismlEraslan
PublisherSupportUrl: https://github.com/KerkenezDev/KerkenezMail/issues
PackageName: Email Summarizer
PackageUrl: https://github.com/KerkenezDev/KerkenezMail
License: MIT
LicenseUrl: https://github.com/KerkenezDev/KerkenezMail/blob/main/LICENSE
Copyright: Copyright (c) 2026 ismlEraslan / KerkenezDev
ShortDescription: Email Summarizer has transitioned to Kerkenez Mail. This release migrates your local data.
Description: |-
  Email Summarizer has evolved into Kerkenez Mail! This transition update automatically migrates your local configuration, preferences, and encrypted accounts to the new Kerkenez suite standard. Please install future updates via: winget install KerkenezDev.Mail
Moniker: emailsummarizer
Tags:
  - email
  - imap
  - ai
  - summarizer
  - llama-cpp
  - win32
  - kerkenez
ReleaseNotesUrl: https://github.com/KerkenezDev/KerkenezMail/releases/tag/$Tag
ManifestType: defaultLocale
ManifestVersion: 1.6.0
"@

Set-Content -Path "$manifestDir/ismlEraslan.EmailSummarizer.yaml" -Value $versionYaml -Encoding utf8
Set-Content -Path "$manifestDir/ismlEraslan.EmailSummarizer.installer.yaml" -Value $installerYaml -Encoding utf8
Set-Content -Path "$manifestDir/ismlEraslan.EmailSummarizer.locale.en-US.yaml" -Value $localeYaml -Encoding utf8

Write-Host "Validating legacy manifests with winget..."
winget validate --manifest $manifestDir

if ($Token) {
    Write-Host "Submitting legacy manifests to microsoft/winget-pkgs under ismlEraslan.EmailSummarizer..."
    wingetcreate submit $manifestDir --token $Token
} else {
    Write-Host "No GitHub Token provided; manifests validated and ready for submission."
}
