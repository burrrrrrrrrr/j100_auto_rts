# CsCheck

- Package: CsCheck 2.14.1 (`netstandard2.0` build)
- Author: Anthony Lloyd
- Project: http://github.com/AnthonyLloyd/CsCheck
- License: Apache-2.0 (https://licenses.nuget.org/Apache-2.0)

CsCheck is an established C# property-based (random) testing library inspired by
QuickCheck. It is used by the `MultiplayerRts.Tests` assembly as the project's
property-based testing framework (see design.md, Testing Strategy).

The `netstandard2.0` build is vendored here so it imports cleanly as a Unity managed
plugin without requiring the NuGet toolchain inside the Unity editor. Only the
`MultiplayerRts.Tests` (Editor test) assembly references it; it is marked
`isExplicitlyReferenced` so it does not leak into runtime assemblies.
