using System.Runtime.CompilerServices;
// Exposes editor internals to the test assembly so preview and thumbnail behavior can be validated safely through unit tests.

[assembly: InternalsVisibleTo("ParticleThumbnailAndPreview.Editor.Tests")]
