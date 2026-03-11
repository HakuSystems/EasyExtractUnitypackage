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
    $lines = Invoke-GitCommand @('log', '--reverse', '--name-only', '--format=__COMMIT__|%H|%s', $range)
    $entries = New-Object System.Collections.Generic.List[object]
    $current = $null

    foreach ($rawLine in $lines) {
        $line = [string] $rawLine
        if ($line.StartsWith('__COMMIT__|', [System.StringComparison]::Ordinal)) {
            if ($null -ne $current) {
                $entries.Add([pscustomobject] $current)
            }

            $parts = $line.Split('|', 3)
            $current = @{
                Hash = $parts[1]
                Subject = $parts[2]
                Files = New-Object System.Collections.Generic.List[string]
            }

            continue
        }

        if ($null -eq $current) {
            continue
        }

        if (-not [string]::IsNullOrWhiteSpace($line)) {
            $current.Files.Add($line.Trim())
        }
    }

    if ($null -ne $current) {
        $entries.Add([pscustomobject] $current)
    }

    return $entries
}

function Test-NoiseCommit {
    param(
        [Parameter(Mandatory = $true)]
        [pscustomobject] $Commit
    )

    $subject = $Commit.Subject.Trim()
    if ($subject -match '(?i)\bbump version to\b') {
        return $true
    }

    if ($subject -match '^(?i)(merge|release)\b') {
        return $true
    }

    return $false
}

function Get-CleanSubject {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Subject
    )

    $clean = $Subject -replace '^\[[^\]]+\]:\s*', ''
    $clean = $clean -replace '^(feat|fix|chore|refactor|docs|test|perf)(\([^)]+\))?:\s*', ''
    $clean = $clean.Trim()

    if ([string]::IsNullOrWhiteSpace($clean)) {
        return 'Maintenance update'
    }

    return [char]::ToUpperInvariant($clean[0]) + $clean.Substring(1)
}

function Get-PatternSummary {
    param(
        [Parameter(Mandatory = $true)]
        [pscustomobject] $Commit
    )

    $subject = $Commit.Subject.ToLowerInvariant()
    $files = ($Commit.Files | ForEach-Object { $_.ToLowerInvariant() })

    $matchers = @(
        @{
            Bucket = 'Security & Extraction'
            Key = 'path-validation'
            Predicate = {
                $subject -match 'path validation|extraction security' -or
                @($files | Where-Object { $_ -match 'unitypackageextractionservice\.(paths|fileoperations|types|info)\.cs$|unitypackageextractionlimits|unitypackageextractionservicesecuritytests' }).Count -gt 0
            }
            Title = 'Path validation and extraction security'
            Description = 'Hardened extraction path checks so unsafe targets get blocked before they can trash your output.'
        },
        @{
            Bucket = 'Security & Extraction'
            Key = 'tar-error-mapping'
            Predicate = {
                $subject -match 'gzip checksum|tar parse failure|invaliddataexception' -or
                @($files | Where-Object { $_ -match 'unitypackageextractionservice\.extraction\.cs$|unitypackageextractionserviceerrormappingtests' }).Count -gt 0
            }
            Title = 'Tar error reporting cleanup'
            Description = 'Stopped normal tar parse failures from getting mislabeled as fake gzip checksum errors, so the logs are less useless.'
        },
        @{
            Bucket = 'Security & Extraction'
            Key = 'garbage-data'
            Predicate = {
                $subject -match 'trailing garbage|truncated trailing data|garbage padding' -or
                @($files | Where-Object { $_ -match 'unitypackagepreviewservice|unitypackageextractionservice\.extraction\.session' }).Count -gt 0
            }
            Title = 'Unitypackage garbage-data handling'
            Description = 'Made extraction and previewing stop choking on trailing junk data so broken packages do not nuke the run.'
        },
        @{
            Bucket = 'Search & UI'
            Key = 'shortcut-handling'
            Predicate = {
                $subject -match 'shortcut handling|keyboard shortcut' -or
                @($files | Where-Object { $_ -match 'deferredtextboxshortcuthandler' }).Count -gt 0
            }
            Title = 'Shortcut handling and UI responsiveness'
            Description = 'Reworked the shortcut pipeline so the UI feels faster and stops lagging behind your input.'
        },
        @{
            Bucket = 'Search & UI'
            Key = 'everything-bootstrap'
            Predicate = {
                $subject -match 'everything sdk bootstrap|bootstrap locking' -or
                @($files | Where-Object { $_ -match 'everythingsdkbootstrapper' }).Count -gt 0
            }
            Title = 'Everything SDK bootstrap stability'
            Description = 'Hardened the Everything SDK startup path so concurrent launches stop stepping on each other.'
        },
        @{
            Bucket = 'Settings & Stability'
            Key = 'settings-access'
            Predicate = {
                $subject -match 'file access handling|settings file|settings parsing|deserialization' -or
                @($files | Where-Object { $_ -match 'appsettingsservice|models/appsettings' }).Count -gt 0
            }
            Title = 'Settings file resilience'
            Description = 'Tightened settings reads and writes so short lock windows or malformed entries stop blowing up your config.'
        },
        @{
            Bucket = 'Settings & Stability'
            Key = 'notifications-and-crashes'
            Predicate = {
                $subject -match 'notificationservice|background security scan|access denied|crash' -or
                @($files | Where-Object { $_ -match 'notificationservice|mainwindow\.security|errordialogservice' }).Count -gt 0
            }
            Title = 'Crash and notification cleanup'
            Description = 'Cleaned up noisy failure paths so background tasks and notifications stop face-planting at runtime.'
        },
        @{
            Bucket = 'OS-Specific Stuff'
            Key = 'linux-search'
            Predicate = {
                $subject -match 'linux fd path|fd path-style queries' -or
                @($files | Where-Object { $_ -match 'linuxsearchservice' }).Count -gt 0
            }
            Title = 'Linux search path handling'
            Description = 'Fixed Linux path-style queries so slash-heavy searches stop breaking the fd integration.'
        },
        @{
            Bucket = 'OS-Specific Stuff'
            Key = 'mac-context-menu'
            Predicate = {
                $subject -match 'home directory path|fix\(mac\)' -or
                @($files | Where-Object { $_ -match 'contextmenuintegrationservice\.mac' }).Count -gt 0
            }
            Title = 'macOS context-menu path resolution'
            Description = 'Corrected the macOS home-directory resolution so the context-menu install path stops pointing at nonsense.'
        },
        @{
            Bucket = 'Under the Hood'
            Key = 'updates'
            Predicate = {
                $subject -match 'update check logic|velopack|update' -or
                @($files | Where-Object { $_ -match 'velopackupdateservice|mainwindow\.updates|package-allplatforms|publish\.yml' }).Count -gt 0
            }
            Title = 'Update pipeline cleanup'
            Description = 'Smoothed out the update flow so release checks and packaging metadata behave more predictably.'
        },
        @{
            Bucket = 'Under the Hood'
            Key = 'sync-dedup'
            Predicate = {
                $subject -match 'deduplication|synchronization' -or
                @($files | Where-Object { $_ -match 'hakusyncservice' }).Count -gt 0
            }
            Title = 'Sync request deduplication'
            Description = 'Stopped duplicate sync work from spamming the same activity over and over for no good reason.'
        }
    )

    foreach ($matcher in $matchers) {
        if (& $matcher.Predicate) {
            return [pscustomobject] @{
                Bucket = $matcher.Bucket
                Key = $matcher.Key
                Title = $matcher.Title
                Description = $matcher.Description
            }
        }
    }

    $fallbackBucket = 'Under the Hood'
    if ($subject -match '^(refactor|chore|docs|test)\b') {
        $fallbackBucket = 'Under the Hood'
    }
    elseif ($subject -match 'search|shortcut|ui' -or @($files | Where-Object { $_ -match 'everythingsdkbootstrapper|linuxsearchservice|deferredtextboxshortcuthandler' }).Count -gt 0) {
        $fallbackBucket = 'Search & UI'
    }
    elseif ($subject -match 'setting|deserial|notification|error|crash' -or @($files | Where-Object { $_ -match 'appsettings|notificationservice|errordialogservice' }).Count -gt 0) {
        $fallbackBucket = 'Settings & Stability'
    }
    elseif ($subject -match 'mac|linux|windows|osx' -or @($files | Where-Object { $_ -match '\.mac\.|\.linux\.|\.windows\.' }).Count -gt 0) {
        $fallbackBucket = 'OS-Specific Stuff'
    }
    elseif ($subject -match 'core|extract|security' -or @($files | Where-Object { $_ -match 'easyextract\.core|unitypackageextractionservice|unitypackagepreviewservice' }).Count -gt 0) {
        $fallbackBucket = 'Security & Extraction'
    }

    $cleanSubject = Get-CleanSubject -Subject $Commit.Subject
    $genericDescription = switch ($fallbackBucket) {
        'Security & Extraction' { "$cleanSubject. Tightened the extraction path so ugly edge cases get blocked instead of wrecking the run." }
        'Search & UI' { "$cleanSubject. This pass keeps the search flow and UI from feeling janky under load." }
        'Settings & Stability' { "$cleanSubject. The goal here is simple: fewer random breakages and less busted state." }
        'OS-Specific Stuff' { "$cleanSubject. Platform-specific weirdness got cleaned up so the app behaves saner per OS." }
        default { "$cleanSubject. Mostly internal cleanup, but it still makes the release less cursed to ship." }
    }

    return [pscustomobject] @{
        Bucket = $fallbackBucket
        Key = ('generic-' + ($cleanSubject.ToLowerInvariant() -replace '[^a-z0-9]+', '-').Trim('-'))
        Title = $cleanSubject
        Description = $genericDescription
    }
}

function Get-GroupedBullets {
    param(
        [Parameter(Mandatory = $true)]
        [System.Collections.Generic.List[object]] $Commits
    )

    $groups = @{}

    foreach ($commit in $Commits) {
        $summary = Get-PatternSummary -Commit $commit
        $bucketKey = $summary.Bucket

        if (-not $groups.ContainsKey($bucketKey)) {
            $groups[$bucketKey] = @{}
        }

        if (-not $groups[$bucketKey].ContainsKey($summary.Key)) {
            $groups[$bucketKey][$summary.Key] = [pscustomobject] @{
                Title = $summary.Title
                Description = $summary.Description
                Count = 0
            }
        }

        $groups[$bucketKey][$summary.Key].Count++
    }

    return $groups
}

function Get-Theme {
    param(
        [Parameter(Mandatory = $true)]
        [hashtable] $GroupedBullets
    )

    $bucketOrder = @(
        'Security & Extraction',
        'Search & UI',
        'Settings & Stability',
        'OS-Specific Stuff',
        'Under the Hood'
    )

    $topBucket = 'Under the Hood'
    $topCount = -1

    foreach ($bucket in $bucketOrder) {
        $count = 0
        if ($GroupedBullets.ContainsKey($bucket)) {
            $count = @($GroupedBullets[$bucket].Values | Measure-Object -Property Count -Sum).Sum
        }

        if ($count -gt $topCount) {
            $topBucket = $bucket
            $topCount = $count
        }
    }

    switch ($topBucket) {
        'Security & Extraction' {
            return [pscustomobject] @{
                Title = 'The Anti-Nuke Update'
                Intro = 'yeah, this one is mostly about locking the app down so unsafe extraction paths and nasty package edge cases stop trying to wreck your machine.'
            }
        }
        'Search & UI' {
            return [pscustomobject] @{
                Title = 'The Search & UI Update'
                Intro = 'this release goes after the annoying laggy bits, so searching and moving around the app feels less cursed.'
            }
        }
        'Settings & Stability' {
            return [pscustomobject] @{
                Title = 'The Stability Update'
                Intro = 'we spent this pass cleaning up the stuff that randomly broke settings, notifications, and background work for no good reason.'
            }
        }
        'OS-Specific Stuff' {
            return [pscustomobject] @{
                Title = 'The Cross-Platform Fix Update'
                Intro = 'this one is mostly platform cleanup, because every OS keeps finding its own special way to be annoying.'
            }
        }
        default {
            return [pscustomobject] @{
                Title = 'The Stability Update'
                Intro = 'mostly under-the-hood cleanup this time, but it is the kind of work that makes the whole release less janky to live with.'
            }
        }
    }
}

function Get-SectionOrder {
    param(
        [Parameter(Mandatory = $true)]
        [hashtable] $GroupedBullets
    )

    $preferred = @(
        'Security & Extraction',
        'Search & UI',
        'Settings & Stability',
        'OS-Specific Stuff',
        'Under the Hood'
    )

    return @($preferred | Where-Object { $GroupedBullets.ContainsKey($_) -and $GroupedBullets[$_].Keys.Count -gt 0 })
}

function Test-ShouldIncludeInstallationGuide {
    param(
        [Parameter(Mandatory = $true)]
        [System.Collections.Generic.List[object]] $Commits
    )

    foreach ($commit in $Commits) {
        foreach ($file in $commit.Files) {
            if ($file -match '(?i)PlatformInstallation\.md|\.github/workflows/publish\.yml|Package-AllPlatforms\.ps1|VelopackUpdateService\.cs|MainWindow\.Updates\.cs|UpdateHandler\.cs') {
                return $true
            }
        }
    }

    return $false
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

    $userFacingCommits = New-Object System.Collections.Generic.List[object]
    foreach ($commit in $Commits) {
        if (-not (Test-NoiseCommit -Commit $commit)) {
            $userFacingCommits.Add($commit)
        }
    }

    if ($userFacingCommits.Count -eq 0) {
        foreach ($commit in $Commits) {
            if ($commit.Subject -match '^(?i)refactor|docs|test') {
                $userFacingCommits.Add($commit)
            }
        }
    }

    if ($userFacingCommits.Count -eq 0) {
        $userFacingCommits.Add([pscustomobject] @{
            Hash = 'synthetic'
            Subject = 'refactor(project): maintenance cleanup'
            Files = New-Object System.Collections.Generic.List[string]
        })
    }

    $groupedBullets = Get-GroupedBullets -Commits $userFacingCommits
    $theme = Get-Theme -GroupedBullets $groupedBullets
    $sections = Get-SectionOrder -GroupedBullets $groupedBullets
    $builder = New-Object System.Text.StringBuilder

    [void] $builder.AppendLine("# $CurrentTag")
    [void] $builder.AppendLine()
    [void] $builder.AppendLine("## $CurrentTag - $($theme.Title)")
    [void] $builder.AppendLine()
    [void] $builder.AppendLine($theme.Intro)
    [void] $builder.AppendLine()
    [void] $builder.AppendLine('Here is the stuff that actually matters:')
    [void] $builder.AppendLine()

    foreach ($section in $sections) {
        [void] $builder.AppendLine("### $section")
        [void] $builder.AppendLine()

        $bullets = @($groupedBullets[$section].Values | Sort-Object Title)
        foreach ($bullet in $bullets) {
            [void] $builder.AppendLine("* **$($bullet.Title):** $($bullet.Description)")
        }

        [void] $builder.AppendLine()
    }

    if (Test-ShouldIncludeInstallationGuide -Commits $userFacingCommits) {
        [void] $builder.AppendLine((Get-InstallationHeading))
        [void] $builder.AppendLine('[View Platform Installation Docs](https://github.com/HakuSystems/EasyExtractUnitypackage/blob/main/EasyExtractUnitypackageRework/EasyExtractCrossPlatform/docs/PlatformInstallation.md)')
        [void] $builder.AppendLine()
    }

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
