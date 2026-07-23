<#
.SYNOPSIS
    R-SEC2: generates a local dev CA + Host server cert + one Agent client
    cert for mTLS between Host and Agent. Dev/test use only — output goes to
    a gitignored certs/ directory and is never committed. Production cert
    issuance (real CA, per-Agent enrollment, rotation) is a documented
    follow-up, not covered by this script.
#>
param(
    [string]$OutputDir = (Join-Path $PSScriptRoot ".." "certs"),
    [string]$CaPassword = "telechron-dev-ca",
    [string]$HostPassword = "telechron-dev-host",
    [string]$AgentPassword = "telechron-dev-agent",
    [string]$AgentName = "agent-dev"
)

$ErrorActionPreference = "Stop"
New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null
$OutputDir = (Resolve-Path $OutputDir).Path

function New-DevCa {
    param([string]$Path, [string]$Password)

    $ca = New-SelfSignedCertificate `
        -Subject "CN=Telechron Dev CA" `
        -KeyUsage CertSign, CRLSign, DigitalSignature `
        -CertStoreLocation "Cert:\CurrentUser\My" `
        -NotAfter (Get-Date).AddYears(2) `
        -KeyExportPolicy Exportable `
        -KeyAlgorithm RSA -KeyLength 4096 `
        -TextExtension @("2.5.29.19={critical}{text}ca=true")

    $securePassword = ConvertTo-SecureString -String $Password -Force -AsPlainText
    Export-PfxCertificate -Cert $ca -FilePath (Join-Path $Path "ca.pfx") -Password $securePassword | Out-Null
    Export-Certificate -Cert $ca -FilePath (Join-Path $Path "ca.crt") | Out-Null
    Remove-Item "Cert:\CurrentUser\My\$($ca.Thumbprint)" -Force
    return $ca.Thumbprint
}

function New-SignedCert {
    param([string]$Subject, [string]$CaPath, [string]$CaPassword, [string]$OutFile, [string]$OutPassword, [string[]]$EnhancedKeyUsage, [string[]]$DnsNames = @())

    $caSecurePassword = ConvertTo-SecureString -String $CaPassword -Force -AsPlainText
    $caCert = Import-PfxCertificate -FilePath (Join-Path $CaPath "ca.pfx") -CertStoreLocation "Cert:\CurrentUser\My" -Password $caSecurePassword

    # DnsName populates the Subject Alternative Name extension — required by
    # .NET's SslStream (and modern TLS stacks generally) for hostname
    # verification; a CN-only cert silently fails the HTTP/2 handshake.
    $certParams = @{
        Subject           = $Subject
        Signer            = $caCert
        CertStoreLocation = "Cert:\CurrentUser\My"
        NotAfter          = (Get-Date).AddYears(1)
        KeyExportPolicy   = "Exportable"
        KeyAlgorithm      = "RSA"
        KeyLength         = 2048
        TextExtension     = @("2.5.29.37={text}$($EnhancedKeyUsage -join ',')")
    }
    if ($DnsNames.Count -gt 0) {
        $certParams["DnsName"] = $DnsNames
    }

    $leaf = New-SelfSignedCertificate @certParams

    $outSecurePassword = ConvertTo-SecureString -String $OutPassword -Force -AsPlainText
    Export-PfxCertificate -Cert $leaf -FilePath $OutFile -Password $outSecurePassword | Out-Null

    Remove-Item "Cert:\CurrentUser\My\$($leaf.Thumbprint)" -Force
    Remove-Item "Cert:\CurrentUser\My\$($caCert.Thumbprint)" -Force
    return $leaf.Thumbprint
}

Write-Host "Generating Telechron dev CA + Host/Agent certs in $OutputDir ..." -ForegroundColor Cyan

$caThumbprint = New-DevCa -Path $OutputDir -Password $CaPassword
Write-Host "  CA thumbprint: $caThumbprint"

# ServerAuth EKU (1.3.6.1.5.5.7.3.1) — the Host presents this to Agents.
# DnsName covers both "localhost" (same-machine dev) and the actual machine
# name (Agent connecting from elsewhere on the LAN using the real hostname).
$hostThumbprint = New-SignedCert -Subject "CN=telechron-host" -CaPath $OutputDir -CaPassword $CaPassword `
    -OutFile (Join-Path $OutputDir "host-server.pfx") -OutPassword $HostPassword `
    -EnhancedKeyUsage @("1.3.6.1.5.5.7.3.1") -DnsNames @("localhost", $env:COMPUTERNAME)
Write-Host "  Host server cert thumbprint: $hostThumbprint"

# ClientAuth EKU (1.3.6.1.5.5.7.3.2) — each Agent presents this to the Host.
$agentThumbprint = New-SignedCert -Subject "CN=$AgentName" -CaPath $OutputDir -CaPassword $CaPassword `
    -OutFile (Join-Path $OutputDir "$AgentName.pfx") -OutPassword $AgentPassword `
    -EnhancedKeyUsage @("1.3.6.1.5.5.7.3.2")
Write-Host "  Agent '$AgentName' cert thumbprint: $agentThumbprint"

Write-Host "Done. Set these env vars (or Telechron: config keys) before running Host/Agent:" -ForegroundColor Green
Write-Host "  TELECHRON_MTLS_CA_PATH=$OutputDir\ca.crt"
Write-Host "  TELECHRON_MTLS_HOST_CERT_PATH=$OutputDir\host-server.pfx / TELECHRON_MTLS_HOST_CERT_PASSWORD=$HostPassword"
Write-Host "  TELECHRON_MTLS_AGENT_CERT_PATH=$OutputDir\$AgentName.pfx / TELECHRON_MTLS_AGENT_CERT_PASSWORD=$AgentPassword"
