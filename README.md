# WaveeVorbis
A C# vorbis decoder based on Symphonia (rust)

Work TODO: 
- [Floor 0](/Wavee.Vorbis/Decoder/Setup/Floor/Floor0.cs)
- Convenient wrapper around OggReader/OggVorbis to provide simple samples access
- Optimize [IMDCT](/WaveeVorbis/blob/main/Wavee.Vorbis/Decoder/Imdct.cs) (Inverse modified discrete cosine transform) 

# Usage

### Decoder creation
```cs

//Construct the Format Reader (ogg)
var stream = ...; // your .NET stream
var oggReaderMaybe = OggReader.TryNew(
    source: new MediaSourceStream(stream, MediaSourceStreamOptions.Default),
    FormatOptions.Default with { EnableGapless = true }
);
var oggReader = oggReaderMaybe.Match(
    Succ: x => x,
    Fail: e =>
    {
        var error = oggReaderMaybe.Match(
            Succ: _ => throw new Exception("Unexpected success"),
            Fail: e => e
        );
        throw error;
    }
);

//Get a decoder for a track (probably the first/default track)
var track = oggReader.DefaultTrack.ValueUnsafe();
var decoderMaybe = VorbisDecoder.TryNew(track.CodecParams, new DecoderOptions());
var decoder = decoderMaybe.Match(
    Succ: x => x,
    Fail: e =>
    {
        var error = decoderMaybe.Match(
            Succ: _ => throw new Exception("Unexpected success"),
            Fail: e => e
        );
        throw error;
    }
);


//Consume packets
while(true)
{
    var packetMaybe = oggReader.NextPacket()
        .Match(Succ: x => Option<OggPacket>.Some(x), Fail: e =>
        {
            Console.WriteLine($"Error reading packet: {e.Message}");
            return Option<OggPacket>.None;
        });
    if (packetMaybe.IsNone)
    {
        continue;
    }
    var packet = packetMaybe.ValueUnsafe();
    // Decode the packet into audio samples.
    var decodeResult = decoder.Decode(packet);
    if (decodeResult.IsFaulted)
    {
        continue;
    }

    var samples = decodeResult.Match(x => x, e => throw e);
    //Do something with samples
    //WriteToAudioSink(samples)...
}
```

### Seeking
```cs
var seekres = oggReader.Seek(SeekMode.Accurate, to: TimeSpan.FromSeconds(60));
if (seekres.IsFaulted)
{
    var exception = seekres.Match(Succ: _ => throw new Exception("Unexpected success"), Fail: e => e)
    Console.WriteLine(e);
}
```
