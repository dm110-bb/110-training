$rawInput = [Console]::In.ReadToEnd()

try {
    $payload = $rawInput | ConvertFrom-Json
} catch {
    exit 0
}

$paths = [System.Collections.Generic.List[string]]::new()
$toolInput = $payload.tool_input

foreach ($propertyName in @("file_path", "path")) {
    $value = $toolInput.$propertyName
    if ($value) {
        $paths.Add([string]$value)
    }
}

$patchText = $toolInput.patch
if (-not $patchText) {
    $patchText = $toolInput.input
}

if ($patchText) {
    $matches = [regex]::Matches(
        [string]$patchText,
        "(?m)^\*\*\* (?:Add|Update|Delete) File: (.+)$"
    )
    foreach ($match in $matches) {
        $paths.Add($match.Groups[1].Value.Trim())
    }
}

$uniquePaths = $paths | Sort-Object -Unique
if (-not $uniquePaths) {
    exit 0
}

$logPath = Join-Path $PSScriptRoot "edit-log.txt"
$timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
$toolName = if ($payload.tool_name) { $payload.tool_name } else { "apply_patch" }

foreach ($path in $uniquePaths) {
    Add-Content -LiteralPath $logPath -Value "$timestamp  $toolName  $path"
}

@{
    systemMessage = "OrderHub edit hook logged $($uniquePaths.Count) file(s)."
} | ConvertTo-Json -Compress
