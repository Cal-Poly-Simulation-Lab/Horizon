# clean-compiled-subsystems.ps1
$CompiledFilesList = "user-compiled-subsystems.txt"

if (Test-Path $CompiledFilesList) {
    Write-Host "Reading compiled files from: $CompiledFilesList"
    Write-Host "Cleanup started at: $(Get-Date)"
    
    $files = Get-Content $CompiledFilesList
    $fileCount = $files.Count
    Write-Host "Found $fileCount files to clean"
    
    foreach ($file in $files) {
        if (Test-Path $file) {
            Write-Host "Removing: $file"
            Remove-Item $file -Force
        } else {
            Write-Host "File not found (may already be cleaned): $file"
        }
    }
    
    # Clear the list after cleaning
    Clear-Content $CompiledFilesList
    Write-Host "Cleanup completed at: $(Get-Date)"
    Write-Host "Cleanup complete!"
} else {
    Write-Host "No compiled files list found. Nothing to clean."
}