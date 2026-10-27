param(
    [string]$PipeName = "mpv-test-pipe",
    [string]$OutFile
)

$server = [System.IO.Pipes.NamedPipeServerStream]::new(
    $PipeName,
    [System.IO.Pipes.PipeDirection]::InOut,
    1,
    [System.IO.Pipes.PipeTransmissionMode]::Byte,
    [System.IO.Pipes.PipeOptions]::Asynchronous)
$server.WaitForConnection()

$reader = [System.IO.StreamReader]::new($server)
$line = $reader.ReadLine()
[System.IO.File]::WriteAllText($OutFile, $line)

$writer = [System.IO.StreamWriter]::new($server)
$writer.AutoFlush = $true
$writer.WriteLine('{"request_id":1,"error":"success","data":null}')

Start-Sleep -Milliseconds 600
$server.Dispose()
