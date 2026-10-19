# Third-party notices

## NAudio.Wasapi

AudioBridge uses NAudio.Wasapi 3.2.0 for WASAPI device enumeration, capture, and playback. NAudio is licensed under the MIT License. Project: https://github.com/naudio/NAudio

## VB-Audio VB-CABLE

VB-CABLE is a separate virtual audio driver made by VB-Audio Software. When building a public release, the build script obtains the current official driver package directly from https://download.vb-audio.com/Download_CABLE/VBCABLE_Driver_Pack45.zip. The released application may embed that official archive to simplify first-run setup. VB-Audio describes VB-CABLE as donationware and permits bundling it alongside an application subject to stated conditions. The app identifies VB-Audio as the provider and links users to https://vb-audio.com/Cable/ where they may donate/pay for a license. See current conditions at https://vb-audio.com/Services/licensing.htm before redistributing this project, especially in a professional context.

AudioBridge does not claim ownership of VB-CABLE and does not modify its installer package.

## RNNoise

The RNNoise library/model is not included in this project. The optional noise control in this version is a small DSP noise gate written for AudioBridge; it is not RNNoise.
