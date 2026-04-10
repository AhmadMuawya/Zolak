Write-Host "Creating Single-File Executable for Zolak Desktop Pet..."
Write-Host "This will take a minute..."

# We use explicitly win-x64, self-contained so the target user doesn't even need .NET installed.
# We also include native libraries embedded so that there's legitimately only 1 .exe file.
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o ./Binaries 

Write-Host ""
Write-Host "Done! You can find the single executable file here:"
Write-Host "c:\Users\User\Documents\VS\VS C#\Zolak\Binaries\WaysToSnooze.Zolak.exe"
Write-Host "Send 'WaysToSnooze.Zolak.exe' to your friend. No installer needed!"
