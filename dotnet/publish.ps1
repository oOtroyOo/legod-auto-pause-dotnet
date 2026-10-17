# 是否为 CI 环境（GitHub Actions 等）。
$IsCI = $env:GITHUB_ACTIONS -eq 'true'

Remove-Item -Recurse ./publish -ErrorAction SilentlyContinue
dotnet clean
if (-not $IsCI) {
    Start-Process sc -ArgumentList 'stop LeigodPauseService' -Verb RunAs -ErrorAction Continue
}

dotnet publish LeigodPause/LeigodPause.csproj -c Release -f net481 -p:PublishLayout=true -p:DisableServiceStop=true -o ./publish
if ($LASTEXITCODE -ne 0) {
    Write-Error "dotnet publish 失败 (ExitCode: $LASTEXITCODE)"
    if (-not $IsCI) {
        pause
    }
    exit $LASTEXITCODE
}
dotnet restore

if (-not $IsCI) {
    Start-Process sc -ArgumentList 'start LeigodPauseService' -Verb RunAs -ErrorAction Continue
}
