$asm = [System.Reflection.Assembly]::LoadFrom("C:\Users\njc\.nuget\packages\bouncycastle.cryptography\2.6.2\lib\net6.0\BouncyCastle.Cryptography.dll")
$t = $asm.GetType("Org.BouncyCastle.Math.BigInteger")
$t.GetMethods() | Where-Object { $_.DeclaringType -eq $t -and $_.Name -match "Mod|Xor|ToByteArray|BitLength|And" } | ForEach-Object {
	 $ps = ($_.GetParameters() | ForEach-Object { $_.ParameterType.Name }) -join ', '
	 Write-Output "$($_.ReturnType.Name) $($_.Name)($ps)"
}
