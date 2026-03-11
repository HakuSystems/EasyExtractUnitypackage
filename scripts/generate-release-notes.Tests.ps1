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
        Invoke-Git -RepositoryPath $RepositoryPath commit -m $Message | Out-Null
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

    It 'creates grouped house-style notes from tagged commits' {
        $repoPath = Join-Path $TestDrive 'repo-grouped'
        New-TestRepository -Path $repoPath
        Invoke-Git -RepositoryPath $repoPath tag V1.0.0 | Out-Null

        New-TestCommit -RepositoryPath $repoPath -Message 'feat(core): enhance path validation and extraction security' -Files @{
            'EasyExtractUnitypackageRework/EasyExtract.Core/Services/UnityPackageExtractionService.Paths.cs' = 'security'
        }
        New-TestCommit -RepositoryPath $repoPath -Message 'fix(search): harden Everything SDK bootstrap locking' -Files @{
            'EasyExtractUnitypackageRework/EasyExtractCrossPlatform/Services/EverythingSdkBootstrapper.cs' = 'search'
        }
        New-TestCommit -RepositoryPath $repoPath -Message 'chore(project): bump version to 1.1.0' -Files @{
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
        $content | Should Match '## V1.1.0 - The .* Update'
        $content | Should Match '### Security & Extraction'
        $content | Should Match '### Search & UI'
        $content | Should Match '\*\*Full Changelog\*\*: https://github.com/HakuSystems/EasyExtractUnitypackage/compare/V1.0.0\.\.\.V1.1.0'
        $content | Should Not Match 'bump version'
    }

    It 'adds the installation docs link when release workflow or installation files change' {
        $repoPath = Join-Path $TestDrive 'repo-install-docs'
        New-TestRepository -Path $repoPath
        Invoke-Git -RepositoryPath $repoPath tag V2.0.0 | Out-Null

        New-TestCommit -RepositoryPath $repoPath -Message 'feat(services): enhance update check logic with detailed states' -Files @{
            '.github/workflows/publish.yml' = 'workflow'
            'EasyExtractUnitypackageRework/EasyExtractCrossPlatform/docs/PlatformInstallation.md' = 'docs'
        }
        Invoke-Git -RepositoryPath $repoPath tag V2.1.0 | Out-Null

        $outputPath = Join-Path $TestDrive 'release-install-notes.md'
        & powershell -NoProfile -ExecutionPolicy Bypass -File $scriptPath `
            -RepositoryPath $repoPath `
            -CurrentTag 'V2.1.0' `
            -PreviousTag 'V2.0.0' `
            -RepoUrl 'https://github.com/HakuSystems/EasyExtractUnitypackage' `
            -OutputPath $outputPath

        $LASTEXITCODE | Should Be 0
        $content = Get-Content -Raw $outputPath
        $content | Should Match '### 📥 Installation Guides'
        $content | Should Match 'View Platform Installation Docs'
    }

    It 'still emits valid notes when only release-noise commits exist' {
        $repoPath = Join-Path $TestDrive 'repo-release-noise'
        New-TestRepository -Path $repoPath
        Invoke-Git -RepositoryPath $repoPath tag V3.0.0 | Out-Null

        New-TestCommit -RepositoryPath $repoPath -Message 'chore(project): bump version to 3.0.1' -Files @{
            'EasyExtractUnitypackageRework/EasyExtractCrossPlatform/EasyExtractCrossPlatform.csproj' = 'version'
        }
        New-TestCommit -RepositoryPath $repoPath -Message 'refactor(project): clean up formatting' -Files @{
            'EasyExtractUnitypackageRework/EasyExtractCrossPlatform/App.axaml.cs' = 'cleanup'
        }
        Invoke-Git -RepositoryPath $repoPath tag V3.0.1 | Out-Null

        $outputPath = Join-Path $TestDrive 'release-noise-notes.md'
        & powershell -NoProfile -ExecutionPolicy Bypass -File $scriptPath `
            -RepositoryPath $repoPath `
            -CurrentTag 'V3.0.1' `
            -PreviousTag 'V3.0.0' `
            -RepoUrl 'https://github.com/HakuSystems/EasyExtractUnitypackage' `
            -OutputPath $outputPath

        $LASTEXITCODE | Should Be 0
        $content = Get-Content -Raw $outputPath
        $content | Should Match '^# V3.0.1'
        $content | Should Match '### Under the Hood'
        $content | Should Match '\*\*Full Changelog\*\*: https://github.com/HakuSystems/EasyExtractUnitypackage/compare/V3.0.0\.\.\.V3.0.1'
    }

    It 'falls back to a commits link when there is no previous tag' {
        $repoPath = Join-Path $TestDrive 'repo-no-previous-tag'
        New-TestRepository -Path $repoPath

        New-TestCommit -RepositoryPath $repoPath -Message 'feat(search): improve keyboard shortcut handling' -Files @{
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

    It 'emits XML-safe notes for Velopack packaging' {
        $repoPath = Join-Path $TestDrive 'repo-velopack-safe'
        New-TestRepository -Path $repoPath
        Invoke-Git -RepositoryPath $repoPath tag V5.0.0 | Out-Null

        New-TestCommit -RepositoryPath $repoPath -Message 'feat(core): enhance path validation and extraction security' -Files @{
            'EasyExtractUnitypackageRework/EasyExtract.Core/Services/UnityPackageExtractionService.Paths.cs' = 'security'
        }
        New-TestCommit -RepositoryPath $repoPath -Message 'fix(search): harden Everything SDK bootstrap locking' -Files @{
            'EasyExtractUnitypackageRework/EasyExtractCrossPlatform/Services/EverythingSdkBootstrapper.cs' = 'search'
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
        $content | Should Match '### Security &amp; Extraction'
        $content | Should Match '### Search &amp; UI'

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
        $xml.DocumentElement.InnerText | Should Match 'Security & Extraction'
        $xml.DocumentElement.InnerText | Should Match 'Search & UI'
    }
}
