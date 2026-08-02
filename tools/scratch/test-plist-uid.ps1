$dll = "$env:USERPROFILE\.nuget\packages\plist-cil\2.2.0\lib\netstandard2.0\plist-cil.dll"
Add-Type -Path $dll

$objects = New-Object Claunia.PropertyList.NSArray 0
$objects.Add([Claunia.PropertyList.NSObject]::Wrap("`$null"))
$dict1 = New-Object Claunia.PropertyList.NSDictionary
$dict1.Add("targetSessionUUID", (New-Object Claunia.PropertyList.UID -ArgumentList ([byte]2)))
$objects.Add($dict1)
$objects.Add([Claunia.PropertyList.NSObject]::Wrap("hello"))

$top = New-Object Claunia.PropertyList.NSDictionary
$top.Add("textOperations", (New-Object Claunia.PropertyList.UID -ArgumentList ([byte]1)))

$root = New-Object Claunia.PropertyList.NSDictionary
$root.Add("`$version", [int64]100000)
$root.Add("`$archiver", "RTIKeyedArchiver")
$root.Add("`$top", $top)
$root.Add("`$objects", $objects)

$bytes = [Claunia.PropertyList.BinaryPropertyListWriter]::WriteToArray($root)
Write-Output "byte length: $($bytes.Length)"

$parsed = [Claunia.PropertyList.PropertyListParser]::Parse($bytes)
$parsedDict = [Claunia.PropertyList.NSDictionary]$parsed
$parsedTop = [Claunia.PropertyList.NSDictionary]$parsedDict.ObjectForKey("`$top")
$uidObj = $parsedTop.ObjectForKey("textOperations")
Write-Output "top.textOperations type: $($uidObj.GetType().FullName)"
if ($uidObj -is [Claunia.PropertyList.UID]) {
	 Write-Output "UID value: $($uidObj.ToUInt64())"
}

$parsedObjects = [Claunia.PropertyList.NSArray]$parsedDict.ObjectForKey("`$objects")
$obj1 = [Claunia.PropertyList.NSDictionary]$parsedObjects.ObjectAtIndex(1)
$nestedUid = $obj1.ObjectForKey("targetSessionUUID")
Write-Output "nested UID type: $($nestedUid.GetType().FullName), value: $($nestedUid.ToUInt64())"
