$dll = "$env:USERPROFILE\.nuget\packages\plist-cil\2.2.0\lib\netstandard2.0\plist-cil.dll"
Add-Type -Path $dll

$d = New-Object Claunia.PropertyList.NSDictionary
$d.Add("foo", "bar")
$missing = $d.ObjectForKey("missing")
if ($null -eq $missing) { Write-Output "ObjectForKey returns null for missing key" } else { Write-Output "ObjectForKey returned: $missing" }

$arr = New-Object Claunia.PropertyList.NSArray 0
$arr.Add([Claunia.PropertyList.NSObject]::Wrap("x"))
try {
	 $arr.ObjectAtIndex(5)
} catch {
	 Write-Output "ObjectAtIndex threw: $($_.Exception.GetType().FullName)"
}

$data = New-Object Claunia.PropertyList.NSData (,[byte[]](1,2,3))
Write-Output "NSData bytes type: $($data.Bytes.GetType().FullName), value: $($data.Bytes -join ',')"

$str = [Claunia.PropertyList.NSObject]::Wrap("hello")
Write-Output "wrapped str type: $($str.GetType().FullName)"
