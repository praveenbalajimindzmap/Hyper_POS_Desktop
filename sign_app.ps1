# Create Code Signing Certificate if not exists
$cert = Get-ChildItem -Path Cert:\CurrentUser\My -CodeSigningCert | Where-Object { $_.Subject -like '*Saraa Boutique*' } | Select-Object -First 1

if ($null -eq $cert) {
    $cert = New-SelfSignedCertificate -Type CodeSigningCert -Subject "CN=Saraa Boutique POS, O=Saraa Boutique" -CertStoreLocation "Cert:\CurrentUser\My" -NotAfter (Get-Date).AddYears(5)
    Write-Host "Created new Code Signing Certificate: $($cert.Thumbprint)"
} else {
    Write-Host "Using existing Code Signing Certificate: $($cert.Thumbprint)"
}

# Sign all release executables
Set-AuthenticodeSignature -FilePath ".\Saraa_Boutique_SingleExe\HyperPOS.Desktop.exe" -Certificate $cert -TimestampServer "http://timestamp.digicert.com"
Set-AuthenticodeSignature -FilePath ".\Saraa_Boutique_POS_Portable\Saraa_Boutique_POS.exe" -Certificate $cert -TimestampServer "http://timestamp.digicert.com"
Set-AuthenticodeSignature -FilePath ".\HyperPOS.exe" -Certificate $cert -TimestampServer "http://timestamp.digicert.com"

# Export public certificate so it can be trusted on any Windows PC
Export-Certificate -Cert $cert -FilePath ".\Saraa_Boutique_POS_Portable\Saraa_Boutique_Certificate.cer" -Force
Copy-Item ".\Saraa_Boutique_POS_Portable\Saraa_Boutique_Certificate.cer" -Destination ".\Saraa_Boutique_Certificate.cer" -Force

# Verify signature
Get-AuthenticodeSignature ".\Saraa_Boutique_POS_Portable\Saraa_Boutique_POS.exe" | Format-List
