# Run this once to permanently add dotnet to PATH
[Environment]::SetEnvironmentVariable(
    "PATH",
    [Environment]::GetEnvironmentVariable("PATH", "User") + ";C:\Program Files\dotnet",
    "User"
)
Write-Host "dotnet PATH added permanently. Close and reopen PowerShell, then run: dotnet --version"
