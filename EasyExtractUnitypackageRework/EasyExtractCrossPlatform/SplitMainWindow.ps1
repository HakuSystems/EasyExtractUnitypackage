$ErrorActionPreference = 'Stop'
$mainPath = 'MainWindow.axaml.cs'
Copy-Item $mainPath "$mainPath.bak" -Force
$script:content = Get-Content -Path $mainPath -Raw

function Extract-Method
{
    param([string]$Name)
    $escaped = [regex]::Escape($Name)
    $pattern = "(?ms)^\s*(?:\[[^\]]+\]\s*)*(?:public|private|protected|internal)[^\n{;=]*\b$escaped\s*\("
    $match = [regex]::Match($script:content, $pattern)
    if (-not $match.Success)
    {
        throw "Method $Name not found"
    }
    $start = $match.Index
    $pre = $start
    while ($pre -gt 0)
    {
        $prevNewline = $script:content.LastIndexOf("`n", [int][Math]::Max(0, $pre - 1))
        if ($prevNewline -lt 0)
        {
            break
        }
        $line = $script:content.Substring($prevNewline + 1, $pre - $prevNewline - 1)
        $trim = $line.Trim()
        if ($trim.Length -eq 0 -or $trim.StartsWith('///') -or $trim.StartsWith('//') -or $trim.StartsWith('['))
        {
            $pre = $prevNewline + 1
            continue
        }
        break
    }
    $start = $pre
    Write-Host "Brace search for $Name starting at $start"
    $braceStart = $script:content.IndexOf('{', $match.Index)
    if ($braceStart -lt 0)
    {
        throw "No body for $Name"
    }
    $depth = 0
    $end = -1
    for ($i = $braceStart; $i -lt $script:content.Length; $i++) {
        $ch = $script:content[$i]
        if ($ch -eq '{')
        {
            $depth++
        }
        elseif ($ch -eq '}')
        {
            $depth--
            if ($depth -eq 0)
            {
                $end = $i + 1; break
            }
        }
    }
    if ($end -lt 0)
    {
        throw "Unmatched braces for $Name"
    }
    Write-Host "Found end for $Name at $end"
    $length = $end - $start
    $chunk = $script:content.Substring($start, $length)
    $script:content = $script:content.Remove($start, $length)
    return $chunk
}

$first = 'DropZoneBorder_OnDragEnter'
Write-Host "Extracting $first"
$chunk = Extract-Method -Name $first
Write-Host "Chunk length: $( $chunk.Length )"
