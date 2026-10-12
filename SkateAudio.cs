using System;
using System.IO;
using System.Reflection;
using LazyBearTechnology;
using UnityEngine;
using UnityEngine.Audio;

namespace GK2Skateboard
{
    /// <summary>
    /// Wheel roll loop (volume/pitch follow speed) plus ollie pop and landing one-shots.
    /// Sounds are CC0 Freesound recordings, see sfx/CREDITS.md. Routed to the game's SFX mixer group so its volume slider applies.
    /// </summary>
    internal class SkateAudio : MonoBehaviour
    {
        private static AudioClip roll, pop, land;
        private static AudioMixerGroup sfxGroup;
        private static bool groupSearched;

        private AudioSource rollSrc, shotSrc;
        private bool airborne;

        public void Init()
        {
            LoadClips();
            var group = FindSfxGroup();
            rollSrc = gameObject.AddComponent<AudioSource>();
            rollSrc.clip = roll;
            rollSrc.loop = true;
            rollSrc.playOnAwake = false;
            rollSrc.spatialBlend = 0f;
            rollSrc.volume = 0f;
            rollSrc.outputAudioMixerGroup = group;
            rollSrc.Play();
            shotSrc = gameObject.AddComponent<AudioSource>();
            shotSrc.playOnAwake = false;
            shotSrc.spatialBlend = 0f;
            shotSrc.outputAudioMixerGroup = group;
        }

        public void OnOllie()
        {
            airborne = true;
            shotSrc.PlayOneShot(pop, Plugin.Volume.Value);
        }

        public void OnLand()
        {
            airborne = false;
            shotSrc.PlayOneShot(land, Plugin.Volume.Value);
        }

        private void Update()
        {
            if (rollSrc == null) return;
            bool paused = MainGame.IsGamePaused;
            if (paused && rollSrc.isPlaying) rollSrc.Pause();
            else if (!paused && !rollSrc.isPlaying) rollSrc.UnPause();
            float top = Mathf.Max(0.01f, Skate.TopSpeed);
            float t = Mathf.Clamp01(Skate.ScreenVelocity.magnitude / top);
            float target = airborne ? 0f : Plugin.Volume.Value * 0.8f * Mathf.Sqrt(t);
            rollSrc.volume = Mathf.MoveTowards(rollSrc.volume, target, Time.unscaledDeltaTime * 3f);
            rollSrc.pitch = 0.75f + 0.55f * t;
        }

        private static AudioMixerGroup FindSfxGroup()
        {
            if (groupSearched) return sfxGroup;
            groupSearched = true;
            try
            {
                if (!LazyAudio.TryGetAudioMixer(out var mixer) || mixer == null) return null;
                var groups = mixer.FindMatchingGroups(string.Empty);
                if (Plugin.DebugLog.Value)
                    Plugin.Log.LogInfo("Mixer groups: " + string.Join(", ", Array.ConvertAll(groups, g => g.name)));
                foreach (var key in new[] { "sfx", "sound", "effect" })
                    foreach (var g in groups)
                        if (g.name.IndexOf(key, StringComparison.OrdinalIgnoreCase) >= 0) return sfxGroup = g;
            }
            catch (Exception ex) { Plugin.Log.LogWarning("Mixer lookup failed: " + ex.Message); }
            return null;
        }

        private static void LoadClips()
        {
            if (roll != null) return;
            roll = LoadWav("roll");
            pop = LoadWav("pop");
            land = LoadWav("land");
        }

        /// <summary>Minimal RIFF/WAVE reader for the embedded 16-bit PCM mono clips.</summary>
        private static AudioClip LoadWav(string name)
        {
            byte[] b;
            using (var s = Assembly.GetExecutingAssembly().GetManifestResourceStream($"GK2Skateboard.{name}.wav"))
            using (var ms = new MemoryStream())
            {
                s.CopyTo(ms);
                b = ms.ToArray();
            }
            int channels = 1, rate = 44100, pos = 12;
            while (pos + 8 <= b.Length)
            {
                string id = System.Text.Encoding.ASCII.GetString(b, pos, 4);
                int size = BitConverter.ToInt32(b, pos + 4);
                if (id == "fmt ")
                {
                    channels = BitConverter.ToInt16(b, pos + 10);
                    rate = BitConverter.ToInt32(b, pos + 12);
                }
                else if (id == "data")
                {
                    int n = size / 2;
                    var data = new float[n];
                    for (int i = 0; i < n; i++) data[i] = BitConverter.ToInt16(b, pos + 8 + i * 2) / 32768f;
                    var clip = AudioClip.Create("Skateboard_" + name, n / channels, channels, rate, false);
                    clip.SetData(data, 0);
                    return clip;
                }
                pos += 8 + size + (size & 1);
            }
            throw new InvalidDataException("No data chunk in " + name);
        }
    }
}
