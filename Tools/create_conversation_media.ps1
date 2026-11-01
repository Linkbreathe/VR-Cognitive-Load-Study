$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Speech
$projectRoot = Split-Path $PSScriptRoot -Parent
$sourceFolder = Join-Path $projectRoot 'MediaSources'
$outputFolder = Join-Path $projectRoot 'Assets/TaskPlatform/Media/Conversation'
$dialogues = @(
    @('Did you leave the notebook upstairs?', 'Yes, it is on the table beside the window.'),
    @('Would you like a cup of tea?', 'That sounds good. I will meet you in the kitchen.'),
    @('What time does the next bus arrive?', 'It should be here soon. We can wait by the entrance.'),
    @('Have you seen the keys for the cupboard?', 'I think they are on the shelf beside the books.'),
    @('Shall we take a walk after lunch?', 'Yes, the weather looks nice. We could go through the park.'),
    @('Is this the room for the afternoon meeting?', 'The meeting is next door. I can show you the way.'),
    @('Did the package arrive this morning?', 'Yes, someone left it at the front desk about an hour ago.'),
    @('Are these chairs needed in the other room?', 'Only two of them. The rest can stay here for now.')
)
$speaker = New-Object System.Speech.Synthesis.SpeechSynthesizer
$speaker.Rate = 0
$records = @()
try {
    for ($index = 0; $index -lt $dialogues.Count; $index++) {
        $identifier = 'conversation-{0:d2}' -f ($index + 1)
        $sourcePath = Join-Path $sourceFolder ($identifier + '-source.wav')
        $targetPath = Join-Path $outputFolder ($identifier + '.wav')
        $speaker.SetOutputToWaveFile($sourcePath)
        $speaker.SelectVoice('Microsoft David Desktop')
        $speaker.Speak($dialogues[$index][0])
        $speaker.SelectVoice('Microsoft Zira Desktop')
        $speaker.Speak($dialogues[$index][1])
        $speaker.SetOutputToNull()
        $duration = [double](& ffprobe -v error -show_entries format=duration -of default=noprint_wrappers=1:nokey=1 $sourcePath)
        $tempo = ([double][Math]::Max([double]1, $duration / 9.5)).ToString('R', [Globalization.CultureInfo]::InvariantCulture)
        & ffmpeg -hide_banner -loglevel error -y -i $sourcePath -af "atempo=$tempo,apad=whole_dur=5,highpass=f=90,lowpass=f=6500,loudnorm=I=-23:TP=-3:LRA=7,afade=t=in:d=0.01" -ac 1 -ar 48000 -c:a pcm_s16le $targetPath
        if ($LASTEXITCODE -ne 0) { throw 'Audio conversion failed.' }
        $finalDuration = [double](& ffprobe -v error -show_entries format=duration -of default=noprint_wrappers=1:nokey=1 $targetPath)
        if ($finalDuration -lt 5 -or $finalDuration -gt 10) { throw 'Dialogue duration outside requested range.' }
        $records += [PSCustomObject]@{id=$identifier;kind='conversation';file="Assets/TaskPlatform/Media/Conversation/$identifier.wav";duration=$finalDuration;voices=@('Microsoft David Desktop','Microsoft Zira Desktop');text=$dialogues[$index];provenance='Original English script, local Windows speech synthesis; not a recording of real people';sha256=(Get-FileHash -LiteralPath $targetPath -Algorithm SHA256).Hash}
        Write-Output "$identifier $finalDuration s"
    }
    $records | ConvertTo-Json -Depth 5 | Set-Content -Encoding UTF8 (Join-Path $sourceFolder 'conversation-manifest.json')
} finally { $speaker.Dispose() }
