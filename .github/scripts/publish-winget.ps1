param(
    [string]$Tag = "v0.4.1",
    [string]$Token
)

$ErrorActionPreference = "Stop"

$version = $Tag.TrimStart('v')
$manifestDir = "manifests/i/ismlEraslan/EmailSummarizer/$version"
New-Item -ItemType Directory -Path $manifestDir -Force | Out-Null

$downloadUrl = "https://github.com/ismlEraslan/email-summarizer-win32/releases/download/$Tag/EmailSummarizer.zip"
Write-Host "Downloading release asset from: $downloadUrl"
$zipFile = "$env:TEMP\EmailSummarizer.zip"
Invoke-WebRequest -Uri $downloadUrl -OutFile $zipFile
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
  - RelativeFilePath: EmailSummarizer.exe
    PortableCommandAlias: EmailSummarizer
Commands:
  - EmailSummarizer
ReleaseDate: $(Get-Date -Format "yyyy-MM-dd")
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
PublisherSupportUrl: https://github.com/ismlEraslan/email-summarizer-win32/issues
PackageName: Email Summarizer
PackageUrl: https://github.com/ismlEraslan/email-summarizer-win32
License: MIT
LicenseUrl: https://github.com/ismlEraslan/email-summarizer-win32/blob/main/LICENSE
Copyright: Copyright (c) 2026 ismlEraslan
ShortDescription: Local AI-powered IMAP email summarizer with llama.cpp, priority triage, and system tray daemon.
Description: Email Summarizer is a native Win32/Windows desktop application for managing and summarizing emails from multiple IMAP accounts using local AI (llama.cpp) or cloud providers with DPAPI encryption.
Moniker: emailsummarizer
Tags:
  - email
  - imap
  - ai
  - summarizer
  - llama-cpp
  - win32
ReleaseNotesUrl: https://github.com/ismlEraslan/email-summarizer-win32/releases/tag/$Tag
ManifestType: defaultLocale
ManifestVersion: 1.6.0
"@

Set-Content -Path "$manifestDir/ismlEraslan.EmailSummarizer.yaml" -Value $versionYaml -Encoding utf8
Set-Content -Path "$manifestDir/ismlEraslan.EmailSummarizer.installer.yaml" -Value $installerYaml -Encoding utf8
Set-Content -Path "$manifestDir/ismlEraslan.EmailSummarizer.locale.en-US.yaml" -Value $localeYaml -Encoding utf8

Write-Host "Validating manifests with winget..."
winget validate --manifest $manifestDir

if ($Token) {
    Write-Host "Submitting manifests to microsoft/winget-pkgs..."
    wingetcreate submit $manifestDir --token $Token
} else {
    Write-Host "No GitHub Token provided; skipped submission."
}
