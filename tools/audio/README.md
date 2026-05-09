# Panoptes Local Audio Generator

This tool generates placeholder Panoptes audio locally with dependency-free
Node.js procedural synthesis. It writes PCM 16-bit mono WAV files and a manifest
directly into the Unity client asset tree.

## Generate Assets

Run from the repository root:

```bash
node tools/audio/generate-audio.mjs
```

Output is written under:

```text
client/Assets/Art/Audio/Resources/Audio/
```

The MVP catalog generates:

- 3 loop-oriented BGM WAVs in `client/Assets/Art/Audio/Resources/Audio/BGM/`
  (`bg1.wav`, `bg2.wav`, `bg3.wav`)
- 1 user-provided gacha screen BGM in `client/Assets/Art/Audio/Resources/Audio/BGM/`
  (`choukabg.wav`)
- 4 UI click WAVs in `client/Assets/Art/Audio/Resources/Audio/SFX/UI/`
- 4 attack SFX WAVs in `client/Assets/Art/Audio/Resources/Audio/SFX/Attack/`
- `client/Assets/Art/Audio/manifest.json`
- deterministic Unity `.meta` files for the generated folders and assets

The nested `Resources/Audio` folder is intentional: runtime presentation code
loads clips with `Resources.Load<AudioClip>("Audio/...")`.

## Verify Assets

Run:

```bash
node tools/audio/generate-audio.mjs verify
```

Verification checks that every catalog asset exists and has the expected
RIFF/WAVE PCM header, channel count, sample rate, bit depth, and sample length.

## Asset Manifest

The generated manifest records:

- asset id
- category and type
- repo-relative Unity asset path
- intended usage
- style notes and future generation prompt
- provider id
- WAV format details
- byte size and SHA-256

The manifest deliberately avoids timestamps so repeated local regeneration is
stable when the catalog and synthesis code do not change.

Unity `.meta` GUIDs are derived from repo-relative asset paths so generated
audio can be committed without waiting for editor-side import.

## Future Provider Seam

The current provider is `local-procedural`. To add an API-backed or DAW-backed
provider later, keep the asset ids, category paths, and manifest fields stable,
then replace or route `renderLocalProcedural()` in `generate-audio.mjs`.

Do not add third-party packages for the local path; this first version is meant
to run with only the Node.js standard library.
