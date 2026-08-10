# Native AOT ingestion smoke test

`fixture.binlog` is a checked-in build log for `fixture.proj`. The smoke test
runs the RID-specific Native AOT tool package against it and verifies that the
console exporter reports the `AotSmoke` target and
`Message: Native AOT binlog ingestion fixture` task spans.

The fixture is not regenerated in CI. To update it intentionally, run the
following command with the next unused numeric binlog name, then replace
`fixture.binlog` with that file:

```powershell
dotnet msbuild tests/NativeAotSmoke/fixture.proj /t:AotSmoke '/bl:<N>.binlog;ProjectImports=None'
```
