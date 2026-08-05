param(
    [string]$Root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
)

$ErrorActionPreference = 'Stop'
$errors = New-Object Collections.Generic.List[string]
$markdownFiles = @(Get-ChildItem -LiteralPath $Root -File -Recurse -Filter '*.md' | Where-Object {
    $_.FullName -notmatch '[\\/]\.git[\\/]' -and $_.FullName -notmatch '[\\/](bin|obj|Build)[\\/]'
})
$linkCount = 0

function Get-MarkdownSlug {
    param([string]$Heading)
    $value = $Heading.Trim() -replace '`', ''
    $value = $value.ToLowerInvariant()
    $value = [regex]::Replace($value, '[^\p{L}\p{Nd}\s-]', '')
    return [regex]::Replace($value, '\s+', '-')
}

foreach ($file in $markdownFiles) {
    $content = Get-Content -LiteralPath $file.FullName -Raw
    foreach ($match in [regex]::Matches($content, '\[[^\]]+\]\(([^)]+)\)')) {
        $linkCount++
        $target = $match.Groups[1].Value.Trim()
        if ($target.StartsWith('<') -and $target.Contains('>')) { $target = $target.Substring(1, $target.IndexOf('>') - 1) }
        if ($target -match '(?i)^(https?://|mailto:|steam:)') { continue }
        $parts = $target.Split('#', 2)
        $relativePath = $parts[0]
        $fragment = if ($parts.Count -eq 2) { $parts[1] } else { $null }
        if ([string]::IsNullOrWhiteSpace($relativePath)) {
            $targetPath = $file.FullName
        }
        else {
            $targetPath = [IO.Path]::GetFullPath((Join-Path $file.DirectoryName ($relativePath.Replace('/', [IO.Path]::DirectorySeparatorChar))))
        }
        if (-not (Test-Path -LiteralPath $targetPath -PathType Leaf)) {
            $errors.Add($file.FullName + ' -> ' + $target) | Out-Null
            continue
        }
        if (-not [string]::IsNullOrWhiteSpace($fragment)) {
            $headingFound = $false
            $targetContent = Get-Content -LiteralPath $targetPath
            foreach ($line in $targetContent) {
                if ($line -match '^\s*#{1,6}\s+(.+?)\s*#*\s*$' -and (Get-MarkdownSlug $Matches[1]) -eq $fragment.ToLowerInvariant()) {
                    $headingFound = $true
                    break
                }
            }
            if (-not $headingFound -and -not ($targetContent -match '(?i)name=["'']' + [regex]::Escape($fragment) + '["'']')) {
                $errors.Add($file.FullName + ' -> missing anchor ' + $target) | Out-Null
            }
        }
    }
}

if ($errors.Count -gt 0) {
    Write-Output ('FAIL documentation-links files={0} links={1} errors={2}' -f $markdownFiles.Count, $linkCount, $errors.Count)
    $errors | ForEach-Object { Write-Output ('FAIL documentation-link ' + $_) }
    exit 1
}
Write-Output ('PASS documentation-links files={0} links={1}' -f $markdownFiles.Count, $linkCount)
exit 0
