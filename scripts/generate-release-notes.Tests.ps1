$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

Describe 'generate-release-notes.ps1' {
    $repoRoot = Split-Path -Parent $PSScriptRoot
    $scriptPath = Join-Path $repoRoot 'scripts\generate-release-notes.ps1'

    function Invoke-Git {
        param(
            [Parameter(Mandatory = $true)]
            [string] $RepositoryPath,

            [Parameter(ValueFromRemainingArguments = $true)]
            [string[]] $Arguments
        )

        $output = & git -C $RepositoryPath @Arguments 2>&1
        if ($LASTEXITCODE -ne 0) {
            throw "git $($Arguments -join ' ') failed: $output"
        }

        return ($output -join [Environment]::NewLine).Trim()
    }

    function New-TestCommit {
        param(
            [Parameter(Mandatory = $true)]
            [string] $RepositoryPath,

            [Parameter(Mandatory = $true)]
            [string] $Message,

            [string] $MessageBody,

            [Parameter(Mandatory = $true)]
            [hashtable] $Files
        )

        foreach ($entry in $Files.GetEnumerator()) {
            $fullPath = Join-Path $RepositoryPath $entry.Key
            $directory = Split-Path -Parent $fullPath
            if ($directory -and -not (Test-Path $directory)) {
                New-Item -ItemType Directory -Path $directory -Force | Out-Null
            }

            Set-Content -Path $fullPath -Value $entry.Value -Encoding UTF8
        }

        Invoke-Git -RepositoryPath $RepositoryPath add .

        if ([string]::IsNullOrWhiteSpace($MessageBody)) {
            Invoke-Git -RepositoryPath $RepositoryPath commit -m $Message | Out-Null
        }
        else {
            Invoke-Git -RepositoryPath $RepositoryPath commit -m $Message -m $MessageBody | Out-Null
        }
    }

    function New-TestRepository {
        param(
            [Parameter(Mandatory = $true)]
            [string] $Path
        )

        New-Item -ItemType Directory -Path $Path -Force | Out-Null
        Invoke-Git -RepositoryPath $Path init | Out-Null
        Invoke-Git -RepositoryPath $Path config user.name 'Codex Tests' | Out-Null
        Invoke-Git -RepositoryPath $Path config user.email 'codex-tests@example.com' | Out-Null

        New-TestCommit -RepositoryPath $Path -Message 'chore: bootstrap repository' -Files @{
            'README.md' = 'seed'
        }
    }

    It 'renders commits since the previous tag in the new raw changelog format' {
        $repoPath = Join-Path $TestDrive 'repo-raw-commits'
        New-TestRepository -Path $repoPath
        Invoke-Git -RepositoryPath $repoPath tag V1.0.0 | Out-Null

        New-TestCommit -RepositoryPath $repoPath `
            -Message 'feat(core): enhance extraction security and sync authorization' `
            -MessageBody @'
Introduce security measures for UnityPackage extraction.

Implement token-based authorization for synchronization services.
'@ `
            -Files @{
                'EasyExtractUnitypackageRework/EasyExtract.Core/Services/UnityPackageExtractionService.Paths.cs' = 'security'
            }

        New-TestCommit -RepositoryPath $repoPath `
            -Message 'refactor(logging): change error logs to warnings for invalid package data' `
            -MessageBody 'Adjust log levels so invalid package data does not show up as a false hard failure.' `
            -Files @{
                'EasyExtractUnitypackageRework/EasyExtractCrossPlatform/Services/LoggingService.cs' = 'logging'
            }

        New-TestCommit -RepositoryPath $repoPath `
            -Message 'chore(project): bump version to 1.1.0' `
            -MessageBody 'Update project files to reflect the new version number.' `
            -Files @{
                'EasyExtractUnitypackageRework/EasyExtractCrossPlatform/EasyExtractCrossPlatform.csproj' = 'version'
            }

        Invoke-Git -RepositoryPath $repoPath tag V1.1.0 | Out-Null

        $outputPath = Join-Path $TestDrive 'release-notes.md'
        & powershell -NoProfile -ExecutionPolicy Bypass -File $scriptPath `
            -RepositoryPath $repoPath `
            -CurrentTag 'V1.1.0' `
            -PreviousTag 'V1.0.0' `
            -RepoUrl 'https://github.com/HakuSystems/EasyExtractUnitypackage' `
            -OutputPath $outputPath

        $LASTEXITCODE | Should Be 0
        Test-Path $outputPath | Should Be $true

        $content = Get-Content -Raw $outputPath
        $content | Should Match '^# V1.1.0'
        $content | Should Match '## New Release Auto Generated Release Notes'
        $content | Should Match 'Here are all commits since the last version:'
        $content | Should Match 'feat\(core\): enhance extraction security and sync authorization'
        $content | Should Match 'Introduce security measures for UnityPackage extraction\.'
        $content | Should Match 'refactor\(logging\): change error logs to warnings for invalid package data'
        $content | Should Match 'chore\(project\): bump version to 1\.1\.0'
        $content | Should Match '### 📥 Installation Guides'
        $content | Should Match '\*\*Full Changelog\*\*: https://github.com/HakuSystems/EasyExtractUnitypackage/compare/V1.0.0\.\.\.V1.1.0'
    }

    It 'skips merge commits but keeps normal commits including version bumps' {
        $repoPath = Join-Path $TestDrive 'repo-skip-merges'
        New-TestRepository -Path $repoPath
        Invoke-Git -RepositoryPath $repoPath tag V2.0.0 | Out-Null

        New-TestCommit -RepositoryPath $repoPath `
            -Message 'fix(core): harden archive parser' `
            -MessageBody 'Prevent malformed payloads from cascading into parser crashes.' `
            -Files @{
                'EasyExtractUnitypackageRework/EasyExtract.Core/Services/ArchiveParser.cs' = 'parser'
            }

        $mergeTree = Invoke-Git -RepositoryPath $repoPath write-tree
        $parentHead = Invoke-Git -RepositoryPath $repoPath rev-parse HEAD
        $mergeCommit = Invoke-Git -RepositoryPath $repoPath @(
            'commit-tree',
            $mergeTree,
            '-p',
            $parentHead,
            '-m',
            "Merge remote-tracking branch 'origin/main'"
        )
        Invoke-Git -RepositoryPath $repoPath reset --hard $mergeCommit | Out-Null

        New-TestCommit -RepositoryPath $repoPath `
            -Message 'chore(project): bump version to 2.1.0' `
            -MessageBody 'Update project files to reflect version 2.1.0.' `
            -Files @{
                'EasyExtractUnitypackageRework/EasyExtractCrossPlatform/EasyExtractCrossPlatform.csproj' = 'version'
            }

        Invoke-Git -RepositoryPath $repoPath tag V2.1.0 | Out-Null

        $outputPath = Join-Path $TestDrive 'release-merge-notes.md'
        & powershell -NoProfile -ExecutionPolicy Bypass -File $scriptPath `
            -RepositoryPath $repoPath `
            -CurrentTag 'V2.1.0' `
            -PreviousTag 'V2.0.0' `
            -RepoUrl 'https://github.com/HakuSystems/EasyExtractUnitypackage' `
            -OutputPath $outputPath

        $LASTEXITCODE | Should Be 0
        $content = Get-Content -Raw $outputPath
        $content | Should Match 'fix\(core\): harden archive parser'
        $content | Should Match 'chore\(project\): bump version to 2\.1\.0'
        $content | Should Not Match 'Merge remote-tracking branch'
    }

    It 'falls back to a commits link when there is no previous tag' {
        $repoPath = Join-Path $TestDrive 'repo-no-previous-tag'
        New-TestRepository -Path $repoPath

        New-TestCommit -RepositoryPath $repoPath `
            -Message 'feat(search): improve keyboard shortcut handling' `
            -MessageBody 'Keep the search box responsive under repeated shortcut input.' `
            -Files @{
                'EasyExtractUnitypackageRework/EasyExtractCrossPlatform/Utilities/DeferredTextBoxShortcutHandler.cs' = 'shortcut'
            }

        Invoke-Git -RepositoryPath $repoPath tag V4.0.0 | Out-Null

        $outputPath = Join-Path $TestDrive 'release-first-tag-notes.md'
        & powershell -NoProfile -ExecutionPolicy Bypass -File $scriptPath `
            -RepositoryPath $repoPath `
            -CurrentTag 'V4.0.0' `
            -RepoUrl 'https://github.com/HakuSystems/EasyExtractUnitypackage' `
            -OutputPath $outputPath

        $LASTEXITCODE | Should Be 0
        $content = Get-Content -Raw $outputPath
        $content | Should Match '^# V4.0.0'
        $content | Should Match '\*\*Full Changelog\*\*: https://github.com/HakuSystems/EasyExtractUnitypackage/commits/V4.0.0'
    }

    It 'preserves conventional commit body and footer sections' {
        $repoPath = Join-Path $TestDrive 'repo-conventional-footer'
        New-TestRepository -Path $repoPath
        Invoke-Git -RepositoryPath $repoPath tag V4.1.0 | Out-Null

        New-TestCommit -RepositoryPath $repoPath `
            -Message 'feat(api): harden release notes parsing' `
            -MessageBody @'
Normalize commit bodies before rendering them into release notes.

BREAKING CHANGE: release notes now split commit footers explicitly.
'@ `
            -Files @{
                'EasyExtractUnitypackageRework/EasyExtractCrossPlatform/Services/ReleaseNotesParser.cs' = 'parser'
            }

        Invoke-Git -RepositoryPath $repoPath tag V4.2.0 | Out-Null

        $outputPath = Join-Path $TestDrive 'release-footer-notes.md'
        & powershell -NoProfile -ExecutionPolicy Bypass -File $scriptPath `
            -RepositoryPath $repoPath `
            -CurrentTag 'V4.2.0' `
            -PreviousTag 'V4.1.0' `
            -RepoUrl 'https://github.com/HakuSystems/EasyExtractUnitypackage' `
            -OutputPath $outputPath

        $LASTEXITCODE | Should Be 0
        $content = Get-Content -Raw $outputPath
        $content | Should Match 'feat\(api\): harden release notes parsing'
        $content | Should Match 'Normalize commit bodies before rendering them into release notes\.'
        $content | Should Match 'BREAKING CHANGE: release notes now split commit footers explicitly\.'
    }

    It 'filters out commits that do not match the supported Rider format' {
        $repoPath = Join-Path $TestDrive 'repo-filter-invalid-subjects'
        New-TestRepository -Path $repoPath
        Invoke-Git -RepositoryPath $repoPath tag V4.2.0 | Out-Null

        New-TestCommit -RepositoryPath $repoPath `
            -Message 'Update README formatting' `
            -MessageBody 'This should not be emitted because the subject is not in the supported format.' `
            -Files @{
                'README.md' = 'readme'
            }

        New-TestCommit -RepositoryPath $repoPath `
            -Message 'docs(readme): clarify release notes format' `
            -MessageBody 'Document the expected Rider-style commit message rules for release notes.' `
            -Files @{
                'docs/release-notes.md' = 'rules'
            }

        Invoke-Git -RepositoryPath $repoPath tag V4.3.0 | Out-Null

        $outputPath = Join-Path $TestDrive 'release-filtered-notes.md'
        & powershell -NoProfile -ExecutionPolicy Bypass -File $scriptPath `
            -RepositoryPath $repoPath `
            -CurrentTag 'V4.3.0' `
            -PreviousTag 'V4.2.0' `
            -RepoUrl 'https://github.com/HakuSystems/EasyExtractUnitypackage' `
            -OutputPath $outputPath

        $LASTEXITCODE | Should Be 0
        $content = Get-Content -Raw $outputPath
        $content | Should Not Match 'Update README formatting'
        $content | Should Match 'docs\(readme\): clarify release notes format'
    }

    It 'emits XML-safe notes for Velopack packaging' {
        $repoPath = Join-Path $TestDrive 'repo-velopack-safe'
        New-TestRepository -Path $repoPath
        Invoke-Git -RepositoryPath $repoPath tag V5.0.0 | Out-Null

        New-TestCommit -RepositoryPath $repoPath `
            -Message 'feat(core): support special characters in release notes' `
            -MessageBody 'Handle A & B < C > D without breaking package metadata.' `
            -Files @{
                'EasyExtractUnitypackageRework/EasyExtract.Core/Services/ReleaseNotesService.cs' = 'special'
            }

        Invoke-Git -RepositoryPath $repoPath tag V5.1.0 | Out-Null

        $outputPath = Join-Path $TestDrive 'release-velopack-notes.md'
        & powershell -NoProfile -ExecutionPolicy Bypass -File $scriptPath `
            -RepositoryPath $repoPath `
            -CurrentTag 'V5.1.0' `
            -PreviousTag 'V5.0.0' `
            -RepoUrl 'https://github.com/HakuSystems/EasyExtractUnitypackage' `
            -OutputPath $outputPath `
            -OutputMode 'Velopack'

        $LASTEXITCODE | Should Be 0
        $content = Get-Content -Raw $outputPath
        $content | Should Match 'A &amp; B &lt; C &gt; D'

        $wrappedXml = "<releaseNotes>$content</releaseNotes>"
        $xmlParseError = $null
        $xml = $null
        try {
            $xml = [xml] $wrappedXml
        }
        catch {
            $xmlParseError = $_
        }

        $xmlParseError | Should Be $null
        $xml.DocumentElement.InnerText | Should Match 'A & B < C > D'
    }
}
