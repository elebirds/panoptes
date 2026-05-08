using System;
using System.Collections.Generic;
using UnityEngine;

namespace Panoptes.Presentation.Audio
{
    public enum UiClickAudioKind
    {
        Soft,
        Confirm,
        Back,
        Disabled
    }

    public enum AttackAudioKind
    {
        Blade,
        Arrow,
        Siege,
        Charge
    }

    public sealed class PresentationAudioService : IDisposable
    {
        private const float DefaultBgmVolume = 0.32f;
        private const float DefaultSfxVolume = 0.72f;

        private readonly Dictionary<string, AudioClip> _clips = new(StringComparer.Ordinal);
        private readonly HashSet<string> _warnedMissingClips = new(StringComparer.Ordinal);

        private GameObject _audioRoot;
        private AudioSource _bgmSource;
        private AudioSource _sfxSource;
        private string _currentBgmPath = string.Empty;

        public void PlayMainMenuBgm()
        {
            PlayBgm(PresentationAudioAssetIds.MainMenuBgm);
        }

        public void PlayTurnBgm(int turn)
        {
            PlayBgm(turn > 15
                ? PresentationAudioAssetIds.LaterFifteenTurnsBgm
                : PresentationAudioAssetIds.FirstFifteenTurnsBgm);
        }

        public void PlayBgm(string resourcesPath, float volume = DefaultBgmVolume)
        {
            if (string.IsNullOrWhiteSpace(resourcesPath))
            {
                return;
            }

            EnsureSources();
            if (_bgmSource == null)
            {
                return;
            }

            var normalizedPath = resourcesPath.Trim().Trim('/');
            if (string.Equals(_currentBgmPath, normalizedPath, StringComparison.Ordinal) && _bgmSource.isPlaying)
            {
                _bgmSource.volume = Mathf.Clamp01(volume);
                return;
            }

            var clip = LoadClip(normalizedPath);
            if (clip == null)
            {
                return;
            }

            _currentBgmPath = normalizedPath;
            _bgmSource.clip = clip;
            _bgmSource.loop = true;
            _bgmSource.volume = Mathf.Clamp01(volume);
            _bgmSource.Play();
        }

        public void StopBgm()
        {
            if (_bgmSource == null)
            {
                return;
            }

            _bgmSource.Stop();
            _bgmSource.clip = null;
            _currentBgmPath = string.Empty;
        }

        public void PlayUiClick(UiClickAudioKind kind = UiClickAudioKind.Soft)
        {
            PlayOneShot(ResolveUiClickPath(kind), DefaultSfxVolume);
        }

        public void PlayAttack(AttackAudioKind kind = AttackAudioKind.Blade)
        {
            PlayOneShot(ResolveAttackPath(kind), DefaultSfxVolume);
        }

        public void PlayOneShot(string resourcesPath, float volume = DefaultSfxVolume)
        {
            if (string.IsNullOrWhiteSpace(resourcesPath))
            {
                return;
            }

            EnsureSources();
            if (_sfxSource == null)
            {
                return;
            }

            var clip = LoadClip(resourcesPath.Trim().Trim('/'));
            if (clip == null)
            {
                return;
            }

            _sfxSource.PlayOneShot(clip, Mathf.Clamp01(volume));
        }

        public void Dispose()
        {
            if (_audioRoot == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(_audioRoot);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(_audioRoot);
            }

            _audioRoot = null;
            _bgmSource = null;
            _sfxSource = null;
            _clips.Clear();
            _currentBgmPath = string.Empty;
        }

        private void EnsureSources()
        {
            if (_audioRoot != null && _bgmSource != null && _sfxSource != null)
            {
                return;
            }

            _audioRoot = new GameObject("Presentation Audio Service");
            if (Application.isPlaying)
            {
                UnityEngine.Object.DontDestroyOnLoad(_audioRoot);
            }

            _bgmSource = CreateSource("BGM");
            _sfxSource = CreateSource("SFX");
        }

        private AudioSource CreateSource(string name)
        {
            var child = new GameObject(name);
            child.transform.SetParent(_audioRoot.transform, false);
            var source = child.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.ignoreListenerPause = true;
            return source;
        }

        private AudioClip LoadClip(string resourcesPath)
        {
            if (_clips.TryGetValue(resourcesPath, out var cached))
            {
                return cached;
            }

            var clip = Resources.Load<AudioClip>(resourcesPath);
            if (clip == null)
            {
                WarnMissingClip(resourcesPath);
                return null;
            }

            _clips[resourcesPath] = clip;
            return clip;
        }

        private void WarnMissingClip(string resourcesPath)
        {
            if (!_warnedMissingClips.Add(resourcesPath))
            {
                return;
            }

            PanoptesLog.Warning($"[PresentationAudio] Missing AudioClip Resources/{resourcesPath}.");
        }

        private static string ResolveUiClickPath(UiClickAudioKind kind)
        {
            return kind switch
            {
                UiClickAudioKind.Confirm => PresentationAudioAssetIds.UiClickConfirm,
                UiClickAudioKind.Back => PresentationAudioAssetIds.UiClickBack,
                UiClickAudioKind.Disabled => PresentationAudioAssetIds.UiClickDisabled,
                _ => PresentationAudioAssetIds.UiClickSoft
            };
        }

        private static string ResolveAttackPath(AttackAudioKind kind)
        {
            return kind switch
            {
                AttackAudioKind.Arrow => PresentationAudioAssetIds.AttackArrowImpact,
                AttackAudioKind.Siege => PresentationAudioAssetIds.AttackSiegeThud,
                AttackAudioKind.Charge => PresentationAudioAssetIds.AttackChargeImpact,
                _ => PresentationAudioAssetIds.AttackBladeHit
            };
        }
    }
}
