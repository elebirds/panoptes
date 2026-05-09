#!/usr/bin/env node

import { createHash } from "node:crypto";
import { existsSync, mkdirSync, readFileSync, writeFileSync } from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const SAMPLE_RATE = 44100;
const BITS_PER_SAMPLE = 16;
const CHANNELS = 1;
const GENERATOR_VERSION = "0.1.0";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const repoRoot = path.resolve(__dirname, "../..");
const audioRoot = path.join(repoRoot, "client", "Assets", "Art", "Audio");
const outputRoot = path.join(audioRoot, "Resources", "Audio");
const manifestPath = path.join(audioRoot, "manifest.json");

const provider = {
  id: "local-procedural",
  version: GENERATOR_VERSION,
  externalDependencies: [],
  extensionPoint:
    "Replace renderLocalProcedural() with an API/DAW provider while preserving asset ids, paths, and manifest fields.",
};

const assets = [
  {
    id: "bgm_main_menu",
    category: "bgm",
    type: "music-loop",
    file: "BGM/bg1.wav",
    durationSeconds: 32,
    loop: true,
    usage: "Main menu and lobby background music.",
    styleNotes: "Relaxed medieval rustic countryside, melodic lazy plains village life, with a slightly modern/trendy flavor.",
    prompt:
      "Loopable medieval rustic countryside main menu music, relaxed melodic lazy plains village life, slightly modern trendy flavor.",
    patch: {
      kind: "bgm",
      seed: "main-menu-countryside",
      baseHz: 124,
      tempo: 88,
      chordMultipliers: [1, 1.25, 1.5, 2, 2.5],
      pulseHz: 2,
      bellHz: 496,
      color: "rustic",
    },
  },
  {
    id: "bgm_first_fifteen_turns",
    category: "bgm",
    type: "music-loop",
    file: "BGM/bg2.wav",
    durationSeconds: 32,
    loop: true,
    usage: "First fifteen turns background music.",
    styleNotes: "Orchestral calm before battle, preparation-phase tension, restrained sword-drawn atmosphere.",
    prompt:
      "Loopable orchestral calm before battle for the first fifteen turns, tense preparation phase, restrained sword-drawn atmosphere.",
    patch: {
      kind: "bgm",
      seed: "first-fifteen-preparation",
      baseHz: 98,
      tempo: 82,
      chordMultipliers: [1, 1.5, 2, 2.25, 3],
      pulseHz: 3,
      bellHz: 294,
      color: "tense",
    },
  },
  {
    id: "bgm_later_fifteen_turns",
    category: "bgm",
    type: "music-loop",
    file: "BGM/bg3.wav",
    durationSeconds: 32,
    loop: true,
    usage: "Later fifteen turns and high-intensity combat resolution background music.",
    styleNotes: "Orchestral, energetic two-armies-clashing combat feeling.",
    prompt:
      "Loopable energetic orchestral battle music for later fifteen turns, two armies clashing, strong combat momentum.",
    patch: {
      kind: "bgm",
      seed: "later-fifteen-battle",
      baseHz: 82,
      tempo: 124,
      chordMultipliers: [1, 1.5, 2, 2.25, 3],
      pulseHz: 5,
      bellHz: 246,
      color: "battle",
    },
  },
  {
    id: "ui_click_soft",
    category: "sfx-ui",
    type: "ui-click",
    file: "SFX/UI/click_soft.wav",
    durationSeconds: 0.18,
    loop: false,
    usage: "Default lightweight button press.",
    styleNotes: "Short soft tick with a rounded high-frequency edge.",
    prompt: "Soft UI click for strategy game buttons, crisp but not sharp.",
    patch: { kind: "ui", seed: "ui-click-soft", toneHz: 1800, bodyHz: 440, noise: 0.08 },
  },
  {
    id: "ui_click_confirm",
    category: "sfx-ui",
    type: "ui-click",
    file: "SFX/UI/click_confirm.wav",
    durationSeconds: 0.22,
    loop: false,
    usage: "Confirm, submit, accept, and positive action buttons.",
    styleNotes: "Two-stage click with a small upward confirmation tone.",
    prompt: "Positive confirm click, short tactile tick with subtle upward tone.",
    patch: { kind: "ui", seed: "ui-click-confirm", toneHz: 1320, bodyHz: 660, noise: 0.1, confirm: true },
  },
  {
    id: "ui_click_back",
    category: "sfx-ui",
    type: "ui-click",
    file: "SFX/UI/click_back.wav",
    durationSeconds: 0.2,
    loop: false,
    usage: "Back, cancel, close, and negative navigation.",
    styleNotes: "Lower rounded click with a falling tail.",
    prompt: "Subtle cancel/back UI click, low and rounded, not alarming.",
    patch: { kind: "ui", seed: "ui-click-back", toneHz: 900, bodyHz: 330, noise: 0.07, falling: true },
  },
  {
    id: "ui_click_disabled",
    category: "sfx-ui",
    type: "ui-click",
    file: "SFX/UI/click_disabled.wav",
    durationSeconds: 0.16,
    loop: false,
    usage: "Disabled or unavailable UI action feedback.",
    styleNotes: "Dull muted tap with little high-end energy.",
    prompt: "Muted disabled button tap, dull short feedback for unavailable action.",
    patch: { kind: "ui", seed: "ui-click-disabled", toneHz: 520, bodyHz: 180, noise: 0.04, muted: true },
  },
  {
    id: "attack_blade_hit",
    category: "sfx-attack",
    type: "attack",
    file: "SFX/Attack/blade_hit.wav",
    durationSeconds: 0.62,
    loop: false,
    usage: "Infantry melee hit placeholder.",
    styleNotes: "Noisy blade transient plus low body thump.",
    prompt: "Short infantry melee hit, metallic scrape transient and low impact body.",
    patch: { kind: "attack", seed: "attack-blade", profile: "blade" },
  },
  {
    id: "attack_arrow_impact",
    category: "sfx-attack",
    type: "attack",
    file: "SFX/Attack/arrow_impact.wav",
    durationSeconds: 0.5,
    loop: false,
    usage: "Archer attack impact placeholder.",
    styleNotes: "Fast whistle into dry wood-and-cloth impact.",
    prompt: "Arrow impact sound, brief whistle and dry tactical hit.",
    patch: { kind: "attack", seed: "attack-arrow", profile: "arrow" },
  },
  {
    id: "attack_siege_thud",
    category: "sfx-attack",
    type: "attack",
    file: "SFX/Attack/siege_thud.wav",
    durationSeconds: 0.9,
    loop: false,
    usage: "Siege engine hit, wall damage, or heavy structure impact placeholder.",
    styleNotes: "Heavy low thud with short rubble noise tail.",
    prompt: "Heavy siege impact, low thud, short stone rubble tail.",
    patch: { kind: "attack", seed: "attack-siege", profile: "siege" },
  },
  {
    id: "attack_charge_impact",
    category: "sfx-attack",
    type: "attack",
    file: "SFX/Attack/charge_impact.wav",
    durationSeconds: 0.7,
    loop: false,
    usage: "Charge or cavalry-like impact placeholder for future unit variants.",
    styleNotes: "Rising noisy rush into a compact impact.",
    prompt: "Charge impact, rising rush and compact tactical hit for strategy combat.",
    patch: { kind: "attack", seed: "attack-charge", profile: "charge" },
  },
];

function main() {
  const command = process.argv[2] ?? "generate";

  if (command === "generate") {
    generateAll();
    return;
  }

  if (command === "verify" || command === "--verify") {
    verifyAll();
    return;
  }

  if (command === "list" || command === "--list") {
    for (const asset of assets) {
      console.log(`${asset.id}\t${asset.category}\t${toRepoPath(outputPath(asset))}`);
    }
    return;
  }

  if (command === "help" || command === "--help" || command === "-h") {
    printHelp();
    return;
  }

  throw new Error(`Unknown command: ${command}`);
}

function generateAll() {
  ensureOutputFolders();

  const manifestAssets = [];
  for (const asset of assets) {
    const samples = renderLocalProcedural(asset);
    const wav = encodePcm16Wav(samples, SAMPLE_RATE);
    const target = outputPath(asset);
    mkdirSync(path.dirname(target), { recursive: true });
    writeFileSync(target, wav);
    verifyWavBuffer(wav, asset);

    manifestAssets.push({
      id: asset.id,
      category: asset.category,
      type: asset.type,
      path: toRepoPath(target),
      resourcesPath: resourcesPath(asset),
      durationSeconds: asset.durationSeconds,
      loop: asset.loop,
      usage: asset.usage,
      styleNotes: asset.styleNotes,
      prompt: asset.prompt,
      provider: provider.id,
      sampleRate: SAMPLE_RATE,
      channels: CHANNELS,
      bitsPerSample: BITS_PER_SAMPLE,
      bytes: wav.length,
      sha256: sha256(wav),
    });
  }

  const manifest = {
    schemaVersion: 1,
    generator: {
      name: "panoptes-local-audio-generator",
      version: GENERATOR_VERSION,
      command: "node tools/audio/generate-audio.mjs",
    },
    provider,
    assetRoot: toRepoPath(audioRoot),
    outputRoot: toRepoPath(outputRoot),
    format: "PCM signed 16-bit mono WAV",
    sampleRate: SAMPLE_RATE,
    assets: manifestAssets,
  };

  writeFileSync(manifestPath, `${JSON.stringify(manifest, null, 2)}\n`);
  writeUnityMetaFiles(manifestAssets);
  verifyAll();

  console.log(`Generated ${assets.length} WAV files and manifest at ${toRepoPath(manifestPath)}.`);
}

function verifyAll() {
  const problems = [];

  if (!existsSync(manifestPath)) {
    problems.push(`Missing manifest: ${toRepoPath(manifestPath)}`);
  }

  for (const asset of assets) {
    const target = outputPath(asset);
    if (!existsSync(target)) {
      problems.push(`Missing asset: ${toRepoPath(target)}`);
      continue;
    }

    try {
      verifyWavBuffer(readFileSync(target), asset);
    } catch (error) {
      problems.push(`${toRepoPath(target)}: ${error.message}`);
    }
  }

  if (problems.length > 0) {
    for (const problem of problems) {
      console.error(problem);
    }
    process.exitCode = 1;
    return;
  }

  console.log(`Verified ${assets.length} WAV files and manifest.`);
}

function printHelp() {
  console.log(`Panoptes local audio generator

Usage:
  node tools/audio/generate-audio.mjs          Generate all WAVs and manifest
  node tools/audio/generate-audio.mjs verify   Verify generated WAV headers
  node tools/audio/generate-audio.mjs list     List asset ids and paths

Output:
  client/Assets/Art/Audio/Resources/Audio/
`);
}

function ensureOutputFolders() {
  const folders = new Set([outputRoot]);
  for (const asset of assets) {
    folders.add(path.dirname(outputPath(asset)));
  }

  for (const folder of folders) {
    mkdirSync(folder, { recursive: true });
  }
}

function writeUnityMetaFiles(manifestAssets) {
  const folderPaths = [
    audioRoot,
    path.join(audioRoot, "Resources"),
    outputRoot,
    path.join(outputRoot, "BGM"),
    path.join(outputRoot, "SFX"),
    path.join(outputRoot, "SFX", "UI"),
    path.join(outputRoot, "SFX", "Attack"),
  ];

  for (const folder of folderPaths) {
    writeFileSync(`${folder}.meta`, unityFolderMeta(toRepoPath(folder)));
  }

  for (const entry of manifestAssets) {
    const absolutePath = path.join(repoRoot, ...entry.path.split("/"));
    writeFileSync(`${absolutePath}.meta`, unityAudioMeta(entry.path, entry.category));
  }

  writeFileSync(`${manifestPath}.meta`, unityTextMeta(toRepoPath(manifestPath)));
}

function outputPath(asset) {
  return path.join(outputRoot, ...asset.file.split("/"));
}

function resourcesPath(asset) {
  const parsed = path.parse(asset.file);
  return `Audio/${path.join(parsed.dir, parsed.name).split(path.sep).join("/")}`;
}

function renderLocalProcedural(asset) {
  const count = Math.floor(asset.durationSeconds * SAMPLE_RATE);
  const samples = new Float32Array(count);
  const random = seededRandom(asset.patch.seed);

  if (asset.patch.kind === "bgm") {
    renderBgm(samples, asset.patch, random);
    makeLoopSeamless(samples, Math.min(0.5, asset.durationSeconds / 8));
  } else if (asset.patch.kind === "ui") {
    renderUiClick(samples, asset.patch, random);
  } else if (asset.patch.kind === "attack") {
    renderAttack(samples, asset.patch, random);
  } else {
    throw new Error(`Unknown patch kind for ${asset.id}: ${asset.patch.kind}`);
  }

  removeDc(samples);
  normalize(samples, asset.patch.kind === "bgm" ? 0.72 : 0.86);
  return samples;
}

function renderBgm(samples, patch, random) {
  const beatSeconds = 60 / patch.tempo;
  const phraseSeconds = 8 * beatSeconds;
  const colorGain = {
    dark: [0.36, 0.2, 0.11],
    warm: [0.3, 0.24, 0.14],
    martial: [0.38, 0.18, 0.16],
    rustic: [0.28, 0.22, 0.16],
    tense: [0.34, 0.18, 0.12],
    battle: [0.44, 0.22, 0.18],
  }[patch.color] ?? [0.34, 0.2, 0.12];

  for (let i = 0; i < samples.length; i += 1) {
    const t = i / SAMPLE_RATE;
    const phrase = (t % phraseSeconds) / phraseSeconds;
    const slowBreath = 0.82 + 0.18 * sine(0.25, t);
    const pulse = Math.pow(0.5 + 0.5 * sine(patch.pulseHz, t), 3);
    let value = 0;

    for (let h = 0; h < patch.chordMultipliers.length; h += 1) {
      const hz = patch.baseHz * patch.chordMultipliers[h];
      const detune = 1 + (h - 2) * 0.002;
      const gain = colorGain[h % colorGain.length] / (h + 1);
      value += gain * sine(hz * detune, t);
      value += gain * 0.36 * triangle(hz * 0.5, t + h * 0.013);
    }

    const accentPhase = (t % (beatSeconds * 2)) / (beatSeconds * 2);
    const accentEnv = expDecay(accentPhase, 16);
    const accentHz = patch.bellHz * (phrase < 0.5 ? 1 : 1.25);
    value += 0.16 * accentEnv * sine(accentHz, t);
    value += 0.06 * accentEnv * sine(accentHz * 2, t);

    if (patch.color === "martial" || patch.color === "battle") {
      const drumPhase = (t % beatSeconds) / beatSeconds;
      const drumEnv = expDecay(drumPhase, 22);
      value += (patch.color === "battle" ? 0.34 : 0.24) * drumEnv * sine(62, t);
      value += (patch.color === "battle" ? 0.1 : 0.06) * drumEnv * filteredNoise(random, 0.25);
    } else if (patch.color === "rustic") {
      const pluckPhase = (t % (beatSeconds * 0.5)) / (beatSeconds * 0.5);
      const pluckEnv = expDecay(pluckPhase, 18);
      value += 0.14 * pluckEnv * triangle(patch.bellHz * (phrase < 0.5 ? 1 : 1.125), t);
      value += 0.025 * pulse * filteredNoise(random, 0.05);
    } else if (patch.color === "tense") {
      const bowPulse = Math.pow(0.5 + 0.5 * sine(patch.pulseHz * 0.5, t), 2);
      value += 0.08 * bowPulse * sine(patch.baseHz * 0.5, t);
      value += 0.04 * pulse * filteredNoise(random, 0.08);
    } else {
      value += 0.035 * pulse * filteredNoise(random, 0.08);
    }

    samples[i] = value * slowBreath;
  }
}

function renderUiClick(samples, patch, random) {
  for (let i = 0; i < samples.length; i += 1) {
    const t = i / SAMPLE_RATE;
    const p = i / samples.length;
    const bodyEnv = Math.exp(-p * (patch.muted ? 20 : 28));
    const tickEnv = Math.exp(-p * 70);
    const bend = patch.falling ? 1 - p * 0.42 : 1 + (patch.confirm ? p * 0.35 : 0);
    let value = 0;

    value += 0.56 * tickEnv * sine(patch.toneHz * bend, t);
    value += 0.3 * bodyEnv * sine(patch.bodyHz * bend, t);
    value += patch.noise * tickEnv * white(random);

    if (patch.confirm && t > 0.06) {
      const local = (t - 0.06) / Math.max(0.001, samples.length / SAMPLE_RATE - 0.06);
      value += 0.18 * Math.exp(-local * 18) * sine(patch.toneHz * 1.5, t);
    }

    if (patch.muted) {
      value = lowpassShape(value, 0.62);
    }

    samples[i] = value;
  }
}

function renderAttack(samples, patch, random) {
  for (let i = 0; i < samples.length; i += 1) {
    const t = i / SAMPLE_RATE;
    const p = i / samples.length;
    let value = 0;

    if (patch.profile === "blade") {
      const scrape = expDecay(p, 8) * bandNoise(random, i, 9);
      const hit = expDecay(Math.max(0, p - 0.05), 16) * sine(135, t);
      value += 0.42 * scrape + 0.34 * hit + 0.12 * expDecay(p, 20) * sine(2100, t);
    } else if (patch.profile === "arrow") {
      const whistleHz = 1600 - p * 900;
      const impact = p > 0.36 ? expDecay(p - 0.36, 25) : 0;
      value += 0.22 * (1 - p) * sine(whistleHz, t);
      value += 0.5 * impact * bandNoise(random, i, 5);
      value += 0.24 * impact * sine(180, t);
    } else if (patch.profile === "siege") {
      const thud = expDecay(p, 9) * sine(48, t) + expDecay(p, 12) * sine(86, t);
      const rubble = p > 0.12 ? expDecay(p - 0.12, 5) * bandNoise(random, i, 13) : 0;
      value += 0.58 * thud + 0.28 * rubble;
    } else if (patch.profile === "charge") {
      const rush = p < 0.42 ? (p / 0.42) * bandNoise(random, i, 7) : 0;
      const hit = p > 0.38 ? expDecay(p - 0.38, 18) : 0;
      value += 0.26 * rush;
      value += 0.48 * hit * sine(92, t);
      value += 0.24 * hit * bandNoise(random, i, 3);
    } else {
      throw new Error(`Unknown attack profile: ${patch.profile}`);
    }

    samples[i] = value;
  }
}

function encodePcm16Wav(samples, sampleRate) {
  const dataSize = samples.length * 2;
  const buffer = Buffer.alloc(44 + dataSize);
  buffer.write("RIFF", 0, "ascii");
  buffer.writeUInt32LE(36 + dataSize, 4);
  buffer.write("WAVE", 8, "ascii");
  buffer.write("fmt ", 12, "ascii");
  buffer.writeUInt32LE(16, 16);
  buffer.writeUInt16LE(1, 20);
  buffer.writeUInt16LE(CHANNELS, 22);
  buffer.writeUInt32LE(sampleRate, 24);
  buffer.writeUInt32LE(sampleRate * CHANNELS * (BITS_PER_SAMPLE / 8), 28);
  buffer.writeUInt16LE(CHANNELS * (BITS_PER_SAMPLE / 8), 32);
  buffer.writeUInt16LE(BITS_PER_SAMPLE, 34);
  buffer.write("data", 36, "ascii");
  buffer.writeUInt32LE(dataSize, 40);

  for (let i = 0; i < samples.length; i += 1) {
    const clipped = Math.max(-1, Math.min(1, samples[i]));
    const intValue = clipped < 0 ? clipped * 32768 : clipped * 32767;
    buffer.writeInt16LE(Math.round(intValue), 44 + i * 2);
  }

  return buffer;
}

function verifyWavBuffer(buffer, asset) {
  if (buffer.length < 44) {
    throw new Error("WAV is shorter than the PCM header.");
  }

  const riff = buffer.toString("ascii", 0, 4);
  const wave = buffer.toString("ascii", 8, 12);
  const fmt = buffer.toString("ascii", 12, 16);
  const data = buffer.toString("ascii", 36, 40);
  const audioFormat = buffer.readUInt16LE(20);
  const channels = buffer.readUInt16LE(22);
  const sampleRate = buffer.readUInt32LE(24);
  const bitsPerSample = buffer.readUInt16LE(34);
  const dataSize = buffer.readUInt32LE(40);
  const expectedSamples = Math.floor(asset.durationSeconds * SAMPLE_RATE);

  if (riff !== "RIFF" || wave !== "WAVE" || fmt !== "fmt " || data !== "data") {
    throw new Error("Invalid RIFF/WAVE PCM header.");
  }
  if (audioFormat !== 1) {
    throw new Error(`Expected PCM format 1, got ${audioFormat}.`);
  }
  if (channels !== CHANNELS) {
    throw new Error(`Expected ${CHANNELS} channel, got ${channels}.`);
  }
  if (sampleRate !== SAMPLE_RATE) {
    throw new Error(`Expected sample rate ${SAMPLE_RATE}, got ${sampleRate}.`);
  }
  if (bitsPerSample !== BITS_PER_SAMPLE) {
    throw new Error(`Expected ${BITS_PER_SAMPLE} bits, got ${bitsPerSample}.`);
  }
  if (dataSize !== expectedSamples * 2) {
    throw new Error(`Expected ${expectedSamples * 2} bytes of sample data, got ${dataSize}.`);
  }
  if (buffer.length !== 44 + dataSize) {
    throw new Error(`Expected total length ${44 + dataSize}, got ${buffer.length}.`);
  }
}

function makeLoopSeamless(samples, seconds) {
  const size = Math.min(samples.length, Math.floor(seconds * SAMPLE_RATE));
  if (size <= 1) {
    return;
  }

  for (let i = 0; i < size; i += 1) {
    const endIndex = samples.length - size + i;
    const alpha = (i + 1) / size;
    samples[endIndex] = samples[endIndex] * (1 - alpha) + samples[i] * alpha;
  }
  samples[samples.length - 1] = samples[0];
}

function removeDc(samples) {
  let sum = 0;
  for (const sample of samples) {
    sum += sample;
  }
  const dc = sum / samples.length;
  for (let i = 0; i < samples.length; i += 1) {
    samples[i] -= dc;
  }
}

function normalize(samples, targetPeak) {
  let peak = 0;
  for (const sample of samples) {
    peak = Math.max(peak, Math.abs(sample));
  }
  if (peak <= 0.000001) {
    return;
  }
  const gain = targetPeak / peak;
  for (let i = 0; i < samples.length; i += 1) {
    samples[i] *= gain;
  }
}

function seededRandom(seedText) {
  let seed = 0x811c9dc5;
  for (let i = 0; i < seedText.length; i += 1) {
    seed ^= seedText.charCodeAt(i);
    seed = Math.imul(seed, 0x01000193);
  }

  return () => {
    seed += 0x6d2b79f5;
    let value = seed;
    value = Math.imul(value ^ (value >>> 15), value | 1);
    value ^= value + Math.imul(value ^ (value >>> 7), value | 61);
    return ((value ^ (value >>> 14)) >>> 0) / 4294967296;
  };
}

function sine(hz, t) {
  return Math.sin(Math.PI * 2 * hz * t);
}

function triangle(hz, t) {
  return (2 / Math.PI) * Math.asin(Math.sin(Math.PI * 2 * hz * t));
}

function expDecay(position, speed) {
  return Math.exp(-Math.max(0, position) * speed);
}

function white(random) {
  return random() * 2 - 1;
}

function filteredNoise(random, amount) {
  return white(random) * amount;
}

function bandNoise(random, index, stride) {
  return index % stride === 0 ? white(random) : 0;
}

function lowpassShape(value, factor) {
  return Math.tanh(value * factor) / factor;
}

function sha256(buffer) {
  return createHash("sha256").update(buffer).digest("hex");
}

function unityGuid(repoRelativePath) {
  return createHash("sha1").update(`panoptes-audio:${repoRelativePath}`).digest("hex").slice(0, 32);
}

function unityFolderMeta(repoRelativePath) {
  return `fileFormatVersion: 2
guid: ${unityGuid(repoRelativePath)}
folderAsset: yes
DefaultImporter:
  externalObjects: {}
  userData:
  assetBundleName:
  assetBundleVariant:
`;
}

function unityTextMeta(repoRelativePath) {
  return `fileFormatVersion: 2
guid: ${unityGuid(repoRelativePath)}
TextScriptImporter:
  externalObjects: {}
  userData:
  assetBundleName:
  assetBundleVariant:
`;
}

function unityAudioMeta(repoRelativePath, category) {
  const loadType = category === "bgm" ? 2 : 0;
  const preloadAudioData = category === "bgm" ? 0 : 1;

  return `fileFormatVersion: 2
guid: ${unityGuid(repoRelativePath)}
AudioImporter:
  externalObjects: {}
  serializedVersion: 7
  defaultSettings:
    serializedVersion: 2
    loadType: ${loadType}
    sampleRateSetting: 0
    sampleRateOverride: ${SAMPLE_RATE}
    compressionFormat: 1
    quality: 1
    conversionMode: 0
  platformSettingOverrides: {}
  forceToMono: 0
  normalize: 1
  preloadAudioData: ${preloadAudioData}
  loadInBackground: ${category === "bgm" ? 1 : 0}
  ambisonic: 0
  3D: 1
  userData:
  assetBundleName:
  assetBundleVariant:
`;
}

function toRepoPath(absolutePath) {
  return path.relative(repoRoot, absolutePath).split(path.sep).join("/");
}

main();
