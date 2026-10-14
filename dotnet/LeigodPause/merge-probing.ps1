param(
    [Parameter(Mandatory = $true)] [string] $PubDir
)

$ErrorActionPreference = 'Stop'
$ns = 'urn:schemas-microsoft-com:asm.v1'
$dir = $PubDir.TrimEnd('\', '/')
$exes = Get-ChildItem -Path $dir -Filter '*.exe' -File

foreach ($exe in $exes) {
    $cfg = $exe.FullName + '.config'
    $doc = New-Object System.Xml.XmlDocument

    if (Test-Path $cfg) {
        $doc.Load($cfg)
    }
    else {
        $doc.LoadXml('<?xml version="1.0" encoding="utf-8"?><configuration></configuration>')
    }

    $configuration = $doc.DocumentElement

    $runtime = $configuration.SelectSingleNode('runtime')
    if ($null -eq $runtime) {
        $runtime = $doc.CreateElement('runtime')
        $configuration.AppendChild($runtime) | Out-Null
    }

    # 找到第一个 assemblyBinding，没有则创建（带正确命名空间）
    $ab = $null
    foreach ($c in $runtime.ChildNodes) {
        if ($c -is [System.Xml.XmlElement] -and $c.LocalName -eq 'assemblyBinding') {
            $ab = $c
            break
        }
    }
    if ($null -eq $ab) {
        $ab = $doc.CreateElement('assemblyBinding', $ns)
        $runtime.AppendChild($ab) | Out-Null
    }

    $has = $false
    foreach ($c in $ab.ChildNodes) {
        if ($c -is [System.Xml.XmlElement] -and $c.LocalName -eq 'probing') {
            $has = $true
            break
        }
    }

    if (-not $has) {
        $probing = $doc.CreateElement('probing', $ns)
        $probing.SetAttribute('privatePath', 'lib') | Out-Null
        $ab.AppendChild($probing) | Out-Null
        $doc.Save($cfg)
        Write-Host "[merge-probing] added probing to $cfg"
    }
}
