$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Speech
$projectRoot=Split-Path $PSScriptRoot -Parent
$entries=Get-Content -Raw (Join-Path $PSScriptRoot 'news-speech-2026-11-01.json') | ConvertFrom-Json
$targetFolder=Join-Path $projectRoot 'Assets/TaskPlatform/Media/News'
New-Item -ItemType Directory -Force $targetFolder | Out-Null
$speaker=New-Object System.Speech.Synthesis.SpeechSynthesizer
$records=@()
try {
    $index=0
    foreach($entry in $entries) {
        $voice=if($index++ % 2 -eq 0){'Microsoft David Desktop'}else{'Microsoft Zira Desktop'}
        $speaker.SelectVoice($voice);$speaker.Rate=0
        $source=Join-Path $projectRoot ('MediaSources/'+$entry.id+'-source.wav')
        $target=Join-Path $targetFolder ($entry.id+'.wav')
        $speaker.SetOutputToWaveFile($source);$speaker.Speak($entry.text);$speaker.SetOutputToNull()
        $length=[double](& ffprobe -v error -show_entries format=duration -of default=noprint_wrappers=1:nokey=1 $source)
        $tempo=([Math]::Max([double]1,[double]($length/9.5))).ToString('R',[Globalization.CultureInfo]::InvariantCulture)
        & ffmpeg -hide_banner -loglevel error -y -i $source -af "atempo=$tempo,apad=whole_dur=5,highpass=f=90,lowpass=f=6500,loudnorm=I=-23:TP=-3:LRA=7,afade=t=in:d=0.02" -ac 1 -ar 48000 -c:a pcm_s16le $target
        if($LASTEXITCODE -ne 0){throw 'Speech conversion failed'}
        $duration=[double](& ffprobe -v error -show_entries format=duration -of default=noprint_wrappers=1:nokey=1 $target)
        if($duration -gt 10 -or $duration -lt 5){throw 'Speech duration outside bounds'}
        $kind=if($entry.id.StartsWith('news')){'news'}else{'softvoice'}
        $records+=[PSCustomObject]@{id=$entry.id;kind=$kind;file="Assets/TaskPlatform/Media/News/$($entry.id).wav";duration=$duration;voice=$voice;text=$entry.text;sourcePage=$entry.source;sourceDate=$entry.date;verifiedOn='2026-11-01';provenance='Original paraphrase or neutral script; local Windows speech synthesis; not a recording or impersonation. Softvoice uses reduced playback gain, not a true whisper synthesis model.';sha256=(Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash}
        Write-Output "$($entry.id): $duration s"
    }
    $records | ConvertTo-Json -Depth 8 | Set-Content -Encoding UTF8 (Join-Path $projectRoot 'MediaSources/news-manifest.json')
    $manifestPath=Join-Path $projectRoot 'Assets/TaskPlatform/Media/distraction-media-manifest.json'
    $manifest=Get-Content -Raw $manifestPath | ConvertFrom-Json
    $manifest.assets=@($manifest.assets | Where-Object {$_.kind -notin @('news','softvoice')})+$records
    $manifest.version='local-media-v1.7-2026-11-01'
    $manifest | ConvertTo-Json -Depth 10 | Set-Content -Encoding UTF8 $manifestPath
}finally{$speaker.Dispose()}
