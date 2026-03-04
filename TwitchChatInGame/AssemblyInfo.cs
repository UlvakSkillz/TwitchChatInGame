using MelonLoader;
using System.Reflection;
using BuildInfo = TwitchChatInGame.BuildInfo;

[assembly: MelonInfo(typeof(TwitchChatInGame.Main), BuildInfo.ModName, BuildInfo.ModVersion, BuildInfo.Author)]
[assembly: MelonGame("Buckethead Entertainment", "RUMBLE")]
[assembly: MelonColor(255, 195, 0, 255)]
[assembly: MelonAuthorColor(255, 195, 0, 255)]
[assembly: VerifyLoaderVersion(0, 7, 0, true)]

[assembly: AssemblyCopyright("Copyright ©  2025")]
