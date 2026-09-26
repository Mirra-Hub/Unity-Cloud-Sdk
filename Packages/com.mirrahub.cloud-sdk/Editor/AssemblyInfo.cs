using System.Runtime.CompilerServices;

// Edit-mode tests pin how the Manager window matches the build target against a platform's types without turning
// those helpers into public API.
[assembly: InternalsVisibleTo("MirraCloudSDK.Tests")]
