param(
    [Parameter(Mandatory = $true)]
    [string] $CurrentTag,

    [string] $PreviousTag,

    [Parameter(Mandatory = $true)]
    [string] $RepoUrl,

    [Parameter(Mandatory = $true)]
    [string] $OutputPath,

    [string] $RepositoryPath = '.',

    [ValidateSet('GitHub', 'Velopack')]
    [string] $OutputMode = 'GitHub'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Invoke-GitCommand {
    param(
        [Parameter(Mandatory = $true)]
        [string[]] $Arguments
    )

    $output = & git -C $script:ResolvedRepositoryPath @Arguments 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "git $($Arguments -join ' ') failed: $output"
    }

    return @($output)
}

function Get-CommitRange {
    if ([string]::IsNullOrWhiteSpace($PreviousTag)) {
        return $CurrentTag
    }

    return "$PreviousTag..$CurrentTag"
}

function Get-CommitEntries {
    $range = Get-CommitRange
    $lines = Invoke-GitCommand @('log', '--reverse', '--no-merges', '--format=__COMMIT__%n%B%n__ENDCOMMIT__', $range)
    $entries = New-Object System.Collections.Generic.List[object]
    $buffer = New-Object System.Collections.Generic.List[string]
    $isCapturingCommit = $false

    foreach ($rawLine in $lines) {
        $line = [string] $rawLine

        if ($line -eq '__COMMIT__') {
            $buffer = New-Object System.Collections.Generic.List[string]
            $isCapturingCommit = $true
            continue
        }

        if ($line -eq '__ENDCOMMIT__') {
            $message = ($buffer -join [Environment]::NewLine).Trim()
            if (
                -not [string]::IsNullOrWhiteSpace($message) -and
                $message -notmatch '^(?i)merge\b'
            ) {
                $entries.Add((ConvertTo-CommitEntry -Message $message))
            }

            $isCapturingCommit = $false
            continue
        }

        if ($isCapturingCommit) {
            $buffer.Add($line.TrimEnd())
        }
    }

    return $entries
}

function Test-FooterLine {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Line
    )

    if ($Line -match '^[A-Za-z-]+(?: [A-Za-z-]+)*: .+') {
        return $true
    }

    if ($Line -match '^(Fixes|Closes|Refs|Resolves|Related to)\s+#?\S+') {
        return $true
    }

    return $false
}

function Test-SupportedCommitSubject {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Subject
    )

    return $Subject -match '^(feat|fix|refactor|chore|docs|style|test|perf)(\([a-z0-9-]+\))?: .+\S$'
}

function ConvertTo-CommitEntry {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Message
    )

    $normalizedMessage = ($Message -replace "`r`n", "`n" -replace "`r", "`n").Trim()
    $lines = $normalizedMessage -split "`n"

    $subject = ''
    $subjectIndex = -1
    for ($i = 0; $i -lt $lines.Length; $i++) {
        if (-not [string]::IsNullOrWhiteSpace($lines[$i])) {
            $subject = $lines[$i].Trim()
            $subjectIndex = $i
            break
        }
    }

    if ([string]::IsNullOrWhiteSpace($subject)) {
        return [pscustomobject] @{
            Include = $false
            Subject = ''
            Body = ''
            Footer = ''
            Message = ''
        }
    }

    $remainderLines = @()
    if ($subjectIndex + 1 -lt $lines.Length) {
        $remainderLines = @($lines[($subjectIndex + 1)..($lines.Length - 1)])
    }

    $remainder = ($remainderLines -join "`n").Trim()
    $body = ''
    $footer = ''

    if (-not [string]::IsNullOrWhiteSpace($remainder)) {
        $paragraphs = @(
            $remainder -split "(?:`n){2,}" |
            ForEach-Object { $_.Trim() } |
            Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
        )

        $footerParagraphs = New-Object System.Collections.Generic.List[string]
        for ($i = $paragraphs.Length - 1; $i -ge 0; $i--) {
            $footerLines = @(
                $paragraphs[$i] -split "`n" |
                ForEach-Object { $_.Trim() } |
                Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
            )

            if ($footerLines.Count -gt 0 -and (@($footerLines | Where-Object { -not (Test-FooterLine -Line $_) }).Count -eq 0)) {
                $footerParagraphs.Insert(0, ($footerLines -join "`n"))
                continue
            }

            break
        }

        $bodyParagraphCount = $paragraphs.Length - $footerParagraphs.Count
        if ($bodyParagraphCount -gt 0) {
            $body = (@($paragraphs[0..($bodyParagraphCount - 1)]) -join "`n`n").Trim()
        }

        if ($footerParagraphs.Count -gt 0) {
            $footer = ($footerParagraphs -join "`n`n").Trim()
        }
    }

    return [pscustomobject] @{
        Include = (Test-SupportedCommitSubject -Subject $subject)
        Subject = $subject
        Body = $body
        Footer = $footer
        Message = $normalizedMessage
    }
}

function Get-ChangelogUrl {
    if ([string]::IsNullOrWhiteSpace($PreviousTag)) {
        return "$RepoUrl/commits/$CurrentTag"
    }

    return "$RepoUrl/compare/$PreviousTag...$CurrentTag"
}

function Get-InstallationHeading {
    $emoji = [System.Text.Encoding]::UTF8.GetString([byte[]] @(0xF0, 0x9F, 0x93, 0xA5))
    return "### $emoji Installation Guides"
}

function ConvertTo-XmlSafeContent {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Content
    )

    return [System.Security.SecurityElement]::Escape($Content)
}

function Get-ReleaseNotesContent {
    param(
        [Parameter(Mandatory = $true)]
        [System.Collections.Generic.List[object]] $Commits
    )

    $builder = New-Object System.Text.StringBuilder

    [void] $builder.AppendLine("# $CurrentTag")
    [void] $builder.AppendLine()
    [void] $builder.AppendLine('## New Release Auto Generated Release Notes')
    [void] $builder.AppendLine()
    [void] $builder.AppendLine('Here are all commits since the last version:')
    [void] $builder.AppendLine()

    $releaseReadyCommits = @($Commits | Where-Object { $_.Include })

    if ($releaseReadyCommits.Count -eq 0) {
        [void] $builder.AppendLine('No release-note-ready commits found since the last version.')
        [void] $builder.AppendLine()
    }
    else {
        foreach ($commit in $releaseReadyCommits) {
            [void] $builder.AppendLine($commit.Subject)

            if (-not [string]::IsNullOrWhiteSpace($commit.Body)) {
                [void] $builder.AppendLine()
                [void] $builder.AppendLine($commit.Body)
            }

            if (-not [string]::IsNullOrWhiteSpace($commit.Footer)) {
                [void] $builder.AppendLine()
                [void] $builder.AppendLine($commit.Footer)
            }

            [void] $builder.AppendLine()
        }
    }

    [void] $builder.AppendLine((Get-InstallationHeading))
    [void] $builder.AppendLine('[View Platform Installation Docs](https://github.com/HakuSystems/EasyExtractUnitypackage/blob/main/EasyExtractUnitypackageRework/EasyExtractCrossPlatform/docs/PlatformInstallation.md)')
    [void] $builder.AppendLine()
    [void] $builder.AppendLine("**Full Changelog**: $(Get-ChangelogUrl)")

    return $builder.ToString().TrimEnd()
}

$script:ResolvedRepositoryPath = (Resolve-Path -Path $RepositoryPath).ProviderPath
$null = Invoke-GitCommand @('rev-parse', '--verify', $CurrentTag)
if (-not [string]::IsNullOrWhiteSpace($PreviousTag)) {
    $null = Invoke-GitCommand @('rev-parse', '--verify', $PreviousTag)
}

$commitEntries = Get-CommitEntries
$releaseNotes = Get-ReleaseNotesContent -Commits $commitEntries

if ($OutputMode -eq 'Velopack') {
    $releaseNotes = ConvertTo-XmlSafeContent -Content $releaseNotes
}

$outputDirectory = Split-Path -Parent $OutputPath
if ($outputDirectory -and -not (Test-Path $outputDirectory)) {
    New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
}

Set-Content -Path $OutputPath -Value $releaseNotes -Encoding UTF8
