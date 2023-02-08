
# HugsLib and Harmony dlls are already in their respective mods, shared depenedencies
# no vs copy setting? Just delete after he fact I guess

$delete = @("Harmony", "0Harmony", "HugsLib")

foreach ($item in $delete) { 
    $path = "../assemblies/${item}.dll"
	Write-Output "deleting $path"
    if (Test-Path $path){
        Remove-Item $path
    }
}

$items = Get-ChildItem -Path ../assemblies
$order = @("Harmony", "Newtonsoft", "websocket", "Archipelago.MultiClient", "RimworldArchipelago")
Write-Output "Assemblies contains the following: $items" 
foreach ($item in $items) { 
    for ( $index = 0; $index -lt $order.count; $index++) {
        if ($item.Name.StartsWith($order[$index])) {
            $item | Rename-Item -NewName { "$index"+"_"+$_.Name };
        }
    }
}
Write-Output "Assembly renaming complete" 
$items = Get-ChildItem -Path ../assemblies
Write-Output "Assemblies contains the following: $items" 

