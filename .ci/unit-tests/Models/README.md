# Test models

No Tekla Structural Designer test model is committed here yet.

Unlike most BHoM toolkits, this one cannot ship a headless test fixture: the Remoting API only
attaches to an already-running instance of Tekla Structural Designer with a model open, so `PullTests.cs`
is marked `[Explicit]` and needs a live TSD 2024 session, not just a file on disk.

To make `PullTests.cs` runnable:

1. Build a small model in Tekla Structural Designer 2024 containing at minimum:
   - A simply supported steel beam under a single gravity load combination (for
     `SimplySupportedBeamShearAndMomentAreSymmetric` and `SolverAndSpanEndRoutesAgree`).
   - At least one load combination solved for `FirstOrderLinear`, and none solved for
     `SecondOrderNonLinear` (for `UnsolvedAnalysisTypeNamesTheUnsolvedCases`).
2. Save the model somewhere reachable by whoever runs the tests. A `.tsmd` file itself does not need
   to be committed to this repository - only Tekla Structural Designer needs to have it open when the
   tests run.
3. Update `memberObjectId` and `combinationName` in `PullTests.cs` to match real names in that model.
4. Run with Tekla Structural Designer open and the model loaded:
   `dotnet test --filter "FullyQualifiedName~PullTests"`

See the toolkit README's "Known Versions of Software Supported" section for which TSD version and
API package this toolkit is pinned to.
