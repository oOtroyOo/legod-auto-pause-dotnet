# 是否为 CI 环境（GitHub Actions 等）。
$IsCI = $env:GITHUB_ACTIONS -eq 'true'

Set-Location $PSScriptRoot

dotnet clean
if (-not $IsCI) {
    Start-Process sc -ArgumentList 'stop LeigodPauseService' -Verb RunAs -ErrorAction Continue

    # 结束可能占用发布目录文件的进程。
    foreach ($name in @('LeigodPause', 'LeigodPause.Service')) {
        $procs = Get-Process -Name $name -ErrorAction Continue
        if ($procs) {
            Write-Host "结束进程: $name"
            $procs | Stop-Process -Force -ErrorAction Continue
        }
    }
}

# 清理发布目录，但保留 ".WebView2"
if (Test-Path -Path ./publish) {
    foreach ($child in (Get-ChildItem -Path ./publish)) {
        if ($child.Name -notlike '*.WebView2') {
            Write-Host 删除 $child.FullName
            $child | Remove-Item  -Recurse -Force -ErrorAction SilentlyContinue
        }
    }
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
