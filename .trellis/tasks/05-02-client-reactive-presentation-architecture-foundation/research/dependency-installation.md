# Client Reactive Dependency Installation Notes

Date: 2026-05-02

## Verified Versions

- VContainer `1.17.0`
- R3 `1.3.0`
- UniTask `2.5.10`

## Unity Package Manager

Use Git URL dependencies for packages that expose Unity package manifests:

- `jp.hadashikick.vcontainer`:
  `https://github.com/hadashiA/VContainer.git?path=VContainer/Assets/VContainer#1.17.0`
- `com.cysharp.unitask`:
  `https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask#2.5.10`
- `com.cysharp.r3`:
  `https://github.com/Cysharp/R3.git?path=src/R3.Unity/Assets/R3.Unity#1.3.0`

## R3 Core DLLs

`R3.Unity` is only the Unity integration package. Its `R3.Unity.asmdef`
precompiled references require these DLL names:

- `R3.dll`
- `Microsoft.Bcl.TimeProvider.dll`
- `Microsoft.Bcl.AsyncInterfaces.dll`

The project imports them under `client/Assets/Plugins/`:

- `R3.dll`: NuGet package `R3` `1.3.0`, `lib/netstandard2.1/R3.dll`
- `Microsoft.Bcl.TimeProvider.dll`: NuGet package
  `Microsoft.Bcl.TimeProvider` `8.0.0`,
  `lib/netstandard2.0/Microsoft.Bcl.TimeProvider.dll`
- `Microsoft.Bcl.AsyncInterfaces.dll`: NuGet package
  `Microsoft.Bcl.AsyncInterfaces` `8.0.0`,
  `lib/netstandard2.1/Microsoft.Bcl.AsyncInterfaces.dll`
- `System.Threading.Channels.dll`: NuGet package
  `System.Threading.Channels` `8.0.0`,
  `lib/netstandard2.1/System.Threading.Channels.dll`
- `System.Runtime.CompilerServices.Unsafe.dll`: NuGet package
  `System.Runtime.CompilerServices.Unsafe` `6.0.0`,
  `lib/netstandard2.0/System.Runtime.CompilerServices.Unsafe.dll`
- `System.ComponentModel.Annotations.dll`: NuGet package
  `System.ComponentModel.Annotations` `5.0.0`,
  `lib/netstandard2.1/System.ComponentModel.Annotations.dll`

SHA-256 after import:

- `R3.dll`:
  `48af716f8ace2fd771241de7e3ebd9ee54bffeafeed14a1ddd69df4ac61226db`
- `Microsoft.Bcl.TimeProvider.dll`:
  `d9941b9603506c46e2aaf8b44166c93646b55ebea0d365792b405c2562753442`
- `Microsoft.Bcl.AsyncInterfaces.dll`:
  `136d5965cf4768e8420b547a8bddea882921f426c371833d558a858a2f0c235a`
- `System.Threading.Channels.dll`:
  `31c7e3704c0477c53d9306362dc6abe741088efb7a7b4e46cded0169cf7bb0b2`
- `System.Runtime.CompilerServices.Unsafe.dll`:
  `01748200f2400c742aa689f1f5101bd6298efdfd92c00c18f4fa473847235ba9`
- `System.ComponentModel.Annotations.dll`:
  `c46069e29ad23fee7f5d2ced736ffea27b0177f82341296d9af6325acf821c69`

## Validation

Unity `6000.4.1f1` batchmode compile passes with this installation shape.
