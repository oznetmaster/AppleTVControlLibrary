$dll = "$env:USERPROFILE\.nuget\packages\plist-cil\2.2.0\lib\netstandard2.0\plist-cil.dll"
$asm = [System.Reflection.Assembly]::LoadFrom($dll)

$t1 = $asm.GetType("Claunia.PropertyList.PropertyListParser")
$t1.GetMethods() | Where-Object { $_.IsStatic } | ForEach-Object { $_.ToString() }
Write-Output "-----"
$t2 = $asm.GetType("Claunia.PropertyList.NSDictionary")
$t2.GetMethods() | Where-Object { $_.DeclaringType -eq $t2 } | ForEach-Object { $_.ToString() }
Write-Output "-----"
$t3 = $asm.GetType("Claunia.PropertyList.NSArray")
$t3.GetMethods() | Where-Object { $_.DeclaringType -eq $t3 } | ForEach-Object { $_.ToString() }
Write-Output "-----"
$t4 = $asm.GetType("Claunia.PropertyList.BinaryPropertyListWriter")
$t4.GetMethods() | Where-Object { $_.IsStatic } | ForEach-Object { $_.ToString() }
Write-Output "-----"
$t5 = $asm.GetType("Claunia.PropertyList.NSObject")
$t5.GetMethods() | Where-Object { $_.DeclaringType -eq $t5 } | ForEach-Object { $_.ToString() }
