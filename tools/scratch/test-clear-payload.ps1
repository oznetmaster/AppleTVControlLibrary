$dll = "$env:USERPROFILE\.nuget\packages\plist-cil\2.2.0\lib\netstandard2.0\plist-cil.dll"
Add-Type -Path $dll

function New-ClearPayload([byte[]]$sessionUuid) {
	 $objects = New-Object Claunia.PropertyList.NSArray 0
	 $objects.Add([Claunia.PropertyList.NSObject]::Wrap("`$null"))

	 $d1 = New-Object Claunia.PropertyList.NSDictionary
	 $d1.Add("`$class", (New-Object Claunia.PropertyList.UID -ArgumentList ([byte]7)))
	 $d1.Add("targetSessionUUID", (New-Object Claunia.PropertyList.UID -ArgumentList ([byte]5)))
	 $d1.Add("keyboardOutput", (New-Object Claunia.PropertyList.UID -ArgumentList ([byte]2)))
	 $d1.Add("textToAssert", (New-Object Claunia.PropertyList.UID -ArgumentList ([byte]4)))
	 $objects.Add($d1)

	 $d2 = New-Object Claunia.PropertyList.NSDictionary
	 $d2.Add("`$class", (New-Object Claunia.PropertyList.UID -ArgumentList ([byte]3)))
	 $objects.Add($d2)

	 $d3 = New-Object Claunia.PropertyList.NSDictionary
	 $classes3 = New-Object Claunia.PropertyList.NSArray 0
	 $classes3.Add([Claunia.PropertyList.NSObject]::Wrap("TIKeyboardOutput"))
	 $classes3.Add([Claunia.PropertyList.NSObject]::Wrap("NSObject"))
	 $d3.Add("`$classname", "TIKeyboardOutput")
	 $d3.Add("`$classes", $classes3)
	 $objects.Add($d3)

	 $objects.Add([Claunia.PropertyList.NSObject]::Wrap(""))

	 $d5 = New-Object Claunia.PropertyList.NSDictionary
	 $d5.Add("NS.uuidbytes", (New-Object Claunia.PropertyList.NSData (,$sessionUuid)))
	 $d5.Add("`$class", (New-Object Claunia.PropertyList.UID -ArgumentList ([byte]6)))
	 $objects.Add($d5)

	 $d6 = New-Object Claunia.PropertyList.NSDictionary
	 $classes6 = New-Object Claunia.PropertyList.NSArray 0
	 $classes6.Add([Claunia.PropertyList.NSObject]::Wrap("NSUUID"))
	 $classes6.Add([Claunia.PropertyList.NSObject]::Wrap("NSObject"))
	 $d6.Add("`$classname", "NSUUID")
	 $d6.Add("`$classes", $classes6)
	 $objects.Add($d6)

	 $d7 = New-Object Claunia.PropertyList.NSDictionary
	 $classes7 = New-Object Claunia.PropertyList.NSArray 0
	 $classes7.Add([Claunia.PropertyList.NSObject]::Wrap("RTITextOperations"))
	 $classes7.Add([Claunia.PropertyList.NSObject]::Wrap("NSObject"))
	 $d7.Add("`$classname", "RTITextOperations")
	 $d7.Add("`$classes", $classes7)
	 $objects.Add($d7)

	 $top = New-Object Claunia.PropertyList.NSDictionary
	 $top.Add("textOperations", (New-Object Claunia.PropertyList.UID -ArgumentList ([byte]1)))

	 $root = New-Object Claunia.PropertyList.NSDictionary
	 $root.Add("`$version", [int64]100000)
	 $root.Add("`$archiver", "RTIKeyedArchiver")
	 $root.Add("`$top", $top)
	 $root.Add("`$objects", $objects)

	 return [Claunia.PropertyList.BinaryPropertyListWriter]::WriteToArray($root)
}

$sessionUuid = [byte[]](0..15)
$clearBytes = New-ClearPayload $sessionUuid
$hex = ($clearBytes | ForEach-Object { $_.ToString("x2") }) -join ""
Write-Output "CLEAR: $hex"
