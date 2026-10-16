Remove-Item -Recurse ./publish -ErrorAction SilentlyContinue
dotnet clean
dotnet publish LeigodPause/LeigodPause.csproj -c Release -f net481 -p:PublishLayout=true -p:DisableServiceStop=true -o ./publish
if ($LASTEXITCODE -ne 0) {
    Write-Error "dotnet publish 失败 (ExitCode: $LASTEXITCODE)"
    pause
    exit $LASTEXITCODE
}
dotnet restore