[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$PublicCertificatePath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$resolvedPublicCertificatePath = [System.IO.Path]::GetFullPath($PublicCertificatePath)
$publicCertificateDirectory = Split-Path -Parent $resolvedPublicCertificatePath

if (-not (Test-Path -LiteralPath $publicCertificateDirectory -PathType Container)) {
    New-Item -ItemType Directory -Path $publicCertificateDirectory | Out-Null
}

$certificate = New-SelfSignedCertificate `
    -Type CodeSigningCert `
    -Subject 'CN=Atomic Art' `
    -FriendlyName 'Atomic Art release signing' `
    -CertStoreLocation 'Cert:\CurrentUser\My' `
    -KeyAlgorithm RSA `
    -KeyLength 3072 `
    -HashAlgorithm SHA256 `
    -KeyExportPolicy Exportable `
    -NotAfter (Get-Date).AddYears(5)

Export-Certificate `
    -Cert $certificate `
    -FilePath $resolvedPublicCertificatePath `
    -Type CERT `
    -Force | Out-Null

Write-Output "Certificate thumbprint: $($certificate.Thumbprint)"
Write-Output "Public certificate: $resolvedPublicCertificatePath"
Write-Warning 'Back up the private key outside the repository. Losing it makes future signatures unverifiable against this publisher.'
