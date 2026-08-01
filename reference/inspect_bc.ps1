$asm = [System.Reflection.Assembly]::LoadFrom("C:\Users\njc\.nuget\packages\bouncycastle.cryptography\2.6.2\lib\net6.0\BouncyCastle.Cryptography.dll")
Write-Output "Type count: $($asm.GetTypes().Count)"
$asm.GetTypes() | Where-Object { $_.Name -match "X25519|Ed25519|Srp6" } | ForEach-Object { Write-Output $_.FullName }


function Dump-Type($typeName) {
	 Write-Output "=== $typeName ==="
	 $t = $asm.GetType($typeName)
	 if ($null -eq $t) { Write-Output "NOT FOUND"; return }
	 $t.GetConstructors() | ForEach-Object {
		  $ps = ($_.GetParameters() | ForEach-Object { $_.ParameterType.Name + ' ' + $_.Name }) -join ', '
		  Write-Output "ctor($ps)"
	 }
	 $t.GetMethods() | Where-Object { $_.DeclaringType -eq $t } | ForEach-Object {
		  $ps = ($_.GetParameters() | ForEach-Object { $_.ParameterType.Name + ' ' + $_.Name }) -join ', '
		  Write-Output "$($_.ReturnType.Name) $($_.Name)($ps)"
	 }
}

Dump-Type "Org.BouncyCastle.Crypto.Parameters.X25519PrivateKeyParameters"
Dump-Type "Org.BouncyCastle.Crypto.Agreement.X25519Agreement"
Dump-Type "Org.BouncyCastle.Crypto.Parameters.Ed25519PrivateKeyParameters"
Dump-Type "Org.BouncyCastle.Crypto.Signers.Ed25519Signer"
Dump-Type "Org.BouncyCastle.Crypto.Agreement.Srp.Srp6Client"
Dump-Type "Org.BouncyCastle.Crypto.Parameters.Srp6GroupParameters"
Dump-Type "Org.BouncyCastle.Crypto.Agreement.Srp.Srp6Utilities"
