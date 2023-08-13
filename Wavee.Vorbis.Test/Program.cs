// See https://aka.ms/new-console-template for more information

using System.Runtime.InteropServices;
using LanguageExt;
using LanguageExt.UnsafeValueAccess;
using NAudio.Wave;
using Serilog;
using Wavee.Vorbis;
using Wavee.Vorbis.Decoder;
using Wavee.Vorbis.Infrastructure.Stream;
using Wavee.Vorbis.Packets;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.Console()
    .CreateLogger();
using var fs = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Free_Test_Data_5MB_OGG.ogg"));

var oggReaderMaybe = OggReader.TryNew(
    source: new MediaSourceStream(fs, MediaSourceStreamOptions.Default),
    FormatOptions.Default with { EnableGapless = true }
);
if (oggReaderMaybe.IsFaulted)
{
    var error = oggReaderMaybe.Match(
        Succ: _ => throw new Exception("Unexpected success"),
        Fail: e => e
    );
    throw error;
}

var oggReader = oggReaderMaybe.Match(x => x, e => throw e);
var track = oggReader.DefaultTrack.ValueUnsafe();
var decoderMaybe = VorbisDecoder.TryNew(track.CodecParams, new DecoderOptions());
if (decoderMaybe.IsFaulted)
{
    var error = decoderMaybe.Match(
        Succ: _ => throw new Exception("Unexpected success"),
        Fail: e => e
    );
    throw error;
}

var decoder = decoderMaybe.Match(x => x, e => throw e);
//var seekres = oggReader.Seek(SeekMode.Accurate, to: TimeSpan.FromSeconds(60));
using var waveout = new WaveOutEvent();
var waveformat = WaveFormat.CreateIeeeFloatWaveFormat(
    sampleRate: (int)track.CodecParams.SampleRate.ValueUnsafe(), channels: track.CodecParams.ChannelsCount.ValueUnsafe()
);
var buffer = new BufferedWaveProvider(waveformat);
waveout.Init(buffer);
waveout.Play();
TimeSpan prevTime = TimeSpan.Zero;
bool seeked = false;
var totalTime = oggReader.TotalTime;
while (true)
{
    // Decode all packets, ignoring all decode errors.
    var packetMaybe = oggReader.NextPacket()
        .Match(Succ: x => Option<OggPacket>.Some(x), Fail: e =>
        {
            Log.Error(e, "Error reading packet");
            return Option<OggPacket>.None;
        });
    if (packetMaybe.IsNone)
    {
        break;
    }

    var packet = packetMaybe.ValueUnsafe();
    // Decode the packet into audio samples.
    var decodeResult = decoder.Decode(packet);
    if (decodeResult.IsFaulted)
    {
        Log.Error(decodeResult.Match(Succ: _ => throw new Exception("Unexpected success"), Fail: e => e),
            "Error decoding packet");
        continue;
    }

    var samples = decodeResult.Match(x => x, e => throw e);
    var bytes = MemoryMarshal.Cast<float, byte>(samples).ToArray();
    buffer.AddSamples(bytes, 0, bytes.Length);
    while (buffer.BufferedDuration.TotalSeconds > .5)
    {
        await Task.Delay(10);
    }

    var time = oggReader.PositionOfPacket(packet).ValueUnsafe();
    //print if time has changed by at least 1 second
    if ((time - prevTime) > TimeSpan.FromSeconds(1))
    {
        Log.Information("Time: {Time}", time);
        prevTime = time;
    }
    //if time reaches 5 seconds, seek to 60 seconds
    if (time > TimeSpan.FromSeconds(5) && !seeked)
    {
        var seekres = oggReader.Seek(SeekMode.Accurate, to: TimeSpan.FromSeconds(60));
        if (seekres.IsFaulted)
        {
            Log.Error(seekres.Match(Succ: _ => throw new Exception("Unexpected success"), Fail: e => e),
                "Error seeking");
        }
        
        seeked = true;
    }
}