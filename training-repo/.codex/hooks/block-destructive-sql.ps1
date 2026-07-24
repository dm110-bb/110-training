$rawInput = [Console]::In.ReadToEnd()

try {
    $payload = $rawInput | ConvertFrom-Json
    $command = $payload.tool_input.command
    if (-not $command) {
        $command = $payload.tool_input.cmd
    }
} catch {
    [Console]::Error.WriteLine("Unable to inspect command input safely.")
    exit 2
}

if ($command -is [array]) {
    $command = $command -join " "
}

if ($command -match "\b(DROP\s+(TABLE|DATABASE)|TRUNCATE(\s+TABLE)?)\b") {
    [Console]::Error.WriteLine(
        "Blocked destructive SQL command. Database schema/data destruction requires explicit manual approval."
    )
    exit 2
}

exit 0
