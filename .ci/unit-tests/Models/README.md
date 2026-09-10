# Test models

No Tekla Structural Designer test model is committed here yet.

Unlike most BHoM toolkits, this one cannot ship a headless test fixture: the Remoting API only
attaches to an already-running instance of Tekla Structural Designer with a model open, so `PullTests.cs`
is marked `[Explicit]` and needs a live TSD 2026 session, not just a file on disk.

To make `PullTests.cs` runnable:

1. Build a small model in Tekla Structural Designer 2026 containing at minimum:
   - A simply supported steel beam under a single gravity load combination (for
     `SimplySupportedBeamShearAndMomentAreSymmetric` and `SolverAndSpanEndRoutesAgree`).
   - At least one load combination solved for `FirstOrderLinear`, and none solved for
     `SecondOrderNonLinear` (for `UnsolvedAnalysisTypeNamesTheUnsolvedCases`).
   - At least two loadcases combined with **different strength and service factors**, so that
     `CombinationFactorSelectsADifferentFactor` can tell the two apart.
   - A **full length UDL applied to that beam**, at a magnitude you have written down, for
     `PullBarLoadsAttachToBarsAndCases` and `UdlMagnitudeMatchesTheModel`. The second of those is the
     test that settles whether applied loads use the same N/mm units as results - see the toolkit
     README - so apply it at a round number and record it in newtons per metre.
2. Save the model somewhere reachable by whoever runs the tests. A `.tsmd` file itself does not need
   to be committed to this repository - only Tekla Structural Designer needs to have it open when the
   tests run.
3. Update `memberObjectId`, `combinationName` and `expectedForcePerMetre` in `PullTests.cs` to match
   real names and the applied UDL magnitude in that model.
4. Run with Tekla Structural Designer open and the model loaded:
   `dotnet test --filter "FullyQualifiedName~PullTests"`

See the toolkit README's "Known Versions of Software Supported" section for which TSD version and
API package this toolkit is pinned to.
