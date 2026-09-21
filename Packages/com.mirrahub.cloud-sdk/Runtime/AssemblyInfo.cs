using System.Runtime.CompilerServices;

// Edit-mode tests pin decisions the client makes internally (which 401/403 refreshes the session, how the error
// envelope is read) without turning those helpers into public API.
[assembly: InternalsVisibleTo("MirraCloudSDK.Tests")]
