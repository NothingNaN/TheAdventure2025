using Silk.NET.SDL;
using System.Runtime.InteropServices;

namespace TheAdventure;

public unsafe class AudioManager : IDisposable
{
    private readonly Sdl _sdl;
    private readonly Dictionary<string, byte[]> _audioBuffers = new();
    private readonly Random _random = new();

    private uint _audioDevice;
    private bool _isInitialized = false;

    public AudioManager(Sdl sdl)
    {
        _sdl = sdl;
        InitializeAudio();
    }

    private void InitializeAudio()
    {
        if (_sdl.InitSubSystem(Sdl.InitAudio) < 0)
        {
            Console.WriteLine($"Failed to initialize SDL Audio: {_sdl.GetErrorS()}");
            return;
        }

        AudioSpec desiredSpec;
        AudioSpec obtainedSpec;

        // Manually zero the struct so the callback is NULL
        desiredSpec = default;
        desiredSpec.Freq = 44100;
        desiredSpec.Format = Sdl.AudioS16Lsb;
        desiredSpec.Channels = 2;
        desiredSpec.Samples = 2048;
        desiredSpec.Callback = default; // This sets it to null under the hood in unsafe code

        _audioDevice = _sdl.OpenAudioDevice((byte*)0, 0, ref desiredSpec, &obtainedSpec, 0);
        if (_audioDevice == 0)
        {
            Console.WriteLine($"Failed to open audio device: {_sdl.GetErrorS()}");
            return;
        }

        _isInitialized = true;
        _sdl.PauseAudioDevice(_audioDevice, 0);
        Console.WriteLine("Audio system initialized successfully");
    }


    public void LoadAudio(string fileName, string key)
    {
        if (!_isInitialized)
        {
            Console.WriteLine("Audio system not initialized");
            return;
        }

        try
        {
            if (!File.Exists(fileName))
            {
                Console.WriteLine($"Audio file not found: {fileName}");
                return;
            }

            // Read the WAV file as raw bytes
            // For basic WAV files, we'll skip the header and load the audio data
            var fileBytes = File.ReadAllBytes(fileName);

            // Basic WAV file validation (check for "RIFF" and "WAVE")
            if (fileBytes.Length < 44 ||
                System.Text.Encoding.ASCII.GetString(fileBytes, 0, 4) != "RIFF" ||
                System.Text.Encoding.ASCII.GetString(fileBytes, 8, 4) != "WAVE")
            {
                Console.WriteLine($"Invalid WAV file format: {fileName}");
                return;
            }

            // Skip WAV header (44 bytes for standard WAV) and get audio data
            var audioData = new byte[fileBytes.Length - 44];
            Array.Copy(fileBytes, 44, audioData, 0, audioData.Length);

            _audioBuffers[key] = audioData;
            Console.WriteLine($"Loaded WAV audio: {key} ({audioData.Length} bytes)");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Exception loading audio {fileName}: {ex.Message}");
        }
    }

    public void PlaySound(string key)
    {
        if (!_isInitialized || !_audioBuffers.ContainsKey(key))
        {
            Console.WriteLine($"Audio not available: {key}");
            return;
        }

        try
        {
            var buffer = _audioBuffers[key];

            // Queue audio for playback
            fixed (byte* bufferPtr = buffer)
            {
                _sdl.QueueAudio(_audioDevice, bufferPtr, (uint)buffer.Length);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Exception playing sound {key}: {ex.Message}");
        }
    }

    public void PlayRandomFart()
    {
        var fartNumber = _random.Next(1, 4); // 1, 2, or 3
        PlaySound($"fart{fartNumber}");
    }

    public void PlayMegaFart()
    {
        PlaySound("megafart");
    }

    public void PlayOof()
    {
        PlaySound("oof");
    }

    public void Dispose()
    {
        if (_isInitialized)
        {
            _sdl.PauseAudioDevice(_audioDevice, 1);
            _sdl.CloseAudioDevice(_audioDevice);
        }

        _audioBuffers.Clear();
        _sdl.QuitSubSystem(Sdl.InitAudio);
    }
}