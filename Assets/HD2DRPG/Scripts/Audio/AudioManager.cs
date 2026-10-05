using System.Collections.Generic;
using UnityEngine;

namespace HD2DRPG
{
    /// <summary>Plays BGM with crossfades and pooled one-shot SFX.</summary>
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        AudioSource bgmA, bgmB;
        AudioSource[] sfx;
        int sfxIndex;
        string currentBgm;
        float bgmVolume = 0.55f, sfxVolume = 0.8f;
        Coroutine fade;

        public float BgmVolume
        {
            get => bgmVolume;
            set { bgmVolume = Mathf.Clamp01(value); if (bgmA) bgmA.volume = bgmVolume; }
        }

        public float SfxVolume
        {
            get => sfxVolume;
            set => sfxVolume = Mathf.Clamp01(value);
        }

        public static AudioManager Create(Transform parent)
        {
            var go = new GameObject("Audio");
            go.transform.SetParent(parent, false);
            return go.AddComponent<AudioManager>();
        }

        void Awake()
        {
            Instance = this;
            bgmA = gameObject.AddComponent<AudioSource>();
            bgmB = gameObject.AddComponent<AudioSource>();
            foreach (var s in new[] { bgmA, bgmB }) { s.loop = true; s.playOnAwake = false; s.spatialBlend = 0; }
            sfx = new AudioSource[10];
            for (int i = 0; i < sfx.Length; i++)
            {
                sfx[i] = gameObject.AddComponent<AudioSource>();
                sfx[i].playOnAwake = false;
                sfx[i].spatialBlend = 0;
            }
        }

        public static void Play(string id, float vol = 1f, float pitch = 1f)
        {
            if (Instance == null) return;
            var s = Instance.sfx[Instance.sfxIndex];
            Instance.sfxIndex = (Instance.sfxIndex + 1) % Instance.sfx.Length;
            s.pitch = pitch;
            s.PlayOneShot(Synth.Get(id), vol * Instance.sfxVolume);
        }

        public static void Music(string id, float fadeTime = 0.8f, bool loop = true)
        {
            if (Instance == null || Instance.currentBgm == id) return;
            Instance.currentBgm = id;
            if (Instance.fade != null) Instance.StopCoroutine(Instance.fade);
            Instance.fade = Instance.StartCoroutine(Instance.CrossFade(id, fadeTime, loop));
        }

        public static void StopMusic(float fadeTime = 0.8f)
        {
            if (Instance == null) return;
            Instance.currentBgm = null;
            if (Instance.fade != null) Instance.StopCoroutine(Instance.fade);
            Instance.fade = Instance.StartCoroutine(Instance.CrossFade(null, fadeTime, true));
        }

        System.Collections.IEnumerator CrossFade(string id, float time, bool loop)
        {
            var from = bgmA;
            var to = bgmB;
            if (id != null)
            {
                to.clip = Synth.Get(id);
                to.loop = loop;
                to.volume = 0;
                to.Play();
            }
            float startVol = from.volume;
            for (float t = 0; t < time; t += Time.unscaledDeltaTime)
            {
                float k = t / time;
                from.volume = startVol * (1 - k);
                if (id != null) to.volume = bgmVolume * k;
                yield return null;
            }
            from.Stop();
            from.volume = 0;
            if (id != null) to.volume = bgmVolume;
            bgmA = to;
            bgmB = from;
            fade = null;
        }
    }
}
