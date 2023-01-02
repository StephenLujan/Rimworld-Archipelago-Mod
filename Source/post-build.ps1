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